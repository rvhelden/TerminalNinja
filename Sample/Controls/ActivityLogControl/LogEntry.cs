namespace Sample;

/// <summary>
/// Represents a single activity log entry with a timestamp and message.
/// Discovered automatically by the source generator via DataType="sample:LogEntry"
/// in ActivityLogControl.xaml — no [BindableObject] attribute needed.
/// </summary>
public class LogEntry
{
    public string Time { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public override string ToString() => $"{Time} {Message}";
}
