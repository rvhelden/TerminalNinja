using System.Collections.Specialized;
using System.Text;
using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.DependencySystem;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace NinjaShellUi;

/// <summary>
/// One row in an <see cref="EditableKeyValueList"/>. The control mutates <see cref="Value"/> on
/// commit; <see cref="Key"/> is treated as immutable. A non-null <see cref="Hint"/> is rendered
/// next to the value (intended for type info — e.g. <c>int</c>, <c>&lt;fn:2&gt;</c>) and is
/// kept out of the editable region.
/// </summary>
public sealed class KeyValueEntry
{
    /// <summary>Creates an entry with an optional hint shown after the value.</summary>
    public KeyValueEntry(string key, string value, string? hint = null, bool editable = true)
    {
        Key = key;
        Value = value;
        Hint = hint;
        Editable = editable;
    }

    /// <summary>Display key. Not editable through the control.</summary>
    public string Key { get; }

    /// <summary>Current value text. The control writes the committed value back here.</summary>
    public string Value { get; set; }

    /// <summary>Optional read-only annotation rendered after the value.</summary>
    public string? Hint { get; }

    /// <summary>When <c>false</c>, the row is shown but Enter does not enter edit mode.</summary>
    public bool Editable { get; }
}

/// <summary>
/// Custom <see cref="Control"/> that renders a scrolling list of (key, value) rows and supports
/// inline editing of the value. Arrow keys navigate selection; Enter starts editing or commits
/// changes; Escape cancels.
/// </summary>
/// <remarks>
/// <para>
/// Designed for the inspector panels in <c>NinjaShellUi</c> (process env, NinjaShell scope) so
/// the deliberate API surface is small and host-agnostic — it writes through the standard
/// <see cref="CellBuffer"/> contract and stays away from theming abstractions for now.
/// </para>
/// <para>
/// State machine: <c>Navigating</c> ↔ <c>Editing</c>. In Navigating, ↑/↓ move
/// <see cref="SelectedIndex"/>, Enter enters Editing (if the row is editable), Tab leaves the
/// control. In Editing, type to mutate the value buffer, Enter to commit (raises
/// <see cref="ItemCommitted"/>), Escape to revert.
/// </para>
/// </remarks>
public sealed class EditableKeyValueList : Control
{
    private readonly StringBuilder _editBuffer = new();
    private int _editCursor;
    private bool _isEditing;
    private int _scrollOffset;

    /// <summary>Currently selected row, or -1 when the list is empty.</summary>
    public int SelectedIndex { get; private set; } = -1;

    /// <summary>True when the control is in inline-edit mode for <see cref="SelectedIndex"/>.</summary>
    public bool IsEditing => _isEditing;

