using System.Text;
using TerminalNinja.Controls;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Rendering;
using TerminalNinja.Resources;

namespace TerminalNinja.App;

/// <summary>
/// Main application class that manages the event loop for interactive terminal UI applications.
/// Provides a singleton instance accessible via Current property and manages application-level resources.
/// </summary>
public sealed class Application : IDisposable
{
    private static Application? _current;
    
    /// <summary>
    /// Gets the current application instance (singleton).
    /// Returns null if no Application has been created.
    /// </summary>
    public static Application? Current => _current;
    
    private readonly ApplicationOptions _options;
    private readonly Renderer _renderer;
    private readonly InputReader _inputReader;
    private readonly FocusManager _focusManager;
    private readonly ResourceDictionary _resources = new();
    
    private UIElement? _rootControl;
    private bool _running;
    private bool _invalidated = true;
    private bool _disposed;
    
    // FPS tracking
    private int _frameCount;
    private DateTime _lastFpsUpdate = DateTime.UtcNow;
    private int _currentFps;
    
    // Time to first render tracking
    private readonly DateTime _startTime;
    private TimeSpan? _timeToFirstRender;
    
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
    public FocusManager FocusManager => _focusManager;
    
    /// <summary>
    /// Gets the renderer for this application.
    /// </summary>
    public Renderer Renderer => _renderer;
    
    /// <summary>
    /// Gets the application-level resource dictionary.
    /// Resources defined here are available to all controls in the application.
    /// </summary>
    public ResourceDictionary Resources => _resources;
    
    /// <summary>
    /// Gets the target frames per second from the application options.
    /// </summary>
    public int TargetFps => _options.TargetFps;
    
    /// <summary>
    /// Gets the current actual frames per second (updated every second).
    /// </summary>
    public int CurrentFps => _currentFps;
    
    /// <summary>
    /// Gets the time taken to render the first frame, or null if first render hasn't happened yet.
    /// </summary>
    public TimeSpan? TimeToFirstRender => _timeToFirstRender;
    
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
                WireInvalidation(_rootControl);
            Invalidate();
        }
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
        _current = this;
        
        // Hook up resource lookup for FrameworkElement
        FrameworkElement.ApplicationResourceLookup = key => _resources.TryGetValue(key, out var value) ? value : null;
        
        // Ensure UTF-8 encoding for proper Unicode character rendering
        System.Console.OutputEncoding = Encoding.UTF8;
        System.Console.InputEncoding = Encoding.UTF8;

        _options = options;
        _renderer = new Renderer();
        _inputReader = new InputReader();
        _focusManager = new FocusManager();
        
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
        var dummyBounds = new Rect(0, 0, _renderer.Width, _renderer.Height);
        var myBounds = control.CalculateBounds(dummyBounds);
        foreach (var (child, _) in control.GetChildrenWithBounds(myBounds))
        {
            if (child is UIElement childElement)
                WireInvalidation(childElement);
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
                _renderer.Clear();
                _renderer.Draw(_rootControl);
                _renderer.Present();
                _invalidated = false;
                
                // Capture time to first render
                if (_timeToFirstRender == null)
                {
                    _timeToFirstRender = DateTime.UtcNow - _startTime;
                }
                
                // Track frame for FPS calculation
                _frameCount++;
            }
            
            // Update FPS counter every second
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastFpsUpdate).TotalSeconds;
            if (elapsed >= 1.0)
            {
                _currentFps = (int)(_frameCount / elapsed);
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
                break;

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
                return;
        }
        
        // Escape key exits the application
        if (keyEvent.Key == ConsoleKey.Escape && !keyEvent.HasModifiers)
        {
            Exit();
            return;
        }
        
        // Tab navigation
        if (_options.EnableTabNavigation && _rootControl is not null)
        {
            if (keyEvent.Key == ConsoleKey.Tab && keyEvent.Shift)
            {
                _focusManager.FocusPrevious(_rootControl, _renderer.Viewport);
                Invalidate();
                return;
            }
            
            if (keyEvent.Key == ConsoleKey.Tab && !keyEvent.HasModifiers)
            {
                _focusManager.FocusNext(_rootControl, _renderer.Viewport);
                Invalidate();
                return;
            }
        }
        
        // Dispatch to focused control
        _focusManager.HandleKeyEvent(keyEvent);
        Invalidate();
    }
    
    /// <summary>
    /// Handles mouse input events.
    /// </summary>
    private void HandleMouseEvent(MouseEvent mouseEvent)
    {
        if (_rootControl is null)
            return;
        
        _focusManager.HandleMouseEvent(_rootControl, _renderer.Viewport, mouseEvent);
        Invalidate();
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
        _renderer.HandleResize();
        
        // Notify subscribers of the resize (pass the actual renderer dimensions)
        Resize?.Invoke(new ResizeEvent(_renderer.Width, _renderer.Height));
        
        // Trigger a full re-render with new dimensions
        Invalidate();
    }
    
    /// <summary>
    /// Disposes the application and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        
        // Clean up singleton and resource lookup hook
        if (_current == this)
        {
            _current = null;
            FrameworkElement.ApplicationResourceLookup = null;
        }
        
        _inputReader.Dispose();
        _renderer.Dispose();
    }
}
