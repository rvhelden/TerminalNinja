namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for the ItemsPresenter control covering:
/// - Owner discovery via parent chain walk
/// - Delegated layout (GetChildrenWithBounds, GetLogicalChildren, GetPreferredSize)
/// - Delegated rendering to the owner's ItemsPanel
/// - Owner invalidation and re-discovery
/// - Behavior when no owner is found
/// </summary>
public class ItemsPresenterTests
{
    #region Owner Discovery

    [Test]
    public async Task Owner_WhenParentIsItemsControl_ReturnsItemsControl()
    {
        // Arrange
        var itemsControl = new ItemsControl();
        var presenter = new ItemsPresenter();
        presenter.Parent = itemsControl;

        // Act
        var owner = presenter.Owner;

        // Assert
        await Assert.That(owner).IsEqualTo(itemsControl);
    }

    [Test]
    public async Task Owner_WhenGrandparentIsItemsControl_ReturnsItemsControl()
    {
        // Arrange — ItemsControl > Border > ItemsPresenter
        var itemsControl = new ItemsControl();
        var border = new Border();
        border.Parent = itemsControl;
        var presenter = new ItemsPresenter();
        presenter.Parent = border;

        // Act
        var owner = presenter.Owner;

        // Assert
        await Assert.That(owner).IsEqualTo(itemsControl);
    }

    [Test]
    public async Task Owner_WhenNoItemsControlInChain_ReturnsNull()
    {
        // Arrange — StackPanel > Border > ItemsPresenter (no ItemsControl)
        var panel = new StackPanel();
        var border = new Border();
        border.Parent = panel;
        var presenter = new ItemsPresenter();
        presenter.Parent = border;

        // Act
        var owner = presenter.Owner;

        // Assert
        await Assert.That(owner).IsNull();
    }

    [Test]
    public async Task Owner_WhenNoParent_ReturnsNull()
    {
        // Arrange
        var presenter = new ItemsPresenter();

        // Act
        var owner = presenter.Owner;

        // Assert
        await Assert.That(owner).IsNull();
    }

    [Test]
    public async Task Owner_IsCachedAcrossAccesses()
    {
        // Arrange
        var itemsControl = new ItemsControl();
        var presenter = new ItemsPresenter();
        presenter.Parent = itemsControl;

        // Act
        var owner1 = presenter.Owner;
        var owner2 = presenter.Owner;

        // Assert — same reference (cached)
        await Assert.That(owner1).IsEqualTo(itemsControl);
        await Assert.That(owner2).IsEqualTo(itemsControl);
        await Assert.That(ReferenceEquals(owner1, owner2)).IsTrue();
    }

    #endregion

    #region Owner Invalidation

    [Test]
    public async Task InvalidateOwner_ClearsCache_ReDiscoversOnNextAccess()
    {
        // Arrange
        var itemsControl1 = new ItemsControl();
        var presenter = new ItemsPresenter();
        presenter.Parent = itemsControl1;

        // Access to cache
        var owner1 = presenter.Owner;
        await Assert.That(owner1).IsEqualTo(itemsControl1);

        // Re-parent
        var itemsControl2 = new ItemsControl();
        presenter.Parent = itemsControl2;
        presenter.InvalidateOwner();

        // Act
        var owner2 = presenter.Owner;

        // Assert
        await Assert.That(owner2).IsEqualTo(itemsControl2);
    }

    [Test]
    public async Task InvalidateOwner_WhenNoNewParent_ReturnsNull()
    {
        // Arrange
        var itemsControl = new ItemsControl();
        var presenter = new ItemsPresenter();
        presenter.Parent = itemsControl;

        var owner1 = presenter.Owner;
        await Assert.That(owner1).IsEqualTo(itemsControl);

        // Remove parent and invalidate
        presenter.Parent = null;
        presenter.InvalidateOwner();

        // Act
        var owner2 = presenter.Owner;

        // Assert
        await Assert.That(owner2).IsNull();
    }

    #endregion

    #region GetChildrenWithBounds

    [Test]
    public async Task GetChildrenWithBounds_WithOwner_YieldsItemsPanel()
    {
        // Arrange
        var itemsControl = new ItemsControl();
        var presenter = new ItemsPresenter();
        presenter.Parent = itemsControl;
        var bounds = new Rect(0, 0, 30, 10);

        // Act
        var children = presenter.GetChildrenWithBounds(bounds).ToList();

        // Assert
        await Assert.That(children.Count).IsEqualTo(1);
        await Assert.That(children[0].Child).IsEqualTo(itemsControl.ItemsPanel);
        await Assert.That(children[0].ChildParentBounds).IsEqualTo(bounds);
    }

    [Test]
    public async Task GetChildrenWithBounds_WithoutOwner_YieldsNothing()
    {
        // Arrange
        var presenter = new ItemsPresenter();
        var bounds = new Rect(0, 0, 30, 10);

        // Act
        var children = presenter.GetChildrenWithBounds(bounds).ToList();

        // Assert
        await Assert.That(children.Count).IsEqualTo(0);
    }

    #endregion

    #region GetLogicalChildren

