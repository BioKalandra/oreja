namespace Oreja.Recording;

/// <summary>Geräteliste, Gerätebenachrichtigungen und Erzeugen von Quellen (WASAPI im Betrieb, Fakes im Test).</summary>
public interface IAudioBackend : IDisposable
{
    IReadOnlyList<string> GetActiveRenderDeviceIds();
    string? GetDefaultCaptureDeviceId();
    IAudioSource CreateSource(string deviceId, SourceKind kind, SampleFifo fifo, SampleObserver? observer);

    event Action<string>? RenderDeviceActivated;
    event Action<string>? DeviceDeactivated;
    event Action<string?>? DefaultCaptureDeviceChanged;
}
