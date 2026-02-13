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

_ = Task.Run(() => {
    bool crossWasPressed = false;
    bool circleWasPressed = false;
    bool l1WasPressed = false;
    bool l2WasPressed = false;

    while (true) {
        try {
            var loader = DeviceList.Local;
            var hidDevice = loader.GetHidDeviceOrNull(VendorId, ProductId);
            
            if (hidDevice != null && hidDevice.TryOpen(out HidStream stream)) {
                Console.WriteLine($"[HID] DualSense 已連接。控制方式: X/O 調整頻率, L1/L2 調整強度。");
                using (stream) {
                    byte[] buffer = new byte[hidDevice.GetMaxInputReportLength()];
                    while (true) {
                        int count = stream.Read(buffer);
                        if (count > 0) {
                            var pressed = new List<string>();
                            
                            // Byte 8: 幾何按鈕 (Square, Cross, Circle, Triangle)
                            byte b8 = buffer[8];
                            bool crossIsPressed = (b8 & 0x20) != 0;
                            bool circleIsPressed = (b8 & 0x40) != 0;

                            // Byte 9: 功能按鈕 (L1, R1, L2, R2)
                            byte b9 = buffer[9];
                            bool l1IsPressed = (b9 & 0x01) != 0;
                            bool l2IsPressed = (b9 & 0x04) != 0;

                            if ((b8 & 0x10) != 0) pressed.Add("Square");
                            if (crossIsPressed) pressed.Add("Cross");
                            if (circleIsPressed) pressed.Add("Circle");
                            if ((b8 & 0x80) != 0) pressed.Add("Triangle");
                            if (l1IsPressed) pressed.Add("L1");
                            if (l2IsPressed) pressed.Add("L2");

                            // --- 頻率控制 (Cross / Circle) ---
                            if (crossIsPressed && !crossWasPressed) {
                                currentFilter = Math.Min(60.0, currentFilter + 1.0);
                                currentGenFreq = Math.Min(60.0, currentGenFreq + 1.0);
                                engine.SetFilterFrequency(currentFilter);
                                engine.SetTestToneFrequency(currentGenFreq);
                                Console.WriteLine($"[控制] 頻率: {currentFilter}Hz");
                            }
                            if (circleIsPressed && !circleWasPressed) {
                                currentFilter = Math.Max(25.0, currentFilter - 1.0);
                                currentGenFreq = Math.Max(25.0, currentGenFreq - 1.0);
                                engine.SetFilterFrequency(currentFilter);
                                engine.SetTestToneFrequency(currentGenFreq);
                                Console.WriteLine($"[控制] 頻率: {currentFilter}Hz");
                            }

                            // --- 強度控制 (L1 / L2) ---
                            if (l1IsPressed && !l1WasPressed) {
                                currentGain = Math.Max(0.0f, currentGain - 0.1f);
                                engine.SetGain(currentGain);
                                Console.WriteLine($"[控制] 強度(Gain): {currentGain:F1}x");
                            }
                            if (l2IsPressed && !l2WasPressed) {
                                currentGain = Math.Min(5.0f, currentGain + 0.1f);
                                engine.SetGain(currentGain);
                                Console.WriteLine($"[控制] 強度(Gain): {currentGain:F1}x");
                            }

                            // 更新狀態紀錄以便進行邊緣偵測
                            crossWasPressed = crossIsPressed;
                            circleWasPressed = circleIsPressed;
                            l1WasPressed = l1IsPressed;
                            l2WasPressed = l2IsPressed;

                            // 其他按鍵解析 (可選)
                            if ((b9 & 0x02) != 0) pressed.Add("R1");
                            if ((b9 & 0x08) != 0) pressed.Add("R2");
                            if ((b9 & 0x20) != 0) pressed.Add("Options");
                            if ((buffer[10] & 0x01) != 0) pressed.Add("PS");

                            activeButtons = new ConcurrentBag<string>(pressed);
                        }
                    }
                }
            }
        } catch { /* 斷線重試 */ }
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
        return Results.Ok(new { connected = false, device = "Searching..." });
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

app.Run("http://localhost:5182");