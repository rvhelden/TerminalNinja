using System.Text;
using TerminalNinja.Controls;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Rendering;
using TerminalNinja.Resources;
using TerminalNinja.Xaml;

namespace TerminalNinja.App;

/// <summary>
/// Main application class that manages the event loop for interactive terminal UI applications.
/// Provides a singleton instance accessible via Current property and manages application-level resources.
/// </summary>
public sealed class Application : IDisposable
{
    /// <summary>
    /// Represents a single entry in the overlay stack.
    /// </summary>
    /// <param name="Element">The overlay UIElement to render.</param>
    /// <param name="IsModal">Whether this overlay captures all input (modal behavior).</param>
    /// <param name="DimBackground">Whether to dim the background beneath this overlay.</param>
    public sealed record OverlayEntry(UIElement Element, bool IsModal, bool DimBackground);
    /// <summary>
    /// Gets the current application instance (singleton).
    /// Returns null if no Application has been created.
    /// </summary>
    public static Application? Current { get; private set; }

    private readonly ApplicationOptions _options;
    private readonly InputReader _inputReader;

    private UIElement? _rootControl;
    private bool _running;
    private bool _invalidated = true;
    private bool _disposed;
    
    // Overlay / modal stack
    private readonly List<OverlayEntry> _overlayStack = [];
    
    // FPS tracking
    private int _frameCount;
    private DateTime _lastFpsUpdate = DateTime.UtcNow;

    // Time to first render tracking
    private readonly DateTime _startTime;

    /// <summary>
    /// Event raised when a key is pressed. Set Handled to true to prevent default handling.
    /// </summary>
    public event Action<KeyEvent, KeyEventArgs>? KeyDown;
    
    /// <summary>
    /// Event raised when the terminal window is resized.
    /// </summary>
    public event Action<ResizeEvent>? Resize;
    
    /// <summary>
    /// Gets the focus manager for this application.
    /// </summary>
    public FocusManager FocusManager { get; }

    /// <summary>
    /// Gets the renderer for this application.
    /// </summary>
    public Renderer Renderer { get; }

    /// <summary>
    /// Gets the application-level resource dictionary.
    /// Resources defined here are available to all controls in the application.
    /// </summary>
    public ResourceDictionary Resources { get; } = new();

    /// <summary>
    /// The currently loaded theme ResourceDictionary, or null if no theme is loaded.
    /// </summary>
    private ResourceDictionary? _themeDictionary;

    /// <summary>
    /// The name of the currently active theme.
    /// </summary>
    private string? _themeName;

    /// <summary>
    /// Gets the names of all built-in themes.
    /// </summary>
    public static IReadOnlyList<string> BuiltInThemes { get; } = ["Dark", "Dracula", "GruvboxDark"];

    /// <summary>
    /// Gets or sets the name of the active theme.
    /// Setting this property loads the corresponding theme XAML from embedded resources
    /// and merges it into <see cref="Resources"/>.MergedDictionaries.
    /// Built-in themes: "Dark", "Dracula", "GruvboxDark".
    /// Set to null to clear the current theme.
    /// </summary>
    public string? ThemeName
    {
        get => _themeName;
        set
        {
            if (string.Equals(_themeName, value, StringComparison.Ordinal))
            {
                return;
            }

            // Remove the previous theme dictionary
            if (_themeDictionary != null)
            {
                Resources.MergedDictionaries.Remove(_themeDictionary);
                _themeDictionary = null;
            }

            _themeName = value;

            if (value != null)
            {
                _themeDictionary = LoadThemeResourceDictionary(value);
                // Insert at position 0 so theme resources have lowest priority
                // (app-level resources and control-level resources override theme)
                Resources.MergedDictionaries.Insert(0, _themeDictionary);
            }

            // Invalidate all controls so implicit styles re-resolve
            InvalidateImplicitStyles();
            Invalidate();
        }
    }

