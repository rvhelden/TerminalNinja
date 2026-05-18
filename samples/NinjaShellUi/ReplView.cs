using System.Text;
using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace NinjaShellUi;

/// <summary>
/// A minimal terminal-style REPL surface: a scrolling output buffer at the top and a
/// single-line input prompt at the bottom. Owns its own input state (no <c>TextBox</c>),
/// so <c>Enter</c>, history navigation, and command execution all funnel through one
/// keyboard handler.
/// </summary>
/// <remarks>
/// <para>
/// The view is host-agnostic — it writes cells through the standard <see cref="CellBuffer"/>
/// contract and therefore renders identically in the console and in the Skia GUI host. The
/// containing view model subscribes to <see cref="CommandEntered"/> to evaluate the input
/// line; once evaluation completes the host calls <see cref="AppendOutput"/> with the result.
/// </para>
/// <para>
/// History (up / down arrows) is in scope for the MVP — multi-line input, auto-completion,
/// and search are deferred to follow-ups.
/// </para>
/// </remarks>
public sealed class ReplView : Control
{
    private readonly List<string> _outputLines = new(capacity: 256);
    private readonly StringBuilder _input = new();
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private int _cursorCol;
    private int _scrollOffset;

    /// <summary>The prompt rendered in front of the input line. Defaults to <c>"ninja&gt; "</c>.</summary>
    public string Prompt { get; set; } = "ninja> ";

    /// <summary>The current contents of the input buffer (without the prompt).</summary>
    public string InputBuffer => _input.ToString();

    /// <summary>Raised when the user presses Enter on a non-empty input line.</summary>
    public event Action<string>? CommandEntered;

