using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using QloudosFileManager.Models;

namespace QloudosFileManager.Services
{
    /// <summary>
    /// Verwaltet die Datenbank-Verbindung für R3.
    /// Unterstützt SQLite (Datei) und SQL Server (ConnectionString).
    /// Tabellen (englisch): users, folders, files, permissions
    /// </summary>
    public class DatabaseService : IDisposable
    {
        private readonly string _connectionString;
        private readonly bool _createIfMissing;
        private SqliteConnection? _sqliteConnection;
        private SqlConnection? _sqlConnection;
        private readonly bool _isSqlServer;

        public DatabaseService(string connectionString, bool createIfMissing)
        {
            _connectionString = connectionString;
            _createIfMissing = createIfMissing;
            _isSqlServer = connectionString.Contains('='); // einfache Heuristik: enthält '=' -> ConnectionString
        }

        /// <summary>
        /// Öffnet die Verbindung und erstellt ggf. die Datenbank und Tabellen.
        /// </summary>
        public void Open()
        {
            if (_isSqlServer)
            {
                var builder = new SqlConnectionStringBuilder(_connectionString);
                var dbName = builder.InitialCatalog;
                if (string.IsNullOrEmpty(dbName)) dbName = builder["Database"] as string ?? "db_r3";

                if (_createIfMissing)
                {
                    // Prüfe, ob DB existiert, sonst erstelle über master
                    var masterBuilder = new SqlConnectionStringBuilder(_connectionString);
                    masterBuilder.InitialCatalog = "master";
                    using var masterConn = new SqlConnection(masterBuilder.ConnectionString);
                    masterConn.Open();
                    using var checkCmd = masterConn.CreateCommand();
                    checkCmd.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @name";
                    checkCmd.Parameters.AddWithValue("@name", dbName);
                    var exists = (int)checkCmd.ExecuteScalar() > 0;
                    if (!exists)
                    {
                        using var createCmd = masterConn.CreateCommand();
                        createCmd.CommandText = $"CREATE DATABASE [{dbName}]";
                        createCmd.ExecuteNonQuery();
                    }
                }

                _sqlConnection = new SqlConnection(_connectionString);
                _sqlConnection.Open();
                EnsureTablesSqlServer();
            }
            else
            {
                var builder = new SqliteConnectionStringBuilder(_connectionString);
                var datenbankPfad = builder.DataSource;
                if (!string.IsNullOrWhiteSpace(datenbankPfad) && !File.Exists(datenbankPfad))
                {
                    if (_createIfMissing)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(datenbankPfad) ?? string.Empty);
                        // Erstelle eine leere Datei für SQLite; Connection erstellt Tabellen später automatisch
                        using (File.Create(datenbankPfad)) { }
                    }
                    else
                    {
                        throw new FileNotFoundException("Datenbank nicht gefunden: " + datenbankPfad);
                    }
                }

