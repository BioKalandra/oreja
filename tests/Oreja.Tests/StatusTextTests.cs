using Oreja.Recording;

namespace Oreja.Tests;

public sealed class StatusTextTests
{
    private static readonly DateTime At1805 = new(2026, 10, 14, 18, 5, 12);
    private static readonly DateTime At1905 = new(2026, 10, 14, 19, 5, 0);

    [Fact]
    public void Idle()
    {
        Assert.Equal("Oreja – bereit", StatusText.Tooltip(RecorderStatus.Idle, UiText.German));
        Assert.Equal("Oreja – ready", StatusText.Tooltip(RecorderStatus.Idle, UiText.English));
        Assert.Equal(TrayIconKind.Idle, StatusText.IconKind(RecorderStatus.Idle));
        Assert.Equal("Aufnahme starten", StatusText.ToggleMenuText(RecorderStatus.Idle, UiText.German));
        Assert.Equal("Start recording", StatusText.ToggleMenuText(RecorderStatus.Idle, UiText.English));
    }

    [Fact]
    public void Recording()
    {
        var status = new RecorderStatus(RecorderState.Recording, StartedAt: At1805);

        Assert.Equal("Oreja – Aufnahme seit 18:05", StatusText.Tooltip(status, UiText.German));
        Assert.Equal("Oreja – recording since 18:05", StatusText.Tooltip(status, UiText.English));
        Assert.Equal(TrayIconKind.Recording, StatusText.IconKind(status));
        Assert.Equal("Aufnahme stoppen", StatusText.ToggleMenuText(status, UiText.German));
        Assert.Equal("Stop recording", StatusText.ToggleMenuText(status, UiText.English));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    public void RecordingTimeIs24HourRegardlessOfThreadCulture(string culture)
    {
        using var _ = new CultureScope(culture);
        var status = new RecorderStatus(RecorderState.Recording, StartedAt: At1905);

        Assert.Equal("Oreja – recording since 19:05", StatusText.Tooltip(status, UiText.English));
        Assert.Equal("Oreja – Aufnahme seit 19:05", StatusText.Tooltip(status, UiText.German));
    }

    [Fact]
    public void RecordingWithSeveralDegradations()
    {
        var status = new RecorderStatus(RecorderState.Recording, Degradation.NoMicrophone | Degradation.NoSystemAudio, At1805);

        Assert.Equal("Oreja – Aufnahme seit 18:05 (ohne Mikrofon, ohne Systemaudio)", StatusText.Tooltip(status, UiText.German));
        Assert.Equal("Oreja – recording since 18:05 (no microphone, no system audio)", StatusText.Tooltip(status, UiText.English));
        Assert.Equal(TrayIconKind.RecordingDegraded, StatusText.IconKind(status));
    }

    [Fact]
    public void RecordingWithSilentMicrophone()
    {
        var status = new RecorderStatus(RecorderState.Recording, Degradation.MicrophoneSilent, At1805);

        Assert.Equal("Oreja – Aufnahme seit 18:05 (Mikrofon stumm/gesperrt)", StatusText.Tooltip(status, UiText.German));
        Assert.Equal("Oreja – recording since 18:05 (microphone muted/blocked)", StatusText.Tooltip(status, UiText.English));
    }

    [Fact]
    public void RecordingWithOnlyNoSystemAudio()
    {
        var status = new RecorderStatus(RecorderState.Recording, Degradation.NoSystemAudio, At1805);

        Assert.Equal("Oreja – Aufnahme seit 18:05 (ohne Systemaudio)", StatusText.Tooltip(status, UiText.German));
        Assert.Equal("Oreja – recording since 18:05 (no system audio)", StatusText.Tooltip(status, UiText.English));
    }

    [Fact]
    public void RecordingWithAllDegradations()
    {
        var status = new RecorderStatus(
            RecorderState.Recording,
            Degradation.NoMicrophone | Degradation.MicrophoneSilent | Degradation.NoSystemAudio,
            At1805);

        Assert.Equal(
            "Oreja – Aufnahme seit 18:05 (ohne Mikrofon, Mikrofon stumm/gesperrt, ohne Systemaudio)",
            StatusText.Tooltip(status, UiText.German));
        Assert.Equal(
            "Oreja – recording since 18:05 (no microphone, microphone muted/blocked, no system audio)",
            StatusText.Tooltip(status, UiText.English));
    }

    [Theory]
    [InlineData(RecorderError.WriteFailed, "Oreja – Fehler: Schreibfehler", "Oreja – error: write failed")]
    [InlineData(RecorderError.Internal, "Oreja – Fehler: Interner Fehler – siehe Protokoll", "Oreja – error: internal error – see log")]
    [InlineData(RecorderError.NoDevice, "Oreja – Fehler: Kein Audiogerät verfügbar", "Oreja – error: no audio device available")]
    public void Error(RecorderError error, string german, string english)
    {
        var status = new RecorderStatus(RecorderState.Error, Error: error);

        Assert.Equal(german, StatusText.Tooltip(status, UiText.German));
        Assert.Equal(english, StatusText.Tooltip(status, UiText.English));
        Assert.Equal(TrayIconKind.Error, StatusText.IconKind(status));
        Assert.Equal("Aufnahme starten", StatusText.ToggleMenuText(status, UiText.German));
        Assert.Equal("Start recording", StatusText.ToggleMenuText(status, UiText.English));
    }

    // Tooltip() kürzt jedes Ergebnis über LimitTooltip auf das Windows-Limit. Bei den heutigen
    // Texten greift das nur defensiv (kein Text kommt in die Nähe von 127 Zeichen); die Kürzung
    // selbst wird deshalb direkt über LimitTooltip geprüft.
    [Fact]
    public void LimitTooltipTruncatesToWindowsLimit()
    {
        Assert.Equal(127, StatusText.LimitTooltip(new string('x', 300)).Length);
        Assert.Equal(new string('x', 127), StatusText.LimitTooltip(new string('x', 127)));
        Assert.Equal("kurz", StatusText.LimitTooltip("kurz"));
    }

    [Fact]
    public void RealTooltipsStayWithinWindowsLimit()
    {
        var all = new RecorderStatus(
            RecorderState.Recording,
            Degradation.NoMicrophone | Degradation.MicrophoneSilent | Degradation.NoSystemAudio,
            At1805);

        Assert.True(StatusText.Tooltip(all, UiText.German).Length <= 127);
        Assert.True(StatusText.Tooltip(all, UiText.English).Length <= 127);
    }

    [Fact]
    public void SampleRateMenuTextUsesGivenLanguage()
    {
        Assert.Equal("44,1 kHz", StatusText.SampleRateMenuText(44100, UiText.German));
        Assert.Equal("44.1 kHz", StatusText.SampleRateMenuText(44100, UiText.English));
        Assert.Equal("16 kHz (Spracherkennung)", StatusText.SampleRateMenuText(16000, UiText.German));
        Assert.Equal("48 kHz (audio clips)", StatusText.SampleRateMenuText(48000, UiText.English));
    }
}
