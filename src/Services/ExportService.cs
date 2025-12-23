using System;
using System.IO;
using System.Collections.Generic;
using QloudosFileManager.Models;
using QloudosFileManager.Utils;

namespace QloudosFileManager.Services
{
    /// <summary>
    /// Exportiert Dateien aus dem R3-DB-Filesystem in das lokale Dateisystem.
    /// </summary>
    public class ExportService
    {
        private readonly DatabaseService _db;
        private readonly Logger _logger;

        public ExportService(DatabaseService db, Logger logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Exportiert Dateien eines R3-Pfads in ein lokales Zielverzeichnis.
        /// </summary>
        public void ExportPath(string r3Path, string localTargetDir, bool recursive, System.Collections.Generic.Dictionary<string,string>? userMapping)
        {
            _logger.Short($"Exportiere R3:{r3Path} -> {localTargetDir}");
            Directory.CreateDirectory(localTargetDir);
            var files = _db.GetFiles(r3Path, recursive);
            foreach (var f in files)
            {
                var outDir = Path.Combine(localTargetDir, f.Path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(outDir);
                var outPath = Path.Combine(outDir, f.Name);
                File.WriteAllBytes(outPath, f.Content);
                _logger.Short($"Exportiert: {outPath}");
            }
        }
    }
}