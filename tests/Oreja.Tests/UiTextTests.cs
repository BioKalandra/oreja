using System.Globalization;
using Oreja.Recording;

namespace Oreja.Tests;

public sealed class UiTextTests
{
    public static TheoryData<string, string, string> SimpleTexts => new()
    {
        { nameof(UiText.Start), "Aufnahme starten", "Start recording" },
        { nameof(UiText.Stop), "Aufnahme stoppen", "Stop recording" },
        { nameof(UiText.OpenFolder), "Aufnahmeordner öffnen", "Open recordings folder" },
        { nameof(UiText.ChangeFolder), "Aufnahmeordner ändern…", "Change recordings folder…" },
        { nameof(UiText.ChooseFolderTitle), "Aufnahmeordner wählen", "Choose recordings folder" },
        { nameof(UiText.SampleRate), "Abtastrate", "Sample rate" },
        { nameof(UiText.Language), "Sprache", "Language" },
        { nameof(UiText.LanguageAuto), "Automatisch (Windows)", "Automatic (Windows)" },
        { nameof(UiText.LanguageGerman), "Deutsch", "Deutsch" },
        { nameof(UiText.LanguageEnglish), "English", "English" },
        { nameof(UiText.Autostart), "Mit Windows starten", "Start with Windows" },
        { nameof(UiText.Exit), "Beenden", "Exit" },
        { nameof(UiText.TooltipIdle), "Oreja – bereit", "Oreja – ready" },
        { nameof(UiText.NoMicrophone), "ohne Mikrofon", "no microphone" },
        { nameof(UiText.MicrophoneSilent), "Mikrofon stumm/gesperrt", "microphone muted/blocked" },
        { nameof(UiText.NoSystemAudio), "ohne Systemaudio", "no system audio" },
    };

    private static string Get(UiText text, string property) =>
        (string)typeof(UiText).GetProperty(property)!.GetValue(text)!;

    public static TheoryData<string, string> GermanTexts => ColumnOf(1);

    public static TheoryData<string, string> EnglishTexts => ColumnOf(2);

    private static TheoryData<string, string> ColumnOf(int column)
    {
        var data = new TheoryData<string, string>();
        foreach (var row in SimpleTexts)
            data.Add((string)row[0]!, (string)row[column]!);
        return data;
    }

    [Theory]
    [MemberData(nameof(GermanTexts))]
    public void GermanTextsMatchTable(string property, string german)
    {
        Assert.Equal(german, Get(UiText.German, property));
    }

    [Theory]
    [MemberData(nameof(EnglishTexts))]
    public void EnglishTextsMatchTable(string property, string english)
    {
        Assert.Equal(english, Get(UiText.English, property));
    }

    [Fact]
    public void GermanAndEnglishAreDistinctInstances()
    {
        Assert.NotSame(UiText.German, UiText.English);
    }

    [Theory]
    [InlineData(RecorderError.WriteFailed, "Schreibfehler", "write failed")]
    [InlineData(RecorderError.Internal, "Interner Fehler – siehe Protokoll", "internal error – see log")]
    [InlineData(RecorderError.NoDevice, "Kein Audiogerät verfügbar", "no audio device available")]
    public void ErrorReasonIsTranslated(RecorderError error, string german, string english)
    {
        Assert.Equal(german, UiText.German.ErrorReason(error));
        Assert.Equal(english, UiText.English.ErrorReason(error));
    }

    [Fact]
    public void TooltipRecordingFormatsTimeAndSuffix()
    {
        Assert.Equal("Oreja – Aufnahme seit 18:05", UiText.German.TooltipRecording("18:05", ""));
        Assert.Equal("Oreja – recording since 18:05", UiText.English.TooltipRecording("18:05", ""));
        Assert.Equal("Oreja – Aufnahme seit 18:05 (ohne Mikrofon)", UiText.German.TooltipRecording("18:05", " (ohne Mikrofon)"));
        Assert.Equal("Oreja – recording since 18:05 (no microphone)", UiText.English.TooltipRecording("18:05", " (no microphone)"));
    }

    [Fact]
    public void TooltipErrorEmbedsReason()
    {
        Assert.Equal("Oreja – Fehler: Schreibfehler", UiText.German.TooltipError("Schreibfehler"));
        Assert.Equal("Oreja – error: write failed", UiText.English.TooltipError("write failed"));
    }

    [Theory]
    [InlineData(16000, "16 kHz (Spracherkennung)", "16 kHz (speech recognition)")]
    [InlineData(44100, "44,1 kHz", "44.1 kHz")]
    [InlineData(48000, "48 kHz (Hörclips)", "48 kHz (audio clips)")]
    public void SampleRateOptionNamesTheRate(int rate, string german, string english)
    {
        Assert.Equal(german, UiText.German.SampleRateOption(rate));
        Assert.Equal(english, UiText.English.SampleRateOption(rate));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    public void SampleRateOptionIgnoresThreadCulture(string culture)
    {
        using var _ = new CultureScope(culture);

        Assert.Equal("44,1 kHz", UiText.German.SampleRateOption(44100));
        Assert.Equal("44.1 kHz", UiText.English.SampleRateOption(44100));
        Assert.Equal("16 kHz (Spracherkennung)", UiText.German.SampleRateOption(16000));
        Assert.Equal("48 kHz (audio clips)", UiText.English.SampleRateOption(48000));
    }

    [Theory]
    [InlineData("", "de-DE")]
    [InlineData("", "de-AT")]
    [InlineData("", "de-CH")]
    [InlineData(null, "de-DE")]
    [InlineData("xx", "de-DE")]
    [InlineData("EN", "de-DE")]
    [InlineData("auto", "de-AT")]
    [InlineData("de", "de-DE")]
    [InlineData("de", "en-US")]
    [InlineData("de", "fr-FR")]
    public void ResolveGivesGerman(string? setting, string uiCulture)
    {
        Assert.Same(UiText.German, UiText.Resolve(setting, CultureInfo.GetCultureInfo(uiCulture)));
    }

    [Theory]
    [InlineData("", "en-US")]
    [InlineData("", "en-GB")]
    [InlineData("", "fr-FR")]
    [InlineData("", "ja-JP")]
    [InlineData(null, "en-US")]
    [InlineData("xx", "en-US")]
    [InlineData("xx", "ja-JP")]
    [InlineData("en", "de-DE")]
    [InlineData("en", "de-AT")]
    [InlineData("en", "en-US")]
    [InlineData("en", "ja-JP")]
    public void ResolveGivesEnglish(string? setting, string uiCulture)
    {
        Assert.Same(UiText.English, UiText.Resolve(setting, CultureInfo.GetCultureInfo(uiCulture)));
    }

    [Fact]
    public void ResolveTreatsInvariantCultureAsEnglish()
    {
        Assert.Same(UiText.English, UiText.Resolve("", CultureInfo.InvariantCulture));
    }
}
