# G-Sim Kneeboard Server

HTTP Server für das G-Sim Kneeboard Add-on für Microsoft Flight Simulator 2020/2024.

## Beschreibung

Der Kneeboard Server ist eine Windows-Anwendung, die einen lokalen HTTP-Server auf Port 815 bereitstellt. Er versorgt das Kneeboard EFB Add-on mit Kartendaten, Flughafeninformationen, Waypoints und Simbrief-Flugplänen.

### Features

- **HTTP Server** auf `localhost:815`
- **Flughafen-Datenbank** mit weltweiten Flughäfen
- **Waypoint-Verwaltung** für Navigation
- **Simbrief-Integration** für Flugplanung
- **Echtzeit-Kommunikation** mit MSFS
- **Karten-Visualisierung** für das Kneeboard
- **Singleton-Anwendung** (nur eine Instanz gleichzeitig)

## Systemanforderungen

- Windows 10/11
- .NET Framework 4.8
- Administrator-Rechte (wird automatisch angefordert)
- Microsoft Flight Simulator 2020 oder 2024

## Installation

### Option 1: Installer verwenden

1. Führe das Setup aus `Kneeboard Server Setup\Debug` oder `Release` aus
2. Der Server wird beim Start automatisch mit Admin-Rechten gestartet

### Option 2: Von Source kompilieren

1. Öffne `Kneeboard Server.sln` in Visual Studio 2017 oder neuer
2. Build Configuration: `Debug` oder `Release`
3. Platform: `x64` empfohlen
4. Build Solution

## Verwendung

1. Starte den Kneeboard Server
2. Die Anwendung läuft im Hintergrund
3. Der HTTP-Server ist unter `http://localhost:815` erreichbar
4. Starte MSFS und das Kneeboard EFB Add-on
5. Das Kneeboard verbindet sich automatisch mit dem Server

## Projektstruktur

```
Kneeboard Server/
├── Kneeboard Server/          # Hauptanwendung
│   ├── Program.cs              # Entry Point
│   ├── KneeboardServer.cs      # Server-Logik
│   ├── SimpleHTTPServer.cs     # HTTP Server
│   ├── Airports.cs             # Flughafen-Datenbank
│   ├── Waypoints.cs            # Waypoint-Verwaltung
│   ├── Simbrief.cs             # Simbrief-Integration
│   └── bin/                    # Kompilierte Binaries
└── Kneeboard Server Setup/     # Installer-Projekt
    ├── Debug/                  # Debug Installer
    └── Release/                # Release Installer
```

## Technologie-Stack

- C# / .NET Framework 4.8
- Windows Forms
- HTTP Server (Port 815)
- JSON für Datenaustausch

## Entwicklung

### Build-Konfigurationen

- **Debug|x64**: Entwicklung mit Debug-Symbolen
- **Release|x64**: Optimierte Production-Version

### Voraussetzungen

- Visual Studio 2017 oder neuer
- .NET Framework 4.8 SDK
- Windows SDK

## Port

Der Server verwendet Port **815**. Stelle sicher, dass dieser Port nicht von anderen Anwendungen verwendet wird.

## Version

Aktuelle Version: 2.0.0

## Lizenz

MIT License - Copyright (c) 2021-2025 G-Simulations / Patrick Moses

Siehe [LICENSE](LICENSE) für Details.

## Autor

Moses / Gsimulations
Website: https://www.gsimulations.com

## Hinweise

- Die Anwendung benötigt Admin-Rechte für Netzwerk-Zugriff
- Es kann nur eine Instanz gleichzeitig laufen
- Firewall-Ausnahme wird beim ersten Start abgefragt
