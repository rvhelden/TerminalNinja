using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Controls.Primitives;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A mutually exclusive toggle control within a group.
/// Renders as <c>(*) Content</c> when checked or <c>( ) Content</c> when unchecked.
/// RadioButtons with the same <see cref="GroupName"/> under the same parent are mutually exclusive.
/// Corresponds to WPF's System.Windows.Controls.RadioButton.
/// </summary>
[ContentProperty("Content")]
[RuntimeNameProperty("Name")]
public sealed class RadioButton : ButtonBase
{
    private bool _updatingGroup;

    public RadioButton()
    {
        DefaultStyleKey = typeof(RadioButton);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty IsCheckedProperty =
        DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(RadioButton),
            new FrameworkPropertyMetadata(false, affectsRender: true,
                propertyChangedCallback: OnIsCheckedChanged));

    public static readonly DependencyProperty GroupNameProperty =
        DependencyProperty.Register(nameof(GroupName), typeof(string), typeof(RadioButton),
            new PropertyMetadata(""));

    public static readonly DependencyProperty FocusColorProperty =
        DependencyProperty.Register(nameof(FocusColor), typeof(Color), typeof(RadioButton),
            new FrameworkPropertyMetadata(Color.Cyan, affectsRender: true));

    public static readonly DependencyProperty HoverColorProperty =
        DependencyProperty.Register(nameof(HoverColor), typeof(Color), typeof(RadioButton),
            new FrameworkPropertyMetadata(Color.Yellow, affectsRender: true));

    private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var rb = (RadioButton)d;
        if (rb._updatingGroup) return;

        if ((bool)e.NewValue!)
        {
            rb.UncheckSiblings();
            rb.Checked?.Invoke();
        }
        else
        {
            rb.Unchecked?.Invoke();
        }
    }

    // ─── CLR Wrappers ────────────────────────────────────────────────

    /// <summary>Gets or sets whether the radio button is checked.</summary>
    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty)!;
        set => SetValue(IsCheckedProperty, value);
    }

    /// <summary>Gets or sets the group name. RadioButtons with the same GroupName are mutually exclusive.</summary>
    public string GroupName
    {
        get => (string)GetValue(GroupNameProperty)!;
        set => SetValue(GroupNameProperty, value);
    }

    /// <summary>Gets or sets the indicator color when focused.</summary>
    public Color FocusColor
    {
        get => (Color)GetValue(FocusColorProperty)!;
        set => SetValue(FocusColorProperty, value);
    }

    /// <summary>Gets or sets the indicator color when hovered.</summary>
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

    // ─── Check ───────────────────────────────────────────────────────

    private void Check()
    {
        if (!IsEnabled) return;
        IsChecked = true;
        RaiseClick();
    }

    private void UncheckSiblings()
    {
        var parent = Parent;
        if (parent == null) return;

        IEnumerable<UIElement>? siblings = null;
        if (parent is Panel panel)
        {
            siblings = panel.Children;
        }
        else if (parent is FrameworkElement fe)
        {
            siblings = fe.GetLogicalChildren();
        }

        if (siblings == null) return;

        _updatingGroup = true;
        try
        {
            foreach (var sibling in siblings)
            {
                if (sibling is RadioButton rb && rb != this && rb.GroupName == GroupName)
                {
                    rb._updatingGroup = true;
                    try
                    {
                        rb.IsChecked = false;
                    }
                    finally
                    {
                        rb._updatingGroup = false;
                    }
                }
            }
        }
        finally
        {
            _updatingGroup = false;
        }
    }

    // ─── Layout ──────────────────────────────────────────────────────

    private const int IndicatorWidth = 4; // "( ) "

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

        // Draw indicator: (*) or ( )
        if (bounds.X >= 0 && bounds.X < buffer.Width && y >= 0 && y < buffer.Height)
            buffer.SetChar(bounds.X, y, '(', indicatorColor, Background);
        if (bounds.X + 1 >= 0 && bounds.X + 1 < buffer.Width && y >= 0 && y < buffer.Height)
            buffer.SetChar(bounds.X + 1, y, IsChecked ? '*' : ' ', fg, Background);
        if (bounds.X + 2 >= 0 && bounds.X + 2 < buffer.Width && y >= 0 && y < buffer.Height)
            buffer.SetChar(bounds.X + 2, y, ')', indicatorColor, Background);
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
        switch (e.Key)
        {
            case ConsoleKey.Enter or ConsoleKey.Spacebar:
                Check();
                return true;
            case ConsoleKey.DownArrow or ConsoleKey.RightArrow:
                FocusSibling(1);
                return true;
            case ConsoleKey.UpArrow or ConsoleKey.LeftArrow:
                FocusSibling(-1);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Moves focus to the next or previous RadioButton in the same group.
    /// </summary>
    private void FocusSibling(int direction)
    {
        var siblings = GetGroupSiblings();
        if (siblings.Count <= 1) return;

        var currentIdx = siblings.IndexOf(this);
        if (currentIdx < 0) return;

        var nextIdx = currentIdx + direction;
        if (nextIdx < 0) nextIdx = siblings.Count - 1;     // wrap to last
        else if (nextIdx >= siblings.Count) nextIdx = 0;    // wrap to first

        var target = siblings[nextIdx];
        App.Application.Current?.FocusManager.SetFocus(target);
    }

    private List<RadioButton> GetGroupSiblings()
    {
        var result = new List<RadioButton>();
        var parent = Parent;
        if (parent == null) return result;

        IEnumerable<UIElement>? children = parent is Panel panel ? panel.Children :
            parent is FrameworkElement fe ? fe.GetLogicalChildren() : null;

        if (children == null) return result;

        foreach (var child in children)
        {
            if (child is RadioButton rb && rb.GroupName == GroupName)
                result.Add(rb);
        }
        return result;
    }

    /// <inheritdoc />
    public override void OnMouseEvent(MouseEvent e)
    {
        if (e is { Action: MouseAction.Press, Button: MouseButton.Left })
            Check();
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static Color DimColor(Color c) =>
        new((byte)(c.R / 2), (byte)(c.G / 2), (byte)(c.B / 2));
}
