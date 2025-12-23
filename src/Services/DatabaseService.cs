using System;
using System.IO;
using Microsoft.Data.Sqlite;
using QloudosFileManager.Models;

namespace QloudosFileManager.Services
{
    /// <summary>
    /// Verwaltet die SQLite-Datenbank als Beispiel-Implementierung des R3-Backends.
    /// Tabellen: users, folders, files, permissions
    /// </summary>
    public class DatabaseService : IDisposable
    {
        private readonly string _connectionString;
        private readonly bool _createIfMissing;
        private SqliteConnection? _connection;

        public DatabaseService(string connectionString, bool createIfMissing)
        {
            _connectionString = connectionString;
            _createIfMissing = createIfMissing;
        }

        /// <summary>
        /// Öffnet die Verbindung und erstellt ggf. die Datenbank und Tabellen.
        /// </summary>
        public void Open()
        {
            var builder = new SqliteConnectionStringBuilder(_connectionString);
            // Falls Pfad enthalten ist und Datei nicht existiert, evtl. erstellen
            if (!string.IsNullOrWhiteSpace(builder.DataSource))
            {
                var datenbankPfad = builder.DataSource;
                if (!File.Exists(datenbankPfad))
                {
                    if (_createIfMissing)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(datenbankPfad) ?? "");
                        SqliteConnection.CreateFile(datenbankPfad);
                    }
                    else
                    {
                        throw new FileNotFoundException("Datenbank nicht gefunden: " + datenbankPfad);
                    }
                }
            }

            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
            EnsureTables();
        }

        private void EnsureTables()
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS users (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  username TEXT UNIQUE NOT NULL,
  displayname TEXT
);
CREATE TABLE IF NOT EXISTS folders (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  path TEXT UNIQUE NOT NULL,
  owner_id INTEGER,
  created_utc TEXT
);
CREATE TABLE IF NOT EXISTS files (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  path TEXT NOT NULL,
  content BLOB,
  owner_id INTEGER,
  created_utc TEXT,
  UNIQUE(path, name)
);
CREATE TABLE IF NOT EXISTS permissions (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id INTEGER,
  object_type TEXT,
  object_id INTEGER,
  rights TEXT
);
";
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Liefert die UserId und legt den Benutzer an, falls nicht vorhanden.
        /// </summary>
        public long GetOrCreateUser(string username, string displayName)
        {
            using var sel = _connection!.CreateCommand();
            sel.CommandText = "SELECT id FROM users WHERE username = $u";
            sel.Parameters.AddWithValue("$u", username);
            var res = sel.ExecuteScalar();
            if (res != null && long.TryParse(res.ToString(), out var id)) return id;

            using var ins = _connection.CreateCommand();
            ins.CommandText = "INSERT INTO users (username, displayname) VALUES ($u,$d); SELECT last_insert_rowid();";
            ins.Parameters.AddWithValue("$u", username);
            ins.Parameters.AddWithValue("$d", displayName);
            var newId = (long)ins.ExecuteScalar();
            return newId;
        }

        /// <summary>
        /// Speichert eine Datei (neue Version überschreibt bisherige Daten).
        /// </summary>
        public long SaveFile(FileEntry datei)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"INSERT INTO files (name,path,content,owner_id,created_utc)
VALUES ($name,$path,$content,$owner,$created);
SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$name", datei.Name);
            cmd.Parameters.AddWithValue("$path", datei.Path);
            cmd.Parameters.AddWithValue("$content", datei.Content);
            cmd.Parameters.AddWithValue("$owner", datei.OwnerId);
            cmd.Parameters.AddWithValue("$created", datei.CreatedUtc.ToString("o"));
            var id = (long)cmd.ExecuteScalar();
            return id;
        }

        /// <summary>
        /// Liest Dateien zurück, die zu einem Pfad gehören. Wenn recursive true, werden Unterpfade mit ausgewertet.
        /// </summary>
        public System.Collections.Generic.List<FileEntry> GetFiles(string folderPath, bool recursive)
        {
            var list = new System.Collections.Generic.List<FileEntry>();
            using var cmd = _connection!.CreateCommand();
            if (recursive)
            {
                cmd.CommandText = "SELECT id,name,path,content,owner_id,created_utc FROM files WHERE path LIKE $p";
                cmd.Parameters.AddWithValue("$p", folderPath.TrimEnd('/') + "%");
            }
            else
            {
                cmd.CommandText = "SELECT id,name,path,content,owner_id,created_utc FROM files WHERE path = $p";
                cmd.Parameters.AddWithValue("$p", folderPath);
            }
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var fe = new FileEntry
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Path = reader.GetString(2),
                    Content = (byte[])reader[3],
                    OwnerId = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                    CreatedUtc = DateTime.Parse(reader.GetString(5))
                };
                list.Add(fe);
            }
            return list;
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}