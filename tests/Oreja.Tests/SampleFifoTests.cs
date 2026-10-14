using Oreja.Recording;

namespace Oreja.Tests;

public sealed class SampleFifoTests
{
    private static float[] Sequence(int start, int count) =>
        Enumerable.Range(start, count).Select(i => (float)i).ToArray();

    [Fact]
    public void ProducesSilenceUntilPrebufferIsFilled()
    {
        var fifo = new SampleFifo(prebufferSamples: 100, maxSamples: 1000);
        fifo.Write(Sequence(1, 99));

        var destination = new float[50];
        fifo.MixInto(destination);

        Assert.All(destination, s => Assert.Equal(0f, s));
        Assert.False(fifo.IsPlaying);
        Assert.Equal(99, fifo.Count);
    }

    [Fact]
    public void DeliversSamplesInOrderOncePrebufferIsReached()
    {
        var fifo = new SampleFifo(100, 1000);
        fifo.Write(Sequence(1, 200));

        var first = new float[50];
        var second = new float[50];
        fifo.MixInto(first);
        fifo.MixInto(second);

        Assert.Equal(Sequence(1, 50), first);
        Assert.Equal(Sequence(51, 50), second);
        Assert.True(fifo.IsPlaying);
        Assert.Equal(100, fifo.Count);
    }

    [Fact]
    public void MixIntoAddsOntoExistingContent()
    {
        var fifo = new SampleFifo(10, 1000);
        fifo.Write(Enumerable.Repeat(0.25f, 10).ToArray());

        var destination = Enumerable.Repeat(0.5f, 10).ToArray();
        fifo.MixInto(destination);

        Assert.All(destination, s => Assert.Equal(0.75f, s));
    }

    [Fact]
    public void UnderrunPadsWithSilenceAndReturnsToPrebuffering()
    {
        var fifo = new SampleFifo(100, 1000);
        fifo.Write(Sequence(1, 120));

        var destination = new float[150];
        fifo.MixInto(destination);

        Assert.Equal(Sequence(1, 120), destination[..120]);
        Assert.All(destination[120..], s => Assert.Equal(0f, s));
        Assert.False(fifo.IsPlaying);
        Assert.Equal(1, fifo.UnderrunCount);

        fifo.Write(Sequence(1, 50));
        var next = new float[10];
        fifo.MixInto(next);
        Assert.All(next, s => Assert.Equal(0f, s)); // wieder im Vorlauf
    }

    [Fact]
    public void OverflowDropsOldestSamplesDownToPrebuffer()
    {
        var fifo = new SampleFifo(100, 1000);
        fifo.Write(Sequence(0, 1001));

        Assert.Equal(100, fifo.Count);
        Assert.Equal(1, fifo.OverflowCount);

        var destination = new float[100];
        fifo.MixInto(destination);
        Assert.Equal(Sequence(901, 100), destination);
    }

    [Fact]
    public void DrainIntoReturnsEverythingEvenDuringPrebuffering()
    {
        var fifo = new SampleFifo(100, 1000);
        fifo.Write(Sequence(1, 30));

        var destination = new float[100];
        int drained = fifo.DrainInto(destination);

        Assert.Equal(30, drained);
        Assert.Equal(Sequence(1, 30), destination[..30]);
        Assert.Equal(0, fifo.Count);
    }

    [Fact]
    public void RingBufferWrapsAroundWithoutLosingOrder()
    {
        var fifo = new SampleFifo(prebufferSamples: 10, maxSamples: 20); // Ringpuffer mit 40 Plätzen
        int next = 0;
        var expected = new List<float>();
        var actual = new List<float>();

        for (int round = 0; round < 20; round++)
        {
            fifo.Write(Sequence(next, 15));
            expected.AddRange(Sequence(next, 15));
            next += 15;
            var destination = new float[15];
            fifo.MixInto(destination);
            actual.AddRange(destination);
        }

        Assert.Equal(expected.Take(actual.Count), actual);
    }
}
