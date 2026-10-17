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
    private readonly List<int> _sinkRates = [];
    private Func<string, int, IFlacSink>? _sinkOverride;
    private int _sampleRate = 16000;
    private int _sampleRateReads;

    public RecorderTests()
    {
        var paths = new OutputPaths(Path.Combine(_root, "work"), () => Path.Combine(_root, "target"));
        var log = new Log(Path.Combine(_root, "oreja.log"));
        _recorder = new Recorder(_backend, _clock, ReadSampleRate, CreateSink, paths, log, dispatch: action => action(), runPumpThread: false);
        _recorder.StatusChanged += _statuses.Add;
        _recorder.RecordingFinished += _finished.Add;
    }

    public void Dispose()
    {
        _recorder.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private FakeSink Sink => _sinks.Last();

    private int ReadSampleRate()
    {
        _sampleRateReads++;
        return _sampleRate;
    }

    private IFlacSink CreateSink(string path, int sampleRate)
    {
        _sinkRates.Add(sampleRate);
        if (_sinkOverride is not null) return _sinkOverride(path, sampleRate);
        File.WriteAllBytes(path, []);
        var sink = new FakeSink(sampleRate);
        _sinks.Add(sink);
        return sink;
    }

    private string ReadLog() => File.ReadAllText(Path.Combine(_root, "oreja.log"));

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
        Assert.Equal(RecorderError.NoDevice, _recorder.Status.Error);
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

    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void MicrophoneAndSystemAudioAreMixedIntoTheFile(int rate)
    {
        _sampleRate = rate;
        MicAndSpeaker();
        _recorder.Start();
        int perStep = rate / 20;

        Advance(1000, () =>
        {
            _backend.Live("mic").Feed(0.25f, perStep);
            _backend.Live("spk").Feed(0.25f, perStep);
        });
        _recorder.Stop();

        // 1 s fällige Samples plus 150 ms, die beim Stoppen noch im Puffer lagen
        Assert.Equal(rate + 3 * perStep, Sink.Samples.Count);
        Assert.All(Sink.Samples.Take(3 * perStep), s => Assert.Equal(0, s));
        Assert.All(Sink.Samples.Skip(3 * perStep), s => Assert.Equal(16384, s));
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
    public void FailedSourceOfVanishedDeviceIsRemoved()
    {
        MicAndSpeaker();
        _recorder.Start();
        _backend.RenderDevices.Remove("spk");

        _backend.Live("spk").Fail(new InvalidOperationException("Gerät getrennt"));

        Assert.True(_backend.Created.Single(s => s.DeviceId == "spk").Disposed);
        Assert.Equal(Degradation.NoSystemAudio, _recorder.Status.Degradation);
    }

    [Fact]
    public void FailedMicrophoneThatIsStillDefaultIsReopened()
    {
        MicAndSpeaker();
        _recorder.Start();

        _backend.Live("mic").Fail(new InvalidOperationException("Format geändert"));

        Assert.Equal(2, _backend.Created.Count(s => s.DeviceId == "mic"));
        Assert.True(_backend.Live("mic").Started);
        Assert.Equal(Degradation.None, _recorder.Status.Degradation);
    }

    [Fact]
    public void FailedPlaybackDeviceThatIsStillActiveIsReopened()
    {
        MicAndSpeaker();
        _recorder.Start();

        _backend.Live("spk").Fail(new InvalidOperationException("Treiber-Reset"));

        Assert.Equal(2, _backend.Created.Count(s => s.DeviceId == "spk"));
        Assert.Equal(SourceKind.Loopback, _backend.Live("spk").Kind);
        Assert.Equal(Degradation.None, _recorder.Status.Degradation);
    }

    [Fact]
    public void ReopenIsNotRetriedWithinCooldown()
    {
        MicAndSpeaker();
        _recorder.Start();

        _backend.Live("mic").Fail(new InvalidOperationException("kaputt"));
        _backend.Live("mic").Fail(new InvalidOperationException("immer noch kaputt"));

        Assert.Equal(2, _backend.Created.Count(s => s.DeviceId == "mic"));
        Assert.Equal(Degradation.NoMicrophone, _recorder.Status.Degradation);
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
        Assert.Equal(RecorderError.WriteFailed, _recorder.Status.Error);
        Assert.All(_backend.Created, s => Assert.True(s.Disposed));
        Assert.True(Sink.Disposed);
        Assert.Single(_finished);
        Assert.Single(ReadLog().Split('\n'), l => l.Contains("Aufnahme beendet") && l.Contains("(Schreibfehler)"));
    }

    [Theory]
    [InlineData(16000)]
    [InlineData(48000)]
    public void StopAfterLongStallSkipsTheGapInsteadOfWritingSilence(int rate)
    {
        _sampleRate = rate;
        MicAndSpeaker();
        _recorder.Start();

        _clock.Advance(TimeSpan.FromMinutes(1)); // Taktgeber stand still, z. B. Modern Standby
        _recorder.Stop();

        Assert.True(Sink.Samples.Count < rate, $"{Sink.Samples.Count} Samples Stille geschrieben");
        Assert.Equal(RecorderStatus.Idle, _recorder.Status);
        Assert.Contains("Zeitsprung", ReadLog());
    }

    [Fact]
    public void FailureToCloseTheFileStillEndsTheRecording()
    {
        MicAndSpeaker();
        _recorder.Start();
        Sink.ThrowOnDispose = new IOException("Datenträger voll");

        _recorder.Stop();

        Assert.Equal(RecorderState.Error, _recorder.Status.State);
        Assert.Equal(RecorderError.WriteFailed, _recorder.Status.Error);
        Assert.Equal(_recorder.Status, _statuses.Last());
        Assert.Single(_finished);
    }

    [Fact]
    public void UnexpectedPipelineErrorIsReportedAsInternalError()
    {
        MicAndSpeaker();
        _recorder.Start();
        Sink.ThrowOnWrite = new InvalidOperationException("unerwartet");

        Advance(50);

        Assert.Equal(RecorderState.Error, _recorder.Status.State);
        Assert.Equal(RecorderError.Internal, _recorder.Status.Error);
        Assert.Single(ReadLog().Split('\n'), l => l.Contains("Aufnahme beendet") && l.Contains("(Interner Fehler – siehe Protokoll)"));
    }

    [Fact]
    public void NonErrorStatusesCarryNoErrorCode()
    {
        MicAndSpeaker();
        _recorder.Start();

        Assert.Null(_recorder.Status.Error);
        Assert.Null(RecorderStatus.Idle.Error);
    }

    [Fact]
    public void SinkCreationFailureIsReportedAsWriteError()
    {
        MicAndSpeaker();
        _sinkOverride = (_, _) => throw new IOException("kein Zugriff");

        _recorder.Start();

        Assert.Equal(RecorderState.Error, _recorder.Status.State);
        Assert.Equal(RecorderError.WriteFailed, _recorder.Status.Error);
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

    [Fact]
    public void SampleRateDefaultsToWhatTheRateSourceReturns()
    {
        MicAndSpeaker();

        _recorder.Start();

        Assert.All(_backend.Created, s => Assert.Equal(16000, s.SampleRate));
        Assert.Equal(16000, Sink.SampleRate);
    }

    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void StartPassesTheSessionRateToEverySourceAndTheSink(int rate)
    {
        _sampleRate = rate;
        MicAndSpeaker();
        _backend.RenderDevices.Add("spk2");

        _recorder.Start();

        Assert.Equal(3, _backend.Created.Count);
        Assert.All(_backend.Created, s => Assert.Equal(rate, s.SampleRate));
        Assert.Equal(rate, Assert.Single(_sinkRates));
        Assert.Equal(rate, Sink.SampleRate);
    }

    [Fact]
    public void SampleRateIsReadOncePerStartAndKeptForTheWholeSession()
    {
        _sampleRate = 48000;
        MicAndSpeaker();

        _recorder.Start();
        int readsAfterStart = _sampleRateReads;
        _backend.Live("mic").Fail(new InvalidOperationException("Format geändert"));
        _backend.ActivateRender("headset");
        _backend.ChangeDefaultCapture("mic2");
        Advance(200);
        _recorder.Stop();

        Assert.Equal(1, readsAfterStart);
        Assert.Equal(readsAfterStart, _sampleRateReads);
    }

    [Fact]
    public void RateChangeDuringRecordingOnlyAffectsTheNextRecording()
    {
        _sampleRate = 48000;
        MicAndSpeaker();
        _recorder.Start();

        _sampleRate = 16000;
        _backend.ActivateRender("headset");
        _backend.Live("spk").Fail(new InvalidOperationException("Treiber-Reset"));
        _backend.ChangeDefaultCapture("mic2");

        Assert.All(_backend.Created, s => Assert.Equal(48000, s.SampleRate));
        Assert.Equal(new[] { 48000 }, _sinkRates);

        _recorder.Stop();
        _recorder.Start();

        Assert.Equal(new[] { 48000, 16000 }, _sinkRates);
        Assert.Equal(16000, _backend.Live("mic").SampleRate);
        Assert.Equal(16000, _sinks.Last().SampleRate);
    }

    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public void ReopenedAndLaterAddedSourcesGetTheSessionRate(int rate)
    {
        _sampleRate = rate;
        MicAndSpeaker();
        _recorder.Start();

        _backend.Live("mic").Fail(new InvalidOperationException("kaputt"));
        _backend.Live("spk").Fail(new InvalidOperationException("Treiber-Reset"));
        _backend.ActivateRender("headset");
        _backend.ChangeDefaultCapture("mic2");

        Assert.Equal(2, _backend.Created.Count(s => s.DeviceId == "mic"));
        Assert.Equal(2, _backend.Created.Count(s => s.DeviceId == "spk"));
        Assert.All(_backend.Created, s => Assert.Equal(rate, s.SampleRate));
        Assert.Equal(rate, _backend.Live("headset").SampleRate);
        Assert.Equal(rate, _backend.Live("mic2").SampleRate);
    }

    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void LogNamesTheRateAtStartAndEnd(int rate)
    {
        _sampleRate = rate;
        _backend.DefaultCapture = "mic";

        _recorder.Start();
        var startLine = ReadLog().Split('\n').Single(l => l.Contains("Aufnahme gestartet"));
        _recorder.Stop();
        var endLine = ReadLog().Split('\n').Single(l => l.Contains("Aufnahme beendet"));

        Assert.Contains($"{rate} Hz", startLine);
        Assert.Contains($"{rate} Hz", endLine);
    }

    [Fact]
    public void LogShowsTheRateOfEachRecordingSeparately()
    {
        _backend.DefaultCapture = "mic";
        _sampleRate = 48000;
        _recorder.Start();
        _recorder.Stop();
        _sampleRate = 44100;
        _recorder.Start();
        _recorder.Stop();

        var lines = ReadLog().Split('\n');
        Assert.Single(lines, l => l.Contains("Aufnahme gestartet") && l.Contains("48000 Hz"));
        Assert.Single(lines, l => l.Contains("Aufnahme gestartet") && l.Contains("44100 Hz"));
        Assert.Single(lines, l => l.Contains("Aufnahme beendet") && l.Contains("48000 Hz"));
        Assert.Single(lines, l => l.Contains("Aufnahme beendet") && l.Contains("44100 Hz"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(22050)]
    [InlineData(-1)]
    public void UnsupportedRateFallsBackToTheDefaultRate(int rate)
    {
        _sampleRate = rate;
        MicAndSpeaker();

        _recorder.Start();

        Assert.Equal(RecorderState.Recording, _statuses.Last().State);
        Assert.Equal(AudioFormat.DefaultSampleRate, Assert.Single(_sinkRates));
        Assert.Equal(AudioFormat.DefaultSampleRate, Sink.SampleRate);
        Assert.Equal(2, _backend.Created.Count);
        Assert.All(_backend.Created, s => Assert.Equal(AudioFormat.DefaultSampleRate, s.SampleRate));
        var startLine = ReadLog().Split('\n').Single(l => l.Contains("Aufnahme gestartet"));
        Assert.Contains("16000 Hz", startLine);
    }
}
