# Oreja – Design

- **Datum:** 2026-10-14
- **Status:** Entwurf zur Prüfung

## Ziel

Oreja ist ein kleines Windows-Tool, das OBS als reines Audio-Aufnahmewerkzeug ersetzt. Es liegt unauffällig als Icon im System-Tray. Ein Klick startet eine Aufnahme von Mikrofon und Systemaudio, ein weiterer Klick stoppt sie. Das Ergebnis ist eine FLAC-Datei, die später mit Whisper transkribiert wird (z. B. Spanischunterricht, Teams-Calls, Videos).

**Erfolgskriterien**

- Eine Aufnahme ist mit einem Klick gestartet und mit einem Klick gestoppt, ohne Töne, Fenster oder Benachrichtigungen.
- Am Tray-Icon ist jederzeit erkennbar, ob gerade aufgenommen wird und ob die Aufnahme vollständig ist.
- Whisper erkennt in der Aufnahme sowohl die eigene Stimme (Mikrofon) als auch die Gegenseite (Systemaudio), egal auf welches Wiedergabegerät eine App ihren Ton schickt.
- Ein Absturz oder Stromausfall kostet höchstens die letzten Sekunden einer Aufnahme.
- Der Aufnahmeordner ist frei wählbar. Liegt er in Google Drive für Desktop (oder einem anderen Sync-Ordner), landen dort nur vollständige Dateien.
- Das Tool läuft auf einem beliebigen Windows-10/11-Rechner ohne Installation von Abhängigkeiten.

## Nicht-Ziele (Version 1)

- Transkription innerhalb des Tools
- Echounterdrückung (bei Lautsprecherbetrieb wird der Ton doppelt aufgenommen; Empfehlung: Headset)
- Geräteauswahl, Einstellungsdialog (einzige Einstellung ist der Aufnahmeordner), Hotkeys, Pause-Funktion
- Eigene Cloud-Anbindung: Google Drive wird über Google Drive für Desktop und die Wahl eines Ordners darin genutzt
- Getrennte Spuren für Mikrofon und Systemaudio
- Native ARM64-Version (die x64-Exe läuft unter Windows on ARM per Emulation)
- Paketierung für winget/Chocolatey, CI-Pipeline, Code-Signing (siehe Folgeschritte)

## Grundsatzentscheidungen

| Thema | Entscheidung | Begründung |
|---|---|---|
| Sprache/Plattform | C# mit .NET 10 (LTS), WinForms nur für `NotifyIcon` | ausgereifte WASAPI-Unterstützung über NAudio, Tray-Icon ist Windows-Standard |
| Audio-Bibliothek | NAudio 3.1 (Paket `NAudio.Wasapi`) | WASAPI-Capture, Loopback, Geräte-Enumeration und -Benachrichtigungen, Resampler |
| FLAC-Encoder | libFLAC 1.5.0 (offizielles Windows-Build von Xiph) per P/Invoke, DLL in die Exe eingebettet | Der in Windows eingebaute FLAC-Encoder unterstützt erst ab 44,1 kHz (geprüft am 2026-10-14); libFLAC erfüllt alle Kriterien (siehe „FLAC-Encoder“) |
| Audioformat | FLAC, 16 kHz, Mono, 16 Bit | Whisper verarbeitet intern 16 kHz Mono, mehr bringt für die Erkennung nichts; ca. 50–70 MB pro Stunde |
| Quellen | Windows-Standardmikrofon + Loopback **aller** aktiven Wiedergabegeräte, gemischt in eine Spur | Ton von Teams & Co. landet in der Aufnahme, auch wenn die App ein anderes Gerät als den Windows-Standard nutzt |
| Ablage | Aufnahme lokal in einen Arbeitsordner, nach dem Stoppen Verschieben in den einstellbaren Zielordner | Sync-Clients sehen nur fertige Dateien; Aufnahme funktioniert auch, wenn der Zielordner gerade nicht erreichbar ist |
| Auslieferung | Self-contained Single-File-Exe (`win-x64`), komprimiert | läuft ohne installierte .NET-Runtime; Grundlage für eine spätere winget-Portable-Paketierung |
| Mindest-OS | Windows 10 22H2, Windows 11 | |
| UI-Sprache | Deutsch | |

