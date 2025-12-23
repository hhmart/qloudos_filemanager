using System;
using System.IO;
using System.Collections.Generic;
using QloudosFileManager.Models;
using QloudosFileManager.Utils;

namespace QloudosFileManager.Services
{
    /// <summary>
    /// Importiert lokale Dateien/Verzeichnisse in das R3-Datenbank-Filesystem.
    /// </summary>
    public class ImportService
    {
        private readonly DatabaseService _db;
        private readonly Logger _logger;

        public ImportService(DatabaseService db, Logger logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Importiert eine Liste von Pfaden. Dateien und Verzeichnisse werden entsprechend behandelt.
        /// </summary>
        public void ImportPaths(IEnumerable<string> paths, bool recursive, bool takeOwners)
        {
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    ImportFile(path, "/", takeOwners);
                }
                else if (Directory.Exists(path))
                {
                    var baseFolder = Path.GetFileName(path) ?? "";
                    ImportDirectory(path, "/" + baseFolder, recursive, takeOwners);
                }
                else
                {
                    _logger.Short($"Pfad nicht gefunden: {path}");
                }
            }
        }

        private void ImportDirectory(string lokalerPfad, string r3Pfad, bool recursive, bool takeOwners)
        {
            _logger.Short($"Importiere Ordner: {lokalerPfad} -> {r3Pfad}");
            foreach (var file in Directory.GetFiles(lokalerPfad))
            {
                ImportFile(file, r3Pfad, takeOwners);
            }
            if (recursive)
            {
                foreach (var dir in Directory.GetDirectories(lokalerPfad))
                {
                    var name = Path.GetFileName(dir) ?? "";
                    ImportDirectory(dir, r3Pfad.TrimEnd('/') + "/" + name, true, takeOwners);
                }
            }
        }

        private void ImportFile(string lokalerDateiPfad, string r3Pfad, bool takeOwners)
        {
            _logger.Verbose($"Lese Datei: {lokalerDateiPfad}");
            var bytes = File.ReadAllBytes(lokalerDateiPfad);
            var name = Path.GetFileName(lokalerDateiPfad) ?? lokalerDateiPfad;

            var ownerName = Environment.UserName;
            if (takeOwners)
            {
                // Vereinfachte Ermittlung: aktueller Benutzer als Owner
                ownerName = Environment.UserName;
            }

            var ownerId = _db.GetOrCreateUser(ownerName, ownerName);

            var fe = new FileEntry
            {
                Name = name,
                Path = r3Pfad,
                Content = bytes,
                OwnerId = ownerId,
                CreatedUtc = DateTime.UtcNow
            };

            var id = _db.SaveFile(fe);
            _logger.Short($"Importiert: {name} ({id}) in {r3Pfad}");
        }
    }
}