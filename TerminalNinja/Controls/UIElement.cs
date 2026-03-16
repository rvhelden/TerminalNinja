using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Base class for all UI elements that participate in layout, rendering, and input.
/// Provides invalidation, property change helpers, visibility, enabled state,
/// focus management, and input event handling — matching WPF's UIElement responsibilities.
/// </summary>
public abstract class UIElement : Visual
{
    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty VisibilityProperty =
        DependencyProperty.Register(nameof(Visibility), typeof(Visibility), typeof(UIElement),
            new FrameworkPropertyMetadata(Visibility.Visible, affectsRender: true));

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.Register(nameof(IsEnabled), typeof(bool), typeof(UIElement),
            new FrameworkPropertyMetadata(true, affectsRender: true));

    public static readonly DependencyProperty FocusableProperty =
        DependencyProperty.Register(nameof(Focusable), typeof(bool), typeof(UIElement),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsFocusedProperty =
        DependencyProperty.Register(nameof(IsFocused), typeof(bool), typeof(UIElement),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsMouseOverProperty =
        DependencyProperty.Register(nameof(IsMouseOver), typeof(bool), typeof(UIElement),
            new PropertyMetadata(false));

    /// <summary>
    /// Gets or sets the visibility of this element.
    /// </summary>
    public Visibility Visibility
    {
        get => (Visibility)GetValue(VisibilityProperty)!;
        set => SetValue(VisibilityProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this element is enabled for interaction.
    /// </summary>
    public bool IsEnabled
    {
        get => (bool)GetValue(IsEnabledProperty)!;
        set => SetValue(IsEnabledProperty, value);
    }

    // ─── Focus ───────────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets whether this element can receive keyboard focus.
    /// In WPF this lives on UIElement. Default is <c>false</c>;
    /// <see cref="Control"/> overrides the default to <c>true</c>.
    /// </summary>
    public bool Focusable
    {
        get => (bool)GetValue(FocusableProperty)!;
        set => SetValue(FocusableProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this element currently has keyboard focus.
    /// Managed by <see cref="FocusManager"/> — controls should not set this directly.
    /// </summary>
    public bool IsFocused
    {
        get => (bool)GetValue(IsFocusedProperty)!;
        set => SetValue(IsFocusedProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the mouse is currently over this element.
    /// Managed by <see cref="FocusManager"/> — controls should not set this directly.
    /// </summary>
    public bool IsMouseOver
    {
        get => (bool)GetValue(IsMouseOverProperty)!;
        set => SetValue(IsMouseOverProperty, value);
    }

    // ─── Input event callbacks ───────────────────────────────────────

    /// <summary>Called when this element receives keyboard focus.</summary>
    public virtual void OnGotFocus() { }

    /// <summary>Called when this element loses keyboard focus.</summary>
    public virtual void OnLostFocus() { }

    /// <summary>Called when the mouse cursor enters this element's bounds.</summary>
    public virtual void OnMouseEnter() { }

    /// <summary>Called when the mouse cursor leaves this element's bounds.</summary>
    public virtual void OnMouseLeave() { }

    /// <summary>
    /// Handles keyboard input when this element has focus.
    /// </summary>
    /// <param name="e">The keyboard event data.</param>
    public virtual void OnKeyEvent(KeyEvent e) { }

    /// <summary>
    /// Handles mouse events that occur within this element's bounds.
    /// </summary>
    /// <param name="e">The mouse event data.</param>
    public virtual void OnMouseEvent(MouseEvent e) { }

    // ─── Hit testing ─────────────────────────────────────────────────

    /// <summary>
    /// Tests if the specified point (in absolute screen coordinates) is within this element's bounds.
    /// </summary>
    /// <param name="x">The absolute X coordinate to test.</param>
    /// <param name="y">The absolute Y coordinate to test.</param>
    /// <param name="parentBounds">The parent container's bounds for calculating absolute position.</param>
    /// <returns>The element's absolute bounds if the point is inside, null otherwise.</returns>
    public virtual Rect? HitTest(int x, int y, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        return bounds.Contains(x, y) ? bounds : null;
    }

    // ─── Invalidation ────────────────────────────────────────────────

    /// <summary>
    /// Gets or sets the callback invoked when this element needs to be re-rendered.
    /// Set by the Application when the element joins the visual tree.
    /// </summary>
    public Action? InvalidationCallback { get; set; }

    /// <summary>
    /// Signals that this element needs to be re-rendered.
    /// </summary>
    public void InvalidateVisual()
    {
        InvalidationCallback?.Invoke();
    }

    /// <inheritdoc />
    protected override void OnPropertyAffectsRender(DependencyProperty dp) => InvalidateVisual();

    // ─── Abstract layout / rendering ─────────────────────────────────

    /// <summary>
    /// Returns the element's preferred size within the given parent bounds.
    /// Used by layout containers to determine Auto-sized children.
    /// </summary>
    public abstract Size2D GetPreferredSize(Rect parent);

    /// <summary>
    /// Calculates the absolute bounds of this element within the parent bounds.
    /// </summary>
    public abstract Rect CalculateBounds(Rect parent);

    /// <summary>
    /// Renders this element to the specified cell buffer.
    /// </summary>
    public abstract void Render(CellBuffer buffer, Rect parentBounds);
}
