using TerminalNinja.Core.Ansi;
using TerminalNinja.Core.Buffers;
using TerminalNinja.Core.Console;
using TerminalNinja.Core.Elements;
using TerminalNinja.Core.Primitives;

namespace TerminalNinja.Core.Rendering;

/// <summary>
/// Main renderer that orchestrates the rendering pipeline with zero-allocation diffing.
/// </summary>
public sealed class Renderer : IDisposable
{
    private readonly CellBuffer _buffer;
    private readonly AnsiWriter _writer;
    private readonly TerminalGuard _guard;
    private bool _disposed;
    
    /// <summary>Gets the width of the rendering viewport.</summary>
    public int Width => _buffer.Width;
    
    /// <summary>Gets the height of the rendering viewport.</summary>
    public int Height => _buffer.Height;
    
    /// <summary>Gets the viewport rectangle (full screen).</summary>
    public Rect Viewport => new(0, 0, Width, Height);
    
    /// <summary>
    /// Creates a new renderer and initializes the terminal for rendering.
    /// </summary>
    public Renderer()
    {
        var stdout = Terminal.OpenStdout();
        _writer = new AnsiWriter(stdout);
        _guard = TerminalGuard.Enter(_writer);
        _buffer = new CellBuffer(Terminal.Width, Terminal.Height);
    }
    
    /// <summary>
    /// Clears the entire rendering buffer.
    /// </summary>
    public void Clear()
    {
        _buffer.Clear();
    }
    
    /// <summary>
    /// Draws a rectangle element to the buffer (does not display until Present() is called).
    /// </summary>
    /// <param name="element">The rectangle element to draw.</param>
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
    /// Handles terminal resize events by recreating the buffer.
    /// </summary>
    public void HandleResize()
    {
        var newWidth = Terminal.Width;
        var newHeight = Terminal.Height;
        
        if (newWidth != Width || newHeight != Height)
        {
            _buffer.Resize(newWidth, newHeight);
            _writer.ClearScreen();
        }
    }
    
    /// <summary>
    /// Disposes the renderer and restores the terminal to its original state.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _guard.Dispose();
        _writer.Dispose();
    }
}
