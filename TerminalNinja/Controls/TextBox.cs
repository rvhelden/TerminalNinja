using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;

namespace TerminalNinja.Controls;

/// <summary>
/// An editable text input control. Single-line by default; set <see cref="AcceptsReturn"/>
/// to <c>true</c> for multi-line mode. Supports caret navigation, text selection,
/// placeholder text, and an internal clipboard.
/// Corresponds to WPF's System.Windows.Controls.TextBox.
/// </summary>
[ContentProperty("Text")]
[RuntimeNameProperty("Name")]
public sealed class TextBox : Control
{
    public TextBox()
    {
        DefaultStyleKey = typeof(TextBox);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(TextBox),
            new FrameworkPropertyMetadata("", affectsRender: true,
                propertyChangedCallback: OnTextChanged));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(TextBox),
            new FrameworkPropertyMetadata(false, affectsRender: false));

    public static readonly DependencyProperty AcceptsReturnProperty =
        DependencyProperty.Register(nameof(AcceptsReturn), typeof(bool), typeof(TextBox),
            new FrameworkPropertyMetadata(false, affectsRender: false));

    public static readonly DependencyProperty MaxLengthProperty =
        DependencyProperty.Register(nameof(MaxLength), typeof(int), typeof(TextBox),
            new FrameworkPropertyMetadata(0, affectsRender: false));

    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register(nameof(TextWrapping), typeof(TextWrapping), typeof(TextBox),
            new FrameworkPropertyMetadata(TextWrapping.NoWrap, affectsRender: true));

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(Size), typeof(TextBox),
            new FrameworkPropertyMetadata(Size.Stretch, affectsRender: true));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(nameof(Height), typeof(Size), typeof(TextBox),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    public static readonly DependencyProperty FocusColorProperty =
        DependencyProperty.Register(nameof(FocusColor), typeof(Color), typeof(TextBox),
            new FrameworkPropertyMetadata(Color.Cyan, affectsRender: true));

    public static readonly DependencyProperty HoverColorProperty =
        DependencyProperty.Register(nameof(HoverColor), typeof(Color), typeof(TextBox),
            new FrameworkPropertyMetadata(Color.Yellow, affectsRender: true));

    public static readonly DependencyProperty SelectionBackgroundProperty =
        DependencyProperty.Register(nameof(SelectionBackground), typeof(Color), typeof(TextBox),
            new FrameworkPropertyMetadata(Color.Blue, affectsRender: true));

    public static readonly DependencyProperty SelectionForegroundProperty =
        DependencyProperty.Register(nameof(SelectionForeground), typeof(Color), typeof(TextBox),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(TextBox),
            new FrameworkPropertyMetadata("", affectsRender: true));

    public static readonly DependencyProperty PlaceholderForegroundProperty =
        DependencyProperty.Register(nameof(PlaceholderForeground), typeof(Color), typeof(TextBox),
            new FrameworkPropertyMetadata(Color.DarkGray, affectsRender: true));

    private bool _isInternalEdit;

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var tb = (TextBox)d;
        var oldText = (string?)e.OldValue ?? "";
        var newText = (string?)e.NewValue ?? "";

        // Clamp caret to new text length
        if (tb._caretIndex > newText.Length)
        {
            tb._caretIndex = newText.Length;
        }

        // Only clear selection for external changes (not our own editing methods)
        if (!tb._isInternalEdit)
        {
            tb.ClearSelection();
        }

        tb.TextChanged?.Invoke(tb, new TextChangedEventArgs(oldText, newText));
    }

    // ─── CLR Wrappers ────────────────────────────────────────────────

    /// <summary>Gets or sets the text content.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty)!;
        set => SetValue(TextProperty, value);
    }

    /// <summary>Gets or sets whether the text is read-only.</summary>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty)!;
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Gets or sets whether Enter inserts a newline (multi-line mode).</summary>
    public bool AcceptsReturn
    {
        get => (bool)GetValue(AcceptsReturnProperty)!;
        set => SetValue(AcceptsReturnProperty, value);
    }

    /// <summary>Gets or sets the maximum number of characters. 0 means unlimited.</summary>
    public int MaxLength
    {
        get => (int)GetValue(MaxLengthProperty)!;
        set => SetValue(MaxLengthProperty, value);
    }

    /// <summary>Gets or sets text wrapping behavior for multi-line mode.</summary>
    public TextWrapping TextWrapping
    {
        get => (TextWrapping)GetValue(TextWrappingProperty)!;
        set => SetValue(TextWrappingProperty, value);
    }

    /// <summary>Gets or sets the control width.</summary>
    public Size Width
    {
        get => (Size)GetValue(WidthProperty)!;
        set => SetValue(WidthProperty, value);
    }

    /// <summary>Gets or sets the control height.</summary>
    public Size Height
    {
        get => (Size)GetValue(HeightProperty)!;
        set => SetValue(HeightProperty, value);
    }

    /// <summary>Gets or sets the border color when focused.</summary>
    public Color FocusColor
    {
        get => (Color)GetValue(FocusColorProperty)!;
        set => SetValue(FocusColorProperty, value);
    }

    /// <summary>Gets or sets the border color when hovered.</summary>
    public Color HoverColor
    {
        get => (Color)GetValue(HoverColorProperty)!;
        set => SetValue(HoverColorProperty, value);
    }

    /// <summary>Gets or sets the background color for selected text.</summary>
    public Color SelectionBackground
    {
        get => (Color)GetValue(SelectionBackgroundProperty)!;
        set => SetValue(SelectionBackgroundProperty, value);
    }

    /// <summary>Gets or sets the foreground color for selected text.</summary>
    public Color SelectionForeground
    {
        get => (Color)GetValue(SelectionForegroundProperty)!;
        set => SetValue(SelectionForegroundProperty, value);
    }

    /// <summary>Gets or sets placeholder text shown when empty and not focused.</summary>
    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty)!;
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>Gets or sets the color for placeholder text.</summary>
    public Color PlaceholderForeground
    {
        get => (Color)GetValue(PlaceholderForegroundProperty)!;
        set => SetValue(PlaceholderForegroundProperty, value);
    }

    // ─── Events ──────────────────────────────────────────────────────

    /// <summary>Raised when the <see cref="Text"/> property changes.</summary>
    public event EventHandler<TextChangedEventArgs>? TextChanged;

    // ─── Internal Editing State ──────────────────────────────────────

    private int _caretIndex;
    private int _selectionAnchor = -1;
    private int _selectionStart = -1;
    private int _selectionLength;
    private int _scrollOffsetX;
    private int _scrollOffsetY;
    private int _lastKnownTextWidth;
    private int _lastKnownTextHeight;

    private static string _internalClipboard = "";

    /// <summary>Gets or sets the caret (cursor) position in the text.</summary>
    public int CaretIndex
    {
        get => _caretIndex;
        set
        {
            _caretIndex = Math.Clamp(value, 0, Text.Length);
            InvalidateVisual();
        }
    }

    /// <summary>Gets the start index of the current selection, or -1 if no selection.</summary>
    public int SelectionStart => _selectionStart;

    /// <summary>Gets the length of the current selection.</summary>
    public int SelectionLength => _selectionLength;

    /// <summary>Gets the selected text, or empty string if no selection.</summary>
    public string SelectedText => _selectionLength > 0
        ? Text.Substring(_selectionStart, _selectionLength)
        : "";

    // ─── Layout ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        // Border takes 2 chars each dimension
        var textWidth = Math.Max(Text.Length, 1) + Padding.HorizontalTotal + 2;
        var textHeight = AcceptsReturn
            ? SplitIntoLines(Text.AsSpan()).Count + Padding.VerticalTotal + 2
            : 1 + Padding.VerticalTotal + 2;

        var w = Width.Mode == SizeMode.Absolute ? Width.Resolve(parent.Width) : textWidth;
        var h = Height.Mode == SizeMode.Absolute ? Height.Resolve(parent.Height) : textHeight;
        return new Size2D(w, h);
    }

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent)
    {
        var preferred = GetPreferredSize(parent);
        var w = Width.Mode == SizeMode.Auto ? preferred.Width : Width.Resolve(parent.Width);
        var h = Height.Mode == SizeMode.Auto ? preferred.Height : Height.Resolve(parent.Height);
        return ApplyAlignment(parent, w, h);
    }

    // ─── Rendering ───────────────────────────────────────────────────

    /// <inheritdoc />
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            return;
        }

        // Border color based on visual state
        var borderColor = IsFocused ? FocusColor : IsMouseOver ? HoverColor : Foreground;
        if (!IsEnabled)
        {
            borderColor = DimColor(borderColor);
        }

        // Fill background
        var bgCell = new Cell(' ', Foreground, Background);
        buffer.FillRect(clipped, bgCell);

        // Draw border
        if (bounds is { Width: >= 2, Height: >= 2 })
        {
            var border = BorderStyle.Rounded(borderColor);
            DrawBorder(buffer, bounds, border.Chars, borderColor);
        }

        // Calculate text area (inside border and padding)
        var textX = bounds.X + 1 + Padding.Left;
        var textY = bounds.Y + 1 + Padding.Top;
        var textWidth = Math.Max(0, bounds.Width - 2 - Padding.HorizontalTotal);
        var textHeight = Math.Max(0, bounds.Height - 2 - Padding.VerticalTotal);
        _lastKnownTextWidth = textWidth;
        _lastKnownTextHeight = textHeight;

        if (textWidth <= 0 || textHeight <= 0)
        {
            return;
        }

        // Show placeholder when empty and not focused
        if (string.IsNullOrEmpty(Text) && !IsFocused && !string.IsNullOrEmpty(PlaceholderText))
        {
            RenderPlaceholder(buffer, textX, textY, textWidth);
        }
        else
        {
            EnsureCaretVisible(textWidth, textHeight);

            if (AcceptsReturn)
            {
                RenderMultiLine(buffer, textX, textY, textWidth, textHeight);
            }
            else
            {
                RenderSingleLine(buffer, textX, textY, textWidth);
            }
        }

        // Render cursor
        if (IsFocused)
        {
            RenderCaret(buffer, textX, textY, textWidth, textHeight);
        }
    }

    private void RenderPlaceholder(CellBuffer buffer, int x, int y, int width)
    {
        var text = PlaceholderText;
        var len = Math.Min(text.Length, width);
        for (var i = 0; i < len; i++)
        {
            var cx = x + i;
            if (cx >= 0 && cx < buffer.Width && y >= 0 && y < buffer.Height)
            {
                buffer.SetChar(cx, y, text[i], PlaceholderForeground, Background);
            }
        }
    }

    private void RenderSingleLine(CellBuffer buffer, int x, int y, int width)
    {
        var text = Text;
        var fg = IsEnabled ? Foreground : DimColor(Foreground);
        var bg = Background;

        for (var i = 0; i < width && _scrollOffsetX + i < text.Length; i++)
        {
            var textIndex = _scrollOffsetX + i;
            var cx = x + i;
            if (cx < 0 || cx >= buffer.Width || y < 0 || y >= buffer.Height)
            {
                continue;
            }

            var charFg = fg;
            var charBg = bg;
            if (IsInSelection(textIndex))
            {
                charFg = SelectionForeground;
                charBg = SelectionBackground;
            }

            buffer.SetChar(cx, y, text[textIndex], charFg, charBg);
        }
    }

    private void RenderMultiLine(CellBuffer buffer, int x, int y, int width, int height)
    {
        var text = Text;
        var lines = SplitIntoLines(text.AsSpan());
        var fg = IsEnabled ? Foreground : DimColor(Foreground);
        var bg = Background;

        for (var lineIdx = 0; lineIdx < height && _scrollOffsetY + lineIdx < lines.Count; lineIdx++)
        {
            var (lineStart, lineLength) = lines[_scrollOffsetY + lineIdx];
            var lineY = y + lineIdx;
            if (lineY < 0 || lineY >= buffer.Height)
            {
                continue;
            }

            var renderLen = Math.Min(lineLength, width);
            for (var col = 0; col < renderLen; col++)
            {
                var cx = x + col;
                if (cx < 0 || cx >= buffer.Width)
                {
                    continue;
                }

                var textIndex = lineStart + col;
                var charFg = fg;
                var charBg = bg;
                if (IsInSelection(textIndex))
                {
                    charFg = SelectionForeground;
                    charBg = SelectionBackground;
                }

                buffer.SetChar(cx, lineY, text[textIndex], charFg, charBg);
            }
        }
    }

    private void RenderCaret(CellBuffer buffer, int textX, int textY, int textWidth, int textHeight)
    {
        int caretScreenX, caretScreenY;

        if (AcceptsReturn)
        {
            var (line, col) = CaretToLineColumn();
            caretScreenX = textX + col;
            caretScreenY = textY + (line - _scrollOffsetY);
        }
        else
        {
            caretScreenX = textX + (_caretIndex - _scrollOffsetX);
            caretScreenY = textY;
        }

        if (caretScreenX < textX || caretScreenX >= textX + textWidth ||
            caretScreenY < textY || caretScreenY >= textY + textHeight)
        {
            return;
        }

        if (caretScreenX < 0 || caretScreenX >= buffer.Width ||
            caretScreenY < 0 || caretScreenY >= buffer.Height)
        {
            return;
        }

        var existing = buffer.GetCell(caretScreenX, caretScreenY);
        var ch = existing.Character is '\0' or ' ' ? ' ' : existing.Character;
        buffer.SetCell(caretScreenX, caretScreenY,
            new Cell(ch, existing.Foreground, existing.Background,
                existing.Decorations | TextDecorations.Inverse));
    }

    private bool IsInSelection(int textIndex)
    {
        return _selectionLength > 0 &&
               textIndex >= _selectionStart &&
               textIndex < _selectionStart + _selectionLength;
    }

    // ─── Text Editing ────────────────────────────────────────────────

    private void InsertText(string text)
    {
        if (IsReadOnly) return;

        // Delete selection first
        if (_selectionLength > 0)
        {
            DeleteSelection();
        }

        // Enforce MaxLength
        if (MaxLength > 0)
        {
            var available = MaxLength - Text.Length;
            if (available <= 0) return;
            if (text.Length > available)
            {
                text = text[..available];
            }
        }

        var newCaret = _caretIndex + text.Length;
        SetTextInternal(Text.Insert(_caretIndex, text));
        _caretIndex = newCaret;
    }

    private void DeleteBackward()
    {
        if (IsReadOnly) return;
        if (_selectionLength > 0)
        {
            DeleteSelection();
            return;
        }

        if (_caretIndex > 0)
        {
            var newCaret = _caretIndex - 1;
            SetTextInternal(Text.Remove(_caretIndex - 1, 1));
            _caretIndex = newCaret;
        }
    }

    private void DeleteForward()
    {
        if (IsReadOnly) return;
        if (_selectionLength > 0)
        {
            DeleteSelection();
            return;
        }

        if (_caretIndex < Text.Length)
        {
            SetTextInternal(Text.Remove(_caretIndex, 1));
        }
    }

    private void DeleteWordBackward()
    {
        if (IsReadOnly) return;
        if (_selectionLength > 0)
        {
            DeleteSelection();
            return;
        }

        var boundary = FindPreviousWordBoundary();
        if (boundary < _caretIndex)
        {
            SetTextInternal(Text.Remove(boundary, _caretIndex - boundary));
            _caretIndex = boundary;
        }
    }

    private void DeleteSelection()
    {
        if (_selectionLength == 0) return;
        var newCaret = _selectionStart;
        var start = _selectionStart;
        var length = _selectionLength;
        ClearSelection();
        SetTextInternal(Text.Remove(start, length));
        _caretIndex = newCaret;
    }

    /// <summary>
    /// Sets the Text property without clearing any active binding expression.
    /// Uses <see cref="DependencyObject.SetValueInternal"/> so two-way bindings
    /// remain attached and push the new value back to the source.
    /// </summary>
    private void SetTextInternal(string newText)
    {
        _isInternalEdit = true;
        try
        {
            SetValueInternal(TextProperty, newText);
        }
        finally
        {
            _isInternalEdit = false;
        }
    }

    // ─── Caret Movement ──────────────────────────────────────────────

    private void MoveCaret(int newIndex, bool extendSelection)
    {
        newIndex = Math.Clamp(newIndex, 0, Text.Length);

        if (extendSelection)
        {
            ExtendSelection(newIndex);
        }
        else
        {
            ClearSelection();
            _caretIndex = newIndex;
        }
    }

    private void MoveUp(bool extendSelection)
    {
        var (line, col) = CaretToLineColumn();
        if (line > 0)
        {
            MoveCaret(LineColumnToCaretIndex(line - 1, col), extendSelection);
        }
    }

    private void MoveDown(bool extendSelection)
    {
        var lines = SplitIntoLines(Text.AsSpan());
        var (line, col) = CaretToLineColumn();
        if (line < lines.Count - 1)
        {
            MoveCaret(LineColumnToCaretIndex(line + 1, col), extendSelection);
        }
    }

    private void MoveToLineStart(bool extendSelection)
    {
        if (AcceptsReturn)
        {
            var (line, _) = CaretToLineColumn();
            MoveCaret(LineColumnToCaretIndex(line, 0), extendSelection);
        }
        else
        {
            MoveCaret(0, extendSelection);
        }
    }

    private void MoveToLineEnd(bool extendSelection)
    {
        if (AcceptsReturn)
        {
            var lines = SplitIntoLines(Text.AsSpan());
            var (line, _) = CaretToLineColumn();
            if (line < lines.Count)
            {
                MoveCaret(LineColumnToCaretIndex(line, lines[line].Length), extendSelection);
            }
        }
        else
        {
            MoveCaret(Text.Length, extendSelection);
        }
    }

    private void MoveToPreviousWord(bool extendSelection)
    {
        MoveCaret(FindPreviousWordBoundary(), extendSelection);
    }

    private void MoveToNextWord(bool extendSelection)
    {
        MoveCaret(FindNextWordBoundary(), extendSelection);
    }

    // ─── Selection ───────────────────────────────────────────────────

    private void ExtendSelection(int newCaretIndex)
    {
        if (_selectionAnchor < 0)
        {
            _selectionAnchor = _caretIndex;
        }

        _caretIndex = newCaretIndex;
        _selectionStart = Math.Min(_selectionAnchor, _caretIndex);
        _selectionLength = Math.Abs(_selectionAnchor - _caretIndex);
    }

    private void ClearSelection()
    {
        _selectionAnchor = -1;
        _selectionStart = -1;
        _selectionLength = 0;
    }

    private void SelectAll()
    {
        _selectionAnchor = 0;
        _selectionStart = 0;
        _selectionLength = Text.Length;
        _caretIndex = Text.Length;
    }

    // ─── Clipboard ───────────────────────────────────────────────────

    private void CopyToClipboard()
    {
        if (_selectionLength > 0)
        {
            _internalClipboard = SelectedText;
        }
    }

    private void CutToClipboard()
    {
        CopyToClipboard();
        DeleteSelection();
    }

    private void PasteFromClipboard()
    {
        if (!string.IsNullOrEmpty(_internalClipboard))
        {
            InsertText(_internalClipboard);
        }
    }

    // ─── Scroll Offset ───────────────────────────────────────────────

    private void EnsureCaretVisible(int textWidth, int textHeight)
    {
        if (AcceptsReturn)
        {
            var (line, col) = CaretToLineColumn();
            if (line < _scrollOffsetY)
            {
                _scrollOffsetY = line;
            }

            if (line >= _scrollOffsetY + textHeight)
            {
                _scrollOffsetY = line - textHeight + 1;
            }
        }
        else
        {
            if (_caretIndex < _scrollOffsetX)
            {
                _scrollOffsetX = _caretIndex;
            }

            if (_caretIndex >= _scrollOffsetX + textWidth)
            {
                _scrollOffsetX = _caretIndex - textWidth + 1;
            }
        }
    }

    // ─── Word Boundary Helpers ───────────────────────────────────────

    private int FindPreviousWordBoundary()
    {
        if (_caretIndex == 0) return 0;
        var text = Text;
        var i = _caretIndex - 1;

        // Skip whitespace
        while (i > 0 && char.IsWhiteSpace(text[i])) i--;
        // Skip word characters
        while (i > 0 && !char.IsWhiteSpace(text[i - 1])) i--;

        return i;
    }

    private int FindNextWordBoundary()
    {
        if (_caretIndex >= Text.Length) return Text.Length;
        var text = Text;
        var i = _caretIndex;

        // Skip current word characters
        while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
        // Skip whitespace
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;

        return i;
    }

    // ─── Multi-line Helpers ──────────────────────────────────────────

    private (int Line, int Column) CaretToLineColumn()
    {
        var lines = SplitIntoLines(Text.AsSpan());
        var remaining = _caretIndex;

        for (var i = 0; i < lines.Count; i++)
        {
            if (remaining <= lines[i].Length)
            {
                return (i, remaining);
            }

            remaining -= lines[i].Length + 1; // +1 for newline
        }

        var lastLine = lines.Count - 1;
        return (lastLine, lines[lastLine].Length);
    }

    private int LineColumnToCaretIndex(int line, int column)
    {
        var lines = SplitIntoLines(Text.AsSpan());
        line = Math.Clamp(line, 0, lines.Count - 1);
        column = Math.Clamp(column, 0, lines[line].Length);

        var index = 0;
        for (var i = 0; i < line; i++)
        {
            index += lines[i].Length + 1; // +1 for newline
        }

        return index + column;
    }

    /// <summary>
    /// Splits text into line boundaries, handling \n, \r, and \r\n.
    /// </summary>
    private static List<(int Start, int Length)> SplitIntoLines(ReadOnlySpan<char> text)
    {
        var lines = new List<(int Start, int Length)>();
        if (text.Length == 0)
        {
            lines.Add((0, 0));
            return lines;
        }

        var lineStart = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines.Add((lineStart, i - lineStart));
                lineStart = i + 1;
            }
            else if (text[i] == '\r')
            {
                lines.Add((lineStart, i - lineStart));
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                lineStart = i + 1;
            }
        }

        if (lineStart <= text.Length)
        {
            lines.Add((lineStart, text.Length - lineStart));
        }

        return lines;
    }

    // ─── Input Handling ──────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnKeyEvent(KeyEvent e)
    {
        if (!IsEnabled) return;

        switch (e)
        {
            // Navigation
            case { Key: ConsoleKey.LeftArrow, Ctrl: true, Shift: var shift }:
                MoveToPreviousWord(shift);
                break;
            case { Key: ConsoleKey.RightArrow, Ctrl: true, Shift: var shift }:
                MoveToNextWord(shift);
                break;
            case { Key: ConsoleKey.LeftArrow, Shift: var shift }:
                MoveCaret(_caretIndex - 1, shift);
                break;
            case { Key: ConsoleKey.RightArrow, Shift: var shift }:
                MoveCaret(_caretIndex + 1, shift);
                break;
            case { Key: ConsoleKey.Home, Shift: var shift }:
                MoveToLineStart(shift);
                break;
            case { Key: ConsoleKey.End, Shift: var shift }:
                MoveToLineEnd(shift);
                break;
            case { Key: ConsoleKey.UpArrow, Shift: var shift } when AcceptsReturn:
                MoveUp(shift);
                break;
            case { Key: ConsoleKey.DownArrow, Shift: var shift } when AcceptsReturn:
                MoveDown(shift);
                break;

            // Editing
            case { Key: ConsoleKey.Backspace, Ctrl: true }:
                DeleteWordBackward();
                break;
            case { Key: ConsoleKey.Backspace }:
                DeleteBackward();
                break;
            case { Key: ConsoleKey.Delete }:
                DeleteForward();
                break;
            case { Key: ConsoleKey.Enter } when AcceptsReturn && !IsReadOnly:
                InsertText("\n");
                break;

            // Shortcuts
            case { Key: ConsoleKey.A, Ctrl: true }:
                SelectAll();
                break;
            case { Key: ConsoleKey.C, Ctrl: true }:
                CopyToClipboard();
                break;
            case { Key: ConsoleKey.X, Ctrl: true }:
                CutToClipboard();
                break;
            case { Key: ConsoleKey.V, Ctrl: true }:
                PasteFromClipboard();
                break;

            // Character input
            default:
                if (!IsReadOnly && e.KeyChar >= ' ')
                {
                    InsertText(e.KeyChar.ToString());
                }

                break;
        }

        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void OnMouseEvent(MouseEvent e)
    {
        if (!IsEnabled) return;

        if (e is { Action: MouseAction.Press, Button: MouseButton.Left })
        {
            var caretPos = ScreenPositionToCaretIndex(e.X, e.Y);
            MoveCaret(caretPos, extendSelection: false);
            InvalidateVisual();
        }
    }

    private int ScreenPositionToCaretIndex(int screenX, int screenY)
    {
        // This is approximate — uses last known text area position
        // The text area starts after border (1) + padding
        // We don't have access to bounds here, so estimate from scroll offset
        var textAreaStartX = 1 + Padding.Left; // relative to control left edge
        var col = screenX - textAreaStartX + _scrollOffsetX;
        col = Math.Clamp(col, 0, Text.Length);

        if (AcceptsReturn)
        {
            var textAreaStartY = 1 + Padding.Top;
            var line = screenY - textAreaStartY + _scrollOffsetY;
            var lines = SplitIntoLines(Text.AsSpan());
            line = Math.Clamp(line, 0, lines.Count - 1);
            col = Math.Clamp(col, 0, lines[line].Length);
            return LineColumnToCaretIndex(line, col);
        }

        return Math.Clamp(col, 0, Text.Length);
    }

    /// <inheritdoc />
    public override void OnGotFocus()
    {
        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void OnLostFocus()
    {
        InvalidateVisual();
    }

    // ─── Drawing Helpers ─────────────────────────────────────────────

    private void DrawBorder(CellBuffer buffer, Rect bounds, BorderChars chars, Color color)
    {
        if (chars.IsEmpty) return;

        // Horizontal edges
        for (var x = bounds.X + 1; x < bounds.X + bounds.Width - 1; x++)
        {
            if (x < 0 || x >= buffer.Width) continue;
            if (bounds.Y >= 0 && bounds.Y < buffer.Height)
                buffer.SetChar(x, bounds.Y, chars.Horizontal, color, Background);
            if (bounds.Y + bounds.Height - 1 >= 0 && bounds.Y + bounds.Height - 1 < buffer.Height)
                buffer.SetChar(x, bounds.Y + bounds.Height - 1, chars.Horizontal, color, Background);
        }

        // Vertical edges
        for (var y = bounds.Y + 1; y < bounds.Y + bounds.Height - 1; y++)
        {
            if (y < 0 || y >= buffer.Height) continue;
            if (bounds.X >= 0 && bounds.X < buffer.Width)
                buffer.SetChar(bounds.X, y, chars.Vertical, color, Background);
            if (bounds.X + bounds.Width - 1 >= 0 && bounds.X + bounds.Width - 1 < buffer.Width)
                buffer.SetChar(bounds.X + bounds.Width - 1, y, chars.Vertical, color, Background);
        }

        // Corners
        if (bounds.X >= 0 && bounds.X < buffer.Width && bounds.Y >= 0 && bounds.Y < buffer.Height)
            buffer.SetChar(bounds.X, bounds.Y, chars.TopLeft, color, Background);
        if (bounds.X + bounds.Width - 1 >= 0 && bounds.X + bounds.Width - 1 < buffer.Width &&
            bounds.Y >= 0 && bounds.Y < buffer.Height)
            buffer.SetChar(bounds.X + bounds.Width - 1, bounds.Y, chars.TopRight, color, Background);
        if (bounds.X >= 0 && bounds.X < buffer.Width &&
            bounds.Y + bounds.Height - 1 >= 0 && bounds.Y + bounds.Height - 1 < buffer.Height)
            buffer.SetChar(bounds.X, bounds.Y + bounds.Height - 1, chars.BottomLeft, color, Background);
        if (bounds.X + bounds.Width - 1 >= 0 && bounds.X + bounds.Width - 1 < buffer.Width &&
            bounds.Y + bounds.Height - 1 >= 0 && bounds.Y + bounds.Height - 1 < buffer.Height)
            buffer.SetChar(bounds.X + bounds.Width - 1, bounds.Y + bounds.Height - 1, chars.BottomRight, color, Background);
    }

    private static Color DimColor(Color color)
    {
        return new Color((byte)(color.R / 2), (byte)(color.G / 2), (byte)(color.B / 2));
    }
}