## Architektur

Eine einzelne Desktop-App ohne Hauptfenster. Jede Komponente hat eine klar umrissene Aufgabe:

| Komponente | Aufgabe | Abhängigkeiten |
|---|---|---|
| `Program` | Einstiegspunkt, Single-Instance-Prüfung, startet die `TrayApp` | – |
| `TrayApp` | Tray-Icon, Tooltip, Linksklick (mit Klickschutz), Kontextmenü, Ordnerauswahl, Reaktion auf Herunterfahren/Standby. Kennt nur Zustände, nichts über Audio. | `Recorder`, `Autostart`, `Settings`, `OutputPaths`, `RecordingMover` |
| `Recorder` | Steuert genau eine Aufnahme (`Start()`, `Stop()`), baut die Pipeline auf und ab, meldet Zustandswechsel per Event | `AudioMixer`, `IFlacSink`, `IAudioBackend` |
| `AudioMixer` | Taktgeber und Mischer: holt im festen Takt Samples aus allen Quellen-FIFOs, füllt Lücken mit Stille, mischt, begrenzt und gibt 16-Bit-PCM an den Sink | `IClock`, `SampleFifo` |
| `SampleFifo` | Jitter-Puffer pro Quelle im Zielformat (16 kHz Mono Float) | – |
| `IAudioSource` | Eine Quelle (Mikrofon oder Loopback eines Geräts). Wandelt im Capture-Thread in das Zielformat um und schreibt in ihren `SampleFifo`. | NAudio |
| `IAudioBackend` (`WasapiBackend`) | Listet Geräte auf, erzeugt Quellen und meldet hinzugekommene/entfernte Wiedergabegeräte sowie Wechsel des Standardmikrofons (übernimmt die Rolle des „DeviceWatcher“) | NAudio (`MMDeviceNotificationClient`) |
| `MicSilenceDetector` | Erkennt ein stummgeschaltetes oder gesperrtes Mikrofon (nur digitale Nullen oder gar keine Daten) | `IClock` |
| `IFlacSink` (`LibFlacSink`) | Schreibt 16-Bit-PCM fortlaufend als FLAC-Datei | libFLAC |
| `OutputPaths` | Arbeitsordner, Zielordner und Dateinamen | `Settings` |
| `RecordingMover` | Verschiebt fertige Aufnahmen vom Arbeitsordner in den Zielordner | `OutputPaths` |
| `Settings` | Liest und schreibt die Einstellungen unter `HKCU\Software\Oreja` (Zielordner, Erststart-Marker) | Registry-Schlüssel injizierbar |
| `Autostart` | Liest und schreibt den Autostart-Eintrag in der Registry | Registry-Schlüssel injizierbar |
| `Log` | Einfaches Dateiprotokoll mit Rotation | – |
| `IClock` | Zeitquelle, im Test simulierbar | – |

**Projektstruktur**

```
oreja/
  Oreja.slnx
  src/Oreja/              net10.0-windows, WinForms
    Program.cs, TrayApp.cs, TrayIcons.cs, StatusText.cs, ClickGuard.cs
    Recording/            Recorder, AudioMixer, SampleFifo, SampleConverter, IAudioSource,
                          IAudioBackend + WasapiBackend/WasapiSource, MicSilenceDetector,
                          IFlacSink + LibFlacSink
    Infrastructure/       Settings, OutputPaths, RecordingMover, Autostart, Log, IClock
    native/               libFLAC.dll + Lizenztext
  tests/Oreja.Tests/      xUnit
  docs/
    manual-tests.md
```

**Threads**

