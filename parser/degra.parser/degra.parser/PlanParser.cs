namespace degra.parser
{
    using UglyToad.PdfPig;
    using UglyToad.PdfPig.Content;
    using UglyToad.PdfPig.Core;
    using System.Text.RegularExpressions;

    public static partial class PlanParser
    {
        public static Schedule Parse(String pdfPath)
        {
            var pdf = PdfDocument.Open(pdfPath);
            var page = pdf.GetPage(1);

            Console.WriteLine($"Strona: {page.Width} x {page.Height}");


            var h = page.Height;
            var words = ExtractRuns(page, h);   //wszystkie slowa z pdf

            var timeslots = ExtractTimeSlots(words);    //godziny 
            double gridTop = timeslots.Min(s => s.Box.Top) - 3;

            foreach (var ts in timeslots)
                Console.WriteLine($"Slot: {ts.Start} - {ts.End}, Box: {ts.Day}");

            Console.WriteLine("------------------------------------------");

            Console.WriteLine($"Liter: {page.Letters.Count}, słów: {words.Count()}, ścieżek: {page.Paths.Count}");

            // kształty: obrys + czy to trójkąt
            var shapes = new List<(Box Box, bool Triangle)>();

            var byColor = page.Paths.Where(p => p.IsFilled)     //Kolorowe bloki pogrupowane
                .GroupBy(p => p.FillColor?.ToRGBValues())
                .ToList();

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

            // kształty o tym samym obrysie = jeden blok; dwa trójkąty = komórka przecięta
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

            foreach (var b in blocks)
            {  //dla kazdego bloku dopisanie słow ze srodka
                var wordsInside = words.Where(w => b.Box.Contains(w.Box.CenterX, w.Box.CenterY))
                    .ToList();

                //dodanie slow do bloku: przecięte -> połówka wg przekątnej, zwykłe -> Parts[0]
                foreach (var w in wordsInside)
                    b.Parts[b.IsSplit ? HalfOf(b.Box, w.Box) : 0].Add(w);

                Console.WriteLine($"Blok {MatchGroup(b.Box, headers)} ---- {b}");
            }


            return null;
        }

        static List<TimeSlot> ExtractTimeSlots(List<TextRun> runs)
        {
            var rx = new Regex(@"(\d{1,2})[.:](\d{2})\s*[—–-]\s*(\d{1,2})[.:](\d{2})");

            // runy, których CAŁY tekst jest godziną
            var matches = runs
                .Select(r => (Run: r, Match: rx.Match(r.Text)))
                .Where(x => x.Match.Success && x.Match.Value.Length == x.Run.Text.Length)
                .ToList();
            if (matches.Count == 0) return [];

            double leftmost = matches.Min(x => x.Run.Box.Left);

            var slots = new List<TimeSlot>();
            int day = 0;
            TimeSpan? last = null;                       // poprzednia godzina startu

            foreach (var (run, m) in matches
                        .Where(x => x.Run.Box.Left < leftmost + 15)   // tylko lewa kolumna
                        .OrderBy(x => x.Run.Box.Top))                 // z góry na dół
            {
                var start = new TimeSpan(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), 0);

                if (last is not null && start <= last) day++;   // godziny zaczęły się od nowa = nowy dzień
                last = start;

                slots.Add(new TimeSlot(
                    day,
                    $"{int.Parse(m.Groups[1].Value):00}:{m.Groups[2].Value}",
                    $"{int.Parse(m.Groups[3].Value):00}:{m.Groups[4].Value}",
                    run.Box));
            }
            return slots;
        }
        static List<HeaderColumn> ExtractHeaders(List<Block> blocks, List<TextRun> runs, double gridTop)
        {
            List<HeaderColumn> headers = new List<HeaderColumn>();
            var rx = new Regex(@"^(W|Ćw|Ps|L|P)\s*(\d+)$");
            var headerBlocks = blocks.Where(b => (b.Box.Bottom <= gridTop + 1.5) && (b.Box.Height < 15))
                .ToList();
            foreach (var header in headerBlocks)
            {
                foreach (var run in runs.Where(r => header.Box.Contains(r.Box.CenterX, r.Box.CenterY)))
                {
                    var m = rx.Match(run.Text);
                    if (!m.Success) continue;

                    headers.Add(new HeaderColumn(
                        $"{m.Groups[1].Value} {m.Groups[2].Value}",   
                        m.Groups[1].Value,                            
                        header.Box));
                    break;                                            
                }
            }

            return headers
                .GroupBy(c => c.Label).Select(g => g.First())         // bez duplikatów
                .OrderBy(c => c.Box.Top).ThenBy(c => c.Box.Left)
                .ToList();
            return null;
        }

        static string? MatchGroup(Box block, List<HeaderColumn> headers)
        {
            var header = headers.Where(h =>
                    Math.Abs(block.Left - h.Box.Left) < 1.5 && Math.Abs(block.Right - h.Box.Right) < 1.5)
                .OrderByDescending(h => h.Box.Width)
                .FirstOrDefault();
            if (header is not null) return header.Label;

            bool fullWidth = Math.Abs(block.Left - headers.Min(h => h.Box.Left)) < 1.5
                          && Math.Abs(block.Right - headers.Max(h => h.Box.Right)) < 1.5;
            return fullWidth ? "Wszyscy" : null;
        }
    }

}
