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
        public void ImportPaths(IEnumerable<string> paths, bool recursive, bool takeOwners, string r3Root,
            System.Collections.Generic.Dictionary<string,string>? userMapping = null,
            bool createMissingUsers = false,
            bool applyPermissions = false,
            System.Collections.Generic.List<string>? blacklist = null)
        {
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    ImportFile(path, r3Root ?? "/", takeOwners, userMapping, createMissingUsers, applyPermissions, blacklist);
                }
                else if (Directory.Exists(path)) 
                {
                    var baseFolder = Path.GetFileName(path) ?? "";
                    var target = (r3Root ?? "/").TrimEnd('/') + "/" + baseFolder;
                    ImportDirectory(path, target, recursive, takeOwners, userMapping, createMissingUsers, applyPermissions, blacklist);
                }
                else
                {
                    _logger.Short($"Pfad nicht gefunden: {path}");
                }
            }
        }

        private void ImportDirectory(string lokalerPfad, string r3Pfad, bool recursive, bool takeOwners,
            System.Collections.Generic.Dictionary<string,string>? userMapping,
            bool createMissingUsers,
            bool applyPermissions,
            System.Collections.Generic.List<string>? blacklist)
        {
            _logger.Short($"Importiere Ordner: {lokalerPfad} -> {r3Pfad}");
            foreach (var file in Directory.GetFiles(lokalerPfad))
            {
                ImportFile(file, r3Pfad, takeOwners, userMapping, createMissingUsers, applyPermissions, blacklist);
            }
            if (recursive)
            {
                foreach (var dir in Directory.GetDirectories(lokalerPfad))
                {
                    var name = Path.GetFileName(dir) ?? "";
                    ImportDirectory(dir, r3Pfad.TrimEnd('/') + "/" + name, true, takeOwners, userMapping, createMissingUsers, applyPermissions, blacklist);
                }
            }
        }

        private void ImportFile(string lokalerDateiPfad, string r3Pfad, bool takeOwners,
            System.Collections.Generic.Dictionary<string,string>? userMapping,
            bool createMissingUsers,
            bool applyPermissions,
            System.Collections.Generic.List<string>? blacklist)
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

            // Berechtigungen übernehmen (vereinfacht)
            if (applyPermissions)
            {
                try
                {
                    var fac = new FileInfo(lokalerDateiPfad).GetAccessControl();
                    foreach (System.Security.AccessControl.FileSystemAccessRule rule in fac.GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount)))
                    {
                        var localUser = rule.IdentityReference.Value.Split('\\').Last();
                        if (blacklist != null && blacklist.Contains(localUser, System.StringComparer.OrdinalIgnoreCase)) continue;
                        string targetUser = string.Empty;
                        if (userMapping != null && userMapping.TryGetValue(localUser, out var mapped)) targetUser = mapped;
                        var targetUserEntry = _db.GetUserByUsername(targetUser ?? string.Empty);
                        if (targetUserEntry == null)
                        {
                            if (createMissingUsers && !string.IsNullOrWhiteSpace(targetUser))
                            {
                                var newId = _db.CreateUser(targetUser, targetUser);
                                targetUserEntry = _db.GetUserByUsername(targetUser);
                            }
                        }
                        if (targetUserEntry != null)
                        {
                            var rights = rule.FileSystemRights.ToString();
                            var perm = new PermissionEntry { UserId = targetUserEntry.Id, ObjectType = "file", ObjectId = id, Rights = rights };
                            _db.AddPermission(perm);
                        }
                    }
                }
                catch { }
            }
        }
    }
}