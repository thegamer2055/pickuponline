# Pick Up Online – gemeinsamer Browser- und Windows-Client

**Entwicklungsstand:** ASP.NET Core 8 + SignalR-Server, responsive Browser-Client, Windows-WebView2-Client, gemeinsame Regelbibliothek und xUnit-Tests. Die beiden Clients verwenden dieselbe Weboberfläche und denselben Server-Endpunkt `/gamehub`.

> **Wichtig zum Prüfstatus:** In der Erstellungsumgebung ist kein .NET SDK vorhanden und der Netzwerkzugriff zum Nachinstallieren ist gesperrt. Deshalb konnte ich hier keinen echten `dotnet build`, `dotnet test` oder Laufzeittest ausführen. Ich habe die vorherigen Dateien statisch überarbeitet, die Browser-JavaScript-Datei mit Node.js auf Syntaxfehler geprüft und Tests/Buildanweisungen beigelegt. Der erste echte Build muss in Visual Studio bzw. mit dem .NET-8-SDK erfolgen.

## Inhalt

- `PickUp.Core` – zentrale, vom Client unabhängige Wertungs-, Ziel- und Zeitbonusregeln sowie Spielzustände.
- `PickUp.Server` – ASP.NET-Core-8-Server, SignalR-Hub, Raumverwaltung, serverseitige Spielaktionen, Countdown-/Computergegner-Takt und Browserdateien.
- `PickUp.Windows` – installierbarer Windows-Wrapper, der die gleiche Server-Weboberfläche in WebView2 öffnet.
- `PickUp.Tests` – xUnit-Tests für Wertungen, Joker, Ziele, Zeitbonus, getrennte Spielerzustände, Rundenwechsel und doppelte Aktionen.
- `PickUpOnline.sln` – Visual-Studio-Lösung.

## Lokal in Visual Studio starten

1. **Visual Studio 2022** mit den Workloads **ASP.NET- und Webentwicklung** sowie **.NET-Desktopentwicklung** installieren. Das **.NET-8-SDK** muss vorhanden sein.
2. `PickUpOnline.sln` öffnen und NuGet-Wiederherstellung abwarten.
3. In einer Developer PowerShell einmal das lokale HTTPS-Entwicklungszertifikat vertrauen:

   ```powershell
   dotnet dev-certs https --trust
   ```

4. `PickUp.Server` als Startprojekt auswählen und mit **F5** starten. Das beigefügte Profil verwendet `https://localhost:7001` (alternativ HTTP `http://localhost:5001`).
5. Browser auf `https://localhost:7001` öffnen. Für einen lokalen Zweispielertest zwei normale Browser-Tabs (oder unterschiedliche Browserprofile) verwenden. Die Zugangsdaten liegen tabbezogen in `sessionStorage`:
   - Fenster A: Namen eingeben → **Online-Raum erstellen** → sechsstelligen Code teilen.
   - Fenster B: Code eingeben → **Beitreten**.
   - Fenster A: **Spiel starten**.
   - Beide sehen ihre eigene Spielfläche; die privaten Würfe werden nicht an den Gegner übertragen.
6. Für den Einzelspielertest **Solo gegen Computer** wählen. Der Computergegner spielt serverseitig mit einer einfachen Heuristik.

### Tests ausführen

```powershell
dotnet test .\PickUp.Tests\PickUp.Tests.csproj
```

### Windows-App veröffentlichen

1. `PickUp.Windows` als Startprojekt öffnen oder in der Developer PowerShell veröffentlichen:

   ```powershell
   dotnet publish .\PickUp.Windows\PickUp.Windows.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
   ```

