using System.Windows.Forms;

namespace Oreja.Tests;

public sealed class SecondStartDecisionTests
{
    private const string LogPath = @"C:\Users\test\AppData\Local\Oreja\oreja.log";

    [Fact]
    public void ReadyShowsAlreadyRunningAndOffersTaskbarSettings()
    {
        var decision = SecondStartDecision.For(Infrastructure.SecondStartState.Ready, UiText.German, LogPath);

        Assert.Equal(UiText.German.AlreadyRunning, decision.Message);
        Assert.Equal("ms-settings:taskbar", decision.Target);
        Assert.Equal(MessageBoxIcon.Information, decision.Icon);
        Assert.Equal("Zweiter Start: Oreja läuft bereits (Symbol bereit)", decision.LogLine);
    }

    [Fact]
    public void NotReadyShowsStartupStuckAndOffersTheLog()
    {
        var decision = SecondStartDecision.For(Infrastructure.SecondStartState.NotReady, UiText.German, LogPath);

        Assert.Equal(UiText.German.StartupStuck, decision.Message);
        Assert.Equal(LogPath, decision.Target);
        Assert.Equal(MessageBoxIcon.Warning, decision.Icon);
        Assert.Equal("Zweiter Start: Oreja läuft bereits (kein Symbol)", decision.LogLine);
    }

    [Fact]
    public void MessageFollowsTheLanguageButLogLineStaysGerman()
    {
        var ready = SecondStartDecision.For(Infrastructure.SecondStartState.Ready, UiText.English, LogPath);
        var stuck = SecondStartDecision.For(Infrastructure.SecondStartState.NotReady, UiText.English, LogPath);

        Assert.Equal(UiText.English.AlreadyRunning, ready.Message);
        Assert.Equal(UiText.English.StartupStuck, stuck.Message);
        Assert.Equal("Zweiter Start: Oreja läuft bereits (Symbol bereit)", ready.LogLine);
        Assert.Equal("Zweiter Start: Oreja läuft bereits (kein Symbol)", stuck.LogLine);
    }
}
