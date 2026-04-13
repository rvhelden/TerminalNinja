using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;

namespace TerminalNinja.Controls;

/// <summary>
/// A color selection control with a preview swatch, hex value display,
/// and a built-in palette grid. Navigate the palette with arrow keys,
/// select with Enter, or type hex digits (0-9, A-F) directly.
/// </summary>
[RuntimeNameProperty("Name")]
public sealed class ColorPicker : Control
{
    private int _paletteIndex;
    private string _hexBuffer = "";
    private bool _isEditingHex;

    private static readonly Color[] Palette =
    [
        Color.Black, new(128, 0, 0), new(0, 128, 0), new(128, 128, 0),
        new(0, 0, 128), new(128, 0, 128), new(0, 128, 128), Color.Gray,
        Color.DarkGray, Color.Red, Color.Green, Color.Yellow,
        Color.Blue, Color.Magenta, Color.Cyan, Color.White
    ];

    private const int PaletteCols = 8;
    private const int PaletteRows = 2;

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
        // Row 0: border top
        // Row 1: "██ #RRGGBB" preview
        // Row 2: palette row 1 (8 swatches)
        // Row 3: palette row 2 (8 swatches)
        // Row 4: border bottom
        var w = Width.Mode == SizeMode.Absolute ? Width.Resolve(parent.Width) : PaletteCols * 2 + 2; // 2 chars per swatch + border
        var h = Height.Mode == SizeMode.Absolute ? Height.Resolve(parent.Height) : PaletteRows + 3; // preview + palette rows + border
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
        var innerW = bounds.Width - 2;

        // Row 0 (inside border): preview swatch + hex value
        var previewY = bounds.Y + 1;
        if (previewY >= 0 && previewY < buffer.Height)
        {
            // Color swatch (2 chars)
            SetCharSafe(buffer, innerX, previewY, '\u2588', SelectedColor, Background); // █
            SetCharSafe(buffer, innerX + 1, previewY, '\u2588', SelectedColor, Background);
            SetCharSafe(buffer, innerX + 2, previewY, ' ', fg, Background);

            // Hex value or hex entry buffer
            var hexText = _isEditingHex ? "#" + _hexBuffer + "_" : SelectedColor.ToHex();
            for (var i = 0; i < hexText.Length && innerX + 3 + i < bounds.Right - 1; i++)
                SetCharSafe(buffer, innerX + 3 + i, previewY, hexText[i], fg, Background);
        }

        // Palette rows
        for (var row = 0; row < PaletteRows; row++)
        {
            var paletteY = bounds.Y + 2 + row;
            if (paletteY < 0 || paletteY >= buffer.Height || paletteY >= bounds.Bottom - 1) continue;

            for (var col = 0; col < PaletteCols; col++)
            {
                var idx = row * PaletteCols + col;
                if (idx >= Palette.Length) break;

                var swatchX = innerX + col * 2;
                var color = Palette[idx];
                var isHighlighted = IsFocused && idx == _paletteIndex;

                // Draw 2-char swatch
                var ch = isHighlighted ? '\u25A0' : '\u2588'; // ■ highlighted, █ normal
                var swatchFg = isHighlighted ? borderColor : color;
                var swatchBg = isHighlighted ? color : Background;

                SetCharSafe(buffer, swatchX, paletteY, ch, swatchFg, swatchBg);
                SetCharSafe(buffer, swatchX + 1, paletteY, ch, swatchFg, swatchBg);
            }
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
                var parsed = Color.FromHex(_hexBuffer);
                SelectedColor = parsed;
                _hexBuffer = "";
                _isEditingHex = false;
            }
            InvalidateVisual();
            return;
        }

        if (e.Key == ConsoleKey.Backspace && _isEditingHex)
        {
            if (_hexBuffer.Length > 0)
                _hexBuffer = _hexBuffer[..^1];
            if (_hexBuffer.Length == 0)
                _isEditingHex = false;
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

        // Palette navigation
        switch (e.Key)
        {
            case ConsoleKey.LeftArrow:
                _paletteIndex = Math.Max(0, _paletteIndex - 1);
                break;
            case ConsoleKey.RightArrow:
                _paletteIndex = Math.Min(Palette.Length - 1, _paletteIndex + 1);
                break;
            case ConsoleKey.UpArrow:
                if (_paletteIndex >= PaletteCols)
                    _paletteIndex -= PaletteCols;
                break;
            case ConsoleKey.DownArrow:
                if (_paletteIndex + PaletteCols < Palette.Length)
                    _paletteIndex += PaletteCols;
                break;
            case ConsoleKey.Enter or ConsoleKey.Spacebar:
                if (_paletteIndex >= 0 && _paletteIndex < Palette.Length)
                    SelectedColor = Palette[_paletteIndex];
                break;
        }

        InvalidateVisual();
    }

    public override void OnGotFocus()
    {
        _hexBuffer = "";
        _isEditingHex = false;
        InvalidateVisual();
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
