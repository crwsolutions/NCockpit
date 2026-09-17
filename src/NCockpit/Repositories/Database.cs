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

    internal const int CurrentUserVersion = 5;

    /// <summary>
    /// Initialize the database, creating it if necessary or running migrations.
    /// </summary>
    internal static void Initialize()
    {
        DbPath = Path.Combine(FileSystem.AppDataDirectory, "ncockpit_data.db");
        Debug.WriteLine("SQLite: " + DbPath);

        if (!File.Exists(DbPath))
        {
            CreateDatabase();
        }
        else
        {
            Migrate();
        }
    }

    private static void Migrate()
    {
        using var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();

        EnableForeignKeys(connection);

        var userVersion = GetInt(connection, "PRAGMA user_version");

        // v0 → v1: initial schema with ProjectFolders table
        if (userVersion < 1)
        {
            CreateCoreTables(connection);
        }

        // v1 → v2: add Projects table
        if (userVersion < 2)
        {
            CreateProjectsTable(connection);
        }

        // v2 → v3: add favorites and recent-usage columns to Projects
        if (userVersion < 3)
        {
            AddProjectFavoriteAndRecentUsageColumns(connection);
        }

        // v3 → v4: add solution file last-write time to Projects
        if (userVersion < 4)
        {
            AddProjectSolutionLastWriteColumn(connection);
        }

        // v4 → v5: replace Projects table with a version that has a hard
        // foreign key to ProjectFolders (ON DELETE CASCADE) and no soft-delete column.
        if (userVersion < 5)
        {
            RecreateProjectsTableWithForeignKey(connection);
        }

        SetUserVersion(connection, CurrentUserVersion);
    }

    private static void CreateDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();

        EnableForeignKeys(connection);

        CreateCoreTables(connection);
        CreateProjectsTable(connection);
        SetUserVersion(connection, CurrentUserVersion);
    }

    private static void CreateCoreTables(SqliteConnection connection)
    {
        using var command = new SqliteCommand(
            """
            CREATE TABLE ProjectFolders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Path TEXT NOT NULL UNIQUE,
                IsPrimary INTEGER NOT NULL DEFAULT 1,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                ProjectCount INTEGER NOT NULL DEFAULT 0,
                LastScanUnix INTEGER,
                CreatedAt INTEGER NOT NULL
            );
            """, connection)
        {
            CommandTimeout = 10
        };
        command.ExecuteNonQuery();
    }

    private static void CreateProjectsTable(SqliteConnection connection)
    {
        using var command = new SqliteCommand(
            """
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
                SolutionLastWriteUnix INTEGER
            );
            """, connection)
        {
            CommandTimeout = 10
        };
        command.ExecuteNonQuery();
    }

    private static void AddProjectFavoriteAndRecentUsageColumns(SqliteConnection connection)
    {
        // IsFavorite
        if (ColumnExists(connection, "Projects", "IsFavorite") is not true)
        {
            Execute(connection, "ALTER TABLE Projects ADD COLUMN IsFavorite INTEGER NOT NULL DEFAULT 0");
        }

        // LastUsedUnix (nullable: null means the project has never been opened)
        if (ColumnExists(connection, "Projects", "LastUsedUnix") is not true)
        {
            Execute(connection, "ALTER TABLE Projects ADD COLUMN LastUsedUnix INTEGER");
        }
    }

    private static void AddProjectSolutionLastWriteColumn(SqliteConnection connection)
    {
        // SolutionLastWriteUnix (nullable: file metadata captured during scan)
        if (ColumnExists(connection, "Projects", "SolutionLastWriteUnix") is not true)
        {
            Execute(connection, "ALTER TABLE Projects ADD COLUMN SolutionLastWriteUnix INTEGER");
        }
    }

    /// <summary>
    /// Drops and recreates the Projects table with a hard foreign key to
    /// ProjectFolders and without the soft-delete column. Existing project rows
    /// are lost; they will be rediscovered on the next scan.
    /// </summary>
    private static void RecreateProjectsTableWithForeignKey(SqliteConnection connection)
    {
        Execute(connection, "DROP TABLE IF EXISTS Projects");
        CreateProjectsTable(connection);
    }

    private static bool? ColumnExists(SqliteConnection connection, string table, string column)
    {
        using var command = new SqliteCommand($"PRAGMA table_info({table})", connection)
        {
            CommandTimeout = 10
        };
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnableForeignKeys(SqliteConnection connection)
    {
        Execute(connection, "PRAGMA foreign_keys = ON");
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = new SqliteCommand(sql, connection)
        {
            CommandTimeout = 10
        };
        command.ExecuteNonQuery();
    }

    private static void SetUserVersion(SqliteConnection connection, int version)
    {
        using var command = new SqliteCommand($"PRAGMA user_version = {version}", connection)
        {
            CommandTimeout = 10
        };
        command.ExecuteNonQuery();
    }

    private static int GetInt(SqliteConnection connection, string pragma)
    {
        using var command = new SqliteCommand(pragma, connection)
        {
            CommandTimeout = 10
        };
        var result = command.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }
}
