using System.Collections.ObjectModel;
using TerminalNinja.Controls;
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Binding;
using TerminalNinja.Xaml.Mvvm;

namespace TerminalNinja.Tests.Xaml;

/// <summary>
/// Tests for deferred binding activation — bindings declared in XAML
/// that are loaded without a DataContext and activated later when
/// DataContext is set.
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

    // ─── Part 1: ElementBinding storage on FrameworkElement ──────

    [Test]
    public async Task AddPendingBinding_StoresBindingOnElement()
    {
        var textBlock = new TextBlock();
        var binding = new ElementBinding("Text", "Title", BindingMode.OneWay, null, null);

        textBlock.AddPendingBinding(binding);

        await Assert.That(textBlock.PendingBindings).IsNotNull();
        await Assert.That(textBlock.PendingBindings!.Count).IsEqualTo(1);
        await Assert.That(textBlock.PendingBindings[0].TargetPropertyName).IsEqualTo("Text");
        await Assert.That(textBlock.PendingBindings[0].Path).IsEqualTo("Title");
    }

    [Test]
    public async Task AddPendingBinding_MultiplePendingBindings_AllStored()
    {
        var textBlock = new TextBlock();
        textBlock.AddPendingBinding(new ElementBinding("Text", "Title", BindingMode.OneWay, null, null));
        textBlock.AddPendingBinding(new ElementBinding("Foreground", "Color", BindingMode.OneWay, null, null));

        await Assert.That(textBlock.PendingBindings!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task PendingBindings_NoneAdded_ReturnsNull()
    {
        var textBlock = new TextBlock();

        await Assert.That(textBlock.PendingBindings).IsNull();
    }

    // ─── Part 2: XAML loading without DataContext stores pending bindings ──

    [Test]
    public async Task Load_NullDataContext_StoresPendingBindingsOnElement()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                   Text="{Binding Title}" />
            """;

        // Load with null DataContext — bindings should be stored, not activated
        var textBlock = TerminalXaml.Load<TextBlock>(xaml, dataContext: null, bindingManager: null);

        // Text should NOT be set (no DataContext to bind from)
        await Assert.That(textBlock.PendingBindings).IsNotNull();
        await Assert.That(textBlock.PendingBindings!.Count).IsEqualTo(1);
        await Assert.That(textBlock.PendingBindings[0].Path).IsEqualTo("Title");
    }

    [Test]
    public async Task Load_NullDataContext_MultipleBindings_AllStored()
    {
        var xaml = """
            <StackPanel xmlns="http://schemas.terminalninja.dev/xaml">
                <TextBlock Text="{Binding Title}" />
                <TextBlock Text="{Binding Count}" />
            </StackPanel>
            """;

        var panel = TerminalXaml.Load<StackPanel>(xaml, dataContext: null, bindingManager: null);

        var tb1 = (TextBlock)panel.Children[0];
        var tb2 = (TextBlock)panel.Children[1];

        await Assert.That(tb1.PendingBindings).IsNotNull();
        await Assert.That(tb1.PendingBindings!.Count).IsEqualTo(1);
        await Assert.That(tb1.PendingBindings[0].Path).IsEqualTo("Title");

        await Assert.That(tb2.PendingBindings).IsNotNull();
        await Assert.That(tb2.PendingBindings!.Count).IsEqualTo(1);
        await Assert.That(tb2.PendingBindings[0].Path).IsEqualTo("Count");
    }

    [Test]
    public async Task Load_WithDataContext_NoPendingBindingsStored()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                   Text="{Binding Title}" />
            """;

        var vm = new DeferredViewModel();
        var bm = new BindingManager();
        var textBlock = TerminalXaml.Load<TextBlock>(xaml, vm, bm);

        // When loaded with DataContext, bindings are activated immediately — no pending
        await Assert.That(textBlock.PendingBindings).IsNull();
        await Assert.That(textBlock.Text).IsEqualTo("Hello");

        bm.Dispose();
    }

    // ─── Part 3: ActivatePendingBindings ────────────────────────

    [Test]
    public async Task ActivatePendingBindings_SetsPropertyValue()
    {
        var textBlock = new TextBlock();
        textBlock.AddPendingBinding(new ElementBinding("Text", "Title", BindingMode.OneWay, null, null));

        var vm = new DeferredViewModel { Title = "Activated!" };
        var bm = new BindingManager();
        textBlock.ActivatePendingBindings(bm, vm);

        await Assert.That(textBlock.Text).IsEqualTo("Activated!");
        bm.Dispose();
    }

    [Test]
    public async Task ActivatePendingBindings_ClearsPendingList()
    {
        var textBlock = new TextBlock();
        textBlock.AddPendingBinding(new ElementBinding("Text", "Title", BindingMode.OneWay, null, null));

        var vm = new DeferredViewModel();
        var bm = new BindingManager();
        textBlock.ActivatePendingBindings(bm, vm);

        // Pending bindings should be cleared after activation
        await Assert.That(textBlock.PendingBindings).IsNull();
        bm.Dispose();
    }

    [Test]
    public async Task ActivatePendingBindings_OneWay_ReactsToSourceChanges()
    {
        var textBlock = new TextBlock();
        textBlock.AddPendingBinding(new ElementBinding("Text", "Title", BindingMode.OneWay, null, null));

        var vm = new DeferredViewModel { Title = "Before" };
        var bm = new BindingManager();
        textBlock.ActivatePendingBindings(bm, vm);

        await Assert.That(textBlock.Text).IsEqualTo("Before");

        // Change source — target should update
        vm.Title = "After";
        await Assert.That(textBlock.Text).IsEqualTo("After");

        bm.Dispose();
    }

    [Test]
    public async Task ActivatePendingBindings_NoPendingBindings_NoOp()
    {
        var textBlock = new TextBlock { Text = "Original" };
        var vm = new DeferredViewModel();
        var bm = new BindingManager();

        // Should not throw or change anything
        textBlock.ActivatePendingBindings(bm, vm);

        await Assert.That(textBlock.Text).IsEqualTo("Original");
        bm.Dispose();
    }

    // ─── Part 4: DataContext callback triggers activation ───────

    [Test]
    public async Task DataContextChanged_NullToNonNull_ActivatesPendingBindings()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                   Text="{Binding Title}" />
            """;

        // Load without DataContext — bindings stored as pending
        var textBlock = TerminalXaml.Load<TextBlock>(xaml, dataContext: null, bindingManager: null);
        await Assert.That(textBlock.PendingBindings).IsNotNull();

        // Set DataContext — should trigger deferred binding activation
        var vm = new DeferredViewModel { Title = "Deferred!" };
        textBlock.DataContext = vm;

        await Assert.That(textBlock.Text).IsEqualTo("Deferred!");
        // Pending bindings should be cleared
        await Assert.That(textBlock.PendingBindings).IsNull();
    }

    [Test]
    public async Task DataContextChanged_NullToNonNull_BindingReactsToChanges()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                   Text="{Binding Title}" />
            """;

        var textBlock = TerminalXaml.Load<TextBlock>(xaml, dataContext: null, bindingManager: null);

        var vm = new DeferredViewModel { Title = "Initial" };
        textBlock.DataContext = vm;

        await Assert.That(textBlock.Text).IsEqualTo("Initial");

        // Change the ViewModel property — deferred binding should still react
        vm.Title = "Changed";
        await Assert.That(textBlock.Text).IsEqualTo("Changed");
    }

    [Test]
    public async Task DataContextChanged_NullToNull_DoesNothing()
    {
        var textBlock = new TextBlock { Text = "Original" };
        textBlock.AddPendingBinding(new ElementBinding("Text", "Title", BindingMode.OneWay, null, null));

        // Setting DataContext to null should NOT activate pending bindings
        textBlock.DataContext = null;

        await Assert.That(textBlock.Text).IsEqualTo("Original");
        await Assert.That(textBlock.PendingBindings).IsNotNull();
    }

    [Test]
    public async Task DataContextChanged_NoPendingBindings_DoesNotThrow()
    {
        var textBlock = new TextBlock { Text = "Original" };

        // No pending bindings — setting DataContext should be a no-op
        var vm = new DeferredViewModel();
        textBlock.DataContext = vm;

        await Assert.That(textBlock.Text).IsEqualTo("Original");
    }

    // ─── Part 5: SetDataContextRecursive triggers deferred bindings ──

    [Test]
    public async Task SetDataContextRecursive_ActivatesDeferredBindingsOnChildren()
    {
        var xaml = """
            <StackPanel xmlns="http://schemas.terminalninja.dev/xaml">
                <TextBlock Text="{Binding Title}" />
                <TextBlock Text="{Binding Count}" />
            </StackPanel>
            """;

        // Load without DataContext
        var panel = TerminalXaml.Load<StackPanel>(xaml, dataContext: null, bindingManager: null);

        var tb1 = (TextBlock)panel.Children[0];
        var tb2 = (TextBlock)panel.Children[1];

        await Assert.That(tb1.PendingBindings).IsNotNull();
        await Assert.That(tb2.PendingBindings).IsNotNull();

        // Now propagate DataContext recursively
        var vm = new DeferredViewModel { Title = "Hello", Count = 99 };
        var bm = new BindingManager();
        bm.SetDataContextRecursive(panel, vm);

        // Both children should have their bindings activated
        await Assert.That(tb1.Text).IsEqualTo("Hello");
        await Assert.That(tb2.Text).IsEqualTo("99");

        // Pending bindings should be cleared
        await Assert.That(tb1.PendingBindings).IsNull();
        await Assert.That(tb2.PendingBindings).IsNull();

        bm.Dispose();
    }

    // ─── Part 6: BindingExpression.Activate() observer leak fix ──

    [Test]
    public async Task BindingManager_SetDataContext_ReactivatesCleanly()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                   Text="{Binding Title}" />
            """;

        var vm1 = new DeferredViewModel { Title = "VM1" };
        var vm2 = new DeferredViewModel { Title = "VM2" };
        var bm = new BindingManager();

        var textBlock = TerminalXaml.Load<TextBlock>(xaml, vm1, bm);
        await Assert.That(textBlock.Text).IsEqualTo("VM1");

        // Switch DataContext — should cleanly reactivate (no observer leak)
        bm.SetDataContext(textBlock, vm2);
        await Assert.That(textBlock.Text).IsEqualTo("VM2");

        // Old VM should no longer affect the target
        vm1.Title = "Should not propagate";
        await Assert.That(textBlock.Text).IsEqualTo("VM2");

        // New VM should affect the target
        vm2.Title = "Updated VM2";
        await Assert.That(textBlock.Text).IsEqualTo("Updated VM2");

        bm.Dispose();
    }

    // ─── Part 7: End-to-end deferred binding with ItemsSource ───

    [Test]
    public async Task DeferredBinding_ItemsSource_BindsCollectionWhenDataContextSet()
    {
        var xaml = """
            <ItemsControl xmlns="http://schemas.terminalninja.dev/xaml"
                          ItemsSource="{Binding Items}" />
            """;

        // Load without DataContext
        var itemsControl = TerminalXaml.Load<ItemsControl>(xaml, dataContext: null, bindingManager: null);

        await Assert.That(itemsControl.PendingBindings).IsNotNull();
        await Assert.That((object?)itemsControl.ItemsSource).IsNull();

        // Set DataContext — should activate the ItemsSource binding
        var vm = new DeferredViewModel();
        itemsControl.DataContext = vm;

        await Assert.That((object?)itemsControl.ItemsSource).IsNotNull();
        await Assert.That(itemsControl.PendingBindings).IsNull();
    }

    // ─── Part 8: Nested structure (Border > StackPanel > TextBlock) ──

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

        var border = TerminalXaml.Load<Border>(xaml, dataContext: null, bindingManager: null);

        // The TextBlock is deeply nested
        var stackPanel = (StackPanel)border.Child!;
        var textBlock = (TextBlock)stackPanel.Children[0];

        await Assert.That(textBlock.PendingBindings).IsNotNull();

        // SetDataContextRecursive propagates through the tree
        var vm = new DeferredViewModel { Title = "Nested!" };
        var bm = new BindingManager();
        bm.SetDataContextRecursive(border, vm);

        await Assert.That(textBlock.Text).IsEqualTo("Nested!");
        await Assert.That(textBlock.PendingBindings).IsNull();

        bm.Dispose();
    }

    // ─── Part 9: Binding mode preservation ──────────────────────

    [Test]
    public async Task DeferredBinding_PreservesBindingMode()
    {
        var textBlock = new TextBlock();
        textBlock.AddPendingBinding(new ElementBinding("Text", "Title", BindingMode.TwoWay, null, null));

        await Assert.That(textBlock.PendingBindings![0].Mode).IsEqualTo(BindingMode.TwoWay);
    }

    [Test]
    public async Task ElementBinding_RecordEquality_Works()
    {
        var a = new ElementBinding("Text", "Title", BindingMode.OneWay, null, null);
        var b = new ElementBinding("Text", "Title", BindingMode.OneWay, null, null);
        var c = new ElementBinding("Text", "Count", BindingMode.OneWay, null, null);

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a).IsNotEqualTo(c);
    }

    // ─── Part 10: DataTemplate cloning of pending bindings ──────

    [Test]
    public async Task DataTemplate_CloneControl_ClonesAllPendingBindings()
    {
        // Arrange — prototype TextBlock with two pending bindings
        var prototype = new TextBlock();
        prototype.AddPendingBinding(new ElementBinding("Text", "Title", BindingMode.OneWay, null, null));
        prototype.AddPendingBinding(new ElementBinding("Foreground", "Color", BindingMode.OneWay, null, null));

        var template = new DataTemplate { TemplateContent = prototype };

        // Act — create a clone
        var clone = template.CreateContent();

        // Assert
        await Assert.That(clone).IsNotNull();
        var cloneFe = clone as FrameworkElement;
        await Assert.That(cloneFe).IsNotNull();
        await Assert.That(cloneFe!.PendingBindings).IsNotNull();
        await Assert.That(cloneFe.PendingBindings!.Count).IsEqualTo(2);
        await Assert.That(cloneFe.PendingBindings[0].TargetPropertyName).IsEqualTo("Text");
        await Assert.That(cloneFe.PendingBindings[0].Path).IsEqualTo("Title");
        await Assert.That(cloneFe.PendingBindings[1].TargetPropertyName).IsEqualTo("Foreground");
        await Assert.That(cloneFe.PendingBindings[1].Path).IsEqualTo("Color");
    }

    [Test]
    public async Task DataTemplate_CloneControl_CloneIsDistinctFromPrototype()
    {
        // Arrange
        var prototype = new TextBlock { Text = "Proto" };
        prototype.AddPendingBinding(new ElementBinding("Text", "Title", BindingMode.OneWay, null, null));

        var template = new DataTemplate { TemplateContent = prototype };

        // Act
        var clone = template.CreateContent();

        // Assert — clone is a different instance
        await Assert.That(clone).IsNotEqualTo(prototype);

        // Prototype's pending bindings are preserved (not consumed)
        await Assert.That(prototype.PendingBindings).IsNotNull();
        await Assert.That(prototype.PendingBindings!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task DataTemplate_CloneControl_MultipleClonesEachGetOwnBindings()
    {
        // Arrange
        var prototype = new TextBlock();
        prototype.AddPendingBinding(new ElementBinding("Text", "Title", BindingMode.OneWay, null, null));

        var template = new DataTemplate { TemplateContent = prototype };

        // Act — create two clones
        var clone1 = template.CreateContent() as FrameworkElement;
        var clone2 = template.CreateContent() as FrameworkElement;

        // Assert — each clone has its own pending bindings
        await Assert.That(clone1!.PendingBindings).IsNotNull();
        await Assert.That(clone2!.PendingBindings).IsNotNull();
        await Assert.That(clone1.PendingBindings!.Count).IsEqualTo(1);
        await Assert.That(clone2.PendingBindings!.Count).IsEqualTo(1);

        // They should be independent instances
        await Assert.That(clone1).IsNotEqualTo(clone2);
    }

    [Test]
    public async Task DataTemplate_ClonedBindings_ActivateWhenDataContextSet()
    {
        // Arrange — prototype with a deferred binding
        var prototype = new TextBlock();
        prototype.AddPendingBinding(new ElementBinding("Text", "Title", BindingMode.OneWay, null, null));

        var template = new DataTemplate { TemplateContent = prototype };
        var clone = template.CreateContent() as TextBlock;

        // Act — set DataContext (triggers OnDataContextChanged → activates pending bindings)
        var vm = new DeferredViewModel { Title = "Cloned!" };
        clone!.DataContext = vm;

        // Assert — binding should have activated and set the Text
        await Assert.That(clone.Text).IsEqualTo("Cloned!");
    }

    [Test]
    public async Task DataTemplate_ClonedBindings_ItemsControlIntegration()
    {
        // Arrange — DataTemplate with pending binding
        var prototype = new TextBlock();
        prototype.AddPendingBinding(new ElementBinding("Text", "Title", BindingMode.OneWay, null, null));

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
    public async Task DataTemplate_NoPendingBindings_CloneStillWorks()
    {
        // Arrange — prototype with no pending bindings
        var prototype = new TextBlock { Text = "Static" };
        var template = new DataTemplate { TemplateContent = prototype };

        // Act
        var clone = template.CreateContent() as TextBlock;

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone!.Text).IsEqualTo("Static");
        await Assert.That(clone.PendingBindings).IsNull();
    }

    // ─── Part 7: Plain POCO binding via reflection fallback ─────

    /// <summary>
    /// A plain data class with no base class, no INotifyPropertyChanged,
    /// and no source-generator discovery. Simulates real-world data objects
    /// like LogEntry that are used as DataTemplate binding sources.
    /// </summary>
    internal class PlainPocoItem
    {
        public string Message { get; set; } = string.Empty;
        public int Priority { get; set; }
    }

    [Test]
    public async Task PlainPoco_BindingActivates_WhenDataContextSet()
    {
        // Arrange — TextBlock with pending binding targeting a POCO property
        var textBlock = new TextBlock();
        textBlock.AddPendingBinding(new ElementBinding("Text", "Message", BindingMode.OneWay, null, null));

        // Act — set DataContext to a plain POCO (no INPC, no ViewModelBase)
        var item = new PlainPocoItem { Message = "Hello from POCO" };
        textBlock.DataContext = item;

        // Assert — binding should resolve via reflection fallback
        await Assert.That(textBlock.Text).IsEqualTo("Hello from POCO");
    }

    [Test]
    public async Task PlainPoco_ItemsControl_RendersAllItems()
    {
        // Arrange — DataTemplate with pending binding for POCO
        var prototype = new TextBlock();
        prototype.AddPendingBinding(new ElementBinding("Text", "Message", BindingMode.OneWay, null, null));
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
        // Arrange — DataTemplate with background color and pending binding
        var prototype = new TextBlock { Background = Color.FromHex("#141428") };
        prototype.AddPendingBinding(new ElementBinding("Text", "Message", BindingMode.OneWay, null, null));
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
        prototype.AddPendingBinding(new ElementBinding("Text", "Message", BindingMode.OneWay, null, null));
        var template = new DataTemplate { TemplateContent = prototype };

        // Act — clone, set DataContext, render
        var clone = template.CreateContent() as TextBlock;
        clone!.DataContext = new PlainPocoItem { Message = "Rendered" };

        var buffer = new CellBuffer(20, 1);
        clone.Render(buffer, new Rect(0, 0, 20, 1));

        // Assert — first character of "Rendered" should appear in the buffer
        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('R');
        await Assert.That(buffer.GetCell(1, 0).Character).IsEqualTo('e');
    }
}
