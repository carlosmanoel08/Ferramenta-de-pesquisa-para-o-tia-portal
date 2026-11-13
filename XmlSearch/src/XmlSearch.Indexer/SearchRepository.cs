using Microsoft.Data.Sqlite;
using XmlSearch.Core;

namespace XmlSearch.Indexer;

public sealed class SearchRepository
{
    private readonly string _connectionString;
    public SearchRepository(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        Initialize();
    }

    private void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS files (path TEXT PRIMARY KEY, relative_path TEXT NOT NULL, project TEXT NOT NULL, cpu TEXT NOT NULL, length INTEGER NOT NULL, last_write_ticks INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS entries (id INTEGER PRIMARY KEY, file_path TEXT NOT NULL, relative_path TEXT NOT NULL, project TEXT NOT NULL, cpu TEXT NOT NULL, kind TEXT NOT NULL, name TEXT NOT NULL, data_type TEXT, section TEXT, comment TEXT, network TEXT, content TEXT NOT NULL DEFAULT '');
            CREATE INDEX IF NOT EXISTS ix_entries_filter ON entries(project, cpu, kind);
            CREATE INDEX IF NOT EXISTS ix_entries_file ON entries(file_path);
            CREATE VIRTUAL TABLE IF NOT EXISTS entries_fts USING fts5(name, data_type, comment, content, content='entries', content_rowid='id', tokenize='trigram case_sensitive 0');
            CREATE TRIGGER IF NOT EXISTS entries_ai AFTER INSERT ON entries BEGIN INSERT INTO entries_fts(rowid,name,data_type,comment,content) VALUES (new.id,new.name,new.data_type,new.comment,new.content); END;
            CREATE TRIGGER IF NOT EXISTS entries_ad AFTER DELETE ON entries BEGIN INSERT INTO entries_fts(entries_fts,rowid,name,data_type,comment,content) VALUES ('delete',old.id,old.name,old.data_type,old.comment,old.content); END;
            CREATE TRIGGER IF NOT EXISTS entries_au AFTER UPDATE ON entries BEGIN INSERT INTO entries_fts(entries_fts,rowid,name,data_type,comment,content) VALUES ('delete',old.id,old.name,old.data_type,old.comment,old.content); INSERT INTO entries_fts(rowid,name,data_type,comment,content) VALUES (new.id,new.name,new.data_type,new.comment,new.content); END;
            """;
        command.ExecuteNonQuery();
    }

    public bool IsCurrent(XmlFileInfo file)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT length = $length AND last_write_ticks = $ticks FROM files WHERE path = $path";
        command.Parameters.AddWithValue("$path", file.FullPath); command.Parameters.AddWithValue("$length", file.Length); command.Parameters.AddWithValue("$ticks", file.LastWriteTicks);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    public void ReplaceFile(XmlFileInfo file, IReadOnlyList<SearchEntry> entries)
    {
        using var connection = Open(); using var transaction = connection.BeginTransaction();
        Execute(connection, transaction, "DELETE FROM entries WHERE file_path = $path", ("$path", file.FullPath));
        Execute(connection, transaction, "DELETE FROM files WHERE path = $path", ("$path", file.FullPath));
        Execute(connection, transaction, "INSERT INTO files(path, relative_path, project, cpu, length, last_write_ticks) VALUES($path,$relative,$project,$cpu,$length,$ticks)",
            ("$path", file.FullPath), ("$relative", file.RelativePath), ("$project", file.Project), ("$cpu", file.Cpu), ("$length", file.Length), ("$ticks", file.LastWriteTicks));
        foreach (var entry in entries)
            Execute(connection, transaction, "INSERT INTO entries(file_path,relative_path,project,cpu,kind,name,data_type,section,comment,network,content) VALUES($file,$relative,$project,$cpu,$kind,$name,$type,$section,$comment,$network,$content)",
                ("$file", entry.FilePath), ("$relative", entry.RelativePath), ("$project", entry.Project), ("$cpu", entry.Cpu), ("$kind", entry.Kind), ("$name", entry.Name), ("$type", entry.DataType), ("$section", entry.Section), ("$comment", entry.Comment), ("$network", entry.Network), ("$content", entry.Content));
        transaction.Commit();
    }

    public void DeleteMissing(ISet<string> existingPaths)
    {
        using var connection = Open(); using var list = connection.CreateCommand(); list.CommandText = "SELECT path FROM files";
        var stored = new List<string>(); using (var reader = list.ExecuteReader()) while (reader.Read()) stored.Add(reader.GetString(0));
        using var transaction = connection.BeginTransaction();
        foreach (var path in stored.Where(path => !existingPaths.Contains(path)))
        {
            Execute(connection, transaction, "DELETE FROM entries WHERE file_path=$path", ("$path", path));
            Execute(connection, transaction, "DELETE FROM files WHERE path=$path", ("$path", path));
        }
        transaction.Commit();
    }

    public IReadOnlyList<SearchEntry> Search(SearchFilters filters)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(filters.Query))
        {
            conditions.Add("(name LIKE $query COLLATE NOCASE OR COALESCE(data_type,'') LIKE $query COLLATE NOCASE OR COALESCE(comment,'') LIKE $query COLLATE NOCASE OR content LIKE $query COLLATE NOCASE)");
            command.Parameters.AddWithValue("$query", $"%{filters.Query.Trim()}%");
            // FTS5 trigram indexes substring terms of three or more characters; LIKE remains the compatible fallback for short terms.
            var term = filters.Query.Trim();
            if (term.Length >= 3) { conditions.Add("id IN (SELECT rowid FROM entries_fts WHERE entries_fts MATCH $fts)"); command.Parameters.AddWithValue("$fts", "\"" + term.Replace("\"", "\"\"") + "\""); }
        }
        AddIn("project", filters.Projects); AddIn("cpu", filters.Cpus); AddIn("kind", filters.Kinds);
        command.CommandText = "SELECT id,file_path,relative_path,project,cpu,kind,name,data_type,section,comment,network,content FROM entries " + (conditions.Count == 0 ? "" : "WHERE " + string.Join(" AND ", conditions)) + " ORDER BY project,cpu,kind,name LIMIT $limit";
        command.Parameters.AddWithValue("$limit", filters.Limit);
        using var reader = command.ExecuteReader(); var results = new List<SearchEntry>();
        while (reader.Read()) results.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11)));
        return results;

        void AddIn(string column, IReadOnlyCollection<string> values)
        {
            if (values.Count == 0) return;
            var names = values.Select((_, i) => "$" + column + i).ToArray(); conditions.Add($"{column} IN ({string.Join(',', names)})");
            foreach (var (value, i) in values.Select((v, i) => (v, i))) command.Parameters.AddWithValue(names[i], value);
        }
    }

    public IReadOnlyList<string> Values(string column)
    {
        if (column is not ("project" or "cpu" or "kind")) throw new ArgumentOutOfRangeException(nameof(column));
        using var connection = Open(); using var command = connection.CreateCommand(); command.CommandText = $"SELECT DISTINCT {column} FROM entries ORDER BY {column}";
        using var reader = command.ExecuteReader(); var values = new List<string>(); while (reader.Read()) values.Add(reader.GetString(0)); return values;
    }
    private SqliteConnection Open() { var connection = new SqliteConnection(_connectionString); connection.Open(); return connection; }
    private static void Execute(SqliteConnection connection, SqliteTransaction tx, string sql, params (string Name, object? Value)[] values) { using var cmd = connection.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql; foreach (var (name, value) in values) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value); cmd.ExecuteNonQuery(); }
}