    /// <summary>
    /// Loads a theme ResourceDictionary from embedded XAML resources.
    /// </summary>
    private static ResourceDictionary LoadThemeResourceDictionary(string themeName)
    {
        var resourceName = $"TerminalNinja.Themes.{themeName}.xaml";
        var assembly = typeof(Application).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            var available = assembly.GetManifestResourceNames();
            throw new InvalidOperationException(
                $"Theme '{themeName}' not found. Expected embedded resource '{resourceName}'. " +
                $"Available resources: [{string.Join(", ", available)}]. " +
                $"Built-in themes: [{string.Join(", ", BuiltInThemes)}]");
        }

        return TerminalXaml.LoadResourceDictionary(stream);
    }

    /// <summary>
    /// Invalidates implicit styles on the root control and all its descendants
    /// so they re-resolve against the new theme dictionary.
    /// </summary>
    private void InvalidateImplicitStyles()
    {
        if (_rootControl is FrameworkElement rootFe)
        {
            InvalidateImplicitStylesRecursive(rootFe);
        }
    }

    private static void InvalidateImplicitStylesRecursive(FrameworkElement fe)
    {
        fe.InvalidateImplicitStyle();
        foreach (var child in fe.GetLogicalChildren())
        {
            if (child is FrameworkElement childFe)
            {
                InvalidateImplicitStylesRecursive(childFe);
            }
        }
    }

    /// <summary>
    /// Gets the target frames per second from the application options.
    /// </summary>
    public int TargetFps => _options.TargetFps;
    
    /// <summary>
    /// Gets the current actual frames per second (updated every second).
    /// </summary>
    public int CurrentFps { get; private set; }

    /// <summary>
    /// Gets the time taken to render the first frame, or null if first render hasn't happened yet.
    /// </summary>
    public TimeSpan? TimeToFirstRender { get; private set; }

    /// <summary>
    /// Gets or sets the root control to display.
    /// </summary>
    public UIElement? RootControl
    {
        get => _rootControl;
        set
        {
            _rootControl = value;
            if (_rootControl != null)
            {
                WireInvalidation(_rootControl);
            }

            Invalidate();
        }
    }
    
    // ─── Overlay / Modal API ─────────────────────────────────────────

    /// <summary>
    /// Gets a read-only view of the current overlay stack (bottom to top).
    /// </summary>
    public IReadOnlyList<OverlayEntry> Overlays => _overlayStack;

    /// <summary>
    /// Gets the topmost modal overlay, or null if no modal is active.
    /// </summary>
    public OverlayEntry? ActiveModal =>
        _overlayStack.FindLast(e => e.IsModal);

    /// <summary>
    /// Returns true if there is at least one modal overlay on the stack.
    /// </summary>
    public bool IsModal => _overlayStack.Exists(e => e.IsModal);

    /// <summary>
    /// Pushes an overlay onto the stack. The overlay renders on top of
    /// the root control and any previously pushed overlays.
    /// </summary>
    /// <param name="element">The UI element to display as an overlay.</param>
    /// <param name="isModal">
    /// If true, all keyboard and mouse input is restricted to this overlay
    /// (and any overlays pushed on top of it later).
    /// </param>
    /// <param name="dimBackground">
    /// If true, the content beneath this overlay is dimmed before the overlay
    /// is rendered, giving a visual cue that the background is inactive.
    /// </param>
    public void PushOverlay(UIElement element, bool isModal = false, bool dimBackground = false)
    {
        ArgumentNullException.ThrowIfNull(element);

        var entry = new OverlayEntry(element, isModal, dimBackground);
        _overlayStack.Add(entry);
        WireInvalidation(element);
        Invalidate();
    }

    /// <summary>
    /// Removes the specified overlay from the stack.
    /// </summary>
    /// <param name="element">The overlay element to remove.</param>
    /// <returns>True if the overlay was found and removed.</returns>
    public bool RemoveOverlay(UIElement element)
    {
        var index = _overlayStack.FindIndex(e => e.Element == element);
        if (index < 0)
        {
            return false;
        }

        _overlayStack.RemoveAt(index);
        Invalidate();
        return true;
    }

    /// <summary>
    /// Pops the topmost overlay from the stack.
    /// </summary>
    /// <returns>The removed overlay entry, or null if the stack was empty.</returns>
    public OverlayEntry? PopOverlay()
    {
        if (_overlayStack.Count == 0)
        {
            return null;
        }

        var entry = _overlayStack[^1];
        _overlayStack.RemoveAt(_overlayStack.Count - 1);
        Invalidate();
        return entry;
    }
    
    /// <summary>
    /// Creates a new application with default options.
    /// </summary>
    public Application() : this(new ApplicationOptions())
    {
    }
    
    /// <summary>
    /// Creates a new application with the specified options.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    public Application(ApplicationOptions options)
    {
        // Record start time for time-to-first-render measurement
        _startTime = DateTime.UtcNow;
        
        // Set singleton (allow replacement for testing scenarios)
        Current = this;
        
        // Hook up resource lookup for FrameworkElement
        FrameworkElement.ApplicationResourceLookup = key => Resources.TryGetValue(key, out var value) ? value : null;

        _options = options;

        if (options.Headless)
        {
            // Headless mode: no-op input backend and offscreen renderer.
            // Used for unit testing and CI environments without a real console.
            Renderer = Renderer.CreateOffscreen(Stream.Null, options.HeadlessWidth, options.HeadlessHeight);
            _inputReader = new InputReader(new NullInputBackend());
        }
        else
        {
            // Production mode: real terminal
            System.Console.OutputEncoding = Encoding.UTF8;
            System.Console.InputEncoding = Encoding.UTF8;
            Renderer = new Renderer();
            _inputReader = new InputReader();
        }
        
        FocusManager = new FocusManager();
        
        if (_options.EnableMouseTracking)
        {
            _inputReader.EnableMouseTracking();
        }
    }
    
    /// <summary>
    /// Marks the UI as needing to be re-rendered.
    /// </summary>
    public void Invalidate()
    {
        _invalidated = true;
    }
    
    /// <summary>
    /// Recursively wires invalidation callbacks for all elements in the visual tree
    /// using the Visual.GetChildrenWithBounds traversal.
    /// </summary>
    private void WireInvalidation(UIElement control)
    {
        control.InvalidationCallback = Invalidate;

        // Use visual tree traversal — works for all container types
        var dummyBounds = new Rect(0, 0, Renderer.Width, Renderer.Height);
        var myBounds = control.CalculateBounds(dummyBounds);
        foreach (var (child, _) in control.GetChildrenWithBounds(myBounds))
        {
            if (child is UIElement childElement)
            {
                WireInvalidation(childElement);
            }
        }
    }
    
    /// <summary>
    /// Exits the application event loop.
    /// </summary>
    public void Exit()
    {
        _running = false;
    }
    
    /// <summary>
    /// Runs the application event loop.
    /// Blocks until Exit() is called or Escape key is pressed.
    /// </summary>
    public void Run()
    {
        _running = true;
        
        while (_running)
        {
            // Process input events
            ProcessInput();
            
            // Render if needed
            if (_invalidated && _rootControl is not null)
            {
                Renderer.Clear();
                Renderer.Draw(_rootControl);
                
                // Render overlays on top (bottom to top order)
                foreach (var overlay in _overlayStack)
                {
                    if (overlay.DimBackground)
                    {
                        Renderer.DimBackground();
                    }

                    Renderer.DrawOverlay(overlay.Element);
                }
                
                Renderer.Present();
                _invalidated = false;
                
                // Capture time to first render
                if (TimeToFirstRender == null)
                {
                    TimeToFirstRender = DateTime.UtcNow - _startTime;
                }
                
                // Track frame for FPS calculation
                _frameCount++;
            }
            
            // Update FPS counter every second
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastFpsUpdate).TotalSeconds;
            if (elapsed >= 1.0)
            {
                CurrentFps = (int)(_frameCount / elapsed);
                _frameCount = 0;
                _lastFpsUpdate = now;
            }
            
            // Limit frame rate
            Thread.Sleep(_options.FrameDelayMs);
        }
    }
    
    /// <summary>
    /// Processes all pending input events.
    /// </summary>
    private void ProcessInput()
    {
        while (true)
        {
            var inputEvents = _inputReader.TryRead();
            if (inputEvents is null || inputEvents.Count == 0)
            {
                break;
            }

            foreach (var inputEvent in inputEvents)
            {
                HandleInputEvent(inputEvent);
            }
        }
    }
    
    /// <summary>
    /// Handles a single input event.
    /// </summary>
    private void HandleInputEvent(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case KeyEvent keyEvent:
                HandleKeyEvent(keyEvent);
                break;
            
            case MouseEvent mouseEvent:
                HandleMouseEvent(mouseEvent);
                break;
            
            case ResizeEvent resizeEvent:
                HandleResizeEvent(resizeEvent);
                break;
        }
    }
    
    /// <summary>
    /// Handles keyboard input events.
    /// </summary>
    private void HandleKeyEvent(KeyEvent keyEvent)
    {
        // Allow custom handling first
        if (KeyDown is not null)
        {
            var args = new KeyEventArgs();
            KeyDown(keyEvent, args);
            if (args.Handled)
            {
                return;
            }
        }
        
        // Escape key: close the topmost modal overlay first, then exit the app
        if (keyEvent.Key == ConsoleKey.Escape && !keyEvent.HasModifiers)
        {
            var modal = ActiveModal;
            if (modal != null)
            {
                // Close the topmost modal by removing it from the overlay stack.
                // If the modal is a Window with ShowDialogAsync, setting DialogResult
                // will trigger the close. Otherwise, just remove the overlay.
                if (modal.Element is Window { IsModal: true } modalWindow)
                {
                    modalWindow.DialogResult ??= false;
                }
                else
                {
                    RemoveOverlay(modal.Element);
                }
                
                Invalidate();
                return;
            }
            
            Exit();
            return;
        }
        
        // Determine the input root: the topmost modal overlay if one exists,
        // otherwise the application root control.
        var inputRoot = GetInputRoot();
        
        // Tab navigation
        if (_options.EnableTabNavigation && inputRoot is not null)
        {
            if (keyEvent.Key == ConsoleKey.Tab && keyEvent.Shift)
            {
                FocusManager.FocusPrevious(inputRoot, Renderer.Viewport);
                Invalidate();
                return;
            }
            
            if (keyEvent.Key == ConsoleKey.Tab && !keyEvent.HasModifiers)
            {
                FocusManager.FocusNext(inputRoot, Renderer.Viewport);
                Invalidate();
                return;
            }
        }
        
        // Dispatch to focused control
        FocusManager.HandleKeyEvent(keyEvent);
        Invalidate();
    }
    
    /// <summary>
    /// Handles mouse input events.
    /// </summary>
    private void HandleMouseEvent(MouseEvent mouseEvent)
    {
        var inputRoot = GetInputRoot();
        if (inputRoot is null)
        {
            return;
        }

        FocusManager.HandleMouseEvent(inputRoot, Renderer.Viewport, mouseEvent);
        Invalidate();
    }
    
    /// <summary>
    /// Returns the UIElement that should receive input. When a modal overlay
    /// is active, input is restricted to that overlay. Otherwise, the root
    /// control is used.
    /// </summary>
    private UIElement? GetInputRoot()
    {
        var modal = ActiveModal;
        return modal?.Element ?? _rootControl;
    }
    
    /// <summary>
    /// Handles terminal resize events.
    /// </summary>
    private void HandleResizeEvent(ResizeEvent resizeEvent)
    {
        // Use HandleResize() which reads the actual visible window dimensions
        // from System.Console.WindowWidth/WindowHeight via the ITerminal
        // abstraction. The ResizeEvent from WINDOW_BUFFER_SIZE_EVENT reports
        // the screen *buffer* size which can be larger than the visible window
        // (e.g. with scrollback), causing the renderer to allocate an oversized
        // buffer that produces scrollbars and stretches content beyond the
        // visible area.
        Renderer.HandleResize();
        
        // Notify subscribers of the resize (pass the actual renderer dimensions)
        Resize?.Invoke(new ResizeEvent(Renderer.Width, Renderer.Height));
        
        // Trigger a full re-render with new dimensions
        Invalidate();
    }
    
    /// <summary>
    /// Disposes the application and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        
        // Clean up singleton and resource lookup hook
        if (Current == this)
        {
            Current = null;
            FrameworkElement.ApplicationResourceLookup = null;
        }
        
        _inputReader?.Dispose();
        Renderer?.Dispose();
    }
}
