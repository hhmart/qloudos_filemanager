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

Workflows
---------

Beispiel: Import mit Mapping und Anlage fehlender Zielbenutzer

```bash
dotnet run -- --db-connection db.sql --create-db --import C:\Temp\data --recursive --target-root /archive --map-users-file benutzer.txt --create-users-if-missing --apply-permissions --verbosity short
```

Beispiel: Export eines R3-Ordners in ein lokales Verzeichnis mit Mapping-Ausgabe

```bash
dotnet run -- --db-connection "Server=BC-W10-VMD;Database=db_r3;User Id=sa;Password=...;" --export /project=./out --map-users-file benutzer.txt --apply-permissions --verbosity short
```

Beispiel: Automatisches Erzeugen einer Mapping-Datei aus NTFS-ACLs (Windows-only)

```bash
dotnet run -- --auto-map "C:\\Shares\\Project=map.txt" --auto-map-recursive
```

Beispiel: SSL/TLS-Optionen beim SQL-Server (wenn Zertifikat nicht vertraut wird)

```powershell
dotnet run -- --db-connection "Server=BC-W10-VMD;Database=db_r3;User Id=sa;Password=...;" --trust-server-cert --encrypt true
```

Hinweis: Verwenden Sie `--trust-server-cert` nur in vertrauenswürdigen Umgebungen; besser ist es, das Serverzertifikat in den Vertrauensspeicher aufzunehmen oder ein Zertifikat einer vertrauenswürdigen CA zu verwenden.
