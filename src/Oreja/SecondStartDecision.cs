using Oreja.Infrastructure;

namespace Oreja;

/// <summary>Meldung eines zweiten Starts und was „Ja“ öffnet. MessageBox und Process.Start selbst stehen in Program.</summary>
public sealed record SecondStartDecision(string Message, string Target, MessageBoxIcon Icon, string LogLine)
{
    public const string TaskbarSettings = "ms-settings:taskbar";

    // Die Protokollzeile bleibt wie das ganze Protokoll deutsch.
    public static SecondStartDecision For(SecondStartState state, UiText text, string logPath) => state switch
    {
        SecondStartState.Ready => new(text.AlreadyRunning, TaskbarSettings, MessageBoxIcon.Information,
            "Zweiter Start: Oreja läuft bereits (Symbol bereit)"),
        SecondStartState.NotReady => new(text.StartupStuck, logPath, MessageBoxIcon.Warning,
            "Zweiter Start: Oreja läuft bereits (kein Symbol)"),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };
}
