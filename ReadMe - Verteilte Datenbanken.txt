Verteilte Datenbank - Synchronisation
======================================

Mit dieser Funktion k�nnen Sie Ihre Vorratsdaten auf mehreren Ger�ten nutzen.
Zum Beispiel:
- Telefon (Master): Hier werden alle Daten eingegeben und verwaltet
- Tablet, Laptop, PC: Zeigen die Daten an oder �ndern sie
- Familienmitglieder: K�nnen mit ihrem eigenen Ger�t auf die Daten zugreifen

Wichtig: Es gibt KEINEN zentralen Server im Internet.
Die Ger�te m�ssen sich im selben Netzwerk befinden (WLAN zu Hause).
Internetzugang ist NICHT erforderlich - alles funktioniert auch ohne Internet
(Off-Grid).


So funktioniert es:
===================

1. Master (Ihr Telefon mit der Vorrats�bersicht App):
   - �ffnen Sie die App
   - Gehen Sie zu den Einstellungen / Master-Modus
   - Tippen Sie auf "Server starten"
   - Ihr Telefon ist jetzt der Master und stellt die Daten bereit

2. Andere Ger�te:
   - �ffnen Sie einen Internet-Browser (Firefox, Chrome, Safari)
   - Geben Sie die Adresse ein, die im Master-Modus angezeigt wird
     (z.B. http://192.168.1.42:5191/)
   - Oder: Scannen Sie den QR-Code im Master-Modus mit dem anderen Ger�t
   - Schon sehen Sie alle Ihre Daten im Browser!

3. Sync-Client App (f�r Windows, Mac, Linux, iPhone):
   - Starten Sie die VorratSync App
   - Tippen Sie auf "Master suchen" - die App findet den Master automatisch
   - Oder: Geben Sie die Adresse von Hand ein (steht im Master-Modus)
   - Tippen Sie auf "Verbinden" und dann auf "Sync"
   - Fertig! Alle Daten sind jetzt auch auf diesem Ger�t


Ohne WLAN-Router (Off-Grid / unterwegs):
=========================================

Kein WLAN vorhanden? Kein Problem! Es gibt mehrere M�glichkeiten:

M�glichkeit 1: WLAN-Hotspot
   - Machen Sie aus Ihrem Telefon einen WLAN-Hotspot (einrichten in den
     Telefon-Einstellungen unter "Hotspot" oder "Tethering")
   - Verbinden Sie das andere Ger�t mit diesem Hotspot
   - Starten Sie den Master-Modus auf dem Telefon
   - Verbinden Sie sich vom anderen Ger�t aus wie oben beschrieben

M�glichkeit 2: WLAN-Direct (kommt demn�chst)
   - Erm�glicht die direkte Verbindung zweier Ger�te ohne Router
   - Wird in einer der n�chsten Versionen erg�nzt

M�glichkeit 3: Browser (immer m�glich)
   - Auch ohne Sync-Client k�nnen Sie einfach den Browser nutzen
   - Die Web-Oberfl�che funktioniert auf jedem Ger�t mit Browser


Welche Daten werden synchronisiert?
====================================

- Alle Artikel (Name, Kategorie, Hersteller, EAN, etc.)
- Der gesamte Lagerbestand (Mengen, Mindesthaltbarkeitsdaten, Lagerorte)
- Die Einkaufsliste

Bilder werden NICHT synchronisiert (zu gro� f�rs schnelle Netzwerk).


Technische Details (f�r Entwickler):
=====================================

Protokoll: REST-API via HTTP, JSON-Format
Port:      HTTP 5191, UDP 5190 (Discovery)
Sync:      bidirektional mit �nderungsprotokoll (SyncChangeLog-Tabelle)
           Volle Synchronisation oder inkrementell (nur �nderungen)
Discovery: UDP-Broadcast "VORRAT_DISCOVERY" auf Port 5190
           Antwort: "VORRAT_MASTER|{hostname}|{port}|{databaseId}"
Sicherheit: Derzeit kein Passwort-Schutz (nur im lokalen Netzwerk empfohlen)

API-Endpunkte:
  GET  /api/discovery          - Informationen �ber den Master
  GET  /api/discovery/ping     - Verbindungstest
  GET  /api/db/info            - Datenbank-Statistiken
  GET  /api/articles           - Alle Artikel abrufen
  POST /api/articles           - Neuen Artikel anlegen
  GET  /api/articles/{id}      - Einzelnen Artikel abrufen
  PUT  /api/articles/{id}      - Artikel �ndern
  DELETE /api/articles/{id}    - Artikel l�schen
  GET  /api/storage-items      - Lagerbestand abrufen
  POST /api/storage-items      - Lagerposition anlegen
  DELETE /api/storage-items/{id} - Lagerposition l�schen
  GET  /api/shopping-items     - Einkaufsliste abrufen
  POST /api/shopping-items     - Einkaufsartikel hinzuf�gen
  PUT  /api/shopping-items/{id} - Einkaufsartikel �ndern
  DELETE /api/shopping-items/{id} - Einkaufsartikel l�schen
  GET  /api/sync/changes?since={timestamp} - �nderungen seit Zeitpunkt
  POST /api/sync/push          - Eigene �nderungen hochladen
