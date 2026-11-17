using CommunityToolkit.Maui.Storage;
using Microsoft.Data.Sqlite;
using System.Diagnostics;

namespace NCockpit.Repositories;

/// <summary>
/// Owns the app's SQLite file: resolves its path and opens the connection.
/// The schema and its migrations live in <see cref="DatabaseSchema"/>, which
/// stays free of MAUI types so it can be applied to any connection.
/// </summary>
internal static class Database
{
    internal static string DbPath { get; private set; } = default!;

    /// <summary>
    /// Initialize the database. The app is in development, so the database is
    /// simply (re)created from the schema — delete the file to reset.
    /// </summary>
    internal static void Initialize()
    {
        DbPath = Path.Combine(FileSystem.AppDataDirectory, "ncockpit_data.db");
        Debug.WriteLine("SQLite: " + DbPath);

        using var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();

        DatabaseSchema.Apply(connection);
    }
}