2. Die erzeugte selbstständige EXE liegt im `bin\Release\net8.0-windows\win-x64\publish`-Ordner. Das ist ein veröffentlichbarer Client, noch kein bereits gebautes MSI/MSIX-Setup.
3. Beim ersten Start die Serveradresse oben in der Windows-App eintragen, z. B. `https://localhost:7001` oder später die öffentliche HTTPS-Adresse. Alternativ vor dem Start die Umgebungsvariable `PICKUP_SERVER_URL` setzen.
4. Auf dem Ziel-PC muss die **Microsoft Edge WebView2 Runtime** installiert sein. Sie ist auf aktuellen Windows-Installationen häufig bereits vorhanden, wird durch diese EXE aber nicht automatisch mitinstalliert.
5. Für einen klassischen Setup-Assistenten ist `installer/PickUpOnline.iss` enthalten. Nach erfolgreichem Publish **Inno Setup 6** installieren und diese `.iss`-Datei kompilieren. Das Setup landet in `installer/Output`. Das Inno-Setup-Skript wurde hier ebenfalls nicht ausgeführt; es ist eine Buildvorlage, kein bereits erzeugter Installer.

## Österreich ↔ Deutschland: öffentlicher Online-Betrieb

**Es ist kein öffentliches Hosting eingerichtet.** Die obigen Schritte starten den Server nur lokal auf dem eigenen PC.

Den Server veröffentlichen (nach erfolgreichem lokalem Build) kannst du beispielsweise mit:

```powershell
dotnet publish .\PickUp.Server\PickUp.Server.csproj -c Release -o .\publish\server
```

Der Inhalt von `publish\server` wird auf dem Zielhost betrieben, typischerweise hinter IIS oder Nginx mit HTTPS und WebSocket-Weiterleitung.

Für echte Partien über das Internet muss `PickUp.Server` auf einem öffentlich erreichbaren Windows- oder Linux-Host bereitgestellt werden. Erforderlich sind:

- eine feste öffentliche HTTPS-Adresse / Domain und ein gültiges TLS-Zertifikat;
- eingehender HTTPS-Verkehr (typisch TCP 443) und aktivierte WebSocket-Unterstützung am Reverse Proxy; bei einem Proxy außerhalb von Loopback dessen IP in `ForwardedHeadersOptions.KnownProxies`/`KnownNetworks` konfigurieren;
- ein einzelner Serverprozess für diesen Entwicklungsstand, weil Räume derzeit nur im Arbeitsspeicher liegen;
- beide Spieler öffnen dieselbe öffentliche URL. Der Windows-Client erhält genau diese URL im Serveradressfeld.

Für mehrere Serverinstanzen wären ein SignalR-Backplane (z. B. Redis), gemeinsame dauerhafte Raumdaten und zusätzliche Synchronisierung nötig. Das ist hier noch nicht eingebaut. Es gibt außerdem noch keine Benutzerkonten, persistente Datenbank, Rate-Limits oder vollständige Anti-Cheat-/Sicherheitsprüfung. Den Raumcode und das Wiederverbindungstoken deshalb nicht öffentlich teilen. Die SignalR-JavaScript-Bibliothek wird von cdnjs geladen, daher brauchen auch lokale Browserclients Internetzugriff. Das Token liegt pro Tab in `sessionStorage`; ein Reload oder automatischer SignalR-Reconnect im selben Tab kann den Spielerplatz wiederherstellen. Nach dem vollständigen Schließen des Tabs ist diese Sitzung nicht garantiert wiederherstellbar.

## Umgesetzter Spielablauf

- Runde 1 bis 5 mit Zielen **200.000 / 250.000 / 300.000 / 350.000 / 400.000**.
- Pro Runde **210 Sekunden**. Der Server ist für Ablauf und Restzeit maßgeblich; die Anzeige im Browser zählt lokal nur zwischen den Serverupdates herunter.
- Fünf Symbole werden beim Start jedes Zuges automatisch geworfen. Es folgen bis zu zwei Neuwürfe; die nicht gehaltenen Symbole werden serverseitig neu gezogen.
- Nach jeder Würfelphase kann eine gültige Wertung übernommen werden. Danach startet automatisch der nächste Zug mit fünf neuen Symbolen.
- Wenn nach dem dritten Wurf keine gültige Wertung möglich ist, wird der Spieler mit X beendet und wartet. Erreicht er sein Rundenziel, wird er mit Flagge beendet und erhält 100 Punkte pro verbleibender voller Anzeigesekunde.
- Jeder Spieler hat einen eigenen Zustand und einen eigenen Zeitstempel. Ein fertiger Spieler wartet, während der andere weiterspielt. Der Rundenschritt erfolgt erst, wenn beide beendet sind oder ihre Zeit abgelaufen ist.
- Online-Aktionen prüfen Raum, Spielstatus, Spieler-Token, aktuelle Verbindung, Zug-ID, Wurfnummer und Aktions-ID. Doppelte identische Aktions-IDs werden idempotent beantwortet; veraltete Zug-/Wurfaktionen werden abgewiesen.
- Nach Verbindungsabbruch bleibt der Spielerplatz mit seinem Zustand bestehen. Derselbe Browser-Tab kann mit Raumcode und tabbezogenem Token wieder beitreten. Ein Serverneustart löscht die flüchtigen Räume.

