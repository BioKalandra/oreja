using System.Diagnostics;
using Microsoft.Win32;
using Oreja.Infrastructure;
using Oreja.Recording;

namespace Oreja;

/// <summary>Das Tray-Icon: Linksklick schaltet die Aufnahme, Rechtsklick öffnet das Menü. Keine Fenster, keine Töne.</summary>
public sealed class TrayApp : ApplicationContext
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MoveTimeoutOnExit = TimeSpan.FromSeconds(10);

    private readonly Recorder _recorder;
    private readonly Settings _settings;
    private readonly Autostart _autostart;
    private readonly OutputPaths _paths;
    private readonly RecordingMover _mover;
    private readonly Log _log;
    private readonly SynchronizationContext _ui;
    private readonly TrayIcons _icons;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly ToolStripMenuItem _sampleRateItem;
    private readonly ClickGuard _clickGuard = new(TimeSpan.FromSeconds(1));
    private readonly object _movesGate = new();
    private Task _moves = Task.CompletedTask;
    private bool _busy;
    private bool _exiting;

    public TrayApp(Recorder recorder, Settings settings, Autostart autostart, OutputPaths paths, RecordingMover mover, Log log)
    {
        _recorder = recorder;
        _settings = settings;
        _autostart = autostart;
        _paths = paths;
        _mover = mover;
        _log = log;
        _ui = SynchronizationContext.Current ?? throw new InvalidOperationException("UI-SynchronizationContext fehlt");
        _icons = new TrayIcons(SystemInformation.SmallIconSize.Width);

        string version = typeof(TrayApp).Assembly.GetName().Version!.ToString(3);
        _toggleItem = new ToolStripMenuItem("Aufnahme starten", null, (_, _) => Toggle());
        _autostartItem = new ToolStripMenuItem("Mit Windows starten", null, (_, _) => ToggleAutostart())
        {
            Checked = _autostart.IsEnabled,
        };
        _sampleRateItem = new ToolStripMenuItem("Abtastrate");
        foreach (int rate in AudioFormat.SupportedSampleRates)
            _sampleRateItem.DropDownItems.Add(new ToolStripMenuItem(StatusText.SampleRateMenuText(rate), null, (_, _) => ChangeSampleRate(rate)) { Tag = rate });
        RenderSampleRate();
        var menu = new ContextMenuStrip();
        menu.Items.AddRange(
        [
            new ToolStripMenuItem($"Oreja {version}") { Enabled = false },
            new ToolStripSeparator(),
            _toggleItem,
            new ToolStripMenuItem("Aufnahmeordner öffnen", null, (_, _) => OpenOutputFolder()),
            new ToolStripMenuItem("Aufnahmeordner ändern…", null, (_, _) => ChangeOutputFolder()),
            _sampleRateItem,
            _autostartItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Beenden", null, (_, _) => Exit()),
        ]);

        _notifyIcon = new NotifyIcon { ContextMenuStrip = menu };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) Toggle();
        };

        _recorder.StatusChanged += status => _ui.Post(_ => Render(status), null);
        _recorder.RecordingFinished += _ => QueueMove();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionEnding += OnSessionEnding;

        Render(_recorder.Status);
        _notifyIcon.Visible = true;
        QueueMove(); // Reste nach Absturz oder nicht erreichbarem Zielordner
    }

    private void Render(RecorderStatus status)
    {
        if (_exiting) return;
        _notifyIcon.Icon = _icons[StatusText.IconKind(status)];
        _notifyIcon.Text = StatusText.Tooltip(status);
        _toggleItem.Text = StatusText.ToggleMenuText(status);
        _sampleRateItem.Enabled = status.State != RecorderState.Recording; // gilt erst ab der nächsten Aufnahme
    }

    private void RenderSampleRate()
    {
        int current = _settings.SampleRate;
        foreach (ToolStripMenuItem item in _sampleRateItem.DropDownItems)
            item.Checked = (int)item.Tag! == current;
    }

    private async void Toggle()
    {
        if (_busy || _exiting || !_clickGuard.TryAccept(Environment.TickCount64)) return;
        _busy = true;
        try
        {
            if (_recorder.Status.State == RecorderState.Recording) await Task.Run(_recorder.Stop);
            else await Task.Run(_recorder.Start);
        }
        catch (Exception ex)
        {
            _log.Error("Umschalten der Aufnahme fehlgeschlagen", ex);
            Render(new RecorderStatus(RecorderState.Error, ErrorMessage: "Interner Fehler – siehe Protokoll"));
        }
        finally
        {
            _busy = false;
        }
    }

    private void QueueMove()
    {
        lock (_movesGate)
        {
            _moves = _moves.ContinueWith(_ =>
            {
                try
                {
                    _mover.MovePending();
                }
                catch (Exception ex)
                {
                    _log.Error("Verschieben fehlgeschlagen", ex);
                }
            }, TaskScheduler.Default);
        }
    }

    private void OpenOutputFolder()
    {
        try
        {
            string folder = _paths.TargetFolder;
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _log.Error("Aufnahmeordner konnte nicht geöffnet werden", ex);
        }
    }

    private void ChangeOutputFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Aufnahmeordner wählen",
            UseDescriptionForTitle = true,
            SelectedPath = _settings.OutputFolder,
            ShowNewFolderButton = true,
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;

        _settings.OutputFolder = dialog.SelectedPath;
        _log.Info($"Aufnahmeordner geändert: {dialog.SelectedPath}");
        QueueMove();
    }

    private void ChangeSampleRate(int sampleRate)
    {
        try
        {
            _settings.SampleRate = sampleRate;
            _log.Info($"Abtastrate geändert: {sampleRate} Hz");
        }
        catch (Exception ex)
        {
            _log.Error("Abtastrate konnte nicht gespeichert werden", ex);
        }
        RenderSampleRate();
    }

    private void ToggleAutostart()
    {
        try
        {
            if (_autostart.IsEnabled) _autostart.Disable();
            else _autostart.Enable();
        }
        catch (Exception ex)
        {
            _log.Error("Autostart konnte nicht geändert werden", ex);
        }
        _autostartItem.Checked = _autostart.IsEnabled;
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) StopAndWait("Standby");
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e) => StopAndWait("Abmelden/Herunterfahren");

    private void StopAndWait(string reason)
    {
        if (_recorder.Status.State != RecorderState.Recording) return;
        _log.Info($"Aufnahme wird gestoppt: {reason}");
        if (!Task.Run(_recorder.Stop).Wait(StopTimeout))
            _log.Error($"Stoppen dauerte länger als {StopTimeout.TotalSeconds} s");
    }

    private void Exit()
    {
        if (_exiting) return;
        StopAndWait("Beenden");
        _exiting = true;

        Task moves;
        lock (_movesGate) moves = _moves;
        if (!moves.Wait(MoveTimeoutOnExit))
            _log.Info("Verschieben beim Beenden abgebrochen, wird beim nächsten Start fortgesetzt");

        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionEnding -= OnSessionEnding;
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _icons.Dispose();
        }
        base.Dispose(disposing);
    }
}
