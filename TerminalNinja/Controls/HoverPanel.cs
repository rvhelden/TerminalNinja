using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Controls.Primitives;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A floating tooltip-style overlay that displays any <see cref="UIElement"/>
/// anchored at a single viewport cell. Designed for mouse-driven hover info —
/// hover content (a signature line, a type+value pair, a small composition of
/// TextBlocks, etc.) is set as <see cref="Content"/>, and the panel is shown
/// at the cell under the mouse via <see cref="ShowAt"/> / <see cref="Hide"/>.
/// </summary>
/// <remarks>
/// <para>
/// HoverPanel is a peer of <see cref="Window"/> and
/// <see cref="Controls.Primitives.Popup"/> — it is a non-visual element in
/// the logical tree (zero inline size) and uses
/// <see cref="App.Application.PushOverlay(UIElement, bool, bool)"/> with
/// <c>isModal=false</c> and <c>dimBackground=false</c> to render its content
/// on top of the rest of the visual tree.
/// </para>
/// <para>
/// Positioning is point-anchored (<see cref="AnchorX"/>, <see cref="AnchorY"/>)
/// rather than element-anchored: a hover target is usually a mouse cell
/// coordinate rather than an existing UI element. The panel flips above the
/// anchor automatically when <see cref="PlacementMode.Bottom"/> would overflow
/// the viewport.
/// </para>
/// </remarks>
[ContentProperty("Content")]
public class HoverPanel : FrameworkElement
{
    private readonly HoverPanelRoot _root = new();
    private bool _isOnOverlayStack;

    // ─── Dependency Properties ───────────────────────────────────────

    /// <summary>Identifies the <see cref="IsOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(HoverPanel),
            new PropertyMetadata(false, OnIsOpenChanged));

    /// <summary>Identifies the <see cref="Content"/> dependency property.</summary>
    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content), typeof(UIElement), typeof(HoverPanel),
            new PropertyMetadata((object?)null, OnContentChanged));

    /// <summary>Identifies the <see cref="AnchorX"/> dependency property.</summary>
    public static readonly DependencyProperty AnchorXProperty =
        DependencyProperty.Register(nameof(AnchorX), typeof(int), typeof(HoverPanel),
            new PropertyMetadata(0));

    /// <summary>Identifies the <see cref="AnchorY"/> dependency property.</summary>
    public static readonly DependencyProperty AnchorYProperty =
        DependencyProperty.Register(nameof(AnchorY), typeof(int), typeof(HoverPanel),
            new PropertyMetadata(0));

    /// <summary>Identifies the <see cref="Placement"/> dependency property.</summary>
    public static readonly DependencyProperty PlacementProperty =
        DependencyProperty.Register(nameof(Placement), typeof(PlacementMode), typeof(HoverPanel),
            new PropertyMetadata(PlacementMode.Bottom));

    /// <summary>Identifies the <see cref="HorizontalOffset"/> dependency property.</summary>
    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(nameof(HorizontalOffset), typeof(int), typeof(HoverPanel),
            new PropertyMetadata(0));

    /// <summary>Identifies the <see cref="VerticalOffset"/> dependency property.</summary>
    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(nameof(VerticalOffset), typeof(int), typeof(HoverPanel),
            new PropertyMetadata(0));

    // ─── CLR Wrappers ────────────────────────────────────────────────

    /// <summary>True when the panel is on the overlay stack.</summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty)!;
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>The element rendered inside the panel. Can be any composition.</summary>
    public UIElement? Content
    {
        get => (UIElement?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>The viewport-cell X coordinate of the anchor.</summary>
    public int AnchorX
    {
        get => (int)GetValue(AnchorXProperty)!;
        set => SetValue(AnchorXProperty, value);
    }

    /// <summary>The viewport-cell Y coordinate of the anchor.</summary>
    public int AnchorY
    {
        get => (int)GetValue(AnchorYProperty)!;
        set => SetValue(AnchorYProperty, value);
    }

    /// <summary>Where the panel sits relative to the anchor cell.</summary>
    public PlacementMode Placement
    {
        get => (PlacementMode)GetValue(PlacementProperty)!;
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>Extra horizontal nudge after placement.</summary>
    public int HorizontalOffset
    {
        get => (int)GetValue(HorizontalOffsetProperty)!;
        set => SetValue(HorizontalOffsetProperty, value);
    }

    /// <summary>Extra vertical nudge after placement.</summary>
    public int VerticalOffset
    {
        get => (int)GetValue(VerticalOffsetProperty)!;
        set => SetValue(VerticalOffsetProperty, value);
    }

    // ─── Events ──────────────────────────────────────────────────────

    /// <summary>Raised when the panel is opened.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised when the panel is closed.</summary>
    public event EventHandler? Closed;

    // ─── Imperative API ──────────────────────────────────────────────

    /// <summary>
    /// Show the panel at the given viewport cell with the given content.
    /// Equivalent to setting <see cref="Content"/>, <see cref="AnchorX"/> /
    /// <see cref="AnchorY"/>, and <see cref="IsOpen"/>.
    /// </summary>
    public void ShowAt(int x, int y, UIElement content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
        AnchorX = x;
        AnchorY = y;
        IsOpen = true;
    }

    /// <summary>Hide the panel. Equivalent to <c>IsOpen = false</c>.</summary>
    public void Hide() => IsOpen = false;

    // ─── Layout / Render ─────────────────────────────────────────────

    /// <summary>HoverPanel is invisible in the main visual tree — always zero size.</summary>
    public override Size2D GetPreferredSize(Rect parent) => new(0, 0);

    /// <summary>HoverPanel is invisible in the main visual tree — always zero bounds.</summary>
    public override Rect CalculateBounds(Rect parent) => new(parent.X, parent.Y, 0, 0);

    /// <summary>HoverPanel renders nothing inline — content is drawn via the overlay stack.</summary>
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        // Intentionally empty.
    }

    // ─── Open / Close ────────────────────────────────────────────────

    private void Open()
    {
        var app = App.Application.Current;
        if (app == null || _isOnOverlayStack) return;

        SyncRootProperties();
        app.PushOverlay(_root, isModal: false, dimBackground: false);
        _isOnOverlayStack = true;
        Opened?.Invoke(this, EventArgs.Empty);
    }

    private void Close()
    {
        var app = App.Application.Current;
        if (app == null || !_isOnOverlayStack) return;

        app.RemoveOverlay(_root);
        _isOnOverlayStack = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void SyncRootProperties()
    {
        _root.Child = Content;
        _root.AnchorX = AnchorX;
        _root.AnchorY = AnchorY;
        _root.Placement = Placement;
        _root.HorizontalOffset = HorizontalOffset;
        _root.VerticalOffset = VerticalOffset;
    }

    // ─── Property-Changed Callbacks ──────────────────────────────────

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not HoverPanel panel) return;
        if ((bool)e.NewValue!) panel.Open();
        else panel.Close();
    }

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not HoverPanel panel) return;
        panel._root.Child = e.NewValue as UIElement;
    }
}
