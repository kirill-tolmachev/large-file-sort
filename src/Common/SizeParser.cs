using System.Globalization;

namespace Common;

public static class SizeParser
{
    public static long Parse(string text)
    {
        var size = text.Trim().ToUpperInvariant();
        size = size.EndsWith('B') ? size[..^1] : size;
        int unit = size.Length > 0 ? "KMGT".IndexOf(size[^1]) : -1;
        var digits = unit < 0 ? size : size[..^1];
        double bytes = double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value * Math.Pow(1024, unit + 1)
            : double.NaN;
        return bytes >= 0 && bytes < long.MaxValue ? (long)bytes : throw new FormatException($"'{text}' is not a valid size");
    }
}
