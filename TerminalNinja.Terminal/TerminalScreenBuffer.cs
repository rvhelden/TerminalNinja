using System.Text;
using TerminalNinja.Primitives;

namespace TerminalNinja.Terminal;

/// <summary>
/// Maintains the visible state of a terminal as a <see cref="Cell"/> grid plus a cursor
/// position and the current SGR (Select Graphic Rendition) attributes. Implements
/// <see cref="IVtParserHandler"/> — feed it parser events and it tracks the resulting
/// screen state, ready for a <c>TerminalView</c> control to render.
/// </summary>
/// <remarks>
/// <para>
/// MVP scope: printable text with line wrap, the common C0 controls (BS, HT, LF, CR),
/// CSI cursor moves (H, A, B, C, D, G, d), erase (J, K), SGR colors / styles
/// (basic 30-37 / 40-47, bright 90-97 / 100-107, 256-color, truecolor, reset, individual
/// attribute on/off), and OSC 0/1/2 window title.
/// </para>
/// <para>
/// Deliberately out of scope for this commit (sequential commits build them up):
/// scrolling regions (DECSTBM), insert/delete line (L, M), alternate screen buffer
/// (?47 / ?1047 / ?1049), save/restore cursor, configurable tab stops, wide-character
/// advance (East Asian Width), DCS / Sixel.
/// </para>
/// <para>
/// Threading: not thread-safe. Single producer (the parser feeding events) on one thread.
/// </para>
/// </remarks>
public sealed class TerminalScreenBuffer : IVtParserHandler
{
    private Cell[] _cells;
    private int _rows;
    private int _cols;
    private int _cursorRow;
    private int _cursorCol;
    private Color _currentFg;
    private Color _currentBg;
    private TextDecorations _currentDeco;

    /// <summary>Cells wide.</summary>
    public int Cols => _cols;

    /// <summary>Cells tall.</summary>
    public int Rows => _rows;

    /// <summary>Cursor row (0-based, 0 = top).</summary>
    public int CursorRow => _cursorRow;

    /// <summary>Cursor column (0-based, 0 = leftmost).</summary>
    public int CursorCol => _cursorCol;

    /// <summary>Visible cursor flag (DEC private mode 25). Default true.</summary>
    public bool CursorVisible { get; private set; } = true;

    /// <summary>Window title set via OSC 0 / 1 / 2. Empty string until the shell sets one.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Raised when <see cref="Title"/> changes (OSC 0/1/2).</summary>
    public event Action<string>? TitleChanged;

    /// <summary>Raised when a BEL (0x07) is executed. Hosts can flash, beep, or ignore.</summary>
    public event Action? BellRang;

