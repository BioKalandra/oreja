using NAudio.MediaFoundation;
using NAudio.Wave;
using Oreja.Recording;

namespace Oreja.Tests;

public sealed class LibFlacSinkTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("oreja-flac-");

    public void Dispose() => _dir.Delete(recursive: true);

    [Fact]
    public void RoundtripIsBitExact()
    {
        var path = Path.Combine(_dir.FullName, "roundtrip.flac");
        var input = TestSignal(32000);

        using (var sink = LibFlacSink.Create(path))
        {
            for (int i = 0; i < input.Length; i += 800) sink.Write(input.AsSpan(i, 800));
            sink.Complete();
        }

        Assert.Equal(input, ReadBack(path));
    }

    [Fact]
    public void WritesFramesBeforeComplete()
    {
        var path = Path.Combine(_dir.FullName, "growing.flac");
        var input = TestSignal(48000);

        using var sink = LibFlacSink.Create(path);
        for (int i = 0; i < input.Length; i += 800) sink.Write(input.AsSpan(i, 800));

        Assert.True(new FileInfo(path).Length > 5000, "Frames müssen schon vor Complete() auf der Platte liegen");
    }

    [Fact]
    public void WritesToPathWithNonAsciiCharacters()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_dir.FullName, "Jürgen", "Español Übungen"));
        var path = Path.Combine(folder.FullName, "aufnahme.flac");
        var input = TestSignal(16000);

        using (var sink = LibFlacSink.Create(path))
        {
            sink.Write(input);
            sink.Complete();
        }

        Assert.Equal(input, ReadBack(path));
    }

    [Fact]
    public void CreateRefusesToOverwriteExistingFile()
    {
        var path = Path.Combine(_dir.FullName, "exists.flac");
        File.WriteAllText(path, "x");

        Assert.Throws<IOException>(() => LibFlacSink.Create(path));
    }

    [Fact]
    public void WriteFailureSurfacesAsIOException()
    {
        var sink = new LibFlacSink(new FailingStream(failAfterBytes: 4000));
        var chunk = TestSignal(800);

        var error = Assert.Throws<IOException>(() =>
        {
            for (int i = 0; i < 100; i++) sink.Write(chunk);
        });

        Assert.Equal("Datenträger voll", error.InnerException?.Message);
        sink.Dispose(); // darf nach einem Schreibfehler nicht werfen
    }

    private static short[] TestSignal(int count)
    {
        var samples = new short[count];
        for (int i = 0; i < count; i++)
            samples[i] = (short)(Math.Sin(2 * Math.PI * 440 * i / 16000.0) * 12000 + (i % 7) - 3);
        return samples;
    }

    private static short[] ReadBack(string path)
    {
        MediaFoundationApi.Startup();
        using var reader = new MediaFoundationReader(path);
        Assert.Equal(16000, reader.WaveFormat.SampleRate);
        Assert.Equal(1, reader.WaveFormat.Channels);
        Assert.Equal(16, reader.WaveFormat.BitsPerSample);
        using var pcm = new MemoryStream();
        reader.CopyTo(pcm);
        var bytes = pcm.ToArray();
        var samples = new short[bytes.Length / 2];
        Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 2);
        return samples;
    }

    private sealed class FailingStream(long failAfterBytes) : MemoryStream
    {
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (Length + buffer.Length > failAfterBytes) throw new IOException("Datenträger voll");
            base.Write(buffer);
        }
    }
}
