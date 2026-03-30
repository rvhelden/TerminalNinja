using System.Collections.ObjectModel;
using System.Windows.Markup;
using TerminalNinja.Aot;
using TerminalNinja.Xaml.Binding;
using TerminalNinja.Xaml.Mvvm;

namespace TerminalNinja.Tests.Xaml;

/// <summary>
/// Tests for deferred binding activation — bindings declared in XAML or
/// programmatically via <see cref="BindingOperations.SetBinding"/> that
/// are attached as expressions immediately but remain dormant until
/// DataContext is available, then activate when DataContext is set.
/// </summary>
public class DeferredBindingTests
{
    // ─── Test ViewModel ──────────────────────────────────────────

    internal class DeferredViewModel : ViewModelBase
    {
        private string _title = "Hello";
        private int _count = 42;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public int Count
        {
            get => _count;
            set => SetProperty(ref _count, value);
        }

        public ObservableCollection<string> Items { get; } = ["A", "B", "C"];
    }

    // ─── Part 1: Programmatic binding via BindingOperations.SetBinding ──

    [Test]
    public async Task SetBinding_StoresExpressionOnDependencyProperty()
    {
        var textBlock = new TextBlock();
        var binding = new Binding("Title") { Mode = BindingMode.OneWay };

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        await Assert.That(BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty)).IsTrue();
        var expression = BindingOperations.GetBindingExpression(textBlock, TextBlock.TextProperty);
        await Assert.That(expression).IsNotNull();
    }

    [Test]
    public async Task SetBinding_MultipleBindings_AllStored()
    {
        var textBlock = new TextBlock();
        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty,
            new Binding("Title") { Mode = BindingMode.OneWay });
        BindingOperations.SetBinding(textBlock, TextBlock.ForegroundProperty,
            new Binding("Color") { Mode = BindingMode.OneWay });

        await Assert.That(BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty)).IsTrue();
        await Assert.That(BindingOperations.IsDataBound(textBlock, TextBlock.ForegroundProperty)).IsTrue();

        // Verify both expressions are present via GetAllExpressions
        var expressions = textBlock.GetAllExpressions().ToList();
        await Assert.That(expressions.Count).IsEqualTo(2);
    }

    [Test]
    public async Task NoBoundProperty_HasNoExpression()
    {
        var textBlock = new TextBlock();

        await Assert.That(BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty)).IsFalse();
        await Assert.That(BindingOperations.GetBindingExpression(textBlock, TextBlock.TextProperty)).IsNull();
    }

    // ─── Part 2: XAML loading with null DataContext attaches dormant expressions ──

    [Test]
    public async Task Load_NullDataContext_AttachesDormantExpression()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                   Text="{Binding Title}" />
            """;

        // Load without DataContext — expression attached but dormant
        var textBlock = TerminalXaml.Load<TextBlock>(xaml);

        // Expression IS attached
        await Assert.That(BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty)).IsTrue();
        // Text should NOT be set (no DataContext to resolve from) — still default
        await Assert.That(textBlock.Text).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Load_NullDataContext_MultipleBindings_AllAttached()
    {
        var xaml = """
            <StackPanel xmlns="http://schemas.terminalninja.dev/xaml">
                <TextBlock Text="{Binding Title}" />
                <TextBlock Text="{Binding Count}" />
            </StackPanel>
            """;

        var panel = TerminalXaml.Load<StackPanel>(xaml);

        var tb1 = (TextBlock)panel.Children[0];
        var tb2 = (TextBlock)panel.Children[1];

        await Assert.That(BindingOperations.IsDataBound(tb1, TextBlock.TextProperty)).IsTrue();
        await Assert.That(BindingOperations.IsDataBound(tb2, TextBlock.TextProperty)).IsTrue();

        // Values should be defaults (dormant)
        await Assert.That(tb1.Text).IsEqualTo(string.Empty);
        await Assert.That(tb2.Text).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Load_WithDataContext_BindingsResolveImmediately()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                   Text="{Binding Title}" />
            """;

        var vm = new DeferredViewModel();
        var textBlock = TerminalXaml.Load<TextBlock>(xaml, vm);

        // Expression is attached and resolved immediately
        await Assert.That(BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty)).IsTrue();
        await Assert.That(textBlock.Text).IsEqualTo("Hello");
    }

    // ─── Part 3: Setting DataContext after load activates bindings ──

    [Test]
    public async Task SetDataContext_AfterLoad_ActivatesBinding()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                   Text="{Binding Title}" />
            """;

        var textBlock = TerminalXaml.Load<TextBlock>(xaml);
        await Assert.That(textBlock.Text).IsEqualTo(string.Empty);

        var vm = new DeferredViewModel { Title = "Activated!" };
        textBlock.DataContext = vm;

        await Assert.That(textBlock.Text).IsEqualTo("Activated!");
    }

    [Test]
    public async Task SetDataContext_AfterProgrammaticBinding_ActivatesBinding()
    {
        var textBlock = new TextBlock();
        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty,
            new Binding("Title") { Mode = BindingMode.OneWay });

        // Dormant — no DC yet
        await Assert.That(textBlock.Text).IsEqualTo(string.Empty);

        // Set DataContext — should activate
        var vm = new DeferredViewModel { Title = "Activated!" };
        textBlock.DataContext = vm;

        await Assert.That(textBlock.Text).IsEqualTo("Activated!");
    }

    // ─── Part 4: DataContext callback triggers activation ──────

    [Test]
    public async Task DataContextChanged_NullToNonNull_ActivatesDormantBindings()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                   Text="{Binding Title}" />
            """;

        var textBlock = TerminalXaml.Load<TextBlock>(xaml);

        // Expression is attached but dormant
        await Assert.That(BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty)).IsTrue();
        await Assert.That(textBlock.Text).IsEqualTo(string.Empty);

        // Set DataContext — triggers OnDataContextChanged → InvalidateDataContextBindings
        var vm = new DeferredViewModel { Title = "Deferred!" };
        textBlock.DataContext = vm;

        await Assert.That(textBlock.Text).IsEqualTo("Deferred!");
    }

    [Test]
    public async Task DataContextChanged_NullToNonNull_BindingReactsToChanges()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                   Text="{Binding Title}" />
            """;

        var textBlock = TerminalXaml.Load<TextBlock>(xaml);

        var vm = new DeferredViewModel { Title = "Initial" };
        textBlock.DataContext = vm;

        await Assert.That(textBlock.Text).IsEqualTo("Initial");

        // Change the ViewModel property — binding should react
        vm.Title = "Changed";
        await Assert.That(textBlock.Text).IsEqualTo("Changed");
    }

    [Test]
    public async Task DataContextChanged_NullToNull_BindingStaysDormant()
    {
        var textBlock = new TextBlock();
        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty,
            new Binding("Title") { Mode = BindingMode.OneWay });

        // Setting DataContext to null should NOT activate the binding
        textBlock.DataContext = null;

        await Assert.That(textBlock.Text).IsEqualTo(string.Empty);
        // Expression should still be attached (dormant)
        await Assert.That(BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty)).IsTrue();
    }

    [Test]
    public async Task DataContextChanged_NoBoundProperties_DoesNotThrow()
    {
        var textBlock = new TextBlock { Text = "Original" };

        // No bindings — setting DataContext should be a no-op
        var vm = new DeferredViewModel();
        textBlock.DataContext = vm;

        await Assert.That(textBlock.Text).IsEqualTo("Original");
    }

    // ─── Part 5: Recursive DataContext propagation ──────────────

    [Test]
    public async Task SetDataContext_OnParent_PropagatesAndActivatesChildBindings()
    {
        var xaml = """
            <StackPanel xmlns="http://schemas.terminalninja.dev/xaml">
                <TextBlock Text="{Binding Title}" />
                <TextBlock Text="{Binding Count}" />
            </StackPanel>
            """;

        // Load without DataContext
        var panel = TerminalXaml.Load<StackPanel>(xaml);

        var tb1 = (TextBlock)panel.Children[0];
        var tb2 = (TextBlock)panel.Children[1];

        await Assert.That(BindingOperations.IsDataBound(tb1, TextBlock.TextProperty)).IsTrue();
        await Assert.That(BindingOperations.IsDataBound(tb2, TextBlock.TextProperty)).IsTrue();
        await Assert.That(tb1.Text).IsEqualTo(string.Empty);
        await Assert.That(tb2.Text).IsEqualTo(string.Empty);

        // Set DataContext on parent — should propagate to children via OnDataContextChanged
        var vm = new DeferredViewModel { Title = "Hello", Count = 99 };
        panel.DataContext = vm;

        await Assert.That(tb1.Text).IsEqualTo("Hello");
        await Assert.That(tb2.Text).IsEqualTo("99");
    }

    // ─── Part 6: DataContext switch cleanly reactivates ─────────

    [Test]
    public async Task DataContext_Switch_ReactivatesCleanly()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                   Text="{Binding Title}" />
            """;

        var vm1 = new DeferredViewModel { Title = "VM1" };
        var vm2 = new DeferredViewModel { Title = "VM2" };

        var textBlock = TerminalXaml.Load<TextBlock>(xaml, vm1);
        await Assert.That(textBlock.Text).IsEqualTo("VM1");

        // Switch DataContext — should cleanly reactivate
        textBlock.DataContext = vm2;
        await Assert.That(textBlock.Text).IsEqualTo("VM2");

        // Old VM should no longer affect the target
        vm1.Title = "Should not propagate";
        await Assert.That(textBlock.Text).IsEqualTo("VM2");

        // New VM should affect the target
        vm2.Title = "Updated VM2";
        await Assert.That(textBlock.Text).IsEqualTo("Updated VM2");
    }

    // ─── Part 7: ItemsSource deferred binding ───────────────────

    [Test]
    public async Task DeferredBinding_ItemsSource_BindsCollectionWhenDataContextSet()
    {
        var xaml = """
            <ItemsControl xmlns="http://schemas.terminalninja.dev/xaml"
                          ItemsSource="{Binding Items}" />
            """;

        // Load without DataContext
        var itemsControl = TerminalXaml.Load<ItemsControl>(xaml);

        await Assert.That(BindingOperations.IsDataBound(itemsControl, ItemsControl.ItemsSourceProperty)).IsTrue();
        await Assert.That((object?)itemsControl.ItemsSource).IsNull();

        // Set DataContext — should activate the ItemsSource binding
        var vm = new DeferredViewModel();
        itemsControl.DataContext = vm;

        await Assert.That((object?)itemsControl.ItemsSource).IsNotNull();
    }

    // ─── Part 8: Nested elements ────────────────────────────────

    [Test]
    public async Task DeferredBinding_NestedElements_AllActivateOnDataContext()
    {
        var xaml = """
            <Border xmlns="http://schemas.terminalninja.dev/xaml">
                <StackPanel>
                    <TextBlock Text="{Binding Title}" />
                </StackPanel>
            </Border>
            """;

        var border = TerminalXaml.Load<Border>(xaml);

        // The TextBlock is deeply nested
        var stackPanel = (StackPanel)border.Child!;
        var textBlock = (TextBlock)stackPanel.Children[0];

        await Assert.That(BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty)).IsTrue();
        await Assert.That(textBlock.Text).IsEqualTo(string.Empty);

        // Set DataContext on root — should propagate through the tree
        var vm = new DeferredViewModel { Title = "Nested!" };
        border.DataContext = vm;

        await Assert.That(textBlock.Text).IsEqualTo("Nested!");
    }

    // ─── Part 9: Binding mode preservation (programmatic) ───────

    [Test]
    public async Task SetBinding_PreservesBindingMode()
    {
        var textBlock = new TextBlock();
        var binding = new Binding("Title") { Mode = BindingMode.TwoWay };

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        var expression = BindingOperations.GetBindingExpression(textBlock, TextBlock.TextProperty)
            as BindingExpression;
        await Assert.That(expression).IsNotNull();
        await Assert.That(expression!.ParentBinding.Mode).IsEqualTo(BindingMode.TwoWay);
    }

    [Test]
    public async Task SetBinding_PreservesOneWayMode()
    {
        var textBlock = new TextBlock();
        var binding = new Binding("Title") { Mode = BindingMode.OneWay };

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        var expression = BindingOperations.GetBindingExpression(textBlock, TextBlock.TextProperty)
            as BindingExpression;
        await Assert.That(expression).IsNotNull();
        await Assert.That(expression!.ParentBinding.Mode).IsEqualTo(BindingMode.OneWay);
    }

    // ─── Part 10: DataTemplate cloning preserves bindings ───────

    [Test]
    public async Task DataTemplate_CloneControl_ClonesAllBindingExpressions()
    {
        // Arrange — prototype TextBlock with two bindings
        var prototype = new TextBlock();
        BindingOperations.SetBinding(prototype, TextBlock.TextProperty,
            new Binding("Title") { Mode = BindingMode.OneWay });
        BindingOperations.SetBinding(prototype, TextBlock.ForegroundProperty,
            new Binding("Color") { Mode = BindingMode.OneWay });

        var template = new DataTemplate { TemplateContent = prototype };

        // Act — create a clone
        var clone = template.CreateContent();

        // Assert — clone should have binding expressions
        await Assert.That(clone).IsNotNull();
        var cloneFe = clone as FrameworkElement;
        await Assert.That(cloneFe).IsNotNull();

        var expressions = cloneFe!.GetAllExpressions().ToList();
        await Assert.That(expressions.Count).IsEqualTo(2);

        await Assert.That(BindingOperations.IsDataBound(cloneFe, TextBlock.TextProperty)).IsTrue();
        await Assert.That(BindingOperations.IsDataBound(cloneFe, TextBlock.ForegroundProperty)).IsTrue();
    }

    [Test]
    public async Task DataTemplate_CloneControl_CloneIsDistinctFromPrototype()
    {
        // Arrange
        var prototype = new TextBlock { Text = "Proto" };
        BindingOperations.SetBinding(prototype, TextBlock.TextProperty,
            new Binding("Title") { Mode = BindingMode.OneWay });

        var template = new DataTemplate { TemplateContent = prototype };

        // Act
        var clone = template.CreateContent();

        // Assert — clone is a different instance
        await Assert.That(clone).IsNotEqualTo(prototype);

        // Prototype's expressions are preserved
        await Assert.That(BindingOperations.IsDataBound(prototype, TextBlock.TextProperty)).IsTrue();
    }

    [Test]
    public async Task DataTemplate_CloneControl_MultipleClonesEachGetOwnExpressions()
    {
        // Arrange
        var prototype = new TextBlock();
        BindingOperations.SetBinding(prototype, TextBlock.TextProperty,
            new Binding("Title") { Mode = BindingMode.OneWay });

        var template = new DataTemplate { TemplateContent = prototype };

        // Act — create two clones
        var clone1 = template.CreateContent() as FrameworkElement;
        var clone2 = template.CreateContent() as FrameworkElement;

        // Assert — each clone has its own expressions
        await Assert.That(BindingOperations.IsDataBound(clone1!, TextBlock.TextProperty)).IsTrue();
        await Assert.That(BindingOperations.IsDataBound(clone2!, TextBlock.TextProperty)).IsTrue();

        var exprs1 = clone1!.GetAllExpressions().ToList();
        var exprs2 = clone2!.GetAllExpressions().ToList();
        await Assert.That(exprs1.Count).IsEqualTo(1);
        await Assert.That(exprs2.Count).IsEqualTo(1);

        // They should be independent instances
        await Assert.That(clone1).IsNotEqualTo(clone2);
    }

    [Test]
    public async Task DataTemplate_ClonedBindings_ActivateWhenDataContextSet()
    {
        // Arrange — prototype with a binding
        var prototype = new TextBlock();
        BindingOperations.SetBinding(prototype, TextBlock.TextProperty,
            new Binding("Title") { Mode = BindingMode.OneWay });

        var template = new DataTemplate { TemplateContent = prototype };
        var clone = template.CreateContent() as TextBlock;

        // Act — set DataContext (triggers OnDataContextChanged → activates binding)
        var vm = new DeferredViewModel { Title = "Cloned!" };
        clone!.DataContext = vm;

        // Assert — binding should have activated and set the Text
        await Assert.That(clone.Text).IsEqualTo("Cloned!");
    }

    [Test]
    public async Task DataTemplate_ClonedBindings_ItemsControlIntegration()
    {
        // Arrange — DataTemplate with binding
        var prototype = new TextBlock();
        BindingOperations.SetBinding(prototype, TextBlock.TextProperty,
            new Binding("Title") { Mode = BindingMode.OneWay });

        var template = new DataTemplate { TemplateContent = prototype };

        // ItemsControl with template and items source
        var itemsControl = new ItemsControl { ItemTemplate = template };

        // Act — set ItemsSource with ViewModel items
        var items = new List<DeferredViewModel>
        {
            new() { Title = "First" },
            new() { Title = "Second" },
            new() { Title = "Third" }
        };
        itemsControl.ItemsSource = items;

        // Assert — ItemsPanel should have 3 children with correct Text
        var children = itemsControl.ItemsPanel.Children;
        await Assert.That(children.Count).IsEqualTo(3);

        var tb0 = children[0] as TextBlock;
        var tb1 = children[1] as TextBlock;
        var tb2 = children[2] as TextBlock;

        await Assert.That(tb0).IsNotNull();
        await Assert.That(tb1).IsNotNull();
        await Assert.That(tb2).IsNotNull();

        await Assert.That(tb0!.Text).IsEqualTo("First");
        await Assert.That(tb1!.Text).IsEqualTo("Second");
        await Assert.That(tb2!.Text).IsEqualTo("Third");
    }

    [Test]
    public async Task DataTemplate_NoBindings_CloneStillWorks()
    {
        // Arrange — prototype with no bindings
        var prototype = new TextBlock { Text = "Static" };
        var template = new DataTemplate { TemplateContent = prototype };

        // Act
        var clone = template.CreateContent() as TextBlock;

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Text).IsEqualTo("Static");
        await Assert.That(BindingOperations.IsDataBound(clone, TextBlock.TextProperty)).IsFalse();
    }

    // ─── Part 11: Plain POCO binding via reflection fallback ────

    /// <summary>
    /// A plain data class with no base class, no INotifyPropertyChanged.
    /// Marked with [BindableObject] so the source generator produces
    /// AOT-safe property accessors for data binding.
    /// </summary>
    [BindableObject]
    internal class PlainPocoItem
    {
        public string Message { get; set; } = string.Empty;
        public int Priority { get; set; }
    }

    [Test]
    public async Task PlainPoco_BindingActivates_WhenDataContextSet()
    {
        // Arrange — TextBlock with binding targeting a POCO property
        var textBlock = new TextBlock();
        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty,
            new Binding("Message") { Mode = BindingMode.OneWay });

        // Act — set DataContext to a plain POCO (no INPC, no ViewModelBase)
        var item = new PlainPocoItem { Message = "Hello from POCO" };
        textBlock.DataContext = item;

        // Assert — binding should resolve via reflection fallback
        await Assert.That(textBlock.Text).IsEqualTo("Hello from POCO");
    }

    [Test]
    public async Task PlainPoco_ItemsControl_RendersAllItems()
    {
        // Arrange — DataTemplate with binding for POCO
        var prototype = new TextBlock();
        BindingOperations.SetBinding(prototype, TextBlock.TextProperty,
            new Binding("Message") { Mode = BindingMode.OneWay });
        var template = new DataTemplate { TemplateContent = prototype };

        var itemsControl = new ItemsControl { ItemTemplate = template };

        // Act — set ItemsSource with plain POCOs
        var items = new List<PlainPocoItem>
        {
            new() { Message = "Log entry 1" },
            new() { Message = "Log entry 2" },
            new() { Message = "Log entry 3" }
        };
        itemsControl.ItemsSource = items;

        // Assert — all items should render with correct text
        var children = itemsControl.ItemsPanel.Children;
        await Assert.That(children.Count).IsEqualTo(3);

        var tb0 = children[0] as TextBlock;
        var tb1 = children[1] as TextBlock;
        var tb2 = children[2] as TextBlock;

        await Assert.That(tb0).IsNotNull();
        await Assert.That(tb1).IsNotNull();
        await Assert.That(tb2).IsNotNull();

        await Assert.That(tb0!.Text).IsEqualTo("Log entry 1");
        await Assert.That(tb1!.Text).IsEqualTo("Log entry 2");
        await Assert.That(tb2!.Text).IsEqualTo("Log entry 3");
    }

    [Test]
    public async Task PlainPoco_ItemsControl_RendersBackground()
    {
        // Arrange — DataTemplate with background color and binding
        var prototype = new TextBlock { Background = Color.FromHex("#141428") };
        BindingOperations.SetBinding(prototype, TextBlock.TextProperty,
            new Binding("Message") { Mode = BindingMode.OneWay });
        var template = new DataTemplate { TemplateContent = prototype };

        var itemsControl = new ItemsControl { ItemTemplate = template };

        // Act
        var items = new List<PlainPocoItem> { new() { Message = "Test" } };
        itemsControl.ItemsSource = items;

        // Assert — container should have the background and correct text
        var tb = itemsControl.ItemsPanel.Children[0] as TextBlock;
        await Assert.That(tb).IsNotNull();
        await Assert.That(tb!.Text).IsEqualTo("Test");
        await Assert.That(tb.Background).IsEqualTo(Color.FromHex("#141428"));
    }

    [Test]
    public async Task PlainPoco_DataTemplate_Clone_RendersToBuffer()
    {
        // Arrange — DataTemplate with POCO binding
        var prototype = new TextBlock { Foreground = Color.White, Background = Color.Black };
        BindingOperations.SetBinding(prototype, TextBlock.TextProperty,
            new Binding("Message") { Mode = BindingMode.OneWay });
        var template = new DataTemplate { TemplateContent = prototype };

        // Act — clone, set DataContext, render
        var clone = template.CreateContent() as TextBlock;
        clone!.DataContext = new PlainPocoItem { Message = "Rendered" };

        using var buffer = new CellBuffer(20, 1);
        clone.Render(buffer, new Rect(0, 0, 20, 1));

        // Assert — first character of "Rendered" should appear in the buffer
        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('R');
        await Assert.That(buffer.GetCell(1, 0).Character).IsEqualTo('e');
    }

    // ─── Part 14: XAML-loaded DataTemplate with bindings in Resources ────

    [Test]
    public async Task XamlDataTemplate_InResources_PrototypeHasBindingExpressions()
    {
        // Arrange — XAML with DataTemplate in Window.Resources containing {Binding Title}
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <DataTemplate x:Key="ItemTemplate">
                        <TextBlock Text="{Binding Title}" />
                    </DataTemplate>
                </Window.Resources>
                <TextBlock Text="placeholder" />
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml);

        // Act — retrieve the DataTemplate from resources
        var template = window.TryFindResource("ItemTemplate") as DataTemplate;

        // Assert — template exists and its prototype has a binding expression
        await Assert.That(template).IsNotNull();
        await Assert.That(template!.TemplateContent).IsNotNull();

        var prototype = template.TemplateContent as TextBlock;
        await Assert.That(prototype).IsNotNull();
        await Assert.That(BindingOperations.IsDataBound(prototype!, TextBlock.TextProperty)).IsTrue();
    }

    [Test]
    public async Task XamlDataTemplate_InResources_ClonedItemsGetBindingsActivated()
    {
        // Arrange — XAML with DataTemplate in Resources and ItemsControl that references it
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <DataTemplate x:Key="ItemTemplate">
                        <TextBlock Text="{Binding Title}" />
                    </DataTemplate>
                </Window.Resources>
                <ItemsControl ItemTemplate="{StaticResource ItemTemplate}" />
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml);

        // Navigate to ItemsControl
        var itemsControl = window.Content as ItemsControl;
        await Assert.That(itemsControl).IsNotNull();

        // Act — set ItemsSource with view model items
        var items = new List<DeferredViewModel>
        {
            new() { Title = "Alpha" },
            new() { Title = "Beta" },
            new() { Title = "Gamma" }
        };
        itemsControl!.ItemsSource = items;

        // Assert — each cloned TextBlock should have its binding activated
        var children = itemsControl.ItemsPanel.Children;
        await Assert.That(children.Count).IsEqualTo(3);

        var tb0 = children[0] as TextBlock;
        var tb1 = children[1] as TextBlock;
        var tb2 = children[2] as TextBlock;

        await Assert.That(tb0).IsNotNull();
        await Assert.That(tb1).IsNotNull();
        await Assert.That(tb2).IsNotNull();

        await Assert.That(tb0!.Text).IsEqualTo("Alpha");
        await Assert.That(tb1!.Text).IsEqualTo("Beta");
        await Assert.That(tb2!.Text).IsEqualTo("Gamma");
    }

    [Test]
    public async Task XamlDataTemplate_InResources_RunBindings_ActivateOnClone()
    {
        // Arrange — DataTemplate with Run bindings inside TextBlock
        // This mirrors the ActivityLogControl scenario
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <DataTemplate x:Key="LogTemplate">
                        <TextBlock>
                            <Run Text="{Binding Title}" />
                        </TextBlock>
                    </DataTemplate>
                </Window.Resources>
                <ItemsControl ItemTemplate="{StaticResource LogTemplate}" />
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml);
        var itemsControl = window.Content as ItemsControl;
        await Assert.That(itemsControl).IsNotNull();

        // Act — set items
        var items = new List<DeferredViewModel>
        {
            new() { Title = "Entry1" },
            new() { Title = "Entry2" }
        };
        itemsControl!.ItemsSource = items;

        // Assert — each cloned TextBlock should have a Run with bound text
        var children = itemsControl.ItemsPanel.Children;
        await Assert.That(children.Count).IsEqualTo(2);

        for (var i = 0; i < 2; i++)
        {
            var tb = children[i] as TextBlock;
            await Assert.That(tb).IsNotNull();
            await Assert.That(tb!.Inlines.Count).IsGreaterThanOrEqualTo(1);

            var run = tb.Inlines[0] as TerminalNinja.Documents.Run;
            await Assert.That(run).IsNotNull();
            await Assert.That(run!.Text).IsEqualTo(items[i].Title);
        }
    }

    [Test]
    public async Task XamlDataTemplate_GridWithMultipleChildren_ClonedBindingsActivate()
    {
        // Arrange — DataTemplate matching the ActivityLogControl structure:
        // Grid with FontIcon + TextBlock containing two Runs with bindings
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <DataTemplate x:Key="LogTemplate">
                        <Grid Columns="Auto *">
                            <FontIcon Symbol="Check" Foreground="Green" />
                            <TextBlock Grid.Column="1">
                                <Run Text="{Binding Title}" />
                                <Run Text="{Binding Count}" />
                            </TextBlock>
                        </Grid>
                    </DataTemplate>
                </Window.Resources>
                <ItemsControl ItemTemplate="{StaticResource LogTemplate}" />
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml);
        var itemsControl = window.Content as ItemsControl;
        await Assert.That(itemsControl).IsNotNull();

        // Act — set items
        var items = new List<DeferredViewModel>
        {
            new() { Title = "First", Count = 1 },
            new() { Title = "Second", Count = 2 }
        };
        itemsControl!.ItemsSource = items;

        // Assert — each cloned Grid should contain a TextBlock with bound Runs
        var children = itemsControl.ItemsPanel.Children;
        await Assert.That(children.Count).IsEqualTo(2);

        for (var i = 0; i < 2; i++)
        {
            var grid = children[i] as Grid;
            await Assert.That(grid).IsNotNull().Because($"Child {i} should be a Grid");

            // Grid should have 2 children: FontIcon + TextBlock
            await Assert.That(grid!.Children.Count).IsEqualTo(2).Because($"Grid {i} should have 2 children");

            var textBlock = grid.Children[1] as TextBlock;
            await Assert.That(textBlock).IsNotNull().Because($"Second child of Grid {i} should be TextBlock");

            // TextBlock should have 2 Runs with bindings
            await Assert.That(textBlock!.Inlines.Count).IsEqualTo(2).Because($"TextBlock {i} should have 2 Runs");

            var run0 = textBlock.Inlines[0] as TerminalNinja.Documents.Run;
            var run1 = textBlock.Inlines[1] as TerminalNinja.Documents.Run;
            await Assert.That(run0).IsNotNull();
            await Assert.That(run1).IsNotNull();

            await Assert.That(run0!.Text).IsEqualTo(items[i].Title)
                .Because($"Run 0 of item {i} should show Title");
            await Assert.That(run1!.Text).IsEqualTo(items[i].Count.ToString())
                .Because($"Run 1 of item {i} should show Count");
        }
    }
}
