using System.Security.Cryptography;
using degra_parser_api.Data;
using degra_parser_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace degra_parser_api.Services
{
    /// <summary>
    /// Periodically downloads the configured PDFs and, when a PDF changed,
    /// parses it and upserts the result into the database (one row per SourceUrl).
    /// </summary>
    public class ScheduleSyncService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpFactory,
        TimeProvider time,
        IOptions<ScheduleSyncOptions> options,
        ILogger<ScheduleSyncService> logger) : BackgroundService
    {
        private readonly ScheduleSyncOptions _options = options.Value;

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            var interval = TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes));
            logger.LogInformation("Schedule sync started: {Count} source(s), every {Interval}",
                _options.Urls.Count, interval);

            await SyncAllAsync(ct);

            using var timer = new PeriodicTimer(interval, time);
            while (await timer.WaitForNextTickAsync(ct))
            {
                await SyncAllAsync(ct);
            }
        }

        private async Task SyncAllAsync(CancellationToken ct)
        {
            logger.LogInformation("Sync round started");

            foreach (var url in _options.Urls)
            {
                try
                {
                    await SyncOneAsync(url, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[{Url}] Sync failed", url);
                }
            }

            logger.LogInformation("Sync round finished");
        }

        private async Task SyncOneAsync(string url, CancellationToken ct)
        {
            logger.LogInformation("[{Url}] Downloading", url);
            var client = httpFactory.CreateClient();
            var bytes = await client.GetByteArrayAsync(url, ct);
            logger.LogInformation("[{Url}] Downloaded {Size} bytes", url, bytes.Length);

            var hash = Convert.ToHexString(SHA256.HashData(bytes));

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var existing = await db.Schedules.FirstOrDefaultAsync(s => s.SourceUrl == url, ct);

            if (existing is not null && existing.ContentHash == hash)
            {
                logger.LogInformation("[{Url}] Unchanged (hash {Hash}), skipping", url, hash[..8]);
                return;
            }

            logger.LogInformation("[{Url}] {Reason}, parsing",
                url, existing is null ? "New source" : "PDF changed");

            var tempPath = Path.Combine(Path.GetTempPath(), $"degra-{Guid.NewGuid():N}.pdf");
            try
            {
                await File.WriteAllBytesAsync(tempPath, bytes, ct);
                var parsed = degra.parser.PlanParser.Parse(tempPath);
                logger.LogInformation("[{Url}] Parsed {Entries} entries, {Warnings} warning(s)",
                    url, parsed.Entries.Count, parsed.Warnings.Count);

                var now = time.GetUtcNow();
                if (existing is null)
                {
                    db.Schedules.Add(new Schedule
                    {
                        Id = Guid.NewGuid(),
                        SourceUrl = url,
                        Title = parsed.Title ?? url,
                        UpdatedAt = now,
                        ContentHash = hash,
                        Data = parsed
                    });
                }
                else
                {
                    existing.Title = parsed.Title ?? url;
                    existing.UpdatedAt = now;
                    existing.ContentHash = hash;
                    existing.Data = parsed;
                }

                await db.SaveChangesAsync(ct);
                logger.LogInformation("[{Url}] Saved to database", url);
            }
            finally
            {
                File.Delete(tempPath);
            }
        }
    }
}
