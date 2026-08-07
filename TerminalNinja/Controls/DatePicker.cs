using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;

namespace TerminalNinja.Controls;

/// <summary>
/// A date input control with field-by-field editing (year, month, day).
/// Navigate fields with Left/Right, change values with Up/Down, or type digits directly.
/// Displays a customizable icon glyph at the right edge.
/// </summary>
[RuntimeNameProperty("Name")]
public sealed class DatePicker : Control
{
    private int _editField; // 0=year, 1=month, 2=day
    private string _digitBuffer = "";

    public DatePicker()
    {
        DefaultStyleKey = typeof(DatePicker);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty SelectedDateProperty =
        DependencyProperty.Register(nameof(SelectedDate), typeof(DateTime?), typeof(DatePicker),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: (d, _) => ((DatePicker)d).SelectedDateChanged?.Invoke(d, EventArgs.Empty)));

    public static readonly DependencyProperty DateFormatProperty =
        DependencyProperty.Register(nameof(DateFormat), typeof(string), typeof(DatePicker),
            new FrameworkPropertyMetadata("yyyy-MM-dd", affectsRender: true));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(DatePicker),
            new FrameworkPropertyMetadata("\uF073", affectsRender: true)); // nf-fa-calendar

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(Size), typeof(DatePicker),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(nameof(Height), typeof(Size), typeof(DatePicker),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    public static readonly DependencyProperty FocusColorProperty =
        DependencyProperty.Register(nameof(FocusColor), typeof(Color), typeof(DatePicker),
            new FrameworkPropertyMetadata(Color.Cyan, affectsRender: true));

    public static readonly DependencyProperty HoverColorProperty =
        DependencyProperty.Register(nameof(HoverColor), typeof(Color), typeof(DatePicker),
            new FrameworkPropertyMetadata(Color.Yellow, affectsRender: true));

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(DatePicker),
            new FrameworkPropertyMetadata("Select date...", affectsRender: true));

    // ─── CLR Wrappers ────────────────────────────────────────────────

    public DateTime? SelectedDate { get => (DateTime?)GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
    public string DateFormat { get => (string)GetValue(DateFormatProperty)!; set => SetValue(DateFormatProperty, value); }
    public string Icon { get => (string)GetValue(IconProperty)!; set => SetValue(IconProperty, value); }
    public Size Width { get => (Size)GetValue(WidthProperty)!; set => SetValue(WidthProperty, value); }
    public Size Height { get => (Size)GetValue(HeightProperty)!; set => SetValue(HeightProperty, value); }
    public Color FocusColor { get => (Color)GetValue(FocusColorProperty)!; set => SetValue(FocusColorProperty, value); }
    public Color HoverColor { get => (Color)GetValue(HoverColorProperty)!; set => SetValue(HoverColorProperty, value); }
    public string PlaceholderText { get => (string)GetValue(PlaceholderTextProperty)!; set => SetValue(PlaceholderTextProperty, value); }

    public event EventHandler? SelectedDateChanged;

    // ─── Layout ──────────────────────────────────────────────────────

    public override Size2D GetPreferredSize(Rect parent)
    {
        var w = Width.Mode == SizeMode.Absolute ? Width.Resolve(parent.Width) : 16; // "yyyy-MM-dd" + icon + border
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

    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
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

        // Icon at right edge
        var iconText = Icon;
        var iconX = bounds.X + bounds.Width - 2 - iconText.Length;
        for (var i = 0; i < iconText.Length; i++)
            SetCharSafe(buffer, iconX + i, textY, iconText[i], borderColor, Background);

        // Date text or placeholder
        var textX = bounds.X + 1;
        if (SelectedDate == null && !IsFocused)
        {
            var ph = PlaceholderText;
            for (var i = 0; i < ph.Length && textX + i < iconX; i++)
                SetCharSafe(buffer, textX + i, textY, ph[i], DimColor(fg), Background);
        }
        else
        {
            var date = SelectedDate ?? DateTime.Today;
            var parts = new[] { date.Year.ToString("D4"), date.Month.ToString("D2"), date.Day.ToString("D2") };
            var sep = "-";
            var x = textX;

            for (var f = 0; f < 3; f++)
            {
                var isActive = IsFocused && f == _editField;
                var partFg = isActive ? Background : fg;
                var partBg = isActive ? fg : Background;

                for (var c = 0; c < parts[f].Length; c++)
                {
                    SetCharSafe(buffer, x, textY, parts[f][c], partFg, partBg);
                    x++;
                }

                if (f < 2)
                {
                    SetCharSafe(buffer, x, textY, sep[0], fg, Background);
                    x++;
                }
            }
        }
    }

    // ─── Input ───────────────────────────────────────────────────────

    public override bool OnKeyEvent(KeyEvent e)
    {
        if (!IsEnabled) return false;

        // Auto-create date if null when user starts editing
        SelectedDate ??= DateTime.Today;
        var date = SelectedDate.Value;

        // Numeric direct entry
        if (e.KeyChar >= '0' && e.KeyChar <= '9')
        {
            _digitBuffer += e.KeyChar;
            var maxDigits = _editField == 0 ? 4 : 2;
            if (_digitBuffer.Length >= maxDigits)
            {
                ApplyDigitBuffer(date);
                _digitBuffer = "";
                if (_editField < 2) _editField++;
            }
            InvalidateVisual();
            return true;
        }

        // Commit partial digit buffer on field change
        if (_digitBuffer.Length > 0)
        {
            ApplyDigitBuffer(date);
            _digitBuffer = "";
        }

        var handled = true;

        switch (e.Key)
        {
            case ConsoleKey.LeftArrow:
                _editField = Math.Max(0, _editField - 1);
                break;
            case ConsoleKey.RightArrow or ConsoleKey.Tab:
                _editField = Math.Min(2, _editField + 1);
                break;
            case ConsoleKey.UpArrow:
                AdjustField(1);
                break;
            case ConsoleKey.DownArrow:
                AdjustField(-1);
                break;

            default:
                handled = false;
                break;
        }

        InvalidateVisual();
        return handled;
    }

    public override void OnGotFocus()
    {
        _editField = 0;
        _digitBuffer = "";
        InvalidateVisual();
    }

    private void AdjustField(int delta)
    {
        if (SelectedDate == null) return;
        var d = SelectedDate.Value;
        try
        {
            SelectedDate = _editField switch
            {
                0 => d.AddYears(delta),
                1 => d.AddMonths(delta),
                2 => d.AddDays(delta),
                _ => d
            };
        }
        catch (ArgumentOutOfRangeException) { }
    }

    private void ApplyDigitBuffer(DateTime date)
    {
        if (!int.TryParse(_digitBuffer, out var val)) return;
        try
        {
            SelectedDate = _editField switch
            {
                0 => new DateTime(Math.Clamp(val, 1, 9999), date.Month, Math.Min(date.Day, DateTime.DaysInMonth(Math.Clamp(val, 1, 9999), date.Month))),
                1 => new DateTime(date.Year, Math.Clamp(val, 1, 12), Math.Min(date.Day, DateTime.DaysInMonth(date.Year, Math.Clamp(val, 1, 12)))),
                2 => new DateTime(date.Year, date.Month, Math.Clamp(val, 1, DateTime.DaysInMonth(date.Year, date.Month))),
                _ => date
            };
        }
        catch (ArgumentOutOfRangeException) { }
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static void SetCharSafe(CellBuffer buf, int x, int y, char c, Color fg, Color bg)
    {
        if (x >= 0 && x < buf.Width && y >= 0 && y < buf.Height) buf.SetChar(x, y, c, fg, bg);
    }

    private void DrawBorder(CellBuffer buf, Rect b, BorderChars ch, Color col)
    {
        if (ch.IsEmpty) return;
        for (var x = b.X + 1; x < b.X + b.Width - 1; x++) { if (x < 0 || x >= buf.Width) continue; if (b.Y >= 0 && b.Y < buf.Height) buf.SetChar(x, b.Y, ch.Horizontal, col, Background); if (b.Y + b.Height - 1 >= 0 && b.Y + b.Height - 1 < buf.Height) buf.SetChar(x, b.Y + b.Height - 1, ch.Horizontal, col, Background); }
        for (var y = b.Y + 1; y < b.Y + b.Height - 1; y++) { if (y < 0 || y >= buf.Height) continue; if (b.X >= 0 && b.X < buf.Width) buf.SetChar(b.X, y, ch.Vertical, col, Background); if (b.X + b.Width - 1 >= 0 && b.X + b.Width - 1 < buf.Width) buf.SetChar(b.X + b.Width - 1, y, ch.Vertical, col, Background); }
        if (b.X >= 0 && b.X < buf.Width && b.Y >= 0 && b.Y < buf.Height) buf.SetChar(b.X, b.Y, ch.TopLeft, col, Background);
        if (b.X + b.Width - 1 >= 0 && b.X + b.Width - 1 < buf.Width && b.Y >= 0 && b.Y < buf.Height) buf.SetChar(b.X + b.Width - 1, b.Y, ch.TopRight, col, Background);
        if (b.X >= 0 && b.X < buf.Width && b.Y + b.Height - 1 >= 0 && b.Y + b.Height - 1 < buf.Height) buf.SetChar(b.X, b.Y + b.Height - 1, ch.BottomLeft, col, Background);
        if (b.X + b.Width - 1 >= 0 && b.X + b.Width - 1 < buf.Width && b.Y + b.Height - 1 >= 0 && b.Y + b.Height - 1 < buf.Height) buf.SetChar(b.X + b.Width - 1, b.Y + b.Height - 1, ch.BottomRight, col, Background);
    }

    private static Color DimColor(Color c) => new((byte)(c.R / 2), (byte)(c.G / 2), (byte)(c.B / 2));
}
