using Microsoft.Win32;
using Oreja.Infrastructure;
using Oreja.Recording;

namespace Oreja;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(initiallyOwned: true, @"Local\Oreja.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance) return;

        ApplicationConfiguration.Initialize();
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Oreja");
        var log = new Log(Path.Combine(appData, "oreja.log"));
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
        var backend = Task.Run(() => new WasapiBackend(log)).GetAwaiter().GetResult();
        var recorder = new Recorder(backend, new SystemClock(), path => LibFlacSink.Create(path), paths, log);

        using (var app = new TrayApp(recorder, settings, autostart, paths, mover, log))
        {
            Application.Run(app);
        }

        Task.Run(() =>
        {
            recorder.Dispose();
            backend.Dispose();
        }).Wait(TimeSpan.FromSeconds(3));
        log.Info("Oreja beendet");
    }
}
