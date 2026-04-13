using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A modal dialog with a large color palette grid for color selection.
/// Opened by <see cref="ColorPicker"/> when the user presses Enter/Space.
/// Use <see cref="ShowAsync"/> for a convenient static API.
/// </summary>
public sealed class ColorPickerDialog : Window
{
    private int _paletteIndex;
    private string _hexBuffer = "";
    private bool _isEditingHex;

    private const int PaletteCols = 16;
    private const int PaletteRows = 6;

    private static readonly Color[] Palette = BuildPalette();

    public ColorPickerDialog() : this(Color.White) { }

    public ColorPickerDialog(Color initialColor)
    {
        DefaultStyleKey = typeof(ColorPickerDialog);
        Title = "Pick a Color";
        Width = Size.Absolute(38);
        Height = Size.Absolute(14);
        SelectedColor = initialColor;

        // Find the initial color in the palette (or default to 0)
        for (var i = 0; i < Palette.Length; i++)
        {
            if (Palette[i] == initialColor) { _paletteIndex = i; break; }
        }
    }

    /// <summary>Gets or sets the currently selected color.</summary>
    public Color SelectedColor { get; set; }

    // ─── Palette Generation ──────────────────────────────────────────

    private static Color[] BuildPalette()
    {
        var colors = new Color[PaletteCols * PaletteRows];

        // Row 0: 16 basic terminal colors
        colors[0] = Color.Black;
        colors[1] = new Color(128, 0, 0);
        colors[2] = new Color(0, 128, 0);
        colors[3] = new Color(128, 128, 0);
        colors[4] = new Color(0, 0, 128);
        colors[5] = new Color(128, 0, 128);
        colors[6] = new Color(0, 128, 128);
        colors[7] = Color.Gray;
        colors[8] = Color.DarkGray;
        colors[9] = Color.Red;
        colors[10] = Color.Green;
        colors[11] = Color.Yellow;
        colors[12] = Color.Blue;
        colors[13] = Color.Magenta;
        colors[14] = Color.Cyan;
        colors[15] = Color.White;

        // Rows 1-5: Color ramps (red, green, blue, yellow, cyan shades — 16 shades each)
        var ramps = new[]
        {
            (r: 1.0, g: 0.0, b: 0.0), // red ramp
            (r: 0.0, g: 1.0, b: 0.0), // green ramp
            (r: 0.3, g: 0.5, b: 1.0), // blue ramp
            (r: 1.0, g: 0.8, b: 0.0), // yellow/orange ramp
            (r: 0.0, g: 0.8, b: 0.8), // cyan/teal ramp
        };

        for (var row = 0; row < 5; row++)
        {
            var (r, g, b) = ramps[row];
            for (var col = 0; col < PaletteCols; col++)
            {
                var t = (col + 1.0) / PaletteCols;
                colors[(row + 1) * PaletteCols + col] = new Color(
                    (byte)(r * t * 255),
                    (byte)(g * t * 255),
                    (byte)(b * t * 255));
            }
        }

        return colors;
    }

    // ─── Rendering ───────────────────────────────────────────────────

    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;

        buffer.FillRect(clipped, new Cell(' ', Foreground, Background));

        // Border
        var border = Styling.BorderStyle.Rounded(Foreground);
        DrawBorder(buffer, bounds, border.Chars);

        // Title
        var titleText = $" {Title} ";
        var titleX = bounds.X + (bounds.Width - titleText.Length) / 2;
        for (var i = 0; i < titleText.Length; i++)
            SetCharSafe(buffer, titleX + i, bounds.Y, titleText[i], Foreground, Background);

        var innerX = bounds.X + 1;

        // Preview row
        var previewY = bounds.Y + 1;
        var previewLabel = "Current: ";
        for (var i = 0; i < previewLabel.Length; i++)
            SetCharSafe(buffer, innerX + i, previewY, previewLabel[i], Foreground, Background);
        SetCharSafe(buffer, innerX + previewLabel.Length, previewY, '\u2588', SelectedColor, Background);
        SetCharSafe(buffer, innerX + previewLabel.Length + 1, previewY, '\u2588', SelectedColor, Background);
        var hexText = " " + SelectedColor.ToHex();
        for (var i = 0; i < hexText.Length; i++)
            SetCharSafe(buffer, innerX + previewLabel.Length + 2 + i, previewY, hexText[i], Foreground, Background);

        // Palette grid using half-block rendering for 2x vertical resolution
        var paletteStartY = bounds.Y + 3;
        var gridW = Math.Min(PaletteCols * 2, bounds.Width - 2);
        var gridH = Math.Min(PaletteRows, bounds.Height - 6); // leave room for hex + buttons

