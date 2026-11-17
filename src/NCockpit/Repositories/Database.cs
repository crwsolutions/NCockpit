using CommunityToolkit.Maui.Storage;
using Microsoft.Data.Sqlite;
using System.Diagnostics;

namespace NCockpit.Repositories;

/// <summary>
/// Manages SQLite database initialization and schema management.
/// </summary>
internal static class Database
{
    internal static string DbPath { get; private set; } = default!;

    /// <summary>
    /// Initialize the database. The app is in development, so the database is
    /// simply (re)created from the schema below — delete the file to reset.
    /// </summary>
    internal static void Initialize()
    {
        DbPath = Path.Combine(FileSystem.AppDataDirectory, "ncockpit_data.db");
        Debug.WriteLine("SQLite: " + DbPath);

        using var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();

        Execute(connection, "PRAGMA foreign_keys = ON");

        using var command = new SqliteCommand(
            """
            CREATE TABLE IF NOT EXISTS ProjectFolders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Path TEXT NOT NULL UNIQUE,
                IsPrimary INTEGER NOT NULL DEFAULT 1,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                ProjectCount INTEGER NOT NULL DEFAULT 0,
                LastScanUnix INTEGER,
                CreatedAt INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS SupportingFolders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Path TEXT NOT NULL UNIQUE COLLATE NOCASE,
                FolderId INTEGER NOT NULL REFERENCES ProjectFolders(Id) ON DELETE CASCADE,
                ProjectId INTEGER REFERENCES Projects(Id) ON DELETE SET NULL,
                CreatedAt INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Projects (
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
                ProjectsJson TEXT,
                SolutionsJson TEXT,
                SolutionLastUsedUnix INTEGER
            );
            """, connection)
        {
            CommandTimeout = 10
        };
        command.ExecuteNonQuery();

        Migrate(connection);
    }

    private static void Migrate(SqliteConnection connection)
    {
        // CurrentUserVersion should track the highest migration step applied.
        const int CurrentUserVersion = 5;

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
            //    solution file) and stops being the unique key. SQLite can
            //    only drop the index it auto-created for the column constraint,
            //    so find it by the automatic name pattern.
            try { Execute(connection, "DROP INDEX sqlite_autoindex_Projects_1"); }
            catch (SqliteException) { }

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

        Execute(connection, $"PRAGMA user_version = {CurrentUserVersion};");
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
