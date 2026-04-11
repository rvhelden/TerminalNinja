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
}
