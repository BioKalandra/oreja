using Oreja.Recording;

namespace Oreja.Tests.Fakes;

public sealed class FakeSink : IFlacSink
{
    public List<short> Samples { get; } = [];
    public bool Completed { get; private set; }
    public bool Disposed { get; private set; }
    public Exception? ThrowOnWrite { get; set; }

    public void Write(ReadOnlySpan<short> samples)
    {
        if (ThrowOnWrite is not null) throw ThrowOnWrite;
        Samples.AddRange(samples.ToArray());
    }

    public void Complete() => Completed = true;

    public void Dispose() => Disposed = true;
}
