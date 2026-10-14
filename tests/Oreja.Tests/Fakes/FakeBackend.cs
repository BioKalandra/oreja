using Oreja.Recording;

namespace Oreja.Tests.Fakes;

public sealed class FakeBackend : IAudioBackend
{
    public List<string> RenderDevices { get; } = [];
    public string? DefaultCapture { get; set; }
    public HashSet<string> BrokenDevices { get; } = [];
    public List<FakeSource> Created { get; } = [];

    public event Action<string>? RenderDeviceActivated;
    public event Action<string>? DeviceDeactivated;
    public event Action<string?>? DefaultCaptureDeviceChanged;

    public IReadOnlyList<string> GetActiveRenderDeviceIds() => RenderDevices.ToList();

    public string? GetDefaultCaptureDeviceId() => DefaultCapture;

    public IAudioSource CreateSource(string deviceId, SourceKind kind, SampleFifo fifo, SampleObserver? observer)
    {
        if (BrokenDevices.Contains(deviceId)) throw new InvalidOperationException($"Gerät {deviceId} defekt");
        var source = new FakeSource(deviceId, kind, fifo, observer);
        Created.Add(source);
        return source;
    }

    /// <summary>Die neueste, noch nicht beendete Quelle für dieses Gerät.</summary>
    public FakeSource Live(string deviceId) => Created.Last(s => s.DeviceId == deviceId && !s.Disposed);

    public void ActivateRender(string id) => RenderDeviceActivated?.Invoke(id);

    public void Deactivate(string id) => DeviceDeactivated?.Invoke(id);

    public void ChangeDefaultCapture(string? id) => DefaultCaptureDeviceChanged?.Invoke(id);

    public void Dispose()
    {
    }
}
