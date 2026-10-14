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
