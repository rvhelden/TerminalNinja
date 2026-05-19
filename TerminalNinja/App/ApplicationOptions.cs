namespace TerminalNinja.App;

/// <summary>
/// Configuration options for the Application event loop.
/// </summary>
public sealed class ApplicationOptions
{
    /// <summary>
    /// Gets or sets the target frame rate (frames per second).
    /// Default is 60 FPS.
    /// </summary>
    public int TargetFps { get; init; } = 60;
    
    /// <summary>
    /// Gets or sets whether mouse tracking should be enabled.
    /// Default is true.
    /// </summary>
    public bool EnableMouseTracking { get; init; } = true;
    
    /// <summary>
    /// Gets or sets whether Tab key navigation should be enabled.
    /// Default is true.
    /// </summary>
    public bool EnableTabNavigation { get; init; } = true;
    
    /// <summary>
    /// Gets or sets whether the application runs in headless mode (no real terminal).
    /// When true, a no-op input backend and offscreen renderer are used instead of
    /// platform-specific backends that require a real console handle.
    /// This is useful for unit testing and CI environments.
    /// Default is false.
    /// </summary>
    public bool Headless { get; init; }
    
    /// <summary>
    /// Gets or sets the viewport width for headless mode.
    /// Only used when <see cref="Headless"/> is true. Default is 80.
    /// </summary>
    public int HeadlessWidth { get; init; } = 80;
    
    /// <summary>
    /// Gets or sets the viewport height for headless mode.
    /// Only used when <see cref="Headless"/> is true. Default is 24.
    /// </summary>
    public int HeadlessHeight { get; init; } = 24;
    
    /// <summary>
    /// Gets or sets the output stream for headless mode rendering.
    /// Only used when <see cref="Headless"/> is true. Default is <see cref="Stream.Null"/>.
    /// Set this to a <see cref="MemoryStream"/> to capture ANSI output (e.g. for WASM).
    /// </summary>
    public Stream? HeadlessOutputStream { get; init; }

    /// <summary>
    /// Gets the frame delay in milliseconds based on target FPS.
    /// </summary>
    public int FrameDelayMs => 1000 / TargetFps;
    
    /// <summary>
    /// Optional custom input backend. When set with <see cref="Headless"/> = true,
    /// this backend is used instead of the default NullInputBackend.
    /// Use this for testing or WASM scenarios where events are injected externally.
    /// </summary>
    public TerminalNinja.Input.IInputBackend? InputBackend { get; init; }

    /// <summary>
    /// Optional renderer to use instead of the default terminal renderer. Set this when
    /// hosting the application in a non-terminal context (e.g. a GPU window) so the
    /// control tree renders through a custom <see cref="Rendering.ICellSink"/>. When set,
    /// the Application skips the per-platform terminal renderer construction and bypasses
    /// console-specific setup (output encoding, Ctrl+C handler).
    /// </summary>
    public Rendering.Renderer? RendererOverride { get; init; }

    /// <summary>
    /// When true, suppress console-specific setup that doesn't apply to a non-terminal
    /// host: <c>Console.OutputEncoding</c>, <c>Console.InputEncoding</c>, the
    /// <c>CancelKeyPress</c> safety handler, and the debug-time hot-reload auto-attach.
    /// Set automatically alongside <see cref="RendererOverride"/>; can also be set on its
    /// own for embedded scenarios.
    /// </summary>
    public bool SuppressConsoleSetup { get; init; }

    /// <summary>
    /// When true (default), an Escape keypress with no active modal exits the event loop.
    /// Set to false in apps where Escape must reach focused controls (e.g. to dismiss a
    /// non-modal IntelliSense popup). Modals still close on Escape regardless of this flag.
    /// </summary>
    public bool EscapeQuits { get; init; } = true;
}
