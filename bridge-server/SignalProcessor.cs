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
        
        // 初始化內部正弦波產生器用於測試音
        _testToneSource = new SignalGenerator(loopbackSource.WaveFormat.SampleRate, 2)
        {
            Type = SignalGeneratorType.Sin,
            Frequency = 25,
            Gain = 0.5 // 產生器的基礎音量
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
        // 從目前啟用的來源讀取
        int samplesRead = _currentActiveSource.Read(buffer, offset, count);

        for (int i = 0; i < samplesRead; i++)
        {
            // 只有系統音訊需要過濾。測試音已經是乾淨的正弦波。
            if (!IsTestToneMode)
            {
                buffer[offset + i] = _lpFilter.Transform(buffer[offset + i]);
            }
            
            buffer[offset + i] *= _gain;

            // 安全限幅
            if (buffer[offset + i] > 1.0f) buffer[offset + i] = 1.0f;
            if (buffer[offset + i] < -1.0f) buffer[offset + i] = -1.0f;
        }

        return samplesRead;
    }
}