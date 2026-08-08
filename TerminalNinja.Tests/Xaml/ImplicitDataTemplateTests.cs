using TerminalNinja.Controls;
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Mvvm;

namespace TerminalNinja.Tests.Xaml;

/// <summary>
/// A person, templated implicitly by <c>DataType</c>.
/// </summary>
public sealed class ImplicitPerson : ViewModelBase
{
    public string Name { get; set; } = string.Empty;

    public override string ToString() => $"person:{Name}";
}

/// <summary>
/// A product, so a single dictionary can hold two competing implicit templates.
/// </summary>
public sealed class ImplicitProduct : ViewModelBase
{
    public string Label { get; set; } = string.Empty;

    public override string ToString() => $"product:{Label}";
}

/// <summary>
/// An item with no template of its own — used to check the ToString() fallback.
/// </summary>
public sealed class ImplicitStranger : ViewModelBase
{
    public override string ToString() => "stranger";
}

/// <summary>
/// Base of <see cref="ImplicitDerived"/>, used to check base-type fallback.
/// </summary>
public class ImplicitAnimal : ViewModelBase
{
    public string Title { get; set; } = string.Empty;
}

/// <summary>
/// Has no template of its own; must be caught by the one on <see cref="ImplicitAnimal"/>.
/// </summary>
public sealed class ImplicitDerived : ImplicitAnimal;

/// <summary>
/// Host view model carrying the item list and the single-content object.
/// </summary>
public sealed class ImplicitTemplateHost : ViewModelBase
{
    public List<object> Items { get; set; } = [];

    public object? Current { get; set; }
}

/// <summary>
/// Implicit <see cref="DataTemplate"/> selection: a template declared with a
/// <c>DataType</c> and no <c>x:Key</c> is applied to items of that type automatically.
/// </summary>
public class ImplicitDataTemplateTests
{
    private const string Xmlns = """
        xmlns="http://schemas.terminalninja.dev/xaml"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:TerminalNinja.Tests.Xaml;assembly=TerminalNinja.Tests"
        """;

    private static List<TextBlock?> TextBlocksOf(ItemsControl items) =>
        [.. items.ItemsPanel.Children.Select(c => c as TextBlock)];

    // ─── ItemsControl ────────────────────────────────────────────────

    [Test]
    public async Task ImplicitTemplate_MatchingDataType_IsAppliedToItems()
    {
        var window = TerminalXaml.Load<Window>($$"""
            <Window {{Xmlns}}>
                <Window.Resources>
                    <DataTemplate DataType="local:ImplicitPerson">
                        <TextBlock Text="{Binding Name}" />
                    </DataTemplate>
                </Window.Resources>
                <ItemsControl />
            </Window>
            """);

        var list = window.Content as ItemsControl;
        await Assert.That(list).IsNotNull();

        list!.ItemsSource = new List<object>
        {
            new ImplicitPerson { Name = "Ada" },
            new ImplicitPerson { Name = "Grace" }
        };

        var texts = TextBlocksOf(list);
        await Assert.That(texts.Count).IsEqualTo(2);
        await Assert.That(texts[0]!.Text).IsEqualTo("Ada");
        await Assert.That(texts[1]!.Text).IsEqualTo("Grace");
    }

    [Test]
    public async Task ExplicitItemTemplate_OverridesImplicitMatch()
    {
        var window = TerminalXaml.Load<Window>($$"""
            <Window {{Xmlns}}>
                <Window.Resources>
                    <DataTemplate DataType="local:ImplicitPerson">
                        <TextBlock Text="implicit" />
                    </DataTemplate>
                    <DataTemplate x:Key="Explicit">
                        <TextBlock Text="explicit" />
                    </DataTemplate>
                </Window.Resources>
                <ItemsControl ItemTemplate="{StaticResource Explicit}" />
            </Window>
            """);

        var list = (ItemsControl)window.Content!;
        list.ItemsSource = new List<object> { new ImplicitPerson { Name = "Ada" } };

        var texts = TextBlocksOf(list);
        await Assert.That(texts.Count).IsEqualTo(1);
        await Assert.That(texts[0]!.Text).IsEqualTo("explicit");
    }

