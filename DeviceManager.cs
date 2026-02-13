using NAudio.CoreAudioApi;

namespace DualSenseHaptics;

public static class DeviceManager
{
    public static MMDevice GetDualSenseDevice()
    {
        var enumerator = new MMDeviceEnumerator();
        
        // Look for Active Audio Render devices (Speakers)
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        
        foreach (var device in devices)
        {
            // PS5 controllers typically identify as "Wireless Controller" in Windows
            if (device.FriendlyName.Contains("Wireless Controller", StringComparison.OrdinalIgnoreCase))
            {
                // Verify it's in Quadraphonic (4-channel) mode
                if (device.AudioClient.MixFormat.Channels < 4)
                {
                    Console.WriteLine("Warning: DualSense found but NOT in 4-channel mode.");
                    Console.WriteLine("Please go to Sound Settings -> Configure Speakers -> Select Quadraphonic.");
                }
                return device;
            }
        }

        throw new Exception("PS5 DualSense Controller not found. Please connect via USB.");
    }
}