# Prüfbericht für diesen Entwicklungsstand

## Statisch geprüft / korrigiert

- Das ursprüngliche Gerüst hatte keine getrennte gemeinsame Regelbibliothek, keine robuste Spielerplatz-Zuordnung für Wiederverbindung, keine belastbare getrennte Spieleruhr und keine sichere Behandlung veralteter/doppelter Aktionen.
- Wertung und Jokerlogik liegen jetzt zentral in `PickUp.Core/GameRules.cs`.
- Raumzustände sind je Spieler getrennt; private Würfelstände werden nur an die jeweilige Spieler-Verbindung gesendet. Der Gegner erhält nur öffentliche Punkt-/Statusdaten.
- Der Server prüft Raum, Spieler-Token, aktuelle Verbindung, Zug-ID, Wurfnummer, Aktions-ID, Wurfanzahl und Zeitlimit. Joker-Zuordnungen werden in der bestmöglichen Wertung als Erklärung mit ausgegeben.
- Rundenzeit wird serverseitig ausgewertet. Ein fertiger Spieler behält seine Restzeit und wartet; die nächste Runde beginnt erst nach Abschluss beider Plätze.
- Automatische Wiederverbindung und Wiederbeitritt per Raumcode + Token sind im Browserclient enthalten.
- Browser-JavaScript wurde mit `node --check` syntaktisch geprüft.
- Projektdateien (`.csproj`) und `launchSettings.json` sind als XML/JSON strukturell angelegt; Solution-Verweise zeigen auf die vorhandenen Projekte.

## Nicht ausgeführt

- `dotnet build` und `dotnet test` konnten nicht ausgeführt werden: In der Erstellungsumgebung ist `dotnet` nicht installiert; externe Downloads sind nicht erreichbar.
- Der ASP.NET-Core-Server wurde hier nicht gestartet und zwei echte SignalR-Verbindungen wurden nicht über einen Browser-End-to-End-Test geprüft.
- Die xUnit-Tests sind vorbereitet, aber noch nicht als bestanden zu melden.
- Die Windows-App konnte hier nicht kompiliert oder veröffentlicht werden.

## Verbleibende Produktivrisiken

- Raumdaten liegen im Arbeitsspeicher und verschwinden bei Serverneustart.
- Keine Benutzerkonten/Auth, Datenbank, Rate-Limits, Missbrauchserkennung oder vollständige Sicherheitsprüfung.
- Für mehrere Serverinstanzen fehlen SignalR-Backplane und gemeinsamer Speicher.
- Das Online-Spiel über Ländergrenzen funktioniert erst nach Einrichtung eines öffentlich erreichbaren HTTPS-Servers; Hosting wurde nicht eingerichtet.
