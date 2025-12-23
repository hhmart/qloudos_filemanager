using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using QloudosFileManager.Models;

namespace QloudosFileManager.Services
{
    /// <summary>
    /// Service zum Laden/Speichern von Benutzerzuordnungen und für automatisches Mapping anhand von NTFS-ACLs.
    /// </summary>
    public class UserMappingService
    {
        /// <summary>
        /// Lädt ein Mapping aus einer Textdatei im Format local=r3 pro Zeile.
        /// </summary>
        public static Dictionary<string, string> LoadMapping(string filePath)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(filePath)) return dict;
            foreach (var line in File.ReadAllLines(filePath))
            {
                var t = line.Trim();
                if (string.IsNullOrWhiteSpace(t) || t.StartsWith("#")) continue;
                var parts = t.Split('=', 2);
                if (parts.Length == 2)
                {
                    dict[parts[0].Trim()] = parts[1].Trim();
                }
            }
            return dict;
        }

        /// <summary>
        /// Speichert ein Mapping in eine Datei (überschreibt).
        /// </summary>
        public static void SaveMapping(Dictionary<string, string> mapping, string filePath)
        {
            var lines = mapping.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => kv.Key + "=" + kv.Value);
            File.WriteAllLines(filePath, lines);
        }

        /// <summary>
        /// Automatisches Mapping: durchsucht ein Verzeichnis nach NTFS-ACL-Benutzern, vergleicht mit Zielsystem-Benutzern und erzeugt Mapping.
        /// </summary>
        public static Dictionary<string, string> AutoMapFromAcl(string searchPath, bool recursive, IEnumerable<UserEntry> targetUsers)
        {
            var localUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(searchPath))
            {
                var dirs = new Queue<string>();
                dirs.Enqueue(searchPath);
                while (dirs.Count > 0)
                {
                    var d = dirs.Dequeue();
                    try
                    {
                        var acl = new DirectoryInfo(d).GetAccessControl();
                        foreach (AuthorizationRule r in acl.GetAccessRules(true, true, typeof(NTAccount)))
                        {
                            localUsers.Add(r.IdentityReference.Value.Split('\\').Last());
                        }
                    }
                    catch { }
                    try
                    {
                        foreach (var f in Directory.GetFiles(d))
                        {
                            try
                            {
                                var facl = new FileInfo(f).GetAccessControl();
                                foreach (AuthorizationRule r in facl.GetAccessRules(true, true, typeof(NTAccount)))
                                {
                                    localUsers.Add(r.IdentityReference.Value.Split('\\').Last());
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                    if (recursive)
                    {
                        try
                        {
                            foreach (var sd in Directory.GetDirectories(d)) dirs.Enqueue(sd);
                        }
                        catch { }
                    }
                }
            }

            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var targets = targetUsers.ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase);
            foreach (var lu in localUsers)
            {
                // direkte Übereinstimmung
                if (targets.ContainsKey(lu)) mapping[lu] = lu;
                else
                {
                    // versuche Fuzzy: match by startswith or contains
                    var candidate = targets.Keys.FirstOrDefault(k => k.Equals(lu, StringComparison.OrdinalIgnoreCase) || k.StartsWith(lu, StringComparison.OrdinalIgnoreCase) || k.Contains(lu, StringComparison.OrdinalIgnoreCase));
                    mapping[lu] = candidate ?? string.Empty;
                }
            }
            return mapping;
        }
    }
}
