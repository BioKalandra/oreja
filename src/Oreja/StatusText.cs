using System.Globalization;
using Oreja.Recording;

namespace Oreja;

public enum TrayIconKind { Idle, Recording, RecordingDegraded, Error }

/// <summary>Bildet den Recorder-Status auf Icon, Tooltip und Menütext in der gewählten Sprache ab.</summary>
public static class StatusText
{
    private const int MaxTooltipLength = 127;

    public static TrayIconKind IconKind(RecorderStatus status) => status.State switch
    {
        RecorderState.Recording when status.Degradation != Degradation.None => TrayIconKind.RecordingDegraded,
        RecorderState.Recording => TrayIconKind.Recording,
        RecorderState.Error => TrayIconKind.Error,
        _ => TrayIconKind.Idle,
    };

    public static string Tooltip(RecorderStatus status, UiText text) => LimitTooltip(status.State switch
    {
        RecorderState.Recording => text.TooltipRecording(
            status.StartedAt?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "",
            DegradationSuffix(status.Degradation, text)),
        RecorderState.Error => text.TooltipError(text.ErrorReason(status.Error ?? RecorderError.Internal)),
        _ => text.TooltipIdle,
    });

    /// <summary>Kürzt auf die 127 Zeichen, die NotifyIcon.Text höchstens annimmt (länger wirft eine Ausnahme).</summary>
    public static string LimitTooltip(string text) =>
        text.Length <= MaxTooltipLength ? text : text[..MaxTooltipLength];

    public static string ToggleMenuText(RecorderStatus status, UiText text) =>
        status.State == RecorderState.Recording ? text.Stop : text.Start;

    public static string SampleRateMenuText(int sampleRate, UiText text) => text.SampleRateOption(sampleRate);

    private static string DegradationSuffix(Degradation degradation, UiText text)
    {
        var parts = new List<string>();
        if (degradation.HasFlag(Degradation.NoMicrophone)) parts.Add(text.NoMicrophone);
        if (degradation.HasFlag(Degradation.MicrophoneSilent)) parts.Add(text.MicrophoneSilent);
        if (degradation.HasFlag(Degradation.NoSystemAudio)) parts.Add(text.NoSystemAudio);
        return parts.Count == 0 ? "" : $" ({string.Join(", ", parts)})";
    }
}
