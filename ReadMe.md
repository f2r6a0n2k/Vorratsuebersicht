# Vorratsübersicht - Haltbarkeitsdatum überwachen

Mit dieser App behalten Sie den Überblick über Ihre Vorräte und deren Mindesthaltbarkeitsdatum. Nie wieder Lebensmittel wegwerfen, weil Sie vergessen haben, was im Schrank steht!

## Neu: Daten auf mehreren Geräten synchronisieren

Seit Version 9.00 können Sie Ihre Daten mit anderen Geräten teilen - **ganz ohne Internet, Cloud oder Server**. Die Geräte müssen sich nur im selben WLAN befinden. 

- **Telefon als Master**: Ein Gerät gibt die Daten frei (Master-Modus)
- **Andere Geräte lesen mit**: Browser, Tablet, Laptop, PC
- **Sync-Client App**: Für Windows, Mac, Linux und iPhone verfügbar
- **Funktioniert off-grid**: Auch ohne Internet (z.B. über Handy-Hotspot)
- **Web-Oberfläche**: Jeder Browser kann die Daten anzeigen und bearbeiten

👉 **[Zur ausführlichen Anleitung: ReadMe - Verteilte Datenbanken.txt](ReadMe%20-%20Verteilte%20Datenbanken.txt)**

### Kurzanleitung für die Synchronisation:

1. Auf dem Telefon: App öffnen → Master-Modus → "Server starten"
2. Die angezeigte Adresse (z.B. `http://192.168.1.42:5191/`) im Browser eines anderen Geräts öffnen
3. Oder: Die VorratSync-App auf Windows/Mac/Linux/iPhone starten und nach Master suchen lassen

## Funktionsweise

1. **Artikel anlegen** - Einmalig in der Artikelliste mit Namen, Kategorie, EAN-Code, etc. erfassen
2. **Lagerbestand führen** - Artikel mit Menge und Haltbarkeitsdatum zum Lager hinzufügen
3. **Warnungen erhalten** - Die App zeigt an, welche Artikel bald ablaufen oder schon abgelaufen sind
4. **Einkaufsliste** - Fehlende Artikel können direkt auf die Einkaufsliste gesetzt werden

## Testdatenbank

Zum Kennenlernen können Sie auf eine Testdatenbank mit Beispiel-Artikeln umschalten.

## Für Entwickler

Die App ist in C# mit Xamarin.Android geschrieben. Der Sync-Server verwendet eine REST-API (JSON über HTTP). Für andere Plattformen gibt es einen .NET MAUI Client.

### Projektstruktur

- `Activities/` - Android Bildschirme (Aktivitäten)
- `Database/` - Datenbank-Zugriff und Modelle
- `Tools/` - Hilfsklassen (SyncServer, Discovery, WiFi-Direct)
- `Service/` - Hintergrunddienste (Sync-Client)
- `Assets/wwwroot/` - Web-Oberfläche (HTML/CSS/JS/PWA)
- `Client/` - .NET MAUI Cross-Platform Client für Windows/Mac/Linux/iOS

### Wichtige Neuerungen (Version 9.00+)

- SyncChangeLog: Alle Datenänderungen werden protokolliert
- REST-API mit CORS-Unterstützung und CamelCase-JSON
- UDP Discovery: Automatische Mastersuche im Netzwerk
- WiFi-Direct: Vorbereitung für direkte Geräte-zu-Geräte Verbindung
- Web UI als Progressive Web App (PWA) mit Service Worker
- .NET MAUI Client für Windows, Mac, Linux und iOS

## Bekannte Probleme

1. Deutsche Umlaute werden nicht korrekt sortiert (SQLite-Einschränkung)
2. Keine Push-Benachrichtigungen bei ablaufenden Artikeln
3. Die Sync-Funktion hat noch keine Passwort-Absicherung (daher nur im eigenen Netzwerk nutzen)

## Lizenz

Siehe [LICENSE](LICENSE)
