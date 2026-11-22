using System.Text.Json;
using degra_parser_api.Data;
using degra_parser_api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Parsed = degra.parser.Schedule;

public class ScheduleBehaviorTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private static Parsed SampleData() => new(
        Title: "Plan 3A",
        UpdatedAt: "2026-11-22",
        TimeSlots: [new(1, "08:00", "08:45", [new("08:00", "08:45")])],
        Groups: [new("3A", "klasa", null, ["3A-1", "3A-2"])],
        Entries:
        [
            new("Pn", "08:00", "08:45", "Matematyka", "MAT", "wykład", "101", "Kowalski", "3A", null),
            new("Wt", "09:00", "09:45", "Fizyka", null, "lab", null, null, null, 1)
        ],
        Warnings: ["ostrzeżenie"]);

    // Rekordy z listami nie mają równości strukturalnej, więc porównujemy JSON.
    private static string Json(Parsed s) => JsonSerializer.Serialize(s, JsonOpts.Default);

    private static Schedule NewSchedule(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Title = "Plan 3A",
        UpdatedAt = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero),
        Data = SampleData()
    };

    [Fact]
    public async Task Data_roundtrips_through_jsonb()
    {
        var schedule = NewSchedule();
        await using (var db = fx.CreateContext())
        {
            db.Schedules.Add(schedule);
            await db.SaveChangesAsync();
        }

        await using var read = fx.CreateContext();
        var loaded = await read.Schedules.SingleAsync(s => s.Id == schedule.Id);

        Assert.Equal(Json(schedule.Data), Json(loaded.Data));
        Assert.Equal("Matematyka", loaded.Data.Entries[0].Subject);
        Assert.Null(loaded.Data.Entries[1].Room);
        Assert.Equal(1, loaded.Data.Entries[1].Part);
    }

    [Fact]
    public async Task Basic_fields_roundtrip()
    {
        var schedule = NewSchedule();
        await using (var db = fx.CreateContext())
        {
            db.Schedules.Add(schedule);
            await db.SaveChangesAsync();
        }

        await using var read = fx.CreateContext();
        var loaded = await read.Schedules.SingleAsync(s => s.Id == schedule.Id);

        Assert.Equal(schedule.Title, loaded.Title);
        Assert.Equal(schedule.UpdatedAt.UtcDateTime, loaded.UpdatedAt.UtcDateTime);
    }

    [Fact]
    public async Task Data_is_stored_as_camelCase_json()
    {
        var schedule = NewSchedule();
        await using var db = fx.CreateContext();
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        var hasKey = await db.Database
            .SqlQuery<bool>($"""SELECT "Data" ? 'timeSlots' AS "Value" FROM schedules WHERE "Id" = {schedule.Id}""")
            .SingleAsync();

        Assert.True(hasKey);
    }

    [Fact]
    public async Task Duplicate_id_is_rejected()
    {
        var id = Guid.NewGuid();
        await using (var db = fx.CreateContext())
        {
            db.Schedules.Add(NewSchedule(id));
            await db.SaveChangesAsync();
        }

        await using var db2 = fx.CreateContext();
        db2.Schedules.Add(NewSchedule(id));
        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task Null_data_is_rejected()
    {
        await using var db = fx.CreateContext();
        var schedule = NewSchedule();
        schedule.Data = null!;
        db.Schedules.Add(schedule);

        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Replacing_data_is_persisted()
    {
        var schedule = NewSchedule();
        await using (var db = fx.CreateContext())
        {
            db.Schedules.Add(schedule);
            await db.SaveChangesAsync();
        }

        await using (var db = fx.CreateContext())
        {
            var loaded = await db.Schedules.SingleAsync(s => s.Id == schedule.Id);
            loaded.Title = "Zmieniony";
            loaded.Data = SampleData() with { Title = "Nowe dane" };
            await db.SaveChangesAsync();
        }

        await using var read = fx.CreateContext();
        var result = await read.Schedules.SingleAsync(s => s.Id == schedule.Id);
        Assert.Equal("Zmieniony", result.Title);
        Assert.Equal("Nowe dane", result.Data.Title);
    }

    [Fact]
    public async Task Deleting_removes_row()
    {
        var schedule = NewSchedule();
        await using (var db = fx.CreateContext())
        {
            db.Schedules.Add(schedule);
            await db.SaveChangesAsync();
        }

        await using (var db = fx.CreateContext())
        {
            var loaded = await db.Schedules.SingleAsync(s => s.Id == schedule.Id);
            db.Schedules.Remove(loaded);
            await db.SaveChangesAsync();
        }

        await using var read = fx.CreateContext();
        Assert.False(await read.Schedules.AnyAsync(s => s.Id == schedule.Id));
    }
}
