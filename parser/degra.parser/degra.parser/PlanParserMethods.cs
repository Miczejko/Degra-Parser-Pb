using System;
using System.Collections.Generic;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using System.Text.RegularExpressions;

namespace degra.parser
{
    public static partial class PlanParser
    {
        static List<TimeSlot> ExtractTimeSlots(List<TextRun> runs)
        {
            var rx = new Regex(@"(\d{1,2})[.:](\d{2})\s*[—–-]\s*(\d{1,2})[.:](\d{2})");

            var matches = runs
                .Select(r => (Run: r, Match: rx.Match(r.Text)))
                .Where(x => x.Match.Success && x.Match.Value.Length == x.Run.Text.Length)
                .ToList();
            if (matches.Count == 0) return [];

            double leftmost = matches.Min(x => x.Run.Box.Left);

            var slots = new List<TimeSlot>();
            int day = 0;
            TimeSpan? last = null;                       

            foreach (var (run, m) in matches
                        .Where(x => x.Run.Box.Left < leftmost + 15)   
                        .OrderBy(x => x.Run.Box.Top))                 
            {
                var start = new TimeSpan(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), 0);

                if (last is not null && start <= last) day++;   
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
                .GroupBy(c => c.Label).Select(g => g.First())         
                .OrderBy(c => c.Box.Top).ThenBy(c => c.Box.Left)
                .ToList();
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

        static List<TimeSlot> CoveredSlots(Box block, List<TimeSlot> slots) =>
            slots.Where(s => s.Box.CenterY > block.Top && s.Box.CenterY < block.Bottom)
                 .OrderBy(s => s.Box.Top)
                 .ToList();
        static List<TextRun> ExtractRuns(Page page, double pageHeight)
        {
            var result = new List<TextRun>();
            var sb = new StringBuilder();
            Letter? prev = null;
            double l = 0, t = 0, r = 0, b = 0;

            void Flush()
            {
                var text = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
                if (text.Length > 0)
                    result.Add(new TextRun(text, new Box(l, t, r, b)));
                sb.Clear();
                prev = null;
            }

            foreach (var letter in page.Letters)
            {
                if (letter.TextOrientation != TextOrientation.Horizontal)
                {
                    Flush();
                    continue;
                }

                var g = letter.BoundingBox;
                double top = pageHeight - Math.Max(g.Top, g.Bottom);
                double bottom = pageHeight - Math.Min(g.Top, g.Bottom);

                if (prev is not null)
                {
                    bool sameLine = Math.Abs(prev.StartBaseLine.Y - letter.StartBaseLine.Y) < 0.5;
                    double gap = letter.StartBaseLine.X - prev.EndBaseLine.X;
                    if (!sameLine || gap < -0.5 || gap > letter.PointSize * 0.6)
                        Flush();
                }

                if (prev is null)
                {
                    l = g.Left; r = g.Right; t = top; b = bottom;
                }
                else
                {
                    l = Math.Min(l, g.Left); r = Math.Max(r, g.Right);
                    t = Math.Min(t, top); b = Math.Max(b, bottom);
                }

                sb.Append(letter.Value);
                prev = letter;
            }
            Flush();

            return result;
        }

        static Box ToBox(PdfRectangle r, double pageHeight) => new Box(
            Math.Min(r.Left, r.Right),
            pageHeight - Math.Max(r.Top, r.Bottom),
            Math.Max(r.Left, r.Right),
            pageHeight - Math.Min(r.Top, r.Bottom));

        static (bool Triangle, bool TopLeft) ClassifyShape(UglyToad.PdfPig.Graphics.PdfPath path)
        {
            var points = path
                .SelectMany(sub => sub.Commands)            
                .SelectMany(cmd => cmd switch
                {
                    PdfSubpath.Move m => new[] { m.Location },
                    PdfSubpath.Line l => new[] { l.From, l.To },
                    _ => []
                })
                .Select(p => (X: Math.Round(p.X, 1), Y: Math.Round(p.Y, 1)))   
                .Distinct()
                .ToList();

            if (points.Count != 3) return (false, false);

            double minX = points.Min(p => p.X);
            double maxY = points.Max(p => p.Y);
            bool topLeft = points.Any(p => Math.Abs(p.X - minX) < 0.5 && Math.Abs(p.Y - maxY) < 0.5);
            return (true, topLeft);
        }

        static int HalfOf(Box cell, Box text)
        {
            double u = (text.CenterX - cell.Left) / cell.Width
                    + (text.CenterY - cell.Top) / cell.Height;
            return u < 1 ? 0 : 1;
        }

        static List<GroupNode> makeGroups(List<HeaderColumn> headers)
        {
            var groups = new List<GroupNode>();
            foreach(var header in headers)
            {
                string? parent = null;
                if (header.Category == "Ćw" || header.Category == "Ps")
                {
                    parent = headers.FirstOrDefault(h =>
                        h.Category == "W" &&
                        header.Box.Left >= h.Box.Left)?.Label;
                }                
                var children = headers.Where(h =>
                    (h.Category == "Ćw" || h.Category == "Ps") &&
                    h.Box.Left >= header.Box.Left &&
                    h.Box.Right <= header.Box.Right &&
                    h.Label != header.Label)
                    .Select(h => h.Label)
                    .ToList();
                groups.Add(new GroupNode(header.Label, header.Category, parent, children));
            }
            return groups;
        }

        static List<TimePeriod> makeTimePeriods(List<TimeSlot> slots)
        {
            var periods = new List<TimePeriod>();
            var dailySlots = slots.Where(s => s.Day == slots[0].Day)
                .OrderBy(s => s.Start);
            int index = 1;
            foreach (var slot in dailySlots.Chunk(2))
            {   
                var units = slot.Select(s => new TimeRange(s.Start, s.End)).ToList();
                periods.Add(new TimePeriod(index, slot[0].Start, slot[^1].End, units));
                index++;
            }
            return periods;
        }

        static List<string> ToLines(List<TextRun> runs)
        {
            var lines = new List<List<TextRun>>();
            foreach (var run in runs.OrderBy(r => r.Box.Top))
            {
                var line = lines.LastOrDefault();
                if (line is not null && Math.Abs(line[0].Box.CenterY - run.Box.CenterY) < 1.5)
                    line.Add(run);
                else
                    lines.Add([run]);
            }
            return lines.Select(l => string.Join(" ", l.OrderBy(r => r.Box.Left).Select(r => r.Text))).ToList();
        }

        static CellText? ParseText(List<string> lines)
        {
            foreach (var rx in new[] { RoomAtEnd, RoomInMiddle, NoRoom })
                for (int i = 0; i < lines.Count; i++)
                {
                    var m = rx.Match(lines[i]);
                    if (!m.Success) continue;

                    string name = m.Groups["name"].Success ? m.Groups["name"].Value : "Nieznane";

                    string? abbr = m.Groups["abbr"].Success ? m.Groups["abbr"].Value : null;

                    string? room = m.Groups["room"].Success ? m.Groups["room"].Value : null;

                    string? teacher = m.Groups["rest"].Success ? string.Join(" ", lines.Where((_, j) => j != i)) : null;


                    return new CellText(name, abbr, ClassType(m.Groups["type"].Value, name, room), room, teacher);
                }

            return null;
        }

        static string ClassType(string code, string name, string? room) => code switch
        {
            "Wykład" or "W" => "Wykład",
            "Ćwiczenia" or "Ćw" => "Ćwiczenia",
            "Ps" or "Prac. spec." => "Pracownia specjalistyczna",
            "L" or "Lab" => "Laboratorium",
            "P" => "Projekt",
            "" when name.StartsWith("Sem") => "Seminarium",
            "" when name.StartsWith("Wychowanie fizyczne", StringComparison.OrdinalIgnoreCase) => "Wychowanie fizyczne",
            "" when room?.StartsWith("CNK") == true => "Lektorat",
            "" => "Inne",
            var other => other,
        };
        const string Name = @"^(?<name>.+?)(?:\s+\((?<abbr>[^)]*\p{Lu}[^)]*)\))?";
        const string Type = @"(?<type>Wykład|Ćwiczenia|Prac\.\s?spec\.|Ćw|Ps|Lab|W|L|P)";
        const string Room = @"(?<room>CNK\s+\S+|Hala\s+\S+|\S*\d\S*)";

        static readonly Regex RoomAtEnd = new(Name + @"(?:\s+" + Type + @")?\s+" + Room + "$");
        static readonly Regex RoomInMiddle = new(Name + @"\s+" + Type + @"\s+" + Room + @"\s+(?<rest>.+)$");
        static readonly Regex NoRoom = new(Name + @"\s+" + Type + "$");
    }
}
