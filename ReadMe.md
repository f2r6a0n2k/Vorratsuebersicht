# Vorratsübersicht – Haltbarkeitsdatum überwachen & synchronisieren

Mit dieser App behalten Sie den Überblick über Ihre Vorräte und deren Mindesthaltbarkeitsdatum. Nie wieder Lebensmittel wegwerfen!

Seit Version 9.00: **Daten auf mehreren Geräten synchronisieren – ohne Internet, Cloud oder Server.** Die Geräte müssen sich nur im selben WLAN (oder Hotspot) befinden.

- **Telefon als Master**: Ein Gerät gibt die Daten frei (Master-Modus)
- **Web-Oberfläche**: Jeder Browser kann die Daten anzeigen und bearbeiten (PWA)
- **Sync-Client App**: Für Android, iOS, Windows, Mac, Linux
- **Funktioniert off-grid**: Auch ohne Internet (z.B. über Handy-Hotspot)

---

## Installation

### Schritt 1: APK herunterladen

Lade die aktuelle APK aus dem **GitHub Release** herunter:

> [**github.com/f2r6a0n2k/Vorratsuebersicht/releases**](https://github.com/f2r6a0n2k/Vorratsuebersicht/releases)

Oder füge das **F-Droid-Repo** in einem F-Droid-Client (z.B. [Droid-ify](https://github.com/Droid-ify/client)) hinzu:

```
https://f2r6a0n2k.github.io/Vorratsuebersicht/repo/
```

#### Welche APK brauche ich?

| APK | Beschreibung |
|-----|-------------|
| `de.stryi.vorratsuebersicht.sync_*.apk` | **Server-APK (Master-Modus)** – wird auf dem Telefon installiert, das die Daten freigibt. Enthält die vollständige Vorratsübersicht-App **plus** Sync-Server. Installiert sich **neben** der Original-App (eigenes Icon, eigene Datenbank). |
| `VorratSync-*-android.apk` | **Client-APK (VorratSync)** – reine Sync-Client-App für Android. Liest Daten vom Master, zeigt sie an und erlaubt Bearbeitung. Ideal für Tablets oder Zweitgeräte. |

Beide APKs können **gleichzeitig** auf demselben Gerät installiert sein.

### Schritt 2: APK installieren (Android)

1. Auf dem Telefon: **Einstellungen → Sicherheit → Unbekannte Apps installieren** erlauben (einmalig für den Dateimanager/Browser)
2. Die heruntergeladene APK öffnen → **Installieren**
3. Beide Apps haben ein eigenes Icon im App-Drawer

> **Hinweis für Samsung/Huawei/Xiaomi**: Manche Hersteller fragen beim ersten Start nach Berechtigungen für "Nicht vertrauenswürdige Apps". Dies bestätigen.

### Schritt 3: iOS (iPhone / iPad)

Für iOS wird ein **unsigned IPA** bereitgestellt (kein $99 Apple Developer Account nötig).

#### AltStore PAL (EU, empfohlen)

[AltStore PAL](https://altstore.io) ist der offizielle EU-Alternativ-App-Store – **kostenlos** (Epic Games MegaGrant deckt die Apple-Gebühren).

1. Auf dem iPhone in Safari: [altstore.io/download](https://altstore.io/download) öffnen
2. "Download" tippen → Marketplace-Installation erlauben (einmalig in Einstellungen)
3. `VorratSync-*-ios.unsigned.ipa` aufs iPhone laden (z.B. iCloud Drive / Dateien-App)
4. In AltStore: **Meine Apps → + → IPA auswählen**
5. Fertig! Kein 7-Tage-Refresh nötig (Dank DMA-Regulierung)

#### SideStore (weltweit, auch außerhalb EU)

[SideStore](https://sidestore.io) ist AltStore ohne PC-Zwang – nach einmaliger Einrichtung läuft alles auf dem Gerät (WireGuard-VPN-Tunnel). Benötigt nur eine kostenlose Apple-ID. Apps werden automatisch im Hintergrund frisch signiert (alle 7 Tage).

---

## Ersteinrichtung

### Master einrichten (Telefon mit Server-APK)

1. Die Server-App **"Vorratsübersicht Sync"** öffnen
2. Zum Menü (≡) → **Einstellungen → Master-Modus**
3. **"Server starten"** antippen
4. Die App zeigt nun eine Adresse an, z.B. `http://192.168.1.42:5191/`

Auf diesem Gerät können Sie wie gewohnt Artikel anlegen und den Lagerbestand verwalten – alle Änderungen werden sofort via REST-API bereitgestellt.

### Web UI (Browser, empfohlen für Laptop/PC)

Öffne die angezeigte Adresse in einem **beliebigen Browser** im selben Netzwerk:

```
http://192.168.1.42:5191/
```

Du siehst die vollständige App-Oberfläche im Browser – inkl. Artikelliste, Lager, Einkaufsliste und Synchronisation.

> **Tipp**: Auf iOS zum Startbildschirm hinzufügen (Teilen-Button → "Zum Home-Bildschirm") – die Web-App funktioniert dann wie eine native App (PWA).

### VorratSync Client App (Android / iOS)

Die VorratSync-App verbindet sich automatisch mit dem Master:

1. VorratSync öffnen
2. **"Master suchen"** tippen – die App findet den Master per UDP-Broadcast im Netzwerk
3. Alternativ: Die Adresse **von Hand eingeben** (steht im Master-Modus)
4. **Verbinden → Sync** – alle Daten werden auf das Gerät geladen

Die App arbeitet **offline-first**: Änderungen am Client werden lokal gespeichert und bei nächster Synchronisation mit dem Master abgeglichen.

### Zugangsschlüssel (PIN)

Wenn im Master-Modus ein **Zugangsschlüssel (PIN)** gesetzt ist, wird bei Verbindung der Web UI oder Client-App automatisch nach der PIN gefragt. Ohne PIN ist der Zugriff nur im lokalen Netzwerk möglich (keine Internet-Freigabe).

---

## Für Entwickler

Die App ist in C# mit Xamarin.Android geschrieben. Der Sync-Server verwendet eine REST-API (JSON über HTTP). Für andere Plattformen gibt es einen .NET MAUI Client.

### Projektstruktur

- `Activities/` – Android Bildschirme (Aktivitäten)
- `Database/` – Datenbank-Zugriff und Modelle
- `Tools/` – Hilfsklassen (SyncServer, Discovery, WiFi-Direct)
- `Service/` – Hintergrunddienste (Sync-Client)
- `Assets/wwwroot/` – Web-Oberfläche (HTML/CSS/JS/PWA)
- `Client/` – .NET MAUI Cross-Platform Client für Windows/Mac/Linux/iOS
- `fdroid/` – F-Droid Repo-Konfiguration
- `.github/workflows/build.yml` – CI/CD Pipeline (build server APK + client Android + iOS)

### Build-Artefakte

| Plattform | Job | Runner |
|-----------|-----|--------|
| **Server APK** | Xamarin.Android in Docker | `ubuntu-latest` |
| **Client Android** | .NET MAUI `net10.0-android` | `ubuntu-latest` |
| **Client iOS** | .NET MAUI `net10.0-ios` (unsigned IPA) | `macos-26` |

Einzelheiten im [CI-Workflow](.github/workflows/build.yml).

### API-Endpunkte

| Methode | Pfad | Beschreibung |
|---------|------|-------------|
| `GET` | `/api/discovery` | Informationen über den Master |
| `GET` | `/api/discovery/ping` | Verbindungstest |
| `GET` | `/api/db/info` | Datenbank-Statistiken |
| `GET` | `/api/articles` | Alle Artikel abrufen |
| `POST` | `/api/articles` | Neuen Artikel anlegen |
| `GET/PUT/DELETE` | `/api/articles/{id}` | Artikel lesen/ändern/löschen |
| `GET` | `/api/storage-items` | Lagerbestand abrufen |
| `POST/DELETE` | `/api/storage-items/{id}` | Lagerposition anlegen/löschen |
| `GET` | `/api/shopping-items` | Einkaufsliste abrufen |
| `PUT/DELETE` | `/api/shopping-items/{id}` | Einkaufsartikel ändern/löschen |
| `GET` | `/api/sync/changes?since={ts}` | Änderungen seit Zeitstempel |
| `POST` | `/api/sync/push` | Eigene Änderungen hochladen |

👉 **[Ausführliche Sync-Dokumentation](ReadMe%20-%20Verteilte%20Datenbanken.txt)**

### Lizenz

Siehe [LICENSE](LICENSE) (Apache 2.0)