        for (var cellY = 0; cellY < gridH; cellY++)
        {
            var y = paletteStartY + cellY;
            if (y >= bounds.Bottom - 3) break;

            for (var cellX = 0; cellX < gridW; cellX++)
            {
                var x = innerX + cellX;

                // Map cell to palette: each palette entry = 2 chars wide, 2 pixel rows (1 cell) tall
                var palCol = cellX / 2;
                var palRow0 = cellY * 2;
                var palRow1 = cellY * 2 + 1;

                var idx0 = palRow0 < PaletteRows ? palRow0 * PaletteCols + Math.Min(palCol, PaletteCols - 1) : -1;
                var idx1 = palRow1 < PaletteRows ? palRow1 * PaletteCols + Math.Min(palCol, PaletteCols - 1) : -1;

                var topColor = idx0 >= 0 && idx0 < Palette.Length ? Palette[idx0] : Background;
                var bottomColor = idx1 >= 0 && idx1 < Palette.Length ? Palette[idx1] : Background;

                // Highlight the selected palette cell
                var isTopHighlighted = idx0 == _paletteIndex;
                var isBotHighlighted = idx1 == _paletteIndex;

                if (isTopHighlighted) topColor = InvertForHighlight(topColor);
                if (isBotHighlighted) bottomColor = InvertForHighlight(bottomColor);

                if (topColor == bottomColor)
                    SetCharSafe(buffer, x, y, '\u2588', topColor, topColor);
                else
                    SetCharSafe(buffer, x, y, '\u2580', topColor, bottomColor);
            }
        }

        // Hex entry row
        var hexY = bounds.Bottom - 3;
        var hexLabel = _isEditingHex ? $"Hex: #{_hexBuffer}_" : $"Hex: {SelectedColor.ToHex()}";
        for (var i = 0; i < hexLabel.Length && innerX + i < bounds.Right - 1; i++)
            SetCharSafe(buffer, innerX + i, hexY, hexLabel[i], Foreground, Background);

        // Buttons
        var btnY = bounds.Bottom - 2;
        var okText = "[ OK ]";
        var cancelText = "[ Cancel ]";
        var btnX = bounds.X + bounds.Width / 2 - (okText.Length + cancelText.Length + 4) / 2;
        for (var i = 0; i < okText.Length; i++)
            SetCharSafe(buffer, btnX + i, btnY, okText[i], Foreground, Background);
        for (var i = 0; i < cancelText.Length; i++)
            SetCharSafe(buffer, btnX + okText.Length + 4 + i, btnY, cancelText[i], Foreground, Background);
    }

    // ─── Input ───────────────────────────────────────────────────────

    public override void OnKeyEvent(KeyEvent e)
    {
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

        // Palette navigation
        switch (e.Key)
        {
            case ConsoleKey.LeftArrow:
                _paletteIndex = Math.Max(0, _paletteIndex - 1);
                SelectedColor = Palette[_paletteIndex];
                break;
            case ConsoleKey.RightArrow:
                _paletteIndex = Math.Min(Palette.Length - 1, _paletteIndex + 1);
                SelectedColor = Palette[_paletteIndex];
                break;
            case ConsoleKey.UpArrow:
                if (_paletteIndex >= PaletteCols) _paletteIndex -= PaletteCols;
                SelectedColor = Palette[_paletteIndex];
                break;
            case ConsoleKey.DownArrow:
                if (_paletteIndex + PaletteCols < Palette.Length) _paletteIndex += PaletteCols;
                SelectedColor = Palette[_paletteIndex];
                break;
            case ConsoleKey.Enter:
                DialogResult = true;
                return;
            case ConsoleKey.Escape:
                DialogResult = false;
                return;
        }

        InvalidateVisual();
    }

    // ─── Static API ──────────────────────────────────────────────────

    /// <summary>
    /// Shows the color picker dialog and returns the selected color, or null if cancelled.
    /// </summary>
    public static async Task<Color?> ShowAsync(Color initialColor)
    {
        var dialog = new ColorPickerDialog(initialColor);
        var result = await dialog.ShowDialogAsync();
        return result == true ? dialog.SelectedColor : null;
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static Color InvertForHighlight(Color c) =>
        new((byte)(255 - c.R), (byte)(255 - c.G), (byte)(255 - c.B));

    private static void SetCharSafe(CellBuffer buf, int x, int y, char c, Color fg, Color bg)
    {
        if (x >= 0 && x < buf.Width && y >= 0 && y < buf.Height) buf.SetChar(x, y, c, fg, bg);
    }

    private void DrawBorder(CellBuffer buf, Rect b, Styling.BorderChars ch)
    {
        for (var x = b.X + 1; x < b.Right - 1; x++) { SetCharSafe(buf, x, b.Y, ch.Horizontal, Foreground, Background); SetCharSafe(buf, x, b.Bottom - 1, ch.Horizontal, Foreground, Background); }
        for (var y = b.Y + 1; y < b.Bottom - 1; y++) { SetCharSafe(buf, b.X, y, ch.Vertical, Foreground, Background); SetCharSafe(buf, b.Right - 1, y, ch.Vertical, Foreground, Background); }
        SetCharSafe(buf, b.X, b.Y, ch.TopLeft, Foreground, Background);
        SetCharSafe(buf, b.Right - 1, b.Y, ch.TopRight, Foreground, Background);
        SetCharSafe(buf, b.X, b.Bottom - 1, ch.BottomLeft, Foreground, Background);
        SetCharSafe(buf, b.Right - 1, b.Bottom - 1, ch.BottomRight, Foreground, Background);
    }
}
