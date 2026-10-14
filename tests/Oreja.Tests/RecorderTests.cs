using Oreja.Infrastructure;
using Oreja.Recording;
using Oreja.Tests.Fakes;

namespace Oreja.Tests;

public sealed class RecorderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("oreja-rec-").FullName;
    private readonly FakeBackend _backend = new();
    private readonly FakeClock _clock = new();
    private readonly List<FakeSink> _sinks = [];
    private readonly List<RecorderStatus> _statuses = [];
    private readonly List<string> _finished = [];
    private readonly Recorder _recorder;
    private Func<string, IFlacSink>? _sinkOverride;

    public RecorderTests()
    {
        var paths = new OutputPaths(Path.Combine(_root, "work"), () => Path.Combine(_root, "target"));
        var log = new Log(Path.Combine(_root, "oreja.log"));
        _recorder = new Recorder(_backend, _clock, CreateSink, paths, log, dispatch: action => action(), runPumpThread: false);
        _recorder.StatusChanged += _statuses.Add;
        _recorder.RecordingFinished += _finished.Add;
    }

    public void Dispose()
    {
        _recorder.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private FakeSink Sink => _sinks.Last();

    private IFlacSink CreateSink(string path)
    {
        if (_sinkOverride is not null) return _sinkOverride(path);
        File.WriteAllBytes(path, []);
        var sink = new FakeSink();
        _sinks.Add(sink);
        return sink;
    }

    /// <summary>Simuliert Echtzeit in 50-ms-Takten: optional Daten einspeisen, Uhr vorstellen, ein Mixer-Takt.</summary>
    private void Advance(int milliseconds, Action? perStep = null)
    {
        for (int t = 0; t < milliseconds; t += 50)
        {
            perStep?.Invoke();
            _clock.Advance(TimeSpan.FromMilliseconds(50));
            _recorder.Pump();
        }
    }

    private void MicAndSpeaker()
    {
        _backend.DefaultCapture = "mic";
        _backend.RenderDevices.Add("spk");
    }

    [Fact]
    public void StartWithMicrophoneAndSpeakerRecordsWithoutDegradation()
    {
        MicAndSpeaker();

        _recorder.Start();

        Assert.Equal(RecorderState.Recording, _recorder.Status.State);
        Assert.Equal(Degradation.None, _recorder.Status.Degradation);
        Assert.NotNull(_recorder.Status.StartedAt);
        Assert.Equal(2, _backend.Created.Count);
        Assert.All(_backend.Created, s => Assert.True(s.Started));
        Assert.Equal(_recorder.Status, _statuses.Last());
    }

    [Fact]
    public void StartWithoutAnyDeviceFailsAndLeavesNoFile()
    {
        _recorder.Start();

        Assert.Equal(RecorderState.Error, _recorder.Status.State);
        Assert.Equal("Kein Audiogerät verfügbar", _recorder.Status.ErrorMessage);
        Assert.True(Sink.Disposed);
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "work"), "*.flac"));
    }

    [Fact]
    public void StartWithoutMicrophoneIsDegraded()
    {
        _backend.RenderDevices.Add("spk");

        _recorder.Start();

        Assert.Equal(RecorderState.Recording, _recorder.Status.State);
        Assert.Equal(Degradation.NoMicrophone, _recorder.Status.Degradation);
    }

    [Fact]
    public void StartWithoutPlaybackDeviceIsDegraded()
    {
        _backend.DefaultCapture = "mic";

        _recorder.Start();

        Assert.Equal(Degradation.NoSystemAudio, _recorder.Status.Degradation);
    }

    [Fact]
    public void BrokenDeviceIsSkipped()
    {
        _backend.DefaultCapture = "mic";
        _backend.RenderDevices.Add("bad");
        _backend.BrokenDevices.Add("bad");

        _recorder.Start();

        Assert.Equal(RecorderState.Recording, _recorder.Status.State);
        Assert.Equal(Degradation.NoSystemAudio, _recorder.Status.Degradation);
        Assert.Single(_backend.Created);
    }

    [Fact]
    public void StartWhileRecordingIsIgnored()
    {
        _backend.DefaultCapture = "mic";

        _recorder.Start();
        _recorder.Start();

        Assert.Single(_sinks);
    }

    [Fact]
    public void MicrophoneAndSystemAudioAreMixedIntoTheFile()
    {
        MicAndSpeaker();
        _recorder.Start();

        Advance(1000, () =>
        {
            _backend.Live("mic").Feed(0.25f, 800);
            _backend.Live("spk").Feed(0.25f, 800);
        });
        _recorder.Stop();

        // 1 s fällige Samples plus 150 ms, die beim Stoppen noch im Puffer lagen
        Assert.Equal(18400, Sink.Samples.Count);
        Assert.All(Sink.Samples.Take(2400), s => Assert.Equal(0, s));
        Assert.All(Sink.Samples.Skip(2400), s => Assert.Equal(16384, s));
    }

    [Fact]
    public void StopCompletesFileDisposesSourcesAndReportsFinishedPath()
    {
        MicAndSpeaker();
        _recorder.Start();
        Advance(200);

        _recorder.Stop();

        Assert.Equal(RecorderStatus.Idle, _recorder.Status);
        Assert.Equal(RecorderStatus.Idle, _statuses.Last());
        Assert.True(Sink.Completed);
        Assert.True(Sink.Disposed);
        Assert.All(_backend.Created, s => Assert.True(s.Disposed));
        var path = Assert.Single(_finished);
        Assert.StartsWith(Path.Combine(_root, "work"), path);
    }

    [Fact]
    public void PlaybackDeviceAddedDuringRecordingIsCaptured()
    {
        _backend.DefaultCapture = "mic";
        _recorder.Start();
        Assert.Equal(Degradation.NoSystemAudio, _recorder.Status.Degradation);

        _backend.ActivateRender("headset");

        Assert.True(_backend.Live("headset").Started);
        Assert.Equal(SourceKind.Loopback, _backend.Live("headset").Kind);
        Assert.Equal(Degradation.None, _recorder.Status.Degradation);
        Assert.Equal(Degradation.None, _statuses.Last().Degradation);
    }

    [Fact]
    public void RepeatedActivationOfSameDeviceCreatesOneSource()
    {
        MicAndSpeaker();
        _recorder.Start();

        _backend.ActivateRender("spk");
        _backend.ActivateRender("spk");

        Assert.Single(_backend.Created, s => s.DeviceId == "spk");
    }

    [Fact]
    public void RemovedPlaybackDeviceIsDropped()
    {
        MicAndSpeaker();
        _recorder.Start();

        _backend.Deactivate("spk");

        Assert.True(_backend.Created.Single(s => s.DeviceId == "spk").Disposed);
        Assert.Equal(RecorderState.Recording, _recorder.Status.State);
        Assert.Equal(Degradation.NoSystemAudio, _recorder.Status.Degradation);
    }

    [Fact]
    public void DefaultMicrophoneChangeSwitchesMicrophone()
    {
        MicAndSpeaker();
        _recorder.Start();

        _backend.ChangeDefaultCapture("mic2");

        Assert.True(_backend.Created.Single(s => s.DeviceId == "mic").Disposed);
        Assert.True(_backend.Live("mic2").Started);
        Assert.Equal(SourceKind.Microphone, _backend.Live("mic2").Kind);
        Assert.Equal(Degradation.None, _recorder.Status.Degradation);
    }

    [Fact]
    public void LosingTheOnlyMicrophoneIsDegraded()
    {
        MicAndSpeaker();
        _recorder.Start();

        _backend.ChangeDefaultCapture(null);

        Assert.Equal(Degradation.NoMicrophone, _recorder.Status.Degradation);
    }

    [Fact]
    public void FailedSourceIsRemoved()
    {
        MicAndSpeaker();
        _recorder.Start();

        _backend.Live("spk").Fail(new InvalidOperationException("Gerät getrennt"));

        Assert.True(_backend.Created.Single(s => s.DeviceId == "spk").Disposed);
        Assert.Equal(Degradation.NoSystemAudio, _recorder.Status.Degradation);
    }

    [Fact]
    public void LosingAllSourcesKeepsRecording()
    {
        MicAndSpeaker();
        _recorder.Start();

        _backend.Deactivate("mic");
        _backend.Deactivate("spk");

        Assert.Equal(RecorderState.Recording, _recorder.Status.State);
        Assert.Equal(Degradation.NoMicrophone | Degradation.NoSystemAudio, _recorder.Status.Degradation);
    }

    [Fact]
    public void SilentMicrophoneIsReportedAndClearsWithSignal()
    {
        MicAndSpeaker();
        _recorder.Start();

        Advance(3000, () => _backend.Live("mic").Feed(0f, 800));
        Assert.Equal(Degradation.MicrophoneSilent, _recorder.Status.Degradation);

        Advance(100, () => _backend.Live("mic").Feed(0.01f, 800));
        Assert.Equal(Degradation.None, _recorder.Status.Degradation);
    }

    [Fact]
    public void WriteErrorStopsRecordingWithErrorAndKeepsFile()
    {
        MicAndSpeaker();
        _recorder.Start();
        Sink.ThrowOnWrite = new IOException("Datenträger voll");

        Advance(50);

        Assert.Equal(RecorderState.Error, _recorder.Status.State);
        Assert.Equal("Schreibfehler", _recorder.Status.ErrorMessage);
        Assert.All(_backend.Created, s => Assert.True(s.Disposed));
        Assert.True(Sink.Disposed);
        Assert.Single(_finished);
    }

    [Fact]
    public void SinkCreationFailureIsReportedAsWriteError()
    {
        MicAndSpeaker();
        _sinkOverride = _ => throw new IOException("kein Zugriff");

        _recorder.Start();

        Assert.Equal(RecorderState.Error, _recorder.Status.State);
        Assert.Equal("Schreibfehler", _recorder.Status.ErrorMessage);
        Assert.Empty(_backend.Created);
    }

    [Fact]
    public void DeviceEventsAfterStopAreIgnored()
    {
        _backend.DefaultCapture = "mic";
        _recorder.Start();
        _recorder.Stop();

        _backend.ActivateRender("spk");

        Assert.DoesNotContain(_backend.Created, s => s.DeviceId == "spk");
        Assert.Equal(RecorderState.Idle, _recorder.Status.State);
    }

    [Fact]
    public void StartStopStartProducesDistinctFiles()
    {
        _backend.DefaultCapture = "mic";

        _recorder.Start();
        _recorder.Stop();
        _recorder.Start();
        _recorder.Stop();

        Assert.Equal(2, _finished.Distinct().Count());
    }
}
