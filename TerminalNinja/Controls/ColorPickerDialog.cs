using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A modal HSL color picker dialog with a hue bar, saturation/lightness gradient,
/// and hex entry. Rendered using half-block characters for high resolution.
/// <para>
/// Layout: hue bar (horizontal rainbow), SL gradient (2D rectangle at selected hue),
/// preview swatch + hex value, OK/Cancel buttons.
/// </para>
/// </summary>
public sealed class ColorPickerDialog : Window
{
    private double _hue;        // 0-360
    private double _saturation; // 0-1
    private double _lightness;  // 0-1
    private bool _hueMode = true; // true = adjusting hue bar, false = adjusting SL rect
    private string _hexBuffer = "";
    private bool _isEditingHex;

    private const int GradientWidth = 32;
    private const int GradientPixelHeight = 16; // pixels (renders as 8 cell rows via half-blocks)
    private const int GradientCellHeight = 8;

    public ColorPickerDialog() : this(Color.White) { }

    public ColorPickerDialog(Color initialColor)
    {
        DefaultStyleKey = typeof(ColorPickerDialog);
        Title = "Pick a Color";
        Width = Size.Absolute(GradientWidth + 4); // +2 border +2 padding
        Height = Size.Absolute(GradientCellHeight + 8); // gradient + hue bar + preview + buttons + borders
        SelectedColor = initialColor;

        // Decompose initial color to HSL
        ColorToHsl(initialColor, out _hue, out _saturation, out _lightness);
    }

    /// <summary>Gets or sets the currently selected color.</summary>
    public Color SelectedColor { get; set; }

    // ─── Rendering ───────────────────────────────────────────────────

    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;

        buffer.FillRect(clipped, new Cell(' ', Foreground, Background));

        var border = Styling.BorderStyle.Rounded(Foreground);
        DrawBorder(buffer, bounds, border.Chars);

        // Title
        var titleText = $" {Title} ";
        var titleX = bounds.X + (bounds.Width - titleText.Length) / 2;
        for (var i = 0; i < titleText.Length; i++)
            SetCharSafe(buffer, titleX + i, bounds.Y, titleText[i], Foreground, Background);

        var innerX = bounds.X + 1;
        var gradW = Math.Min(GradientWidth, bounds.Width - 2);

        // ── Hue bar (1 row, full spectrum) ──
        var hueY = bounds.Y + 1;
        for (var x = 0; x < gradW; x++)
        {
            var h = (double)x / gradW * 360.0;
            var color = HslToColor(h, 1.0, 0.5);
            var isSelected = _hueMode && Math.Abs(h - _hue) < (360.0 / gradW);
            var ch = isSelected ? '\u25BC' : '\u2588'; // ▼ indicator or █ bar
            SetCharSafe(buffer, innerX + x, hueY, ch, isSelected ? Foreground : color, isSelected ? color : Background);
        }

        // ── SL gradient (half-block rendered, X=saturation, Y=lightness) ──
        var gradStartY = hueY + 1;
        for (var cellY = 0; cellY < GradientCellHeight && gradStartY + cellY < bounds.Bottom - 3; cellY++)
        {
            for (var cellX = 0; cellX < gradW; cellX++)
            {
                var s = (double)cellX / gradW;
                var lTop = 1.0 - (double)(cellY * 2) / GradientPixelHeight;
                var lBot = 1.0 - (double)(cellY * 2 + 1) / GradientPixelHeight;

                var topColor = HslToColor(_hue, s, lTop);
                var bottomColor = HslToColor(_hue, s, lBot);

                // Highlight the selected position
                var selX = (int)(_saturation * gradW);
                var selPixelY = (int)((1.0 - _lightness) * GradientPixelHeight);
                var selCellY = selPixelY / 2;
                var isTopSel = !_hueMode && cellX == selX && cellY == selCellY && selPixelY % 2 == 0;
                var isBotSel = !_hueMode && cellX == selX && cellY == selCellY && selPixelY % 2 == 1;

                if (isTopSel) topColor = InvertForHighlight(topColor);
                if (isBotSel) bottomColor = InvertForHighlight(bottomColor);

                if (topColor == bottomColor)
                    SetCharSafe(buffer, innerX + cellX, gradStartY + cellY, '\u2588', topColor, topColor);
                else
                    SetCharSafe(buffer, innerX + cellX, gradStartY + cellY, '\u2580', topColor, bottomColor);
            }
        }

