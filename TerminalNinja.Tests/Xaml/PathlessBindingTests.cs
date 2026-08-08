using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Markup;
using TerminalNinja.Xaml.Binding;
using TerminalNinja.Xaml.Mvvm;

namespace TerminalNinja.Tests.Xaml;

/// <summary>
/// Tests for the pathless binding — <c>{Binding}</c>, equivalently <c>{Binding Path=.}</c> —
/// which binds to the DataContext object itself rather than to a property on it.
/// Without it a collection of plain strings cannot be templated at all: every item needs a
/// wrapper view model whose only job is to expose the string under a property name.
/// </summary>
public class PathlessBindingTests
{
    internal class NamedViewModel : ViewModelBase
    {
        private string _title = "Initial";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }

    // ─── The DataContext itself as the source ───────────────────────

    [Test]
    public async Task PathlessBinding_StringDataContext_BindsToTheStringItself()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                       Text="{Binding}" />
            """;

        var textBlock = TerminalXaml.Load<TextBlock>(xaml, "hello world");

        await Assert.That(textBlock.Text).IsEqualTo("hello world");
    }

    [Test]
    public async Task PathlessBinding_DotPath_IsEquivalentToEmptyBinding()
    {
        var dotted = TerminalXaml.Load<TextBlock>(
            """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                       Text="{Binding Path=.}" />
            """,
            "same thing");

        var positional = TerminalXaml.Load<TextBlock>(
            """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                       Text="{Binding .}" />
            """,
            "same thing");

        var empty = TerminalXaml.Load<TextBlock>(
            """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                       Text="{Binding}" />
            """,
            "same thing");

        await Assert.That(dotted.Text).IsEqualTo("same thing");
        await Assert.That(positional.Text).IsEqualTo("same thing");
        await Assert.That(empty.Text).IsEqualTo("same thing");
    }

    [Test]
    public async Task PathlessBinding_NonStringDataContext_ConvertsLikeAnyOtherBinding()
    {
        // The pathless binding invents no coercion of its own: the value goes through exactly
        // the same Convert.ChangeType path as a property binding, so an IConvertible source
        // lands in a string target and a plain object does not.
        var convertible = TerminalXaml.Load<TextBlock>(
            """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                       Text="{Binding}" />
            """,
            42);

        await Assert.That(convertible.Text).IsEqualTo("42");

        var textBlock = new TextBlock();
        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, new Binding("Title"));
        textBlock.DataContext = new NamedViewModel { Title = "42" };
        await Assert.That(textBlock.Text).IsEqualTo("42");
    }

    [Test]
    public async Task PathlessBinding_ViaBindingObject_ResolvesWhenDataContextArrivesLater()
    {
        var textBlock = new TextBlock();
        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty,
            new Binding { Mode = BindingMode.OneWay });

        await Assert.That(textBlock.Text).IsEqualTo(string.Empty);

        textBlock.DataContext = "deferred";

        await Assert.That(textBlock.Text).IsEqualTo("deferred");
    }

    // ─── The motivating case: a collection of plain strings ─────────

    [Test]
    public async Task PathlessBinding_ItemsControlOverStrings_RendersEachString()
    {
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <DataTemplate x:Key="StringTemplate">
                        <TextBlock Text="{Binding}" />
                    </DataTemplate>
                </Window.Resources>
                <ItemsControl ItemTemplate="{StaticResource StringTemplate}" />
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml);
        var itemsControl = window.Content as ItemsControl;
        await Assert.That(itemsControl).IsNotNull();

        itemsControl!.ItemsSource = new ObservableCollection<string> { "alpha", "beta", "gamma" };

        var children = itemsControl.ItemsPanel.Children;
        await Assert.That(children.Count).IsEqualTo(3);
        await Assert.That((children[0] as TextBlock)!.Text).IsEqualTo("alpha");
        await Assert.That((children[1] as TextBlock)!.Text).IsEqualTo("beta");
        await Assert.That((children[2] as TextBlock)!.Text).IsEqualTo("gamma");
    }

    [Test]
    public async Task PathlessBinding_ListBoxOverStrings_RendersEachString()
    {
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <DataTemplate x:Key="StringTemplate">
                        <TextBlock Text="{Binding}" />
                    </DataTemplate>
                </Window.Resources>
                <ListBox ItemTemplate="{StaticResource StringTemplate}" />
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml);
        var listBox = window.Content as ListBox;
        await Assert.That(listBox).IsNotNull();

        listBox!.ItemsSource = new ObservableCollection<string> { "one", "two" };

        var children = listBox.ItemsPanel.Children;
        await Assert.That(children.Count).IsEqualTo(2);

        // Each row is a ListBoxItem whose content is the templated TextBlock.
        var texts = children
            .OfType<ListBoxItem>()
            .Select(item => (item.Content as TextBlock)?.Text)
            .ToList();

        await Assert.That(texts.Count).IsEqualTo(2);
        await Assert.That(texts[0]).IsEqualTo("one");
        await Assert.That(texts[1]).IsEqualTo("two");
    }

    [Test]
    public async Task PathlessBinding_ObservableCollectionReplace_UpdatesTheRow()
    {
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <DataTemplate x:Key="StringTemplate">
                        <TextBlock Text="{Binding}" />
                    </DataTemplate>
                </Window.Resources>
                <ItemsControl ItemTemplate="{StaticResource StringTemplate}" />
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml);
        var itemsControl = (ItemsControl)window.Content!;

        var items = new ObservableCollection<string> { "first", "second" };
        itemsControl.ItemsSource = items;

        // Whole-item replacement — the only kind possible when the item is an immutable string.
        items[1] = "replaced";

        var children = itemsControl.ItemsPanel.Children;
        await Assert.That(children.Count).IsEqualTo(2);
        await Assert.That((children[0] as TextBlock)!.Text).IsEqualTo("first");
        await Assert.That((children[1] as TextBlock)!.Text).IsEqualTo("replaced");
    }

    [Test]
    public async Task PathlessBinding_ObservableCollectionAdd_AppendsARow()
    {
        var template = new DataTemplate { TemplateContent = CreatePathlessTextBlock() };
        var itemsControl = new ItemsControl { ItemTemplate = template };

        var items = new ObservableCollection<string> { "a" };
        itemsControl.ItemsSource = items;
        items.Add("b");

        var children = itemsControl.ItemsPanel.Children;
        await Assert.That(children.Count).IsEqualTo(2);
        await Assert.That((children[0] as TextBlock)!.Text).IsEqualTo("a");
        await Assert.That((children[1] as TextBlock)!.Text).IsEqualTo("b");
    }

    // ─── A normal path binding must be unaffected ───────────────────

    [Test]
    public async Task PathBinding_StillResolvesTheProperty_AndTracksChanges()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                       Text="{Binding Title}" />
            """;

        var vm = new NamedViewModel();
        var textBlock = TerminalXaml.Load<TextBlock>(xaml, vm);

        await Assert.That(textBlock.Text).IsEqualTo("Initial");

        vm.Title = "Updated";

        await Assert.That(textBlock.Text).IsEqualTo("Updated");
    }

    [Test]
    public async Task PathBinding_UnknownProperty_DoesNotFallBackToTheDataContextItself()
    {
        // Regression guard: the self path must be reachable only through "." / no path,
        // never as a silent fallback for a path that failed to resolve.
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                       Text="{Binding NoSuchProperty}" />
            """;

        var textBlock = TerminalXaml.Load<TextBlock>(xaml, new NamedViewModel());

        await Assert.That(textBlock.Text).IsEqualTo(string.Empty);
    }

    private static TextBlock CreatePathlessTextBlock()
    {
        var prototype = new TextBlock();
        BindingOperations.SetBinding(prototype, TextBlock.TextProperty,
            new Binding { Mode = BindingMode.OneWay });
        return prototype;
    }
}
