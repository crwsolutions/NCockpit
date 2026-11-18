using Dapper;
using Microsoft.Data.Sqlite;

namespace NCockpit.Repositories;

/// <summary>
/// Stores simple key/value application settings in the app's SQLite database.
/// Used for small per-device preferences such as the collapsed state of the
/// left navigation flyout.
/// </summary>
public interface IAppSettingsRepository
{
    /// <summary>
    /// Returns the stored value for <paramref name="key"/>, or null when the
    /// key has never been set.
    /// </summary>
    Task<string?> GetValueAsync(string key);

    /// <summary>
    /// Creates or updates the stored value for <paramref name="key"/>.
    /// </summary>
    Task SetAsync(string key, string value);
}

public class AppSettingsRepository : IAppSettingsRepository
{
    private readonly string _dbPath;

    public AppSettingsRepository()
    {
        _dbPath = Database.DbPath;
    }

    public async Task<string?> GetValueAsync(string key)
    {
        using var connection = OpenConnection();
        const string sql = "SELECT Value FROM AppSettings WHERE Key = @Key";
        return await connection.QueryFirstOrDefaultAsync<string?>(sql, new { Key = key });
    }

    public async Task SetAsync(string key, string value)
    {
        using var connection = OpenConnection();
        await connection.OpenAsync();

        const string upsertSql = """
            INSERT INTO AppSettings (Key, Value) VALUES (@Key, @Value)
            ON CONFLICT (Key) DO UPDATE SET Value = excluded.Value;
            """;

        await connection.ExecuteAsync(upsertSql, new { Key = key, Value = value });
    }

    private SqliteConnection OpenConnection()
    {
        return new SqliteConnection($"Data Source={_dbPath}");
    }
}
