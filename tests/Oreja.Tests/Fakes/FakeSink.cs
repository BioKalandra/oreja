using Oreja.Recording;

namespace Oreja.Tests.Fakes;

public sealed class FakeSink(int sampleRate = 16000) : IFlacSink
{
    public int SampleRate { get; } = sampleRate;
    public List<short> Samples { get; } = [];
    public bool Completed { get; private set; }
    public bool Disposed { get; private set; }
    public Exception? ThrowOnWrite { get; set; }
    public Exception? ThrowOnDispose { get; set; }

    public void Write(ReadOnlySpan<short> samples)
    {
        if (ThrowOnWrite is not null) throw ThrowOnWrite;
        Samples.AddRange(samples.ToArray());
    }

    public void Complete() => Completed = true;

    public void Dispose()
    {
        Disposed = true;
        if (ThrowOnDispose is not null) throw ThrowOnDispose;
    }
}
