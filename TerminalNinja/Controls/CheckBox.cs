using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Controls.Primitives;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A toggle control that displays a check indicator and optional content.
/// Renders as <c>[x] Content</c> when checked or <c>[ ] Content</c> when unchecked.
/// Corresponds to WPF's System.Windows.Controls.CheckBox.
/// </summary>
[ContentProperty("Content")]
[RuntimeNameProperty("Name")]
public sealed class CheckBox : ButtonBase
{
    public CheckBox()
    {
        DefaultStyleKey = typeof(CheckBox);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty IsCheckedProperty =
        DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(CheckBox),
            new FrameworkPropertyMetadata(false, affectsRender: true,
                propertyChangedCallback: OnIsCheckedChanged));

    public static readonly DependencyProperty FocusColorProperty =
        DependencyProperty.Register(nameof(FocusColor), typeof(Color), typeof(CheckBox),
            new FrameworkPropertyMetadata(Color.Cyan, affectsRender: true));

    public static readonly DependencyProperty HoverColorProperty =
        DependencyProperty.Register(nameof(HoverColor), typeof(Color), typeof(CheckBox),
            new FrameworkPropertyMetadata(Color.Yellow, affectsRender: true));

    private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var cb = (CheckBox)d;
        if ((bool)e.NewValue!)
            cb.Checked?.Invoke();
        else
            cb.Unchecked?.Invoke();
    }

    // ─── CLR Wrappers ────────────────────────────────────────────────

    /// <summary>Gets or sets whether the checkbox is checked.</summary>
    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty)!;
        set => SetValue(IsCheckedProperty, value);
    }

    /// <summary>Gets or sets the border/indicator color when focused.</summary>
    public Color FocusColor
    {
        get => (Color)GetValue(FocusColorProperty)!;
        set => SetValue(FocusColorProperty, value);
    }

    /// <summary>Gets or sets the border/indicator color when hovered.</summary>
    public Color HoverColor
    {
        get => (Color)GetValue(HoverColorProperty)!;
        set => SetValue(HoverColorProperty, value);
    }

    // ─── Events ──────────────────────────────────────────────────────

    /// <summary>Raised when IsChecked becomes true.</summary>
    public event Action? Checked;

    /// <summary>Raised when IsChecked becomes false.</summary>
    public event Action? Unchecked;

    // ─── Toggle ──────────────────────────────────────────────────────

    private void Toggle()
    {
        if (!IsEnabled) return;
        IsChecked = !IsChecked;
        RaiseClick();
    }

    // ─── Layout ──────────────────────────────────────────────────────

    private const int IndicatorWidth = 4; // "[ ] "

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        var contentSize = base.GetPreferredSize(parent);
        return new Size2D(IndicatorWidth + contentSize.Width, Math.Max(1, contentSize.Height));
    }

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent)
    {
        var preferred = GetPreferredSize(parent);
        return ApplyAlignment(parent, preferred.Width, preferred.Height);
    }

    // ─── Rendering ───────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;

        var indicatorColor = IsFocused ? FocusColor : IsMouseOver ? HoverColor : Foreground;
        var fg = Foreground;
        if (!IsEnabled)
        {
            indicatorColor = DimColor(indicatorColor);
            fg = DimColor(fg);
        }

        var y = bounds.Y;

        // Draw indicator: [x] or [ ]
        if (bounds.X >= 0 && bounds.X < buffer.Width && y >= 0 && y < buffer.Height)
            buffer.SetChar(bounds.X, y, '[', indicatorColor, Background);
        if (bounds.X + 1 >= 0 && bounds.X + 1 < buffer.Width && y >= 0 && y < buffer.Height)
            buffer.SetChar(bounds.X + 1, y, IsChecked ? 'x' : ' ', fg, Background);
        if (bounds.X + 2 >= 0 && bounds.X + 2 < buffer.Width && y >= 0 && y < buffer.Height)
            buffer.SetChar(bounds.X + 2, y, ']', indicatorColor, Background);
        if (bounds.X + 3 >= 0 && bounds.X + 3 < buffer.Width && y >= 0 && y < buffer.Height)
            buffer.SetChar(bounds.X + 3, y, ' ', fg, Background);

        // Render content after indicator
        if (bounds.Width > IndicatorWidth)
        {
            var contentBounds = new Rect(bounds.X + IndicatorWidth, bounds.Y,
                bounds.Width - IndicatorWidth, bounds.Height);
            foreach (var (child, _) in base.GetChildrenWithBounds(contentBounds))
            {
                if (child is UIElement uiChild)
                    uiChild.Render(buffer, contentBounds);
            }
        }
    }

    // ─── Input ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public override bool OnKeyEvent(KeyEvent e)
    {
        if (e.Key is not (ConsoleKey.Enter or ConsoleKey.Spacebar))
        {
            return false;
        }

        Toggle();
        return true;
    }

    /// <inheritdoc />
    public override void OnMouseEvent(MouseEvent e)
    {
        if (e is { Action: MouseAction.Press, Button: MouseButton.Left })
            Toggle();
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static Color DimColor(Color c) =>
        new((byte)(c.R / 2), (byte)(c.G / 2), (byte)(c.B / 2));
}
