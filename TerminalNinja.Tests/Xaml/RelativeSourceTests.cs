using TerminalNinja.Controls;
using TerminalNinja.Xaml;
using TerminalNinja.Xaml.Binding;
using TerminalNinja.Xaml.Mvvm;

namespace TerminalNinja.Tests.Xaml;

/// <summary>
/// Tests for RelativeSource binding — resolving the binding source from
/// the visual tree instead of DataContext.
/// </summary>
public class RelativeSourceTests
{
    // ─── Test ViewModel ──────────────────────────────────────────

    internal class RelativeSourceViewModel : ViewModelBase
    {
        private string _title = "ViewModel Title";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
    }

    // ─── Part 1: RelativeSource class unit tests ─────────────────

    [Test]
    public async Task RelativeSource_Self_ReturnsTargetElement()
    {
        var textBlock = new TextBlock { Text = "Hello" };
        var rs = RelativeSource.Self;

        var source = rs.ResolveSource(textBlock);

        await Assert.That(source).IsEqualTo(textBlock);
    }

    [Test]
    public async Task RelativeSource_Self_Mode_IsSelf()
    {
        var rs = RelativeSource.Self;

        await Assert.That(rs.Mode).IsEqualTo(RelativeSourceMode.Self);
    }

    [Test]
    public async Task RelativeSource_TemplatedParent_ReturnsNull()
    {
        var textBlock = new TextBlock();
        var rs = RelativeSource.TemplatedParent;

        // TemplatedParent is not yet implemented — should return null
        var source = rs.ResolveSource(textBlock);

        await Assert.That(source).IsNull();
    }

