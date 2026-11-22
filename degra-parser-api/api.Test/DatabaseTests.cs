using Microsoft.EntityFrameworkCore;
using Xunit;

public class DatabaseTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Can_connect_to_database()
    {
        await using var db = fx.CreateContext();
        Assert.True(await db.Database.CanConnectAsync());
    }

    [Fact]
    public async Task All_migrations_applied()
    {
        await using var db = fx.CreateContext();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Schedule_table_exists()
    {
        await using var db = fx.CreateContext();
        var tableExists = await db.Database.SqlQueryRaw<bool>(
            "SELECT to_regclass('public.schedules') IS NOT NULL AS \"Value\"")
            .SingleAsync();
        Assert.True(tableExists);
    }

    [Fact]
    public async Task Schedule_table_has_correct_columns()
    {
        await using var db = fx.CreateContext();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'schedules'
            """;

        await db.Database.OpenConnectionAsync();
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        Assert.Contains("Id", columns);
        Assert.Contains("Title", columns);
        Assert.Contains("UpdatedAt", columns);
        Assert.Contains("Data", columns);
    }

    [Fact]
    public async Task Schedule_table_has_correct_data_types()
    {
        await using var db = fx.CreateContext();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'schedules'
            """;

        await db.Database.OpenConnectionAsync();
        await using var reader = await command.ExecuteReaderAsync();
        var columnTypes = new Dictionary<string, string>();
        while (await reader.ReadAsync())
        {
            columnTypes[reader.GetString(0)] = reader.GetString(1);
        }

        Assert.Equal("uuid", columnTypes["Id"]);
        Assert.Equal("text", columnTypes["Title"]);
        Assert.Equal("timestamp with time zone", columnTypes["UpdatedAt"]);
        Assert.Equal("jsonb", columnTypes["Data"]);
    }

    [Fact]
    public async Task Schedule_table_has_correct_primary_key()
    {
        await using var db = fx.CreateContext();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.constraint_column_usage ccu ON tc.constraint_name = ccu.constraint_name
            WHERE tc.table_schema = 'public' AND tc.table_name = 'schedules' AND tc.constraint_type = 'PRIMARY KEY'
            """;

        await db.Database.OpenConnectionAsync();
        await using var reader = await command.ExecuteReaderAsync();
        var primaryKeyColumns = new List<string>();
        while (await reader.ReadAsync())
        {
            primaryKeyColumns.Add(reader.GetString(0));
        }

        Assert.Contains("Id", primaryKeyColumns);
    }
}
