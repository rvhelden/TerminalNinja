using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Terminal;

/// <summary>
/// A <see cref="UIElement"/> that hosts a terminal: it owns a <see cref="TerminalScreenBuffer"/>
/// + <see cref="VtParser"/>, optionally subscribes to an <see cref="ITerminalBackend"/> for
/// child-process bytes, renders the screen state into its parent's <see cref="CellBuffer"/>,
/// and forwards keyboard input back to the backend.
/// </summary>
/// <remarks>
/// <para>
/// Works in any TerminalNinja host — console <c>Application</c> or <c>SkiaApplication</c> —
/// because rendering goes through the standard <c>CellBuffer</c> contract.
/// </para>
/// <para>
/// Threading: <see cref="ITerminalBackend.DataReceived"/> may fire on a background thread.
/// We marshal incoming bytes into a thread-safe queue and pump them through the parser on
/// the render thread, so the buffer mutations stay single-threaded.
/// </para>
/// </remarks>
public sealed class TerminalView : UIElement
{
    private readonly object _pendingLock = new();
    private readonly Queue<byte[]> _pendingBytes = new();
    private readonly VtParser _parser = new();

    private TerminalScreenBuffer _screen;
    private ITerminalBackend? _backend;

    /// <summary>The screen-state buffer driven by the parser.</summary>
    public TerminalScreenBuffer Screen => _screen;

    /// <summary>
    /// The backend the view sends keystrokes to and receives bytes from. Setting this
    /// unsubscribes from the previous backend (if any) and subscribes to the new one's
    /// <see cref="ITerminalBackend.DataReceived"/>.
    /// </summary>
    public ITerminalBackend? Backend
    {
        get => _backend;
        set
        {
            if (ReferenceEquals(_backend, value))
            {
                return;
            }

            if (_backend is not null)
            {
                _backend.DataReceived -= OnBackendDataReceived;
            }

            _backend = value;

            if (_backend is not null)
            {
                _backend.DataReceived += OnBackendDataReceived;
            }
        }
    }

    /// <summary>
    /// Creates a terminal view with a screen buffer of the given dimensions.
    /// </summary>
    public TerminalView(int rows = 24, int cols = 80)
    {
        _screen = new TerminalScreenBuffer(rows, cols);
        Focusable = true;
    }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect availableSpace)
        => new(_screen.Cols, _screen.Rows);

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parentBounds)
        => parentBounds; // fill what we're given; resize-to-fit handled separately

    /// <inheritdoc />
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        // Pull any bytes that arrived since the last render. Done on the render thread so
        // parser + screen-buffer mutations are single-producer (avoids tearing).
        PumpPendingBytes();

        var bounds = CalculateBounds(parentBounds);
        var rowsToRender = Math.Min(_screen.Rows, bounds.Height);
        var colsToRender = Math.Min(_screen.Cols, bounds.Width);

        for (var r = 0; r < rowsToRender; r++)
        {
            var bufferY = bounds.Y + r;
            if ((uint)bufferY >= (uint)buffer.Height)
            {
                continue;
            }

            for (var c = 0; c < colsToRender; c++)
            {
                var bufferX = bounds.X + c;
                if ((uint)bufferX >= (uint)buffer.Width)
                {
                    continue;
                }

                buffer.SetCell(bufferX, bufferY, _screen.GetCell(r, c));
            }
        }

        // Cursor: invert fg/bg on the cell at the cursor position so it stands out against
        // any cell color. Standard for terminal emulators when the view is focused and
        // CursorVisible is set; we don't currently gate on focus, but a follow-up can.
        if (_screen.CursorVisible)
        {
            var cx = bounds.X + _screen.CursorCol;
            var cy = bounds.Y + _screen.CursorRow;
            if ((uint)cx < (uint)buffer.Width && (uint)cy < (uint)buffer.Height)
            {
                var cell = buffer.GetCell(cx, cy);
                buffer.SetCell(cx, cy, new Cell(cell.Codepoint, cell.Background, cell.Foreground, cell.Decorations, cell.Flags));
            }
        }
    }

    /// <inheritdoc />
    public override void OnKeyEvent(KeyEvent keyEvent)
    {
        if (_backend is null || !_backend.IsRunning)
        {
            return;
        }

        var bytes = KeyEventEncoder.Encode(keyEvent);
        if (bytes is null)
        {
            return;
        }

        // Fire-and-forget. WriteAsync on a healthy backend completes quickly (a queued
        // write to the master pipe); failures here mean the child process has died, and
        // a subsequent backend event will flip IsRunning so we stop trying.
        _ = WriteToBackendAsync(bytes);
    }

    private async Task WriteToBackendAsync(byte[] bytes)
    {
        try
        {
            if (_backend is null)
            {
                return;
            }

            await _backend.WriteAsync(bytes).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Swallow — typically a broken-pipe after the child exited. The next backend
            // event will surface the exit; we don't want to bubble a keystroke into the
            // rendering loop. Diagnostic logging is a follow-up (event-source / ILogger).
        }
    }

    /// <summary>
    /// Manually feeds bytes into the parser, bypassing any backend. Used by tests and
    /// for replaying pre-recorded ANSI streams (e.g. <c>asciinema</c>).
    /// </summary>
    public void Feed(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        _parser.Feed(bytes, _screen);
        InvalidationCallback?.Invoke();
    }

    /// <summary>
    /// Resizes the screen buffer and notifies the backend so the child can reflow.
    /// </summary>
    public void ResizeScreen(int rows, int cols)
    {
        _screen.Resize(rows, cols);
        var backend = _backend;
        if (backend is not null && backend.IsRunning)
        {
            _ = ResizeBackendAsync(backend, cols, rows);
        }

        InvalidationCallback?.Invoke();
    }

    private static async Task ResizeBackendAsync(ITerminalBackend backend, int cols, int rows)
    {
        try
        {
            await backend.ResizeAsync(cols, rows).ConfigureAwait(false);
        }
        catch
        {
            // Same rationale as WriteToBackendAsync: a dead child surfaces via the
            // ProcessExited event; we don't want the resize to bubble exceptions.
        }
    }

    private void OnBackendDataReceived(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        // Copy + queue. The backend's buffer may be reused after the event returns, and
        // the consumer (render thread) decides when to feed the parser.
        var copy = data.ToArray();
        lock (_pendingLock)
        {
            _pendingBytes.Enqueue(copy);
        }

        InvalidationCallback?.Invoke();
    }

    private void PumpPendingBytes()
    {
        while (true)
        {
            byte[] next;
            lock (_pendingLock)
            {
                if (_pendingBytes.Count == 0)
                {
                    return;
                }

                next = _pendingBytes.Dequeue();
            }

            _parser.Feed(next, _screen);
        }
    }
}
