namespace Oreja;

/// <summary>Ignoriert Klicks kurz nach dem letzten angenommenen Klick (versehentliche Doppelklicks).</summary>
public sealed class ClickGuard
{
    private readonly long _intervalMilliseconds;
    private long? _lastAccepted;

    public ClickGuard(TimeSpan interval) => _intervalMilliseconds = (long)interval.TotalMilliseconds;

    public bool TryAccept(long nowMilliseconds)
    {
        if (_lastAccepted is long last && nowMilliseconds - last < _intervalMilliseconds) return false;
        _lastAccepted = nowMilliseconds;
        return true;
    }
}
