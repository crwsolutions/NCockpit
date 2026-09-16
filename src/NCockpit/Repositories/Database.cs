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

    internal const int CurrentUserVersion = 1;

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

        var userVersion = GetInt(connection, "PRAGMA user_version");

        // v0 → v1: initial schema with ProjectFolders table
        if (userVersion < 1)
        {
            CreateCoreTables(connection);
            SetUserVersion(connection, CurrentUserVersion);
            return;
        }
    }

    private static void CreateDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();

        CreateCoreTables(connection);
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
