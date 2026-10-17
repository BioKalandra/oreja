using System.Globalization;
using Oreja.Recording;

namespace Oreja;

public enum TrayIconKind { Idle, Recording, RecordingDegraded, Error }

/// <summary>Bildet den Recorder-Status auf Icon, Tooltip und Menütext ab.</summary>
public static class StatusText
{
    private const int MaxTooltipLength = 127;
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public static TrayIconKind IconKind(RecorderStatus status) => status.State switch
    {
        RecorderState.Recording when status.Degradation != Degradation.None => TrayIconKind.RecordingDegraded,
        RecorderState.Recording => TrayIconKind.Recording,
        RecorderState.Error => TrayIconKind.Error,
        _ => TrayIconKind.Idle,
    };

    public static string Tooltip(RecorderStatus status)
    {
        string text = status.State switch
        {
            RecorderState.Recording =>
                $"Oreja – Aufnahme seit {status.StartedAt?.ToString("HH:mm", CultureInfo.InvariantCulture)}{DegradationSuffix(status.Degradation)}",
            RecorderState.Error => $"Oreja – Fehler: {status.ErrorMessage}",
            _ => "Oreja – bereit",
        };
        return text.Length <= MaxTooltipLength ? text : text[..MaxTooltipLength];
    }

    public static string ToggleMenuText(RecorderStatus status) =>
        status.State == RecorderState.Recording ? "Aufnahme stoppen" : "Aufnahme starten";

    /// <summary>Menüeintrag einer Abtastrate, z. B. „44,1 kHz“; 16 und 48 kHz nennen zusätzlich ihren Zweck.</summary>
    public static string SampleRateMenuText(int sampleRate)
    {
        string khz = $"{(sampleRate / 1000.0).ToString("0.#", German)} kHz";
        return sampleRate switch
        {
            16000 => $"{khz} (Spracherkennung)",
            48000 => $"{khz} (Hörclips)",
            _ => khz,
        };
    }

    private static string DegradationSuffix(Degradation degradation)
    {
        var parts = new List<string>();
        if (degradation.HasFlag(Degradation.NoMicrophone)) parts.Add("ohne Mikrofon");
        if (degradation.HasFlag(Degradation.MicrophoneSilent)) parts.Add("Mikrofon stumm/gesperrt");
        if (degradation.HasFlag(Degradation.NoSystemAudio)) parts.Add("ohne Systemaudio");
        return parts.Count == 0 ? "" : $" ({string.Join(", ", parts)})";
    }
}
