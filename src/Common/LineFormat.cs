using System.Buffers.Binary;
using System.Buffers.Text;

namespace Common;

public static class LineFormat
{
    const int PrefixLength = sizeof(ulong);

    public static bool TryParse(ReadOnlySpan<byte> line, out long number, out Range text)
    {
        if (Utf8Parser.TryParse(line, out number, out int consumed) && line[consumed..].StartsWith(". "u8))
        {
            int end = line[^1] == (byte)'\r' ? line.Length - 1 : line.Length;
            text = (consumed + 2)..end;
            return true;
        }
        text = default;
        return false;
    }

    // The first eight text bytes, packed big-endian, compare exactly like the bytes themselves,
    // so most comparisons are decided without touching the line's memory.
    public static ulong Prefix(ReadOnlySpan<byte> text)
    {
        if (text.Length >= PrefixLength)
            return BinaryPrimitives.ReadUInt64BigEndian(text);
        ulong prefix = 0;
        foreach (byte b in text)
            prefix = prefix << 8 | b;
        return prefix << 8 * (PrefixLength - text.Length);
    }

    public static int Compare(
        ulong prefixA, ReadOnlySpan<byte> textA, long numberA,
        ulong prefixB, ReadOnlySpan<byte> textB, long numberB)
    {
        if (prefixA != prefixB)
            return prefixA < prefixB ? -1 : 1;
        int order = textA.SequenceCompareTo(textB);
        return order != 0 ? order : numberA.CompareTo(numberB);
    }
}
