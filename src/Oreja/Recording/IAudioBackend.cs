namespace Oreja.Recording;

/// <summary>Geräteliste, Gerätebenachrichtigungen und Erzeugen von Quellen (WASAPI im Betrieb, Fakes im Test).</summary>
public interface IAudioBackend : IDisposable
{
    IReadOnlyList<string> GetActiveRenderDeviceIds();
    string? GetDefaultCaptureDeviceId();
    /// <summary>Erzeugt eine Quelle, die auf <paramref name="sampleRate"/> umgerechnet in <paramref name="fifo"/> schreibt.</summary>
    IAudioSource CreateSource(string deviceId, SourceKind kind, int sampleRate, SampleFifo fifo, SampleObserver? observer);

    event Action<string>? RenderDeviceActivated;
    event Action<string>? DeviceDeactivated;
    event Action<string?>? DefaultCaptureDeviceChanged;
}
