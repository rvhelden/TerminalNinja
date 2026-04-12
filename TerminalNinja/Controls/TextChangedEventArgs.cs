namespace TerminalNinja.Controls;

/// <summary>
/// Provides data for the <see cref="TextBox.TextChanged"/> event.
/// </summary>
public sealed class TextChangedEventArgs : EventArgs
{
    /// <summary>Gets the text value before the change.</summary>
    public string OldText { get; }

    /// <summary>Gets the text value after the change.</summary>
    public string NewText { get; }

    public TextChangedEventArgs(string oldText, string newText)
    {
        OldText = oldText;
        NewText = newText;
    }
}
