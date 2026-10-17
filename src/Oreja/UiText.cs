using System.Globalization;
using Oreja.Infrastructure;
using Oreja.Recording;

namespace Oreja;

/// <summary>
/// Alle Texte der Oberfläche in einer Sprache. Bewusst im Code statt in .resx-Dateien,
/// damit die Single-File-Exe ohne Satelliten-Assemblies auskommt.
/// </summary>
public sealed class UiText
{
    public static UiText German { get; } = new()
    {
        Start = "Aufnahme starten",
        Stop = "Aufnahme stoppen",
        OpenFolder = "Aufnahmeordner öffnen",
        ChangeFolder = "Aufnahmeordner ändern…",
        ChooseFolderTitle = "Aufnahmeordner wählen",
        SampleRate = "Abtastrate",
        Language = "Sprache",
        LanguageAuto = "Automatisch (Windows)",
        Autostart = "Mit Windows starten",
        Exit = "Beenden",
        TooltipIdle = "Oreja – bereit",
        NoMicrophone = "ohne Mikrofon",
        MicrophoneSilent = "Mikrofon stumm/gesperrt",
        NoSystemAudio = "ohne Systemaudio",
        RecordingSince = "Oreja – Aufnahme seit",
        ErrorPrefix = "Oreja – Fehler:",
        WriteFailed = "Schreibfehler",
        Internal = "Interner Fehler – siehe Protokoll",
        NoDevice = "Kein Audiogerät verfügbar",
        SpeechRecognition = "Spracherkennung",
        AudioClips = "Hörclips",
        Numbers = CultureInfo.GetCultureInfo("de-DE"),
    };

    public static UiText English { get; } = new()
    {
        Start = "Start recording",
        Stop = "Stop recording",
        OpenFolder = "Open recordings folder",
        ChangeFolder = "Change recordings folder…",
        ChooseFolderTitle = "Choose recordings folder",
        SampleRate = "Sample rate",
        Language = "Language",
        LanguageAuto = "Automatic (Windows)",
        Autostart = "Start with Windows",
        Exit = "Exit",
        TooltipIdle = "Oreja – ready",
        NoMicrophone = "no microphone",
        MicrophoneSilent = "microphone muted/blocked",
        NoSystemAudio = "no system audio",
        RecordingSince = "Oreja – recording since",
        ErrorPrefix = "Oreja – error:",
        WriteFailed = "write failed",
        Internal = "internal error – see log",
        NoDevice = "no audio device available",
        SpeechRecognition = "speech recognition",
        AudioClips = "audio clips",
        Numbers = CultureInfo.InvariantCulture,
    };

    private UiText() { }

    public required string Start { get; init; }
    public required string Stop { get; init; }
    public required string OpenFolder { get; init; }
    public required string ChangeFolder { get; init; }
    public required string ChooseFolderTitle { get; init; }
    public required string SampleRate { get; init; }
    public required string Language { get; init; }
    public required string LanguageAuto { get; init; }
    public required string Autostart { get; init; }
    public required string Exit { get; init; }
    public required string TooltipIdle { get; init; }
    public required string NoMicrophone { get; init; }
    public required string MicrophoneSilent { get; init; }
    public required string NoSystemAudio { get; init; }

    // Sprachnamen stehen immer in der eigenen Sprache, damit man sie auch in der falschen Oberfläche findet.
    public string LanguageGerman => "Deutsch";
    public string LanguageEnglish => "English";

    private string RecordingSince { get; init; } = "";
    private string ErrorPrefix { get; init; } = "";
    private string WriteFailed { get; init; } = "";
    private string Internal { get; init; } = "";
    private string NoDevice { get; init; } = "";
    private string SpeechRecognition { get; init; } = "";
    private string AudioClips { get; init; } = "";
    private CultureInfo Numbers { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Sprache zur Einstellung (<see cref="Settings.Language"/>): „de“ oder „en“ fest, sonst automatisch –
    /// Deutsch bei deutscher Windows-Sprache, in allen anderen Fällen Englisch.
    /// </summary>
    public static UiText Resolve(string? languageSetting, CultureInfo uiCulture) => languageSetting switch
    {
        Settings.LanguageGerman => German,
        Settings.LanguageEnglish => English,
        _ => uiCulture.TwoLetterISOLanguageName == "de" ? German : English,
    };

    public string TooltipRecording(string time, string suffix) => $"{RecordingSince} {time}{suffix}";

    public string TooltipError(string reason) => $"{ErrorPrefix} {reason}";

    public string ErrorReason(RecorderError error) => error switch
    {
        RecorderError.WriteFailed => WriteFailed,
        RecorderError.NoDevice => NoDevice,
        _ => Internal,
    };

    /// <summary>Menüeintrag einer Abtastrate, z. B. „44,1 kHz“; 16 und 48 kHz nennen zusätzlich ihren Zweck.</summary>
    public string SampleRateOption(int sampleRate)
    {
        string khz = $"{(sampleRate / 1000.0).ToString("0.#", Numbers)} kHz";
        return sampleRate switch
        {
            16000 => $"{khz} ({SpeechRecognition})",
            48000 => $"{khz} ({AudioClips})",
            _ => khz,
        };
    }
}
