using Oreja.Recording;

namespace Oreja.Tests.Fakes;

public sealed class FakeSource(string deviceId, SourceKind kind, SampleFifo fifo, SampleObserver? observer) : IAudioSource
{
    public string DeviceId { get; } = deviceId;
    public SourceKind Kind { get; } = kind;
    public bool Started { get; private set; }
    public bool Disposed { get; private set; }

    public event Action<IAudioSource, Exception?>? Stopped;

    public void Start() => Started = true;

    public void Dispose() => Disposed = true;

    /// <summary>Simuliert einen Capture-Block mit konstantem Wert.</summary>
    public void Feed(float value, int count)
    {
        var samples = Enumerable.Repeat(value, count).ToArray();
        observer?.Invoke(samples);
        fifo.Write(samples);
    }

    public void Fail(Exception error) => Stopped?.Invoke(this, error);
}
