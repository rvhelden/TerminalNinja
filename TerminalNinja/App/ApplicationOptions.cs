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
    /// Gets the frame delay in milliseconds based on target FPS.
    /// </summary>
    public int FrameDelayMs => 1000 / TargetFps;
}
