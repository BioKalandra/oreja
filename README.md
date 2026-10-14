# Oreja

Kleines Windows-Tray-Tool, das Mikrofon und Systemaudio gemischt als FLAC-Datei aufnimmt
(16 kHz, Mono) – gedacht für die spätere Transkription mit Whisper.

## Bedienung

- **Linksklick** aufs Tray-Icon: Aufnahme starten bzw. stoppen
- **Rechtsklick:** Menü mit Aufnahmeordner öffnen/ändern, Autostart, Beenden
- **Icons:** grauer Ring = bereit, roter Kreis = Aufnahme, roter Kreis mit gelbem Punkt =
  Aufnahme mit Einschränkung (Tooltip nennt den Grund), grauer Ring mit gelbem Dreieck = Fehler

Aufnahmen entstehen in `%LOCALAPPDATA%\Oreja\recording\` und werden nach dem Stoppen in den
Aufnahmeordner verschoben (Voreinstellung `Dokumente\Oreja`). Protokoll: `%LOCALAPPDATA%\Oreja\oreja.log`.

## Hinweise

- **Windows 11:** Das Icon einmalig unter *Einstellungen → Personalisierung → Taskleiste →
  Andere Symbole der Taskleiste* sichtbar machen.
- **Headset statt Lautsprecher** verwenden, sonst nimmt das Mikrofon den Ton ein zweites Mal auf.
- **Headset-Mikro als Windows-Standard** einstellen, als „Standardgerät“ und als
  „Standardkommunikationsgerät“. Systemaudio wird von allen Wiedergabegeräten aufgenommen.
- **Google Drive:** Google Drive für Desktop installieren und einen Ordner darin als
  Aufnahmeordner wählen. Dort landen nur vollständige Dateien.
- **Transkription:** Bei langen Pausen hilft der VAD-Filter, z. B. `vad_filter=True` in faster-whisper.
- Gespräche mit anderen nur mit deren Einverständnis aufnehmen (§ 201 StGB).

## Entwickeln

- Voraussetzung: .NET 10 SDK
- Tests: `dotnet test`
- Exe bauen: `dotnet publish src/Oreja/Oreja.csproj -p:PublishProfile=win-x64` → `publish\Oreja.exe`
- Manuelle Abnahme: `docs/manual-tests.md`

## Lizenzen

Oreja steht unter der MIT-Lizenz (siehe `LICENSE`). Drittkomponenten (NAudio, libFLAC) siehe `THIRD-PARTY-NOTICES.md`.
