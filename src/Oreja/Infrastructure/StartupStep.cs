using System.Diagnostics;
using System.Globalization;

namespace Oreja.Infrastructure;

/// <summary>Startschritt mit Protokoll, damit ein hängender Start im Log erkennbar ist.</summary>
public static class StartupStep
{
    /// <summary>
    /// Führt <paramref name="step"/> auf einem Thread-Pool-Thread (MTA) aus und wartet ohne Abbruch auf das Ergebnis.
    /// Protokolliert Beginn und Dauer, dauert es länger als <paramref name="warnAfter"/>, zusätzlich einmal eine Warnung.
    /// Ausnahmen kommen unverändert beim Aufrufer an.
    /// </summary>
    public static T Run<T>(Log log, string what, Func<T> step, TimeSpan warnAfter)
    {
        log.Info($"{what} …");
        var watch = Stopwatch.StartNew();
        var task = Task.Run(step);
        if (Task.WaitAny([task], warnAfter) < 0) // WaitAny wirft anders als Wait nicht bei Fehlern
            log.Info($"{what} dauert ungewöhnlich lange (über {warnAfter.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s)");

        T result = task.GetAwaiter().GetResult(); // wirft die Originalausnahme, nicht AggregateException
        log.Info($"{what} fertig ({watch.ElapsedMilliseconds} ms)");
        return result;
    }
}