## Wertung und Joker

Die sechs normalen Symbole verwenden exakt die vorgegebenen Punkte für 3/4/5 gleiche. Zusätzlich gelten Full House (3+2) = 45.000 und fünf verschiedene = 37.500. Die Wertungsfunktion durchsucht mögliche Joker-Zuordnungen und nimmt die höchste gültige Wertung.

**Explizite Joker-Arbeitsannahme:** Der Paar-Joker darf nur einem Symbol zugeordnet werden, von dem im ursprünglichen Wurf mindestens zwei natürliche Exemplare vorhanden sind. Er kann nicht erst durch einen anderen Joker seine eigene Voraussetzung erzeugen. Der freie Joker darf jedes Symbol ersetzen. Beide Joker dürfen ungenutzt bleiben, falls das für die beste gültige Kombination sinnvoller ist. Das ist nachvollziehbar implementiert, aber die genaue Original-Jokerregel wurde im bereitgestellten Video nicht vollständig erklärt.

## Offene Regelannahmen – bewusst nicht als bestätigte Originalregeln ausgegeben

1. **Wertung als Zugabschluss:** Eine gültige Kombination kann nach jedem der drei Würfe gebucht werden. Danach beginnt ein neuer Zug mit einem automatischen Erstwurf. Das ist die derzeitige Arbeitslogik.
2. **Kein gültiger Zug:** Ein Spieler wird automatisch beendet, wenn nach dem dritten Wurf keine Kombination aus der angegebenen Wertungstabelle möglich ist. Gibt es eine gültige Wertung, kann er sie buchen; er wird nicht automatisch beendet.
3. **Nicht genannte Kombinationen:** Paare, zwei Paare, Straßen o. Ä. geben keine Punkte, weil dafür keine Werte genannt wurden.
4. **Restsekunden:** Für den Zeitbonus zählen volle angezeigte Restsekunden. Direkt nach Rundenschluss gibt es 0 Bonus.
5. **Rundenpunkte:** Der Zeitbonus wird zum Rundenergebnis addiert; das Gesamtresultat summiert die Rundenergebnisse.
6. **Computergegner:** Der Bot ist eine einfache Heuristik, kein Nachbau eines originalen Computergegners.
7. **Zwei Spieler am selben PC:** Für einen lokalen Test können zwei Browserfenster denselben Server und Raumcode nutzen. Ein spezieller Split-Screen-Modus mit zwei Personen an einer einzigen Tastatur ist noch nicht implementiert.
8. **Originaloberfläche:** Das bereitgestellte Video zeigt eine violette/neonfarbene Tischoptik mit zwei getrennten Spielflächen. Die neue Oberfläche greift Farben und Symbole auf, ist aber noch keine pixelgenaue Rekonstruktion aller Originalfelder/Animationen.

## Noch nicht erledigt

- echten Build- und Laufzeittest mit .NET 8 auf Windows durchführen;
- SignalR-End-to-End-Test mit zwei echten Browserverbindungen ergänzen (die vorhandenen Multiplayer-Tests prüfen derzeit den gemeinsamen Coordinator-/Raumzustand, nicht einen echten Internettransport);
- Originalregeln für Joker, Zugabschluss und sonstige Original-UI-Elemente anhand einer Regelbeschreibung bzw. weiterer Aufnahmen bestätigen;
- persistente Sitzungen, Authentifizierung, Rate-Limits und Deployment konfigurieren;
- öffentliches Hosting einrichten und von Österreich/Deutschland aus testen.


