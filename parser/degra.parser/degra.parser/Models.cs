using System;
using System.Collections.Generic;
using System.Text;

namespace degra.parser
{
    public record Schedule(
        string? Title,
        string? UpdatedAt,
        List<TimePeriod> TimeSlots,  // pełna siatka godzin, także tych bez zajęć
        List<GroupNode> Groups,      // wszystkie grupy razem z hierarchią (W → Ps → Ćw)
        List<ClassEntry> Entries,
        List<string> Warnings);

    /// <summary>
    /// Jedna "para" zajęć, np. 08:30–10:00, złożona z dwóch jednostek 45-minutowych.
    /// Number to kolejny numer bloku w ciągu dnia (1 = 08:30).
    /// </summary>
    public record TimePeriod(int Number, string Start, string End, List<TimeRange> Units);

    public record TimeRange(string Start, string End);

    /// <summary>
    /// Grupa wraz z miejscem w hierarchii. Parent/Children służą tylko do podpowiedzi:
    /// "W 1" ma dzieci "Ps 1".."Ps 3", a "Ps 1" ma dzieci "Ćw 1", "Ćw 2".
    /// </summary>
    public record GroupNode(string Name, string Category, string? Parent, List<string> Children);

    /// <summary>Jedne zajęcia (jeden kolorowy blok w tabeli).</summary>
    public record ClassEntry(
        string Day,
        string Start,
        string End,
        string Subject,
        string? SubjectShort,
        string Type,
        string? Room,
        string? Teacher,
        string? Group,             // grupa, której dotyczą zajęcia: "W 1" dla wykładu, "Ps 4" dla pracowni; null dla lektoratów
        int? Part);                // 1/2 gdy komórka jest przecięta po przekątnej, inaczej null

    // ---- typy pomocnicze używane w trakcie parsowania (współrzędne: y rośnie w dół) ----

    internal record Box(double Left, double Top, double Right, double Bottom)
    {
        public double Width => Right - Left;
        public double Height => Bottom - Top;
        public double CenterX => (Left + Right) / 2;
        public double CenterY => (Top + Bottom) / 2;
        public bool Contains(double x, double y, double tol = 0.5) =>
            x >= Left - tol && x <= Right + tol && y >= Top - tol && y <= Bottom + tol;
    }

    /// <summary>Ciągły kawałek tekstu narysowany jednym poleceniem w PDF-ie.</summary>
    internal record TextRun(string Text, Box Box);

    internal record TimeSlot(int Day, string Start, string End, Box Box);

    internal record HeaderColumn(string Label, string Category, Box Box);

    internal class Block(Box box, bool isSplit)
    {
        public Box Box { get; } = box;
        public bool IsSplit { get; } = isSplit;
        public List<TextRun>[] Parts { get; } = isSplit ? [[], []] : [[]];

        public override string ToString() => $"Block(IsSplit={IsSplit}, Parts=[{string.Join(", ", Parts[0].Select(p => p.Text))}])";
    }
}