                _sqliteConnection = new SqliteConnection(_connectionString);
                _sqliteConnection.Open();
                EnsureTablesSqlite();
            }
        }

        private void EnsureTablesSqlite()
        {
            using var cmd = _sqliteConnection!.CreateCommand();
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

        private void EnsureTablesSqlServer()
        {
            using var cmd = _sqlConnection!.CreateCommand();
            cmd.CommandText = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'users')
BEGIN
CREATE TABLE users (
  id BIGINT IDENTITY(1,1) PRIMARY KEY,
  username NVARCHAR(200) UNIQUE NOT NULL,
  displayname NVARCHAR(500)
);
END
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'folders')
BEGIN
CREATE TABLE folders (
  id BIGINT IDENTITY(1,1) PRIMARY KEY,
  path NVARCHAR(2000) UNIQUE NOT NULL,
  owner_id BIGINT,
  created_utc DATETIME2
);
END
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'files')
BEGIN
CREATE TABLE files (
  id BIGINT IDENTITY(1,1) PRIMARY KEY,
  name NVARCHAR(500) NOT NULL,
  path NVARCHAR(2000) NOT NULL,
  content VARBINARY(MAX),
  owner_id BIGINT,
  created_utc DATETIME2,
  CONSTRAINT UQ_files_path_name UNIQUE (path, name)
);
END
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'permissions')
BEGIN
CREATE TABLE permissions (
  id BIGINT IDENTITY(1,1) PRIMARY KEY,
  user_id BIGINT,
  object_type NVARCHAR(50),
  object_id BIGINT,
  rights NVARCHAR(50)
);
END
";
            cmd.CommandTimeout = 60;
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Liefert die UserId und legt den Benutzer an, falls nicht vorhanden.
        /// </summary>
        public long GetOrCreateUser(string username, string displayName)
        {
            if (_isSqlServer)
            {
                using var sel = _sqlConnection!.CreateCommand();
                sel.CommandText = "SELECT id FROM users WHERE username = @u";
                sel.Parameters.AddWithValue("@u", username);
                var res = sel.ExecuteScalar();
                if (res != null && long.TryParse(res.ToString(), out var id)) return id;

                using var ins = _sqlConnection.CreateCommand();
                ins.CommandText = "INSERT INTO users (username, displayname) OUTPUT INSERTED.id VALUES (@u,@d);";
                ins.Parameters.AddWithValue("@u", username);
                ins.Parameters.AddWithValue("@d", displayName);
                var newId = (long)ins.ExecuteScalar();
                return newId;
            }
            else
            {
                using var sel = _sqliteConnection!.CreateCommand();
                sel.CommandText = "SELECT id FROM users WHERE username = $u";
                sel.Parameters.AddWithValue("$u", username);
                var res = sel.ExecuteScalar();
                if (res != null && long.TryParse(res.ToString(), out var id)) return id;

                using var ins = _sqliteConnection.CreateCommand();
                ins.CommandText = "INSERT INTO users (username, displayname) VALUES ($u,$d); SELECT last_insert_rowid();";
                ins.Parameters.AddWithValue("$u", username);
                ins.Parameters.AddWithValue("$d", displayName);
                var newId = (long)ins.ExecuteScalar();
                return newId;
            }
        }

        /// <summary>
        /// Liefert alle Benutzer im Zielsystem.
        /// </summary>
        public System.Collections.Generic.List<UserEntry> GetAllUsers()
        {
            var list = new System.Collections.Generic.List<UserEntry>();
            if (_isSqlServer)
            {
                using var cmd = _sqlConnection!.CreateCommand();
                cmd.CommandText = "SELECT id,username,displayname FROM users";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new UserEntry { Id = r.GetInt64(0), Username = r.GetString(1), DisplayName = r.IsDBNull(2) ? string.Empty : r.GetString(2) });
                }
            }
            else
            {
                using var cmd = _sqliteConnection!.CreateCommand();
                cmd.CommandText = "SELECT id,username,displayname FROM users";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new UserEntry { Id = r.GetInt64(0), Username = r.GetString(1), DisplayName = r.IsDBNull(2) ? string.Empty : r.GetString(2) });
                }
            }
            return list;
        }

        /// <summary>
        /// Legt einen Benutzer an. Wenn vorhanden, wird die vorhandene Id zurückgegeben.
        /// </summary>
        public long CreateUser(string username, string displayName)
        {
            return GetOrCreateUser(username, displayName);
        }

        /// <summary>
        /// Löscht einen Benutzer nach Benutzernamen.
        /// </summary>
        public bool DeleteUserByUsername(string username)
        {
            if (_isSqlServer)
            {
                using var cmd = _sqlConnection!.CreateCommand();
                cmd.CommandText = "DELETE FROM users WHERE username = @u";
                cmd.Parameters.AddWithValue("@u", username);
                var changed = cmd.ExecuteNonQuery();
                return changed > 0;
            }
            else
            {
                using var cmd = _sqliteConnection!.CreateCommand();
                cmd.CommandText = "DELETE FROM users WHERE username = $u";
                cmd.Parameters.AddWithValue("$u", username);
                var changed = cmd.ExecuteNonQuery();
                return changed > 0;
            }
        }

        /// <summary>
        /// Fügt eine Rechte-Zuordnung hinzu.
        /// </summary>
        public long AddPermission(PermissionEntry perm)
        {
            if (_isSqlServer)
            {
                using var cmd = _sqlConnection!.CreateCommand();
                cmd.CommandText = "INSERT INTO permissions (user_id,object_type,object_id,rights) VALUES (@uid,@otype,@oid,@rights); SELECT CAST(SCOPE_IDENTITY() as bigint);";
                cmd.Parameters.AddWithValue("@uid", perm.UserId);
                cmd.Parameters.AddWithValue("@otype", perm.ObjectType);
                cmd.Parameters.AddWithValue("@oid", perm.ObjectId);
                cmd.Parameters.AddWithValue("@rights", perm.Rights);
                var id = (long)cmd.ExecuteScalar();
                return id;
            }
            else
            {
                using var cmd = _sqliteConnection!.CreateCommand();
                cmd.CommandText = "INSERT INTO permissions (user_id,object_type,object_id,rights) VALUES ($uid,$otype,$oid,$rights); SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$uid", perm.UserId);
                cmd.Parameters.AddWithValue("$otype", perm.ObjectType);
                cmd.Parameters.AddWithValue("$oid", perm.ObjectId);
                cmd.Parameters.AddWithValue("$rights", perm.Rights);
                var nid = (long)cmd.ExecuteScalar();
                return nid;
            }
        }

        /// <summary>
        /// Liefert einen Benutzer nach Benutzernamen oder null.
        /// </summary>
        public UserEntry? GetUserByUsername(string username)
        {
            if (_isSqlServer)
            {
                using var cmd = _sqlConnection!.CreateCommand();
                cmd.CommandText = "SELECT id,username,displayname FROM users WHERE username = @u";
                cmd.Parameters.AddWithValue("@u", username);
                using var r = cmd.ExecuteReader();
                if (r.Read()) return new UserEntry { Id = r.GetInt64(0), Username = r.GetString(1), DisplayName = r.IsDBNull(2) ? string.Empty : r.GetString(2) };
                return null;
            }
            else
            {
                using var cmd = _sqliteConnection!.CreateCommand();
                cmd.CommandText = "SELECT id,username,displayname FROM users WHERE username = $u";
                cmd.Parameters.AddWithValue("$u", username);
                using var r = cmd.ExecuteReader();
                if (r.Read()) return new UserEntry { Id = r.GetInt64(0), Username = r.GetString(1), DisplayName = r.IsDBNull(2) ? string.Empty : r.GetString(2) };
                return null;
            }
        }

        /// <summary>
        /// Liefert einen Benutzer nach Id oder null.
        /// </summary>
        public UserEntry? GetUserById(long id)
        {
            if (_isSqlServer)
            {
                using var cmd = _sqlConnection!.CreateCommand();
                cmd.CommandText = "SELECT id,username,displayname FROM users WHERE id = @id";
                cmd.Parameters.AddWithValue("@id", id);
                using var r = cmd.ExecuteReader();
                if (r.Read()) return new UserEntry { Id = r.GetInt64(0), Username = r.GetString(1), DisplayName = r.IsDBNull(2) ? string.Empty : r.GetString(2) };
                return null;
            }
            else
            {
                using var cmd = _sqliteConnection!.CreateCommand();
                cmd.CommandText = "SELECT id,username,displayname FROM users WHERE id = $id";
                cmd.Parameters.AddWithValue("$id", id);
                using var r = cmd.ExecuteReader();
                if (r.Read()) return new UserEntry { Id = r.GetInt64(0), Username = r.GetString(1), DisplayName = r.IsDBNull(2) ? string.Empty : r.GetString(2) };
                return null;
            }
        }

        /// <summary>
        /// Legt einen Ordnereintrag an, wenn nicht vorhanden. Liefert die Folder-Id.
        /// </summary>
        public long CreateFolderIfNotExists(string path, long ownerId)
        {
            if (_isSqlServer)
            {
                using var sel = _sqlConnection!.CreateCommand();
                sel.CommandText = "SELECT id FROM folders WHERE path = @p";
                sel.Parameters.AddWithValue("@p", path);
                var res = sel.ExecuteScalar();
                if (res != null && long.TryParse(res.ToString(), out var id)) return id;

                using var ins = _sqlConnection.CreateCommand();
                ins.CommandText = "INSERT INTO folders (path,owner_id,created_utc) OUTPUT INSERTED.id VALUES (@p,@o,@c);";
                ins.Parameters.AddWithValue("@p", path);
                ins.Parameters.AddWithValue("@o", ownerId);
                ins.Parameters.AddWithValue("@c", DateTime.UtcNow);
                return (long)ins.ExecuteScalar();
            }
            else
            {
                using var sel = _sqliteConnection!.CreateCommand();
                sel.CommandText = "SELECT id FROM folders WHERE path = $p";
                sel.Parameters.AddWithValue("$p", path);
                var res = sel.ExecuteScalar();
                if (res != null && long.TryParse(res.ToString(), out var id)) return id;
            }
            using var cmd = _sqliteConnection!.CreateCommand();
            cmd.CommandText = "INSERT INTO folders (path,owner_id,created_utc) VALUES ($p,$o,$c); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$p", path);
            cmd.Parameters.AddWithValue("$o", ownerId);
            cmd.Parameters.AddWithValue("$c", DateTime.UtcNow.ToString("o"));
            return (long)cmd.ExecuteScalar();
        }

        /// <summary>
        /// Speichert eine Datei (neue Version überschreibt bisherige Daten).
        /// </summary>
        public long SaveFile(FileEntry datei)
        {
            if (_isSqlServer)
            {
                using var cmd = _sqlConnection!.CreateCommand();
                cmd.CommandText = @"INSERT INTO files (name,path,content,owner_id,created_utc)
VALUES (@name,@path,@content,@owner,@created);
SELECT CAST(SCOPE_IDENTITY() as bigint);";
                cmd.Parameters.AddWithValue("@name", datei.Name);
                cmd.Parameters.AddWithValue("@path", datei.Path);
                cmd.Parameters.AddWithValue("@content", datei.Content ?? Array.Empty<byte>());
                cmd.Parameters.AddWithValue("@owner", datei.OwnerId);
                cmd.Parameters.AddWithValue("@created", datei.CreatedUtc);
                var id = (long)cmd.ExecuteScalar();
                return id;
            }
            else
            {
                using var cmd = _sqliteConnection!.CreateCommand();
                cmd.CommandText = @"INSERT INTO files (name,path,content,owner_id,created_utc)
VALUES ($name,$path,$content,$owner,$created);
SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$name", datei.Name);
                cmd.Parameters.AddWithValue("$path", datei.Path);
                cmd.Parameters.AddWithValue("$content", datei.Content ?? Array.Empty<byte>());
                cmd.Parameters.AddWithValue("$owner", datei.OwnerId);
                cmd.Parameters.AddWithValue("$created", datei.CreatedUtc.ToString("o"));
                var id = (long)cmd.ExecuteScalar();
                return id;
            }
        }

        /// <summary>
        /// Liest Dateien zurück, die zu einem Pfad gehören. Wenn recursive true, werden Unterpfade mit ausgewertet.
        /// </summary>
        public System.Collections.Generic.List<FileEntry> GetFiles(string folderPath, bool recursive)
        {
            var list = new System.Collections.Generic.List<FileEntry>();
            if (_isSqlServer)
            {
                using var cmd = _sqlConnection!.CreateCommand();
                if (recursive)
                {
                    cmd.CommandText = "SELECT id,name,path,content,owner_id,created_utc FROM files WHERE path LIKE @p";
                    cmd.Parameters.AddWithValue("@p", folderPath.TrimEnd('/') + "%");
                }
                else
                {
                    cmd.CommandText = "SELECT id,name,path,content,owner_id,created_utc FROM files WHERE path = @p";
                    cmd.Parameters.AddWithValue("@p", folderPath);
                }
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var fe = new FileEntry
                    {
                        Id = reader.GetInt64(0),
                        Name = reader.GetString(1),
                        Path = reader.GetString(2),
                        Content = reader.IsDBNull(3) ? Array.Empty<byte>() : (byte[])reader[3],
                        OwnerId = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                        CreatedUtc = reader.IsDBNull(5) ? DateTime.MinValue : reader.GetDateTime(5)
                    };
                    list.Add(fe);
                }
            }
            else
            {
                using var cmd = _sqliteConnection!.CreateCommand();
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
                        Content = reader.IsDBNull(3) ? Array.Empty<byte>() : (byte[])reader[3],
                        OwnerId = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                        CreatedUtc = reader.IsDBNull(5) ? DateTime.MinValue : DateTime.Parse(reader.GetString(5))
                    };
                    list.Add(fe);
                }
            }
            return list;
        }

        public void Dispose()
        {
            _sqliteConnection?.Dispose();
            _sqlConnection?.Dispose();
        }
    }
}