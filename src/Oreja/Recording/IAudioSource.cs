namespace Oreja.Recording;

public enum SourceKind { Microphone, Loopback }

/// <summary>Erhält jede gewandelte Sample-Folge einer Quelle (Capture-Thread).</summary>
public delegate void SampleObserver(ReadOnlySpan<float> samples);

/// <summary>Laufende Aufnahme eines Geräts, die Mono Float in der Abtastrate der Aufnahme in ihren FIFO schreibt.</summary>
public interface IAudioSource : IDisposable
{
    string DeviceId { get; }
    SourceKind Kind { get; }
    void Start();

    /// <summary>Wird einmal ausgelöst, wenn die Quelle von selbst endet (Gerät weg, Fehler); nie nach Dispose.</summary>
    event Action<IAudioSource, Exception?>? Stopped;
}
