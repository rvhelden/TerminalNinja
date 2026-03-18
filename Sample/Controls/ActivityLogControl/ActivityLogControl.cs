using System.Collections;
using TerminalNinja.Controls;
using TerminalNinja.DependencySystem;

namespace Sample;

/// <summary>
/// A reusable UserControl that displays activity log entries.
/// The visual structure is defined in ActivityLogControl.xaml with x:Class support.
///
/// Exposes an <see cref="ItemsSource"/> DependencyProperty that consumers bind to
/// their ViewModel's collection. The callback pushes the value down to the inner
/// ItemsControl, decoupling the UserControl from any specific ViewModel shape.
///
/// Usage in XAML:
///   &lt;sample:ActivityLogControl ItemsSource="{Binding LogEntries}" /&gt;
///
/// Visual structure (from XAML):
///   Border (rounded, dark background)
///     StackPanel (vertical)
///       TextBlock (title: "Activity Log")
///       ItemsControl (items driven by ItemsSource DP)
/// </summary>
public partial class ActivityLogControl : UserControl
{
    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ActivityLogControl),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true,
                propertyChangedCallback: (d, e) =>
                {
                    var control = (ActivityLogControl)d;
                    control.LogItemsControl.ItemsSource = (IEnumerable?)e.NewValue;
                }));

    /// <summary>
    /// Gets or sets the collection of items displayed in the activity log.
    /// Bind this to your ViewModel's collection (e.g., <c>ObservableCollection&lt;LogEntry&gt;</c>).
    /// </summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ActivityLogControl()
    {
        InitializeComponent();
    }
}
