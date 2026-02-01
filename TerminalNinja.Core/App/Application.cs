using System.Text;
using TerminalNinja.Core.Elements;
using TerminalNinja.Core.Input;
using TerminalNinja.Core.Rendering;

namespace TerminalNinja.Core.App;

/// <summary>
/// Main application class that manages the event loop for interactive terminal UI applications.
/// </summary>
public sealed class Application : IDisposable
{
    private readonly ApplicationOptions _options;
    private readonly Renderer _renderer;
    private readonly InputReader _inputReader;
    private readonly FocusManager _focusManager;
    
    private IElement? _rootElement;
    private bool _running;
    private bool _invalidated = true;
    private bool _disposed;
    
    /// <summary>
    /// Event raised when a key is pressed. Set Handled to true to prevent default handling.
    /// </summary>
    public event Action<KeyEvent, KeyEventArgs>? KeyDown;
    
    /// <summary>
    /// Gets the focus manager for this application.
    /// </summary>
    public FocusManager FocusManager => _focusManager;
    
    /// <summary>
    /// Gets the renderer for this application.
    /// </summary>
    public Renderer Renderer => _renderer;
    
    /// <summary>
    /// Gets or sets the root element to display.
    /// </summary>
    public IElement? RootElement
    {
        get => _rootElement;
        set
        {
            _rootElement = value;
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
            if (_invalidated && _rootElement is not null)
            {
                _renderer.Clear();
                _renderer.Draw(_rootElement);
                _renderer.Present();
                _invalidated = false;
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
        if (_options.EnableTabNavigation && _rootElement is not null)
        {
            if (keyEvent.Key == ConsoleKey.Tab && keyEvent.Shift)
            {
                _focusManager.FocusPrevious(_rootElement, _renderer.Viewport);
                Invalidate();
                return;
            }
            
            if (keyEvent.Key == ConsoleKey.Tab && !keyEvent.HasModifiers)
            {
                _focusManager.FocusNext(_rootElement, _renderer.Viewport);
                Invalidate();
                return;
            }
        }
        
        // Dispatch to focused element
        _focusManager.HandleKeyEvent(keyEvent);
        Invalidate();
    }
    
    /// <summary>
    /// Handles mouse input events.
    /// </summary>
    private void HandleMouseEvent(MouseEvent mouseEvent)
    {
        if (_rootElement is null)
            return;
        
        _focusManager.HandleMouseEvent(_rootElement, _renderer.Viewport, mouseEvent);
        Invalidate();
    }
    
    /// <summary>
    /// Handles terminal resize events.
    /// </summary>
    private void HandleResizeEvent(ResizeEvent resizeEvent)
    {
        // Terminal size has changed - resize the renderer's buffer
        _renderer.Resize(resizeEvent.Width, resizeEvent.Height);
        
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
        _inputReader.Dispose();
        _renderer.Dispose();
    }
}
