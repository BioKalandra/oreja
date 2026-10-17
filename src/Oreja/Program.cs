using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;
using Oreja.Infrastructure;
using Oreja.Recording;

namespace Oreja;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Vor dem ersten Fenster, also auch vor der Meldung eines zweiten Starts (visuelle Stile, DPI).
        ApplicationConfiguration.Initialize();

        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Oreja");
        string logPath = Path.Combine(appData, "oreja.log");
        var log = new Log(logPath);

        SingleInstance? acquired;
        try
        {
            acquired = SingleInstance.TryAcquire();
        }
        catch (Exception ex)
        {
            // Z. B. UnauthorizedAccessException, wenn die erste Instanz mit Adminrechten läuft.
            log.Error("Einzelinstanz-Prüfung fehlgeschlagen, Start abgebrochen", ex);
            return;
        }

        using var singleInstance = acquired;
        if (singleInstance is null)
        {
            ReportSecondStart(log, logPath);
            return;
        }

        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        Application.ThreadException += (_, e) => log.Error("Unbehandelter Fehler (UI)", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => log.Error("Unbehandelter Fehler", e.ExceptionObject as Exception);
        log.Info($"Oreja {typeof(Program).Assembly.GetName().Version!.ToString(3)} gestartet");

        using var settingsKey = Registry.CurrentUser.CreateSubKey(@"Software\Oreja");
        using var runKey = Registry.CurrentUser.CreateSubKey(Autostart.RunKeyPath);
        var settings = new Settings(settingsKey);
        var autostart = new Autostart(runKey, "Oreja", Environment.ProcessPath!);
        if (!settings.IsInitialized)
        {
            autostart.Enable();
            settings.MarkInitialized();
        }
        else
        {
            autostart.RepairPath();
        }

        var paths = new OutputPaths(Path.Combine(appData, "recording"), () => settings.OutputFolder);
        var mover = new RecordingMover(paths, log);

        // WASAPI-Objekte müssen im MTA leben, deshalb entsteht das Backend auf einem Thread-Pool-Thread.
        // Hängt der Audiodienst, steht das im Protokoll („Audio-Initialisierung …“ ohne „fertig“).
        var backend = StartupStep.Run(log, "Audio-Initialisierung", () => new WasapiBackend(log), TimeSpan.FromSeconds(10));
        var recorder = new Recorder(backend, new SystemClock(), () => settings.SampleRate,
            (path, sampleRate) => LibFlacSink.Create(path, sampleRate), paths, log);

        using (var app = new TrayApp(recorder, settings, autostart, paths, mover, log, singleInstance.MarkReady))
        {
            Application.Run(app);
        }
        // Das Icon ist weg: Ein zweiter Start während des Aufräumens soll nicht auf ein Icon verweisen.
        singleInstance.MarkNotReady();

        Task.Run(() =>
        {
            recorder.Dispose();
            backend.Dispose();
        }).Wait(TimeSpan.FromSeconds(3));
        log.Info("Oreja beendet");
    }

    /// <summary>
    /// Oreja läuft schon: kurz auf das Icon der ersten Instanz warten und dann sagen, wo es steckt –
    /// oder dass ihr Start hängt.
    /// </summary>
    private static void ReportSecondStart(Log log, string logPath)
    {
        string? language = null; // automatisch, falls die Einstellung nicht lesbar ist
        try
        {
            using var settingsKey = Registry.CurrentUser.OpenSubKey(@"Software\Oreja");
            if (settingsKey is not null) language = new Settings(settingsKey).Language;
        }
        catch (Exception ex)
        {
            log.Error("Zweiter Start: Einstellungen nicht lesbar", ex);
        }
        var text = UiText.Resolve(language, CultureInfo.CurrentUICulture);

        var state = SingleInstance.WaitForFirstInstance(TimeSpan.FromSeconds(5));
        var decision = SecondStartDecision.For(state, text, logPath);
        log.Info(decision.LogLine);
        if (MessageBox.Show(decision.Message, "Oreja", MessageBoxButtons.YesNo, decision.Icon) != DialogResult.Yes) return;

        try
        {
            Process.Start(new ProcessStartInfo(decision.Target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            log.Error($"Zweiter Start: {decision.Target} konnte nicht geöffnet werden", ex);
        }
    }
}
