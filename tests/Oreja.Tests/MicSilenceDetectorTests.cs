using Oreja.Recording;
using Oreja.Tests.Fakes;

namespace Oreja.Tests;

public sealed class MicSilenceDetectorTests
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(100);
    private readonly FakeClock _clock = new();

    private MicSilenceDetector NewDetector() => new(_clock, TimeSpan.FromSeconds(3));

    [Fact]
    public void IsNotSilentBeforeThresholdElapsed()
    {
        var detector = NewDetector();
        _clock.Advance(TimeSpan.FromMilliseconds(2900));

        Assert.False(detector.IsSilent);
    }

    [Fact]
    public void ReportsSilenceAfterThreeSecondsOfDigitalZeros()
    {
        var detector = NewDetector();
        var zeros = new float[1600];
        for (int i = 0; i < 30; i++) { detector.Observe(zeros); _clock.Advance(Step); }

        Assert.True(detector.IsSilent);
    }

    [Fact]
    public void ReportsSilenceWhenNoDataArrivesAtAll()
    {
        var detector = NewDetector();
        _clock.Advance(TimeSpan.FromSeconds(3));

        Assert.True(detector.IsSilent);
    }

    [Fact]
    public void FaintNoiseCountsAsSignal()
    {
        var detector = NewDetector();
        for (int i = 0; i < 50; i++) { detector.Observe([0f, 1e-7f, 0f]); _clock.Advance(Step); }

        Assert.False(detector.IsSilent);
    }

    [Fact]
    public void SignalClearsSilence()
    {
        var detector = NewDetector();
        _clock.Advance(TimeSpan.FromSeconds(4));
        Assert.True(detector.IsSilent);

        detector.Observe([0.01f]);

        Assert.False(detector.IsSilent);
    }

    [Fact]
    public void ResetGrantsNewGracePeriod()
    {
        var detector = NewDetector();
        _clock.Advance(TimeSpan.FromSeconds(4));

        detector.Reset();

        Assert.False(detector.IsSilent);
    }
}
