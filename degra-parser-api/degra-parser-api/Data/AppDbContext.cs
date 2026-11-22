namespace degra_parser_api.Data
{
    using Microsoft.EntityFrameworkCore;
    using System.Text.Json;
    using degra_parser_api.Models;

    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Schedule> Schedules => Set<Schedule>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Schedule>(e =>
            {
                e.ToTable("schedules");
                e.HasKey(v => v.Id);
                e.Property(v => v.Data)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        s => JsonSerializer.Serialize(s, JsonOpts.Default),
                        j => JsonSerializer.Deserialize<degra.parser.Schedule>(j, JsonOpts.Default)!);
            });
        }
    }

    public static class JsonOpts
    {
        public static readonly JsonSerializerOptions Default = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
