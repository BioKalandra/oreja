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

        var fifo = new SampleFifo(AudioMixer.PrebufferSamples, AudioMixer.MaxBufferedSamples);
        Exception? stoppedWith = null;
        bool stopped = false;
        using (var source = backend.CreateSource(ids[0], SourceKind.Loopback, fifo, observer: null))
        {
            source.Stopped += (_, error) => { stopped = true; stoppedWith = error; };
            source.Start();
            Thread.Sleep(500);
        }

        Assert.False(stopped, $"Quelle hat sich unerwartet beendet: {stoppedWith}");
    });

    [Fact]
    public Task MicrophoneSourceDeliversSamples() => Task.Run(() =>
    {
        using var backend = new WasapiBackend(NewLog());
        var micId = backend.GetDefaultCaptureDeviceId();
        if (micId is null) return;

        var fifo = new SampleFifo(AudioMixer.PrebufferSamples, AudioMixer.MaxBufferedSamples);
        int observed = 0;
        using (var source = backend.CreateSource(micId, SourceKind.Microphone, fifo, samples => Interlocked.Add(ref observed, samples.Length)))
        {
            source.Start();
            Thread.Sleep(500);
        }

        Assert.True(fifo.Count > 0, "Mikrofon hat keine Daten geliefert");
        Assert.Equal(fifo.Count, Volatile.Read(ref observed));
    });

    [Fact]
    public Task UnknownDeviceIdThrows() => Task.Run(() =>
    {
        using var backend = new WasapiBackend(NewLog());
        var fifo = new SampleFifo(AudioMixer.PrebufferSamples, AudioMixer.MaxBufferedSamples);

        Assert.ThrowsAny<Exception>(() => backend.CreateSource("{gibt-es-nicht}", SourceKind.Loopback, fifo, null));
    });
}
