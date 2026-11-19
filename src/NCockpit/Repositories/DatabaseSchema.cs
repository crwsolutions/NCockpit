using Microsoft.Data.Sqlite;

namespace NCockpit.Repositories;

/// <summary>
/// The SQLite schema of the app and the migrations between its versions. Kept
/// free of any MAUI dependency so it can be applied to any connection (the
/// app resolves the path and opens the connection itself).
/// The app is in development, so a broken database may simply be deleted: the
/// file is (re)created from the schema below.
/// </summary>
internal static class DatabaseSchema
{
    /// <summary>
    /// Highest migration step this build knows about. Bump this and add a new
    /// <c>if (userVersion &lt; N)</c> step for a schema change; never rewrite an
    /// existing step.
    /// </summary>
    private const int CurrentUserVersion = 10;

    private const string ProjectFoldersDdl = """
        CREATE TABLE IF NOT EXISTS ProjectFolders (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Path TEXT NOT NULL UNIQUE,
            IsPrimary INTEGER NOT NULL DEFAULT 1,
            ProjectCount INTEGER NOT NULL DEFAULT 0,
            LastScanUnix INTEGER,
            CreatedAt INTEGER NOT NULL
        );
        """;

    private const string SupportingFoldersDdl = """
        CREATE TABLE IF NOT EXISTS SupportingFolders (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Path TEXT NOT NULL UNIQUE COLLATE NOCASE,
            FolderId INTEGER NOT NULL REFERENCES ProjectFolders(Id) ON DELETE CASCADE,
            ProjectId INTEGER REFERENCES Projects(Id) ON DELETE SET NULL,
            CreatedAt INTEGER NOT NULL
        );
        """;

    private const string AppSettingsDdl = """
        CREATE TABLE IF NOT EXISTS AppSettings (
            Key TEXT PRIMARY KEY,
            Value TEXT
        );
        """;

    private const string RelatedProjectsDdl = """
        CREATE TABLE IF NOT EXISTS RelatedProjects (
            ProjectId INTEGER NOT NULL REFERENCES Projects(Id) ON DELETE CASCADE,
            RelatedProjectId INTEGER NOT NULL REFERENCES Projects(Id) ON DELETE CASCADE,
            CreatedAt INTEGER NOT NULL,
            PRIMARY KEY (ProjectId, RelatedProjectId)
        );
        """;

    /// <summary>
    /// The one unique key of <c>Projects</c>: the folder path, which is what
    /// <see cref="ProjectRepository.UpsertAsync"/> conflicts on. Index DDL is
    /// kept apart from the table DDL because a rebuild drops the old table
    /// (and with it its indexes) before creating the new ones.
    /// </summary>
    private const string ProjectsIndexDdl = """
        CREATE UNIQUE INDEX IF NOT EXISTS IX_Projects_FolderPath ON Projects (FolderPath);
        """;

    /// <summary>
    /// The current shape of the projects table. A project is a folder, so
    /// <c>FolderPath</c> is its identity (one unique index);
    /// <c>SolutionPath</c> is only the currently selected solution of that
    /// folder and is deliberately not unique: two rows may name the same
    /// solution file until a scan merges them, and the user's "open this
    /// solution" must never be blocked by another row.
    /// </summary>
    private const string ProjectsDdl = """
        CREATE TABLE IF NOT EXISTS Projects (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Type INTEGER NOT NULL DEFAULT 0,
            SolutionPath TEXT COLLATE NOCASE,
            FolderPath TEXT NOT NULL,
            FolderId INTEGER NOT NULL REFERENCES ProjectFolders(Id) ON DELETE CASCADE,
            LastSeenUnix INTEGER NOT NULL,
            ScanUnix INTEGER NOT NULL,
            CreatedAt INTEGER NOT NULL,
            IsFavorite INTEGER NOT NULL DEFAULT 0,
            LastUsedUnix INTEGER,
            SolutionLastWriteUnix INTEGER,
            FilesJson TEXT,
            GitRepoUrl TEXT,
            ProjectsJson TEXT,
            SolutionsJson TEXT,
            SolutionLastUsedUnix INTEGER,
            SolutionProjectsJson TEXT,
            OrphanProjectsJson TEXT,
            FavoriteSolutionPath TEXT,
            FavoriteProjectPath TEXT
        );
        """;

