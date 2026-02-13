using NAudio.Wave;
using NAudio.Dsp;
using NAudio.Wave.SampleProviders;

namespace DualSenseHaptics;

public class HapticSignalProcessor : ISampleProvider
{
    private readonly ISampleProvider _loopbackSource;
    private readonly SignalGenerator _testToneSource;
    private ISampleProvider _currentActiveSource;
    
    private BiQuadFilter _lpFilter;
    private double _currentFreq = 60.0;
    
    private float _gain = 1.5f;
    public float Gain 
    { 
        get => _gain; 
        set => _gain = value; 
    }

    public bool IsTestToneMode { get; private set; } = false;

    public WaveFormat WaveFormat => _loopbackSource.WaveFormat;

    public HapticSignalProcessor(ISampleProvider loopbackSource)
    {
        _loopbackSource = loopbackSource;
        
        // Initialize internal sine generator for manual testing
        _testToneSource = new SignalGenerator(loopbackSource.WaveFormat.SampleRate, 2)
        {
            Type = SignalGeneratorType.Sin,
            Frequency = 25,
            Gain = 0.5 
        };

        _currentActiveSource = _loopbackSource;
        _lpFilter = BiQuadFilter.LowPassFilter(WaveFormat.SampleRate, (float)_currentFreq, 1);
    }

    public void UpdateFilterFrequency(double freq)
    {
        _currentFreq = freq;
        _lpFilter = BiQuadFilter.LowPassFilter(WaveFormat.SampleRate, (float)_currentFreq, 1);
    }

    public void SetTestToneFrequency(double freq)
    {
        _testToneSource.Frequency = freq;
    }

    public void SetMode(bool testToneEnabled)
    {
        IsTestToneMode = testToneEnabled;
        _currentActiveSource = testToneEnabled ? _testToneSource : _loopbackSource;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _currentActiveSource.Read(buffer, offset, count);

        for (int i = 0; i < samplesRead; i++)
        {
            // Apply filter to loopback only; test tones are already pure sine
            if (!IsTestToneMode)
            {
                buffer[offset + i] = _lpFilter.Transform(buffer[offset + i]);
            }
            
            buffer[offset + i] *= _gain;

            // Hard clipping safety
            if (buffer[offset + i] > 1.0f) buffer[offset + i] = 1.0f;
            if (buffer[offset + i] < -1.0f) buffer[offset + i] = -1.0f;
        }

        return samplesRead;
    }
}