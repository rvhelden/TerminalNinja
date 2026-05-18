using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;

namespace TerminalNinja.Controls;

/// <summary>
/// A combined date and time input control with field-by-field editing
/// (year, month, day, hours, minutes, seconds). Navigate with Left/Right,
/// change values with Up/Down, or type digits directly.
/// </summary>
[RuntimeNameProperty("Name")]
public sealed class DateTimePicker : Control
{
    private int _editField; // 0=year,1=month,2=day,3=hours,4=minutes,5=seconds
    private string _digitBuffer = "";

    public DateTimePicker()
    {
        DefaultStyleKey = typeof(DateTimePicker);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty SelectedDateTimeProperty =
        DependencyProperty.Register(nameof(SelectedDateTime), typeof(DateTime?), typeof(DateTimePicker),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: (d, _) => ((DateTimePicker)d).SelectedDateTimeChanged?.Invoke(d, EventArgs.Empty)));

    public static readonly DependencyProperty ShowSecondsProperty =
        DependencyProperty.Register(nameof(ShowSeconds), typeof(bool), typeof(DateTimePicker),
            new FrameworkPropertyMetadata(false, affectsRender: true));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(DateTimePicker),
            new FrameworkPropertyMetadata("\uF073", affectsRender: true)); // nf-fa-calendar

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(Size), typeof(DateTimePicker),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(nameof(Height), typeof(Size), typeof(DateTimePicker),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    public static readonly DependencyProperty FocusColorProperty =
        DependencyProperty.Register(nameof(FocusColor), typeof(Color), typeof(DateTimePicker),
            new FrameworkPropertyMetadata(Color.Cyan, affectsRender: true));

    public static readonly DependencyProperty HoverColorProperty =
        DependencyProperty.Register(nameof(HoverColor), typeof(Color), typeof(DateTimePicker),
            new FrameworkPropertyMetadata(Color.Yellow, affectsRender: true));

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(DateTimePicker),
            new FrameworkPropertyMetadata("Select date/time...", affectsRender: true));

    // ─── CLR Wrappers ────────────────────────────────────────────────

    public DateTime? SelectedDateTime { get => (DateTime?)GetValue(SelectedDateTimeProperty); set => SetValue(SelectedDateTimeProperty, value); }
    public bool ShowSeconds { get => (bool)GetValue(ShowSecondsProperty)!; set => SetValue(ShowSecondsProperty, value); }
    public string Icon { get => (string)GetValue(IconProperty)!; set => SetValue(IconProperty, value); }
    public Size Width { get => (Size)GetValue(WidthProperty)!; set => SetValue(WidthProperty, value); }
    public Size Height { get => (Size)GetValue(HeightProperty)!; set => SetValue(HeightProperty, value); }
    public Color FocusColor { get => (Color)GetValue(FocusColorProperty)!; set => SetValue(FocusColorProperty, value); }
    public Color HoverColor { get => (Color)GetValue(HoverColorProperty)!; set => SetValue(HoverColorProperty, value); }
    public string PlaceholderText { get => (string)GetValue(PlaceholderTextProperty)!; set => SetValue(PlaceholderTextProperty, value); }

    public event EventHandler? SelectedDateTimeChanged;

    // ─── Layout ──────────────────────────────────────────────────────

    private int FieldCount => ShowSeconds ? 6 : 5;

    public override Size2D GetPreferredSize(Rect parent)
    {
        var textWidth = ShowSeconds ? 19 : 16; // "yyyy-MM-dd HH:mm:ss" or "yyyy-MM-dd HH:mm"
        var w = Width.Mode == SizeMode.Absolute ? Width.Resolve(parent.Width) : textWidth + 5;
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

        // Icon
        var iconX = bounds.X + bounds.Width - 2 - Icon.Length;
        for (var i = 0; i < Icon.Length; i++)
            SetCharSafe(buffer, iconX + i, textY, Icon[i], borderColor, Background);

        var textX = bounds.X + 1;
        if (SelectedDateTime == null && !IsFocused)
        {
            var ph = PlaceholderText;
            for (var i = 0; i < ph.Length && textX + i < iconX; i++)
                SetCharSafe(buffer, textX + i, textY, ph[i], DimColor(fg), Background);
        }
        else
        {
            var dt = SelectedDateTime ?? DateTime.Today;
            var dateParts = new[] { dt.Year.ToString("D4"), dt.Month.ToString("D2"), dt.Day.ToString("D2") };
            var timeParts = ShowSeconds
                ? new[] { dt.Hour.ToString("D2"), dt.Minute.ToString("D2"), dt.Second.ToString("D2") }
                : new[] { dt.Hour.ToString("D2"), dt.Minute.ToString("D2") };

            var x = textX;

            // Date fields (0-2)
            for (var f = 0; f < 3; f++)
            {
                var isActive = IsFocused && f == _editField;
                var partFg = isActive ? Background : fg;
                var partBg = isActive ? fg : Background;
                for (var c = 0; c < dateParts[f].Length; c++) { SetCharSafe(buffer, x, textY, dateParts[f][c], partFg, partBg); x++; }
                if (f < 2) { SetCharSafe(buffer, x, textY, '-', fg, Background); x++; }
            }

            // Space separator
            SetCharSafe(buffer, x, textY, ' ', fg, Background);
            x++;

            // Time fields (3+)
            for (var f = 0; f < timeParts.Length; f++)
            {
                var fieldIdx = f + 3;
                var isActive = IsFocused && fieldIdx == _editField;
                var partFg = isActive ? Background : fg;
                var partBg = isActive ? fg : Background;
                for (var c = 0; c < timeParts[f].Length; c++) { SetCharSafe(buffer, x, textY, timeParts[f][c], partFg, partBg); x++; }
                if (f < timeParts.Length - 1) { SetCharSafe(buffer, x, textY, ':', fg, Background); x++; }
            }
        }
    }

    // ─── Input ───────────────────────────────────────────────────────

    public override void OnKeyEvent(KeyEvent e)
    {
        if (!IsEnabled) return;

        SelectedDateTime ??= DateTime.Today;
        var dt = SelectedDateTime.Value;

        if (e.KeyChar >= '0' && e.KeyChar <= '9')
        {
            _digitBuffer += e.KeyChar;
            var maxDigits = _editField == 0 ? 4 : 2;
            if (_digitBuffer.Length >= maxDigits)
            {
                ApplyDigitBuffer(dt);
                _digitBuffer = "";
                if (_editField < FieldCount - 1) _editField++;
            }
            InvalidateVisual();
            return;
        }

        if (_digitBuffer.Length > 0) { ApplyDigitBuffer(dt); _digitBuffer = ""; }

        switch (e.Key)
        {
            case ConsoleKey.LeftArrow:
                _editField = Math.Max(0, _editField - 1);
                break;
            case ConsoleKey.RightArrow or ConsoleKey.Tab:
                _editField = Math.Min(FieldCount - 1, _editField + 1);
                break;
            case ConsoleKey.UpArrow:
                AdjustField(1);
                break;
            case ConsoleKey.DownArrow:
                AdjustField(-1);
                break;
        }
        InvalidateVisual();
    }

    public override void OnGotFocus()
    {
        _editField = 0;
        _digitBuffer = "";
        InvalidateVisual();
    }

    private void AdjustField(int delta)
    {
        if (SelectedDateTime == null) return;
        var d = SelectedDateTime.Value;
        try
        {
            SelectedDateTime = _editField switch
            {
                0 => d.AddYears(delta),
                1 => d.AddMonths(delta),
                2 => d.AddDays(delta),
                3 => d.AddHours(delta),
                4 => d.AddMinutes(delta),
                5 => d.AddSeconds(delta),
                _ => d
            };
        }
        catch (ArgumentOutOfRangeException) { }
    }

    private void ApplyDigitBuffer(DateTime dt)
    {
        if (!int.TryParse(_digitBuffer, out var val)) return;
        try
        {
            SelectedDateTime = _editField switch
            {
                0 => new DateTime(Math.Clamp(val, 1, 9999), dt.Month, Math.Min(dt.Day, DateTime.DaysInMonth(Math.Clamp(val, 1, 9999), dt.Month)), dt.Hour, dt.Minute, dt.Second),
                1 => new DateTime(dt.Year, Math.Clamp(val, 1, 12), Math.Min(dt.Day, DateTime.DaysInMonth(dt.Year, Math.Clamp(val, 1, 12))), dt.Hour, dt.Minute, dt.Second),
                2 => new DateTime(dt.Year, dt.Month, Math.Clamp(val, 1, DateTime.DaysInMonth(dt.Year, dt.Month)), dt.Hour, dt.Minute, dt.Second),
                3 => new DateTime(dt.Year, dt.Month, dt.Day, Math.Clamp(val, 0, 23), dt.Minute, dt.Second),
                4 => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, Math.Clamp(val, 0, 59), dt.Second),
                5 => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, Math.Clamp(val, 0, 59)),
                _ => dt
            };
        }
        catch (ArgumentOutOfRangeException) { }
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static void SetCharSafe(CellBuffer buf, int x, int y, char c, Color fg, Color bg) { if (x >= 0 && x < buf.Width && y >= 0 && y < buf.Height) buf.SetChar(x, y, c, fg, bg); }

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
