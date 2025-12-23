qloudos_filemanager
====================

Kurze Anleitung (Deutsch)

Zielumgebung:
- Microsoft Visual Studio Community 2022 (64-Bit) Version 17.14.13 (August 2025)
- .NET 10 SDK

Kompilieren und Ausführen mit `dotnet`:

```bash
dotnet build
dotnet run -- --help
```

Beispiel: Erstelle DB und importiere rekursiv

```bash
dotnet run -- --create-db --db-connection db_r3.sqlite --import C:\Temp\daten --recursive
```

Export Beispiel

```bash
dotnet run -- --export /myfolder=./out --verbosity verbose
```

Wichtige Dateien:
- `db.sql` : optionale Textdatei mit SQL-Server-ConnectionString
- `benutzer.txt` : leere Datei, kann für Benutzerzuordnungen genutzt werden

Parameter sind englisch (z.B. `--import`, `--export`), Hilfe und Ausgaben sind deutsch.
