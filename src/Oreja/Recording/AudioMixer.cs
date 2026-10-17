using Oreja.Infrastructure;

namespace Oreja.Recording;

public delegate void PcmWriter(ReadOnlySpan<short> samples);

/// <summary>
/// Taktgeber und Mischer: holt bei jedem Tick so viele Samples aus allen Quellen-FIFOs,
/// wie laut Uhr seit Start fällig sind, addiert sie, begrenzt auf [-1, 1] und gibt
/// 16-Bit-PCM aus. Quellen dürfen während der Aufnahme hinzukommen oder wegfallen.
/// </summary>
public sealed class AudioMixer
{
    private readonly IClock _clock;
    private readonly PcmWriter _output;
    private readonly Action<TimeSpan>? _onDiscontinuity;
    private long _skipped;
    private readonly object _gate = new();
    private readonly List<SampleFifo> _sources = [];
    private readonly float[] _mix;
    private short[] _pcm;
    private TimeSpan _start;
    private long _written;
    private bool _started;
    private int _removedOverflows;

    /// <param name="sampleRate">Abtastrate, in der Quellen liefern und der Mixer ausgibt.</param>
    /// <param name="onDiscontinuity">Wird mit der Länge einer übersprungenen Lücke aufgerufen (siehe <see cref="MaxCatchUpSamplesFor"/>).</param>
    public AudioMixer(IClock clock, int sampleRate, PcmWriter output, Action<TimeSpan>? onDiscontinuity = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        _clock = clock;
        SampleRate = sampleRate;
        _output = output;
        _onDiscontinuity = onDiscontinuity;
        _mix = new float[sampleRate]; // Blöcke von höchstens 1 s
        _pcm = new short[sampleRate];
    }

    /// <summary>Vorlauf je Quelle: 200 ms.</summary>
    public static int PrebufferSamplesFor(int sampleRate) => sampleRate / 5;

    /// <summary>Höchstfüllstand je Quelle: 1 s.</summary>
    public static int MaxBufferedSamplesFor(int sampleRate) => sampleRate;

    /// <summary>
    /// Größere Rückstände (10 s) bedeuten, dass der Taktgeber stillstand – etwa im Modern Standby
    /// ohne Suspend-Ereignis. Dann wird die Lücke übersprungen statt stundenlang Stille zu kodieren,
    /// was das Stoppen blockieren und die Transkription mit Leerlauf füllen würde.
    /// </summary>
    public static int MaxCatchUpSamplesFor(int sampleRate) => sampleRate * 10;

    public int SampleRate { get; }

    public long SamplesWritten { get { lock (_gate) return _written; } }

    public int OverflowCount
    {
        get { lock (_gate) return _removedOverflows + _sources.Sum(s => s.OverflowCount); }
    }

    public SampleFifo AddSource()
    {
        var fifo = new SampleFifo(PrebufferSamplesFor(SampleRate), MaxBufferedSamplesFor(SampleRate));
        lock (_gate) _sources.Add(fifo);
        return fifo;
    }

    public void RemoveSource(SampleFifo fifo)
    {
        lock (_gate)
        {
            if (_sources.Remove(fifo)) _removedOverflows += fifo.OverflowCount;
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            _start = _clock.Elapsed;
            _written = 0;
            _skipped = 0;
            _started = true;
        }
    }

    /// <summary>
    /// Gibt alle laut Uhr fälligen Samples aus (in Blöcken von höchstens 1 s). Rückstände über
    /// <see cref="MaxCatchUpSamplesFor"/> werden als Zeitsprung übersprungen und gemeldet.
    /// </summary>
    public void Tick()
    {
        lock (_gate)
        {
            if (!_started) throw new InvalidOperationException("Start() wurde nicht aufgerufen");
            long due = (_clock.Elapsed - _start).Ticks * SampleRate / TimeSpan.TicksPerSecond - _skipped;
            long pending = due - _written;
            if (pending > MaxCatchUpSamplesFor(SampleRate))
            {
                _skipped += pending;
                _onDiscontinuity?.Invoke(TimeSpan.FromTicks(pending * TimeSpan.TicksPerSecond / SampleRate));
                return;
            }
            while (pending > 0)
            {
                var mix = _mix.AsSpan(0, (int)Math.Min(pending, _mix.Length));
                mix.Clear();
                foreach (var source in _sources) source.MixInto(mix);
                Emit(mix);
                pending -= mix.Length;
            }
        }
    }

    /// <summary>Beim Stoppen: fällige Samples plus alles, was noch in den Puffern liegt.</summary>
    public void Flush()
    {
        lock (_gate)
        {
            Tick();
            int remaining = _sources.Count == 0 ? 0 : _sources.Max(s => s.Count);
            if (remaining == 0) return;
            var mix = (remaining <= _mix.Length ? _mix : new float[remaining]).AsSpan(0, remaining);
            mix.Clear();
            foreach (var source in _sources) source.DrainInto(mix);
            Emit(mix);
        }
    }

    private void Emit(Span<float> mix)
    {
        if (_pcm.Length < mix.Length) _pcm = new short[mix.Length];
        var pcm = _pcm.AsSpan(0, mix.Length);
        for (int i = 0; i < mix.Length; i++)
            pcm[i] = (short)Math.Round(Math.Clamp(mix[i], -1f, 1f) * short.MaxValue);
        _output(pcm);
        _written += mix.Length;
    }
}
