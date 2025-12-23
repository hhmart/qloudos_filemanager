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
            var importPaths = new List<string>();
            var exportPaths = new List<string>();
            bool recursive = false;
            bool takeOwners = false;
            string? mapUsersFile = null;
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

            // Verbindung bauen: simpel als Data Source=file
            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbFile }.ToString();

            try
            {
                using var db = new DatabaseService(connectionString, createDbIfMissing);
                db.Open();

                var importer = new ImportService(db, logger);
                var exporter = new ExportService(db, logger);

                if (importPaths.Count == 0 && exportPaths.Count == 0)
                {
                    logger.Short("Keine Aktion angegeben. Verwenden Sie --help für Hilfe.");
                }

                if (importPaths.Count > 0)
                {
                    importer.ImportPaths(importPaths, recursive, takeOwners);
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
                        exporter.ExportPath(r3p, localTarget, recursive, null);
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
