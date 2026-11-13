# Fahrzeug-Pack-Tool – Spezifikation

Arbeitsstand: Planung abgeschlossen, noch kein Code. Dieses Dokument hält die getroffenen Entscheidungen fest und – fast genauso wichtig – die Punkte, die noch im Spiel verifiziert werden müssen. Alles in Abschnitt „Zu verifizieren“ ist Annahme, kein Fakt. Wer daran Code baut, behandelt es auch so.

## Zweck

Das Tool ist eine Windows-Desktop-Anwendung. Es nimmt heruntergeladene, FiveM-fertige Fahrzeug-Resources entgegen, legt jedes Fahrzeug als Record in einer Bibliothek ab, prüft es auf Fehler und Konflikte und baut daraus Resources für den Server: frei definierbare Packs oder eine Einzel-Resource zum Testen. Es ist primär für die eigene Nutzung gedacht, soll aber so gebaut sein, dass später weitere Personen aus dem Serverteam damit arbeiten können.

Das Tool verändert keine Modell- oder Texturinhalte. Es arbeitet mit Dateien, Namen und Meta-Dateien. Modellarbeit (Sirenen-Bones einbauen, UVs, Optimierung) bleibt in Blender bzw. ist ein späteres, separates Thema.

## Begriffe

**Bibliothek** – ein Ordner, dessen Pfad beim ersten Start (Onboarding) festgelegt wird. Sie ist die einzige Quelle der Wahrheit und wird als Git-Repo geführt.

**Record** – die *Quelle* eines Fahrzeugs: die unveränderten Originaldateien (`.yft`, `_hi.yft`, `.ytd`, mitgelieferte Metas) plus eine `record.json`. Ein Record wird nie direkt gespawnt.

**Variante** – ein *baubares* Fahrzeug, abgeleitet aus einem Record. Jeder Record hat mindestens eine Variante. Eine Variante hat einen eigenen Spawnnamen, ein eigenes Siren-Setting, eine Livery-zu-Department-Zuordnung und Standard-Extras. Varianten teilen sich die Modelldateien ihres Records. Braucht ein Department ein anderes *Modell* (z. B. fest verbauter anderer Lichtbalken statt Extra), ist das ein eigener Record, keine Variante.

**Pack** – eine benannte Zusammenstellung von Records. Ein Record liegt mit allen seinen Varianten in genau einem Pack. Packs sind frei definierbar (geplant sind vorerst Einsatz- und Zivilfahrzeuge, das Tool schränkt das aber nicht ein). Ein Pack ist entweder *aktiv* (läuft auf dem Server) oder nicht.

**Build** – das Erzeugen einer FiveM-Resource aus einem Pack. Eine Test-Resource ist technisch ein Build eines temporären Packs mit genau einem Record. Build-Ergebnisse werden nie von Hand bearbeitet; der nächste Build überschreibt sie.

## Namensschema

Jeder Record bekommt einen Basisnamen aus Präfix, Kurzbezeichnung und Nummer, z. B. `ev_suv01` (Einsatzfahrzeug) oder `civ_golf01` (zivil). Varianten hängen einen Buchstaben an: `ev_suv01a`, `ev_suv01b`, `ev_suv01c`.

Auch Records mit nur einer Variante bekommen den Buchstaben (`civ_golf01a`). Grund: Kommt später eine zweite Variante dazu, muss kein bereits genutzter Spawnname umbenannt werden – umbenannte Spawnnamen brechen Garagen- und Datenbankeinträge im Framework.

Das Department steht bewusst *nicht* im Namen. Welche Variante und welche Livery zu welchem Department gehört, steht in den Daten.

Namen sollen kurz bleiben (siehe „Zu verifizieren“: mögliche Längengrenze).

## Aufbau der Bibliothek

```
<bibliothek>/
  records/
    ev_suv01/
      original/            # unveränderte Dateien aus dem Import, nach Import nie mehr geändert
        stream/...
        data/...
      record.json
  packs/
    einsatz.json
    zivil.json
  presets/
    sirens/                # Siren-Setting-Vorlagen als Startpunkt
    handling/              # handling-Vorlagen für generierte Metas
    vehicles/              # vehicles.meta-Vorlagen pro Fahrzeugtyp
  library.json             # Bibliotheks-Einstellungen (ID-Bereiche, Präfixe, Größen-Schwellenwerte)
  .cache/                  # normalisierte Dateien, gitignored
  build/                   # Build-Ergebnisse, gitignored
```

Der Pfad zur Bibliothek selbst wird nicht in der Bibliothek gespeichert, sondern in den lokalen Benutzereinstellungen des Tools (z. B. `%APPDATA%`), weil er pro Rechner unterschiedlich sein kann.

### record.json (Entwurf)

