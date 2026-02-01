using TerminalNinja.Ansi;
using TerminalNinja.Buffers;
using TerminalNinja.Console;
using TerminalNinja.Elements;
using TerminalNinja.Primitives;

namespace TerminalNinja.Rendering;

/// <summary>
/// Main renderer that orchestrates the rendering pipeline with zero-allocation diffing.
/// </summary>
public sealed class Renderer : IDisposable
{
    private readonly CellBuffer _buffer;
    private readonly AnsiWriter _writer;
    private readonly TerminalGuard? _guard;
    private readonly ITerminal? _terminal;
    private bool _disposed;
    
    /// <summary>Gets the width of the rendering viewport.</summary>
    public int Width => _buffer.Width;
    
    /// <summary>Gets the height of the rendering viewport.</summary>
    public int Height => _buffer.Height;
    
    /// <summary>Gets the viewport rectangle (full screen).</summary>
    public Rect Viewport => new(0, 0, Width, Height);
    
    /// <summary>
    /// Creates a new renderer using the system terminal (production use).
    /// </summary>
    public Renderer() : this(SystemTerminal.Instance)
    {
    }
    
    /// <summary>
    /// Creates a new renderer with dependency injection for testing.
    /// </summary>
    /// <param name="terminal">The terminal abstraction to use.</param>
    public Renderer(ITerminal terminal)
    {
        _terminal = terminal;
        var stdout = terminal.OpenOutput();
        _writer = new AnsiWriter(stdout);
        _guard = TerminalGuard.Enter(_writer, terminal);
        _buffer = new CellBuffer(terminal.Width, terminal.Height);
    }
    
    /// <summary>
    /// Creates a test renderer with explicit stream and dimensions (no terminal interaction).
    /// </summary>
    internal Renderer(Stream output, int width, int height)
    {
        _writer = new AnsiWriter(output);
        _buffer = new CellBuffer(width, height);
    }
    
    /// <summary>
    /// Clears the entire rendering buffer.
    /// </summary>
    public void Clear()
    {
        _buffer.Clear();
    }
    
    /// <summary>
    /// Draws an element to the buffer (does not display until Present() is called).
    /// </summary>
    /// <param name="element">The element to draw.</param>
    public void Draw(IElement element)
    {
        element.Render(_buffer, Viewport);
    }
    
    /// <summary>
    /// Draws a rectangle element to the buffer (does not display until Present() is called).
    /// </summary>
    /// <param name="element">The rectangle element to draw.</param>
    [Obsolete("Use Draw(IElement) instead for better flexibility")]
    public void Draw(Rectangle element)
    {
        element.Render(_buffer, Viewport);
    }
    
    /// <summary>
    /// Presents the rendered frame to the terminal with zero-allocation diffing.
    /// Only changed cells are transmitted to the terminal.
    /// </summary>
    public void Present()
    {
        // Zero-allocation iteration using struct enumerator
        foreach (var change in _buffer.GetChanges())
        {
            _writer.WriteCell(change.X, change.Y, change.Cell);
        }
        
        _writer.Flush();
        _buffer.SwapBuffers();
    }
    
    /// <summary>
    /// Resizes the renderer to the specified dimensions.
    /// </summary>
    /// <param name="newWidth">The new width in columns.</param>
    /// <param name="newHeight">The new height in rows.</param>
    public void Resize(int newWidth, int newHeight)
    {
        if (newWidth == Width && newHeight == Height)
            return; // No change needed
        
        _buffer.Resize(newWidth, newHeight);
        _writer.ClearScreen();
    }
    
    /// <summary>
    /// Dumps the current screen buffer contents to a file for debugging.
    /// The dump includes a human-readable representation of the screen with color information.
    /// </summary>
    /// <param name="filePath">The path where the dump file will be saved. If null, uses a timestamped filename.</param>
    /// <returns>The path to the created dump file.</returns>
    public string DumpScreen(string? filePath = null)
    {
        // Generate default filename if not provided
        if (string.IsNullOrEmpty(filePath))
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            filePath = $"screen_dump_{timestamp}.txt";
        }
        
        _buffer.SwapBuffers(); // Ensure we dump the latest buffer
        // Get the dump string from the buffer
        var dumpContent = _buffer.DumpToString();
        _buffer.SwapBuffers(); // Ensure we dump the latest buffer
        
        // Write to file
        File.WriteAllText(filePath, dumpContent);
        
        return filePath;
    }
    
    /// <summary>
    /// Handles terminal resize events by recreating the buffer.
    /// </summary>
    public void HandleResize()
    {
        if (_terminal == null)
            return; // Test renderer, no resize support
            
        var newWidth = _terminal.Width;
        var newHeight = _terminal.Height;
        
        Resize(newWidth, newHeight);
    }
    
    /// <summary>
    /// Disposes the renderer and restores the terminal to its original state.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _guard?.Dispose();
        _writer.Dispose();
    }
}
