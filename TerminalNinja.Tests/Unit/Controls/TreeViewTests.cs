namespace TerminalNinja.Tests.Unit.Controls;

public class TreeViewTests
{
    #region Default Values

    [Test]
    public async Task Focusable_Default_IsTrue()
    {
        var tv = new TreeView();
        await Assert.That(tv.Focusable).IsTrue();
    }

    [Test]
    public async Task SelectedItem_Default_IsNull()
    {
        var tv = new TreeView();
        await Assert.That(tv.SelectedItem).IsNull();
    }

    [Test]
    public async Task Items_Default_IsEmpty()
    {
        var tv = new TreeView();
        await Assert.That(tv.Items.Count).IsEqualTo(0);
    }

    #endregion

    #region Expand/Collapse

    [Test]
    public async Task IsExpanded_Default_IsFalse()
    {
        var item = new TreeViewItem { Header = "Node" };
        await Assert.That(item.IsExpanded).IsFalse();
    }

    [Test]
    public async Task HasItems_WithChildren_IsTrue()
    {
        var item = new TreeViewItem { Header = "Parent" };
        item.Items.Add(new TreeViewItem { Header = "Child" });

        await Assert.That(item.HasItems).IsTrue();
    }

    [Test]
    public async Task HasItems_WithoutChildren_IsFalse()
    {
        var item = new TreeViewItem { Header = "Leaf" };
        await Assert.That(item.HasItems).IsFalse();
    }

    [Test]
    public async Task EnterKey_TogglesExpanded()
    {
        var tv = CreateSimpleTree();
        tv.SelectedItem = (TreeViewItem)tv.Items[0];

        tv.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(((TreeViewItem)tv.Items[0]).IsExpanded).IsTrue();

        tv.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(((TreeViewItem)tv.Items[0]).IsExpanded).IsFalse();
    }

    #endregion

    #region Keyboard Navigation

    [Test]
    public async Task DownArrow_SelectsNextVisibleNode()
    {
        var tv = CreateExpandedTree();
        tv.SelectedItem = (TreeViewItem)tv.Items[0];

        tv.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        // Next visible is Child 1 (since Root is expanded)
        await Assert.That(tv.SelectedItem).IsEqualTo(((TreeViewItem)tv.Items[0]).Items[0]);
    }

    [Test]
    public async Task UpArrow_SelectsPreviousVisibleNode()
    {
        var tv = CreateExpandedTree();
        var root = (TreeViewItem)tv.Items[0];
        tv.SelectedItem = root.Items[0]; // Child 1

        tv.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(tv.SelectedItem).IsEqualTo(root);
    }

