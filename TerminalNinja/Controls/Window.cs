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
    private string _title = "";
    
    /// <summary>
    /// Gets or sets the window title.
    /// </summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
    
    /// <summary>
    /// Gets or sets the width of the window.
    /// Use Size.Stretch (default) to fill available width.
    /// </summary>
    public Size Width { get; set; } = Size.Stretch;
    
    /// <summary>
    /// Gets or sets the height of the window.
    /// Use Size.Stretch (default) to fill available height.
    /// </summary>
    public Size Height { get; set; } = Size.Stretch;
    
    /// <summary>
    /// Returns the preferred size of the window content.
    /// </summary>
    public override Size2D GetPreferredSize(Rect parent)
    {
        if (Content == null)
            return new Size2D(0, 0);
        
        return Content.GetPreferredSize(parent);
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
    /// Window itself has no visual representation - it delegates to Content.
    /// </summary>
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        
        // Render content if present
        Content?.Render(buffer, bounds);
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
