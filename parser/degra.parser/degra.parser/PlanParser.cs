namespace degra.parser
{
    using UglyToad.PdfPig;
    using UglyToad.PdfPig.Content;
    using UglyToad.PdfPig.Core;
    using System.Text.RegularExpressions;
    using System.Diagnostics.Contracts;

    public static partial class PlanParser
    {
        public static Schedule Parse(String pdfPath)
        {
            Dictionary<int, string> dayNames = new Dictionary<int, string>
            {
                { 0, "Poniedziałek" },
                { 1, "Wtorek" },
                { 2, "Środa" },
                { 3, "Czwartek" },
                { 4, "Piątek" }
            };


            var pdf = PdfDocument.Open(pdfPath);
            var page = pdf.GetPage(1);

            Console.WriteLine($"Strona: {page.Width} x {page.Height}");

            List<ClassEntry> classEntries = new List<ClassEntry>();

            var h = page.Height;
            var words = ExtractRuns(page, h);   

            var timeslots = ExtractTimeSlots(words);    

            //Top position of the grid containing the schedule, used to filter out blocks that are not classes
            double gridTop = timeslots.Min(s => s.Box.Top) - 3;

            foreach (var ts in timeslots)
                Console.WriteLine($"Slot: {ts.Start} - {ts.End}, Box: {ts.Day}");

            Console.WriteLine("------------------------------------------");

            Console.WriteLine($"Liter: {page.Letters.Count}, słów: {words.Count()}, ścieżek: {page.Paths.Count}");

            var shapes = new List<(Box Box, bool Triangle)>();

            //All color filled blocks, grouped by color
            var byColor = page.Paths.Where(p => p.IsFilled)     
                .GroupBy(p => p.FillColor?.ToRGBValues())
                .ToList();

            //Adding all blocks to the shapes list, along with their bounding box and whether they are triangles or not
            foreach (var colorBoxes in byColor)
            {
                Console.WriteLine($"Kolor {colorBoxes.Key}: {colorBoxes.Count()} bloków");
                foreach (var p in colorBoxes)
                {
                    var r = p.GetBoundingRectangle();
                    if (r is null) continue;
                    (bool isTriangle, _) = ClassifyShape(p);
                    shapes.Add((ToBox(r.Value, h), isTriangle));
                }
            }

            //Group blocks by their bounding box, and create a new Block object for each group
            var blocks = shapes
                .GroupBy(s => (Math.Round(s.Box.Left), Math.Round(s.Box.Top),
                               Math.Round(s.Box.Right), Math.Round(s.Box.Bottom)))
                .Select(g => new Block(g.First().Box, g.Count(s => s.Triangle) >= 2))
                .ToList();

            var headers = ExtractHeaders(blocks, words, gridTop);
            foreach (HeaderColumn header in headers)
            {
                Console.WriteLine(header.Category);
            }

            //Adding text runs to the blocks, and creating ClassEntry objects for each block
            foreach (var b in blocks.Where(b => b.Box.Top > gridTop - 1.5))
            {  
                var wordsInside = words.Where(w => b.Box.Contains(w.Box.CenterX, w.Box.CenterY))
                    .ToList();

                foreach (var w in wordsInside)
                    b.Parts[b.IsSplit ? HalfOf(b.Box, w.Box) : 0].Add(w);

                var timeSlots = CoveredSlots(b.Box, timeslots); //Time slots that match the block's position on the page
                var group = MatchGroup(b.Box, headers);     //Group that matches the block's position on the page

                if (timeSlots.Count == 0)
                {
                    Console.WriteLine($"  ! Blok bez godzin: {b.Box}");
                    continue;
                }

                for (int i = 0; i < b.Parts.Length; i++)
                {
                    if (b.Parts[i].Count == 0) continue;

                    var lines = ToLines(b.Parts[i]);
                    var cell = ParseText(lines);
                    if (cell is null)
                    {
                        Console.WriteLine($"  ! Nie rozpoznano tekstu: \"{string.Join(" | ", lines)}\"");
                        continue;
                    }

                    Console.WriteLine($"Blok {b}");

                    classEntries.Add(new ClassEntry(
                        dayNames.TryGetValue(timeSlots.First().Day, out string? dayName) ? dayName : "Nieznany",
                        timeSlots.First().Start,
                        timeSlots.Last().End,
                        cell.Subject,
                        cell.Short,     // skrót przedmiotu
                        cell.Type,      // typ zajęć
                        cell.Room,      // sala
                        cell.Teacher,   // nauczyciel
                        group,          // grupa
                        b.IsSplit ? i + 1 : null));

                }
            }

            List<GroupNode> groups = makeGroups(headers);   //all groups, with their parent/child relationships
            List<TimePeriod> periods = makeTimePeriods(timeslots);  //all time periods, with their start/end times and the 45-minute units that make them up

            return new Schedule("test", "0", periods, groups, classEntries, new List<string>());
        }                         
    }
}
