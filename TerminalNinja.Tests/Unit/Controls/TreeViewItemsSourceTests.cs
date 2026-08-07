using System.Collections.ObjectModel;
using System.Windows.Markup;
using TerminalNinja.Aot;
using TerminalNinja.Xaml.Binding;

namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for TreeView data binding: ItemsSource + ChildrenPath/HeaderPath materialization,
/// ItemTemplate headers rendered as visuals, expansion/selection preserved across rebuilds,
/// and SelectedValue ↔ SelectedItem sync.
/// </summary>
public class TreeViewItemsSourceTests
{
    [BindableObject]
    internal class Node
    {
        public string Name { get; set; } = "";
        public bool StartOpen { get; set; }
        public ObservableCollection<Node> Children { get; } = [];
    }

    private static (TreeView Tree, Node Root, Node Child) CreateBoundTree()
    {
        var child = new Node { Name = "Child" };
        var root = new Node { Name = "Root", Children = { child } };
        var tree = new TreeView
        {
            ChildrenPath = "Children",
            HeaderPath = "Name",
            ItemsSource = new ObservableCollection<Node> { root },
        };
        return (tree, root, child);
    }

    [Test]
    public async Task ItemsSource_MaterializesHierarchy()
    {
        var (tree, root, child) = CreateBoundTree();

        await Assert.That(tree.Items.Count).IsEqualTo(1);
        await Assert.That(tree.Items[0].Header).IsEqualTo("Root");
        await Assert.That(tree.Items[0].DataContext).IsEqualTo(root);
        await Assert.That(tree.Items[0].Items.Count).IsEqualTo(1);
        await Assert.That(tree.Items[0].Items[0].Header).IsEqualTo("Child");
        await Assert.That(tree.Items[0].Items[0].DataContext).IsEqualTo(child);
    }

    [Test]
    public async Task ItemsSource_WithoutHeaderPath_FallsBackToDataItem()
    {
        var tree = new TreeView { ItemsSource = new[] { "plain" } };

        await Assert.That(tree.Items.Count).IsEqualTo(1);
        await Assert.That(tree.Items[0].Header).IsEqualTo("plain");
    }

    [Test]
    public async Task RootCollectionChange_RebuildsTree()
    {
        var source = new ObservableCollection<Node> { new() { Name = "A" } };
        var tree = new TreeView { HeaderPath = "Name", ItemsSource = source };

        source.Add(new Node { Name = "B" });

        await Assert.That(tree.Items.Count).IsEqualTo(2);
        await Assert.That(tree.Items[1].Header).IsEqualTo("B");
    }

    [Test]
    public async Task RefreshItems_PreservesExpansionByDataItem()
    {
        var (tree, _, _) = CreateBoundTree();
        tree.Items[0].IsExpanded = true;

        tree.RefreshItems();

        await Assert.That(tree.Items[0].IsExpanded).IsTrue();
        // The nodes were rebuilt — state survived because it is keyed by data item.
    }

    [Test]
    public async Task IsExpandedPath_SetsInitialExpansion_ButPreservedStateWins()
    {
        var source = new ObservableCollection<Node>
        {
            new() { Name = "Open", StartOpen = true, Children = { new Node { Name = "C" } } },
            new() { Name = "Shut", Children = { new Node { Name = "C" } } },
        };
        var tree = new TreeView
        {
            HeaderPath = "Name",
            ChildrenPath = "Children",
            IsExpandedPath = "StartOpen",
            ItemsSource = source,
        };

        await Assert.That(tree.Items[0].IsExpanded).IsTrue();
        await Assert.That(tree.Items[1].IsExpanded).IsFalse();

        // The user collapses the open node; a rebuild must respect that over the path.
        tree.Items[0].IsExpanded = false;
        tree.RefreshItems();
        await Assert.That(tree.Items[0].IsExpanded).IsFalse();
    }

    [Test]
    public async Task RefreshItems_PreservesSelectionByDataItem()
    {
        var (tree, _, child) = CreateBoundTree();
        tree.SelectedValue = child;

        tree.RefreshItems();

        await Assert.That(tree.SelectedValue).IsEqualTo(child);
        await Assert.That(tree.SelectedItem).IsNotNull();
        await Assert.That(tree.SelectedItem!.DataContext).IsEqualTo(child);
    }

    [Test]
    public async Task SelectedItem_UpdatesSelectedValue()
    {
        var (tree, root, _) = CreateBoundTree();

        tree.SelectedItem = tree.Items[0];

        await Assert.That(tree.SelectedValue).IsEqualTo(root);
    }

    [Test]
    public async Task SelectedValue_SelectsMatchingNode()
    {
        var (tree, _, child) = CreateBoundTree();

        tree.SelectedValue = child;

        await Assert.That(tree.SelectedItem).IsEqualTo(tree.Items[0].Items[0]);
    }

    [Test]
    public async Task ItemTemplate_HeaderIsVisual_WithDataContextAndActiveBinding()
    {
        var (tree, _, _) = CreateBoundTree();
        tree.ItemTemplate = new DataTemplate
        {
            TemplateFactory = () =>
            {
                var tb = new TextBlock();
                BindingOperations.SetBinding(tb, TextBlock.TextProperty, new Binding("Name"));
                return tb;
            },
        };

        var header = tree.Items[0].Header;
        await Assert.That(header).IsTypeOf<TextBlock>();
        await Assert.That(((TextBlock)header!).Text).IsEqualTo("Root");
    }

    [Test]
    public async Task Render_UiElementHeader_DrawsIntoRow()
    {
        var (tree, _, _) = CreateBoundTree();
        tree.ItemTemplate = new DataTemplate
        {
            TemplateFactory = () =>
            {
                var tb = new TextBlock { Foreground = Color.Red };
                BindingOperations.SetBinding(tb, TextBlock.TextProperty, new Binding("Name"));
                return tb;
            },
        };

        using var buffer = new CellBuffer(20, 5);
        tree.Render(buffer, new Rect(0, 0, 20, 5));

        // Header starts at indent (0) + 2; the visual's own colour must survive.
        var cell = buffer.GetCell(2, 0);
        await Assert.That(cell.Codepoint).IsEqualTo((uint)'R');
        await Assert.That(cell.Foreground).IsEqualTo(Color.Red);
    }
}
