# Beispiel: Import mit Benutzer-Mapping und Anlage fehlender Zielbenutzer
# Nutzung: Powershell öffnen, in Projektordner wechseln und das Skript ausführen.

# Pfad zur Datenbank-Connection (db.sql enthält den ConnectionString) und Quellverzeichnis
$dbFile = "db.sql"
$source = "C:\Temp\data"

# Mapping-Datei (format: localUser=r3User)
$mapping = "benutzer.txt"

# Zielpfad im R3-Dateisystem
$targetRoot = "/archive"

# Beispielaufruf
dotnet run -- --db-connection $dbFile --create-db --import $source --recursive --target-root $targetRoot --map-users-file $mapping --create-users-if-missing --apply-permissions --verbosity short

# Beispiel: Export eines R3-Ordners
# dotnet run -- --db-connection $dbFile --export /project=./out --map-users-file $mapping --apply-permissions --verbosity short

Write-Host "Fertig. Prüfen Sie die Log-Ausgaben oben."