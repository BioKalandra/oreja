namespace Oreja.Recording;

/// <summary>Schreibt 16-Bit-Mono-PCM fortlaufend in eine FLAC-Datei.</summary>
public interface IFlacSink : IDisposable
{
    /// <summary>Kodiert Samples; wirft <see cref="IOException"/>, wenn nicht geschrieben werden kann.</summary>
    void Write(ReadOnlySpan<short> samples);

    /// <summary>Schreibt den Rest und aktualisiert den FLAC-Kopf (Gesamtlänge).</summary>
    void Complete();
}
