# Manuelle Abnahme

Vorbereitung: `publish\Oreja.exe` starten, Icon im Tray sichtbar machen, ffmpeg und Whisper bereithalten.
Nach jedem Test im Zweifel `%LOCALAPPDATA%\Oreja\oreja.log` ansehen.

| # | Schritt | Erwartet | OK |
|---|---|---|---|
| 1 | 5 Minuten ein spanisches YouTube-Video abspielen und dabei sprechen, dann stoppen und mit Whisper transkribieren | Beide Seiten (Video und eigene Stimme) im Transkript | ☐ |
| 2 | Teams-Testanruf mit dem Headset als Teams-Gerät, während Windows-Standard die Lautsprecher sind | Testansage und eigene Stimme in der Aufnahme | ☐ |
| 3 | Während der Aufnahme Headset abziehen, 10 s warten, wieder anstecken | Aufnahme läuft durch, Icon zeigt zwischendurch den gelben Punkt, danach wieder rot | ☐ |
| 4 | Mikrofonzugriff für Desktop-Apps in den Windows-Datenschutzeinstellungen sperren, Aufnahme starten | Gelber Punkt, Tooltip „Mikrofon stumm/gesperrt“ oder „ohne Mikrofon“ | ☐ |
| 5 | Während einer Aufnahme herunterfahren; neu anmelden | Datei liegt im Aufnahmeordner und ist vollständig abspielbar | ☐ |
| 6 | Während einer Aufnahme Standby, aufwecken | Icon „bereit“, Datei bis zum Standby vollständig | ☐ |
| 7 | Während einer Aufnahme `Oreja.exe` im Task-Manager beenden, Oreja neu starten | Datei landet im Aufnahmeordner, `ffmpeg -i datei.flac -f null -` läuft ohne Abbruch durch | ☐ |
| 8 | Aufnahmeordner auf einen Ordner in Google Drive für Desktop ändern, aufnehmen | Datei erscheint vollständig in Google Drive, keine `.tmp`-Reste | ☐ |
| 9 | Aufnahmeordner auf einen USB-Stick legen, Stick abziehen, aufnehmen, Stick anstecken, Oreja neu starten | Datei wird nachträglich auf den Stick verschoben | ☐ |
| 10 | Zweimal schnell hintereinander auf das Icon klicken | Aufnahme startet und bleibt an | ☐ |
| 11 | `Oreja.exe` ein zweites Mal starten | Kein zweites Icon | ☐ |
| 12 | Menü „Mit Windows starten“ aus- und wieder einschalten, neu anmelden | Häkchen entspricht dem Verhalten beim Anmelden | ☐ |
| 13 | Rechtsklick → „Abtastrate“ öffnen, „44,1 kHz“ wählen, Oreja beenden und neu starten | Einträge „16 kHz (Spracherkennung)“, „44,1 kHz“, „48 kHz (Hörclips)“; Häkchen vorher bei 16 kHz (Erststart), nach dem Neustart bei 44,1 kHz | ☐ |
| 14 | „48 kHz (Hörclips)“ wählen, 1 Minute aufnehmen, `ffprobe datei.flac` | `48000 Hz, mono`; Protokoll: „Aufnahme gestartet: … (n Quellen, 48000 Hz)“ und „Aufnahme beendet: … (48000 Hz) …“ | ☐ |
| 15 | Dasselbe mit „44,1 kHz“ und „16 kHz (Spracherkennung)“ | Datei hat 44100 bzw. 16000 Hz, Protokoll nennt die Rate; die 48-kHz-Datei ist deutlich größer als die 16-kHz-Datei | ☐ |
| 16 | Während einer Aufnahme Rechtsklick → „Abtastrate“ | Eintrag ausgegraut; nach dem Stoppen wieder wählbar | ☐ |
| 17 | Bei deutscher Windows-Sprache und Erststart Rechtsklick → „Sprache“ | Menü deutsch, Häkchen bei „Automatisch (Windows)“ | ☐ |
| 18 | „Sprache“ → „English“ wählen, Menü erneut öffnen, Maus aufs Icon halten | Alle Einträge sofort englisch („Start recording“, „Sample rate“ mit „44.1 kHz“, „Language“, „Exit“ …), Tooltip „Oreja – ready“, Häkchen bei „English“; Protokoll: „Sprache geändert: en“ | ☐ |
| 19 | Aufnahme starten, Tooltip ansehen, „Language“ → „Deutsch“ wählen, Tooltip erneut ansehen, Menü erneut öffnen, Aufnahme stoppen | Tooltip wechselt von „Oreja – recording since …“ zu „Oreja – Aufnahme seit …“ und der Menüeintrag von „Stop recording“ zu „Aufnahme stoppen“, beides sofort ohne Neustart | ☐ |
| 20 | „English“ wählen, „Change recordings folder…“ öffnen | Dialogtitel „Choose recordings folder“ | ☐ |
| 21 | Oreja beenden und neu starten | Menü weiterhin englisch, Häkchen bei „English“ | ☐ |
| 22 | „Language“ → „Automatic (Windows)“ wählen | Menü und Tooltip sofort wieder deutsch, Häkchen bei „Automatisch (Windows)“; Protokoll: „Sprache geändert: automatisch“ | ☐ |
