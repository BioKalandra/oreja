using Oreja.Infrastructure;
using Oreja.Recording;

namespace Oreja.Tests;

/// <summary>Smoke-Tests gegen echte Audiohardware. Ohne Geräte (z. B. CI) gibt es nichts zu prüfen.</summary>
[Trait("Category", "Hardware")]
public sealed class WasapiBackendTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("oreja-hw-");

    public void Dispose() => _dir.Delete(recursive: true);

    private Log NewLog() => new(Path.Combine(_dir.FullName, "oreja.log"));

    [Fact]
    public Task LoopbackSourceRunsWithoutStoppingOnItsOwn() => Task.Run(() =>
    {
        using var backend = new WasapiBackend(NewLog());
        var ids = backend.GetActiveRenderDeviceIds();
        if (ids.Count == 0) return;

        var fifo = new SampleFifo(AudioMixer.PrebufferSamplesFor(16000), AudioMixer.MaxBufferedSamplesFor(16000));
        Exception? stoppedWith = null;
        bool stopped = false;
        using (var source = backend.CreateSource(ids[0], SourceKind.Loopback, 16000, fifo, observer: null))
        {
            source.Stopped += (_, error) => { stopped = true; stoppedWith = error; };
            source.Start();
            Thread.Sleep(500);
        }

        Assert.False(stopped, $"Quelle hat sich unerwartet beendet: {stoppedWith}");
    });

    [Theory]
    [InlineData(16000)]
    [InlineData(48000)]
    public Task MicrophoneSourceDeliversSamples(int rate) => Task.Run(() =>
    {
        using var backend = new WasapiBackend(NewLog());
        var micId = backend.GetDefaultCaptureDeviceId();
        if (micId is null) return;

        // Großzügige Obergrenze (5 s): Die Aufnahme dauert ~1 s plus Anlaufzeit; ein Überlauf
        // würde die FIFO kürzen und den Vergleich mit den beobachteten Samples verfälschen.
        var fifo = new SampleFifo(AudioMixer.PrebufferSamplesFor(rate), rate * 5);
        int observed = 0;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        using (var source = backend.CreateSource(micId, SourceKind.Microphone, rate, fifo, samples => Interlocked.Add(ref observed, samples.Length)))
        {
            source.Start();
            Thread.Sleep(1000);
            watch.Stop();
        }

        Assert.True(fifo.Count > 0, "Mikrofon hat keine Daten geliefert");
        Assert.Equal(fifo.Count, Volatile.Read(ref observed));
        double expected = rate * watch.Elapsed.TotalSeconds;
        Assert.InRange(Volatile.Read(ref observed), expected * 0.75, expected * 1.25);
    });

    [Fact]
    public Task UnknownDeviceIdThrows() => Task.Run(() =>
    {
        using var backend = new WasapiBackend(NewLog());
        var fifo = new SampleFifo(AudioMixer.PrebufferSamplesFor(16000), AudioMixer.MaxBufferedSamplesFor(16000));

        Assert.ThrowsAny<Exception>(() => backend.CreateSource("{gibt-es-nicht}", SourceKind.Loopback, 16000, fifo, null));
    });
}
