using ParsedSchedule = degra.parser.Schedule;

namespace degra_parser_api.Models
{
    public class Schedule
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTimeOffset UpdatedAt { get; set; }
        public ParsedSchedule Data { get; set; } = default!;

        // URL of the PDF this schedule was parsed from (identifies the row for upsert)
        public string SourceUrl { get; set; } = string.Empty;
        // SHA256 (hex) of the downloaded PDF, used to detect changes
        public string ContentHash { get; set; } = string.Empty;

    }
}
