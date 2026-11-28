using degra_parser_api.Controllers;
using degra_parser_api.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Parsed = degra.parser.Schedule;

public class SchedulesControllerTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private static Schedule NewSchedule(string title, DateTimeOffset updatedAt) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        UpdatedAt = updatedAt,
        SourceUrl = $"https://degra.test/{Guid.NewGuid():N}.pdf",
        Data = new Parsed(title, null, [], [], [], [])
    };

    [Fact]
    public async Task GetSchedule_returns_NotFound_for_unknown_id()
    {
        await using var db = fx.CreateContext();
        var controller = new SchedulesController(db);

        var result = await controller.GetSchedule(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetSchedule_returns_the_requested_schedule()
    {
        var schedule = NewSchedule("Plan A", DateTimeOffset.UtcNow);
        await using (var seed = fx.CreateContext())
        {
            seed.Schedules.Add(schedule);
            await seed.SaveChangesAsync();
        }

        await using var db = fx.CreateContext();
        var result = await new SchedulesController(db).GetSchedule(schedule.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(schedule.Id, Assert.IsType<Schedule>(ok.Value).Id);
    }

    [Fact]
    public async Task GetLite_returns_newest_first_without_data()
    {
        var older = NewSchedule("Starszy", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var newer = NewSchedule("Nowszy", new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        await using (var seed = fx.CreateContext())
        {
            seed.Schedules.AddRange(older, newer);
            await seed.SaveChangesAsync();
        }

        await using var db = fx.CreateContext();
        var result = await new SchedulesController(db).GetLite(CancellationToken.None);

        var list = Assert.IsType<List<ScheduleLiteDto>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var ids = list.Select(x => x.Id).ToList();

        Assert.True(ids.IndexOf(newer.Id) < ids.IndexOf(older.Id));   // sorted by UpdatedAt desc
        Assert.Equal("Nowszy", list.First(x => x.Id == newer.Id).Title);
    }
}
