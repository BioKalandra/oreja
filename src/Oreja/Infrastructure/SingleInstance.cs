using System.Diagnostics;

namespace Oreja.Infrastructure;

/// <summary>Was ein zweiter Start über die laufende erste Instanz erfährt: Icon da oder Start hängt vermutlich.</summary>
public enum SecondStartState { Ready, NotReady }

/// <summary>
/// Einzelinstanz über einen benannten Mutex plus Bereitschaftssignal (benanntes ManualReset-Event).
/// Die erste Instanz setzt das Signal, sobald ihr Tray-Icon sichtbar ist; ein zweiter Start erkennt daran,
/// ob er auf ein vorhandenes Icon verweisen kann oder der Start der ersten Instanz hängt.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    public const string DefaultMutexName = @"Local\Oreja.SingleInstance";
    public const string DefaultReadyEventName = @"Local\Oreja.Ready";

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _ready;

    private SingleInstance(Mutex mutex, EventWaitHandle ready)
    {
        _mutex = mutex;
        _ready = ready;
    }

    /// <summary>Erste Instanz: Objekt, das den Mutex hält (Signal noch nicht gesetzt). Läuft schon eine: null.</summary>
    public static SingleInstance? TryAcquire(string mutexName = DefaultMutexName, string readyEventName = DefaultReadyEventName)
    {
        var mutex = new Mutex(initiallyOwned: true, mutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            mutex.Dispose();
            return null;
        }

        try
        {
            var ready = new EventWaitHandle(false, EventResetMode.ManualReset, readyEventName);
            ready.Reset(); // Das Event kann noch gesetzt von einer beendeten Instanz stammen, solange ein Wartender es offen hält.
            return new SingleInstance(mutex, ready);
        }
        catch
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
            throw;
        }
    }

    public void MarkReady() => _ready.Set();

    /// <summary>Nimmt das Signal zurück, z. B. sobald das Tray-Icon beim Beenden verschwunden ist.</summary>
    public void MarkNotReady() => _ready.Reset();

    /// <summary>
    /// Wartet bis zu <paramref name="timeout"/> auf das Signal der ersten Instanz. Gibt es das Event noch nicht
    /// (beide fast gleichzeitig gestartet), wird weiter nachgesehen. Jedes geöffnete Handle wird gleich wieder
    /// geschlossen, damit das Event mit der ersten Instanz verschwindet.
    /// </summary>
    public static SecondStartState WaitForFirstInstance(TimeSpan timeout, string readyEventName = DefaultReadyEventName)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            TimeSpan remaining = timeout - watch.Elapsed;
            if (remaining <= TimeSpan.Zero) return SecondStartState.NotReady;

            if (EventWaitHandle.TryOpenExisting(readyEventName, out var ready))
            {
                using (ready)
                {
                    if (ready.WaitOne(remaining < PollInterval ? remaining : PollInterval)) return SecondStartState.Ready;
                }
            }
            else
            {
                Thread.Sleep(remaining < PollInterval ? remaining : PollInterval);
            }
        }
    }

    /// <summary>Muss auf dem Thread laufen, der <see cref="TryAcquire"/> aufgerufen hat (Mutex-Besitz ist threadgebunden).</summary>
    public void Dispose()
    {
        _ready.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
