using System.Windows.Data;
using System.Windows.Markup;
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
    public async Task RelativeSource_Self_Mode_IsSelf()
    {
        var rs = new RelativeSource(RelativeSourceMode.Self);

        await Assert.That(rs.Mode).IsEqualTo(RelativeSourceMode.Self);
    }

    [Test]
    public async Task RelativeSource_TemplatedParent_Mode_IsTemplatedParent()
    {
        var rs = new RelativeSource(RelativeSourceMode.TemplatedParent);

        await Assert.That(rs.Mode).IsEqualTo(RelativeSourceMode.TemplatedParent);
    }

    [Test]
    public async Task RelativeSource_Constructor_SetsMode()
    {
        var rs = new RelativeSource(RelativeSourceMode.FindAncestor);

        await Assert.That(rs.Mode).IsEqualTo(RelativeSourceMode.FindAncestor);
        await Assert.That(rs.AncestorType).IsNull();
    }

    [Test]
    public async Task RelativeSource_DefaultConstructor_DefaultsToFindAncestor()
    {
        var rs = new RelativeSource();

        await Assert.That(rs.Mode).IsEqualTo(RelativeSourceMode.FindAncestor);
    }

    [Test]
    public async Task RelativeSource_ThreeArgConstructor_SetsAllProperties()
    {
        var rs = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Window), 3);

        await Assert.That(rs.Mode).IsEqualTo(RelativeSourceMode.FindAncestor);
        await Assert.That(rs.AncestorType).IsEqualTo(typeof(Window));
        await Assert.That(rs.AncestorLevel).IsEqualTo(3);
    }

    [Test]
    public async Task RelativeSource_FindAncestor_ThroughBindingSystem()
    {
        // Build a visual tree: Window > StackPanel > TextBlock
        var window = new Window { Title = "TestWindow" };
        var stackPanel = new StackPanel();
        var textBlock = new TextBlock();

        stackPanel.Children.Add(textBlock);
        window.Content = stackPanel;

        // Create a binding with RelativeSource FindAncestor targeting Window.Title
        var binding = new Binding("Title")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(Window)
            }
        };

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        await Assert.That(textBlock.Text).IsEqualTo("TestWindow");
    }

    // ─── Part 2: BindingOperations.SetBinding with RelativeSource ──

    [Test]
    public async Task SetBinding_RelativeSourceSelf_BindsTextToName()
    {
        var textBlock = new TextBlock { Name = "MyTextBlock" };

        var binding = new Binding("Name")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.Self)
        };

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        await Assert.That(textBlock.Text).IsEqualTo("MyTextBlock");
    }

    [Test]
    public async Task SetBinding_FindAncestor_BindsToWindowTitle()
    {
        var window = new Window { Title = "Ancestor Title" };
        var stackPanel = new StackPanel();
        var textBlock = new TextBlock();

        stackPanel.Children.Add(textBlock);
        window.Content = stackPanel;

        var binding = new Binding("Title")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(Window)
            }
        };

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        await Assert.That(textBlock.Text).IsEqualTo("Ancestor Title");
    }

    [Test]
    public async Task SetBinding_FindAncestor_ReactsToSourceChanges()
    {
        var window = new Window { Title = "Before" };
        var stackPanel = new StackPanel();
        var textBlock = new TextBlock();

        stackPanel.Children.Add(textBlock);
        window.Content = stackPanel;

        var binding = new Binding("Title")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(Window)
            }
        };

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        await Assert.That(textBlock.Text).IsEqualTo("Before");

        // Window.Title is a DependencyProperty that raises INPC
        window.Title = "After";

        await Assert.That(textBlock.Text).IsEqualTo("After");
    }

    [Test]
    public async Task SetBinding_DataContext_NoRelativeSource()
    {
        var vm = new RelativeSourceViewModel { Title = "DC Title" };
        var textBlock = new TextBlock();
        textBlock.DataContext = vm;

        var binding = new Binding("Title");

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        await Assert.That(textBlock.Text).IsEqualTo("DC Title");
    }

    [Test]
    public async Task SetBinding_FindAncestor_WithAncestorLevel()
    {
        // Nested StackPanels: outer > inner > textBlock
        var outer = new StackPanel { Name = "OuterPanel" };
        var inner = new StackPanel { Name = "InnerPanel" };
        var textBlock = new TextBlock();

        inner.Children.Add(textBlock);
        outer.Children.Add(inner);

        // AncestorLevel=1 should find inner StackPanel
        var binding1 = new Binding("Name")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(StackPanel),
                AncestorLevel = 1
            }
        };

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding1);
        await Assert.That(textBlock.Text).IsEqualTo("InnerPanel");

        // Clear and rebind with AncestorLevel=2 to find outer StackPanel
        BindingOperations.ClearBinding(textBlock, TextBlock.TextProperty);

        var binding2 = new Binding("Name")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(StackPanel),
                AncestorLevel = 2
            }
        };

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding2);
        await Assert.That(textBlock.Text).IsEqualTo("OuterPanel");
    }

    // ─── Part 3: XAML parsing of RelativeSource ──────────────────

    [Test]
    public async Task Xaml_RelativeSourceSelf_BindsOwnProperty()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                       Name="TestElement"
                       Text="{Binding Name, RelativeSource={RelativeSource Self}}" />
            """;

        var textBlock = TerminalXaml.Load<TextBlock>(xaml);

        await Assert.That(textBlock.Text).IsEqualTo("TestElement");
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

        var window = TerminalXaml.Load<Window>(xaml);

        var stackPanel = FindDescendant<StackPanel>(window);
        await Assert.That(stackPanel).IsNotNull();

        var textBlock = (TextBlock)stackPanel!.Children[0];
        await Assert.That(textBlock.Text).IsEqualTo("Window Title");
    }

    [Test]
    public async Task Xaml_RelativeSourceFindAncestor_WithAncestorLevel()
    {
        var xaml = """
            <StackPanel xmlns="http://schemas.terminalninja.dev/xaml"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        x:Name="OuterPanel">
                <StackPanel x:Name="InnerPanel">
                    <TextBlock Text="{Binding Name, RelativeSource={RelativeSource FindAncestor, AncestorType=StackPanel, AncestorLevel=2}}" />
                </StackPanel>
            </StackPanel>
            """;

        var outerPanel = TerminalXaml.Load<StackPanel>(xaml);

        var innerPanel = (StackPanel)outerPanel.Children[0];
        var textBlock = (TextBlock)innerPanel.Children[0];

        // AncestorLevel=2 should skip the inner StackPanel and bind to outer's Name
        await Assert.That(textBlock.Text).IsEqualTo("OuterPanel");
    }

    // ─── Part 4: RelativeSource with null DataContext ────────────

    [Test]
    public async Task Xaml_RelativeSourceSelf_WorksWithoutDataContext()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                       Name="DeferredElement"
                       Text="{Binding Name, RelativeSource={RelativeSource Self}}" />
            """;

        // Load without DataContext — RelativeSource bindings should still activate
        var textBlock = TerminalXaml.Load<TextBlock>(xaml);

        // RelativeSource Self doesn't depend on DataContext, so it should work
        await Assert.That(textBlock.Text).IsEqualTo("DeferredElement");
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

        var window = TerminalXaml.Load<Window>(xaml);

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
                    <TextBlock xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                               x:Name="rsLabel"
                               Text="{Binding Title, RelativeSource={RelativeSource FindAncestor, AncestorType=Window}}" />
                    <TextBlock xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                               x:Name="dcLabel"
                               Text="{Binding Title}" />
                </StackPanel>
            </Window>
            """;

        var vm = new RelativeSourceViewModel { Title = "VM Title" };
        var window = TerminalXaml.Load<Window>(xaml, vm);

        var stackPanel = FindDescendant<StackPanel>(window);
        await Assert.That(stackPanel).IsNotNull();

        var rsLabel = (TextBlock)stackPanel!.Children[0];
        var dcLabel = (TextBlock)stackPanel.Children[1];

        // RelativeSource binding should use Window.Title
        await Assert.That(rsLabel.Text).IsEqualTo("WindowTitle");

        // DataContext binding should use ViewModel.Title
        await Assert.That(dcLabel.Text).IsEqualTo("VM Title");
    }

    // ─── Part 5: Edge cases ──────────────────────────────────────

    [Test]
    public async Task FindAncestor_NoMatchingType_TextStaysDefault()
    {
        var stackPanel = new StackPanel();
        var textBlock = new TextBlock();
        stackPanel.Children.Add(textBlock);

        // Bind to Window.Title but there's no Window in the tree
        var binding = new Binding("Title")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(Window)
            }
        };

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        // No Window ancestor found — text should stay at default ("")
        await Assert.That(textBlock.Text).IsEqualTo("");
    }

    [Test]
    public async Task FindAncestor_ThroughBorder()
    {
        var window = new Window { Title = "BorderParent" };
        var border = new Border();
        var textBlock = new TextBlock();

        border.Child = textBlock;
        window.Content = border;

        var binding = new Binding("Title")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(Window)
            }
        };

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        await Assert.That(textBlock.Text).IsEqualTo("BorderParent");
    }

    [Test]
    public async Task FindAncestor_AncestorLevel2_SkipsFirstMatch()
    {
        // Nested StackPanels: outer > inner > textBlock
        var outer = new StackPanel { Name = "Outer" };
        var inner = new StackPanel { Name = "Inner" };
        var textBlock = new TextBlock();

        inner.Children.Add(textBlock);
        outer.Children.Add(inner);

        // AncestorLevel=2 should skip inner and return outer
        var binding = new Binding("Name")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(StackPanel),
                AncestorLevel = 2
            }
        };

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        await Assert.That(textBlock.Text).IsEqualTo("Outer");
    }

    [Test]
    public async Task FindAncestor_AncestorLevel_TooHigh_TextStaysDefault()
    {
        var stackPanel = new StackPanel();
        var textBlock = new TextBlock();
        stackPanel.Children.Add(textBlock);

        // Only one StackPanel in tree, but requesting level 2
        var binding = new Binding("Name")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(StackPanel),
                AncestorLevel = 2
            }
        };

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        // Ancestor not found — text should stay at default
        await Assert.That(textBlock.Text).IsEqualTo("");
    }

    [Test]
    public async Task FindAncestor_MatchesBaseClass()
    {
        // Panel is the base class of StackPanel — should match
        var stackPanel = new StackPanel { Name = "MyPanel" };
        var textBlock = new TextBlock();
        stackPanel.Children.Add(textBlock);

        var binding = new Binding("Name")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(Panel)
            }
        };

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);

        await Assert.That(textBlock.Text).IsEqualTo("MyPanel");
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
                {
                    return found;
                }

                queue.Enqueue(child);
            }
        }

        return null;
    }
}
