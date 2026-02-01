namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for Panel base class covering:
/// - Children collection behavior
/// - Parent auto-setting
/// - ObservableControlCollection operations (Add, Remove, Insert, Clear, indexer)
/// - OnChildAdded/OnChildRemoved callbacks
/// - InvalidateVisual triggering
/// - Background property
/// </summary>
public class PanelTests
{
    /// <summary>
    /// Concrete test implementation of Panel for testing abstract base class.
    /// </summary>
    private class TestPanel : Panel
    {
        public List<string> Events { get; } = new();

        public override void Render(CellBuffer buffer, Rect bounds)
        {
            // No-op for testing
        }

        public override Rect CalculateBounds(Rect parentBounds) => parentBounds;

        public override Size2D GetPreferredSize(Rect parentBounds) => new(parentBounds.Width, parentBounds.Height);

        internal override void OnChildAdded(IControl child)
        {
            base.OnChildAdded(child);
            Events.Add($"Added:{child.GetType().Name}");
        }

        internal override void OnChildRemoved(IControl child)
        {
            base.OnChildRemoved(child);
            Events.Add($"Removed:{child.GetType().Name}");
        }
    }

    #region Constructor and Properties

    [Test]
    public async Task Constructor_InitializesEmptyChildren()
    {
        // Arrange & Act
        var panel = new TestPanel();

        // Assert
        await Assert.That(panel.Children).IsNotNull();
        await Assert.That(panel.Children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Background_DefaultValue_IsNull()
    {
        // Arrange
        var panel = new TestPanel();

        // Assert
        await Assert.That(panel.Background).IsNull();
    }

    [Test]
    public async Task Background_SetValue_UpdatesBackground()
    {
        // Arrange
        var panel = new TestPanel();

        // Act
        panel.Background = Color.Red;

        // Assert
        await Assert.That(panel.Background).IsEqualTo(Color.Red);
    }

    #endregion

    #region Children Collection - Add

    [Test]
    public async Task Children_Add_AddsChild()
    {
        // Arrange
        var panel = new TestPanel();
        var child = new Rectangle();

        // Act
        panel.Children.Add(child);

        // Assert
        await Assert.That(panel.Children.Count).IsEqualTo(1);
        await Assert.That(panel.Children[0]).IsEqualTo(child);
    }

    [Test]
    public async Task Children_Add_SetsParent()
    {
        // Arrange
        var panel = new TestPanel();
        var child = new Rectangle();

        // Act
        panel.Children.Add(child);

        // Assert
        await Assert.That(child.Parent).IsEqualTo(panel);
    }

    [Test]
    public async Task Children_Add_TriggersOnChildAdded()
    {
        // Arrange
        var panel = new TestPanel();
        var child = new Rectangle();

        // Act
        panel.Children.Add(child);

        // Assert
        await Assert.That(panel.Events.Count).IsEqualTo(1);
        await Assert.That(panel.Events[0]).IsEqualTo("Added:Rectangle");
    }

    [Test]
    public async Task Children_AddNull_ThrowsArgumentNullException()
    {
        // Arrange
        var panel = new TestPanel();

        // Act & Assert
        await Assert.That(() => panel.Children.Add(null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task Children_AddMultiple_AddsAllChildren()
    {
        // Arrange
        var panel = new TestPanel();
        var child1 = new Rectangle();
        var child2 = new Label { Text = "Test" };
        var child3 = new Button();

        // Act
        panel.Children.Add(child1);
        panel.Children.Add(child2);
        panel.Children.Add(child3);

        // Assert
        await Assert.That(panel.Children.Count).IsEqualTo(3);
        await Assert.That(panel.Children[0]).IsEqualTo(child1);
        await Assert.That(panel.Children[1]).IsEqualTo(child2);
        await Assert.That(panel.Children[2]).IsEqualTo(child3);
    }

    #endregion

    #region Children Collection - Remove

    [Test]
    public async Task Children_Remove_RemovesChild()
    {
        // Arrange
        var panel = new TestPanel();
        var child = new Rectangle();
        panel.Children.Add(child);

        // Act
        var result = panel.Children.Remove(child);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(panel.Children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Children_Remove_ClearsParent()
    {
        // Arrange
        var panel = new TestPanel();
        var child = new Rectangle();
        panel.Children.Add(child);

        // Act
        panel.Children.Remove(child);

        // Assert
        await Assert.That(child.Parent).IsNull();
    }

    [Test]
    public async Task Children_Remove_TriggersOnChildRemoved()
    {
        // Arrange
        var panel = new TestPanel();
        var child = new Rectangle();
        panel.Children.Add(child);
        panel.Events.Clear(); // Clear the Add event

        // Act
        panel.Children.Remove(child);

        // Assert
        await Assert.That(panel.Events.Count).IsEqualTo(1);
        await Assert.That(panel.Events[0]).IsEqualTo("Removed:Rectangle");
    }

    [Test]
    public async Task Children_RemoveNonExistent_ReturnsFalse()
    {
        // Arrange
        var panel = new TestPanel();
        var child = new Rectangle();

        // Act
        var result = panel.Children.Remove(child);

        // Assert
        await Assert.That(result).IsFalse();
        await Assert.That(panel.Children.Count).IsEqualTo(0);
    }

    #endregion

    #region Children Collection - RemoveAt

    [Test]
    public async Task Children_RemoveAt_RemovesChildAtIndex()
    {
        // Arrange
        var panel = new TestPanel();
        var child1 = new Rectangle();
        var child2 = new Label { Text = "Test" };
        panel.Children.Add(child1);
        panel.Children.Add(child2);

        // Act
        panel.Children.RemoveAt(0);

        // Assert
        await Assert.That(panel.Children.Count).IsEqualTo(1);
        await Assert.That(panel.Children[0]).IsEqualTo(child2);
    }

    [Test]
    public async Task Children_RemoveAt_ClearsParent()
    {
        // Arrange
        var panel = new TestPanel();
        var child = new Rectangle();
        panel.Children.Add(child);

        // Act
        panel.Children.RemoveAt(0);

        // Assert
        await Assert.That(child.Parent).IsNull();
    }

    [Test]
    public async Task Children_RemoveAt_TriggersOnChildRemoved()
    {
        // Arrange
        var panel = new TestPanel();
        var child = new Rectangle();
        panel.Children.Add(child);
        panel.Events.Clear();

        // Act
        panel.Children.RemoveAt(0);

        // Assert
        await Assert.That(panel.Events.Count).IsEqualTo(1);
        await Assert.That(panel.Events[0]).IsEqualTo("Removed:Rectangle");
    }

    #endregion

    #region Children Collection - Insert

    [Test]
    public async Task Children_Insert_InsertsAtIndex()
    {
        // Arrange
        var panel = new TestPanel();
        var child1 = new Rectangle();
        var child2 = new Label { Text = "Test" };
        var child3 = new Button();
        panel.Children.Add(child1);
        panel.Children.Add(child3);

        // Act
        panel.Children.Insert(1, child2);

        // Assert
        await Assert.That(panel.Children.Count).IsEqualTo(3);
        await Assert.That(panel.Children[0]).IsEqualTo(child1);
        await Assert.That(panel.Children[1]).IsEqualTo(child2);
        await Assert.That(panel.Children[2]).IsEqualTo(child3);
    }

    [Test]
    public async Task Children_Insert_SetsParent()
    {
        // Arrange
        var panel = new TestPanel();
        var child = new Rectangle();

        // Act
        panel.Children.Insert(0, child);

        // Assert
        await Assert.That(child.Parent).IsEqualTo(panel);
    }

    [Test]
    public async Task Children_Insert_TriggersOnChildAdded()
    {
        // Arrange
        var panel = new TestPanel();
        var child = new Rectangle();

        // Act
        panel.Children.Insert(0, child);

        // Assert
        await Assert.That(panel.Events.Count).IsEqualTo(1);
        await Assert.That(panel.Events[0]).IsEqualTo("Added:Rectangle");
    }

    [Test]
    public async Task Children_InsertNull_ThrowsArgumentNullException()
    {
        // Arrange
        var panel = new TestPanel();

        // Act & Assert
        await Assert.That(() => panel.Children.Insert(0, null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    #endregion

    #region Children Collection - Clear

    [Test]
    public async Task Children_Clear_RemovesAllChildren()
    {
        // Arrange
        var panel = new TestPanel();
        var child1 = new Rectangle();
        var child2 = new Label { Text = "Test" };
        panel.Children.Add(child1);
        panel.Children.Add(child2);

        // Act
        panel.Children.Clear();

        // Assert
        await Assert.That(panel.Children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Children_Clear_ClearsAllParents()
    {
        // Arrange
        var panel = new TestPanel();
        var child1 = new Rectangle();
        var child2 = new Label { Text = "Test" };
        panel.Children.Add(child1);
        panel.Children.Add(child2);

        // Act
        panel.Children.Clear();

        // Assert
        await Assert.That(child1.Parent).IsNull();
        await Assert.That(child2.Parent).IsNull();
    }

    [Test]
    public async Task Children_Clear_TriggersOnChildRemovedForAll()
    {
        // Arrange
        var panel = new TestPanel();
        var child1 = new Rectangle();
        var child2 = new Label { Text = "Test" };
        panel.Children.Add(child1);
        panel.Children.Add(child2);
        panel.Events.Clear();

        // Act
        panel.Children.Clear();

        // Assert
        await Assert.That(panel.Events.Count).IsEqualTo(2);
        await Assert.That(panel.Events[0]).IsEqualTo("Removed:Rectangle");
        await Assert.That(panel.Events[1]).IsEqualTo("Removed:Label");
    }

    #endregion

    #region Children Collection - Indexer

    [Test]
    public async Task Children_IndexerGet_ReturnsChildAtIndex()
    {
        // Arrange
        var panel = new TestPanel();
        var child1 = new Rectangle();
        var child2 = new Label { Text = "Test" };
        panel.Children.Add(child1);
        panel.Children.Add(child2);

        // Act
        var result = panel.Children[1];

        // Assert
        await Assert.That(result).IsEqualTo(child2);
    }

    [Test]
    public async Task Children_IndexerSet_ReplacesChild()
    {
        // Arrange
        var panel = new TestPanel();
        var oldChild = new Rectangle();
        var newChild = new Label { Text = "Test" };
        panel.Children.Add(oldChild);

        // Act
        panel.Children[0] = newChild;

        // Assert
        await Assert.That(panel.Children.Count).IsEqualTo(1);
        await Assert.That(panel.Children[0]).IsEqualTo(newChild);
    }

    [Test]
    public async Task Children_IndexerSet_ClearsOldParentAndSetsNewParent()
    {
        // Arrange
        var panel = new TestPanel();
        var oldChild = new Rectangle();
        var newChild = new Label { Text = "Test" };
        panel.Children.Add(oldChild);

        // Act
        panel.Children[0] = newChild;

        // Assert
        await Assert.That(oldChild.Parent).IsNull();
        await Assert.That(newChild.Parent).IsEqualTo(panel);
    }

    [Test]
    public async Task Children_IndexerSet_TriggersCallbacks()
    {
        // Arrange
        var panel = new TestPanel();
        var oldChild = new Rectangle();
        var newChild = new Label { Text = "Test" };
        panel.Children.Add(oldChild);
        panel.Events.Clear();

        // Act
        panel.Children[0] = newChild;

        // Assert
        await Assert.That(panel.Events.Count).IsEqualTo(2);
        await Assert.That(panel.Events[0]).IsEqualTo("Removed:Rectangle");
        await Assert.That(panel.Events[1]).IsEqualTo("Added:Label");
    }

    #endregion

    #region Children Collection - Other Operations

    [Test]
    public async Task Children_Contains_ReturnsTrueForExistingChild()
    {
        // Arrange
        var panel = new TestPanel();
        var child = new Rectangle();
        panel.Children.Add(child);

        // Act
        var result = panel.Children.Contains(child);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Children_Contains_ReturnsFalseForNonExistentChild()
    {
        // Arrange
        var panel = new TestPanel();
        var child = new Rectangle();

        // Act
        var result = panel.Children.Contains(child);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Children_IndexOf_ReturnsCorrectIndex()
    {
        // Arrange
        var panel = new TestPanel();
        var child1 = new Rectangle();
        var child2 = new Label { Text = "Test" };
        panel.Children.Add(child1);
        panel.Children.Add(child2);

        // Act
        var index = panel.Children.IndexOf(child2);

        // Assert
        await Assert.That(index).IsEqualTo(1);
    }

    [Test]
    public async Task Children_IndexOf_ReturnsMinusOneForNonExistent()
    {
        // Arrange
        var panel = new TestPanel();
        var child = new Rectangle();

        // Act
        var index = panel.Children.IndexOf(child);

        // Assert
        await Assert.That(index).IsEqualTo(-1);
    }

    [Test]
    public async Task Children_CopyTo_CopiesElementsToArray()
    {
        // Arrange
        var panel = new TestPanel();
        var child1 = new Rectangle();
        var child2 = new Label { Text = "Test" };
        panel.Children.Add(child1);
        panel.Children.Add(child2);
        var array = new IControl[2];

        // Act
        panel.Children.CopyTo(array, 0);

        // Assert
        await Assert.That(array[0]).IsEqualTo(child1);
        await Assert.That(array[1]).IsEqualTo(child2);
    }

    [Test]
    public async Task Children_IsReadOnly_ReturnsFalse()
    {
        // Arrange
        var panel = new TestPanel();

        // Assert
        await Assert.That(panel.Children.IsReadOnly).IsFalse();
    }

    [Test]
    public async Task Children_GetEnumerator_IteratesAllChildren()
    {
        // Arrange
        var panel = new TestPanel();
        var child1 = new Rectangle();
        var child2 = new Label { Text = "Test" };
        panel.Children.Add(child1);
        panel.Children.Add(child2);

        // Act
        var list = new List<IControl>();
        foreach (var child in panel.Children)
        {
            list.Add(child);
        }

        // Assert
        await Assert.That(list.Count).IsEqualTo(2);
        await Assert.That(list[0]).IsEqualTo(child1);
        await Assert.That(list[1]).IsEqualTo(child2);
    }

    #endregion
}
