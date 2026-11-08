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
                // pionowe napisy (nazwy dni) pomijamy – dni bierzemy z kolejności bloków godzin
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
                .SelectMany(sub => sub.Commands)             // z podścieżek robimy płaską listę komend
                .SelectMany(cmd => cmd switch
                {
                    PdfSubpath.Move m => new[] { m.Location },
                    PdfSubpath.Line l => new[] { l.From, l.To },
                    _ => []
                })
                .Select(p => (X: Math.Round(p.X, 1), Y: Math.Round(p.Y, 1)))   // krotka (X, Y)
                .Distinct()
                .ToList();

            if (points.Count != 3) return (false, false);

            // W PDF-ie Y rośnie w górę: lewy górny róg = najmniejsze X i największe Y
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
    }
}
