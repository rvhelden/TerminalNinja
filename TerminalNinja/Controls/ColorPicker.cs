using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;

namespace TerminalNinja.Controls;

/// <summary>
/// A compact color selection control with a preview swatch and hex value display.
/// Type hex digits (0-9, A-F) for inline editing, or press Enter/Space to open
/// a full <see cref="ColorPickerDialog"/> with a large palette grid.
/// </summary>
[RuntimeNameProperty("Name")]
public sealed class ColorPicker : Control
{
    private string _hexBuffer = "";
    private bool _isEditingHex;

    public ColorPicker()
    {
        DefaultStyleKey = typeof(ColorPicker);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(ColorPicker),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true,
                propertyChangedCallback: OnSelectedColorChanged));

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(Size), typeof(ColorPicker),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(nameof(Height), typeof(Size), typeof(ColorPicker),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    public static readonly DependencyProperty FocusColorProperty =
        DependencyProperty.Register(nameof(FocusColor), typeof(Color), typeof(ColorPicker),
            new FrameworkPropertyMetadata(Color.Cyan, affectsRender: true));

    public static readonly DependencyProperty HoverColorProperty =
        DependencyProperty.Register(nameof(HoverColor), typeof(Color), typeof(ColorPicker),
            new FrameworkPropertyMetadata(Color.Yellow, affectsRender: true));

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var cp = (ColorPicker)d;
        cp.SelectedColorChanged?.Invoke(cp, new ValueChangedEventArgs<Color>((Color)e.OldValue!, (Color)e.NewValue!));
    }

    // ─── CLR Wrappers ────────────────────────────────────────────────

    public Color SelectedColor { get => (Color)GetValue(SelectedColorProperty)!; set => SetValue(SelectedColorProperty, value); }
    public Size Width { get => (Size)GetValue(WidthProperty)!; set => SetValue(WidthProperty, value); }
    public Size Height { get => (Size)GetValue(HeightProperty)!; set => SetValue(HeightProperty, value); }
    public Color FocusColor { get => (Color)GetValue(FocusColorProperty)!; set => SetValue(FocusColorProperty, value); }
    public Color HoverColor { get => (Color)GetValue(HoverColorProperty)!; set => SetValue(HoverColorProperty, value); }

    public event EventHandler<ValueChangedEventArgs<Color>>? SelectedColorChanged;

    // ─── Layout ──────────────────────────────────────────────────────

    public override Size2D GetPreferredSize(Rect parent)
    {
        var w = Width.Mode == SizeMode.Absolute ? Width.Resolve(parent.Width) : 14; // "██ #RRGGBB" + border
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

        var fg = IsEnabled ? Foreground : DimColor(Foreground);
        var innerX = bounds.X + 1;

        // Preview swatch + hex value
        var previewY = bounds.Y + bounds.Height / 2;
        if (previewY >= 0 && previewY < buffer.Height)
        {
            // Color swatch (2 chars)
            SetCharSafe(buffer, innerX, previewY, '\u2588', SelectedColor, Background);
            SetCharSafe(buffer, innerX + 1, previewY, '\u2588', SelectedColor, Background);
            SetCharSafe(buffer, innerX + 2, previewY, ' ', fg, Background);

            // Hex value or entry buffer
            var hexText = _isEditingHex ? "#" + _hexBuffer + "_" : SelectedColor.ToHex();
            for (var i = 0; i < hexText.Length && innerX + 3 + i < bounds.Right - 1; i++)
                SetCharSafe(buffer, innerX + 3 + i, previewY, hexText[i], fg, Background);
        }
    }

    // ─── Input ───────────────────────────────────────────────────────

    public override void OnKeyEvent(KeyEvent e)
    {
        if (!IsEnabled) return;

        // Hex digit entry
        var ch = char.ToUpperInvariant(e.KeyChar);
        if (ch is >= '0' and <= '9' or >= 'A' and <= 'F')
        {
            _isEditingHex = true;
            _hexBuffer += ch;
            if (_hexBuffer.Length >= 6)
            {
                SelectedColor = Color.FromHex(_hexBuffer);
                _hexBuffer = "";
                _isEditingHex = false;
            }
            InvalidateVisual();
            return;
        }

        if (e.Key == ConsoleKey.Backspace && _isEditingHex)
        {
            if (_hexBuffer.Length > 0) _hexBuffer = _hexBuffer[..^1];
            if (_hexBuffer.Length == 0) _isEditingHex = false;
            InvalidateVisual();
            return;
        }

        if (e.Key == ConsoleKey.Escape && _isEditingHex)
        {
            _hexBuffer = "";
            _isEditingHex = false;
            InvalidateVisual();
            return;
        }

        // Enter/Space: open the full color picker dialog
        if (e.Key is ConsoleKey.Enter or ConsoleKey.Spacebar)
        {
            OpenColorDialog();
        }
    }

    public override void OnGotFocus()
    {
        _hexBuffer = "";
        _isEditingHex = false;
        InvalidateVisual();
    }

    private async void OpenColorDialog()
    {
        var result = await ColorPickerDialog.ShowAsync(SelectedColor);
        if (result.HasValue)
        {
            SelectedColor = result.Value;
        }
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
