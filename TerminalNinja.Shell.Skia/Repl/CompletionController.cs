using TerminalNinja.Controls;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Shell.Language.Services;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Skia;

/// <summary>
/// Owns the Tab-triggered completion popup: the underlying <see cref="CompletionPanel"/>,
/// the list/selection/anchor state, and the keystroke dispatcher that runs while it's open.
/// </summary>
internal sealed class CompletionController
{
    private readonly CompletionPanel _panel = new();
    private readonly InputBuffer _input;
    private readonly Action _invalidate;

    private IReadOnlyList<CompletionItem>? _items;
    private int _index;
    private int _anchorCol;

    public CompletionController(InputBuffer input, Action invalidate)
    {
        _input = input;
        _invalidate = invalidate;
    }

    public bool IsOpen => _items is { Count: > 0 };

    /// <summary>
    /// Try to open a completion popup at the cursor. Returns false when there's nothing
    /// to complete — typically the caller (Tab handler) then falls back to focus
    /// navigation so empty-input Tab leaves the REPL.
    /// </summary>
    public bool Open(IReadOnlyDictionary<string, NValue>? scope, in ReplLayout layout)
    {
        var (cursorLine, cursorCol) = _input.CursorToLineCol(_input.CursorCol);
        var items = LanguageService.GetCompletions(_input.Text, new Position(cursorLine, cursorCol), scope);
        if (items.Count == 0) return false;

        _anchorCol = InputBuffer.FindWordStart(_input.Text, _input.CursorCol);
        _items = items;
        _index = 0;

        var entries = new CompletionEntry[items.Count];
        for (int i = 0; i < items.Count; i++) entries[i] = ToEntry(items[i]);

        var (anchorX, anchorY) = GetAnchor(layout);
        _panel.Placement = PlacementMode.Top;
        _panel.ShowAt(anchorX, anchorY, entries, 0);
        _invalidate();
        return true;
    }

    /// <summary>
    /// Replace the partial token under the cursor with the selected completion's label.
    /// </summary>
    public void Accept()
    {
        if (_items is null || _items.Count == 0) return;
        var item = _items[_index];

        var removeLen = _input.CursorCol - _anchorCol;
        if (removeLen > 0) _input.Remove(_anchorCol, removeLen);
        _input.Insert(_anchorCol, item.Label);
        _input.CursorCol = _anchorCol + item.Label.Length;
        Close();
    }

    public void Close()
    {
        _items = null;
        _index = 0;
        if (_panel.IsOpen) _panel.Hide();
        _invalidate();
    }

    /// <summary>
    /// Handle a keystroke while the popup is open. Returns true when the popup
    /// consumed (or dismissed itself on) the key — the caller should not run its
    /// normal handler. Returns false when the popup wasn't open or the key
    /// implicitly closes it and should keep flowing to the input handler.
    /// </summary>
    public CompletionKeyResult HandleKey(KeyEvent e)
    {
        if (!IsOpen) return CompletionKeyResult.NotHandled;

        switch (e.Key)
        {
            case ConsoleKey.UpArrow:
                _index = (_index - 1 + _items!.Count) % _items.Count;
                _panel.SelectedIndex = _index;
                _invalidate();
                return CompletionKeyResult.Consumed;
            case ConsoleKey.DownArrow:
                _index = (_index + 1) % _items!.Count;
                _panel.SelectedIndex = _index;
                _invalidate();
                return CompletionKeyResult.Consumed;
            case ConsoleKey.Escape:
                Close();
                return CompletionKeyResult.Consumed;
            case ConsoleKey.Tab:
            case ConsoleKey.Enter:
                Accept();
                return CompletionKeyResult.Consumed;
            default:
                // Any other key drops the popup. Caller still handles the keystroke
                // normally — the user kept typing past the prefix.
                Close();
                return CompletionKeyResult.ClosedFallthrough;
        }
    }

    /// <summary>
    /// Place the panel one row above the input line that holds the partial token,
    /// anchored to the column of the partial token.
    /// </summary>
    private (int X, int Y) GetAnchor(in ReplLayout layout)
    {
        var (anchorLine, _) = _input.CursorToLineCol(_anchorCol);
        var anchorLineStart = _input.LineColToIndex(anchorLine, 0);
        var anchorColOnLine = _anchorCol - anchorLineStart;
        return (layout.Bounds.X + layout.PromptWidth + anchorColOnLine, layout.InputTopY + anchorLine);
    }

    /// <summary>
    /// Map an LSP-shaped <see cref="CompletionItem"/> to a renderer-friendly
    /// <see cref="CompletionEntry"/>: glyph + colour per kind, Detail/Documentation passthrough.
    /// </summary>
    private static CompletionEntry ToEntry(CompletionItem item)
    {
        var (glyph, color) = item.Kind switch
        {
            CompletionKind.Function    => ("ƒ", new Color(0x89, 0xB4, 0xFA)),
            CompletionKind.Method      => ("ƒ", new Color(0x89, 0xB4, 0xFA)),
            CompletionKind.Constructor => ("ƒ", new Color(0x89, 0xB4, 0xFA)),
            CompletionKind.Variable    => ("α", new Color(0xA6, 0xE3, 0xA1)),
            CompletionKind.Field       => ("▪", new Color(0x94, 0xE2, 0xD5)),
            CompletionKind.Property    => ("▪", new Color(0x94, 0xE2, 0xD5)),
            CompletionKind.Module      => ("■", new Color(0xF9, 0xE2, 0xAF)),
            CompletionKind.Class       => ("C", new Color(0xF9, 0xE2, 0xAF)),
            CompletionKind.Interface   => ("I", new Color(0xF9, 0xE2, 0xAF)),
            CompletionKind.Keyword     => ("★", new Color(0xCB, 0xA6, 0xF7)),
            CompletionKind.Enum        => ("E", new Color(0xFA, 0xB3, 0x87)),
            CompletionKind.Snippet     => ("◇", new Color(0x9C, 0xA0, 0xB0)),
            _                          => ("·", new Color(0x9C, 0xA0, 0xB0)),
        };
        return new CompletionEntry(item.Label, glyph, color, item.Detail, item.Documentation);
    }
}

internal enum CompletionKeyResult
{
    /// <summary>The popup was not open; caller should run its normal key handler.</summary>
    NotHandled,
    /// <summary>The popup handled the key fully; caller should return.</summary>
    Consumed,
    /// <summary>The popup closed itself; caller should keep running its normal handler with the same key.</summary>
    ClosedFallthrough,
}
