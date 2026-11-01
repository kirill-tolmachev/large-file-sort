using Common;

namespace Sorter;

readonly record struct LineRef(ulong Prefix, long Number, int Start, int Length, int TextOffset, int TextLength);

readonly struct LineRefComparer(byte[] data) : IComparer<LineRef>
{
    public int Compare(LineRef a, LineRef b) =>
        LineFormat.Compare(a.Prefix, Text(a), a.Number, b.Prefix, Text(b), b.Number);

    ReadOnlySpan<byte> Text(in LineRef line) => data.AsSpan(line.Start + line.TextOffset, line.TextLength);
}
