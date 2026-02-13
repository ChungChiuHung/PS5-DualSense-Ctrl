using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using DualSenseHaptics;
using NAudio.CoreAudioApi;
using HidSharp;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options => options.AddPolicy("AllowAll", 
    p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors("AllowAll");

HapticEngine engine = new HapticEngine();
MMDevice? controller = null;
float currentGain = 1.5f;
double currentFilter = 60.0;
double currentGenFreq = 25.0;
bool isTestMode = false;

// --- HID 實時按鈕監測與控制邏輯 ---
ConcurrentBag<string> activeButtons = new ConcurrentBag<string>();
const int VendorId = 0x054C; // Sony
const int ProductId = 0x0CE6; // DualSense

// 修正 CS4014 警告：使用 _ = 丟棄變數來明確表示我們不需要等待此任務
_ = Task.Run(() => {
    bool crossWasPressed = false;
    bool circleWasPressed = false;

    while (true) {
        try {
            var loader = DeviceList.Local;
            var hidDevice = loader.GetHidDeviceOrNull(VendorId, ProductId);
            
            if (hidDevice != null && hidDevice.TryOpen(out HidStream stream)) {
                Console.WriteLine($"[HID] 已連接到 DualSense 控制器，現在可以使用 Cross/Circle 調整頻率。");
                using (stream) {
                    byte[] buffer = new byte[hidDevice.GetMaxInputReportLength()];
                    while (true) {
                        int count = stream.Read(buffer);
                        if (count > 0) {
                            var pressed = new List<string>();
                            
                            // 解析 DualSense 標準 USB 報告 (Report 0x01)
                            // Byte 8: 幾何按鈕
                            byte b8 = buffer[8];
                            bool crossIsPressed = (b8 & 0x20) != 0;
                            bool circleIsPressed = (b8 & 0x40) != 0;

                            if ((b8 & 0x10) != 0) pressed.Add("Square");
                            if (crossIsPressed) pressed.Add("Cross");
                            if (circleIsPressed) pressed.Add("Circle");
                            if ((b8 & 0x80) != 0) pressed.Add("Triangle");
                            
                            // 頻率控制邏輯 (邊緣偵測：按下瞬間觸發)
                            if (crossIsPressed && !crossWasPressed) {
                                currentFilter = Math.Min(60.0, currentFilter + 1.0);
                                currentGenFreq = Math.Min(60.0, currentGenFreq + 1.0);
                                engine.SetFilterFrequency(currentFilter);
                                engine.SetTestToneFrequency(currentGenFreq);
                                Console.WriteLine($"[控制] 頻率增加至: {currentFilter}Hz");
                            }
                            if (circleIsPressed && !circleWasPressed) {
                                currentFilter = Math.Max(25.0, currentFilter - 1.0);
                                currentGenFreq = Math.Max(25.0, currentGenFreq - 1.0);
                                engine.SetFilterFrequency(currentFilter);
                                engine.SetTestToneFrequency(currentGenFreq);
                                Console.WriteLine($"[控制] 頻率降低至: {currentFilter}Hz");
                            }
                            crossWasPressed = crossIsPressed;
                            circleWasPressed = circleIsPressed;

                            // Byte 9 & 10 其他按鈕
                            if ((buffer[9] & 0x01) != 0) pressed.Add("L1");
                            if ((buffer[9] & 0x02) != 0) pressed.Add("R1");
                            if ((buffer[9] & 0x04) != 0) pressed.Add("L2");
                            if ((buffer[9] & 0x08) != 0) pressed.Add("R2");
                            if ((buffer[9] & 0x10) != 0) pressed.Add("Share");
                            if ((buffer[9] & 0x20) != 0) pressed.Add("Options");
                            if ((buffer[9] & 0x40) != 0) pressed.Add("L3");
                            if ((buffer[9] & 0x80) != 0) pressed.Add("R3");
                            if ((buffer[10] & 0x01) != 0) pressed.Add("PS");
                            if ((buffer[10] & 0x02) != 0) pressed.Add("Touchpad");

                            // 更新共享狀態
                            activeButtons = new ConcurrentBag<string>(pressed);
                        }
                    }
                }
            }
        } catch (Exception ex) {
            Console.WriteLine($"[HID] 掃描中... {ex.Message}");
        }
        Thread.Sleep(1000); 
    }
});

app.MapGet("/status", () => {
    try {
        controller ??= DeviceManager.GetDualSenseDevice();
        return Results.Ok(new { 
            connected = true,
            device = controller.FriendlyName,
            gain = currentGain,
            filter = currentFilter,
            generatorFreq = currentGenFreq,
            testMode = isTestMode,
            buttons = activeButtons.ToArray() 
        });
    } catch {
        return Results.Ok(new { connected = false, device = "正在搜尋..." });
    }
});

app.MapPost("/start", () => {
    try {
        controller ??= DeviceManager.GetDualSenseDevice();
        engine.Start(controller);
        return Results.Ok(new { status = "Started" });
    } catch (Exception ex) { return Results.BadRequest(ex.Message); }
});

app.MapPost("/stop", () => {
    engine.Stop();
    return Results.Ok(new { status = "Stopped" });
});

app.MapPost("/gain/{value}", (float value) => {
    currentGain = value;
    engine.SetGain(currentGain);
    return Results.Ok();
});

app.MapPost("/frequency/{value}", (double value) => {
    currentFilter = value;
    engine.SetFilterFrequency(value);
    return Results.Ok();
});

app.MapPost("/generate/{value}", (double value) => {
    currentGenFreq = value;
    engine.SetTestToneFrequency(value);
    return Results.Ok();
});

app.MapPost("/test-mode/{state}", (string state) => {
    isTestMode = (state == "on");
    engine.SetMode(isTestMode);
    return Results.Ok(new { testMode = isTestMode });
});

Console.WriteLine("=== DualSense Haptic Bridge 服務已啟動 ===");
Console.WriteLine("按鈕功能: Cross (增加頻率) / Circle (降低頻率)");

app.Run("http://localhost:5182");