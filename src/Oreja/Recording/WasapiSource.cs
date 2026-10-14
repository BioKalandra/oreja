using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Oreja.Recording;

// NAudio 3.1 markiert WasapiCapture/WasapiLoopbackCapture als veraltet zugunsten von WasapiRecorder.
// Wir bleiben bewusst bei der klassischen, auf diesem Projekt live erprobten API; sie ist weiterhin
// Teil von NAudio 3.x. Ein Umstieg auf WasapiRecorder ist ein eigener Schritt.
#pragma warning disable CS0618

/// <summary>Nimmt einen WASAPI-Endpunkt auf (Mikrofon oder Loopback eines Wiedergabegeräts) und schreibt in den FIFO.</summary>
public sealed class WasapiSource : IAudioSource
{
    private readonly MMDevice _device;
    private readonly WasapiCapture _capture;
    private readonly SampleConverter _converter;
    private readonly SampleFifo _fifo;
    private readonly SampleObserver? _observer;
    private volatile bool _disposed;

    /// <summary>Übernimmt den Besitz von <paramref name="device"/>.</summary>
    public WasapiSource(MMDevice device, SourceKind kind, SampleFifo fifo, SampleObserver? observer)
    {
        _device = device;
        DeviceId = device.ID;
        Kind = kind;
        _fifo = fifo;
        _observer = observer;
        _capture = kind == SourceKind.Loopback ? new WasapiLoopbackCapture(device) : new WasapiCapture(device);
        try
        {
            _converter = new SampleConverter(_capture.WaveFormat);
        }
        catch
        {
            _capture.Dispose();
            throw;
        }
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
    }

    public string DeviceId { get; }
    public SourceKind Kind { get; }

    public event Action<IAudioSource, Exception?>? Stopped;

    public void Start() => _capture.StartRecording();

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var samples = _converter.Convert(e.BufferSpan[..e.BytesRecorded]);
        _observer?.Invoke(samples);
        _fifo.Write(samples);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (!_disposed) Stopped?.Invoke(this, e.Exception);
    }

    public void Dispose()
    {
        _disposed = true;
        _capture.Dispose();
        _device.Dispose();
    }
}