```json
{
  "id": "ev_suv01",
  "originalName": "fpiu20",
  "source": "Freitext: woher das Fahrzeug stammt",
  "importedAt": "2026-10-04",
  "category": "einsatz",
  "handlingId": "EV_SUV01",
  "generatedFiles": ["carcols.meta"],
  "checkStatus": { "errors": 0, "warnings": 2, "checkedAt": "2026-10-04" },
  "variants": [
    {
      "name": "ev_suv01a",
      "sirenSettingId": 1201,
      "sirenSetting": "variants/ev_suv01a.sirens.xml",
      "extras": { "1": true, "2": false },
      "liveries": [
        { "index": 1, "department": "SAPD" }
      ]
    },
    {
      "name": "ev_suv01b",
      "sirenSettingId": 1202,
      "sirenSetting": "variants/ev_suv01b.sirens.xml",
      "extras": { "1": false, "2": true },
      "liveries": [
        { "index": 2, "department": "SAMS" },
        { "index": 3, "department": "VFD" }
      ]
    }
  ]
}
```

Jede Variante speichert ihr vollständiges Siren-Setting, nicht nur einen Verweis auf ein Preset. Presets sind Startpunkte: Ein Siren-Setting ordnet Farben und Muster den Indizes `siren1`–`siren20` zu, und wo diese Bones sitzen, hängt vom Modell ab. Ein Preset passt deshalb selten unverändert auf ein anderes Fahrzeug.

### Pack-Definition (Entwurf)

```json
{
  "name": "einsatz",
  "resourceName": "vehicles_einsatz",
  "active": true,
  "records": ["ev_suv01", "ev_rtw01"]
}
```

## Import

Eingabe ist ein Ordner mit einer FiveM-fertigen Resource (`stream/`, Metas, `fxmanifest.lua`). Singleplayer-Mods mit `dlc.rpf` werden nicht unterstützt; die müssen vorher manuell mit CodeWalker oder OpenIV entpackt werden.

Viele Resources enthalten mehrere Fahrzeuge (mehrere `<Item>` in einer `vehicles.meta`, gemeinsame `carcols.meta`, gemischter `stream`-Ordner). Der Import zerlegt sie entlang der Referenzen in einzelne Records:

- `vehicles.meta` → `modelName` → Stream-Dateien (`<name>.yft`, `<name>_hi.yft`, `<name>.ytd`)
- `vehicles.meta` → `handlingId` → passender Block in `handling.meta`
- `carvariations.meta` → Kit-Name → Mod-Kit in `carcols.meta`
- `carvariations.meta` → Siren-ID → Siren-Setting in `carcols.meta`

Fälle, die der Import erkennt, meldet und **nicht** automatisch importiert:

- Fahrzeuge, die über `txdRelationships` ein gemeinsames Eltern-ytd nutzen (nicht sauber einem Record zuordenbar)
- Stream-Dateien, die keinem Fahrzeug zugeordnet werden können
- Referenzen, die ins Leere zeigen

Beim Import wird der Basisname vergeben und eine erste Variante (`…a`) aus den mitgelieferten Daten angelegt. Die Originaldateien werden unverändert nach `original/` kopiert.

## Normalisierung und Umbenennung

Die Normalisierung erzeugt aus `original/` + `record.json` die umbenannten Dateien im `.cache/`. Sie ist deterministisch: gleiche Eingabe, gleiches Ergebnis. Deshalb muss `.cache/` nicht ins Git.

Umbenannt bzw. angepasst werden pro Variante:

- Dateinamen: `.yft`, `_hi.yft` (und `.ytd`, falls nicht geteilt – siehe „Zu verifizieren“)
- `vehicles.meta`: `modelName`, `txdName`, `gameName`, ggf. `handlingId`
- `carvariations.meta`: `modelName`, Siren-ID, Kit-Name
- `carcols.meta`: Siren-Setting-ID, Kit-ID/-Name
- `handling.meta`: `handlingName`

## Prüfungen

Prüfungen laufen pro Record und zusätzlich über alle *aktiven* Packs hinweg. Jeder Befund ist entweder ein **Fehler** (blockiert den Build) oder eine **Warnung** (wird im Bericht ausgewiesen, blockiert nicht).

Fehler:

- doppelte Siren-Setting-IDs über alle aktiven Packs
- doppelte Mod-Kit-IDs oder Kit-Namen über alle aktiven Packs
- doppelte Spawnnamen oder Texture-Dictionary-Namen
- `modelName` passt nicht zu vorhandenen Stream-Dateien
- `handlingId` existiert nicht in einer `handling.meta`
- `carvariations` verweist auf ein nicht vorhandenes Kit oder Siren-Setting
- fehlende `.yft`, `_hi.yft` oder `.ytd`
- ein Record ist mehr als einem Pack zugeordnet

Warnungen:

