using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using DualSenseHaptics;
using NAudio.CoreAudioApi;

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

// Mock for button monitor; real implementation would use HID polling
var getMockButtons = () => new string[] { };

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
            buttons = getMockButtons()
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