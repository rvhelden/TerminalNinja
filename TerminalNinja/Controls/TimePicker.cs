using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;

namespace TerminalNinja.Controls;

/// <summary>
/// A time input control with field-by-field editing (hours, minutes, seconds).
/// Navigate fields with Left/Right, change values with Up/Down, or type digits directly.
/// Displays a customizable icon glyph at the right edge.
/// </summary>
[RuntimeNameProperty("Name")]
public sealed class TimePicker : Control
{
    private int _editField; // 0=hours, 1=minutes, 2=seconds
    private string _digitBuffer = "";

    public TimePicker()
    {
        DefaultStyleKey = typeof(TimePicker);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty SelectedTimeProperty =
        DependencyProperty.Register(nameof(SelectedTime), typeof(TimeSpan?), typeof(TimePicker),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: (d, _) => ((TimePicker)d).SelectedTimeChanged?.Invoke(d, EventArgs.Empty)));

    public static readonly DependencyProperty ShowSecondsProperty =
        DependencyProperty.Register(nameof(ShowSeconds), typeof(bool), typeof(TimePicker),
            new FrameworkPropertyMetadata(false, affectsRender: true));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(TimePicker),
            new FrameworkPropertyMetadata("\uF017", affectsRender: true)); // nf-fa-clock_o

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(Size), typeof(TimePicker),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(nameof(Height), typeof(Size), typeof(TimePicker),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    public static readonly DependencyProperty FocusColorProperty =
        DependencyProperty.Register(nameof(FocusColor), typeof(Color), typeof(TimePicker),
            new FrameworkPropertyMetadata(Color.Cyan, affectsRender: true));

    public static readonly DependencyProperty HoverColorProperty =
        DependencyProperty.Register(nameof(HoverColor), typeof(Color), typeof(TimePicker),
            new FrameworkPropertyMetadata(Color.Yellow, affectsRender: true));

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(TimePicker),
            new FrameworkPropertyMetadata("Select time...", affectsRender: true));

    // ─── CLR Wrappers ────────────────────────────────────────────────

    public TimeSpan? SelectedTime { get => (TimeSpan?)GetValue(SelectedTimeProperty); set => SetValue(SelectedTimeProperty, value); }
    public bool ShowSeconds { get => (bool)GetValue(ShowSecondsProperty)!; set => SetValue(ShowSecondsProperty, value); }
    public string Icon { get => (string)GetValue(IconProperty)!; set => SetValue(IconProperty, value); }
    public Size Width { get => (Size)GetValue(WidthProperty)!; set => SetValue(WidthProperty, value); }
    public Size Height { get => (Size)GetValue(HeightProperty)!; set => SetValue(HeightProperty, value); }
    public Color FocusColor { get => (Color)GetValue(FocusColorProperty)!; set => SetValue(FocusColorProperty, value); }
    public Color HoverColor { get => (Color)GetValue(HoverColorProperty)!; set => SetValue(HoverColorProperty, value); }
    public string PlaceholderText { get => (string)GetValue(PlaceholderTextProperty)!; set => SetValue(PlaceholderTextProperty, value); }

    public event EventHandler? SelectedTimeChanged;

    // ─── Layout ──────────────────────────────────────────────────────

    private int FieldCount => ShowSeconds ? 3 : 2;

    public override Size2D GetPreferredSize(Rect parent)
    {
        var textWidth = ShowSeconds ? 8 : 5; // "HH:mm:ss" or "HH:mm"
        var w = Width.Mode == SizeMode.Absolute ? Width.Resolve(parent.Width) : textWidth + 5; // +icon+border+padding
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
        if (SelectedTime == null && !IsFocused)
        {
            var ph = PlaceholderText;
            for (var i = 0; i < ph.Length && textX + i < iconX; i++)
                SetCharSafe(buffer, textX + i, textY, ph[i], DimColor(fg), Background);
        }
        else
        {
            var t = SelectedTime ?? TimeSpan.Zero;
            var parts = ShowSeconds
                ? new[] { t.Hours.ToString("D2"), t.Minutes.ToString("D2"), t.Seconds.ToString("D2") }
                : new[] { t.Hours.ToString("D2"), t.Minutes.ToString("D2") };

            var x = textX;
            for (var f = 0; f < parts.Length; f++)
            {
                var isActive = IsFocused && f == _editField;
                var partFg = isActive ? Background : fg;
                var partBg = isActive ? fg : Background;

                for (var c = 0; c < parts[f].Length; c++)
                {
                    SetCharSafe(buffer, x, textY, parts[f][c], partFg, partBg);
                    x++;
                }

                if (f < parts.Length - 1)
                {
                    SetCharSafe(buffer, x, textY, ':', fg, Background);
                    x++;
                }
            }
        }
    }

    // ─── Input ───────────────────────────────────────────────────────

    public override void OnKeyEvent(KeyEvent e)
    {
        if (!IsEnabled) return;

        SelectedTime ??= TimeSpan.Zero;
        var t = SelectedTime.Value;

        if (e.KeyChar >= '0' && e.KeyChar <= '9')
        {
            _digitBuffer += e.KeyChar;
            if (_digitBuffer.Length >= 2)
            {
                ApplyDigitBuffer(t);
                _digitBuffer = "";
                if (_editField < FieldCount - 1) _editField++;
            }
            InvalidateVisual();
            return;
        }

        if (_digitBuffer.Length > 0) { ApplyDigitBuffer(t); _digitBuffer = ""; }

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
        if (SelectedTime == null) return;
        var t = SelectedTime.Value;
        SelectedTime = _editField switch
        {
            0 => new TimeSpan((t.Hours + delta + 24) % 24, t.Minutes, t.Seconds),
            1 => new TimeSpan(t.Hours, (t.Minutes + delta + 60) % 60, t.Seconds),
            2 => new TimeSpan(t.Hours, t.Minutes, (t.Seconds + delta + 60) % 60),
            _ => t
        };
    }

    private void ApplyDigitBuffer(TimeSpan t)
    {
        if (!int.TryParse(_digitBuffer, out var val)) return;
        SelectedTime = _editField switch
        {
            0 => new TimeSpan(Math.Clamp(val, 0, 23), t.Minutes, t.Seconds),
            1 => new TimeSpan(t.Hours, Math.Clamp(val, 0, 59), t.Seconds),
            2 => new TimeSpan(t.Hours, t.Minutes, Math.Clamp(val, 0, 59)),
            _ => t
        };
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
