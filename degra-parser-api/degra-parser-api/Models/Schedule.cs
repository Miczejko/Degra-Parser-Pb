using ParsedSchedule = degra.parser.Schedule;

namespace degra_parser_api.Models
{
    public class Schedule
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTimeOffset UpdatedAt { get; set; }
        public ParsedSchedule Data { get; set; } = default!;

    }
}