- Capture-Threads (von NAudio/WASAPI): wandeln ins Zielformat um und schreiben in den FIFO ihrer Quelle.
- Ein Taktgeber-Thread im `AudioMixer`: liest aus den FIFOs, mischt, schreibt in den `IFlacSink`.
- UI-Thread (WinForms): Tray-Icon und Menü. Events des `Recorder` werden über den `SynchronizationContext` auf den UI-Thread gebracht.
- WASAPI-Objekte werden nur auf MTA-Threads (Thread-Pool) erzeugt und benutzt, nie auf dem UI-Thread.
- Gerätebenachrichtigungen kommen auf einem COM-Thread. Sie werden nur weitergereicht; der Umbau der Quellen geschieht außerhalb des Callbacks.

## Audio-Pipeline

```
Mikrofon (Standard) ──────► Umwandlung ──► FIFO ──┐
Loopback Wiedergabegerät 1 ► Umwandlung ──► FIFO ──┼──► Mischen ─► Begrenzen ─► 16 Bit ─► FLAC-Datei
Loopback Wiedergabegerät n ► Umwandlung ──► FIFO ──┘
                                                  ▲
                                   Taktgeber (alle 50 ms, nach Echtzeit)
```

**Umwandlung (pro Quelle, im Capture-Thread)**

1. Gerätformat (z. B. 48 kHz Stereo Float) in Float-Samples wandeln.
2. Auf Mono heruntermischen (Mittelwert der Kanäle).
3. Auf 16 kHz resamplen (WDL-Resampler aus NAudio).
4. In den `SampleFifo` der Quelle schreiben.

**Taktgeber und Jitter-Puffer**

- Der Taktgeber läuft alle 50 ms und berechnet anhand der verstrichenen Echtzeit seit Aufnahmestart, wie viele Samples insgesamt fällig sind. Die Differenz zu den bereits geschriebenen Samples wird in diesem Takt erzeugt. Die Datei ist dadurch immer so lang wie die echte Aufnahmedauer.
- Jeder `SampleFifo` hat zwei Zustände:
  - **Füllen:** Die Quelle liefert Stille, bis 200 ms im Puffer liegen. Dann wechselt sie zu „Abspielen“.
  - **Abspielen:** Die fälligen Samples werden aus dem Puffer gelesen. Läuft der Puffer dabei leer, wird der Rest mit Stille aufgefüllt und die Quelle wechselt zurück zu „Füllen“.
- Liegt mehr als 1 s im Puffer (Taktdrift oder hängender Taktgeber), werden die ältesten Samples verworfen, bis wieder 200 ms im Puffer liegen. Das wird protokolliert.
- Folge: Alle Quellen haben dieselbe Verzögerung von etwa 200 ms und bleiben damit zueinander synchron. Loopback-Quellen, die in Pausen gar keine Daten liefern, erzeugen Stille. Taktdrift zwischen Geräten (typisch < 0,2 s pro Stunde) wird abgefangen, ohne dass Audiodaten verloren gehen, außer im Überlauffall.

**Mischen**

- Die Samples aller Quellen werden addiert und auf den Bereich [-1, 1] begrenzt.
- Anschließend Umwandlung in 16-Bit-PCM.

**Quellen und Geräte**

- **Mikrofon:** Windows-Standardaufnahmegerät (Rolle „Console“). Wechselt der Standard während der Aufnahme, wird auf das neue Gerät umgeschaltet; bis dahin Stille.
- **Systemaudio:** Je eine Loopback-Quelle für jedes aktive Wiedergabegerät. Geräte, die während der Aufnahme hinzukommen, werden in die laufende Aufnahme aufgenommen; entfernte Geräte fallen still weg. Kann ein Gerät nicht per Loopback geöffnet werden (z. B. weil eine App es exklusiv nutzt), wird es übersprungen und protokolliert.

**FLAC-Encoder**

Kriterien für den Encoder:

1. Er erzeugt eine gültige FLAC-Datei mit 16 kHz, Mono, 16 Bit.
2. Er schreibt fortlaufend auf die Platte: Die Datei wächst während der Aufnahme, und nach einem harten Prozessabbruch ist sie mit ffmpeg bis auf die letzten Sekunden lesbar.
3. Er funktioniert in der self-contained Single-File-Exe.

