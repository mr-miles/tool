namespace DotnetDiffCoverage.Models;

/// <summary>An inclusive range of consecutive line numbers.</summary>
public sealed record LineRange(int Start, int End)
{
    public int Count => End - Start + 1;
    public bool Contains(int line) => line >= Start && line <= End;
    public IEnumerable<int> Lines => Enumerable.Range(Start, Count);
}