    [Test]
    public async Task GetLogicalChildren_WithOwner_YieldsItemsPanel()
    {
        // Arrange
        var itemsControl = new ItemsControl();
        var presenter = new ItemsPresenter();
        presenter.Parent = itemsControl;

        // Act
        var logicalChildren = presenter.GetLogicalChildren().ToList();

        // Assert
        await Assert.That(logicalChildren.Count).IsEqualTo(1);
        await Assert.That(logicalChildren[0]).IsEqualTo(itemsControl.ItemsPanel);
    }

    [Test]
    public async Task GetLogicalChildren_WithoutOwner_YieldsNothing()
    {
        // Arrange
        var presenter = new ItemsPresenter();

        // Act
        var logicalChildren = presenter.GetLogicalChildren().ToList();

        // Assert
        await Assert.That(logicalChildren.Count).IsEqualTo(0);
    }

    #endregion

    #region GetPreferredSize

    [Test]
    public async Task GetPreferredSize_WithOwner_DelegatesToItemsPanel()
    {
        // Arrange
        var itemsControl = new ItemsControl();
        itemsControl.ItemsSource = new[] { "A", "B", "C" };
        var presenter = new ItemsPresenter();
        presenter.Parent = itemsControl;
        var parentRect = new Rect(0, 0, 40, 20);

        // Act
        var size = presenter.GetPreferredSize(parentRect);

        // Assert — should match what the ItemsPanel would return
        var panelSize = itemsControl.ItemsPanel.GetPreferredSize(parentRect);
        await Assert.That(size.Width).IsEqualTo(panelSize.Width);
        await Assert.That(size.Height).IsEqualTo(panelSize.Height);
    }

    [Test]
    public async Task GetPreferredSize_WithoutOwner_ReturnsZero()
    {
        // Arrange
        var presenter = new ItemsPresenter();
        var parentRect = new Rect(0, 0, 40, 20);

        // Act
        var size = presenter.GetPreferredSize(parentRect);

        // Assert
        await Assert.That(size.Width).IsEqualTo(0);
        await Assert.That(size.Height).IsEqualTo(0);
    }

    #endregion

    #region CalculateBounds

    [Test]
    public async Task CalculateBounds_ReturnsParentBounds()
    {
        // Arrange
        var presenter = new ItemsPresenter();
        var parent = new Rect(5, 10, 30, 20);

        // Act
        var bounds = presenter.CalculateBounds(parent);

        // Assert
        await Assert.That(bounds).IsEqualTo(parent);
    }

    #endregion

    #region Render

    [Test]
    public async Task Render_WithOwnerAndItems_DelegatesToItemsPanel()
    {
        // Arrange
        var itemsControl = new ItemsControl();
        itemsControl.ItemsSource = new[] { "Hello" };
        var presenter = new ItemsPresenter();
        presenter.Parent = itemsControl;

        using var buffer = new CellBuffer(20, 5);

        // Act
        presenter.Render(buffer, new Rect(0, 0, 20, 5));

        // Assert — the text "Hello" should be rendered via the panel
        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo('H');
        await Assert.That(buffer.GetCell(4, 0).Codepoint).IsEqualTo('o');
    }

    [Test]
    public async Task Render_WithoutOwner_DoesNotThrow()
    {
        // Arrange
        var presenter = new ItemsPresenter();
        using var buffer = new CellBuffer(20, 5);

        // Act & Assert — should not throw
        presenter.Render(buffer, new Rect(0, 0, 20, 5));

        // Buffer should remain at default (space characters)
        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo(' ');
    }

    [Test]
    public async Task Render_WithMultipleItems_RendersAll()
    {
        // Arrange
        var itemsControl = new ItemsControl();
        itemsControl.ItemsSource = new[] { "AB", "CD" };
        var presenter = new ItemsPresenter();
        presenter.Parent = itemsControl;

        using var buffer = new CellBuffer(20, 5);

        // Act
        presenter.Render(buffer, new Rect(0, 0, 20, 5));

        // Assert — "AB" on row 0, "CD" on row 1 (vertical StackPanel default)
        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsEqualTo('A');
        await Assert.That(buffer.GetCell(1, 0).Codepoint).IsEqualTo('B');
        await Assert.That(buffer.GetCell(0, 1).Codepoint).IsEqualTo('C');
        await Assert.That(buffer.GetCell(1, 1).Codepoint).IsEqualTo('D');
    }

    #endregion

    #region FrameworkElement inheritance

    [Test]
    public async Task ItemsPresenter_IsFrameworkElement()
    {
        // Arrange & Act
        var presenter = new ItemsPresenter();

        // Assert
        await Assert.That(presenter is FrameworkElement).IsTrue();
    }

    [Test]
    public async Task ItemsPresenter_IsNotControl()
    {
        // Arrange & Act
        var presenter = new ItemsPresenter();

        // Assert — ItemsPresenter inherits FrameworkElement, NOT Control
        // Use GetType() to verify the inheritance chain doesn't include Control
        await Assert.That(typeof(Control).IsAssignableFrom(presenter.GetType())).IsFalse();
    }

    #endregion
}