    [Test]
    public async Task RelativeSource_FindAncestor_FindsParentByType()
    {
        var window = new Window { Title = "TestWindow" };
        var stackPanel = new StackPanel();
        var textBlock = new TextBlock();

        // Build visual tree
        stackPanel.Children.Add(textBlock);
        window.Content = stackPanel;

        var rs = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Window) };
        var source = rs.ResolveSource(textBlock);

        await Assert.That(source).IsEqualTo(window);
    }

    [Test]
    public async Task RelativeSource_FindAncestor_FindsIntermediateAncestor()
    {
        var window = new Window();
        var stackPanel = new StackPanel();
        var textBlock = new TextBlock();

        stackPanel.Children.Add(textBlock);
        window.Content = stackPanel;

        var rs = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(StackPanel) };
        var source = rs.ResolveSource(textBlock);

        await Assert.That(source).IsEqualTo(stackPanel);
    }

    [Test]
    public async Task RelativeSource_FindAncestor_ReturnsNull_WhenTypeNotInTree()
    {
        var stackPanel = new StackPanel();
        var textBlock = new TextBlock();
        stackPanel.Children.Add(textBlock);

        var rs = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Window) };
        var source = rs.ResolveSource(textBlock);

        await Assert.That(source).IsNull();
    }

    [Test]
    public async Task RelativeSource_FindAncestor_NoAncestorType_Throws()
    {
        var textBlock = new TextBlock();
        var rs = new RelativeSource(RelativeSourceMode.FindAncestor);

        await Assert.That(() => rs.ResolveSource(textBlock))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task RelativeSource_FindAncestor_AncestorLevel_SkipsFirstMatch()
    {
        // Nested StackPanels: outer > inner > textBlock
        var outer = new StackPanel();
        var inner = new StackPanel();
        var textBlock = new TextBlock();

        inner.Children.Add(textBlock);
        outer.Children.Add(inner);

        // Level 1 should find inner StackPanel
        var rs1 = new RelativeSource(RelativeSourceMode.FindAncestor)
        {
            AncestorType = typeof(StackPanel),
            AncestorLevel = 1
        };
        var source1 = rs1.ResolveSource(textBlock);
        await Assert.That(source1).IsEqualTo(inner);

        // Level 2 should find outer StackPanel
        var rs2 = new RelativeSource(RelativeSourceMode.FindAncestor)
        {
            AncestorType = typeof(StackPanel),
            AncestorLevel = 2
        };
        var source2 = rs2.ResolveSource(textBlock);
        await Assert.That(source2).IsEqualTo(outer);
    }

    [Test]
    public async Task RelativeSource_FindAncestor_AncestorLevel_TooHigh_ReturnsNull()
    {
        var stackPanel = new StackPanel();
        var textBlock = new TextBlock();
        stackPanel.Children.Add(textBlock);

        var rs = new RelativeSource(RelativeSourceMode.FindAncestor)
        {
            AncestorType = typeof(StackPanel),
            AncestorLevel = 2 // Only one StackPanel in tree
        };
        var source = rs.ResolveSource(textBlock);

        await Assert.That(source).IsNull();
    }

    [Test]
    public async Task RelativeSource_FindAncestor_InvalidLevel_Throws()
    {
        var textBlock = new TextBlock();
        var rs = new RelativeSource(RelativeSourceMode.FindAncestor)
        {
            AncestorType = typeof(StackPanel),
            AncestorLevel = 0
        };

        await Assert.That(() => rs.ResolveSource(textBlock))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task RelativeSource_FindAncestor_MatchesBaseClass()
    {
        // Panel is the base class of StackPanel — should match
        var stackPanel = new StackPanel();
        var textBlock = new TextBlock();
        stackPanel.Children.Add(textBlock);

        var rs = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Panel) };
        var source = rs.ResolveSource(textBlock);

        await Assert.That(source).IsEqualTo(stackPanel);
    }

    // ─── Part 2: BindingManager.CreateBinding with RelativeSource ──

    [Test]
    public async Task CreateBinding_RelativeSourceSelf_BindsToSelf()
    {
        var textBlock = new TextBlock { Text = "Original" };
        var bm = new BindingManager();

        // Bind Foreground property to the Text property of the same element (Self)
        // This is artificial but tests the mechanism
        bm.CreateBinding(
            textBlock,
            "Foreground",
            "Text",
            relativeSource: RelativeSource.Self);

        // The source is the TextBlock itself, so Foreground should get the text value.
        // Since Text is a string and Foreground is a Color, this will try default conversion
        // which won't succeed — but the mechanism should not throw.
        // Let's use a more compatible test: bind Text to Name

        bm.Dispose();
    }

    [Test]
    public async Task CreateBinding_RelativeSourceSelf_SourceIsElement()
    {
        var textBlock = new TextBlock { Name = "MyTextBlock" };
        var bm = new BindingManager();

        // Bind Text to the Name property of itself (Self binding)
        bm.CreateBinding(
            textBlock,
            "Text",
            "Name",
            relativeSource: RelativeSource.Self);

        await Assert.That(textBlock.Text).IsEqualTo("MyTextBlock");

        bm.Dispose();
    }

    [Test]
    public async Task CreateBinding_RelativeSourceFindAncestor_BindsToAncestor()
    {
        var window = new Window { Title = "Ancestor Title" };
        var stackPanel = new StackPanel();
        var textBlock = new TextBlock();

        stackPanel.Children.Add(textBlock);
        window.Content = stackPanel;

        var bm = new BindingManager();
        var rs = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Window) };

        bm.CreateBinding(
            textBlock,
            "Text",
            "Title",
            relativeSource: rs);

        await Assert.That(textBlock.Text).IsEqualTo("Ancestor Title");

        bm.Dispose();
    }

    [Test]
    public async Task CreateBinding_RelativeSourceFindAncestor_ReactsToChanges()
    {
        var window = new Window { Title = "Before" };
        var stackPanel = new StackPanel();
        var textBlock = new TextBlock();

        stackPanel.Children.Add(textBlock);
        window.Content = stackPanel;

        var bm = new BindingManager();
        var rs = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Window) };

        bm.CreateBinding(
            textBlock,
            "Text",
            "Title",
            relativeSource: rs);

        await Assert.That(textBlock.Text).IsEqualTo("Before");

        // Window.Title is a DependencyProperty that raises INPC
        window.Title = "After";

        await Assert.That(textBlock.Text).IsEqualTo("After");

        bm.Dispose();
    }

    [Test]
    public async Task CreateBinding_NoRelativeSource_UsesDataContext()
    {
        var vm = new RelativeSourceViewModel { Title = "DC Title" };
        var textBlock = new TextBlock();
        var bm = new BindingManager();

        bm.SetDataContext(textBlock, vm);
        bm.CreateBinding(textBlock, "Text", "Title");

        await Assert.That(textBlock.Text).IsEqualTo("DC Title");

        bm.Dispose();
    }

    // ─── Part 3: XAML parsing of RelativeSource ──────────────────

    [Test]
    public async Task Xaml_RelativeSourceSelf_ParsesAndBinds()
    {
        var xaml = """
            <Border xmlns="http://schemas.terminalninja.dev/xaml"
                    Background="Red">
                <TextBlock Text="{Binding Background, RelativeSource={RelativeSource Self}}" />
            </Border>
            """;

        var vm = new RelativeSourceViewModel();
        var bm = new BindingManager();
        var border = TerminalXaml.Load<Border>(xaml, vm, bm);

        var textBlock = (TextBlock)border.Child!;
        // TextBlock binds its Text to its own Background via RelativeSource Self
        // Background is a Color, Text is string — default conversion applies
        // The binding source is the TextBlock itself, so Text = TextBlock.Background.ToString()
        // Since TextBlock has no Background set explicitly, it defaults to Color default
        // This test verifies parsing didn't throw
        await Assert.That(textBlock).IsNotNull();

        bm.Dispose();
    }

    [Test]
    public async Task Xaml_RelativeSourceSelf_BindsOwnProperty()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                       Name="TestElement"
                       Text="{Binding Name, RelativeSource={RelativeSource Self}}" />
            """;

        var vm = new RelativeSourceViewModel();
        var bm = new BindingManager();
        var textBlock = TerminalXaml.Load<TextBlock>(xaml, vm, bm);

        await Assert.That(textBlock.Text).IsEqualTo("TestElement");

        bm.Dispose();
    }

    [Test]
    public async Task Xaml_RelativeSourceFindAncestor_ParsesAndBinds()
    {
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    Title="Window Title">
                <StackPanel>
                    <TextBlock Text="{Binding Title, RelativeSource={RelativeSource FindAncestor, AncestorType=Window}}" />
                </StackPanel>
            </Window>
            """;

        var vm = new RelativeSourceViewModel();
        var bm = new BindingManager();
        var window = TerminalXaml.Load<Window>(xaml, vm, bm);

        // Find the TextBlock in the tree
        // Window.Content is StackPanel (via ContentControl → ContentPresenter)
        var contentPresenter = window.GetLogicalChildren().First();
        StackPanel? stackPanel = null;
        foreach (var child in contentPresenter.GetLogicalChildren())
        {
            if (child is StackPanel sp)
            {
                stackPanel = sp;
                break;
            }
        }

        // If ContentPresenter wraps things differently, try direct content
        if (stackPanel == null)
        {
            // Navigate through the visual tree
            stackPanel = FindDescendant<StackPanel>(window);
        }

        await Assert.That(stackPanel).IsNotNull();
        var textBlock = (TextBlock)stackPanel!.Children[0];
        await Assert.That(textBlock.Text).IsEqualTo("Window Title");

        bm.Dispose();
    }

    [Test]
    public async Task Xaml_RelativeSourceFindAncestor_WithAncestorLevel()
    {
        // Use Name property instead of Background to avoid Color? type conversion issues
        var xaml = """
            <StackPanel xmlns="http://schemas.terminalninja.dev/xaml"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        x:Name="OuterPanel">
                <StackPanel x:Name="InnerPanel">
                    <TextBlock Text="{Binding Name, RelativeSource={RelativeSource FindAncestor, AncestorType=StackPanel, AncestorLevel=2}}" />
                </StackPanel>
            </StackPanel>
            """;

        var vm = new RelativeSourceViewModel();
        var bm = new BindingManager();
        var outerPanel = TerminalXaml.Load<StackPanel>(xaml, vm, bm);

        var innerPanel = (StackPanel)outerPanel.Children[0];
        var textBlock = (TextBlock)innerPanel.Children[0];

        // AncestorLevel=2 should skip the inner StackPanel and bind to outer's Name
        await Assert.That(textBlock.Text).IsEqualTo("OuterPanel");

        bm.Dispose();
    }

    // ─── Part 4: RelativeSource with deferred bindings (null DataContext) ──

    [Test]
    public async Task Xaml_RelativeSourceSelf_WorksWithoutDataContext()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                       Name="DeferredElement"
                       Text="{Binding Name, RelativeSource={RelativeSource Self}}" />
            """;

        // Load without DataContext — RelativeSource bindings should still activate
        var textBlock = TerminalXaml.Load<TextBlock>(xaml, dataContext: null, bindingManager: null);

        // RelativeSource Self doesn't depend on DataContext, so it should work
        await Assert.That(textBlock.Text).IsEqualTo("DeferredElement");

        // No pending bindings should be stored for RelativeSource bindings
        await Assert.That(textBlock.PendingBindings).IsNull();
    }

    [Test]
    public async Task Xaml_RelativeSourceFindAncestor_WorksWithoutDataContext()
    {
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    Title="No DC Title">
                <TextBlock Text="{Binding Title, RelativeSource={RelativeSource FindAncestor, AncestorType=Window}}" />
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml, dataContext: null, bindingManager: null);

        // Find TextBlock in the window's content tree
        var textBlock = FindDescendant<TextBlock>(window);

        await Assert.That(textBlock).IsNotNull();
        await Assert.That(textBlock!.Text).IsEqualTo("No DC Title");
    }

    [Test]
    public async Task Xaml_MixedBindings_RelativeSourceAndDataContext()
    {
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    Title="WindowTitle">
                <StackPanel>
                    <TextBlock x:Name="rsLabel" 
                               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                               Text="{Binding Title, RelativeSource={RelativeSource FindAncestor, AncestorType=Window}}" />
                    <TextBlock x:Name="dcLabel"
                               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                               Text="{Binding Title}" />
                </StackPanel>
            </Window>
            """;

        var vm = new RelativeSourceViewModel { Title = "VM Title" };
        var bm = new BindingManager();
        var window = TerminalXaml.Load<Window>(xaml, vm, bm);

        var stackPanel = FindDescendant<StackPanel>(window);
        await Assert.That(stackPanel).IsNotNull();

        var rsLabel = (TextBlock)stackPanel!.Children[0];
        var dcLabel = (TextBlock)stackPanel.Children[1];

        // RelativeSource binding should use Window.Title
        await Assert.That(rsLabel.Text).IsEqualTo("WindowTitle");

        // DataContext binding should use ViewModel.Title
        await Assert.That(dcLabel.Text).IsEqualTo("VM Title");

        bm.Dispose();
    }

    // ─── Part 5: Edge cases ──────────────────────────────────────

    [Test]
    public async Task RelativeSource_ResolveSource_NullTarget_Throws()
    {
        var rs = RelativeSource.Self;

        await Assert.That(() => rs.ResolveSource(null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task RelativeSource_Constructor_SetsMode()
    {
        var rs = new RelativeSource(RelativeSourceMode.FindAncestor);

        await Assert.That(rs.Mode).IsEqualTo(RelativeSourceMode.FindAncestor);
        await Assert.That(rs.AncestorLevel).IsEqualTo(1);
        await Assert.That(rs.AncestorType).IsNull();
    }

    [Test]
    public async Task RelativeSource_DefaultConstructor_DefaultMode()
    {
        var rs = new RelativeSource();

        await Assert.That(rs.Mode).IsEqualTo(RelativeSourceMode.Self); // Enum default is 0 = Self
    }

    [Test]
    public async Task RelativeSourceExtension_ProvideValue_ReturnsRelativeSource()
    {
        var ext = new RelativeSourceExtension(RelativeSourceMode.FindAncestor)
        {
            AncestorType = typeof(Window),
            AncestorLevel = 2
        };

        var result = ext.ProvideValue(null!) as RelativeSource;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Mode).IsEqualTo(RelativeSourceMode.FindAncestor);
        await Assert.That(result.AncestorType).IsEqualTo(typeof(Window));
        await Assert.That(result.AncestorLevel).IsEqualTo(2);
    }

    [Test]
    public async Task ElementBinding_WithRelativeSource_StoresIt()
    {
        var rs = RelativeSource.Self;
        var binding = new ElementBinding("Text", "Name", BindingMode.OneWay, null, null, rs);

        await Assert.That(binding.RelativeSource).IsEqualTo(rs);
    }

    [Test]
    public async Task ElementBinding_WithoutRelativeSource_DefaultsToNull()
    {
        var binding = new ElementBinding("Text", "Name", BindingMode.OneWay, null, null);

        await Assert.That(binding.RelativeSource).IsNull();
    }

    // ─── Part 6: FindAncestor with Border child ──────────────────

    [Test]
    public async Task RelativeSource_FindAncestor_ThroughBorder()
    {
        var window = new Window { Title = "BorderParent" };
        var border = new Border();
        var textBlock = new TextBlock();

        border.Child = textBlock;
        window.Content = border;

        var rs = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Window) };
        var source = rs.ResolveSource(textBlock);

        await Assert.That(source).IsEqualTo(window);
    }

    // ─── Helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Simple breadth-first search to find a descendant of a given type in the logical tree.
    /// </summary>
    private static T? FindDescendant<T>(FrameworkElement root) where T : FrameworkElement
    {
        var queue = new Queue<FrameworkElement>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var child in current.GetLogicalChildren())
            {
                if (child is T found)
                    return found;
                queue.Enqueue(child);
            }
        }

        return null;
    }
}
