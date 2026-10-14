using Oreja.Infrastructure;

namespace Oreja.Tests.Fakes;

public sealed class FakeClock : IClock
{
    private long _ticks;

    public TimeSpan Elapsed => TimeSpan.FromTicks(Interlocked.Read(ref _ticks));

    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}