    /// <summary>Creates a focusable REPL view.</summary>
    public ReplView()
    {
        Focusable = true;
    }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect availableSpace) => new(availableSpace.Width, availableSpace.Height);

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parentBounds) => parentBounds;

    /// <summary>Appends <paramref name="text"/> to the output buffer, splitting on newlines.</summary>
    public void AppendOutput(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        foreach (var line in text.Split('\n'))
        {
            _outputLines.Add(line.TrimEnd('\r'));
        }
        ScrollToBottom();
        InvalidationCallback?.Invoke();
    }

    /// <summary>Removes every line from the output buffer.</summary>
    public void ClearOutput()
    {
        _outputLines.Clear();
        _scrollOffset = 0;
        InvalidationCallback?.Invoke();
    }

    /// <inheritdoc />
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Reserve the bottom row for the input prompt; everything above is output.
        var outputHeight = Math.Max(0, bounds.Height - 1);
        var promptY = bounds.Y + bounds.Height - 1;

        var fg = Foreground;
        var bg = Background;
        ClearRegion(buffer, bounds, bg);
        RenderOutput(buffer, bounds.X, bounds.Y, bounds.Width, outputHeight, fg, bg);
        RenderPromptLine(buffer, bounds.X, promptY, bounds.Width, fg, bg);
    }

    private void RenderOutput(CellBuffer buffer, int x, int y, int width, int height, Color fg, Color bg)
    {
        if (height <= 0) return;
        var firstLine = Math.Max(0, _outputLines.Count - height - _scrollOffset);
        var lastLine = Math.Min(_outputLines.Count, firstLine + height);

        for (var i = firstLine; i < lastLine; i++)
        {
            var row = y + (i - firstLine);
            DrawText(buffer, x, row, _outputLines[i], width, fg, bg);
        }
    }

    private void RenderPromptLine(CellBuffer buffer, int x, int y, int width, Color fg, Color bg)
    {
        DrawText(buffer, x, y, Prompt, width, fg, bg);
        var inputX = x + Prompt.Length;
        var inputWidth = Math.Max(0, width - Prompt.Length);
        DrawText(buffer, inputX, y, _input.ToString(), inputWidth, fg, bg);

        // Cursor: invert fg/bg on the cell at the cursor position.
        var cursorX = inputX + Math.Min(_cursorCol, inputWidth - 1);
        if (cursorX >= inputX && cursorX < inputX + inputWidth && (uint)y < (uint)buffer.Height)
        {
            var cell = buffer.GetCell(cursorX, y);
            buffer.SetCell(cursorX, y, new Cell(cell.Codepoint, cell.Background, cell.Foreground, cell.Decorations, cell.Flags));
        }
    }

    private static void ClearRegion(CellBuffer buffer, Rect bounds, Color bg)
    {
        for (var row = 0; row < bounds.Height; row++)
        {
            var y = bounds.Y + row;
            if ((uint)y >= (uint)buffer.Height) continue;
            for (var col = 0; col < bounds.Width; col++)
            {
                var bx = bounds.X + col;
                if ((uint)bx >= (uint)buffer.Width) continue;
                buffer.SetCell(bx, y, new Cell(' ', Color.White, bg));
            }
        }
    }

    private static void DrawText(CellBuffer buffer, int x, int y, string text, int maxWidth, Color fg, Color bg)
    {
        if ((uint)y >= (uint)buffer.Height) return;
        for (var i = 0; i < text.Length && i < maxWidth; i++)
        {
            var cx = x + i;
            if ((uint)cx >= (uint)buffer.Width) break;
            buffer.SetChar(cx, y, text[i], fg, bg);
        }
    }

    private void ScrollToBottom() => _scrollOffset = 0;

    /// <inheritdoc />
    public override void OnKeyEvent(KeyEvent e)
    {
        switch (e.Key)
        {
            case ConsoleKey.Enter:
                Submit();
                return;
            case ConsoleKey.Backspace:
                if (_cursorCol > 0)
                {
                    _input.Remove(_cursorCol - 1, 1);
                    _cursorCol--;
                    InvalidationCallback?.Invoke();
                }
                return;
            case ConsoleKey.Delete:
                if (_cursorCol < _input.Length)
                {
                    _input.Remove(_cursorCol, 1);
                    InvalidationCallback?.Invoke();
                }
                return;
            case ConsoleKey.LeftArrow:
                if (_cursorCol > 0) { _cursorCol--; InvalidationCallback?.Invoke(); }
                return;
            case ConsoleKey.RightArrow:
                if (_cursorCol < _input.Length) { _cursorCol++; InvalidationCallback?.Invoke(); }
                return;
            case ConsoleKey.Home:
                _cursorCol = 0; InvalidationCallback?.Invoke();
                return;
            case ConsoleKey.End:
                _cursorCol = _input.Length; InvalidationCallback?.Invoke();
                return;
            case ConsoleKey.UpArrow:
                NavigateHistory(-1);
                return;
            case ConsoleKey.DownArrow:
                NavigateHistory(+1);
                return;
            case ConsoleKey.PageUp:
                _scrollOffset = Math.Min(_scrollOffset + 5, Math.Max(0, _outputLines.Count - 1));
                InvalidationCallback?.Invoke();
                return;
            case ConsoleKey.PageDown:
                _scrollOffset = Math.Max(0, _scrollOffset - 5);
                InvalidationCallback?.Invoke();
                return;
        }

        // Printable text input: SDL3 TEXT_INPUT delivers shifted symbols here as KeyChar.
        if (e.KeyChar >= 0x20 && e.KeyChar < 0x7F && !e.Ctrl && !e.Alt)
        {
            _input.Insert(_cursorCol, e.KeyChar);
            _cursorCol++;
            InvalidationCallback?.Invoke();
        }
    }

    private void Submit()
    {
        var line = _input.ToString();
        _input.Clear();
        _cursorCol = 0;
        _historyIndex = -1;

        AppendOutput(Prompt + line);

        if (!string.IsNullOrWhiteSpace(line))
        {
            _history.Add(line);
            CommandEntered?.Invoke(line);
        }
        else
        {
            InvalidationCallback?.Invoke();
        }
    }

    private void NavigateHistory(int direction)
    {
        if (_history.Count == 0) return;

        if (_historyIndex == -1 && direction < 0)
        {
            _historyIndex = _history.Count - 1;
        }
        else if (_historyIndex >= 0)
        {
            _historyIndex = Math.Clamp(_historyIndex + direction, -1, _history.Count - 1);
        }

        _input.Clear();
        if (_historyIndex >= 0 && _historyIndex < _history.Count)
        {
            _input.Append(_history[_historyIndex]);
        }
        _cursorCol = _input.Length;
        InvalidationCallback?.Invoke();
    }
}
