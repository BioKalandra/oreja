using NAudio.CoreAudioApi;
using Oreja.Infrastructure;

namespace Oreja.Recording;

/// <summary>
/// WASAPI-Zugriff: listet Endpunkte, erzeugt Quellen und übersetzt Windows-Gerätebenachrichtigungen
/// in Recorder-Events. Nur auf MTA-Threads (Thread-Pool) erzeugen und benutzen.
/// </summary>
public sealed class WasapiBackend : IAudioBackend
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly MMDeviceNotificationClient _notifications;
    private readonly Log _log;

    public WasapiBackend(Log log)
    {
        _log = log;
        _notifications = _enumerator.CreateNotificationClient(useSynchronizationContext: false);
        _notifications.DeviceStateChanged += OnDeviceStateChanged;
        _notifications.DeviceRemoved += (_, e) => DeviceDeactivated?.Invoke(e.DeviceId);
        _notifications.DefaultDeviceChanged += OnDefaultDeviceChanged;
    }

    public event Action<string>? RenderDeviceActivated;
    public event Action<string>? DeviceDeactivated;
    public event Action<string?>? DefaultCaptureDeviceChanged;

    public IReadOnlyList<string> GetActiveRenderDeviceIds()
    {
        var ids = new List<string>();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device) ids.Add(device.ID);
        }
        return ids;
    }

    public string? GetDefaultCaptureDeviceId()
    {
        if (!_enumerator.TryGetDefaultAudioEndpoint(DataFlow.Capture, Role.Console, out var device) || device is null) return null;
        using (device) return device.ID;
    }

    public IAudioSource CreateSource(string deviceId, SourceKind kind, SampleFifo fifo, SampleObserver? observer)
    {
        var device = _enumerator.GetDevice(deviceId);
        try
        {
            return new WasapiSource(device, kind, fifo, observer);
        }
        catch
        {
            device.Dispose();
            throw;
        }
    }

    private void OnDeviceStateChanged(object? sender, DeviceStateChangedEventArgs e)
    {
        if (e.NewState != DeviceState.Active)
        {
            DeviceDeactivated?.Invoke(e.DeviceId);
            return;
        }

        // Im Benachrichtigungs-Callback keine Enumerator-Aufrufe: Abfrage auf den Thread-Pool verlagern.
        string id = e.DeviceId;
        Task.Run(() =>
        {
            try
            {
                using var device = _enumerator.GetDevice(id);
                if (device.DataFlow == DataFlow.Render) RenderDeviceActivated?.Invoke(id);
            }
            catch (Exception ex)
            {
                _log.Error($"Gerät {id} konnte nicht abgefragt werden", ex);
            }
        });
    }

    private void OnDefaultDeviceChanged(object? sender, DefaultDeviceChangedEventArgs e)
    {
        if (e.Flow == DataFlow.Capture && e.Role == Role.Console)
            DefaultCaptureDeviceChanged?.Invoke(string.IsNullOrEmpty(e.DeviceId) ? null : e.DeviceId);
    }

    public void Dispose()
    {
        _notifications.Dispose();
        _enumerator.Dispose();
    }
}