    [Test]
    public async Task TwoDataTypes_EachItemGetsItsOwnTemplate()
    {
        var window = TerminalXaml.Load<Window>($$"""
            <Window {{Xmlns}}>
                <Window.Resources>
                    <DataTemplate DataType="local:ImplicitPerson">
                        <TextBlock Text="{Binding Name}" />
                    </DataTemplate>
                    <DataTemplate DataType="local:ImplicitProduct">
                        <TextBlock Text="{Binding Label}" />
                    </DataTemplate>
                </Window.Resources>
                <ItemsControl />
            </Window>
            """);

        var list = (ItemsControl)window.Content!;
        list.ItemsSource = new List<object>
        {
            new ImplicitPerson { Name = "Ada" },
            new ImplicitProduct { Label = "Widget" },
            new ImplicitPerson { Name = "Grace" }
        };

        var texts = TextBlocksOf(list);
        await Assert.That(texts.Count).IsEqualTo(3);
        await Assert.That(texts[0]!.Text).IsEqualTo("Ada");
        await Assert.That(texts[1]!.Text).IsEqualTo("Widget");
        await Assert.That(texts[2]!.Text).IsEqualTo("Grace");
    }

    [Test]
    public async Task ItemWithNoMatchingTemplate_FallsBackToToString()
    {
        var window = TerminalXaml.Load<Window>($$"""
            <Window {{Xmlns}}>
                <Window.Resources>
                    <DataTemplate DataType="local:ImplicitPerson">
                        <TextBlock Text="{Binding Name}" />
                    </DataTemplate>
                </Window.Resources>
                <ItemsControl />
            </Window>
            """);

        var list = (ItemsControl)window.Content!;
        list.ItemsSource = new List<object>
        {
            new ImplicitPerson { Name = "Ada" },
            new ImplicitStranger()
        };

        var texts = TextBlocksOf(list);
        await Assert.That(texts.Count).IsEqualTo(2);
        await Assert.That(texts[0]!.Text).IsEqualTo("Ada");
        await Assert.That(texts[1]!.Text).IsEqualTo("stranger");
    }

    [Test]
    public async Task KeyedTemplate_WithDataType_IsNotAppliedImplicitly()
    {
        // An x:Key'd template stays addressable only by that key — giving it a DataType as
        // documentation must not turn it into an estate-wide default.
        var window = TerminalXaml.Load<Window>($$"""
            <Window {{Xmlns}}>
                <Window.Resources>
                    <DataTemplate x:Key="PersonTemplate" DataType="local:ImplicitPerson">
                        <TextBlock Text="{Binding Name}" />
                    </DataTemplate>
                </Window.Resources>
                <ItemsControl />
            </Window>
            """);

        var list = (ItemsControl)window.Content!;
        list.ItemsSource = new List<object> { new ImplicitPerson { Name = "Ada" } };

        var texts = TextBlocksOf(list);
        await Assert.That(texts[0]!.Text).IsEqualTo("person:Ada");
    }

    [Test]
    public async Task ImplicitTemplate_OnBaseType_AppliesToDerivedItem()
    {
        var window = TerminalXaml.Load<Window>($$"""
            <Window {{Xmlns}}>
                <Window.Resources>
                    <DataTemplate DataType="local:ImplicitAnimal">
                        <TextBlock Text="{Binding Title}" />
                    </DataTemplate>
                </Window.Resources>
                <ItemsControl />
            </Window>
            """);

        var list = (ItemsControl)window.Content!;
        list.ItemsSource = new List<object> { new ImplicitDerived { Title = "Cat" } };

        var texts = TextBlocksOf(list);
        await Assert.That(texts[0]!.Text).IsEqualTo("Cat");
    }

    [Test]
    public async Task ImplicitTemplate_ExactTypeWins_OverBaseType()
    {
        var window = TerminalXaml.Load<Window>($$"""
            <Window {{Xmlns}}>
                <Window.Resources>
                    <DataTemplate DataType="local:ImplicitAnimal">
                        <TextBlock Text="base" />
                    </DataTemplate>
                    <DataTemplate DataType="local:ImplicitDerived">
                        <TextBlock Text="derived" />
                    </DataTemplate>
                </Window.Resources>
                <ItemsControl />
            </Window>
            """);

        var list = (ItemsControl)window.Content!;
        list.ItemsSource = new List<object> { new ImplicitDerived { Title = "Cat" } };

        var texts = TextBlocksOf(list);
        await Assert.That(texts[0]!.Text).IsEqualTo("derived");
    }

