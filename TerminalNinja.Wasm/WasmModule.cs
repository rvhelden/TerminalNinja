#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text;
using TerminalNinja.App;
using TerminalNinja.Controls;
using TerminalNinja.Input;
using TerminalNinja.Rendering;
using TerminalNinja.Xaml;

namespace TerminalNinja.Wasm;

/// <summary>
/// Browser-WASM entry point exposing both single-frame rendering and interactive
/// live sessions to JavaScript. A live session runs the TerminalNinja event loop
/// tick-by-tick (driven by <c>requestAnimationFrame</c> on the JS side), supports
/// keyboard and mouse input injection, hover/focus states, and animations.
/// </summary>
[SupportedOSPlatform("browser")]
public partial class WasmModule
{
    // ─── Shared state ───────────────────────────────────────────────

    private static Application? _app;
    private static WasmInputBackend? _inputBackend;
    private static MemoryStream? _renderStream;
    private static string? _currentTheme;

    // ─── Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Ensures a headless Application exists for single-frame rendering
    /// (themes, implicit styles, StaticResource lookups).
    /// </summary>
    private static void EnsureApplication()
    {
        if (_app != null) return;

        _app = new Application(new ApplicationOptions
        {
            Headless = true,
            HeadlessWidth = 80,
            HeadlessHeight = 24
        });
    }

    // ─── Single-frame rendering (unchanged API) ─────────────────────

    /// <summary>
    /// Renders a XAML string to ANSI escape sequences (single frame).
    /// </summary>
    [JSExport]
    public static string RenderXaml(string xaml, int width, int height)
    {
        try
        {
            EnsureApplication();

            var window = TerminalXaml.Load<Window>(xaml);

            using var memoryStream = new MemoryStream();
            using var renderer = Renderer.CreateOffscreen(memoryStream, width, height);

            renderer.Clear();
            renderer.Draw(window);
            renderer.Present();
            renderer.WriteReset();

            return Encoding.UTF8.GetString(memoryStream.ToArray());
        }
        catch (Exception ex)
        {
            return $"\e[31mError: {ex.Message}\e[0m";
        }
    }

    // ─── Theme support ──────────────────────────────────────────────

    [JSExport]
    public static void SetTheme(string? themeName)
    {
        _currentTheme = string.IsNullOrEmpty(themeName) ? null : themeName;
        EnsureApplication();
        Application.Current!.ThemeName = _currentTheme;
    }

    [JSExport]
    public static string[] GetThemeNames()
    {
        return Application.BuiltInThemes.ToArray();
    }

    // ─── Interactive session API ────────────────────────────────────

    /// <summary>
    /// Starts a live interactive session. Creates an Application with a queued
    /// input backend, loads the XAML, and prepares for tick-based rendering.
    /// Call <see cref="Tick"/> on each requestAnimationFrame to drive the loop.
    /// </summary>
    [JSExport]
    public static string StartSession(string xaml, int width, int height)
    {
        try
        {
            // Dispose previous session
            StopSession();

            _inputBackend = new WasmInputBackend();
            _renderStream = new MemoryStream();

            _app = new Application(new ApplicationOptions
            {
                Headless = true,
                HeadlessWidth = width,
                HeadlessHeight = height,
                HeadlessOutputStream = _renderStream,
                EnableMouseTracking = true,
                EnableTabNavigation = true,
                InputBackend = _inputBackend
            });

            // Re-apply the saved theme to the new Application
            if (_currentTheme != null)
            {
                _app.ThemeName = _currentTheme;
            }

            var window = TerminalXaml.Load<Window>(xaml);
            window.Show(); // Sets app.RootControl

            // Wire invalidation for the entire visual tree
            _app.WireInvalidation(window);

            // Render the first frame
            _app.Invalidate();
            _app.ProcessTick();

            _renderStream.Position = 0;
            var result = Encoding.UTF8.GetString(_renderStream.ToArray());
            _renderStream.SetLength(0);
            return result;
        }
        catch (Exception ex)
        {
            return $"\e[31mError: {ex.Message}\e[0m";
        }
    }

