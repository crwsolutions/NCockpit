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
                SolutionPath TEXT NOT NULL UNIQUE COLLATE NOCASE,
                FolderPath TEXT NOT NULL,
                FolderId INTEGER NOT NULL REFERENCES ProjectFolders(Id) ON DELETE CASCADE,
                LastSeenUnix INTEGER NOT NULL,
                ScanUnix INTEGER NOT NULL,
                CreatedAt INTEGER NOT NULL,
                IsFavorite INTEGER NOT NULL DEFAULT 0,
                LastUsedUnix INTEGER,
                SolutionLastWriteUnix INTEGER,
                FilesJson TEXT
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
        const int CurrentUserVersion = 2;

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
