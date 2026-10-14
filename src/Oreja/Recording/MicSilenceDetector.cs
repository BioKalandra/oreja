using Oreja.Infrastructure;

namespace Oreja.Recording;

/// <summary>
/// Erkennt ein Mikrofon ohne Signal: nur exakte digitale Nullen oder gar keine Daten
/// (stummgeschaltetes Headset, in den Windows-Datenschutzeinstellungen gesperrt).
/// Ein echtes Mikrofon rauscht immer minimal.
/// </summary>
public sealed class MicSilenceDetector
{
    public static readonly TimeSpan DefaultThreshold = TimeSpan.FromSeconds(3);

    private readonly IClock _clock;
    private readonly TimeSpan _threshold;
    private long _lastSignalTicks;

    public MicSilenceDetector(IClock clock, TimeSpan threshold)
    {
        _clock = clock;
        _threshold = threshold;
        Reset();
    }

    public void Reset() => Interlocked.Exchange(ref _lastSignalTicks, _clock.Elapsed.Ticks);

    public void Observe(ReadOnlySpan<float> samples)
    {
        foreach (var sample in samples)
        {
            if (sample != 0f)
            {
                Interlocked.Exchange(ref _lastSignalTicks, _clock.Elapsed.Ticks);
                return;
            }
        }
    }

    public bool IsSilent => _clock.Elapsed.Ticks - Interlocked.Read(ref _lastSignalTicks) >= _threshold.Ticks;
}