- Dateigröße über Schwellenwert (in `library.json` konfigurierbar, siehe „Zu verifizieren“)
- Spawnname länger als konfigurierte Grenze
- generierte statt mitgelieferte Metas

Der Bericht wird als Konsolenausgabe und zusätzlich als Datei geschrieben.

## Generierung fehlender Metas

Metas werden nur generiert, wenn sie tatsächlich gebraucht werden. Eine fehlende `carcols.meta` ist kein Fehler, solange das Fahrzeug weder Sirenen noch ein eigenes Mod-Kit hat.

Generiert wird aus Presets in `presets/`, die vom Nutzer gepflegt und im Spiel verifiziert sind. Das Tool füllt nur Namen und IDs ein, es erfindet keine Meta-Werte. Generierte Dateien werden in `record.json` unter `generatedFiles` vermerkt und im Bericht ausgewiesen.

Einschränkung: Ob ein Modell überhaupt Siren-Bones hat, steht in der `.yft`. Ohne yft-Inspektion (spätere Phase, CodeWalker.Core) kann das Tool das nicht prüfen. Eine generierte Siren-Konfiguration für ein Modell ohne Bones funktioniert nicht.

## Build

Ein Build erzeugt aus einem Pack eine Resource unter `build/<resourceName>/`:

```
vehicles_einsatz/
  fxmanifest.lua
  stream/
    ev_suv01a/  ev_suv01a.yft  ev_suv01a_hi.yft  [ev_suv01a.ytd]
    ev_suv01b/  ...
  data/
    ev_suv01a/  vehicles.meta  carvariations.meta  carcols.meta  handling.meta
    ev_suv01b/  ...
```

Jede Variante bekommt eigene Meta-Dateien in einem eigenen Unterordner; Metas werden nicht zusammengeführt. Das `fxmanifest.lua` bindet sie per Wildcard ein, z. B. `data_file 'VEHICLE_METADATA_FILE' 'data/**/vehicles.meta'` (siehe „Zu verifizieren“).

Weil das Siren-Setting am Modellnamen hängt, braucht jede Variante eine eigene Kopie der `.yft`-Dateien unter ihrem Namen. Das erhöht die Downloadgröße auf Clientseite und ist bekannt und akzeptiert. Ob die Varianten sich das `.ytd` des Records über `txdName` teilen können, ist offen.

Die Test-Resource (`test <record>`) baut einen Record mit allen Varianten als eigene Resource, damit die Varianten im Spiel nebeneinander verglichen werden können.

## Framework-Export

Exporter sind austauschbare Module mit der Aufgabe: „nimm die Varianten eines Packs und schreibe ein Format X“. Der Exporter für das eigene Framework (Fahrzeugliste für Garagen/Dienstfahrzeuge inkl. Department → Variante + Livery + Extras) wird gebaut, sobald das Zielformat im Framework nachgeschaut ist. Bis dahin blockiert er nichts.

## Git und Mehrbenutzerbetrieb

Die Bibliothek ist ein Git-Repo mit Git LFS für Binärdateien (`*.yft`, `*.ytd`). Versioniert werden nur `records/*/original/`, alle JSON-Dateien und `presets/`. `.cache/` und `build/` stehen in der `.gitignore`.

Jede Binärdatei landet so genau einmal in der Historie. Weil jeder Record eine eigene `record.json` hat, sind Merge-Konflikte selten. Den einzigen realistischen Konflikt – zwei Personen vergeben parallel dieselbe Siren-ID – fängt die ID-Prüfung beim Build ab.

## Tech-Stack

**Sprache und Runtime:** C# auf .NET 10 (LTS). Nicht .NET 8, dessen Support im November 2026 endet.

Warum C# und nicht TypeScript: CodeWalker.Core (die Bibliothek für RAGE-Binärformate wie `.yft`/`.ytd`) zielt auf .NET Standard 2.0 und lässt sich direkt einbinden. Mit C# bleibt das Tool eine einzige Anwendung, auch wenn spätere Phasen Binärformate lesen oder schreiben. Mit TypeScript bräuchte es dafür ein zweites Programm.

**Projektstruktur:**

- `Core` – Klassenbibliothek mit der gesamten Logik: Bibliothek, Records, Varianten, Import, Normalisierung, Prüfungen, Build, Exporter. Kennt keine Oberfläche.
- `App` – WPF-Anwendung, reine Hülle um `Core`. MVVM, z. B. mit CommunityToolkit.Mvvm.
- `Cli` – kleine Kommandozeilen-Hülle um `Core`. Nicht für Endnutzer gedacht, sondern damit Import, Prüfung und Build automatisiert und ohne Klicken getestet werden können (auch von Claude Code).
- `Tests` – xUnit, mit echten Testfahrzeugen als Fixtures.

Regel: Logik gehört immer in `Core`. Was in `App` oder `Cli` mehr tut als Eingaben entgegennehmen und Ergebnisse anzeigen, ist falsch platziert.

