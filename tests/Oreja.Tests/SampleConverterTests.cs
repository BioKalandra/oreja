using NAudio.Wave;
using Oreja.Recording;

namespace Oreja.Tests;

public sealed class SampleConverterTests
{
    private static byte[] FloatBytes(float[] values)
    {
        var bytes = new byte[values.Length * 4];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static byte[] PcmBytes(short[] values)
    {
        var bytes = new byte[values.Length * 2];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>Wandelt eine Sekunde Signal in 10-ms-Blöcken und sammelt die Ausgabe.</summary>
    private static List<float> ConvertOneSecond(WaveFormat format, Func<int, int, float> sampleAt, Func<float[], byte[]> encode)
    {
        var converter = new SampleConverter(format);
        int framesPerChunk = format.SampleRate / 100;
        var output = new List<float>();
        for (int chunk = 0; chunk < 100; chunk++)
        {
            var interleaved = new float[framesPerChunk * format.Channels];
            for (int f = 0; f < framesPerChunk; f++)
                for (int c = 0; c < format.Channels; c++)
                    interleaved[f * format.Channels + c] = sampleAt(chunk * framesPerChunk + f, c);
            output.AddRange(converter.Convert(encode(interleaved)).ToArray());
        }
        return output;
    }

    [Fact]
    public void PassesThroughPcm16MonoAt16k()
    {
        var converter = new SampleConverter(new WaveFormat(16000, 16, 1));

        var result = converter.Convert(PcmBytes([0, 16384, -16384, 32767])).ToArray();

        Assert.Equal(new[] { 0f, 0.5f, -0.5f, 32767 / 32768f }, result);
    }

    [Fact]
    public void DownmixesStereoFloatByAveraging()
    {
        var converter = new SampleConverter(WaveFormat.CreateIeeeFloatWaveFormat(16000, 2));

        var result = converter.Convert(FloatBytes([1.0f, 0.0f, 0.5f, -0.5f])).ToArray();

        Assert.Equal(new[] { 0.5f, 0f }, result);
    }

    [Fact]
    public void Reads24BitPcm()
    {
        var converter = new SampleConverter(new WaveFormat(16000, 24, 1));

        var result = converter.Convert([0x00, 0x00, 0x40, 0x00, 0x00, 0xC0]).ToArray();

        Assert.Equal(new[] { 0.5f, -0.5f }, result);
    }

    [Fact]
    public void ReadsExtensibleFloatFormat()
    {
        var format = new WaveFormatExtensible(16000, 32, 1, true, 32, 4);
        var converter = new SampleConverter(format);

        var result = converter.Convert(FloatBytes([0.25f, -0.75f])).ToArray();

        Assert.Equal(new[] { 0.25f, -0.75f }, result);
    }

    [Fact]
    public void ResamplesStereoFloat48kTo16k()
    {
        var output = ConvertOneSecond(
            WaveFormat.CreateIeeeFloatWaveFormat(48000, 2),
            (frame, _) => (float)(0.5 * Math.Sin(2 * Math.PI * 440 * frame / 48000.0)),
            FloatBytes);

        Assert.InRange(output.Count, 16000 - 32, 16000 + 32);
        double rms = Math.Sqrt(output.Skip(160).Average(s => (double)s * s));
        Assert.InRange(rms, 0.3536 - 0.02, 0.3536 + 0.02);
    }

    [Fact]
    public void Resamples44100Pcm16Stereo()
    {
        var output = ConvertOneSecond(
            new WaveFormat(44100, 16, 2),
            (frame, _) => (float)(0.5 * Math.Sin(2 * Math.PI * 440 * frame / 44100.0)),
            floats => PcmBytes(floats.Select(f => (short)(f * 32767)).ToArray()));

        Assert.InRange(output.Count, 16000 - 32, 16000 + 32);
    }

    [Fact]
    public void DownmixesSixChannelsAndResamples()
    {
        var output = ConvertOneSecond(
            WaveFormat.CreateIeeeFloatWaveFormat(48000, 6),
            (_, _) => 0.6f,
            FloatBytes);

        Assert.InRange(output.Count, 16000 - 32, 16000 + 32);
        Assert.All(output.Skip(160), s => Assert.InRange(s, 0.59f, 0.61f));
    }

    [Fact]
    public void RejectsUnsupportedFormat()
    {
        Assert.Throws<NotSupportedException>(() => new SampleConverter(new WaveFormat(16000, 8, 1)));
    }
}
