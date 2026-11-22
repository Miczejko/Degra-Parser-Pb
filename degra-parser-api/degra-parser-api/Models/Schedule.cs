using Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.Json;
using degra.parser;

namespace degra_parser_api.Models
{
    public class Schedule
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTimeOffset UpdatedAt { get; set; }
        public Schedule Data { get; set; } = default!;

    }
}
