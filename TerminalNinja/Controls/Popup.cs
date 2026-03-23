using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Displays content in a floating overlay above other controls.
/// The popup is positioned relative to a <see cref="PlacementTarget"/> using
/// the specified <see cref="Placement"/> mode and offsets.
/// <para>
/// Popup is a non-visual element in the logical tree — it has zero size and does
/// not render inline. When <see cref="IsOpen"/> is set to true, the popup pushes
/// its content onto the Application's overlay stack.
/// </para>
/// </summary>
[ContentProperty("Child")]
public class Popup : FrameworkElement
{
    private readonly PopupRoot _popupRoot = new();
    private bool _isOnOverlayStack;

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Popup),
            new PropertyMetadata(false, OnIsOpenChanged));

    public static readonly DependencyProperty ChildProperty =
        DependencyProperty.Register(nameof(Child), typeof(UIElement), typeof(Popup),
            new PropertyMetadata((object?)null, OnChildChanged));

    public static readonly DependencyProperty PlacementTargetProperty =
        DependencyProperty.Register(nameof(PlacementTarget), typeof(UIElement), typeof(Popup),
            new PropertyMetadata((object?)null));

    public static readonly DependencyProperty PlacementProperty =
        DependencyProperty.Register(nameof(Placement), typeof(PlacementMode), typeof(Popup),
            new PropertyMetadata(PlacementMode.Bottom));

    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(nameof(HorizontalOffset), typeof(int), typeof(Popup),
            new PropertyMetadata(0));

    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(nameof(VerticalOffset), typeof(int), typeof(Popup),
            new PropertyMetadata(0));

    public static readonly DependencyProperty StaysOpenProperty =
        DependencyProperty.Register(nameof(StaysOpen), typeof(bool), typeof(Popup),
            new PropertyMetadata(true));

    // ─── CLR Property Wrappers ───────────────────────────────────────

    /// <summary>
    /// Gets or sets whether the popup is currently open (visible).
    /// </summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty)!;
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// Gets or sets the content to display inside the popup.
    /// </summary>
    public UIElement? Child
    {
        get => (UIElement?)GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    /// <summary>
    /// Gets or sets the element that the popup is positioned relative to.
    /// If null, the popup uses its visual parent as the placement target.
    /// </summary>
    public UIElement? PlacementTarget
    {
        get => (UIElement?)GetValue(PlacementTargetProperty);
        set => SetValue(PlacementTargetProperty, value);
    }

    /// <summary>
    /// Gets or sets the position of the popup relative to the placement target.
    /// </summary>
    public PlacementMode Placement
    {
        get => (PlacementMode)GetValue(PlacementProperty)!;
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal offset from the calculated position.
    /// </summary>
    public int HorizontalOffset
    {
        get => (int)GetValue(HorizontalOffsetProperty)!;
        set => SetValue(HorizontalOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets the vertical offset from the calculated position.
    /// </summary>
    public int VerticalOffset
    {
        get => (int)GetValue(VerticalOffsetProperty)!;
        set => SetValue(VerticalOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the popup stays open when the user clicks outside it.
    /// When false, the popup implements "light-dismiss" behavior: clicking outside closes it.
    /// </summary>
    public bool StaysOpen
    {
        get => (bool)GetValue(StaysOpenProperty)!;
        set => SetValue(StaysOpenProperty, value);
    }

    // ─── Events ──────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the popup is opened.
    /// </summary>
    public event EventHandler? Opened;

    /// <summary>
    /// Raised when the popup is closed.
    /// </summary>
    public event EventHandler? Closed;

    // ─── Layout Overrides ────────────────────────────────────────────

    /// <summary>
    /// Popup is invisible in the main visual tree — it always reports zero size.
    /// </summary>
    public override Size2D GetPreferredSize(Rect parent) => new(0, 0);

    /// <summary>
    /// Popup is invisible in the main visual tree — it always reports zero bounds.
    /// </summary>
    public override Rect CalculateBounds(Rect parent) => new(parent.X, parent.Y, 0, 0);

    /// <summary>
    /// Popup does not render inline — its content is rendered via the overlay stack.
    /// </summary>
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        // Intentionally empty — the PopupRoot is rendered by Application's overlay loop
    }

    // ─── Open / Close Logic ──────────────────────────────────────────

    private void Open()
    {
        var app = App.Application.Current;
        if (app == null || _isOnOverlayStack)
        {
            return;
        }

        // Sync properties to PopupRoot
        _popupRoot.Placement = Placement;
        _popupRoot.HorizontalOffset = HorizontalOffset;
        _popupRoot.VerticalOffset = VerticalOffset;
        _popupRoot.PlacementTarget = GetEffectivePlacementTarget();
        _popupRoot.TargetBounds = ComputeTargetBounds(app);

        app.PushOverlay(_popupRoot, isModal: false, dimBackground: false);
        _isOnOverlayStack = true;

        Opened?.Invoke(this, EventArgs.Empty);
    }

    private void ClosePopup()
    {
        var app = App.Application.Current;
        if (app == null || !_isOnOverlayStack)
        {
            return;
        }

        app.RemoveOverlay(_popupRoot);
        _isOnOverlayStack = false;

        Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets the effective placement target: the explicit target if set,
    /// otherwise the popup's visual parent.
    /// </summary>
    private UIElement? GetEffectivePlacementTarget()
    {
        return PlacementTarget ?? Parent as UIElement;
    }

    /// <summary>
    /// Computes the viewport-space bounds of the placement target by walking
    /// the visual tree from root. If the target cannot be found, returns a
    /// zero-size rect at the viewport origin.
    /// </summary>
    private Rect ComputeTargetBounds(App.Application app)
    {
        var target = GetEffectivePlacementTarget();
        if (target == null || app.RootControl == null)
        {
            return new Rect(0, 0, 0, 0);
        }

        // Walk the visual tree to find the target's bounds
        var viewport = app.Renderer.Viewport;
        var found = FindElementBounds(app.RootControl, viewport, target);
        return found ?? new Rect(0, 0, 0, 0);
    }

    /// <summary>
    /// Recursively searches the visual tree for the target element and
    /// returns its computed bounds in viewport coordinates.
    /// </summary>
    private static Rect? FindElementBounds(UIElement element, Rect parentBounds, UIElement target)
    {
        var myBounds = element.CalculateBounds(parentBounds);

        if (element == target)
        {
            return myBounds;
        }

        foreach (var (child, childParentBounds) in element.GetChildrenWithBounds(myBounds))
        {
            if (child is UIElement childElement)
            {
                var result = FindElementBounds(childElement, childParentBounds, target);
                if (result.HasValue)
                {
                    return result;
                }
            }
        }

        return null;
    }

    // ─── Property Changed Callbacks ──────────────────────────────────

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup popup)
        {
            return;
        }

        var isOpen = (bool)e.NewValue!;
        if (isOpen)
        {
            popup.Open();
        }
        else
        {
            popup.ClosePopup();
        }
    }

    private static void OnChildChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup popup)
        {
            return;
        }

        popup._popupRoot.Child = e.NewValue as UIElement;
    }
}
