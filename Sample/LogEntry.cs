using TerminalNinja.Aot;

namespace Sample;

/// <summary>
/// Represents a single activity log entry with a timestamp and message.
/// </summary>
[BindableObject]
public class LogEntry
{
    public string Time { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public override string ToString() => $"{Time} {Message}";
}