        // ── Preview + hex ──
        var previewY = gradStartY + GradientCellHeight;
        if (previewY < bounds.Bottom - 2)
        {
            SetCharSafe(buffer, innerX, previewY, '\u2588', SelectedColor, Background);
            SetCharSafe(buffer, innerX + 1, previewY, '\u2588', SelectedColor, Background);
            SetCharSafe(buffer, innerX + 2, previewY, ' ', Foreground, Background);

            var hexText = _isEditingHex ? "#" + _hexBuffer + "_" : SelectedColor.ToHex();
            for (var i = 0; i < hexText.Length && innerX + 3 + i < bounds.Right - 1; i++)
                SetCharSafe(buffer, innerX + 3 + i, previewY, hexText[i], Foreground, Background);

            // Mode indicator
            var modeText = _hueMode ? " [Hue]" : " [S/L]";
            var modeX = bounds.Right - 2 - modeText.Length;
            for (var i = 0; i < modeText.Length; i++)
                SetCharSafe(buffer, modeX + i, previewY, modeText[i], DimColor(Foreground), Background);
        }

        // ── Buttons ──
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
                ColorToHsl(SelectedColor, out _hue, out _saturation, out _lightness);
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

        switch (e.Key)
        {
            case ConsoleKey.Tab:
                _hueMode = !_hueMode;
                break;

            case ConsoleKey.LeftArrow:
                if (_hueMode)
                    _hue = (_hue - 5 + 360) % 360;
                else
                    _saturation = Math.Max(0, _saturation - 0.05);
                UpdateColorFromHsl();
                break;

            case ConsoleKey.RightArrow:
                if (_hueMode)
                    _hue = (_hue + 5) % 360;
                else
                    _saturation = Math.Min(1, _saturation + 0.05);
                UpdateColorFromHsl();
                break;

            case ConsoleKey.UpArrow:
                if (!_hueMode)
                    _lightness = Math.Min(1, _lightness + 0.05);
                UpdateColorFromHsl();
                break;

            case ConsoleKey.DownArrow:
                if (!_hueMode)
                    _lightness = Math.Max(0, _lightness - 0.05);
                UpdateColorFromHsl();
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

    private void UpdateColorFromHsl()
    {
        SelectedColor = HslToColor(_hue, _saturation, _lightness);
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

    // ─── HSL Conversion ──────────────────────────────────────────────

    private static Color HslToColor(double h, double s, double l)
    {
        if (s == 0)
        {
            var v = (byte)(l * 255);
            return new Color(v, v, v);
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        var hNorm = h / 360.0;

        var r = HueToRgb(p, q, hNorm + 1.0 / 3.0);
        var g = HueToRgb(p, q, hNorm);
        var b = HueToRgb(p, q, hNorm - 1.0 / 3.0);

        return new Color((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }

    private static void ColorToHsl(Color c, out double h, out double s, out double l)
    {
        var r = c.R / 255.0;
        var g = c.G / 255.0;
        var b = c.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        l = (max + min) / 2.0;

        if (delta == 0)
        {
            h = 0;
            s = 0;
            return;
        }

        s = l < 0.5 ? delta / (max + min) : delta / (2.0 - max - min);

        if (max == r)
            h = ((g - b) / delta + (g < b ? 6 : 0)) * 60;
        else if (max == g)
            h = ((b - r) / delta + 2) * 60;
        else
            h = ((r - g) / delta + 4) * 60;
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static Color InvertForHighlight(Color c) =>
        new((byte)(255 - c.R), (byte)(255 - c.G), (byte)(255 - c.B));

    private static Color DimColor(Color c) =>
        new((byte)(c.R / 2), (byte)(c.G / 2), (byte)(c.B / 2));

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
