using static System.Net.WebRequestMethods;

namespace degra_parser_api.Services
{
    public class ScheduleSyncOptions
    {
        public const string SectionName = "ScheduleSync";

        // How often the PDFs are checked
        public int IntervalMinutes { get; set; } = 60;

        // PDF URLs on the degra site, one per schedule
        public List<string> Urls { get; set; } = [];
    }
}
