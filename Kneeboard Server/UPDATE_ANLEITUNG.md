# Auto-Update Anleitung für Kneeboard Server

Diese Anleitung beschreibt, wie das automatische Update-System für den Kneeboard Server konfiguriert wird.

## Server-Konfiguration

### 1. XML-Versionsdatei

Die Datei `Kneeboard_version.xml` muss unter folgender URL erreichbar sein:
```
https://gsimulations.com/Kneeboard_version.xml
```

**Inhalt der XML-Datei:**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<item>
    <version>2.1.0.0</version>
    <url>https://gsimulations.com/Kneeboard_Server_2.1.0.0.zip</url>
    <changelog>https://gsimulations.com/changelog.html</changelog>
    <mandatory>false</mandatory>
</item>
```

### 2. Felder-Erklärung

| Feld | Beschreibung | Erforderlich |
|------|-------------|--------------|
| `version` | Die neue Versionsnummer (muss höher sein als die installierte Version) | Ja |
| `url` | Download-Link zur ZIP-Datei oder zum Installer (.exe, .msi, .zip) | Ja |
| `changelog` | Link zur Changelog-Seite (wird dem Benutzer angezeigt) | Nein |
| `mandatory` | `true` = Pflicht-Update, `false` = Optional | Nein |
| `checksum` | MD5/SHA256 Prüfsumme der Datei | Nein |

### 3. ZIP-Datei Namensschema

```
Kneeboard_Server_[VERSION].zip
```

**Beispiele:**
- `Kneeboard_Server_2.1.0.0.zip`
- `Kneeboard_Server_2.2.0.0.zip`

### 4. ZIP-Datei Inhalt

Die ZIP-Datei sollte folgende Dateien enthalten:
- `Kneeboard Server.exe` (Hauptanwendung)
- Alle benötigten DLL-Dateien
- `data/` Ordner (falls Änderungen)

## GitHub Release-Prozess (Empfohlen)

Der bevorzugte Weg für Releases nutzt Git Tags und GitHub Actions für eine automatisierte Veröffentlichung.

### Voraussetzungen

- MSI-Installer und setup.exe müssen im Ordner `Kneeboard Server Setup/Debug/` vorhanden sein
- Git Repository ist mit GitHub verbunden
- GitHub Actions Workflow ist konfiguriert (`.github/workflows/release.yml`)

### Schritt 1: Version erhöhen

In `Properties/AssemblyInfo.cs` die Version erhöhen:

```csharp
[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.0")]
```

### Schritt 2: Installer erstellen

In Visual Studio das Setup-Projekt kompilieren, sodass die Dateien im `Kneeboard Server Setup/Debug/` Ordner aktualisiert werden:
- `Kneeboard Server.msi`
- `setup.exe`

### Schritt 3: Änderungen committen

```bash
git add .
git commit -m "Release v2.1.0.0"
```

### Schritt 4: Git Tag erstellen und pushen

```bash
git tag v2.1.0.0
git push origin main
git push origin v2.1.0.0
```

### Was passiert automatisch

Nach dem Push des Tags führt GitHub Actions folgende Schritte aus:

1. **Version extrahieren** - Version wird aus dem Tag-Namen ermittelt
2. **ZIP erstellen** - `KneeboardServer-v2.1.0.0-Setup.zip` wird aus MSI + setup.exe erstellt
3. **Update-XML generieren** - `Kneeboard_version.xml` wird automatisch generiert
4. **GitHub Release erstellen** - Release mit ZIP und XML wird veröffentlicht

### URL-Schema

Nach erfolgreichem Release sind die Dateien unter folgenden URLs verfügbar:

- **Download:** `https://github.com/[user]/[repo]/releases/download/v[VERSION]/KneeboardServer-v[VERSION]-Setup.zip`
- **Changelog:** `https://github.com/[user]/[repo]/releases/tag/v[VERSION]`

### Pre-Release / Beta-Versionen

Für Test-Releases kann ein Suffix an die Version angehängt werden:

```bash
git tag v2.1.0.0-beta1
git push origin v2.1.0.0-beta1
```

Mögliche Suffixe:
- `-alpha` - Frühe Entwicklungsversion
- `-beta` / `-beta1`, `-beta2` - Beta-Testversion
- `-rc1`, `-rc2` - Release Candidate

---

## Manueller Release-Prozess (Alternative)

Falls der automatische Prozess nicht verwendet werden kann, hier der manuelle Ablauf:

### Schritt 1: Version erhöhen

In `Properties/AssemblyInfo.cs` die Version erhöhen:

```csharp
[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.0")]
```

### Schritt 2: Projekt kompilieren

Release-Build erstellen in Visual Studio.

### Schritt 3: ZIP-Datei erstellen

Alle Dateien aus dem `bin/x64/Release/` Ordner in eine ZIP-Datei packen:
```
Kneeboard_Server_2.1.0.0.zip
```

### Schritt 4: Dateien hochladen

1. ZIP-Datei hochladen nach: `https://gsimulations.com/Kneeboard_Server_2.1.0.0.zip`
2. Optional: Changelog aktualisieren

### Schritt 5: XML-Datei aktualisieren

`Kneeboard_version.xml` mit neuer Version und URL aktualisieren:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<item>
    <version>2.1.0.0</version>
    <url>https://gsimulations.com/Kneeboard_Server_2.1.0.0.zip</url>
    <changelog>https://gsimulations.com/changelog.html</changelog>
    <mandatory>false</mandatory>
</item>
```

## Erweiterte Optionen

### Prüfsumme hinzufügen

Für zusätzliche Sicherheit kann eine Prüfsumme hinzugefügt werden:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<item>
    <version>2.1.0.0</version>
    <url>https://gsimulations.com/Kneeboard_Server_2.1.0.0.zip</url>
    <checksum algorithm="MD5">dein_md5_hash_hier</checksum>
    <mandatory>false</mandatory>
</item>
```

### Pflicht-Update

Für kritische Updates kann `mandatory` auf `true` gesetzt werden:

```xml
<mandatory>true</mandatory>
```

Bei Pflicht-Updates wird der Benutzer gezwungen, das Update zu installieren.

## Funktionsweise

1. Beim Start der Anwendung wird `Kneeboard_version.xml` abgerufen
2. Die Version in der XML wird mit der installierten Version verglichen
3. Wenn eine neue Version verfügbar ist:
   - Update wird automatisch heruntergeladen
   - Nach dem Download wird die Installation gestartet
   - Die Anwendung wird beendet und neu gestartet

## Fehlerbehebung

### Update wird nicht erkannt
- Prüfen ob die Version in der XML höher ist als in `AssemblyInfo.cs`
- XML-Datei im Browser aufrufen um Erreichbarkeit zu testen

### Download schlägt fehl
- URL in der XML prüfen (Leerzeichen mit `%20` kodieren)
- ZIP-Datei im Browser testen

### Installation schlägt fehl
- Anwendung benötigt Admin-Rechte
- Antivirus-Software kann Installation blockieren