    [Test]
    public async Task ImplicitTemplate_IsFound_WhenItemsAreBoundDuringLoad()
    {
        // The XAML loader fills a control before hanging it off its parent, so the first lookup
        // searches a resource chain that stops at the control itself. The template must still be
        // picked up once the tree is whole.
        var vm = new ImplicitTemplateHost
        {
            Items = [new ImplicitPerson { Name = "Ada" }],
            Current = new ImplicitPerson { Name = "Grace" }
        };

        var window = TerminalXaml.Load<Window>($$"""
            <Window {{Xmlns}}>
                <Window.Resources>
                    <DataTemplate DataType="local:ImplicitPerson">
                        <TextBlock Text="{Binding Name}" />
                    </DataTemplate>
                </Window.Resources>
                <StackPanel>
                    <ItemsControl ItemsSource="{Binding Items}" />
                    <ContentControl Content="{Binding Current}" />
                </StackPanel>
            </Window>
            """, vm);

        var panel = (StackPanel)window.Content!;
        var list = (ItemsControl)panel.Children[0];
        var host = (ContentControl)panel.Children[1];

        // Rendering is when a presenter finally resolves its child.
        window.Render(new CellBuffer(40, 10), new Rect(0, 0, 40, 10));

        await Assert.That(TextBlocksOf(list)[0]!.Text).IsEqualTo("Ada");

        var content = host.GetLogicalChildren().OfType<TextBlock>().FirstOrDefault();
        await Assert.That(content!.Text).IsEqualTo("Grace");
    }

    // ─── ListBox ─────────────────────────────────────────────────────

    [Test]
    public async Task ImplicitTemplate_IsAppliedToListBoxItems()
    {
        // IsVirtualizing off so the containers exist without a render pass.
        var window = TerminalXaml.Load<Window>($$"""
            <Window {{Xmlns}}>
                <Window.Resources>
                    <DataTemplate DataType="local:ImplicitPerson">
                        <TextBlock Text="{Binding Name}" />
                    </DataTemplate>
                </Window.Resources>
                <ListBox IsVirtualizing="False" />
            </Window>
            """);

        var list = (ListBox)window.Content!;
        list.ItemsSource = new List<object> { new ImplicitPerson { Name = "Ada" } };

        var item = list.ItemsPanel.Children[0] as ListBoxItem;
        await Assert.That(item).IsNotNull();
        await Assert.That((item!.Content as TextBlock)?.Text).IsEqualTo("Ada");
    }

    // ─── ContentControl / ContentPresenter ───────────────────────────

    [Test]
    public async Task ImplicitTemplate_IsAppliedToContentControlContent()
    {
        var window = TerminalXaml.Load<Window>($$"""
            <Window {{Xmlns}}>
                <Window.Resources>
                    <DataTemplate DataType="local:ImplicitPerson">
                        <TextBlock Text="{Binding Name}" />
                    </DataTemplate>
                </Window.Resources>
                <ContentControl />
            </Window>
            """);

        var host = (ContentControl)window.Content!;
        host.Content = new ImplicitPerson { Name = "Ada" };

        var text = host.GetLogicalChildren().OfType<TextBlock>().FirstOrDefault();
        await Assert.That(text).IsNotNull();
        await Assert.That(text!.Text).IsEqualTo("Ada");
    }

    [Test]
    public async Task ExplicitContentTemplate_OverridesImplicitMatch()
    {
        var window = TerminalXaml.Load<Window>($$"""
            <Window {{Xmlns}}>
                <Window.Resources>
                    <DataTemplate DataType="local:ImplicitPerson">
                        <TextBlock Text="implicit" />
                    </DataTemplate>
                    <DataTemplate x:Key="Explicit">
                        <TextBlock Text="explicit" />
                    </DataTemplate>
                </Window.Resources>
                <ContentControl ContentTemplate="{StaticResource Explicit}" />
            </Window>
            """);

        var host = (ContentControl)window.Content!;
        host.Content = new ImplicitPerson { Name = "Ada" };

        var text = host.GetLogicalChildren().OfType<TextBlock>().FirstOrDefault();
        await Assert.That(text!.Text).IsEqualTo("explicit");
    }