**Bausteine:**

- Oberfläche: WPF (nur Windows, bewusst gewählt: ausgereift, gut dokumentiert, für Claude Code verlässlich)
- XML: `System.Xml.Linq` (XDocument); Struktur und Reihenfolge der Metas beim Umschreiben erhalten
- JSON: `System.Text.Json`
- CLI: Spectre.Console / `Spectre.Console.Cli`
- Git: Aufruf der `git`-CLI als Prozess, keine Git-Bibliothek (libgit2 unterstützt Git LFS kaum). Git und Git LFS müssen installiert sein; das Onboarding prüft das und meldet es verständlich.
- RAGE-Binärformate (ab Phase 4): CodeWalker.Core. Abhängigkeit ist SharpDX (nur Windows). Ob CodeWalker.Core als NuGet-Paket verfügbar ist oder als Git-Submodul/Projektreferenz eingebunden werden muss, ist offen. Lizenz vor Einbindung prüfen.

**Auslieferung:** self-contained single-file Publish für `win-x64`. Eine `.exe` ohne Installation und ohne vorinstallierte .NET-Runtime. Die Dateigröße (bis ca. 150 MB) ist bewusst akzeptiert. Trimming wird für WPF nicht genutzt.

**Onboarding (erster Start der App):** Bibliothekspfad wählen (neu anlegen oder bestehendes Repo öffnen), Git/Git LFS prüfen, bei neuer Bibliothek Repo mit `.gitignore` und LFS-Konfiguration anlegen.

Befehle der CLI (Entwurf, gleiche Funktionen wie die App):

```
tool init                         # Onboarding: Bibliothekspfad festlegen, Repo anlegen
tool import <ordner>              # Resource importieren, ggf. in mehrere Records zerlegen
tool check [<record>|--all]       # Prüfungen ausführen, Bericht ausgeben
tool variant add <record>         # neue Variante anlegen
tool pack create <name>
tool pack add <pack> <record>
tool build <pack>
tool test <record>                # Einzel-Resource zum Testen
tool export <pack> --format <x>
```

## Phasen

1. Bibliothek, Import inkl. Zerlegung, Normalisierung/Umbenennung, Varianten, Prüfungen, Build, Test-Resource – in `Core`, bedienbar über `Cli` und eine schlichte WPF-Oberfläche (Onboarding, Record-Liste, Varianten bearbeiten, Packs zuweisen, Prüfbericht, Build)
2. Generierung fehlender Metas aus Presets
3. Framework-Exporter
4. Lesende yft-Inspektion (Siren-Bones vorhanden? Bone-Namen?) über CodeWalker.Core
5. Später, separat zu bewerten: Texturoptimierung, Skinning-Templates

## Zu verifizieren

Annahmen, die vor bzw. während Phase 1 im Spiel getestet werden müssen. Ergebnis und Datum hier eintragen.

| Annahme | Warum relevant | Status |
|---|---|---|
| `data_file` im `fxmanifest` akzeptiert Wildcards (`data/**/vehicles.meta`) | Grundlage für „Metas pro Variante getrennt“ | offen |
| Varianten können über `txdName` ein gemeinsames `.ytd` nutzen, ohne Probleme mit Liveries | spart Downloadgröße | offen |
| Siren-Setting wird pro Modell in `carvariations` zugewiesen, nicht pro Livery | Begründung für das Variantenmodell | offen (ziemlich sicher) |
| Mögliche Längengrenze für Spawnnamen (vermutet um 11 Zeichen) | Namensschema | offen |
| Wertebereich bzw. Limit für Siren-Setting-IDs in FiveM | ID-Vergabe | offen |
| Wie Liveries (`sign_*`) in `carvariations` deklariert werden müssen | Livery-Zuordnung pro Variante | offen |
| Aktueller FiveM-Schwellenwert für Oversized-Asset-Warnungen | Standardwert in `library.json` | offen |
| Umbenennen von `.yft`/`.ytd` ohne Änderung des Dateiinhalts funktioniert | Grundlage der Normalisierung | offen |
| CodeWalker.Core zielt auf .NET Standard 2.0 und ist aus .NET 10 nutzbar | Begründung für C# als einzige Sprache | geprüft (Projektdatei, 04.10.2026); praktischer Test beim ersten Einbinden steht aus |
| Lizenz von CodeWalker erlaubt die Einbindung in ein eigenes Tool | Voraussetzung für Phase 4 | offen |

## Bewusst nicht enthalten

- `dlc.rpf`-Import
- Bearbeitung von Modell- oder Texturinhalten
- automatisches Mergen von Metas
- Fahrzeuge mit geteiltem Eltern-ytd (`txdRelationships`) – werden erkannt und abgelehnt
- Unterstützung anderer Betriebssysteme als Windows