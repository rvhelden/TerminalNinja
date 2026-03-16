using System.Windows.Markup;

namespace TerminalNinja.Controls;

/// <summary>
/// A simple content control that users can extend to create reusable UI components.
/// By default, UserControl is not focusable.
/// Corresponds to WPF's System.Windows.Controls.UserControl.
/// </summary>
[ContentProperty("Content")]
[RuntimeNameProperty("Name")]
public class UserControl : ContentControl
{
    /// <summary>
    /// Initializes a new instance of the UserControl class.
    /// Sets Focusable to false by default (unlike Control which defaults to true).
    /// </summary>
    public UserControl()
    {
        Focusable = false;
    }
}
