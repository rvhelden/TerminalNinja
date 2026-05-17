using TerminalNinja.Ansi;
using TerminalNinja.Buffers;
using TerminalNinja.Console;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;

namespace TerminalNinja.Rendering;

/// <summary>
/// Main renderer that orchestrates the rendering pipeline with zero-allocation diffing.
/// </summary>
public sealed class Renderer : IDisposable
{
    private readonly CellBuffer _buffer;
    private readonly ICellSink _sink;
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
        var writer = new AnsiWriter(stdout);
        _sink = writer;
        _guard = TerminalGuard.Enter(writer, terminal);
        _buffer = new CellBuffer(terminal.Width, terminal.Height);
    }

    /// <summary>
    /// Creates a renderer that writes to an arbitrary <see cref="ICellSink"/>.
    /// Used by tests and non-ANSI backends (e.g. a future GPU sink); no terminal interaction.
    /// </summary>
    /// <param name="sink">The destination sink for cell writes.</param>
    /// <param name="width">The viewport width in columns.</param>
    /// <param name="height">The viewport height in rows.</param>
    public Renderer(ICellSink sink, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
        _buffer = new CellBuffer(width, height);
    }

    /// <summary>
    /// Creates an offscreen renderer that writes ANSI sequences to the given stream.
    /// No terminal interaction (no cursor hide/show, no ANSI mode toggling).
    /// Useful for CLI snapshot tools, piping output, and WASM scenarios.
    /// </summary>
    /// <param name="output">The output stream to write ANSI sequences to.</param>
    /// <param name="width">The viewport width in columns.</param>
    /// <param name="height">The viewport height in rows.</param>
    /// <returns>A renderer that writes to the stream.</returns>
    public static Renderer CreateOffscreen(Stream output, int width, int height)
    {
        return new Renderer(output, width, height);
    }

    /// <summary>
    /// Creates a renderer that writes ANSI sequences to an arbitrary stream with explicit
    /// dimensions. Used by <see cref="CreateOffscreen"/> and by tests that need to inspect
    /// the rendered byte stream.
    /// </summary>
    internal Renderer(Stream output, int width, int height)
        : this(new AnsiWriter(output), width, height)
    {
    }
    
    /// <summary>
    /// Clears the entire rendering buffer.
    /// </summary>
    public void Clear()
    {
        _buffer.Clear();
    }
    
    /// <summary>
    /// Draws a UIElement to the buffer (does not display until Present() is called).
    /// Exposes the active sink to controls via <see cref="CellBuffer.ActiveSink"/> for the
    /// duration of the call so capability-aware controls (e.g. <c>TextBlock</c> when an
    /// <see cref="IShapedRunSink"/> is attached) can emit higher-level operations alongside
    /// their per-cell writes.
    /// </summary>
    /// <param name="control">The control to draw.</param>
    public void Draw(UIElement control)
    {
        _buffer.ActiveSink = _sink;
        try
        {
            control.Render(_buffer, Viewport);
        }
        finally
        {
            _buffer.ActiveSink = null;
        }
    }
    
    /// <summary>
    /// Dims the entire buffer to create a backdrop effect for modal overlays.
    /// Should be called after <see cref="Draw"/> and before drawing overlay content.
    /// </summary>
    public void DimBackground()
    {
        _buffer.DimAll();
    }
    
    /// <summary>
    /// Draws an overlay UIElement on top of the existing buffer content.
    /// Unlike <see cref="Draw"/>, this does NOT clear the buffer first —
    /// the overlay paints over whatever is already in the buffer.
    /// </summary>
    /// <param name="overlay">The overlay control to draw.</param>
    public void DrawOverlay(UIElement overlay)
    {
        _buffer.ActiveSink = _sink;
        try
        {
            overlay.Render(_buffer, Viewport);
        }
        finally
        {
            _buffer.ActiveSink = null;
        }
    }
    
    /// <summary>
    /// Presents the rendered frame to the sink with diffing.
    /// </summary>
    /// <remarks>
    /// Two paths depending on the sink's capabilities:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <see cref="IShapedRunSink"/> — walks dirty rows, groups contiguous cells with matching
    /// style into runs, and calls <c>WriteRun</c> per run. The sink does the entire job
    /// (background + glyph shaping + decorations) per run; <c>WriteCell</c> is not invoked.
    /// This is the path the GPU backend takes so HarfBuzz can shape whole runs and produce
    /// ligatures the per-cell path can't.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Plain <see cref="ICellSink"/> — the original per-cell diff loop. Optimal for byte-stream
    /// output (<see cref="Ansi.AnsiWriter"/>) where minimal cell emission matters more than
    /// per-row grouping.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    public void Present()
    {
        // BeginFrame invalidates per-frame state (e.g. cursor tracking on the ANSI sink)
        // so the first cell always emits absolute positioning. Without this, stale state
        // from the previous frame can cause rendering corruption (characters at wrong positions).
        _sink.BeginFrame();

        if (_sink is IShapedRunSink shaped)
        {
            PresentShaped(shaped);
        }
        else
        {
            foreach (var change in _buffer.GetChanges())
            {
                _sink.WriteCell(change.X, change.Y, change.Cell);
            }
        }

        _sink.EndFrame();
        _buffer.SwapBuffers();
    }

    private void PresentShaped(IShapedRunSink shaped)
    {
        // Per-frame stack-allocated text buffer reused across runs. Sized generously to
        // accommodate multi-codepoint grapheme clusters; max codepoints per cell is a small
        // constant, so width × 8 covers virtually all realistic content. Truncated runs are
        // emitted as far as they fit, with the rest of the style group skipped to avoid spin.
        const int InlineCharCap = 1024;
        Span<char> textBuf = stackalloc char[InlineCharCap];

        foreach (var y in _buffer.GetDirtyRows())
        {
            var x = 0;
            while (x < _buffer.Width)
            {
                var leadCell = _buffer.GetCell(x, y);

                // Skip empty cells (default-coloured spaces). They don't need shaping;
                // they fall to the surface's clear color via the host's per-frame Clear().
                if (IsEmptyCell(leadCell))
                {
                    x++;
                    continue;
                }

                // Walk forward while style matches; this defines the run's extent.
                var runStartX = x;
                var fg = leadCell.Foreground;
                var bg = leadCell.Background;
                var deco = leadCell.Decorations;

                var textLen = 0;
                var truncated = false;

                while (x < _buffer.Width)
                {
                    var cell = _buffer.GetCell(x, y);

                    // WideTrail belongs to its lead cell; advance past it without contributing text.
                    if ((cell.Flags & CellFlags.WideTrail) != 0)
                    {
                        x++;
                        continue;
                    }

                    // Default-colored empty cells (Cell.Empty: white/black space) end the run.
                    // Their backgrounds are indistinguishable from the surface clear, so emitting
                    // a shaped run that covers them is wasted work.
                    if (IsEmptyCell(cell))
                    {
                        break;
                    }

                    // Style break ends the run.
                    if (cell.Foreground != fg || cell.Background != bg || cell.Decorations != deco)
                    {
                        break;
                    }

                    if ((cell.Flags & CellFlags.HasGrapheme) != 0)
                    {
                        var grapheme = _buffer.GetGrapheme(x, y);
                        foreach (var cp in grapheme)
                        {
                            if (!AppendCodepoint(textBuf, ref textLen, cp))
                            {
                                truncated = true;
                                break;
                            }
                        }

                        if (truncated) break;
                    }
                    else if (cell.Codepoint != 0)
                    {
                        if (!AppendCodepoint(textBuf, ref textLen, cell.Codepoint))
                        {
                            truncated = true;
                            break;
                        }
                    }

                    x += (cell.Flags & CellFlags.WideLead) != 0 ? 2 : 1;
                }

                if (textLen > 0)
                {
                    shaped.WriteRun(runStartX, y, textBuf[..textLen], fg, bg, deco);
                }

                // If we truncated mid-run, skip the rest of this style group so we don't loop forever.
                if (truncated)
                {
                    while (x < _buffer.Width)
                    {
                        var cell = _buffer.GetCell(x, y);
                        if (cell.Foreground != fg || cell.Background != bg || cell.Decorations != deco)
                        {
                            break;
                        }

                        x++;
                    }
                }
            }
        }
    }

    private static bool IsEmptyCell(Cell cell) =>
        cell.Codepoint is 0 or (uint)' '
        && cell.Foreground == Color.White
        && cell.Background == Color.Black
        && cell.Decorations == TextDecorations.None
        && (cell.Flags & ~CellFlags.None) == 0;

    private static bool AppendCodepoint(Span<char> buffer, ref int length, uint codepoint)
    {
        // Rune.EncodeToUtf16 may write 1 or 2 chars. Surrogate pairs need both.
        if (System.Text.Rune.TryCreate(codepoint, out var rune))
        {
            var needed = rune.Utf16SequenceLength;
            if (length + needed > buffer.Length)
            {
                return false;
            }

            rune.EncodeToUtf16(buffer[length..]);
            length += needed;
            return true;
        }

        // Unknown codepoint — emit U+FFFD if there's room, otherwise truncate.
        if (length + 1 > buffer.Length)
        {
            return false;
        }

        buffer[length++] = '�';
        return true;
    }
    
    /// <summary>
    /// Resizes the renderer to the specified dimensions.
    /// </summary>
    /// <param name="newWidth">The new width in columns.</param>
    /// <param name="newHeight">The new height in rows.</param>
    public void Resize(int newWidth, int newHeight)
    {
        if (newWidth == Width && newHeight == Height)
        {
            return; // No change needed
        }

        _buffer.Resize(newWidth, newHeight);

        // Resize on the sink resets style state and clears the surface so the
        // diff-based Present() starts from a known-clean baseline. Without this,
        // stale SGR attributes from the previous frame would bleed into the new
        // surface, and unchanged cells outside the content area would not be repainted.
        _sink.Resize(newWidth, newHeight);
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
    /// Resets the sink's style state and flushes any pending output. For the ANSI
    /// sink this emits the SGR reset sequence; for other sinks it is implementation-defined.
    /// </summary>
    public void WriteReset()
    {
        _sink.Reset();
        _sink.EndFrame();
    }

    /// <summary>
    /// Handles terminal resize events by recreating the buffer.
    /// </summary>
    public void HandleResize()
    {
        if (_terminal == null)
        {
            return; // Test renderer, no resize support
        }

        var newWidth = _terminal.Width;
        var newHeight = _terminal.Height;
        
        Resize(newWidth, newHeight);
    }
    
    /// <summary>
    /// Disposes the renderer and restores the terminal to its original state.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _buffer.Dispose();
        _guard?.Dispose();
        _sink.Dispose();
    }
}
