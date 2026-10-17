namespace Oreja.Recording;

/// <summary>
/// Jitter-Puffer einer Quelle im Zielformat (Mono Float in der Abtastrate der Aufnahme). Wird vom Capture-Thread
/// beschrieben und vom Mixer-Thread gelesen.
/// Zustände: „Füllen“ liefert Stille, bis der Vorlauf erreicht ist; „Abspielen“ liest,
/// bis der Puffer leerläuft. Über dem Höchstfüllstand wird das Älteste verworfen.
/// </summary>
public sealed class SampleFifo
{
    private readonly object _gate = new();
    private readonly float[] _buffer;
    private readonly int _prebuffer;
    private readonly int _max;
    private int _head;
    private int _count;
    private bool _playing;
    private int _overflows;
    private int _underruns;

    public SampleFifo(int prebufferSamples, int maxSamples)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(prebufferSamples);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSamples, prebufferSamples);
        _prebuffer = prebufferSamples;
        _max = maxSamples;
        _buffer = new float[maxSamples * 2];
    }

    public int Count { get { lock (_gate) return _count; } }
    public bool IsPlaying { get { lock (_gate) return _playing; } }
    public int OverflowCount { get { lock (_gate) return _overflows; } }
    public int UnderrunCount { get { lock (_gate) return _underruns; } }

    public void Write(ReadOnlySpan<float> samples)
    {
        lock (_gate)
        {
            if (samples.Length > _buffer.Length) samples = samples[^_buffer.Length..];
            int free = _buffer.Length - _count;
            if (samples.Length > free) Discard(samples.Length - free);

            int tail = (_head + _count) % _buffer.Length;
            int first = Math.Min(samples.Length, _buffer.Length - tail);
            samples[..first].CopyTo(_buffer.AsSpan(tail));
            samples[first..].CopyTo(_buffer);
            _count += samples.Length;

            if (_count > _max)
            {
                Discard(_count - _prebuffer);
                _overflows++;
            }
        }
    }

    /// <summary>Addiert bis zu destination.Length Samples auf destination. Im Vorlauf passiert nichts (Stille).</summary>
    public void MixInto(Span<float> destination)
    {
        lock (_gate)
        {
            if (!_playing)
            {
                if (_count < _prebuffer) return;
                _playing = true;
            }

            int n = Math.Min(destination.Length, _count);
            AddFromBuffer(destination[..n]);
            if (n < destination.Length)
            {
                _playing = false;
                _underruns++;
            }
        }
    }

    /// <summary>Addiert alles Gepufferte (auch im Vorlauf) auf destination; liefert die Anzahl Samples.</summary>
    public int DrainInto(Span<float> destination)
    {
        lock (_gate)
        {
            int n = Math.Min(destination.Length, _count);
            AddFromBuffer(destination[..n]);
            _playing = false;
            return n;
        }
    }

    private void AddFromBuffer(Span<float> destination)
    {
        for (int i = 0; i < destination.Length; i++)
            destination[i] += _buffer[(_head + i) % _buffer.Length];
        Discard(destination.Length);
    }

    private void Discard(int n)
    {
        _head = (_head + n) % _buffer.Length;
        _count -= n;
    }
}
