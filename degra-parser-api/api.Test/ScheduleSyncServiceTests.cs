using System.Net;
using System.Security.Cryptography;
using degra_parser_api.Data;
using degra_parser_api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class ScheduleSyncServiceTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    // ---- Test doubles --------------------------------------------------------------

    // Fake "degra website": url -> PDF bytes (or an error status when the url is unknown)
    private sealed class FakeSite : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Files { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            return Task.FromResult(Files.TryGetValue(url, out var bytes)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }

    private sealed class FakeHttpFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    // Clock we can move by hand, so "UpdatedAt" assertions are deterministic
    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // ---- Helpers -------------------------------------------------------------------

    private static byte[] Pdf(string name) => File.ReadAllBytes(Path.Combine("TestData", name));

    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    // Each test uses its own unique URL, so tests sharing one database don't affect each other
    private static string NewUrl() => $"https://degra.test/{Guid.NewGuid():N}.pdf";

    private ScheduleSyncService CreateService(FakeSite site, ManualTime time, params string[] urls)
    {
        var services = new ServiceCollection()
            .AddDbContext<AppDbContext>(o => o.UseNpgsql(fx.ConnectionString))
            .BuildServiceProvider();

        var options = Options.Create(new ScheduleSyncOptions { Urls = [.. urls] });

        return new ScheduleSyncService(
            services.GetRequiredService<IServiceScopeFactory>(),
            new FakeHttpFactory(site),
            time,
            options,
            NullLogger<ScheduleSyncService>.Instance);
    }

    // ---- Tests ---------------------------------------------------------------------

    [Fact]
    public async Task New_source_is_parsed_and_inserted()
    {
        var url = NewUrl();
        var pdf = Pdf("isi5.pdf");
        var site = new FakeSite { Files = { [url] = pdf } };
        var time = new ManualTime(new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero));

        await CreateService(site, time, url).SyncAllAsync(CancellationToken.None);

        await using var db = fx.CreateContext();
        var row = await db.Schedules.SingleAsync(s => s.SourceUrl == url);

        Assert.Equal(Sha(pdf), row.ContentHash);
        Assert.Equal(time.Now, row.UpdatedAt);
        Assert.NotEmpty(row.Data.Entries);
    }

    [Fact]
    public async Task Unchanged_pdf_leaves_row_untouched()
    {
        var url = NewUrl();
        var site = new FakeSite { Files = { [url] = Pdf("isi5.pdf") } };
        var time = new ManualTime(new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero));
        var service = CreateService(site, time, url);

        await service.SyncAllAsync(CancellationToken.None);
        var firstUpdate = time.Now;

        time.Now = firstUpdate.AddHours(1);          // an hour passes, the PDF stays the same
        await service.SyncAllAsync(CancellationToken.None);

        await using var db = fx.CreateContext();
        var rows = await db.Schedules.Where(s => s.SourceUrl == url).ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal(firstUpdate, row.UpdatedAt);    // not rewritten
    }

    [Fact]
    public async Task Changed_pdf_updates_the_same_row()
    {
        var url = NewUrl();
        var site = new FakeSite { Files = { [url] = Pdf("isi5.pdf") } };
        var time = new ManualTime(new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero));
        var service = CreateService(site, time, url);

        await service.SyncAllAsync(CancellationToken.None);
        Guid firstId;
        await using (var db = fx.CreateContext())
            firstId = (await db.Schedules.SingleAsync(s => s.SourceUrl == url)).Id;

        // Same URL, but degra published a different PDF
        var newPdf = Pdf("isi1.pdf");
        site.Files[url] = newPdf;
        time.Now = time.Now.AddHours(1);
        await service.SyncAllAsync(CancellationToken.None);

        await using var read = fx.CreateContext();
        var row = Assert.Single(await read.Schedules.Where(s => s.SourceUrl == url).ToListAsync());

        Assert.Equal(firstId, row.Id);               // upsert, not a second row
        Assert.Equal(Sha(newPdf), row.ContentHash);
        Assert.Equal(time.Now, row.UpdatedAt);
    }

    [Fact]
    public async Task Failing_download_does_not_block_other_sources()
    {
        var broken = NewUrl();                       // not in FakeSite -> HTTP 500
        var good = NewUrl();
        var site = new FakeSite { Files = { [good] = Pdf("isi5.pdf") } };
        var time = new ManualTime(DateTimeOffset.UtcNow);

        // must not throw, and the second URL must still be processed
        await CreateService(site, time, broken, good).SyncAllAsync(CancellationToken.None);

        await using var db = fx.CreateContext();
        Assert.False(await db.Schedules.AnyAsync(s => s.SourceUrl == broken));
        Assert.True(await db.Schedules.AnyAsync(s => s.SourceUrl == good));
    }
}
