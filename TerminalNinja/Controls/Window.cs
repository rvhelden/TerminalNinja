using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A top-level container control representing a window in the terminal UI.
/// Window is a ContentControl that holds Content and provides window-scoped resources.
/// </summary>
[ContentProperty("Content")]
[RuntimeNameProperty("Name")]
public class Window : ContentControl
{
    public Window()
    {
        DefaultStyleKey = typeof(Window);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(Window),
            new FrameworkPropertyMetadata("", affectsRender: true));

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(Size), typeof(Window),
            new FrameworkPropertyMetadata(Size.Stretch, affectsRender: true));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(nameof(Height), typeof(Size), typeof(Window),
            new FrameworkPropertyMetadata(Size.Stretch, affectsRender: true));

    /// <summary>
    /// Gets or sets the window title.
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the width of the window.
    /// Use Size.Stretch (default) to fill available width.
    /// </summary>
    public Size Width
    {
        get => (Size)GetValue(WidthProperty)!;
        set => SetValue(WidthProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the height of the window.
    /// Use Size.Stretch (default) to fill available height.
    /// </summary>
    public Size Height
    {
        get => (Size)GetValue(HeightProperty)!;
        set => SetValue(HeightProperty, value);
    }
    
    /// <summary>
    /// Returns the preferred size of the window content.
    /// </summary>
    public override Size2D GetPreferredSize(Rect parent)
    {
        if (!HasContent)
        {
            return new Size2D(0, 0);
        }

        return base.GetPreferredSize(parent);
    }
    
    /// <summary>
    /// Calculates the bounds of this window within the parent bounds.
    /// </summary>
    public override Rect CalculateBounds(Rect parent)
    {
        var w = Width.Resolve(parent.Width);
        var h = Height.Resolve(parent.Height);
        
        return new Rect(parent.X, parent.Y, w, h);
    }
    
    /// <summary>
    /// Renders the window content to the buffer.
    /// Window itself has no visual representation - it delegates to Content via ContentPresenter.
    /// </summary>
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        
        // Render content if present (delegated to ContentPresenter via base)
        if (HasContent)
        {
            base.Render(buffer, bounds);
        }
    }

    /// <summary>
    /// Shows this window by setting it as the root control of the current Application.
    /// </summary>
    public void Show()
    {
        var app = App.Application.Current;
        if (app != null)
        {
            app.RootControl = this;
        }
    }
    
    /// <summary>
    /// Closes this window.
    /// </summary>
    public void Close()
    {
        var app = App.Application.Current;
        if (app?.RootControl == this)
        {
            app.RootControl = null;
        }
    }
}