Ergebnis der Prüfung vom 2026-10-14:

- **Windows-Encoder (Media Foundation):** erfüllt Kriterium 1 nicht. Er bietet nur Abtastraten ab 44,1 kHz an.
- **libFLAC 1.5.0** (offizielles Windows-Build `flac-1.5.0-win.zip` von Xiph, BSD-Lizenz) per P/Invoke erfüllt alle drei:
  - Roundtrip bitgenau, der Windows-Decoder liest die Datei.
  - Die Datei wächst während der Aufnahme. Nach einem harten Abbruch fehlten nur rund 0,2 s, ffmpeg las den Rest.
  - Die DLL läuft eingebettet in der self-contained Single-File-Exe.
- **Umsetzung:** libFLAC schreibt über Callbacks in einen .NET-`FileStream`. Dadurch sind auch Pfade mit Umlauten unproblematisch, und nach jedem FLAC-Frame wird geflusht.

**Stoppen**

Captures beenden → Taktgeber schreibt die letzten fälligen Samples → `IFlacSink` schließt die Datei ab (Kopfdaten mit Gesamtlänge aktualisieren) → Datei schließen. Das Stoppen muss innerhalb von 2 s abgeschlossen sein, damit es auch beim Herunterfahren zuverlässig klappt. Das anschließende Verschieben in den Zielordner (siehe „Ablage“) läuft im Hintergrund und zählt nicht zu diesen 2 s.

## Tray-Verhalten

**Zustände**

| Zustand | Icon | Tooltip |
|---|---|---|
| Bereit | grauer Ring | „Oreja – bereit“ |
| Aufnahme | roter gefüllter Kreis | „Oreja – Aufnahme seit 18:05“ |
| Aufnahme mit Einschränkung | roter Kreis mit gelbem Punkt unten rechts | „Oreja – Aufnahme seit 18:05 (ohne Mikrofon)“ |
| Fehler | grauer Ring mit gelbem Warndreieck | „Oreja – Fehler: Kein Audiogerät verfügbar“ |

Die Icons werden beim Start in der aktuellen Tray-Icon-Größe gezeichnet (keine Bilddateien nötig, scharf bei jeder Skalierung) und sind auf heller und dunkler Taskleiste gut erkennbar. Der Tooltip wird beim Zustandswechsel gesetzt, ein laufender Timer ist nicht nötig. Bestehen mehrere Einschränkungen gleichzeitig, nennt der Tooltip alle, durch Komma getrennt.

**Bedienung**

- **Linksklick:** Bereit/Fehler → Aufnahme starten. Aufnahme → Aufnahme stoppen. Klicks innerhalb von 1 s nach dem letzten Umschalten werden ignoriert.
- **Rechtsklick-Menü:**
  - „Oreja 0.1.0“ (Versionsanzeige, nicht anklickbar)
  - „Aufnahme starten“ bzw. „Aufnahme stoppen“
  - „Aufnahmeordner öffnen“ (öffnet den Zielordner im Explorer)
  - „Aufnahmeordner ändern…“ (Windows-Ordnerauswahldialog; einziges Fenster der App, erscheint nur auf Aufruf)
  - „Mit Windows starten“ (mit Häkchen)
  - „Beenden“
- Keine Töne, keine Fenster, keine Benachrichtigungen, auch nicht bei Fehlern.

**Sonderfälle**

- **Beenden während einer Aufnahme:** Die Aufnahme wird sauber abgeschlossen, dann beendet sich die App. Ein laufendes Verschieben in den Zielordner darf noch höchstens 10 s weiterlaufen. Danach wird es abgebrochen, und der nächste Start holt es nach. Durch den Umweg über `.tmp` entsteht dabei keine halbe Datei im Zielordner.
- **Herunterfahren/Abmelden während einer Aufnahme:** Die Aufnahme wird im Arbeitsordner sauber abgeschlossen. Das Verschieben in den Zielordner holt der nächste Start nach.
- **Standby/Ruhezustand während einer Aufnahme:** Die Aufnahme wird vorher sauber gestoppt. Nach dem Aufwachen ist die App im Zustand „Bereit“.
- **Zweiter Start:** Läuft Oreja bereits (benannter Mutex pro Benutzersitzung), beendet sich der zweite Prozess ohne Meldung.

