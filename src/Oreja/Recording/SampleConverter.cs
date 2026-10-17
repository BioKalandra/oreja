using System.Buffers.Binary;
using NAudio.Dsp;
using NAudio.Wave;

namespace Oreja.Recording;

/// <summary>
/// Wandelt Capture-Puffer im Geräteformat in Mono Float mit der Zielrate (Kanäle gemittelt).
/// Weicht die Geräterate ab, rechnet der WDL-Resampler im Sinc-Modus um; dessen Tiefpass
/// unterdrückt beim Heruntertakten alles oberhalb der halben Zielrate, statt es zurückzuspiegeln.
/// </summary>
public sealed class SampleConverter
{
    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00aa00389b71");
    private static readonly Guid FloatSubFormat = new("00000003-0000-0010-8000-00aa00389b71");

    private enum SampleKind { Float32, Pcm16, Pcm24, Pcm32 }

    private readonly SampleKind _kind;
    private readonly int _channels;
    private readonly int _blockAlign;
    private readonly int _bytesPerSample;
    private readonly int _inputRate;
    private readonly int _outputRate;
    private readonly WdlResampler? _resampler;
    private float[] _mono = new float[4096];
    private float[] _output = new float[4096];

    public SampleConverter(WaveFormat input, int outputRate)
    {
        _kind = Classify(input);
        _channels = input.Channels;
        _blockAlign = input.BlockAlign;
        _bytesPerSample = input.BlockAlign / input.Channels;
        _inputRate = input.SampleRate;
        _outputRate = outputRate;

        if (_inputRate != _outputRate)
        {
            _resampler = new WdlResampler();
            _resampler.SetMode(true, 0, true, 64, 32); // Sinc: 64 Stützstellen, 32-fach überabgetastet
            _resampler.SetFilterParms();
            _resampler.SetFeedMode(true);
            _resampler.SetRates(_inputRate, _outputRate);
        }
    }

    /// <summary>Liefert die gewandelten Samples; die Span bleibt bis zum nächsten Aufruf gültig.</summary>
    public ReadOnlySpan<float> Convert(ReadOnlySpan<byte> input)
    {
        int frames = input.Length / _blockAlign;
        if (frames == 0) return ReadOnlySpan<float>.Empty;
        if (_mono.Length < frames) _mono = new float[frames];

        for (int f = 0; f < frames; f++)
        {
            float sum = 0;
            for (int c = 0; c < _channels; c++)
                sum += ReadSample(input.Slice(f * _blockAlign + c * _bytesPerSample, _bytesPerSample));
            _mono[f] = sum / _channels;
        }

        if (_resampler is null) return _mono.AsSpan(0, frames);

        int needed = _resampler.ResamplePrepare(frames, 1, out Span<float> inBuffer);
        _mono.AsSpan(0, needed).CopyTo(inBuffer);
        int capacity = (int)((long)frames * _outputRate / _inputRate) + 16;
        if (_output.Length < capacity) _output = new float[capacity];
        int produced = _resampler.ResampleOut(_output, needed, _output.Length, 1);
        return _output.AsSpan(0, produced);
    }

    private float ReadSample(ReadOnlySpan<byte> b) => _kind switch
    {
        SampleKind.Float32 => BinaryPrimitives.ReadSingleLittleEndian(b),
        SampleKind.Pcm16 => BinaryPrimitives.ReadInt16LittleEndian(b) / 32768f,
        SampleKind.Pcm24 => ((b[2] << 24) | (b[1] << 16) | (b[0] << 8)) / 2147483648f,
        SampleKind.Pcm32 => BinaryPrimitives.ReadInt32LittleEndian(b) / 2147483648f,
        _ => 0f,
    };

    private static SampleKind Classify(WaveFormat format)
    {
        bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat
            || (format is WaveFormatExtensible ext && ext.SubFormat == FloatSubFormat);
        bool isPcm = format.Encoding == WaveFormatEncoding.Pcm
            || (format is WaveFormatExtensible pcm && pcm.SubFormat == PcmSubFormat);

        return (isFloat, isPcm, format.BitsPerSample) switch
        {
            (true, _, 32) => SampleKind.Float32,
            (_, true, 16) => SampleKind.Pcm16,
            (_, true, 24) => SampleKind.Pcm24,
            (_, true, 32) => SampleKind.Pcm32,
            _ => throw new NotSupportedException($"Nicht unterstütztes Aufnahmeformat: {format}"),
        };
    }
}
