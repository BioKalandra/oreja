namespace Oreja.Recording;

/// <summary>
/// Wählbare Abtastraten der Aufnahmedatei. 16 kHz genügt der Spracherkennung und hält die Dateien
/// klein; 44,1 und 48 kHz behalten auch Höhen, etwa für Hörclips.
/// </summary>
public static class AudioFormat
{
    public const int DefaultSampleRate = 16000;

    public static IReadOnlyList<int> SupportedSampleRates { get; } = [16000, 44100, 48000];

    public static bool IsSupported(int sampleRate) => SupportedSampleRates.Contains(sampleRate);
}