    /// <summary>
    /// Create the database when it is new and bring an existing one up to
    /// <see cref="CurrentUserVersion"/>.
    /// </summary>
    internal static void Apply(SqliteConnection connection)
    {
        Execute(connection, "PRAGMA foreign_keys = ON");

        // A file holding no tables is new: the CREATE statements below put it
        // straight into the current shape, so the migration steps must not run
        // (they would rebuild tables that are already right, reintroducing an
        // older shape only to repair it again).
        var isNewDatabase = CountTables(connection) == 0;

        Execute(connection, ProjectFoldersDdl);
        Execute(connection, SupportingFoldersDdl);
        Execute(connection, AppSettingsDdl);
        Execute(connection, ProjectsDdl);
        Execute(connection, ProjectsIndexDdl);
        Execute(connection, RelatedProjectsDdl);

        if (isNewDatabase)
        {
            Execute(connection, $"PRAGMA user_version = {CurrentUserVersion};");
            return;
        }

        Migrate(connection);
    }

    private static int CountTables(SqliteConnection connection)
    {
        using var command = new SqliteCommand(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name <> 'sqlite_sequence';",
            connection)
        {
            CommandTimeout = 10
        };

        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void Migrate(SqliteConnection connection)
    {
        // CurrentUserVersion should track the highest migration step applied.
        using var versionCommand = new SqliteCommand("PRAGMA user_version;", connection);
        var userVersion = Convert.ToInt32(versionCommand.ExecuteScalar());

        if (userVersion < 1)
        {
            // Initial schema is created by the CREATE TABLE statements.
            // No migration needed.
        }

        if (userVersion < 2)
        {
            // Add FilesJson column for discovered root files, if it doesn't
            // already exist (fresh databases have it in the CREATE TABLE).
            try
            {
                Execute(connection, "ALTER TABLE Projects ADD COLUMN FilesJson TEXT");
            }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column name"))
            {
                // Column already exists — nothing to do.
            }
        }

        if (userVersion < 3)
        {
            // Add GitRepoUrl column for the git remote URL discovered during scanning.
            try
            {
                Execute(connection, "ALTER TABLE Projects ADD COLUMN GitRepoUrl TEXT");
            }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column name"))
            {
                // Column already exists — nothing to do.
            }
        }

        if (userVersion < 4)
        {
            // Add ProjectsJson column for the solution's projects discovered
            // during scanning, if it doesn't already exist (fresh databases
            // have it in the CREATE TABLE).
            try
            {
                Execute(connection, "ALTER TABLE Projects ADD COLUMN ProjectsJson TEXT");
            }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column name"))
            {
                // Column already exists — nothing to do.
            }
        }

        if (userVersion < 5)
        {
            // Repo-first project detection: a project is now a folder.
            //
            // 1) New columns.
            try { Execute(connection, "ALTER TABLE Projects ADD COLUMN Type INTEGER NOT NULL DEFAULT 0"); }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column name")) { }
            try { Execute(connection, "ALTER TABLE Projects ADD COLUMN SolutionsJson TEXT"); }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column name")) { }
            try { Execute(connection, "ALTER TABLE Projects ADD COLUMN SolutionLastUsedUnix INTEGER"); }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column name")) { }

            // 2) SolutionPath becomes nullable (folder type projects have no
            //    solution file). It should stop being the unique key, but this
            //    step kept declaring it UNIQUE in the recreated table:
            //    migration 7 removes that. The inline UNIQUE index cannot be
            //    dropped directly (SQLite refuses to drop an autoindex), which
            //    is why the table is rebuilt below.
            Execute(connection, """
                ALTER TABLE Projects RENAME TO ProjectsOld;
                """);

            Execute(connection, """
                CREATE TABLE Projects (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Type INTEGER NOT NULL DEFAULT 0,
                    SolutionPath TEXT UNIQUE COLLATE NOCASE,
                    FolderPath TEXT NOT NULL UNIQUE,
                    FolderId INTEGER NOT NULL REFERENCES ProjectFolders(Id) ON DELETE CASCADE,
                    LastSeenUnix INTEGER NOT NULL,
                    ScanUnix INTEGER NOT NULL,
                    CreatedAt INTEGER NOT NULL,
                    IsFavorite INTEGER NOT NULL DEFAULT 0,
                    LastUsedUnix INTEGER,
                    SolutionLastWriteUnix INTEGER,
                    FilesJson TEXT,
                    GitRepoUrl TEXT,
                    ProjectsJson TEXT,
                    SolutionsJson TEXT,
                    SolutionLastUsedUnix INTEGER
                );
                """);

            Execute(connection, """
                INSERT INTO Projects (
                    Id, Name, Type, SolutionPath, FolderPath, FolderId,
                    LastSeenUnix, ScanUnix, CreatedAt, IsFavorite, LastUsedUnix,
                    SolutionLastWriteUnix, FilesJson, GitRepoUrl, ProjectsJson,
                    SolutionsJson, SolutionLastUsedUnix
                )
                SELECT
                    Id, Name, Type, SolutionPath, FolderPath, FolderId,
                    LastSeenUnix, ScanUnix, CreatedAt, IsFavorite, LastUsedUnix,
                    SolutionLastWriteUnix, FilesJson, GitRepoUrl, ProjectsJson,
                    SolutionsJson, SolutionLastUsedUnix
                FROM ProjectsOld;
                """);

            Execute(connection, "DROP TABLE ProjectsOld;");

            // 3) Re-key: existing rows are legacy (solution type) projects
            //    keyed by solution path; the new identity is the folder path.
            //    Deduplicate rows that share both the folder and the solution
            //    (e.g. an older scan that ran the root rule differently),
            //    keeping the most recently used row. Rows with the same folder
            //    but different solutions stay — the next scan merges them into
            //    one repo project, preserving their state via MergeIntoAsync.
            //    After this step every (FolderPath, SolutionPath) pair is
            //    unique, so the new unique indexes can be created.
            Execute(connection, """
                DELETE FROM Projects
                WHERE Id NOT IN (
                    SELECT Id FROM (
                        SELECT Id,
                               ROW_NUMBER() OVER (
                                   PARTITION BY FolderPath, SolutionPath
                                   ORDER BY COALESCE(LastUsedUnix, 0) DESC, Id
                               ) AS rn
                        FROM Projects
                    )
                    WHERE rn = 1
                );
                """);

            Execute(connection, """
                CREATE UNIQUE INDEX IF NOT EXISTS IX_Projects_FolderPath
                ON Projects (FolderPath);
                """);
        }

        if (userVersion < 6)
        {
            // Solutions & Projects panel: the scan now parses every solution in
            // a repo (not just the primary one) and discovers the orphan
            // project files, and the user can pick a favorite solution and a
            // favorite project.
            //
            // New columns: SolutionProjectsJson (every solution with the
            // projects it references), OrphanProjectsJson (project files not
            // referenced by any solution), FavoriteSolutionPath and
            // FavoriteProjectPath (the user's choices; null until one is made).
            try { Execute(connection, "ALTER TABLE Projects ADD COLUMN SolutionProjectsJson TEXT"); }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column name")) { }
            try { Execute(connection, "ALTER TABLE Projects ADD COLUMN OrphanProjectsJson TEXT"); }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column name")) { }
            try { Execute(connection, "ALTER TABLE Projects ADD COLUMN FavoriteSolutionPath TEXT"); }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column name")) { }
            try { Execute(connection, "ALTER TABLE Projects ADD COLUMN FavoriteProjectPath TEXT"); }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column name")) { }

            // Seed rows that already had a primary solution (the old
            // ProjectsJson held exactly that solution's projects): copy it into
            // the new SolutionProjectsJson so the panel is not empty until the
            // next scan re-discovers everything. Rows without a primary
            // solution (folder type) get a null SolutionProjectsJson.
            Execute(connection, """
                UPDATE Projects
                SET SolutionProjectsJson = (
                        SELECT json_array(
                            json_object(
                                'Path', SolutionPath,
                                'Projects', COALESCE(ProjectsJson, '[]')
                            )
                        )
                        WHERE SolutionPath IS NOT NULL
                    )
                WHERE SolutionProjectsJson IS NULL
                  AND (ProjectsJson IS NOT NULL OR SolutionPath IS NOT NULL);
                """);
        }

        if (userVersion < 7)
        {
            // Two schema faults that migration 5 left behind, both of which
            // break a scan:
            //
            // 1) Projects.SolutionPath was still UNIQUE. The project identity is
            //    the folder path, so a second row naming the same solution (a
            //    stale row not yet merged, or the user opening that solution
            //    through TouchSolutionAsync) aborted the statement with
            //    "UNIQUE constraint failed: Projects.SolutionPath" and took the
            //    whole scan down with it.
            // 2) Migration 5 renamed Projects while foreign keys were enabled,
            //    and SQLite rewrote SupportingFolders' REFERENCES clause to the
            //    temporary name; that reference now points at a dropped table,
            //    so the project link of a supporting folder cannot be used.
            RebuildTable(connection, "Projects", ProjectsDdl, ProjectsIndexDdl, ProjectColumns);
            RepairBrokenReferences(connection, "SupportingFolders", SupportingFoldersDdl, SupportingFolderColumns);
        }

        if (userVersion < 8)
        {
            // ProjectFolders.IsEnabled was never wired to anything: the scan decides
            // where to look for projects through IsPrimary alone (secondary folders
            // are never scanned), and no screen ever toggled the flag. Dropping it
            // needs no table rebuild because no index, key or view uses the column.
            try { Execute(connection, "ALTER TABLE ProjectFolders DROP COLUMN IsEnabled"); }
            catch (SqliteException ex) when (ex.Message.Contains("no such column")) { }
        }

        if (userVersion < 9)
        {
            // Key/value table for small per-device preferences, created at the top
            // for fresh databases and recreated here for existing ones.
            Execute(connection, AppSettingsDdl);
        }

        if (userVersion < 10)
        {
            // Related projects: a manually configured relationship between two
            // projects (the "Related projects" panel of the details page),
            // created at the top for fresh databases and recreated here for
            // existing ones.
            Execute(connection, RelatedProjectsDdl);
        }

        Execute(connection, $"PRAGMA user_version = {CurrentUserVersion};");
    }

    /// <summary>
    /// Column list of <c>Projects</c> as of <see cref="CurrentUserVersion"/>,
    /// used to copy a table across a rebuild.
    /// </summary>
    private static readonly string[] ProjectColumns =
    [
        "Id", "Name", "Type", "SolutionPath", "FolderPath", "FolderId",
        "LastSeenUnix", "ScanUnix", "CreatedAt", "IsFavorite", "LastUsedUnix",
        "SolutionLastWriteUnix", "FilesJson", "GitRepoUrl", "ProjectsJson",
        "SolutionsJson", "SolutionLastUsedUnix", "SolutionProjectsJson",
        "OrphanProjectsJson", "FavoriteSolutionPath", "FavoriteProjectPath"
    ];

    private static readonly string[] SupportingFolderColumns =
    [
        "Id", "Path", "FolderId", "ProjectId", "CreatedAt"
    ];

    /// <summary>
    /// Rebuilds <paramref name="table"/> into the shape
    /// <paramref name="ddl"/> creates, keeping every row of
    /// <paramref name="columns"/>. No-op when the table already matches
    /// (fresh databases are created in the current shape).
    ///
    /// The rename must not rewrite the REFERENCES clauses of other tables:
    /// with the default <c>legacy_alter_table</c> off, it would point them at
    /// the temporary name and break them when that is dropped (that is how
    /// migration 5 damaged SupportingFolders). Turning it on keeps those
    /// clauses naming the final table, which the rebuild recreates.
    /// </summary>
    private static void RebuildTable(
        SqliteConnection connection,
        string table,
        string ddl,
        string? indexDdl,
        IReadOnlyList<string> columns,
        bool onlyWhenShapeDiffers = true)
    {
        var tableSql = ReadTableSql(connection, table);
        if (tableSql is null)
        {
            return;
        }

        if (onlyWhenShapeDiffers && !NeedsRebuild(tableSql))
        {
            return;
        }

        var columnList = string.Join(", ", columns);
        var staging = $"{table}_m{CurrentUserVersion}";

        // Both pragmas must be set outside a transaction to take effect.
        Execute(connection, "PRAGMA legacy_alter_table = ON");
        Execute(connection, "PRAGMA foreign_keys = OFF");
        try
        {
            Execute(connection, "BEGIN IMMEDIATE");
            try
            {
                Execute(connection, $"ALTER TABLE {table} RENAME TO {staging};");
                Execute(connection, ddl);
                Execute(connection, $"""
                    INSERT INTO {table} ({columnList})
                    SELECT {columnList} FROM {staging};
                    """);
                // The indexes of the old table go with it; the names they hold
                // (IX_Projects_FolderPath) must be free for the new table, so
                // the new indexes are created only after the drop.
                Execute(connection, $"DROP TABLE {staging};");
                if (indexDdl is not null)
                {
                    Execute(connection, indexDdl);
                }

                Execute(connection, "COMMIT;");
            }
            catch
            {
                Execute(connection, "ROLLBACK;");
                throw;
            }
        }
        finally
        {
            Execute(connection, "PRAGMA foreign_keys = ON");
            Execute(connection, "PRAGMA legacy_alter_table = OFF");
        }
    }

    /// <summary>
    /// Rebuilds <paramref name="table"/> when one of its foreign keys points at
    /// a table that no longer exists — the damage an earlier migration did when
    /// it renamed a table while foreign keys were enabled. Without this repair
    /// the affected rows keep an unusable link.
    /// </summary>
    private static void RepairBrokenReferences(
        SqliteConnection connection,
        string table,
        string ddl,
        IReadOnlyList<string> columns)
    {
        if (ReadTableSql(connection, table) is null)
        {
            return;
        }

        var referenced = ReadReferencedTables(connection, table);
        var broken = referenced.Any(name => ReadTableSql(connection, name) is null);
        if (!broken)
        {
            return;
        }

        RebuildTable(connection, table, ddl, indexDdl: null, columns, onlyWhenShapeDiffers: false);
    }

    /// <summary>
    /// True when the stored table definition is not the current one: the
    /// databases this build meets either declare SolutionPath UNIQUE (the
    /// pre-migration-7 shape) or are already rebuilt.
    /// </summary>
    private static bool NeedsRebuild(string tableSql)
    {
        return tableSql.Contains("SolutionPath TEXT UNIQUE", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadTableSql(SqliteConnection connection, string table)
    {
        using var command = new SqliteCommand(
            "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $name;", connection);
        command.Parameters.AddWithValue("$name", table);
        return command.ExecuteScalar() as string;
    }

    private static List<string> ReadReferencedTables(SqliteConnection connection, string table)
    {
        using var command = new SqliteCommand($"PRAGMA foreign_key_list('{table}');", connection);
        using var reader = command.ExecuteReader();

        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(2));
        }

        return names;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = new SqliteCommand(sql, connection)
        {
            CommandTimeout = 10
        };
        command.ExecuteNonQuery();
    }
}
