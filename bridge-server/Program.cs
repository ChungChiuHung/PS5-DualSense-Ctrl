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

// --- HID Real-time Data ---
ConcurrentBag<string> activeButtons = new ConcurrentBag<string>();
float lsX = 0, lsY = 0, rsX = 0, rsY = 0;

const int VendorId = 0x054C; 
const int ProductId = 0x0CE6; 

_ = Task.Run(() => {
    while (true) {
        try {
            var loader = DeviceList.Local;
            var hidDevice = loader.GetHidDeviceOrNull(VendorId, ProductId);
            
            if (hidDevice != null && hidDevice.TryOpen(out HidStream stream)) {
                Console.WriteLine($"[HID] DualSense Connected. Input monitoring and Analog sticks active.");
                using (stream) {
                    byte[] buffer = new byte[hidDevice.GetMaxInputReportLength()];
                    while (true) {
                        int count = stream.Read(buffer);
                        if (count > 0) {
                            var pressed = new List<string>();
                            
                            // --- Analog Sticks (Byte 1-4) ---
                            lsX = (buffer[1] - 128f) / 128f;
                            lsY = (buffer[2] - 128f) / 128f;
                            rsX = (buffer[3] - 128f) / 128f;
                            rsY = (buffer[4] - 128f) / 128f;

                            if (Math.Abs(lsX) < 0.05) lsX = 0;
                            if (Math.Abs(lsY) < 0.05) lsY = 0;
                            if (Math.Abs(rsX) < 0.05) rsX = 0;
                            if (Math.Abs(rsY) < 0.05) rsY = 0;

                            // --- D-Pad & Geometric Buttons (Byte 8) ---
                            byte b8 = buffer[8];
                            byte hat = (byte)(b8 & 0x0F);
                            bool dUp = (hat == 0 || hat == 1 || hat == 7);
                            bool dRight = (hat == 1 || hat == 2 || hat == 3);
                            bool dDown = (hat == 3 || hat == 4 || hat == 5);
                            bool dLeft = (hat == 5 || hat == 6 || hat == 7);

                            if (dUp) pressed.Add("DpadUp");
                            if (dDown) pressed.Add("DpadDown");
                            if (dLeft) pressed.Add("DpadLeft");
                            if (dRight) pressed.Add("DpadRight");

                            if ((b8 & 0x10) != 0) pressed.Add("Square");
                            if ((b8 & 0x20) != 0) pressed.Add("Cross");
                            if ((b8 & 0x40) != 0) pressed.Add("Circle");
                            if ((b8 & 0x80) != 0) pressed.Add("Triangle");

                            // --- Shoulder & Function Buttons (Byte 9) ---
                            byte b9 = buffer[9];
                            if ((b9 & 0x01) != 0) pressed.Add("L1");
                            if ((b9 & 0x02) != 0) pressed.Add("R1");
                            if ((b9 & 0x04) != 0) pressed.Add("L2");
                            if ((b9 & 0x08) != 0) pressed.Add("R2");
                            if ((b9 & 0x10) != 0) pressed.Add("Share");
                            if ((b9 & 0x20) != 0) pressed.Add("Options");
                            if ((b9 & 0x40) != 0) pressed.Add("L3");
                            if ((b9 & 0x80) != 0) pressed.Add("R3");

                            // --- System Buttons (Byte 10) ---
                            if ((buffer[10] & 0x01) != 0) pressed.Add("PS");
                            if ((buffer[10] & 0x02) != 0) pressed.Add("Touchpad");

                            // Note: Control overrides (linking buttons to gain/freq) have been removed.
                            
                            activeButtons = new ConcurrentBag<string>(pressed);
                        }
                    }
                }
            }
        } catch { /* Silent Retry */ }
        Thread.Sleep(10); 
    }
});

app.MapGet("/", () => Results.Text("DualSense Haptic Bridge Running. Access via index.html."));

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
            buttons = activeButtons.ToArray(),
            sticks = new { lsX, lsY, rsX, rsY }
        });
    } catch {
        return Results.Ok(new { connected = false, device = "Searching..." });
    }
});

app.MapPost("/start", () => {
    try {
        controller ??= DeviceManager.GetDualSenseDevice();
        engine.Start(controller);
        engine.SetGain(currentGain);
        engine.SetFilterFrequency(currentFilter);
        engine.SetTestToneFrequency(currentGenFreq);
        engine.SetMode(isTestMode);
        return Results.Ok(new { status = "Started" });
    } catch (Exception ex) { return Results.BadRequest(ex.Message); }
});

app.MapPost("/stop", () => { engine.Stop(); return Results.Ok(); });
app.MapPost("/gain/{value}", (float value) => { currentGain = value; engine.SetGain(value); return Results.Ok(); });
app.MapPost("/frequency/{value}", (double value) => { currentFilter = value; engine.SetFilterFrequency(value); return Results.Ok(); });
app.MapPost("/generate/{value}", (double value) => { currentGenFreq = value; engine.SetTestToneFrequency(value); return Results.Ok(); });
app.MapPost("/test-mode/{state}", (string state) => { isTestMode = (state == "on"); engine.SetMode(isTestMode); return Results.Ok(); });

app.Run("http://localhost:5182");