using TerminalNinja.Buffers;
using TerminalNinja.Controls.Primitives;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A floating IntelliSense-style overlay: a list of <see cref="CompletionEntry"/>
/// rows with an icon + label on the left, and a details pane on the right
/// showing the focused item's signature and documentation. Peer of
/// <see cref="HoverPanel"/> and <see cref="Primitives.Popup"/> — pushed onto the
/// <see cref="App.Application"/> overlay stack while open.
/// </summary>
/// <remarks>
/// <para>
/// Designed for completion-popup-shaped UI: a callable (REPL, editor) opens it
/// at the current cursor, fills <see cref="Items"/> with entries it built from
/// its language service, and updates <see cref="SelectedIndex"/> as the user
/// arrows up / down. The panel re-renders the details pane on every selection
/// change.
/// </para>
/// <para>
/// CompletionPanel is intentionally self-contained: callers don't compose
/// child UIElements like they would for <see cref="HoverPanel"/>. The two-pane
/// layout, glyph rendering, and selection inversion are baked in so every
/// host gets a consistent IntelliSense surface.
/// </para>
/// </remarks>
public class CompletionPanel : FrameworkElement
{
    private readonly CompletionPanelRoot _root = new();
    private bool _isOnOverlayStack;

    // ─── Dependency Properties ───────────────────────────────────────

    /// <summary>Identifies the <see cref="IsOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(CompletionPanel),
            new PropertyMetadata(false, OnIsOpenChanged));

    /// <summary>Identifies the <see cref="Items"/> dependency property.</summary>
    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(IReadOnlyList<CompletionEntry>), typeof(CompletionPanel),
            new PropertyMetadata((object?)null, OnItemsChanged));

    /// <summary>Identifies the <see cref="SelectedIndex"/> dependency property.</summary>
    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(CompletionPanel),
            new PropertyMetadata(0, OnSelectedIndexChanged));

    /// <summary>Identifies the <see cref="AnchorX"/> dependency property.</summary>
    public static readonly DependencyProperty AnchorXProperty =
        DependencyProperty.Register(nameof(AnchorX), typeof(int), typeof(CompletionPanel),
            new PropertyMetadata(0));

    /// <summary>Identifies the <see cref="AnchorY"/> dependency property.</summary>
    public static readonly DependencyProperty AnchorYProperty =
        DependencyProperty.Register(nameof(AnchorY), typeof(int), typeof(CompletionPanel),
            new PropertyMetadata(0));

    /// <summary>Identifies the <see cref="Placement"/> dependency property.</summary>
    public static readonly DependencyProperty PlacementProperty =
        DependencyProperty.Register(nameof(Placement), typeof(PlacementMode), typeof(CompletionPanel),
            new PropertyMetadata(PlacementMode.Bottom));

    // ─── CLR Wrappers ────────────────────────────────────────────────

    /// <summary>True when the panel is on the overlay stack.</summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty)!;
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>The completion entries to show in the list pane.</summary>
    public IReadOnlyList<CompletionEntry>? Items
    {
        get => (IReadOnlyList<CompletionEntry>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>The currently focused index in <see cref="Items"/>. Drives the details pane.</summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty)!;
        set => SetValue(SelectedIndexProperty, value);
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

    // ─── Events ──────────────────────────────────────────────────────

    /// <summary>Raised when the panel is opened.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised when the panel is closed.</summary>
    public event EventHandler? Closed;

    // ─── Imperative API ──────────────────────────────────────────────

    /// <summary>
    /// Show the panel at the given viewport cell with the given items.
    /// Equivalent to setting <see cref="Items"/>, <see cref="SelectedIndex"/>,
    /// <see cref="AnchorX"/> / <see cref="AnchorY"/>, and <see cref="IsOpen"/>.
    /// </summary>
    public void ShowAt(int x, int y, IReadOnlyList<CompletionEntry> items, int selectedIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = items;
        SelectedIndex = selectedIndex;
        AnchorX = x;
        AnchorY = y;
        IsOpen = true;
    }

    /// <summary>Hide the panel. Equivalent to <c>IsOpen = false</c>.</summary>
    public void Hide() => IsOpen = false;

    // ─── Layout / Render ─────────────────────────────────────────────

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent) => new(0, 0);

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent) => new(parent.X, parent.Y, 0, 0);

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        // Intentionally empty — content lives on the overlay stack via _root.
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
        _root.Items = Items ?? Array.Empty<CompletionEntry>();
        _root.SelectedIndex = SelectedIndex;
        _root.AnchorX = AnchorX;
        _root.AnchorY = AnchorY;
        _root.Placement = Placement;
    }

    // ─── Property-Changed Callbacks ──────────────────────────────────

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CompletionPanel panel) return;
        if ((bool)e.NewValue!) panel.Open();
        else panel.Close();
    }

    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CompletionPanel panel) return;
        panel._root.Items = (IReadOnlyList<CompletionEntry>?)e.NewValue ?? Array.Empty<CompletionEntry>();
    }

    private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CompletionPanel panel) return;
        panel._root.SelectedIndex = (int)e.NewValue!;
    }
}