## Kostenloser Hosting-Test mit Render (Docker)

Das Repository enthält jetzt `Dockerfile`, `.dockerignore` und `render.yaml` für einen Render-Web-Service in Frankfurt. Der Service verwendet den kostenlosen Tarif. Beachte: Kostenlose Render-Webdienste schlafen nach 15 Minuten ohne eingehende HTTP-Anfrage bzw. WebSocket-Nachricht ein; der erste Aufruf danach kann ungefähr eine Minute benötigen. Render kann kostenlose Dienste außerdem neu starten. Spielräume liegen aktuell nur im Arbeitsspeicher und gehen bei einem Neustart verloren. Das ist daher für Tests/Hobbybetrieb geeignet, nicht für einen verlässlichen dauerhaften Produktivbetrieb.

### Server auf Render bereitstellen

1. Entpacke das Projekt und erstelle ein privates oder öffentliches GitHub-Repository. Lade den Inhalt dieses Projektordners in das Repository hoch (insbesondere `Dockerfile`, `render.yaml`, `PickUp.Core/` und `PickUp.Server/`). `bin`, `obj` und `.vs` werden nicht benötigt.
2. Erstelle unter https://render.com ein Konto und verbinde GitHub.
3. In Render: **New → Blueprint** wählen, das Repository auswählen und `render.yaml` bestätigen. Alternativ **New → Web Service** und als Runtime Docker wählen.
4. Warte auf den erfolgreichen Build. Die Render-Adresse sieht etwa so aus: `https://pickup-online-server.onrender.com` (die tatsächliche Adresse zeigt Render im Dashboard).
5. Öffne `https://DEINE-RENDER-ADRESSE/health`. Erwartet wird JSON mit `status: ok`.
6. Kopiere die tatsächliche HTTPS-Adresse in `PickUp.Server/wwwroot/server-config.js`: `window.PICKUP_SERVER_URL = "https://DEINE-RENDER-ADRESSE";` (ohne abschließenden Schrägstrich). Danach Änderungen committen/pushen, damit Render neu deployt.
7. Für den itch.io-Upload verwende `PickUp.Server/wwwroot/index.html` zusammen mit `PickUp.Server/wwwroot/server-config.js` in einer ZIP-Datei. Beide Dateien müssen direkt im ZIP-Hauptverzeichnis liegen. Lade diese ZIP als HTML-Projekt bei itch.io hoch und aktiviere **This file will be played in the browser**.

### CORS / itch.io

`Program.cs` erlaubt standardmäßig `https://html-classic.itch.zone` für SignalR. Wenn die Browserkonsole später eine CORS-Meldung mit einem anderen `Origin` zeigt, ergänze diesen exakten Origin in Render unter **Environment → AllowedOrigins** (kommagetrennt, ohne Pfad), z. B. `https://html-classic.itch.zone,https://DEINNAME.itch.io`, und deploye neu. Nicht `AllowAnyOrigin` zusammen mit Credentials aktivieren.

### Wichtige Einschränkungen

- Render Free schläft nach 15 Minuten Inaktivität und wacht beim nächsten HTTP-/WebSocket-Verbindungsversuch wieder auf; der erste Start kann ca. eine Minute dauern.
- Render Free kann Dienste jederzeit neu starten. Die aktuellen Spielräume liegen im RAM; laufende Partien gehen bei Neustart/Schlafphase verloren.
- Es ist nur eine Serverinstanz vorgesehen; keine Redis-Backplane oder persistente Datenbank ist eingerichtet.
- Das .NET SDK war in der Bearbeitungsumgebung nicht verfügbar. Deshalb konnte hier kein `dotnet build`, `dotnet test` oder echter Deployment-/SignalR-Laufzeittest ausgeführt werden. Vor öffentlicher Nutzung bitte mit .NET 8 testen.