    /// <summary>
    /// Rows shown in the list. Wire to the view model's <see cref="ObservableCollection{T}"/>;
    /// adding/removing items reset the selection if it falls out of range.
    /// </summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IList<KeyValueEntry>), typeof(EditableKeyValueList),
            new FrameworkPropertyMetadata(default(IList<KeyValueEntry>), affectsRender: true, OnItemsSourceChanged));

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IList<KeyValueEntry>? ItemsSource
    {
        get => (IList<KeyValueEntry>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Raised after the user presses Enter to commit an edit. Argument is the row.</summary>
    public event Action<KeyValueEntry>? ItemCommitted;

    /// <summary>Creates a focusable list.</summary>
    public EditableKeyValueList()
    {
        Focusable = true;
    }

    /// <inheritdoc />
    public override void OnGotFocus() => InvalidateVisual();

    /// <inheritdoc />
    public override void OnLostFocus()
    {
        // Drop any in-flight edit when focus moves away — committing implicitly would surprise
        // the user, and keeping the edit-state pending while another control accepts keystrokes
        // means a stray Enter on the new focus would commit our buffer.
        if (_isEditing)
        {
            CancelEdit();
        }
        InvalidateVisual();
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var list = (EditableKeyValueList)d;

        if (e.OldValue is INotifyCollectionChanged oldNotify)
        {
            oldNotify.CollectionChanged -= list.OnItemsCollectionChanged;
        }
        if (e.NewValue is INotifyCollectionChanged newNotify)
        {
            newNotify.CollectionChanged += list.OnItemsCollectionChanged;
        }

        list.CancelEdit();
        list.ClampSelection();
        list.InvalidateVisual();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isEditing)
        {
            // The underlying collection mutated under us — safest to drop the edit so we don't
            // commit a stale buffer into a different row.
            CancelEdit();
        }
        ClampSelection();
        InvalidateVisual();
    }

    private void ClampSelection()
    {
        var count = ItemsSource?.Count ?? 0;
        if (count == 0)
        {
            SelectedIndex = -1;
            return;
        }
        if (SelectedIndex < 0)
        {
            SelectedIndex = 0;
        }
        else if (SelectedIndex >= count)
        {
            SelectedIndex = count - 1;
        }
    }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent) => new(parent.Width, parent.Height);

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent) => parent;

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var bg = Background;
        var fg = Foreground;
        ClearRegion(buffer, bounds, bg);

        var items = ItemsSource;
        if (items is null || items.Count == 0) return;

        // Keep the selected row in view: if it scrolled above or below the visible window,
        // adjust _scrollOffset just enough to bring it back.
        EnsureSelectionVisible(bounds.Height);

        var firstRow = _scrollOffset;
        var rowCount = Math.Min(bounds.Height, items.Count - firstRow);

        var focused = IsFocused;

        for (var r = 0; r < rowCount; r++)
        {
            var itemIndex = firstRow + r;
            var entry = items[itemIndex];
            var y = bounds.Y + r;
            var isSelected = itemIndex == SelectedIndex;

            var rowFg = fg;
            var rowBg = bg;
            if (isSelected)
            {
                if (focused)
                {
                    // Full inverse for focused selection.
                    (rowFg, rowBg) = (rowBg, rowFg);
                }
                else
                {
                    // Dim selection bar when the control doesn't have focus, so the user can
                    // see which panel will receive their next keystroke.
                    rowBg = new Color(0x45, 0x47, 0x5A);
                }
            }

            DrawRow(buffer, bounds.X, y, bounds.Width, entry, isSelected, rowFg, rowBg);
        }
    }

    private void EnsureSelectionVisible(int viewportHeight)
    {
        if (SelectedIndex < 0 || viewportHeight <= 0) return;
        if (SelectedIndex < _scrollOffset)
        {
            _scrollOffset = SelectedIndex;
        }
        else if (SelectedIndex >= _scrollOffset + viewportHeight)
        {
            _scrollOffset = SelectedIndex - viewportHeight + 1;
        }
    }

    private void DrawRow(CellBuffer buffer, int x, int y, int width, KeyValueEntry entry, bool isSelected, Color fg, Color bg)
    {
        if ((uint)y >= (uint)buffer.Height) return;

        // Fill the whole row with the (possibly inverted) background so selection reads as a bar.
        for (var c = 0; c < width; c++)
        {
            var bx = x + c;
            if ((uint)bx >= (uint)buffer.Width) break;
            buffer.SetCell(bx, y, new Cell(' ', fg, bg));
        }

        // Layout: " key = value [hint]" — key + " = " + value + optional " <hint>".
        // We don't word-wrap; long values get truncated with an ellipsis so the panel doesn't
        // bleed into siblings.
        var col = x;

        // Leading space for breathing room.
        if (col < x + width) { buffer.SetChar(col++, y, ' ', fg, bg); }

        col = DrawText(buffer, col, y, entry.Key, x + width - col, fg, bg);
        col = DrawText(buffer, col, y, " = ", x + width - col, fg, bg);

        var valueText = isSelected && _isEditing ? _editBuffer.ToString() : entry.Value;
        var hintLen = entry.Hint is null ? 0 : entry.Hint.Length + 1; // " hint"
        var maxValueWidth = Math.Max(0, x + width - col - hintLen);
        col = DrawText(buffer, col, y, valueText, maxValueWidth, fg, bg);

        if (entry.Hint is not null)
        {
            col = DrawText(buffer, col, y, " ", x + width - col, fg, bg);
            col = DrawText(buffer, col, y, entry.Hint, x + width - col, fg, bg);
        }

        // Inline edit cursor — invert the cell at the cursor position so it stands out
        // against the already-inverted selection background.
        if (isSelected && _isEditing)
        {
            // Cursor lives within the value column. Recompute its absolute X: key + " = ".
            var valueStartX = x + 1 + entry.Key.Length + 3; // leading space + key + " = "
            var cursorX = valueStartX + Math.Min(_editCursor, maxValueWidth - 1);
            if (cursorX >= valueStartX && cursorX < valueStartX + maxValueWidth && (uint)cursorX < (uint)buffer.Width)
            {
                var cell = buffer.GetCell(cursorX, y);
                buffer.SetCell(cursorX, y, new Cell(cell.Codepoint, cell.Background, cell.Foreground, cell.Decorations, cell.Flags));
            }
        }
    }

    private static int DrawText(CellBuffer buffer, int x, int y, string text, int maxWidth, Color fg, Color bg)
    {
        if (maxWidth <= 0 || (uint)y >= (uint)buffer.Height) return x;
        var drawn = 0;
        for (var i = 0; i < text.Length && drawn < maxWidth; i++)
        {
            var cx = x + drawn;
            if ((uint)cx >= (uint)buffer.Width) break;
            buffer.SetChar(cx, y, text[i], fg, bg);
            drawn++;
        }
        return x + drawn;
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

    /// <inheritdoc />
    public override void OnKeyEvent(KeyEvent e)
    {
        if (_isEditing)
        {
            HandleEditKey(e);
        }
        else
        {
            HandleNavigationKey(e);
        }
    }

    private void HandleNavigationKey(KeyEvent e)
    {
        var items = ItemsSource;
        if (items is null || items.Count == 0) return;

        switch (e.Key)
        {
            case ConsoleKey.UpArrow:
                if (SelectedIndex > 0) SelectedIndex--;
                InvalidateVisual();
                return;
            case ConsoleKey.DownArrow:
                if (SelectedIndex < items.Count - 1) SelectedIndex++;
                InvalidateVisual();
                return;
            case ConsoleKey.Home:
                SelectedIndex = 0;
                InvalidateVisual();
                return;
            case ConsoleKey.End:
                SelectedIndex = items.Count - 1;
                InvalidateVisual();
                return;
            case ConsoleKey.PageUp:
                SelectedIndex = Math.Max(0, SelectedIndex - 5);
                InvalidateVisual();
                return;
            case ConsoleKey.PageDown:
                SelectedIndex = Math.Min(items.Count - 1, SelectedIndex + 5);
                InvalidateVisual();
                return;
            case ConsoleKey.Enter:
                BeginEdit();
                return;
        }
    }

    private void HandleEditKey(KeyEvent e)
    {
        switch (e.Key)
        {
            case ConsoleKey.Enter:
                CommitEdit();
                return;
            case ConsoleKey.Escape:
                CancelEdit();
                InvalidateVisual();
                return;
            case ConsoleKey.Backspace:
                if (_editCursor > 0)
                {
                    _editBuffer.Remove(_editCursor - 1, 1);
                    _editCursor--;
                    InvalidateVisual();
                }
                return;
            case ConsoleKey.Delete:
                if (_editCursor < _editBuffer.Length)
                {
                    _editBuffer.Remove(_editCursor, 1);
                    InvalidateVisual();
                }
                return;
            case ConsoleKey.LeftArrow:
                if (_editCursor > 0) { _editCursor--; InvalidateVisual(); }
                return;
            case ConsoleKey.RightArrow:
                if (_editCursor < _editBuffer.Length) { _editCursor++; InvalidateVisual(); }
                return;
            case ConsoleKey.Home:
                _editCursor = 0; InvalidateVisual();
                return;
            case ConsoleKey.End:
                _editCursor = _editBuffer.Length; InvalidateVisual();
                return;
        }

        if (e.KeyChar >= 0x20 && e.KeyChar < 0x7F && !e.Ctrl && !e.Alt)
        {
            _editBuffer.Insert(_editCursor, e.KeyChar);
            _editCursor++;
            InvalidateVisual();
        }
    }

    private void BeginEdit()
    {
        var items = ItemsSource;
        if (items is null || SelectedIndex < 0 || SelectedIndex >= items.Count) return;

        var entry = items[SelectedIndex];
        if (!entry.Editable) return;

        _editBuffer.Clear();
        _editBuffer.Append(entry.Value);
        _editCursor = _editBuffer.Length;
        _isEditing = true;
        InvalidateVisual();
    }

    private void CommitEdit()
    {
        var items = ItemsSource;
        if (items is null || SelectedIndex < 0 || SelectedIndex >= items.Count)
        {
            CancelEdit();
            return;
        }

        var entry = items[SelectedIndex];
        var newValue = _editBuffer.ToString();
        _isEditing = false;
        _editBuffer.Clear();
        _editCursor = 0;

        if (!ReferenceEquals(entry.Value, newValue) && entry.Value != newValue)
        {
            entry.Value = newValue;
            ItemCommitted?.Invoke(entry);
        }
        InvalidateVisual();
    }

    private void CancelEdit()
    {
        _isEditing = false;
        _editBuffer.Clear();
        _editCursor = 0;
    }
}