    [Test]
    public async Task ContentWithNoMatchingTemplate_FallsBackToToString()
    {
        var window = TerminalXaml.Load<Window>($$"""
            <Window {{Xmlns}}>
                <Window.Resources>
                    <DataTemplate DataType="local:ImplicitPerson">
                        <TextBlock Text="{Binding Name}" />
                    </DataTemplate>
                </Window.Resources>
                <ContentControl />
            </Window>
            """);

        var host = (ContentControl)window.Content!;
        host.Content = new ImplicitStranger();

        var text = host.GetLogicalChildren().OfType<TextBlock>().FirstOrDefault();
        await Assert.That(text!.Text).IsEqualTo("stranger");
    }

    // ─── Resource dictionary plumbing ────────────────────────────────

    [Test]
    public async Task KeylessTemplate_IsFiledUnderDataTemplateKey()
    {
        var dict = TerminalXaml.LoadResourceDictionary($$"""
            <ResourceDictionary {{Xmlns}}>
                <DataTemplate DataType="local:ImplicitPerson">
                    <TextBlock Text="{Binding Name}" />
                </DataTemplate>
            </ResourceDictionary>
            """);

        await Assert.That(dict.ContainsKey(new DataTemplateKey(typeof(ImplicitPerson)))).IsTrue();
        await Assert.That(dict[new DataTemplateKey(typeof(ImplicitPerson))]).IsTypeOf<DataTemplate>();
    }

    [Test]
    public async Task KeylessTemplate_WithoutDataType_Throws()
    {
        var act = () => TerminalXaml.LoadResourceDictionary($$"""
            <ResourceDictionary {{Xmlns}}>
                <DataTemplate>
                    <TextBlock Text="orphan" />
                </DataTemplate>
            </ResourceDictionary>
            """);

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task DataTemplateKey_DoesNotCollideWithImplicitStyleKey()
    {
        // Implicit styles are keyed by the bare TargetType. A template for the same type must
        // live beside it, not on top of it.
        var dict = TerminalXaml.LoadResourceDictionary($$"""
            <ResourceDictionary {{Xmlns}}>
                <Style TargetType="TextBlock">
                    <Setter Property="Text" Value="styled" />
                </Style>
                <DataTemplate DataType="TextBlock">
                    <TextBlock Text="templated" />
                </DataTemplate>
            </ResourceDictionary>
            """);

        await Assert.That(dict[typeof(TextBlock)]).IsTypeOf<Style>();
        await Assert.That(dict[new DataTemplateKey(typeof(TextBlock))]).IsTypeOf<DataTemplate>();
    }

    [Test]
    public async Task ProgrammaticDataTemplateKey_IsFoundByItemsControl()
    {
        var panel = new StackPanel();
        panel.Resources[new DataTemplateKey(typeof(ImplicitPerson))] =
            new DataTemplate { TemplateFactory = () => new TextBlock { Text = "code" } };

        var list = new ItemsControl();
        panel.Children.Add(list);
        list.ItemsSource = new List<object> { new ImplicitPerson { Name = "Ada" } };

        var texts = TextBlocksOf(list);
        await Assert.That(texts[0]!.Text).IsEqualTo("code");
    }

    [Test]
    public async Task ItemsSetBeforeParenting_StillPickUpTheTemplate()
    {
        // Built bottom-up: the control is filled while it is still an orphan, so the first
        // lookup can only fail. Grafting it on has to make it look again.
        var list = new ItemsControl { ItemsSource = new List<object> { new ImplicitPerson { Name = "Ada" } } };
        await Assert.That(TextBlocksOf(list)[0]!.Text).IsEqualTo("person:Ada");

        var panel = new StackPanel();
        panel.Resources[new DataTemplateKey(typeof(ImplicitPerson))] =
            new DataTemplate { TemplateFactory = () => new TextBlock { Text = "late" } };
        panel.Children.Add(list);

        await Assert.That(TextBlocksOf(list)[0]!.Text).IsEqualTo("late");
    }

    [Test]
    public async Task ContentSetBeforeParenting_StillPicksUpTheTemplate()
    {
        var host = new ContentControl { Content = new ImplicitPerson { Name = "Ada" } };

        var panel = new StackPanel();
        panel.Resources[new DataTemplateKey(typeof(ImplicitPerson))] =
            new DataTemplate { TemplateFactory = () => new TextBlock { Text = "late" } };
        panel.Children.Add(host);

        // The presenter re-resolves on the next layout/render pass.
        host.Render(new CellBuffer(20, 3), new Rect(0, 0, 20, 3));

        var text = host.GetLogicalChildren().OfType<TextBlock>().FirstOrDefault();
        await Assert.That(text!.Text).IsEqualTo("late");
    }
}
