using System.Diagnostics;

namespace Oreja.Infrastructure;

/// <summary>Monotone Zeitquelle; in Tests durch eine simulierte Uhr ersetzt.</summary>
public interface IClock
{
    TimeSpan Elapsed { get; }
}

public sealed class SystemClock : IClock
{
    private readonly long _start = Stopwatch.GetTimestamp();

    public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_start);
}
