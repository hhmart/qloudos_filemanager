qloudos_filemanager
====================

Kurze Anleitung (Deutsch)

Kompiliere mit dotnet (SDK 7+):

```bash
dotnet build
```

Beispiel: Erstelle DB und importiere rekursiv

```bash
dotnet run -- --create-db --db-connection db_r3.sqlite --import C:\Temp\daten --recursive
```

Export Beispiel

```bash
dotnet run -- --export /myfolder=./out --verbosity verbose
```

Parameter sind englisch (z.B. `--import`, `--export`), Hilfe und Ausgaben sind deutsch.
