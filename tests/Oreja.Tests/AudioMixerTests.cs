using Oreja.Recording;
using Oreja.Tests.Fakes;

namespace Oreja.Tests;

public sealed class AudioMixerTests
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(50);
    private readonly FakeClock _clock = new();
    private readonly List<short> _output = [];

    private AudioMixer NewMixer(int rate, Action<TimeSpan>? onDiscontinuity = null) =>
        new(_clock, rate, samples => _output.AddRange(samples.ToArray()), onDiscontinuity);

    private static float[] Constant(float value, int count) => Enumerable.Repeat(value, count).ToArray();

    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void ExposesItsSampleRate(int rate)
    {
        Assert.Equal(rate, NewMixer(rate).SampleRate);
    }

    [Theory]
    [InlineData(16000, 3200, 16000, 160000)]
    [InlineData(44100, 8820, 44100, 441000)]
    [InlineData(48000, 9600, 48000, 480000)]
    public void BufferSizesAreDerivedFromTheRate(int rate, int prebuffer, int maxBuffered, int maxCatchUp)
    {
        Assert.Equal(prebuffer, AudioMixer.PrebufferSamplesFor(rate));
        Assert.Equal(maxBuffered, AudioMixer.MaxBufferedSamplesFor(rate));
        Assert.Equal(maxCatchUp, AudioMixer.MaxCatchUpSamplesFor(rate));
    }

    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void EmitsExactlyTheDueNumberOfSamples(int rate)
    {
        var mixer = NewMixer(rate);
        mixer.Start();
        for (int i = 0; i < 20; i++) { _clock.Advance(Step); mixer.Tick(); }
        Assert.Equal(rate, _output.Count);

        _clock.Advance(TimeSpan.FromMilliseconds(500));
        mixer.Tick();
        Assert.Equal(rate * 3 / 2, _output.Count);
        Assert.Equal(rate * 3 / 2, mixer.SamplesWritten);
    }

    [Fact]
    public void OddTickLengthsAt44100HzDoNotDrift()
    {
        var mixer = NewMixer(44100);
        mixer.Start();
        for (int i = 0; i < 1000; i++) { _clock.Advance(TimeSpan.FromMilliseconds(7)); mixer.Tick(); }

        Assert.Equal(308700, _output.Count);
        Assert.Equal(308700, mixer.SamplesWritten);
    }

    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void EmitsSilenceWithoutSources(int rate)
    {
        var mixer = NewMixer(rate);
        mixer.Start();
        _clock.Advance(TimeSpan.FromSeconds(1));
        mixer.Tick();

        Assert.Equal(rate, _output.Count);
        Assert.All(_output, s => Assert.Equal(0, s));
    }

    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void RealTimeSourceStartsAfterPrebuffer(int rate)
    {
        var mixer = NewMixer(rate);
        var fifo = mixer.AddSource();
        mixer.Start();
        int perStep = rate / 20;

        for (int i = 0; i < 20; i++)
        {
            fifo.Write(Constant(0.25f, perStep));
            _clock.Advance(Step);
            mixer.Tick();
        }

        // Der Vorlauf (4 Schritte) ist erst nach dem vierten Schreiben gefüllt; der Takt davor lief noch leer.
        int silent = 3 * perStep;
        Assert.Equal(rate, _output.Count);
        Assert.All(_output.Take(silent), s => Assert.Equal(0, s));
        Assert.All(_output.Skip(silent), s => Assert.Equal(8192, s));
    }

    [Fact]
    public void PrebufferAt48kIs200Milliseconds()
    {
        var mixer = NewMixer(48000);
        var fifo = mixer.AddSource();
        mixer.Start();

        fifo.Write(Constant(0.25f, 9599));
        _clock.Advance(TimeSpan.FromSeconds(1));
        mixer.Tick();

        Assert.Equal(48000, _output.Count);
        Assert.All(_output, s => Assert.Equal(0, s)); // 9599 < 9600: noch im Vorlauf

        fifo.Write(Constant(0.25f, 1));
        _clock.Advance(Step);
        mixer.Tick();
        Assert.Equal(8192, _output.Last());
    }

    [Fact]
    public void SourceBufferAt48kHoldsOneSecondAndOverflowsBeyond()
    {
        var mixer = NewMixer(48000);
        var fifo = mixer.AddSource();

        fifo.Write(Constant(0.1f, 48000));
        Assert.Equal(48000, fifo.Count);
        Assert.Equal(0, mixer.OverflowCount);

        fifo.Write(Constant(0.1f, 1));

        Assert.Equal(1, mixer.OverflowCount);
        Assert.Equal(9600, fifo.Count); // auf den Vorlauf gekappt
    }

    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void MixesSourcesBySumming(int rate)
    {
        var mixer = NewMixer(rate);
        mixer.AddSource().Write(Constant(0.25f, rate));
        mixer.AddSource().Write(Constant(0.25f, rate));
        mixer.Start();

        _clock.Advance(TimeSpan.FromSeconds(1));
        mixer.Tick();

        Assert.Equal(rate, _output.Count);
        Assert.All(_output, s => Assert.Equal(16384, s));
    }

    [Theory]
    [InlineData(0.7f, 32767)]
    [InlineData(-0.7f, -32767)]
    public void ClampsToFullScale(float value, short expected)
    {
        var mixer = NewMixer(48000);
        mixer.AddSource().Write(Constant(value, 48000));
        mixer.AddSource().Write(Constant(value, 48000));
        mixer.Start();

        _clock.Advance(TimeSpan.FromSeconds(1));
        mixer.Tick();

        Assert.Equal(48000, _output.Count);
        Assert.All(_output, s => Assert.Equal(expected, s));
    }

    [Theory]
    [InlineData(16000)]
    [InlineData(48000)]
    public void RemovedSourceNoLongerContributes(int rate)
    {
        var mixer = NewMixer(rate);
        mixer.AddSource().Write(Constant(0.25f, rate));
        var second = mixer.AddSource();
        second.Write(Constant(0.5f, rate));
        mixer.Start();

        _clock.Advance(TimeSpan.FromMilliseconds(500));
        mixer.Tick();
        mixer.RemoveSource(second);
        _clock.Advance(TimeSpan.FromMilliseconds(500));
        mixer.Tick();

        Assert.All(_output.Take(rate / 2), s => Assert.Equal(24575, s));
        Assert.All(_output.Skip(rate / 2), s => Assert.Equal(8192, s));
    }

    [Theory]
    [InlineData(16000)]
    [InlineData(48000)]
    public void FlushDrainsSamplesStillInPrebuffer(int rate)
    {
        var mixer = NewMixer(rate);
        mixer.AddSource().Write(Constant(0.25f, 1000));
        mixer.Start();
        mixer.Tick();

        mixer.Flush();

        Assert.Equal(1000, _output.Count);
        Assert.All(_output, s => Assert.Equal(8192, s));
    }

    [Fact]
    public void FlushDrainsAFullSecondBufferAt48k()
    {
        var mixer = NewMixer(48000);
        mixer.AddSource().Write(Constant(0.25f, 48000));
        mixer.Start();

        mixer.Flush();

        Assert.Equal(48000, _output.Count);
        Assert.All(_output, s => Assert.Equal(8192, s));
    }

    [Fact]
    public void ExceptionFromWriterPropagates()
    {
        var mixer = new AudioMixer(_clock, 48000, _ => throw new IOException("Datenträger voll"));
        mixer.Start();
        _clock.Advance(Step);

        Assert.Throws<IOException>(mixer.Tick);
    }

    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void GapsUpToTenSecondsAreFilledInChunks(int rate)
    {
        long total = 0;
        int largestChunk = 0;
        var mixer = new AudioMixer(_clock, rate, samples => { total += samples.Length; largestChunk = Math.Max(largestChunk, samples.Length); });
        mixer.Start();

        _clock.Advance(TimeSpan.FromSeconds(10));
        mixer.Tick();

        Assert.Equal(rate * 10L, total);
        Assert.True(largestChunk <= rate);
    }

    [Theory]
    [InlineData(16000)]
    [InlineData(44100)]
    [InlineData(48000)]
    public void ClockJumpBeyondTenSecondsIsSkippedAndReported(int rate)
    {
        long total = 0;
        var gaps = new List<TimeSpan>();
        var mixer = new AudioMixer(_clock, rate, samples => total += samples.Length, gaps.Add);
        mixer.Start();

        _clock.Advance(TimeSpan.FromHours(8)); // z. B. Modern Standby ohne Suspend-Ereignis
        mixer.Tick();

        Assert.Equal(0, total);
        Assert.Equal(TimeSpan.FromHours(8), Assert.Single(gaps));

        _clock.Advance(Step);
        mixer.Tick();
        Assert.Equal(rate / 20, total);
    }

    [Fact]
    public void GapLimitAt48kIsMeasuredInTimeNotInSixteenKilohertzSamples()
    {
        long total = 0;
        var gaps = new List<TimeSpan>();
        var mixer = new AudioMixer(_clock, 48000, samples => total += samples.Length, gaps.Add);
        mixer.Start();

        _clock.Advance(TimeSpan.FromSeconds(11)); // 528000 Samples > 480000
        mixer.Tick();

        Assert.Equal(0, total);
        Assert.Equal(TimeSpan.FromSeconds(11), Assert.Single(gaps));
    }

    [Theory]
    [InlineData(16000)]
    [InlineData(48000)]
    public void LongRunWithDriftingSourcesStaysExactAndBounded(int rate)
    {
        long total = 0;
        var mixer = new AudioMixer(_clock, rate, samples => total += samples.Length);
        var fast = mixer.AddSource();
        var slow = mixer.AddSource();
        int perStep = rate / 20;
        var fastChunk = Constant(0.1f, perStep + 1);
        var slowChunk = Constant(0.1f, perStep - 1);
        mixer.Start();

        for (int i = 0; i < 144_000; i++) // 2 Stunden in 50-ms-Schritten
        {
            fast.Write(fastChunk);
            slow.Write(slowChunk);
            Assert.True(fast.Count <= AudioMixer.MaxBufferedSamplesFor(rate));
            _clock.Advance(Step);
            mixer.Tick();
        }

        Assert.Equal(2L * 60 * 60 * rate, total);
        Assert.True(mixer.OverflowCount > 0);
        Assert.True(slow.UnderrunCount > 0);
    }
}
