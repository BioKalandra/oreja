using Oreja.Infrastructure;

namespace Oreja.Recording;

/// <summary>
/// Führt jeweils eine Aufnahme: verbindet Quellen mit dem Mixer, pumpt den Mixer in den
/// FLAC-Sink, folgt Gerätewechseln und meldet seinen Zustand.
/// Start/Stop nur von Thread-Pool-Threads aufrufen, nie vom UI-Thread (WASAPI braucht MTA).
/// </summary>
public sealed class Recorder : IDisposable
{
    public static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(50);

    private const string WriteError = "Schreibfehler";
    private const string InternalError = "Interner Fehler – siehe Protokoll";
    private const string NoDeviceError = "Kein Audiogerät verfügbar";

    private readonly IAudioBackend _backend;
    private readonly IClock _clock;
    private readonly Func<string, IFlacSink> _createSink;
    private readonly OutputPaths _paths;
    private readonly Log _log;
    private readonly Action<Action> _dispatch;
    private readonly bool _runPumpThread;
    private readonly object _gate = new();
    private RecorderStatus _status = RecorderStatus.Idle;
    private Session? _session;

    public Recorder(IAudioBackend backend, IClock clock, Func<string, IFlacSink> createSink, OutputPaths paths, Log log,
        Action<Action>? dispatch = null, bool runPumpThread = true)
    {
        _backend = backend;
        _clock = clock;
        _createSink = createSink;
        _paths = paths;
        _log = log;
        _dispatch = dispatch ?? DispatchToThreadPool;
        _runPumpThread = runPumpThread;

        // Benachrichtigungen kommen auf fremden Threads; die Arbeit passiert asynchron, damit
        // nie ein Capture- oder COM-Thread auf das Lock wartet.
        _backend.RenderDeviceActivated += id => _dispatch(() => OnRenderDeviceActivated(id));
        _backend.DeviceDeactivated += id => _dispatch(() => OnDeviceDeactivated(id));
        _backend.DefaultCaptureDeviceChanged += id => _dispatch(() => OnDefaultCaptureDeviceChanged(id));
    }

    public RecorderStatus Status { get { lock (_gate) return _status; } }

    /// <summary>Bei jeder Zustandsänderung (beliebiger Thread).</summary>
    public event Action<RecorderStatus>? StatusChanged;

    /// <summary>Wenn eine Aufnahmedatei abgeschlossen ist (Pfad im Arbeitsordner).</summary>
    public event Action<string>? RecordingFinished;

    public void Start()
    {
        RecorderStatus status;
        lock (_gate)
        {
            if (_session is not null) return;
            status = _status = StartLocked();
        }
        StatusChanged?.Invoke(status);
    }

    public void Stop()
    {
        Session? session;
        lock (_gate) session = _session;
        if (session is not null) StopSession(session, errorMessage: null);
    }

    public void Dispose() => Stop();

    /// <summary>Testhaken: ein Mixer-Takt, wie ihn der Pump-Thread ausführt.</summary>
    internal void Pump()
    {
        Session? session;
        lock (_gate) session = _session;
        if (session is not null) PumpOnce(session);
    }

    private static void DispatchToThreadPool(Action action) => Task.Run(action);

