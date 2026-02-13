using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.CoreAudioApi;

namespace DualSenseHaptics;

public class HapticEngine
{
    private WasapiLoopbackCapture? _capture;
    private WasapiOut? _output;
    private HapticSignalProcessor? _processor;

    public void Start(MMDevice dualSense)
    {
        _capture = new WasapiLoopbackCapture();
        var targetFormat = dualSense.AudioClient.MixFormat;
        
        var waveProvider = new WaveInProvider(_capture);
        ISampleProvider captureSampleProvider = waveProvider.ToSampleProvider();

        if (captureSampleProvider.WaveFormat.SampleRate != targetFormat.SampleRate)
        {
            captureSampleProvider = new WdlResamplingSampleProvider(captureSampleProvider, targetFormat.SampleRate);
        }

        _processor = new HapticSignalProcessor(captureSampleProvider);
        var finalProvider = new DualSenseByteMapper(_processor, targetFormat);

        _output = new WasapiOut(dualSense, AudioClientShareMode.Shared, true, 20);
        _output.Init(finalProvider);

        _capture.StartRecording();
        _output.Play();
    }

    public void SetGain(float value)
    {
        if (_processor != null) _processor.Gain = value;
    }

    public void SetFilterFrequency(double freq)
    {
        if (_processor != null) _processor.UpdateFilterFrequency(freq);
    }

    public void SetTestToneFrequency(double freq)
    {
        if (_processor != null) _processor.SetTestToneFrequency(freq);
    }

    public void SetMode(bool isTestTone)
    {
        if (_processor != null) _processor.SetMode(isTestTone);
    }

    public void Stop()
    {
        _capture?.StopRecording();
        _output?.Stop();
        _capture?.Dispose();
        _output?.Dispose();
    }
}

public class DualSenseByteMapper : IWaveProvider
{
    private readonly ISampleProvider _source;
    private float[] _sourceBuffer;
    public WaveFormat WaveFormat { get; }

    public DualSenseByteMapper(ISampleProvider source, WaveFormat targetFormat)
    {
        _source = source;
        WaveFormat = targetFormat;
        _sourceBuffer = new float[1024];
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        int bytesPerSample = WaveFormat.BitsPerSample / 8;
        int outChannels = WaveFormat.Channels;
        int bytesPerFrame = bytesPerSample * outChannels;
        int framesRequested = count / bytesPerFrame;
        int sourceChannels = _source.WaveFormat.Channels;
        int sourceSamplesToRead = framesRequested * sourceChannels;

        if (_sourceBuffer.Length < sourceSamplesToRead)
            Array.Resize(ref _sourceBuffer, sourceSamplesToRead);

        int samplesRead = _source.Read(_sourceBuffer, 0, sourceSamplesToRead);
        int framesRead = samplesRead / sourceChannels;

        Array.Clear(buffer, offset, count);

        for (int i = 0; i < framesRead; i++)
        {
            int outFrameOffset = offset + (i * bytesPerFrame);
            int inSampleOffset = i * sourceChannels;

            float leftSample = _sourceBuffer[inSampleOffset];
            float rightSample = _sourceBuffer[inSampleOffset + 1];

            // Map to Channels 2 & 3 (Haptic Actuators)
            WriteFloatToBuffer(buffer, outFrameOffset + (2 * bytesPerSample), leftSample);
            WriteFloatToBuffer(buffer, outFrameOffset + (3 * bytesPerSample), rightSample);
        }

        return framesRead * bytesPerFrame;
    }

    private void WriteFloatToBuffer(byte[] buffer, int offset, float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        for (int i = 0; i < 4; i++) buffer[offset + i] = bytes[i];
    }
}