    /// <summary>
    /// Reloads the XAML in the current session without restarting.
    /// Preserves the Application, input backend, and render stream.
    /// Returns the first frame of the new XAML, or an error string.
    /// </summary>
    [JSExport]
    public static string ReloadXaml(string xaml, int width, int height)
    {
        if (_app == null || _renderStream == null)
        {
            return StartSession(xaml, width, height);
        }

        try
        {
            _app.Renderer.Resize(width, height);

            var window = TerminalXaml.Load<Window>(xaml);
            window.Show();

            _app.WireInvalidation(window);
            _app.Invalidate();
            _app.ProcessTick();

            _renderStream.Position = 0;
            var result = Encoding.UTF8.GetString(_renderStream.ToArray());
            _renderStream.SetLength(0);
            return result;
        }
        catch (Exception ex)
        {
            return $"\e[31mReload error: {ex.Message}\e[0m";
        }
    }

    /// <summary>
    /// Performs one tick of the event loop: processes queued input events,
    /// re-renders if invalidated. Returns the ANSI delta (empty string if no change).
    /// </summary>
    [JSExport]
    public static string Tick()
    {
        if (_app == null || _renderStream == null)
        {
            return "";
        }

        try
        {
            var rendered = _app.ProcessTick();
            if (!rendered)
            {
                return "";
            }

            _renderStream.Position = 0;
            var result = Encoding.UTF8.GetString(_renderStream.ToArray());
            _renderStream.SetLength(0);
            return result;
        }
        catch (Exception ex)
        {
            return $"\e[31mTick error: {ex.Message}\e[0m";
        }
    }

    /// <summary>
    /// Injects a keyboard event into the session's input queue.
    /// </summary>
    [JSExport]
    public static void InjectKeyEvent(int key, char keyChar, bool shift, bool alt, bool ctrl)
    {
        _inputBackend?.Enqueue(new KeyEvent((ConsoleKey)key, keyChar, shift, alt, ctrl));
    }

    /// <summary>
    /// Injects a mouse event into the session's input queue.
    /// </summary>
    /// <param name="x">Column (0-based).</param>
    /// <param name="y">Row (0-based).</param>
    /// <param name="button">0=Left, 1=Middle, 2=Right.</param>
    /// <param name="action">0=Press, 1=Release, 2=Move.</param>
    [JSExport]
    public static void InjectMouseEvent(int x, int y, int button, int action)
    {
        _inputBackend?.Enqueue(new MouseEvent(
            x, y,
            (MouseButton)button,
            (MouseAction)action));
    }

    /// <summary>
    /// Resizes the session viewport and triggers a full re-render.
    /// </summary>
    [JSExport]
    public static void SessionResize(int width, int height)
    {
        if (_app == null) return;

        _app.Renderer.Resize(width, height);
        _app.Invalidate();
    }

    /// <summary>
    /// Stops the current interactive session and cleans up resources.
    /// </summary>
    [JSExport]
    public static void StopSession()
    {
        _app?.Dispose();
        _app = null;
        _renderStream?.Dispose();
        _renderStream = null;
        _inputBackend = null;
    }
}

/// <summary>
/// Input backend for WASM that queues events injected from JavaScript.
/// </summary>
internal sealed class WasmInputBackend : IInputBackend
{
    private readonly ConcurrentQueue<InputEvent> _queue = new();

    public void Enqueue(InputEvent evt) => _queue.Enqueue(evt);

    public IReadOnlyList<InputEvent>? TryRead()
    {
        if (_queue.IsEmpty) return null;

        var events = new List<InputEvent>();
        while (_queue.TryDequeue(out var evt))
        {
            events.Add(evt);
        }

        return events.Count > 0 ? events : null;
    }

    public IReadOnlyList<InputEvent> Read() => TryRead() ?? [];

    public void EnableMouseTracking() { }
    public void DisableMouseTracking() { }
    public void Dispose() { }
}