    [Test]
    public async Task RightArrow_ExpandsCollapsedNode()
    {
        var tv = CreateSimpleTree();
        var root = (TreeViewItem)tv.Items[0];
        tv.SelectedItem = root;

        tv.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));

        await Assert.That(root.IsExpanded).IsTrue();
    }

    [Test]
    public async Task RightArrow_OnExpandedNode_MovesToFirstChild()
    {
        var tv = CreateExpandedTree();
        var root = (TreeViewItem)tv.Items[0];
        tv.SelectedItem = root;

        tv.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));

        await Assert.That(tv.SelectedItem).IsEqualTo(root.Items[0]);
    }

    [Test]
    public async Task RightArrow_OnLeaf_DoesNothing()
    {
        var tv = CreateExpandedTree();
        var leaf = ((TreeViewItem)tv.Items[0]).Items[0];
        tv.SelectedItem = leaf;

        tv.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));

        await Assert.That(tv.SelectedItem).IsEqualTo(leaf);
    }

    [Test]
    public async Task LeftArrow_CollapsesExpandedNode()
    {
        var tv = CreateExpandedTree();
        var root = (TreeViewItem)tv.Items[0];
        tv.SelectedItem = root;

        tv.OnKeyEvent(new KeyEvent(ConsoleKey.LeftArrow, '\0', false, false, false));

        await Assert.That(root.IsExpanded).IsFalse();
    }

    [Test]
    public async Task LeftArrow_OnCollapsedNode_MovesToParent()
    {
        var tv = CreateExpandedTree();
        var child = ((TreeViewItem)tv.Items[0]).Items[0];
        tv.SelectedItem = child;

        tv.OnKeyEvent(new KeyEvent(ConsoleKey.LeftArrow, '\0', false, false, false));

        await Assert.That(tv.SelectedItem).IsEqualTo(tv.Items[0]);
    }

    [Test]
    public async Task HomeKey_SelectsFirstNode()
    {
        var tv = CreateExpandedTree();
        tv.SelectedItem = ((TreeViewItem)tv.Items[0]).Items[1];

        tv.OnKeyEvent(new KeyEvent(ConsoleKey.Home, '\0', false, false, false));

        await Assert.That(tv.SelectedItem).IsEqualTo(tv.Items[0]);
    }

    [Test]
    public async Task EndKey_SelectsLastVisibleNode()
    {
        var tv = CreateSimpleTree();
        tv.SelectedItem = (TreeViewItem)tv.Items[0];

        tv.OnKeyEvent(new KeyEvent(ConsoleKey.End, '\0', false, false, false));

        await Assert.That(tv.SelectedItem).IsEqualTo(tv.Items[1]);
    }

    [Test]
    public async Task KeyboardNav_EmptyTree_DoesNotThrow()
    {
        var tv = new TreeView();
        tv.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));
        tv.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        await Assert.That(tv.SelectedItem).IsNull();
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_ExpandedNode_ShowsDownArrow()
    {
        var tv = CreateExpandedTree();

        using var buffer = new CellBuffer(30, 10);
        tv.Render(buffer, new Rect(0, 0, 30, 10));

        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo('\u25BC'); // ▼
    }

    [Test]
    public async Task Render_CollapsedNode_ShowsRightArrow()
    {
        var tv = CreateSimpleTree();

        using var buffer = new CellBuffer(30, 10);
        tv.Render(buffer, new Rect(0, 0, 30, 10));

        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo('\u25B6'); // ▶
    }

    [Test]
    public async Task Render_Indentation_TwoCharsPerLevel()
    {
        var tv = CreateExpandedTree();

        using var buffer = new CellBuffer(30, 10);
        tv.Render(buffer, new Rect(0, 0, 30, 10));

        // Root at indent 0, header at x=2
        await Assert.That(buffer.GetCell(2, 0).Codepoint).IsEqualTo('R'); // "Root"

        // Child at indent 2, header at x=4
        await Assert.That(buffer.GetCell(4, 1).Codepoint).IsEqualTo('C'); // "Child 1"
    }

    [Test]
    public async Task Render_SelectedNode_UsesSelectedBackground()
    {
        var tv = CreateExpandedTree();
        tv.SelectedItem = (TreeViewItem)tv.Items[0];
        tv.SelectedBackground = Color.Blue;

        using var buffer = new CellBuffer(30, 10);
        tv.Render(buffer, new Rect(0, 0, 30, 10));

        await Assert.That(buffer.GetCell(2, 0).Background).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task Render_EmptyTree_DoesNotThrow()
    {
        var tv = new TreeView();

        using var buffer = new CellBuffer(30, 10);
        tv.Render(buffer, new Rect(0, 0, 30, 10));

        await Assert.That(tv.Items.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Render_HeaderText_IsVisible()
    {
        var tv = new TreeView();
        tv.Items.Add(new TreeViewItem { Header = "Test" });

        using var buffer = new CellBuffer(30, 10);
        tv.Render(buffer, new Rect(0, 0, 30, 10));

        // No children = no indicator, text at x=2
        await Assert.That(buffer.GetCell(2, 0).Codepoint).IsEqualTo('T');
    }

    #endregion

    #region XAML

    [Test]
    public async Task Xaml_ParsesNestedTreeViewItems()
    {
        var xaml = """
            <TreeView xmlns="http://schemas.terminalninja.dev/xaml">
                <TreeViewItem Header="Root" IsExpanded="True">
                    <TreeViewItem Header="Child1" />
                    <TreeViewItem Header="Child2" />
                </TreeViewItem>
            </TreeView>
            """;

        var tv = TerminalXaml.Load<TreeView>(xaml);

        await Assert.That(tv.Items.Count).IsEqualTo(1);
        var root = tv.Items[0];
        await Assert.That(root.HeaderText).IsEqualTo("Root");
        await Assert.That(root.IsExpanded).IsTrue();
        await Assert.That(root.Items.Count).IsEqualTo(2);
    }

    #endregion

    #region Helpers

    private static TreeView CreateSimpleTree()
    {
        var tv = new TreeView();
        var root = new TreeViewItem { Header = "Root" };
        root.Items.Add(new TreeViewItem { Header = "Child 1" });
        root.Items.Add(new TreeViewItem { Header = "Child 2" });
        tv.Items.Add(root);
        tv.Items.Add(new TreeViewItem { Header = "Root 2" });
        return tv;
    }

    private static TreeView CreateExpandedTree()
    {
        var tv = CreateSimpleTree();
        ((TreeViewItem)tv.Items[0]).IsExpanded = true;
        return tv;
    }

    #endregion
}
