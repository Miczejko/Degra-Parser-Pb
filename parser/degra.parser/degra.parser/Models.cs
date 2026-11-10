using System;
using System.Collections.Generic;
using System.Text;

namespace degra.parser
{
    public record Schedule(
        string? Title,
        string? UpdatedAt,
        List<TimePeriod> TimeSlots, 
        List<GroupNode> Groups,      
        List<ClassEntry> Entries,
        List<string> Warnings);


    public record TimePeriod(int Number, string Start, string End, List<TimeRange> Units);

    public record TimeRange(string Start, string End);

    public record GroupNode(string Name, string Category, string? Parent, List<string> Children);

    public record ClassEntry(
        string Day,
        string Start,
        string End,
        string Subject,
        string? SubjectShort,
        string Type,
        string? Room,
        string? Teacher,
        string? Group,             
        int? Part);                


    internal record Box(double Left, double Top, double Right, double Bottom)
    {
        public double Width => Right - Left;
        public double Height => Bottom - Top;
        public double CenterX => (Left + Right) / 2;
        public double CenterY => (Top + Bottom) / 2;
        public bool Contains(double x, double y, double tol = 0.5) =>
            x >= Left - tol && x <= Right + tol && y >= Top - tol && y <= Bottom + tol;
    }

    internal record TextRun(string Text, Box Box);

    internal record TimeSlot(int Day, string Start, string End, Box Box);

    internal record HeaderColumn(string Label, string Category, Box Box);

    internal record CellText(string Subject, string? Short, string Type, string? Room, string? Teacher);

    internal class Block(Box box, bool isSplit)
    {
        public Box Box { get; } = box;
        public bool IsSplit { get; } = isSplit;
        public List<TextRun>[] Parts { get; } = isSplit ? [[], []] : [[]];

        public override string ToString() => $"Block(IsSplit={IsSplit}, Parts=[{string.Join(", ", Parts[0].Select(p => p.Text))}])";
    }
}
