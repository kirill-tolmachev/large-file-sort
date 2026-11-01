using Common;
using Microsoft.Win32.SafeHandles;

namespace Sorter;

sealed class SectionReader(SafeFileHandle file, long start, long end, byte[] buffer)
{
    long _position = start;
    int _lineStart, _lineLength, _dataEnd, _textOffset, _textLength;

    public ulong Prefix { get; private set; }
    public long Number { get; private set; }
    public ReadOnlySpan<byte> Line => buffer.AsSpan(_lineStart, _lineLength);
    public ReadOnlySpan<byte> Text => buffer.AsSpan(_lineStart + _textOffset, _textLength);

    ReadOnlySpan<byte> Unread => buffer.AsSpan(_lineStart, _dataEnd - _lineStart);

    public bool MoveNext()
    {
        _lineStart += _lineLength;
        int newline;
        while ((newline = Unread.IndexOf((byte)'\n')) < 0)
        {
            if (_position == end)
                return false;
            Unread.CopyTo(buffer);
            _dataEnd -= _lineStart;
            _lineStart = 0;
            if (_dataEnd == buffer.Length)
                Array.Resize(ref buffer, buffer.Length * 2);
            int read = RandomAccess.Read(file, buffer.AsSpan(_dataEnd, (int)Math.Min(buffer.Length - _dataEnd, end - _position)), _position);
            if (read == 0)
                throw new EndOfStreamException();
            _position += read;
            _dataEnd += read;
        }
        _lineLength = newline + 1;
        if (!LineFormat.TryParse(buffer.AsSpan(_lineStart, newline), out long number, out var text))
            throw new InvalidDataException("A run file is corrupted");
        Number = number;
        (_textOffset, _textLength) = text.GetOffsetAndLength(newline);
        Prefix = LineFormat.Prefix(Text);
        return true;
    }
}