    /// <summary>
    /// Creates a screen buffer of the given size, filled with <see cref="Cell.Empty"/>.
    /// </summary>
    public TerminalScreenBuffer(int rows, int cols)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cols);
        _rows = rows;
        _cols = cols;
        _cells = new Cell[rows * cols];
        SetDefaults();
        ClearAll();
    }

    /// <summary>Reads a single cell. Throws if (row, col) is out of bounds.</summary>
    public Cell GetCell(int row, int col)
    {
        if ((uint)row >= (uint)_rows || (uint)col >= (uint)_cols)
        {
            throw new ArgumentOutOfRangeException($"({row}, {col}) is outside the {_rows}x{_cols} buffer.");
        }

        return _cells[row * _cols + col];
    }

    /// <summary>Reads a row as a span. Cheaper than calling <see cref="GetCell"/> in a loop.</summary>
    public ReadOnlySpan<Cell> GetRow(int row)
    {
        if ((uint)row >= (uint)_rows)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        return new ReadOnlySpan<Cell>(_cells, row * _cols, _cols);
    }

    /// <summary>
    /// Resizes the buffer to <paramref name="newRows"/> × <paramref name="newCols"/>.
    /// Existing cells in the overlapping region are preserved. The cursor is clamped
    /// into the new bounds.
    /// </summary>
    public void Resize(int newRows, int newCols)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newRows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newCols);

        if (newRows == _rows && newCols == _cols)
        {
            return;
        }

        var newCells = new Cell[newRows * newCols];
        Array.Fill(newCells, Cell.Empty);

        var copyRows = Math.Min(_rows, newRows);
        var copyCols = Math.Min(_cols, newCols);
        for (var r = 0; r < copyRows; r++)
        {
            for (var c = 0; c < copyCols; c++)
            {
                newCells[r * newCols + c] = _cells[r * _cols + c];
            }
        }

        _cells = newCells;
        _rows = newRows;
        _cols = newCols;
        _cursorRow = Math.Min(_cursorRow, _rows - 1);
        _cursorCol = Math.Min(_cursorCol, _cols - 1);
    }

    // ─── IVtParserHandler ────────────────────────────────────────────────

    /// <inheritdoc />
    public void OnPrint(uint codepoint)
    {
        if (_cursorCol >= _cols)
        {
            // Autowrap: move to the start of the next row, scrolling if needed.
            _cursorCol = 0;
            LineFeed();
        }

        var idx = _cursorRow * _cols + _cursorCol;
        _cells[idx] = new Cell(codepoint, _currentFg, _currentBg, _currentDeco);
        _cursorCol++;
    }

    /// <inheritdoc />
    public void OnExecute(byte controlByte)
    {
        switch (controlByte)
        {
            case 0x07: // BEL
                BellRang?.Invoke();
                break;
            case 0x08: // BS
                if (_cursorCol > 0) _cursorCol--;
                break;
            case 0x09: // HT — advance to the next 8-column tab stop
                _cursorCol = Math.Min(_cols - 1, (_cursorCol / 8 + 1) * 8);
                break;
            case 0x0A: // LF
            case 0x0B: // VT (treated as LF)
            case 0x0C: // FF (treated as LF)
                LineFeed();
                break;
            case 0x0D: // CR
                _cursorCol = 0;
                break;
                // Other C0 controls (SO, SI, ENQ, etc.) ignored in MVP.
        }
    }

    /// <inheritdoc />
    public void OnCsiDispatch(byte finalByte, ReadOnlySpan<int> parameters, ReadOnlySpan<byte> intermediates, bool isPrivate)
    {
        // Helper: read parameter at index with a default value.
        static int P(ReadOnlySpan<int> p, int index, int @default)
        {
            if (index >= p.Length) return @default;
            var v = p[index];
            return v < 0 ? @default : v;
        }

        switch (finalByte)
        {
            case (byte)'H': // CUP
            case (byte)'f': // HVP
                MoveCursor(P(parameters, 0, 1) - 1, P(parameters, 1, 1) - 1);
                break;
            case (byte)'A': // CUU
                MoveCursor(_cursorRow - P(parameters, 0, 1), _cursorCol);
                break;
            case (byte)'B': // CUD
                MoveCursor(_cursorRow + P(parameters, 0, 1), _cursorCol);
                break;
            case (byte)'C': // CUF
                MoveCursor(_cursorRow, _cursorCol + P(parameters, 0, 1));
                break;
            case (byte)'D': // CUB
                MoveCursor(_cursorRow, _cursorCol - P(parameters, 0, 1));
                break;
            case (byte)'G': // CHA — column only
                MoveCursor(_cursorRow, P(parameters, 0, 1) - 1);
                break;
            case (byte)'d': // VPA — row only
                MoveCursor(P(parameters, 0, 1) - 1, _cursorCol);
                break;
            case (byte)'J': // ED
                EraseDisplay(P(parameters, 0, 0));
                break;
            case (byte)'K': // EL
                EraseLine(P(parameters, 0, 0));
                break;
            case (byte)'m':
                ApplySgr(parameters);
                break;
            case (byte)'h': // SM / DECSET
                if (isPrivate) ApplyDecPrivateMode(parameters, set: true);
                break;
            case (byte)'l': // RM / DECRST
                if (isPrivate) ApplyDecPrivateMode(parameters, set: false);
                break;
                // Unhandled CSI finals: ignored in MVP. Follow-up commits add r (DECSTBM),
                // S/T (scroll), L/M (insert/delete line), s/u (save/restore), etc.
        }

        _ = intermediates; // not used in the MVP
    }

    /// <inheritdoc />
    public void OnEscDispatch(byte finalByte, ReadOnlySpan<byte> intermediates)
    {
        switch (finalByte)
        {
            case (byte)'c': // RIS — hard reset
                Reset();
                break;
                // Other ESC dispatches (charset, IND, RI, etc.) deferred.
        }

        _ = intermediates;
    }

    /// <inheritdoc />
    public void OnOscDispatch(int command, ReadOnlySpan<byte> data)
    {
        switch (command)
        {
            case 0: // ICON name + window title
            case 1: // ICON name
            case 2: // window title
            {
                var title = Encoding.UTF8.GetString(data);
                if (!string.Equals(title, Title, StringComparison.Ordinal))
                {
                    Title = title;
                    TitleChanged?.Invoke(title);
                }

                break;
            }
                // OSC 8 (hyperlinks), 52 (clipboard), 4 (palette set), etc. — deferred.
        }
    }

    // ─── Internals ───────────────────────────────────────────────────────

    private void SetDefaults()
    {
        _currentFg = Color.White;
        _currentBg = Color.Black;
        _currentDeco = TextDecorations.None;
    }

    private void ClearAll() => Array.Fill(_cells, Cell.Empty);

    /// <summary>Hard reset — clear cells, reset cursor, reset SGR, reset title.</summary>
    public void Reset()
    {
        SetDefaults();
        ClearAll();
        _cursorRow = 0;
        _cursorCol = 0;
        CursorVisible = true;
        if (!string.IsNullOrEmpty(Title))
        {
            Title = string.Empty;
            TitleChanged?.Invoke(Title);
        }
    }

    private void MoveCursor(int row, int col)
    {
        _cursorRow = Math.Clamp(row, 0, _rows - 1);
        _cursorCol = Math.Clamp(col, 0, _cols - 1);
    }

    private void LineFeed()
    {
        if (_cursorRow + 1 < _rows)
        {
            _cursorRow++;
            return;
        }

        // Scroll the buffer up by one row. The vacated bottom row is filled with empty cells.
        var span = _cells.AsSpan();
        span[_cols..].CopyTo(span[..^_cols]);
        span[^_cols..].Fill(Cell.Empty);
    }

    private void EraseDisplay(int mode)
    {
        switch (mode)
        {
            case 0: // cursor to end-of-screen
                EraseRange(_cursorRow, _cursorCol, _rows - 1, _cols - 1);
                break;
            case 1: // start-of-screen to cursor
                EraseRange(0, 0, _cursorRow, _cursorCol);
                break;
            case 2: // entire screen
            case 3: // entire screen + scrollback (we have no scrollback)
                ClearAll();
                break;
        }
    }

    private void EraseLine(int mode)
    {
        switch (mode)
        {
            case 0: // cursor to end-of-line
                EraseRange(_cursorRow, _cursorCol, _cursorRow, _cols - 1);
                break;
            case 1: // start-of-line to cursor
                EraseRange(_cursorRow, 0, _cursorRow, _cursorCol);
                break;
            case 2: // entire line
                EraseRange(_cursorRow, 0, _cursorRow, _cols - 1);
                break;
        }
    }

    private void EraseRange(int startRow, int startCol, int endRow, int endCol)
    {
        for (var r = startRow; r <= endRow; r++)
        {
            var cStart = r == startRow ? startCol : 0;
            var cEnd = r == endRow ? endCol : _cols - 1;
            for (var c = cStart; c <= cEnd; c++)
            {
                _cells[r * _cols + c] = Cell.Empty;
            }
        }
    }

    private void ApplyDecPrivateMode(ReadOnlySpan<int> parameters, bool set)
    {
        foreach (var p in parameters)
        {
            switch (p)
            {
                case 25:
                    CursorVisible = set;
                    break;
                    // ?1, ?7, ?47, ?1047, ?1049, ?1000-series — deferred.
            }
        }
    }

    private void ApplySgr(ReadOnlySpan<int> parameters)
    {
        if (parameters.Length == 0)
        {
            // "ESC [ m" with no args = reset.
            SetDefaults();
            return;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (p < 0) p = 0; // missing param defaults to 0 (reset) in SGR

            switch (p)
            {
                case 0: SetDefaults(); break;

                case 1: _currentDeco |= TextDecorations.Bold; break;
                case 2: _currentDeco |= TextDecorations.Dim; break;
                case 3: _currentDeco |= TextDecorations.Italic; break;
                case 4: _currentDeco |= TextDecorations.Underline; break;
                case 5: _currentDeco |= TextDecorations.Blink; break;
                case 7: _currentDeco |= TextDecorations.Inverse; break;
                case 9: _currentDeco |= TextDecorations.Strikethrough; break;

                case 22: _currentDeco &= ~(TextDecorations.Bold | TextDecorations.Dim); break;
                case 23: _currentDeco &= ~TextDecorations.Italic; break;
                case 24: _currentDeco &= ~TextDecorations.Underline; break;
                case 25: _currentDeco &= ~TextDecorations.Blink; break;
                case 27: _currentDeco &= ~TextDecorations.Inverse; break;
                case 29: _currentDeco &= ~TextDecorations.Strikethrough; break;

                case >= 30 and <= 37:
                    _currentFg = BasicColor(p - 30, bright: false);
                    break;
                case 38:
                    if (TryReadExtendedColor(parameters, ref i, out var fgEx))
                    {
                        _currentFg = fgEx;
                    }
                    break;
                case 39:
                    _currentFg = Color.White;
                    break;

                case >= 40 and <= 47:
                    _currentBg = BasicColor(p - 40, bright: false);
                    break;
                case 48:
                    if (TryReadExtendedColor(parameters, ref i, out var bgEx))
                    {
                        _currentBg = bgEx;
                    }
                    break;
                case 49:
                    _currentBg = Color.Black;
                    break;

                case >= 90 and <= 97:
                    _currentFg = BasicColor(p - 90, bright: true);
                    break;
                case >= 100 and <= 107:
                    _currentBg = BasicColor(p - 100, bright: true);
                    break;
            }
        }
    }

    /// <summary>
    /// Reads the extended color form following 38 or 48: <c>;5;n</c> (256-color palette)
    /// or <c>;2;r;g;b</c> (truecolor). Advances <paramref name="i"/> past the consumed
    /// parameters; returns false if the form is malformed.
    /// </summary>
    private static bool TryReadExtendedColor(ReadOnlySpan<int> parameters, ref int i, out Color color)
    {
        color = default;
        if (i + 1 >= parameters.Length) return false;
        var form = parameters[i + 1];

        if (form == 5 && i + 2 < parameters.Length)
        {
            var palette = Math.Clamp(parameters[i + 2], 0, 255);
            color = Palette256(palette);
            i += 2;
            return true;
        }

        if (form == 2 && i + 4 < parameters.Length)
        {
            var r = Math.Clamp(parameters[i + 2], 0, 255);
            var g = Math.Clamp(parameters[i + 3], 0, 255);
            var b = Math.Clamp(parameters[i + 4], 0, 255);
            color = new Color((byte)r, (byte)g, (byte)b);
            i += 4;
            return true;
        }

        return false;
    }

    private static Color BasicColor(int index, bool bright)
    {
        // Standard 8 / bright 8 palette. Values chosen to match VT100 / xterm conventions.
        return (index, bright) switch
        {
            (0, false) => new Color(0x00, 0x00, 0x00), // black
            (1, false) => new Color(0xCD, 0x00, 0x00), // red
            (2, false) => new Color(0x00, 0xCD, 0x00), // green
            (3, false) => new Color(0xCD, 0xCD, 0x00), // yellow
            (4, false) => new Color(0x00, 0x00, 0xEE), // blue
            (5, false) => new Color(0xCD, 0x00, 0xCD), // magenta
            (6, false) => new Color(0x00, 0xCD, 0xCD), // cyan
            (7, false) => new Color(0xE5, 0xE5, 0xE5), // white (light grey)
            (0, true) => new Color(0x7F, 0x7F, 0x7F), // bright black (grey)
            (1, true) => new Color(0xFF, 0x00, 0x00),
            (2, true) => new Color(0x00, 0xFF, 0x00),
            (3, true) => new Color(0xFF, 0xFF, 0x00),
            (4, true) => new Color(0x5C, 0x5C, 0xFF),
            (5, true) => new Color(0xFF, 0x00, 0xFF),
            (6, true) => new Color(0x00, 0xFF, 0xFF),
            (7, true) => new Color(0xFF, 0xFF, 0xFF),
            _ => Color.White,
        };
    }

    private static Color Palette256(int n)
    {
        // 0-15: basic + bright (same as the 30-37 / 90-97 SGR codes)
        if (n < 8) return BasicColor(n, bright: false);
        if (n < 16) return BasicColor(n - 8, bright: true);

        // 16-231: 6x6x6 RGB cube. Each component maps n ∈ [0..5] → [0, 95, 135, 175, 215, 255].
        if (n < 232)
        {
            var idx = n - 16;
            var r = idx / 36;
            var g = (idx / 6) % 6;
            var b = idx % 6;
            return new Color(CubeStep(r), CubeStep(g), CubeStep(b));
        }

        // 232-255: 24-step grayscale.
        var level = (byte)(8 + (n - 232) * 10);
        return new Color(level, level, level);
    }

    private static byte CubeStep(int component) => component switch
    {
        0 => 0,
        1 => 95,
        2 => 135,
        3 => 175,
        4 => 215,
        _ => 255,
    };
}
