using System;
using System.Collections.Generic;
using System.IO;
using QloudosFileManager.Services;
using QloudosFileManager.Utils;

namespace QloudosFileManager
{
    /// <summary>
    /// Hauptprogramm der Konsolenanwendung `qloudos_filemanager`.
    /// CLI-Parameter sind englisch, Ausgaben und Hilfe sind deutsch.
    /// </summary>
    internal class Program
    {
        static int Main(string[] args)
        {
            // Standardwerte
            string dbFile = "db_r3.sqlite";
            bool createDbIfMissing = false;
            string targetRoot = "/"; // Verzeichnisebene im R3, ab der importiert wird (default: root)
            var importPaths = new List<string>();
            var exportPaths = new List<string>();
            bool recursive = false;
            bool takeOwners = false;
            string? mapUsersFile = null;
            bool createUsersIfMissing = false;
            bool applyPermissions = false;
            string? autoMapArg = null;
            bool autoMapRecursive = false;
            string? userManageFile = null;
            string? userManageAction = null;
            string? blacklistFile = null;
            Logger.Stufe verbosity = Logger.Stufe.Short;

            // Einfache Argument-Verarbeitung
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i].ToLowerInvariant();
                switch (a)
                {
                    case "--help":
                    case "-h":
                    case "help":
                        PrintHelp();
                        return 0;
                    case "--db-connection":
                        if (i + 1 < args.Length) { dbFile = args[++i]; } else { Console.WriteLine("Fehlender Wert für --db-connection"); return 2; }
                        break;
                    case "--target-root":
                        if (i + 1 < args.Length) { targetRoot = args[++i]; } else { Console.WriteLine("Fehlender Wert für --target-root"); return 2; }
                        break;
                    case "--create-db":
                        createDbIfMissing = true;
                        break;
                    case "--import":
                        if (i + 1 < args.Length) { importPaths.Add(args[++i]); } else { Console.WriteLine("Fehlender Wert für --import"); return 2; }
                        break;
                    case "--export":
                        if (i + 1 < args.Length) { exportPaths.Add(args[++i]); } else { Console.WriteLine("Fehlender Wert für --export"); return 2; }
                        break;
                    case "--recursive":
                        recursive = true; break;
                    case "--take-owners":
                        takeOwners = true; break;
                    case "--map-users-file":
                        if (i + 1 < args.Length) { mapUsersFile = args[++i]; } else { Console.WriteLine("Fehlender Wert für --map-users-file"); return 2; }
                        break;
                    case "--create-users-if-missing":
                        createUsersIfMissing = true; break;
                    case "--apply-permissions":
                        applyPermissions = true; break;
                    case "--auto-map":
                        if (i + 1 < args.Length) { autoMapArg = args[++i]; } else { Console.WriteLine("Fehlender Wert für --auto-map"); return 2; }
                        break;
                    case "--auto-map-recursive":
                        autoMapRecursive = true; break;
                    case "--user-manage-file":
                        if (i + 1 < args.Length) { userManageFile = args[++i]; } else { Console.WriteLine("Fehlender Wert für --user-manage-file"); return 2; }
                        break;
                    case "--user-manage-action":
                        if (i + 1 < args.Length) { userManageAction = args[++i]; } else { Console.WriteLine("Fehlender Wert für --user-manage-action"); return 2; }
                        break;
                    case "--blacklist":
                        if (i + 1 < args.Length) { blacklistFile = args[++i]; } else { Console.WriteLine("Fehlender Wert für --blacklist"); return 2; }
                        break;
                    case "--verbosity":
                        if (i + 1 < args.Length)
                        {
                            var v = args[++i].ToLowerInvariant();
                            verbosity = v switch { "none" => Logger.Stufe.None, "short" => Logger.Stufe.Short, "verbose" => Logger.Stufe.Verbose, _ => Logger.Stufe.Short };
                        }
                        else { Console.WriteLine("Fehlender Wert für --verbosity"); return 2; }
                        break;
                    default:
                        // unbekannt: erlauben, dass Pfade ohne Flag übergeben werden
                        if (a.StartsWith("-") )
                        {
                            Console.WriteLine($"Unbekannter Parameter: {args[i]}");
                            return 2;
                        }
                        importPaths.Add(args[i]);
                        break;
                }
            }

            var logger = new Logger(verbosity);

            // Verbindung bauen: wenn dbFile ein ConnectionString enthält (z.B. 'Server='), benutze direkt.
            // Wenn dbFile ein Pfad zu einer Datei ist, wird der Inhalt gelesen und als ConnectionString verwendet.
            string connectionString;
            bool looksLikeConnectionString(string s) => !string.IsNullOrWhiteSpace(s) && s.Contains('=');

