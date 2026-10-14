using Oreja.Recording;
using Oreja.Tests.Fakes;

namespace Oreja.Tests;

public sealed class AudioMixerTests
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(50);
    private readonly FakeClock _clock = new();
    private readonly List<short> _output = [];
    private readonly AudioMixer _mixer;

    public AudioMixerTests() => _mixer = new AudioMixer(_clock, samples => _output.AddRange(samples.ToArray()));

    private static float[] Constant(float value, int count) => Enumerable.Repeat(value, count).ToArray();

    [Fact]
    public void EmitsExactlyTheDueNumberOfSamples()
    {
        _mixer.Start();
        for (int i = 0; i < 20; i++) { _clock.Advance(Step); _mixer.Tick(); }
        Assert.Equal(16000, _output.Count);

        _clock.Advance(TimeSpan.FromMilliseconds(500));
        _mixer.Tick();
        Assert.Equal(24000, _output.Count);
        Assert.Equal(24000, _mixer.SamplesWritten);
    }

    [Fact]
    public void EmitsSilenceWithoutSources()
    {
        _mixer.Start();
        _clock.Advance(TimeSpan.FromSeconds(1));
        _mixer.Tick();

        Assert.Equal(16000, _output.Count);
        Assert.All(_output, s => Assert.Equal(0, s));
    }

    [Fact]
    public void RealTimeSourceStartsAfterPrebuffer()
    {
        var fifo = _mixer.AddSource();
        _mixer.Start();

        for (int i = 0; i < 20; i++)
        {
            fifo.Write(Constant(0.25f, 800));
            _clock.Advance(Step);
            _mixer.Tick();
        }

        Assert.Equal(16000, _output.Count);
        Assert.All(_output.Take(2400), s => Assert.Equal(0, s));
        Assert.All(_output.Skip(2400), s => Assert.Equal(8192, s));
    }

    [Fact]
    public void MixesSourcesBySumming()
    {
        _mixer.AddSource().Write(Constant(0.25f, 16000));
        _mixer.AddSource().Write(Constant(0.25f, 16000));
        _mixer.Start();

        _clock.Advance(TimeSpan.FromSeconds(1));
        _mixer.Tick();

        Assert.All(_output, s => Assert.Equal(16384, s));
    }

    [Theory]
    [InlineData(0.7f, 32767)]
    [InlineData(-0.7f, -32767)]
    public void ClampsToFullScale(float value, short expected)
    {
        _mixer.AddSource().Write(Constant(value, 16000));
        _mixer.AddSource().Write(Constant(value, 16000));
        _mixer.Start();

        _clock.Advance(TimeSpan.FromSeconds(1));
        _mixer.Tick();

        Assert.All(_output, s => Assert.Equal(expected, s));
    }

    [Fact]
    public void RemovedSourceNoLongerContributes()
    {
        _mixer.AddSource().Write(Constant(0.25f, 16000));
        var second = _mixer.AddSource();
        second.Write(Constant(0.5f, 16000));
        _mixer.Start();

        _clock.Advance(TimeSpan.FromMilliseconds(500));
        _mixer.Tick();
        _mixer.RemoveSource(second);
        _clock.Advance(TimeSpan.FromMilliseconds(500));
        _mixer.Tick();

        Assert.All(_output.Take(8000), s => Assert.Equal(24575, s));
        Assert.All(_output.Skip(8000), s => Assert.Equal(8192, s));
    }

    [Fact]
    public void FlushDrainsSamplesStillInPrebuffer()
    {
        _mixer.AddSource().Write(Constant(0.25f, 1000));
        _mixer.Start();
        _mixer.Tick();

        _mixer.Flush();

        Assert.Equal(1000, _output.Count);
        Assert.All(_output, s => Assert.Equal(8192, s));
    }

    [Fact]
    public void ExceptionFromWriterPropagates()
    {
        var mixer = new AudioMixer(_clock, _ => throw new IOException("Datenträger voll"));
        mixer.Start();
        _clock.Advance(Step);

        Assert.Throws<IOException>(mixer.Tick);
    }

    [Fact]
    public void GapsUpToTenSecondsAreFilledInChunks()
    {
        long total = 0;
        int largestChunk = 0;
        var mixer = new AudioMixer(_clock, samples => { total += samples.Length; largestChunk = Math.Max(largestChunk, samples.Length); });
        mixer.Start();

        _clock.Advance(TimeSpan.FromSeconds(10));
        mixer.Tick();

        Assert.Equal(160_000, total);
        Assert.True(largestChunk <= AudioMixer.SampleRate);
    }

    [Fact]
    public void ClockJumpBeyondTenSecondsIsSkippedAndReported()
    {
        long total = 0;
        var gaps = new List<TimeSpan>();
        var mixer = new AudioMixer(_clock, samples => total += samples.Length, gaps.Add);
        mixer.Start();

        _clock.Advance(TimeSpan.FromHours(8)); // z. B. Modern Standby ohne Suspend-Ereignis
        mixer.Tick();

        Assert.Equal(0, total);
        Assert.Equal(TimeSpan.FromHours(8), Assert.Single(gaps));

        _clock.Advance(Step);
        mixer.Tick();
        Assert.Equal(800, total);
    }

    [Fact]
    public void LongRunWithDriftingSourcesStaysExactAndBounded()
    {
        long total = 0;
        var mixer = new AudioMixer(_clock, samples => total += samples.Length);
        var fast = mixer.AddSource();
        var slow = mixer.AddSource();
        var fastChunk = Constant(0.1f, 801);
        var slowChunk = Constant(0.1f, 799);
        mixer.Start();

        for (int i = 0; i < 144_000; i++) // 2 Stunden in 50-ms-Schritten
        {
            fast.Write(fastChunk);
            slow.Write(slowChunk);
            Assert.True(fast.Count <= AudioMixer.MaxBufferedSamples);
            _clock.Advance(Step);
            mixer.Tick();
        }

        Assert.Equal(2L * 60 * 60 * AudioMixer.SampleRate, total);
        Assert.True(mixer.OverflowCount > 0);
        Assert.True(slow.UnderrunCount > 0);
    }
}
