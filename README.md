# Oreja

**English:** Oreja is a small Windows tray tool that records your microphone and system audio,
mixed into one FLAC file, for later transcription with Whisper. The menus follow the Windows
display language: German on German Windows, English otherwise. To switch, right-click the tray
icon and choose *Language* (*Sprache*).

Kleines Windows-Tray-Tool, das Mikrofon und Systemaudio gemischt als FLAC-Datei aufnimmt
(Mono, Abtastrate einstellbar: 16 / 44,1 / 48 kHz) – gedacht für die spätere Transkription mit Whisper.

## Bedienung

- **Linksklick** aufs Tray-Icon: Aufnahme starten bzw. stoppen
- **Rechtsklick:** Menü mit Aufnahmeordner öffnen/ändern, Abtastrate, Sprache, Autostart, Beenden
- **Icons:** grauer Ring = bereit, roter Kreis = Aufnahme, roter Kreis mit gelbem Punkt =
  Aufnahme mit Einschränkung (Tooltip nennt den Grund), grauer Ring mit gelbem Dreieck = Fehler

Aufnahmen entstehen in `%LOCALAPPDATA%\Oreja\recording\` und werden nach dem Stoppen in den
Aufnahmeordner verschoben (Voreinstellung `Dokumente\Oreja`). Protokoll: `%LOCALAPPDATA%\Oreja\oreja.log`.

## Hinweise

- **Speicherort:** Die Exe auf die lokale Platte legen, z. B. nach `%LOCALAPPDATA%\Programs\Oreja\`,
  nicht auf OneDrive, Google Drive, ein Netzlaufwerk oder einen USB-Stick.
- **Windows 11:** Das Icon einmalig unter *Einstellungen → Personalisierung → Taskleiste →
  Andere Symbole der Taskleiste* sichtbar machen. Windows merkt sich das pro Exe-Pfad
  (`HKCU\Control Panel\NotifyIconSettings`) – nach dem Verschieben der Exe das Icon dort neu
  einblenden.
- **Headset statt Lautsprecher** verwenden, sonst nimmt das Mikrofon den Ton ein zweites Mal auf.
- **Headset-Mikro als Windows-Standard** einstellen, als „Standardgerät“ und als
  „Standardkommunikationsgerät“. Systemaudio wird von allen Wiedergabegeräten aufgenommen.
- **Google Drive:** Google Drive für Desktop installieren und einen Ordner darin als
  Aufnahmeordner wählen. Dort landen nur vollständige Dateien.
- **Abtastrate:** 16 kHz (Voreinstellung) genügt der Spracherkennung. 44,1 oder 48 kHz behalten
  auch die Höhen, etwa für Hörclips, und ergeben deutlich größere Dateien (bis etwa dreimal so
  groß). Die Wahl gilt ab der nächsten Aufnahme; während einer Aufnahme ist das Menü gesperrt.
- **Sprache:** Die Oberfläche folgt der Windows-Sprache – Deutsch bei deutschem Windows, sonst
  Englisch. Im Menü „Sprache“ lässt sie sich fest auf Deutsch oder English stellen; die Wahl wirkt
  sofort und bleibt erhalten. Das Protokoll bleibt immer deutsch.
- **Transkription:** Bei langen Pausen hilft der VAD-Filter, z. B. `vad_filter=True` in faster-whisper.
- Gespräche mit anderen nur mit deren Einverständnis aufnehmen (§ 201 StGB).

## Fehlersuche

- **Kein Icon zu sehen?** Oreja einfach noch einmal starten. Läuft es schon, meldet der zweite Start
  das und bietet an, die Taskleisten-Einstellungen zu öffnen. Hat die laufende Oreja ihr Icon nach
  5 Sekunden noch nicht angelegt, hängt vermutlich ihr Start; dann bietet die Meldung das Protokoll an.
- **Protokoll** (`%LOCALAPPDATA%\Oreja\oreja.log`): „Audio-Initialisierung …“, „Audio-Initialisierung
  fertig (… ms)“ und „Tray-Icon bereit“ zeigen, wie weit der Start gekommen ist. Folgt auf die letzte
  Zeile „Audio-Initialisierung …“ kein „fertig“ (höchstens „dauert ungewöhnlich lange“), hängt der
  Start beim Windows-Audiodienst.

## Entwickeln

- Voraussetzung: .NET 10 SDK
- Tests: `dotnet test`
- Exe bauen: `dotnet publish src/Oreja/Oreja.csproj -p:PublishProfile=win-x64` → `publish\Oreja.exe`
- Manuelle Abnahme: `docs/manual-tests.md`

## Lizenzen

Oreja steht unter der MIT-Lizenz (siehe `LICENSE`). Drittkomponenten (NAudio, libFLAC) siehe `THIRD-PARTY-NOTICES.md`.