            if (looksLikeConnectionString(dbFile))
            {
                // Direkter ConnectionString übergeben
                connectionString = dbFile;
            }
            else if (File.Exists(dbFile))
            {
                // Datei existiert; lies Inhalt und bestimme, ob es ein ConnectionString oder SQLite-Datei ist
                var content = File.ReadAllText(dbFile).Trim();
                if (looksLikeConnectionString(content)) connectionString = content;
                else connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbFile }.ToString();
            }
            else if (File.Exists("db.sql"))
            {
                var content = File.ReadAllText("db.sql").Trim();
                if (looksLikeConnectionString(content)) connectionString = content;
                else connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = "db_r3.sqlite" }.ToString();
            }
            else
            {
                // Standard: SQLite-Datei
                connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbFile }.ToString();
            }

            try
            {
                using var db = new DatabaseService(connectionString, createDbIfMissing);
                db.Open();

                var importer = new ImportService(db, logger);
                var exporter = new ExportService(db, logger);
                var mapping = new System.Collections.Generic.Dictionary<string,string>(System.StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(mapUsersFile) && File.Exists(mapUsersFile))
                {
                    mapping = UserMappingService.LoadMapping(mapUsersFile);
                }

                // blacklist
                var blacklist = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(blacklistFile) && File.Exists(blacklistFile))
                {
                    foreach (var l in File.ReadAllLines(blacklistFile)) if (!string.IsNullOrWhiteSpace(l)) blacklist.Add(l.Trim());
                }

                // auto-map: format <searchPath>=<outFile> or just <searchPath>
                if (!string.IsNullOrWhiteSpace(autoMapArg))
                {
                    var outFile = "auto_mapping.txt";
                    var searchPath = autoMapArg;
                    if (autoMapArg.Contains('=')) { var parts = autoMapArg.Split('=',2); searchPath = parts[0]; outFile = parts[1]; }
                    var auto = UserMappingService.AutoMapFromAcl(searchPath, autoMapRecursive, db.GetAllUsers());
                    UserMappingService.SaveMapping(auto, outFile);
                    Console.WriteLine($"Automapping erzeugt: {outFile}");
                }

                // user-manage: apply mapping file to create/delete users in target system
                if (!string.IsNullOrWhiteSpace(userManageFile) && File.Exists(userManageFile) && !string.IsNullOrWhiteSpace(userManageAction))
                {
                    var um = UserMappingService.LoadMapping(userManageFile);
                    foreach (var kv in um)
                    {
                        if (blacklist.Contains(kv.Key) || blacklist.Contains(kv.Value)) continue;
                        if (userManageAction == "add-first" || userManageAction == "add-both") db.CreateUser(kv.Key, kv.Key);
                        if (userManageAction == "add-second" || userManageAction == "add-both") if (!string.IsNullOrWhiteSpace(kv.Value)) db.CreateUser(kv.Value, kv.Value);
                        if (userManageAction == "delete-first" || userManageAction == "delete-both") db.DeleteUserByUsername(kv.Key);
                        if (userManageAction == "delete-second" || userManageAction == "delete-both") if (!string.IsNullOrWhiteSpace(kv.Value)) db.DeleteUserByUsername(kv.Value);
                    }
                }

                if (importPaths.Count == 0 && exportPaths.Count == 0)
                {
                    logger.Short("Keine Aktion angegeben. Verwenden Sie --help für Hilfe.");
                }

                if (importPaths.Count > 0)
                {
                    importer.ImportPaths(importPaths, recursive, takeOwners, targetRoot, mapping, createUsersIfMissing, applyPermissions, blacklist);
                }

                if (exportPaths.Count > 0)
                {
                    // exportPaths: erwartetes Format: r3Path=localTarget oder wenn nur r3Path angegeben, dann export in ./export/{r3Path}
                    foreach (var ep in exportPaths)
                    {
                        string r3p = ep;
                        string localTarget = Path.Combine(Environment.CurrentDirectory, "export");
                        if (ep.Contains('='))
                        {
                            var parts = ep.Split('=', 2);
                            r3p = parts[0];
                            localTarget = parts[1];
                        }
                        exporter.ExportPath(r3p, localTarget, recursive, mapping, createUsersIfMissing, applyPermissions);
                    }
                }

                logger.Short("Fertig.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Fehler: " + ex.Message);
                return 3;
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("qloudos_filemanager - kurze Hilfe\n");
            Console.WriteLine("Verwendung:");
            Console.WriteLine("  qloudos_filemanager [options] [paths]");
            Console.WriteLine("\nOptionen:");
            Console.WriteLine("  --help, -h, help           : Diese Hilfe anzeigen");
            Console.WriteLine("  --db-connection <file>     : Pfad zur SQLite-Datei (Standard: db_r3.sqlite)");
            Console.WriteLine("  --create-db                : Datenbank erstellen, wenn sie nicht existiert");
            Console.WriteLine("  --import <path>            : Importiere Datei oder Verzeichnis (mehrfach möglich)");
            Console.WriteLine("  --export <r3path>=<local>  : Exportiere R3-Pfad in lokales Verzeichnis. Ohne '=' -> ./export/{r3path}");
            Console.WriteLine("  --recursive                : Rekursive Verarbeitung von Verzeichnissen");
            Console.WriteLine("  --take-owners              : Übernehme (vereinfachte) Besitzerinformationen beim Import");
            Console.WriteLine("  --map-users-file <file>    : JSON-Datei zur Zuordnung R3-User -> Windows-User (optional)");
            Console.WriteLine("  --verbosity <none|short|verbose> : Ausgabelautstärke (keine, kurz, ausführlich)");
            Console.WriteLine("\nBeispiele:");
            Console.WriteLine("  qloudos_filemanager --create-db --db-connection mydb.sqlite --import C:\\Temp\\data --recursive");
            Console.WriteLine("  qloudos_filemanager --export /myfolder=./out --verbosity verbose");
            Console.WriteLine("\nHinweis: Parameter sind englisch, Ausgaben auf Deutsch.");
        }
    }
}