**Autostart**

- Eintrag `Oreja` unter `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, Wert ist der Pfad der laufenden Exe (`Environment.ProcessPath`) in Anführungszeichen.
- Beim allerersten Start wird Autostart eingeschaltet. Der erste Start wird über den Wert `Initialized` unter `HKCU\Software\Oreja` erkannt.
- Bei jedem Start: Existiert der Autostart-Eintrag, zeigt aber auf einen anderen Pfad (z. B. weil die Exe verschoben wurde), wird er auf den aktuellen Pfad aktualisiert.

**Einstellungen**

- Einzige Einstellung ist der Zielordner, gespeichert als Wert `OutputFolder` unter `HKCU\Software\Oreja`. Es gibt keine Einstellungsdatei.
- Voreinstellung (Wert fehlt): `<Dokumente>\Oreja\`.
- Eine Änderung über „Aufnahmeordner ändern…“ gilt ab dem nächsten Verschieben, also auch für eine gerade laufende Aufnahme.

**Ablage**

- **Arbeitsordner:** `%LOCALAPPDATA%\Oreja\recording\`. Hier entsteht die Datei während der Aufnahme.
- **Zielordner:** der eingestellte Ordner, wird bei Bedarf angelegt.
- **Dateiname:** `yyyy-MM-dd_HH-mm-ss.flac` (lokale Zeit des Aufnahmestarts). Ist der Name im Arbeits- oder Zielordner bereits vergeben, wird `_2`, `_3` usw. angehängt. Ist er beim Verschieben im Zielordner inzwischen vergeben, wird dort erneut ein Suffix vergeben.
- **Verschieben:** Nach jedem Stoppen und bei jedem Start der App werden alle Dateien im Arbeitsordner außer der gerade aufgenommenen in den Zielordner verschoben. Jede Datei wird dazu unter dem temporären Namen `<name>.flac.tmp` in den Zielordner kopiert, dort auf den endgültigen Namen umbenannt und erst danach lokal gelöscht. Sync-Clients wie Google Drive sehen dadurch nur vollständige Dateien.
- **Zielordner nicht erreichbar** (z. B. Laufwerk `G:` noch nicht verbunden, USB-Stick abgezogen): Die Datei bleibt im Arbeitsordner, das wird protokolliert, und beim nächsten Stoppen oder Start wird es erneut versucht. Das Icon ändert sich nicht, da nichts verloren geht. Eine übrig gebliebene `.tmp`-Datei im Zielordner wird beim nächsten Versuch überschrieben.

**Vom System vorgegeben (Hinweise, keine Funktionen)**

- Unter Windows 11 landet das Icon zunächst im Überlauf und muss einmalig unter *Einstellungen → Personalisierung → Taskleiste → Andere Symbole der Taskleiste* sichtbar gemacht werden.
- Windows zeigt während der Aufnahme sein eigenes Mikrofon-Symbol im Tray.

## Fehlerbehandlung

**Grundsatz:** Eine laufende Aufnahme wird nicht wegen einer einzelnen Quelle abgebrochen. Fehlende Quellen werden über den Zustand „Aufnahme mit Einschränkung“ sichtbar gemacht.

| Situation | Verhalten |
|---|---|
| Beim Start: weder Mikrofon noch Wiedergabegerät nutzbar | Keine Aufnahme, Zustand „Fehler: Kein Audiogerät verfügbar“ |
| Beim Start: kein Mikrofon | Aufnahme mit Einschränkung „ohne Mikrofon“ |
| Beim Start: kein Wiedergabegerät | Aufnahme mit Einschränkung „ohne Systemaudio“ |
| Mikrofon liefert länger als 3 s ausschließlich exakte Nullen oder gar keine Daten | Einschränkung „Mikrofon stumm/gesperrt“; wird aufgehoben, sobald wieder Signal kommt |
| Wiedergabegerät kommt hinzu | Wird in die laufende Aufnahme aufgenommen |
| Wiedergabegerät verschwindet | Quelle entfällt still, Aufnahme läuft weiter (Einschränkung nur, wenn danach keines mehr übrig ist) |
| Mikrofon verschwindet | Umschalten auf neues Windows-Standardmikrofon, bis dahin Stille. Gibt es keins mehr: Einschränkung „ohne Mikrofon“ |
| Während der Aufnahme fallen alle Quellen weg | Aufnahme läuft mit Stille weiter (Geräte können zurückkommen), Einschränkung „ohne Mikrofon, ohne Systemaudio“ |
| Schreibfehler im Arbeitsordner (Platte voll, Ordner nicht beschreibbar) | Aufnahme stoppen, bisher Geschriebenes bleibt erhalten, Zustand „Fehler: Schreibfehler“ |
| Zielordner beim Verschieben nicht erreichbar | Datei bleibt im Arbeitsordner, erneuter Versuch beim nächsten Stoppen oder Start, nur Protokolleintrag |
| Unerwartete Ausnahme in der Pipeline | Aufnahme sauber stoppen, Zustand „Fehler: Interner Fehler – siehe Protokoll“ |
| Absturz/Stromausfall | Datei im Arbeitsordner bis auf die letzten Sekunden erhalten und beim nächsten Start in den Zielordner verschoben; ggf. fehlt die Gesamtlänge im FLAC-Kopf, Whisper/ffmpeg lesen die Datei trotzdem |

- **Fehlerzustand verlassen:** Ein Linksklick startet einen neuen Aufnahmeversuch.
- **Protokoll:** `%LOCALAPPDATA%\Oreja\oreja.log` mit Zeitstempel pro Zeile. Protokolliert werden Start/Stopp, Gerätewechsel, Pufferüberläufe, Einschränkungen und Fehler mit Ausnahmedetails. Ab 1 MB wird die Datei nach `oreja.log.1` rotiert (eine ältere Version wird aufbewahrt).

## Tests

**Testbarkeit:** Audioquellen liegen hinter `IAudioSource`, die Zeit kommt aus `IClock`, der Sink ist `IFlacSink`. Taktgeber, Mischen und Zustandslogik sind dadurch ohne Hardware und ohne echte Wartezeiten testbar.

**Automatisierte Tests (xUnit, `dotnet test`)**

- `AudioMixer`/`SampleFifo`:
  - Ausgabelänge entspricht exakt der simulierten verstrichenen Zeit.
  - Lücken werden mit Stille gefüllt; Füllen/Abspielen-Übergänge mit 200 ms Vorlauf.
  - Mischen addiert und begrenzt korrekt.
  - Quellen können während der Aufnahme hinzugefügt und entfernt werden.
  - Eine zu schnelle Quelle führt zum Verwerfen bei 1 s Füllstand, eine zu langsame zu Stille-Einfügung; kein unbegrenztes Pufferwachstum.
- `MicSilenceDetector`: meldet nach 3 s exakter Nullen „stumm“, bei leisem Rauschen nicht, hebt den Zustand bei Signal wieder auf.
- `Recorder`: Zustandsübergänge Bereit → Aufnahme → Einschränkung → Fehler mit Fake-Quellen und Fake-Sink.
- Klickschutz: Klicks innerhalb 1 s nach dem Umschalten werden ignoriert.
- `OutputPaths`: Dateinamenformat, Suffix `_2` bei Kollision im Arbeits- oder Zielordner.
- `RecordingMover` (gegen temporäre Verzeichnisse):
  - Fertige Dateien landen im Zielordner und verschwinden aus dem Arbeitsordner.
  - Die gerade aufgenommene Datei wird nicht angefasst.
  - Namenskollision im Zielordner führt zu Suffix statt Überschreiben.
  - Nicht erreichbarer Zielordner: Datei bleibt im Arbeitsordner, kein Datenverlust.
  - Übrig gebliebene `.tmp`-Datei im Zielordner wird beim nächsten Versuch überschrieben.
- `Settings`: Voreinstellung bei fehlendem Wert, Lesen und Schreiben des Zielordners gegen einen Test-Registry-Schlüssel.
- `IFlacSink`-Implementierung: Testton schreiben, mit dem Windows-FLAC-Decoder (Media Foundation) zurücklesen, Samples bitgenau vergleichen.
- `Autostart`: Lesen, Schreiben, Pfadaktualisierung und Erststart-Erkennung gegen einen Test-Registry-Schlüssel.

**Manuelle Checkliste (`docs/manual-tests.md`)**

1. 5 Minuten YouTube abspielen und dabei sprechen, mit Whisper transkribieren: Beide Seiten werden erkannt.
2. Teams-Testanruf mit dem Headset als Teams-Gerät, während Windows-Standard die Lautsprecher sind: Beide Seiten sind in der Aufnahme.
3. Während der Aufnahme Headset abziehen und wieder anstecken: Aufnahme läuft durch, Icon zeigt zwischenzeitlich die Einschränkung.
4. Mikrofonzugriff in den Windows-Datenschutzeinstellungen sperren: Icon zeigt „Mikrofon stumm/gesperrt“.
5. Rechner während einer Aufnahme herunterfahren bzw. in den Standby schicken: Datei ist vollständig lesbar.
6. Prozess während einer Aufnahme im Task-Manager beenden: Nach dem nächsten Start liegt die Datei im Zielordner und ist mit ffmpeg lesbar.
7. Zielordner in Google Drive für Desktop wählen, eine Aufnahme machen: Die Datei erscheint vollständig in Google Drive, ohne Zwischenstände.
8. Zielordner auf einem USB-Stick wählen, Stick abziehen, Aufnahme machen, Stick wieder anstecken, Oreja neu starten: Die Datei wird nachträglich verschoben.
9. Zweimal kurz hintereinander klicken: Aufnahme startet und bleibt an.
10. Exe ein zweites Mal starten: Es erscheint kein zweites Icon.

## Reihenfolge der Umsetzung

1. Projektgerüst und `LibFlacSink` (Encoder-Entscheidung ist gefallen, siehe „FLAC-Encoder“)
2. Kernlogik mit Tests: `SampleFifo`, `AudioMixer`, `SampleConverter`, `MicSilenceDetector`, `OutputPaths`, `RecordingMover`, `Settings`
3. `Recorder`, WASAPI-Quellen und `WasapiBackend`
4. `TrayApp`, Icons, Ordnerauswahl, `Autostart`, `Log`, Single Instance
5. Self-contained Single-File-Build, manuelle Checkliste durchgehen

## Folgeschritte (nicht Teil von Version 1)

- Öffentliches GitHub-Repo mit Lizenz, GitHub Actions für Build, Tests und Releases
- Paketierung als winget-Portable-Paket, danach ggf. Chocolatey
- Code-Signing (z. B. Azure Trusted Signing), um SmartScreen-Warnungen zu vermeiden

## Risiken

- **Native DLL:** libFLAC wird als Binärdatei aus dem offiziellen Release ins Repo übernommen (Herkunft und Prüfsumme in `THIRD-PARTY-NOTICES.md`). Updates von libFLAC sind ein manueller Schritt.
- **Loopback-Eigenheiten:** Bluetooth-Headsets wechseln beim Mikrofongebrauch das Profil und damit das Wiedergabegerät. Das ist durch die Aufnahme aller aktiven Wiedergabegeräte und die Gerätebenachrichtigungen abgedeckt, muss aber manuell geprüft werden (Checkliste Punkt 2 und 3).
- **Virenscanner:** Eine unsignierte Exe, die das Mikrofon aufnimmt, kann Fehlalarme auslösen.
