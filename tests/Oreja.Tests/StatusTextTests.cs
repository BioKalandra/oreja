using Oreja.Recording;

namespace Oreja.Tests;

public sealed class StatusTextTests
{
    private static readonly DateTime At1805 = new(2026, 10, 14, 18, 5, 12);

    [Fact]
    public void Idle()
    {
        Assert.Equal("Oreja – bereit", StatusText.Tooltip(RecorderStatus.Idle));
        Assert.Equal(TrayIconKind.Idle, StatusText.IconKind(RecorderStatus.Idle));
        Assert.Equal("Aufnahme starten", StatusText.ToggleMenuText(RecorderStatus.Idle));
    }

    [Fact]
    public void Recording()
    {
        var status = new RecorderStatus(RecorderState.Recording, StartedAt: At1805);

        Assert.Equal("Oreja – Aufnahme seit 18:05", StatusText.Tooltip(status));
        Assert.Equal(TrayIconKind.Recording, StatusText.IconKind(status));
        Assert.Equal("Aufnahme stoppen", StatusText.ToggleMenuText(status));
    }

    [Fact]
    public void RecordingWithSeveralDegradations()
    {
        var status = new RecorderStatus(RecorderState.Recording, Degradation.NoMicrophone | Degradation.NoSystemAudio, At1805);

        Assert.Equal("Oreja – Aufnahme seit 18:05 (ohne Mikrofon, ohne Systemaudio)", StatusText.Tooltip(status));
        Assert.Equal(TrayIconKind.RecordingDegraded, StatusText.IconKind(status));
    }

    [Fact]
    public void RecordingWithSilentMicrophone()
    {
        var status = new RecorderStatus(RecorderState.Recording, Degradation.MicrophoneSilent, At1805);

        Assert.Equal("Oreja – Aufnahme seit 18:05 (Mikrofon stumm/gesperrt)", StatusText.Tooltip(status));
    }

    [Fact]
    public void Error()
    {
        var status = new RecorderStatus(RecorderState.Error, ErrorMessage: "Schreibfehler");

        Assert.Equal("Oreja – Fehler: Schreibfehler", StatusText.Tooltip(status));
        Assert.Equal(TrayIconKind.Error, StatusText.IconKind(status));
        Assert.Equal("Aufnahme starten", StatusText.ToggleMenuText(status));
    }

    [Fact]
    public void TooltipIsTruncatedToWindowsLimit()
    {
        var status = new RecorderStatus(RecorderState.Error, ErrorMessage: new string('x', 300));

        Assert.Equal(127, StatusText.Tooltip(status).Length);
    }
}
