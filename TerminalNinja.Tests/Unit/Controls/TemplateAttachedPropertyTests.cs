using System.Collections.ObjectModel;
using TerminalNinja.Rendering;
using TerminalNinja.Xaml.Mvvm;

namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests that a DataTemplate carries attached properties into its clones, so a layout panel is
/// usable inside an item template — and, on top of that, that a SharedSizeGroup lines the rows up.
/// </summary>
public class TemplateAttachedPropertyTests
{
    internal sealed class ShortcutRow : ViewModelBase
    {
        public string Key { get; set => SetProperty(ref field, value); } = "";
        public string Description { get; set => SetProperty(ref field, value); } = "";
    }

    internal sealed class ShortcutsViewModel : ViewModelBase
    {
        public ObservableCollection<ShortcutRow> Lines { get; } = [];
    }

    /// <summary>Two columns per row, each row its own Grid, keys sharing one width.</summary>
    private const string SharedLayout = """
        <Window xmlns="http://schemas.terminalninja.dev/xaml"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <Window.Resources>
                <DataTemplate x:Key="RowTemplate">
                    <Grid ColumnSpacing="1">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="auto" SharedSizeGroup="keys" />
                            <ColumnDefinition Width="*" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="{Binding Key}" />
                        <TextBlock Grid.Column="1" Text="{Binding Description}" />
                    </Grid>
                </DataTemplate>
            </Window.Resources>
            <ItemsControl Grid.IsSharedSizeScope="True"
                          ItemsSource="{Binding Lines}" ItemTemplate="{StaticResource RowTemplate}" />
        </Window>
        """;

    /// <summary>The same rows with no group, so each Grid sizes its key column alone.</summary>
    private const string UngroupedLayout = """
        <Window xmlns="http://schemas.terminalninja.dev/xaml"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <Window.Resources>
                <DataTemplate x:Key="RowTemplate">
                    <Grid ColumnSpacing="1">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="auto" />
                            <ColumnDefinition Width="*" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="{Binding Key}" />
                        <TextBlock Grid.Column="1" Text="{Binding Description}" />
                    </Grid>
                </DataTemplate>
            </Window.Resources>
            <ItemsControl ItemsSource="{Binding Lines}" ItemTemplate="{StaticResource RowTemplate}" />
        </Window>
        """;

    private static string[] Capture(string layout, params (string Key, string Description)[] rows)
    {
        var vm = new ShortcutsViewModel();
        foreach (var (key, description) in rows)
        {
            vm.Lines.Add(new ShortcutRow { Key = key, Description = description });
        }

        var window = TerminalXaml.Load<Window>(layout, vm);

        // One frame only. That is what a headless capture gets, and the mode a shared size has to
        // be right in — an interactive app would paper over a late answer on the next redraw.
        return FrameCapture.ToText(window, 40, 6).Split('\n');
    }

    [Test]
    public async Task CloneControl_KeepsAttachedProperties()
    {
        var source = new TextBlock { Text = "x" };
        Grid.SetColumn(source, 3);
        Grid.SetRow(source, 2);
        Grid.SetColumnSpan(source, 4);
        StackPanel.SetSizeMode(source, ChildSizeMode.Fixed);
        StackPanel.SetFixedSize(source, 7);

        var clone = new DataTemplate { TemplateContent = source }.CreateContent();

        await Assert.That(clone).IsNotNull();
        await Assert.That(Grid.GetColumn(clone!)).IsEqualTo(3);
        await Assert.That(Grid.GetRow(clone!)).IsEqualTo(2);
        await Assert.That(Grid.GetColumnSpan(clone!)).IsEqualTo(4);
        await Assert.That(StackPanel.GetSizeMode(clone!)).IsEqualTo(ChildSizeMode.Fixed);
        await Assert.That(StackPanel.GetFixedSize(clone!)).IsEqualTo(7);
    }

    [Test]
    public async Task ItemTemplateWithAGrid_PutsEachChildInItsOwnColumn()
    {
        // Before attached properties survived the clone, both TextBlocks lost their Grid.Column
        // and drew over each other in cell zero.
        var lines = Capture(UngroupedLayout, ("esc", "back"));

        await Assert.That(lines[0].TrimEnd()).IsEqualTo("esc back");
    }

    [Test]
    public async Task Ungrouped_KeyColumnsStayRagged()
    {
        var lines = Capture(UngroupedLayout, ("esc", "back"), ("backspace", "up"));

        await Assert.That(lines[0].TrimEnd()).IsEqualTo("esc back");
        await Assert.That(lines[1].TrimEnd()).IsEqualTo("backspace up");
    }

    [Test]
    public async Task SharedSizeGroup_AlignsEveryRowOfAnItemsControl()
    {
        var lines = Capture(SharedLayout, ("esc", "back"), ("backspace", "up"), ("q", "quit"));

        // "backspace" is the widest key at nine cells, plus one of ColumnSpacing.
        await Assert.That(lines[0].TrimEnd()).IsEqualTo("esc       back");
        await Assert.That(lines[1].TrimEnd()).IsEqualTo("backspace up");
        await Assert.That(lines[2].TrimEnd()).IsEqualTo("q         quit");
    }
}
