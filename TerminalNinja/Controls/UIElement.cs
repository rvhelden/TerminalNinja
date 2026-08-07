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
    /// <returns>
    /// True if the element consumed the key, which stops it reaching the application's global
    /// shortcut handler. False — the default — lets it through.
    /// </returns>
    /// <remarks>
    /// Returning a verdict is what makes a focused text field safe. Keys reach the focused element
    /// first and the application's <c>KeyDown</c> hook second, so an unclaimed key still triggers a
    /// global shortcut, while a <see cref="TextBox"/> holding focus swallows the letters it is
    /// being typed into instead of firing "q for quit" on the fourth character of "query".
    ///
    /// Claim only what was acted on. A control that returns true for every key it is offered
    /// silently disables every shortcut in the application while it holds focus.
    /// </remarks>
    public virtual bool OnKeyEvent(KeyEvent e) => false;

    /// <summary>
    /// Whether this element is currently accepting typed characters, so the application should
    /// offer it printable keys before its own shortcuts. Defaults to false.
    /// </summary>
    /// <remarks>
    /// A terminal application's shortcuts are usually bare letters, and a bare letter is also what
    /// someone types into a field. Without this the two are indistinguishable: typing "query" into
    /// a focused box fires whatever "q" is bound to, which is why an inline input previously had to
    /// be a modal — the application could only tell the difference by knowing a dialog was open.
    ///
    /// Only printable characters with no Ctrl or Alt are diverted. Arrows, Enter, Escape, Tab and
    /// every chord still reach the application first, so a text field cannot capture the shortcuts
    /// that close or commit it.
    /// </remarks>
    public virtual bool WantsTextInput => false;

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
    /// Renders this element to the specified cell buffer, honoring <see cref="Visibility"/>.
    /// <see cref="Visibility.Hidden"/> and <see cref="Visibility.Collapsed"/> both skip the
    /// render entirely; the difference is in layout, which is the panel's responsibility
    /// (a <see cref="Collapsed"/> child should also be treated as zero-size when sizing its
    /// siblings — see <c>StackPanel.CalculateChildSizes</c>).
    /// </summary>
    /// <remarks>
    /// Subclasses override <see cref="OnRender"/> instead of this method — the visibility
    /// short-circuit lives here so call sites never need to gate manually.
    /// </remarks>
    public void Render(CellBuffer buffer, Rect parentBounds)
    {
        if (Visibility != Visibility.Visible)
        {
            return;
        }
        OnRender(buffer, parentBounds);
    }

    /// <summary>
    /// Renders this element's content. Called by the public <see cref="Render"/> wrapper
    /// after the <see cref="Visibility"/> check passes; subclasses implement their painting
    /// here. Calling <c>base.OnRender</c> from an override bypasses the visibility check
    /// because the caller already cleared it.
    /// </summary>
    protected abstract void OnRender(CellBuffer buffer, Rect parentBounds);
}
