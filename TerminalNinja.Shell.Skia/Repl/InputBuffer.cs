using System.Text;

namespace TerminalNinja.Shell.Skia;

/// <summary>
/// The multi-line input buffer for <see cref="ReplView"/>: a <see cref="StringBuilder"/>
/// plus a linear UTF-16 cursor offset, with line/column math layered on top. The cursor
/// is a single linear index — multi-line buffers store <c>\n</c> inline; row/column on
/// screen is derived through <see cref="CursorToLineCol"/>.
/// </summary>
internal sealed class InputBuffer
{
    private readonly StringBuilder _text = new();

    /// <summary>Linear UTF-16 cursor offset into <see cref="Text"/>.</summary>
    public int CursorCol { get; set; }

    public int Length => _text.Length;

    public string Text => _text.ToString();

    public char this[int index] => _text[index];

    public bool IsEmpty => _text.Length == 0;

    public void Clear()
    {
        _text.Clear();
        CursorCol = 0;
    }

    public void Replace(string value)
    {
        _text.Clear();
        _text.Append(value);
        CursorCol = _text.Length;
    }

    public void Insert(int index, char value) => _text.Insert(index, value);

    public void Insert(int index, string value) => _text.Insert(index, value);

    public void Remove(int index, int length) => _text.Remove(index, length);

    /// <summary>Number of logical rows the buffer occupies (1 + count of '\n').</summary>
    public int CountLines()
    {
        var n = 1;
        for (var i = 0; i < _text.Length; i++)
        {
            if (_text[i] == '\n') n++;
        }
        return n;
    }

    /// <summary>Buffer split on <c>\n</c>, indexed by input row.</summary>
    public IReadOnlyList<string> Lines() => _text.ToString().Split('\n');

    /// <summary>
    /// Maps a linear cursor index into (line, column). Counts '\n' characters;
    /// the column resets to 0 after each. Indices past end-of-input clamp to the
    /// last line's last column.
    /// </summary>
    public (int Line, int Col) CursorToLineCol(int index)
    {
        var line = 0;
        var lineStart = 0;
        var clamped = Math.Clamp(index, 0, _text.Length);
        for (var i = 0; i < clamped; i++)
        {
            if (_text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }
        return (line, clamped - lineStart);
    }

    /// <summary>
    /// Reverse of <see cref="CursorToLineCol"/>: convert a (line, col) target to a
    /// linear index, clamping the column to the actual length of <paramref name="line"/>.
    /// </summary>
    public int LineColToIndex(int line, int col)
    {
        if (line < 0) return 0;
        var i = 0;
        var currentLine = 0;
        while (currentLine < line && i < _text.Length)
        {
            if (_text[i] == '\n') currentLine++;
            i++;
        }
        if (currentLine < line)
        {
            return _text.Length;
        }
        var lineStart = i;
        var lineEnd = lineStart;
        while (lineEnd < _text.Length && _text[lineEnd] != '\n') lineEnd++;
        return lineStart + Math.Min(col, lineEnd - lineStart);
    }

    /// <summary>Walk left from <paramref name="cursor"/> while the previous char is an identifier char.</summary>
    public static int FindWordStart(string text, int cursor)
    {
        var s = cursor;
        while (s > 0 && IsIdentifierChar(text[s - 1])) s--;
        return s;
    }

    public static bool IsIdentifierChar(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
}
