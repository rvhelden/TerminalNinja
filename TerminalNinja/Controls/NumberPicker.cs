using System.Globalization;
using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;

namespace TerminalNinja.Controls;

/// <summary>
/// A numeric input control with increment/decrement via arrow keys and direct numeric entry.
/// Renders as a bordered control with left/right arrows and a centered numeric value.
/// </summary>
[RuntimeNameProperty("Name")]
public sealed class NumberPicker : Control
{
    private string _inputBuffer = "";
    private bool _isDirectEntry;

    public NumberPicker()
    {
        DefaultStyleKey = typeof(NumberPicker);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(NumberPicker),
            new FrameworkPropertyMetadata(0.0, affectsRender: true,
                propertyChangedCallback: OnValueChanged,
                coerceValueCallback: CoerceValue));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(NumberPicker),
            new FrameworkPropertyMetadata(0.0, affectsRender: true));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(NumberPicker),
            new FrameworkPropertyMetadata(100.0, affectsRender: true));

    public static readonly DependencyProperty IncrementProperty =
        DependencyProperty.Register(nameof(Increment), typeof(double), typeof(NumberPicker),
            new PropertyMetadata(1.0));

    public static readonly DependencyProperty DecimalPlacesProperty =
        DependencyProperty.Register(nameof(DecimalPlaces), typeof(int), typeof(NumberPicker),
            new FrameworkPropertyMetadata(0, affectsRender: true));

    public static readonly DependencyProperty FormatStringProperty =
        DependencyProperty.Register(nameof(FormatString), typeof(string), typeof(NumberPicker),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true));

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(Size), typeof(NumberPicker),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(nameof(Height), typeof(Size), typeof(NumberPicker),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    public static readonly DependencyProperty FocusColorProperty =
        DependencyProperty.Register(nameof(FocusColor), typeof(Color), typeof(NumberPicker),
            new FrameworkPropertyMetadata(Color.Cyan, affectsRender: true));

    public static readonly DependencyProperty HoverColorProperty =
        DependencyProperty.Register(nameof(HoverColor), typeof(Color), typeof(NumberPicker),
            new FrameworkPropertyMetadata(Color.Yellow, affectsRender: true));

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var np = (NumberPicker)d;
        np.ValueChanged?.Invoke(np, new ValueChangedEventArgs<double>((double)e.OldValue!, (double)e.NewValue!));
    }

    private static object? CoerceValue(DependencyObject d, object? baseValue)
    {
        var np = (NumberPicker)d;
        var v = (double)(baseValue ?? 0.0);
        return Math.Clamp(v, np.Minimum, np.Maximum);
    }

    // ─── CLR Wrappers ────────────────────────────────────────────────

    public double Value { get => (double)GetValue(ValueProperty)!; set => SetValue(ValueProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty)!; set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty)!; set => SetValue(MaximumProperty, value); }
    public double Increment { get => (double)GetValue(IncrementProperty)!; set => SetValue(IncrementProperty, value); }
    public int DecimalPlaces { get => (int)GetValue(DecimalPlacesProperty)!; set => SetValue(DecimalPlacesProperty, value); }
    public string? FormatString { get => (string?)GetValue(FormatStringProperty); set => SetValue(FormatStringProperty, value); }
    public Size Width { get => (Size)GetValue(WidthProperty)!; set => SetValue(WidthProperty, value); }
    public Size Height { get => (Size)GetValue(HeightProperty)!; set => SetValue(HeightProperty, value); }
    public Color FocusColor { get => (Color)GetValue(FocusColorProperty)!; set => SetValue(FocusColorProperty, value); }
    public Color HoverColor { get => (Color)GetValue(HoverColorProperty)!; set => SetValue(HoverColorProperty, value); }

    // ─── Events ──────────────────────────────────────────────────────

    public event EventHandler<ValueChangedEventArgs<double>>? ValueChanged;

    // ─── Layout ──────────────────────────────────────────────────────

    public override Size2D GetPreferredSize(Rect parent)
    {
        var text = FormatValue();
        var contentWidth = text.Length + 6; // 2 arrows + 2 spaces + 2 border
        var w = Width.Mode == SizeMode.Absolute ? Width.Resolve(parent.Width) : Math.Max(contentWidth, 12);
        var h = Height.Mode == SizeMode.Absolute ? Height.Resolve(parent.Height) : 3;
        return new Size2D(w, h);
    }

    public override Rect CalculateBounds(Rect parent)
    {
        var preferred = GetPreferredSize(parent);
        var w = Width.Mode == SizeMode.Auto ? preferred.Width : Width.Resolve(parent.Width);
        var h = Height.Mode == SizeMode.Auto ? preferred.Height : Height.Resolve(parent.Height);
        return ApplyAlignment(parent, w, h);
    }

    // ─── Rendering ───────────────────────────────────────────────────

    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;

        var borderColor = IsFocused ? FocusColor : IsMouseOver ? HoverColor : Foreground;
        if (!IsEnabled) borderColor = DimColor(borderColor);

        buffer.FillRect(clipped, new Cell(' ', Foreground, Background));

        if (bounds is { Width: >= 2, Height: >= 2 })
        {
            var border = BorderStyle.Rounded(borderColor);
            DrawBorder(buffer, bounds, border.Chars, borderColor);
        }

        var textY = bounds.Y + bounds.Height / 2;
        var fg = IsEnabled ? Foreground : DimColor(Foreground);
        var text = _isDirectEntry ? _inputBuffer + "_" : FormatValue();
        var contentX = bounds.X + 1;
        var contentWidth = bounds.Width - 2;

        // Left arrow
        SetCharSafe(buffer, contentX, textY, '\u25C0', borderColor, Background); // ◀
        // Right arrow
        SetCharSafe(buffer, bounds.X + bounds.Width - 2, textY, '\u25B6', borderColor, Background); // ▶

        // Centered value text
        var textArea = contentWidth - 2; // minus arrows
        var textStart = contentX + 1 + (textArea - text.Length) / 2;
        for (var i = 0; i < text.Length && textStart + i < bounds.X + bounds.Width - 2; i++)
        {
            SetCharSafe(buffer, textStart + i, textY, text[i], fg, Background);
        }
    }

    // ─── Input ───────────────────────────────────────────────────────

    public override void OnKeyEvent(KeyEvent e)
    {
        if (!IsEnabled) return;

        // Numeric direct entry
        if (e.KeyChar >= '0' && e.KeyChar <= '9')
        {
            _isDirectEntry = true;
            _inputBuffer += e.KeyChar;
            InvalidateVisual();
            return;
        }

        if (e.KeyChar == '.' && DecimalPlaces > 0 && !_inputBuffer.Contains('.'))
        {
            _isDirectEntry = true;
            _inputBuffer += '.';
            InvalidateVisual();
            return;
        }

        if (e.Key == ConsoleKey.Backspace && _isDirectEntry)
        {
            if (_inputBuffer.Length > 0)
            {
                _inputBuffer = _inputBuffer[..^1];
                if (_inputBuffer.Length == 0) _isDirectEntry = false;
            }
            InvalidateVisual();
            return;
        }

        // Commit direct entry on Enter or navigation
        if (_isDirectEntry && e.Key is ConsoleKey.Enter)
        {
            CommitDirectEntry();
            return;
        }

        // If in direct entry and a non-digit key is pressed, commit first
        if (_isDirectEntry)
        {
            CommitDirectEntry();
        }

        switch (e.Key)
        {
            case ConsoleKey.UpArrow or ConsoleKey.RightArrow:
                Value += Increment;
                break;
            case ConsoleKey.DownArrow or ConsoleKey.LeftArrow:
                Value -= Increment;
                break;
            case ConsoleKey.PageUp:
                Value += Increment * 10;
                break;
            case ConsoleKey.PageDown:
                Value -= Increment * 10;
                break;
            case ConsoleKey.Home:
                Value = Minimum;
                break;
            case ConsoleKey.End:
                Value = Maximum;
                break;
        }

        InvalidateVisual();
    }

    public override void OnLostFocus()
    {
        if (_isDirectEntry) CommitDirectEntry();
        base.OnLostFocus();
    }

    private void CommitDirectEntry()
    {
        if (double.TryParse(_inputBuffer, CultureInfo.InvariantCulture, out var parsed))
        {
            Value = parsed;
        }
        _inputBuffer = "";
        _isDirectEntry = false;
        InvalidateVisual();
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private string FormatValue()
    {
        if (!string.IsNullOrEmpty(FormatString))
            return Value.ToString(FormatString, CultureInfo.InvariantCulture);
        return Value.ToString($"F{DecimalPlaces}", CultureInfo.InvariantCulture);
    }

    private static void SetCharSafe(CellBuffer buffer, int x, int y, char c, Color fg, Color bg)
    {
        if (x >= 0 && x < buffer.Width && y >= 0 && y < buffer.Height)
            buffer.SetChar(x, y, c, fg, bg);
    }

    private void DrawBorder(CellBuffer buffer, Rect bounds, BorderChars chars, Color color)
    {
        if (chars.IsEmpty) return;
        for (var x = bounds.X + 1; x < bounds.X + bounds.Width - 1; x++)
        {
            if (x < 0 || x >= buffer.Width) continue;
            if (bounds.Y >= 0 && bounds.Y < buffer.Height) buffer.SetChar(x, bounds.Y, chars.Horizontal, color, Background);
            if (bounds.Y + bounds.Height - 1 >= 0 && bounds.Y + bounds.Height - 1 < buffer.Height) buffer.SetChar(x, bounds.Y + bounds.Height - 1, chars.Horizontal, color, Background);
        }
        for (var y = bounds.Y + 1; y < bounds.Y + bounds.Height - 1; y++)
        {
            if (y < 0 || y >= buffer.Height) continue;
            if (bounds.X >= 0 && bounds.X < buffer.Width) buffer.SetChar(bounds.X, y, chars.Vertical, color, Background);
            if (bounds.X + bounds.Width - 1 >= 0 && bounds.X + bounds.Width - 1 < buffer.Width) buffer.SetChar(bounds.X + bounds.Width - 1, y, chars.Vertical, color, Background);
        }
        if (bounds.X >= 0 && bounds.X < buffer.Width && bounds.Y >= 0 && bounds.Y < buffer.Height) buffer.SetChar(bounds.X, bounds.Y, chars.TopLeft, color, Background);
        if (bounds.X + bounds.Width - 1 >= 0 && bounds.X + bounds.Width - 1 < buffer.Width && bounds.Y >= 0 && bounds.Y < buffer.Height) buffer.SetChar(bounds.X + bounds.Width - 1, bounds.Y, chars.TopRight, color, Background);
        if (bounds.X >= 0 && bounds.X < buffer.Width && bounds.Y + bounds.Height - 1 >= 0 && bounds.Y + bounds.Height - 1 < buffer.Height) buffer.SetChar(bounds.X, bounds.Y + bounds.Height - 1, chars.BottomLeft, color, Background);
        if (bounds.X + bounds.Width - 1 >= 0 && bounds.X + bounds.Width - 1 < buffer.Width && bounds.Y + bounds.Height - 1 >= 0 && bounds.Y + bounds.Height - 1 < buffer.Height) buffer.SetChar(bounds.X + bounds.Width - 1, bounds.Y + bounds.Height - 1, chars.BottomRight, color, Background);
    }

    private static Color DimColor(Color c) => new((byte)(c.R / 2), (byte)(c.G / 2), (byte)(c.B / 2));
}