    private RecorderStatus StartLocked()
    {
        var startedAt = DateTime.Now;
        string path;
        IFlacSink sink;
        try
        {
            path = _paths.NewRecordingPath(startedAt);
            sink = _createSink(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error("Aufnahmedatei konnte nicht angelegt werden", ex);
            return new RecorderStatus(RecorderState.Error, ErrorMessage: WriteError);
        }

        var session = new Session(path, sink, new AudioMixer(_clock, sink.Write),
            new MicSilenceDetector(_clock, MicSilenceDetector.DefaultThreshold));

        string? micId = null;
        IReadOnlyList<string> renderIds = [];
        try
        {
            micId = _backend.GetDefaultCaptureDeviceId();
            renderIds = _backend.GetActiveRenderDeviceIds();
        }
        catch (Exception ex)
        {
            _log.Error("Audiogeräte konnten nicht aufgelistet werden", ex);
        }

        if (micId is not null) TryAddSource(session, micId, SourceKind.Microphone);
        foreach (var id in renderIds) TryAddSource(session, id, SourceKind.Loopback);

        if (session.Sources.Count == 0)
        {
            sink.Dispose();
            TryDelete(path);
            _log.Error(NoDeviceError);
            return new RecorderStatus(RecorderState.Error, ErrorMessage: NoDeviceError);
        }

        session.Mixer.Start();
        session.MicDetector.Reset();
        _session = session;
        if (_runPumpThread)
        {
            session.PumpThread = new Thread(() => PumpLoop(session)) { IsBackground = true, Name = "Oreja-Mixer" };
            session.PumpThread.Start();
        }

        _log.Info($"Aufnahme gestartet: {path} ({session.Sources.Count} Quellen)");
        return new RecorderStatus(RecorderState.Recording, ComputeDegradation(session), startedAt);
    }

    private void StopSession(Session session, string? errorMessage)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_session, session)) return; // bereits gestoppt
            _session = null; // ab jetzt ignorieren Geräte-Events und Pump-Takte diese Sitzung
        }

        session.StopPump.Set();
        if (session.PumpThread is { } pump && pump != Thread.CurrentThread) pump.Join();

        // Quellen beenden, ihre FIFOs aber im Mixer lassen, damit Flush den Rest noch schreibt.
        foreach (var source in session.Sources.Keys) DisposeQuietly(source);
        session.Sources.Clear();

        try
        {
            session.Mixer.Flush();
            session.Sink.Complete();
        }
        catch (Exception ex)
        {
            _log.Error("Aufnahme konnte nicht sauber abgeschlossen werden", ex);
            errorMessage ??= ErrorMessageFor(ex);
        }
        finally
        {
            session.Sink.Dispose();
            session.StopPump.Dispose();
        }

        var status = errorMessage is null ? RecorderStatus.Idle : new RecorderStatus(RecorderState.Error, ErrorMessage: errorMessage);
        lock (_gate)
        {
            if (_session is null) _status = status; // nur, wenn inzwischen keine neue Aufnahme läuft
            else status = _status;
        }

        _log.Info($"Aufnahme beendet: {session.Path}, Pufferüberläufe: {session.Mixer.OverflowCount}" +
                  (errorMessage is null ? "" : $" ({errorMessage})"));
        StatusChanged?.Invoke(status);
        RecordingFinished?.Invoke(session.Path);
    }

    private void PumpLoop(Session session)
    {
        while (!session.StopPump.Wait(PumpInterval))
        {
            if (!PumpOnce(session)) return;
        }
    }

    private bool PumpOnce(Session session)
    {
        try
        {
            session.Mixer.Tick();
        }
        catch (Exception ex)
        {
            _log.Error("Fehler in der Aufnahme-Pipeline", ex);
            string message = ErrorMessageFor(ex);
            _dispatch(() => StopSession(session, message));
            return false;
        }

        RecorderStatus? changed;
        lock (_gate) changed = ReferenceEquals(_session, session) ? UpdateDegradationLocked(session) : null;
        if (changed is not null) StatusChanged?.Invoke(changed);
        return true;
    }

    private void OnRenderDeviceActivated(string id) => Mutate(session =>
    {
        if (!session.Sources.Keys.Any(s => s.Kind == SourceKind.Loopback && s.DeviceId == id))
            TryAddSource(session, id, SourceKind.Loopback);
    });

    private void OnDeviceDeactivated(string id) => Mutate(session =>
    {
        foreach (var source in session.Sources.Keys.Where(s => s.DeviceId == id).ToList())
            RemoveSource(session, source);
    });

    private void OnDefaultCaptureDeviceChanged(string? id) => Mutate(session =>
    {
        var mic = session.Sources.Keys.FirstOrDefault(s => s.Kind == SourceKind.Microphone);
        if (mic is not null && mic.DeviceId == id) return;
        if (mic is not null) RemoveSource(session, mic);
        if (id is not null) TryAddSource(session, id, SourceKind.Microphone);
    });

    private void OnSourceStopped(IAudioSource source, Exception? error) => Mutate(session =>
    {
        if (!session.Sources.ContainsKey(source)) return;
        _log.Error($"Quelle ausgefallen: {source.Kind} {source.DeviceId}", error);
        RemoveSource(session, source);
    });

    /// <summary>Wendet eine Änderung auf die laufende Sitzung an und meldet den neuen Zustand.</summary>
    private void Mutate(Action<Session> change)
    {
        RecorderStatus? changed;
        lock (_gate)
        {
            if (_session is null) return;
            try
            {
                change(_session);
            }
            catch (Exception ex)
            {
                _log.Error("Gerätewechsel fehlgeschlagen", ex);
            }
            changed = UpdateDegradationLocked(_session);
        }
        if (changed is not null) StatusChanged?.Invoke(changed);
    }

    private bool TryAddSource(Session session, string deviceId, SourceKind kind)
    {
        var fifo = session.Mixer.AddSource();
        IAudioSource? source = null;
        try
        {
            var observer = kind == SourceKind.Microphone ? new SampleObserver(session.MicDetector.Observe) : null;
            source = _backend.CreateSource(deviceId, kind, fifo, observer);
            source.Stopped += (s, error) => _dispatch(() => OnSourceStopped(s, error));
            source.Start();
            session.Sources.Add(source, fifo);
            if (kind == SourceKind.Microphone) session.MicDetector.Reset();
            _log.Info($"Quelle hinzugefügt: {kind} {deviceId}");
            return true;
        }
        catch (Exception ex)
        {
            session.Mixer.RemoveSource(fifo);
            if (source is not null) DisposeQuietly(source);
            _log.Error($"Quelle konnte nicht geöffnet werden: {kind} {deviceId}", ex);
            return false;
        }
    }

    private void RemoveSource(Session session, IAudioSource source)
    {
        if (!session.Sources.Remove(source, out var fifo)) return;
        session.Mixer.RemoveSource(fifo);
        DisposeQuietly(source);
        _log.Info($"Quelle entfernt: {source.Kind} {source.DeviceId}");
    }

    /// <summary>Berechnet die Einschränkung neu; liefert den neuen Status, falls er sich geändert hat.</summary>
    private RecorderStatus? UpdateDegradationLocked(Session session)
    {
        if (_status.State != RecorderState.Recording) return null;
        var degradation = ComputeDegradation(session);
        if (degradation == _status.Degradation) return null;
        _status = _status with { Degradation = degradation };
        _log.Info($"Einschränkung: {degradation}");
        return _status;
    }

    private static Degradation ComputeDegradation(Session session)
    {
        var degradation = Degradation.None;
        if (!session.Sources.Keys.Any(s => s.Kind == SourceKind.Microphone)) degradation |= Degradation.NoMicrophone;
        else if (session.MicDetector.IsSilent) degradation |= Degradation.MicrophoneSilent;
        if (!session.Sources.Keys.Any(s => s.Kind == SourceKind.Loopback)) degradation |= Degradation.NoSystemAudio;
        return degradation;
    }

    private static string ErrorMessageFor(Exception ex) =>
        ex is IOException or UnauthorizedAccessException ? WriteError : InternalError;

    private void DisposeQuietly(IAudioSource source)
    {
        try
        {
            source.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error($"Quelle konnte nicht beendet werden: {source.Kind} {source.DeviceId}", ex);
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error($"{path} konnte nicht gelöscht werden", ex);
        }
    }

    private sealed class Session(string path, IFlacSink sink, AudioMixer mixer, MicSilenceDetector micDetector)
    {
        public string Path { get; } = path;
        public IFlacSink Sink { get; } = sink;
        public AudioMixer Mixer { get; } = mixer;
        public MicSilenceDetector MicDetector { get; } = micDetector;
        public Dictionary<IAudioSource, SampleFifo> Sources { get; } = [];
        public ManualResetEventSlim StopPump { get; } = new();
        public Thread? PumpThread { get; set; }
    }
}
