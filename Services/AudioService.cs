using NAudio.Wave;

namespace MoodyClone.Services;

public class AudioService : IDisposable
{
    private WaveInEvent? _waveIn;
    private float _currentRms;
    private bool _isMonitoring;

    public event Action<float>? VolumeChanged;

    public float CurrentRms => _currentRms;
    public bool IsMonitoring => _isMonitoring;

    /// <summary>
    /// Returns a list of (deviceIndex, deviceName) for all audio input devices.
    /// </summary>
    public static List<(int Index, string Name)> GetMicrophones()
    {
        var devices = new List<(int, string)>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            devices.Add((i, caps.ProductName));
        }
        return devices;
    }

    public void StartMonitoring(int deviceIndex = 0)
    {
        StopMonitoring();

        try
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceIndex,
                WaveFormat = new WaveFormat(44100, 16, 1),
                BufferMilliseconds = 33 // ~30fps updates
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();
            _isMonitoring = true;
        }
        catch
        {
            _isMonitoring = false;
        }
    }

    public void StopMonitoring()
    {
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            try { _waveIn.StopRecording(); } catch { }
            _waveIn.Dispose();
            _waveIn = null;
        }
        _isMonitoring = false;
        _currentRms = 0;
    }

    public void SwitchDevice(int deviceIndex)
    {
        StartMonitoring(deviceIndex);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        // Calculate RMS amplitude
        double sum = 0;
        int sampleCount = e.BytesRecorded / 2; // 16-bit samples
        for (int i = 0; i < e.BytesRecorded; i += 2)
        {
            short sample = BitConverter.ToInt16(e.Buffer, i);
            double normalized = sample / 32768.0;
            sum += normalized * normalized;
        }

        _currentRms = (float)Math.Sqrt(sum / Math.Max(sampleCount, 1));
        VolumeChanged?.Invoke(_currentRms);
    }

    public void Dispose()
    {
        StopMonitoring();
    }
}
