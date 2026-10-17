using System.Collections.Concurrent;
using Oreja.Infrastructure;

namespace Oreja.Tests;

/// <summary>
/// Mutex-Besitz ist threadgebunden. Damit zwei Instanzen sich wie zwei Prozesse verhalten,
/// läuft die "erste Instanz" hier auf einem eigenen Thread und wird auch dort freigegeben.
/// </summary>
public sealed class SingleInstanceTests : IDisposable
{
    private readonly string _mutexName = $@"Local\OrejaTest.Mutex.{Guid.NewGuid():N}";
    private readonly string _eventName = $@"Local\OrejaTest.Ready.{Guid.NewGuid():N}";
    private readonly List<OwnerThread> _owners = new();

    public void Dispose()
    {
        foreach (var owner in _owners) owner.Dispose();
    }

    private OwnerThread StartFirstInstance()
    {
        var owner = new OwnerThread(_mutexName, _eventName);
        _owners.Add(owner);
        return owner;
    }

    [Fact]
    public void FirstInstanceGetsTheMutex()
    {
        var first = StartFirstInstance();

        Assert.True(first.Acquired);
    }

    [Fact]
    public void SecondInstanceDoesNotGetTheMutex()
    {
        var first = StartFirstInstance();
        Assert.True(first.Acquired);

        using var second = SingleInstance.TryAcquire(_mutexName, _eventName);

        Assert.Null(second);
    }

    [Fact]
    public void SecondInstanceSeesReadyImmediatelyAfterMarkReady()
    {
        var first = StartFirstInstance();
        first.MarkReady();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var state = SingleInstance.WaitForFirstInstance(TimeSpan.FromSeconds(5), _eventName);
        watch.Stop();

        Assert.Equal(SecondStartState.Ready, state);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"wartete {watch.Elapsed}");
    }

    [Fact]
    public void SecondInstanceSeesNotReadyAfterTimeoutWithoutMarkReady()
    {
        var first = StartFirstInstance();
        Assert.True(first.Acquired);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var state = SingleInstance.WaitForFirstInstance(TimeSpan.FromMilliseconds(200), _eventName);
        watch.Stop();

        Assert.Equal(SecondStartState.NotReady, state);
        Assert.True(watch.Elapsed >= TimeSpan.FromMilliseconds(150), $"wartete nur {watch.Elapsed}");
    }

    [Fact]
    public void NotReadyWhenNoFirstInstanceExists()
    {
        var state = SingleInstance.WaitForFirstInstance(TimeSpan.FromMilliseconds(200), _eventName);

        Assert.Equal(SecondStartState.NotReady, state);
    }

    [Fact]
    public async Task MarkReadyWhileWaitingEndsTheWaitEarly()
    {
        var first = StartFirstInstance();
        Assert.True(first.Acquired);

        var waiter = Task.Run(() =>
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var state = SingleInstance.WaitForFirstInstance(TimeSpan.FromSeconds(10), _eventName);
            return (state, watch.Elapsed);
        });
        Thread.Sleep(200);
        Assert.False(waiter.IsCompleted);

        first.MarkReady();

        var (state, elapsed) = await waiter.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.Equal(SecondStartState.Ready, state);
        Assert.True(elapsed < TimeSpan.FromSeconds(7), $"wartete {elapsed}");
    }

    [Fact]
    public async Task WaitingStartedBeforeFirstInstanceExistsStillSeesReady()
    {
        var waiter = Task.Run(() => SingleInstance.WaitForFirstInstance(TimeSpan.FromSeconds(10), _eventName));
        Thread.Sleep(200);

        var first = StartFirstInstance();
        first.MarkReady();

        var state = await waiter.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.Equal(SecondStartState.Ready, state);
    }

    [Fact]
    public void NewInstanceGetsTheMutexAfterFirstIsDisposed()
    {
        var first = StartFirstInstance();
        Assert.True(first.Acquired);
        first.Dispose();

        var next = StartFirstInstance();

        Assert.True(next.Acquired);
    }

    [Fact]
    public void ReadySignalIsGoneAfterFirstIsDisposed()
    {
        var first = StartFirstInstance();
        first.MarkReady();
        first.Dispose();

        var state = SingleInstance.WaitForFirstInstance(TimeSpan.FromMilliseconds(200), _eventName);

        Assert.Equal(SecondStartState.NotReady, state);
    }

    [Fact]
    public void NewFirstInstanceResetsAReadySignalLeftBehindByAnEarlierOne()
    {
        var first = StartFirstInstance();
        first.MarkReady();
        // Ein Wartender hält das Event offen und lässt es die erste Instanz überleben.
        using var keepAlive = EventWaitHandle.OpenExisting(_eventName);
        first.Dispose();

        var next = StartFirstInstance();
        Assert.True(next.Acquired);

        var state = SingleInstance.WaitForFirstInstance(TimeSpan.FromMilliseconds(200), _eventName);

        Assert.Equal(SecondStartState.NotReady, state);
    }

    [Fact]
    public void MarkNotReadyTakesTheReadySignalBack()
    {
        var first = StartFirstInstance();
        first.MarkReady();
        first.MarkNotReady();

        var state = SingleInstance.WaitForFirstInstance(TimeSpan.FromMilliseconds(200), _eventName);

        Assert.Equal(SecondStartState.NotReady, state);
    }

    [Fact]
    public void MarkReadyAfterMarkNotReadySignalsReadyAgain()
    {
        var first = StartFirstInstance();
        first.MarkReady();
        first.MarkNotReady();
        first.MarkReady();

        var state = SingleInstance.WaitForFirstInstance(TimeSpan.FromSeconds(5), _eventName);

        Assert.Equal(SecondStartState.Ready, state);
    }

    [Fact]
    public void DefaultNamesAreTheDocumentedOnes()
    {
        Assert.Equal(@"Local\Oreja.SingleInstance", SingleInstance.DefaultMutexName);
        Assert.Equal(@"Local\Oreja.Ready", SingleInstance.DefaultReadyEventName);
    }

    /// <summary>Hält eine SingleInstance auf einem eigenen Thread, wie ein eigener Prozess es täte.</summary>
    private sealed class OwnerThread : IDisposable
    {
        private readonly BlockingCollection<Action> _work = new();
        private readonly Thread _thread;
        private SingleInstance? _instance;
        private bool _disposed;

        public OwnerThread(string mutexName, string eventName)
        {
            var started = new ManualResetEventSlim();
            _thread = new Thread(() =>
            {
                _instance = SingleInstance.TryAcquire(mutexName, eventName);
                started.Set();
                foreach (var action in _work.GetConsumingEnumerable())
                    action();
                _instance?.Dispose();
            }) { IsBackground = true };
            _thread.Start();
            started.Wait();
        }

        public bool Acquired => _instance is not null;

        public void MarkReady() => Run(() => _instance!.MarkReady());

        public void MarkNotReady() => Run(() => _instance!.MarkNotReady());

        private void Run(Action action)
        {
            var done = new ManualResetEventSlim();
            Exception? error = null;
            _work.Add(() =>
            {
                try { action(); }
                catch (Exception ex) { error = ex; }
                finally { done.Set(); }
            });
            done.Wait();
            if (error is not null) throw error;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _work.CompleteAdding();
            _thread.Join();
        }
    }
}
