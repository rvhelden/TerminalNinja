using System.Collections.ObjectModel;

namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for the Selector abstract base class covering:
/// - SelectedIndex / SelectedItem synchronization
/// - SelectionChanged event
/// - SelectionMode property
/// - Selection preservation after RefreshItems
/// - NotifyItemClicked / NotifyContainerClicked
/// - Edge cases (out-of-range index, null item, empty collection)
/// </summary>
public class SelectorTests
{
    #region Test Helpers

    /// <summary>
    /// Concrete Selector subclass for testing the abstract base.
    /// Uses ListBox container generation behavior.
    /// </summary>
    private class TestSelector : Selector
    {
        public override Size2D GetPreferredSize(Rect parent) => new(parent.Width, parent.Height);
        public override Rect CalculateBounds(Rect parent) => parent;
        public override void Render(CellBuffer buffer, Rect parentBounds)
        {
            ItemsPanel.Render(buffer, parentBounds);
        }

        protected override bool IsItemItsOwnContainer(object item) => item is ListBoxItem;

        protected override UIElement CreateContainerForItem(object item)
        {
            var lbi = new ListBoxItem
            {
                Content = new TextBlock { Text = item?.ToString() ?? string.Empty }
            };
            return lbi;
        }

        protected override void PrepareContainerForItem(UIElement container, object item)
        {
            base.PrepareContainerForItem(container, item);
            if (container is ListBoxItem lbi)
            {
                lbi.IsSelected = SelectedItem == item;
            }
        }
    }

    #endregion

    #region Default State

    [Test]
    public async Task SelectedIndex_Default_IsMinusOne()
    {
        var selector = new TestSelector();
        await Assert.That(selector.SelectedIndex).IsEqualTo(-1);
    }

    [Test]
    public async Task SelectedItem_Default_IsNull()
    {
        var selector = new TestSelector();
        await Assert.That(selector.SelectedItem).IsNull();
    }

    [Test]
    public async Task SelectionMode_Default_IsSingle()
    {
        var selector = new TestSelector();
        await Assert.That(selector.SelectionMode).IsEqualTo(SelectionMode.Single);
    }

    #endregion

    #region SelectedIndex ↔ SelectedItem Sync

    [Test]
    public async Task SetSelectedIndex_UpdatesSelectedItem()
    {
        var selector = new TestSelector();
        var items = new ObservableCollection<string> { "Alpha", "Beta", "Gamma" };
        selector.ItemsSource = items;

        selector.SelectedIndex = 1;

        await Assert.That(selector.SelectedItem).IsEqualTo("Beta");
    }

    [Test]
    public async Task SetSelectedItem_UpdatesSelectedIndex()
    {
        var selector = new TestSelector();
        var items = new ObservableCollection<string> { "Alpha", "Beta", "Gamma" };
        selector.ItemsSource = items;

        selector.SelectedItem = "Gamma";

        await Assert.That(selector.SelectedIndex).IsEqualTo(2);
    }

    [Test]
    public async Task SetSelectedIndex_ToMinusOne_ClearsSelectedItem()
    {
        var selector = new TestSelector();
        var items = new ObservableCollection<string> { "Alpha", "Beta" };
        selector.ItemsSource = items;
        selector.SelectedIndex = 0;

        selector.SelectedIndex = -1;

        await Assert.That(selector.SelectedItem).IsNull();
    }

    [Test]
    public async Task SetSelectedItem_ToNull_ClearsSelectedIndex()
    {
        var selector = new TestSelector();
        var items = new ObservableCollection<string> { "Alpha", "Beta" };
        selector.ItemsSource = items;
        selector.SelectedItem = "Alpha";

        selector.SelectedItem = null;

        await Assert.That(selector.SelectedIndex).IsEqualTo(-1);
    }

    [Test]
    public async Task SetSelectedItem_ToNonExistent_SetsIndexMinusOne()
    {
        var selector = new TestSelector();
        var items = new ObservableCollection<string> { "Alpha", "Beta" };
        selector.ItemsSource = items;

        selector.SelectedItem = "NotInList";

        await Assert.That(selector.SelectedIndex).IsEqualTo(-1);
    }

    [Test]
    public async Task SetSelectedIndex_OutOfRange_ClearsSelection()
    {
        var selector = new TestSelector();
        var items = new ObservableCollection<string> { "Alpha", "Beta" };
        selector.ItemsSource = items;
        selector.SelectedIndex = 0;

        selector.SelectedIndex = 99;

        await Assert.That(selector.SelectedItem).IsNull();
    }

    #endregion

    #region SelectionChanged Event

    [Test]
    public async Task SelectionChanged_FiredOnSelectedIndexChange()
    {
        var selector = new TestSelector();
        var items = new ObservableCollection<string> { "A", "B", "C" };
        selector.ItemsSource = items;

        SelectionChangedEventArgs? eventArgs = null;
        selector.SelectionChanged += (_, e) => eventArgs = e;

        selector.SelectedIndex = 2;

        await Assert.That(eventArgs).IsNotNull();
        await Assert.That(eventArgs!.AddedItems.Count).IsEqualTo(1);
        await Assert.That(eventArgs.AddedItems[0]).IsEqualTo("C");
        await Assert.That(eventArgs.RemovedItems.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SelectionChanged_FiredOnSelectedItemChange()
    {
        var selector = new TestSelector();
        var items = new ObservableCollection<string> { "A", "B", "C" };
        selector.ItemsSource = items;

        SelectionChangedEventArgs? eventArgs = null;
        selector.SelectionChanged += (_, e) => eventArgs = e;

        selector.SelectedItem = "B";

        await Assert.That(eventArgs).IsNotNull();
        await Assert.That(eventArgs!.AddedItems.Count).IsEqualTo(1);
        await Assert.That(eventArgs.AddedItems[0]).IsEqualTo("B");
    }

    [Test]
    public async Task SelectionChanged_IncludesRemovedItems_WhenChangingSelection()
    {
        var selector = new TestSelector();
        var items = new ObservableCollection<string> { "A", "B", "C" };
        selector.ItemsSource = items;
        selector.SelectedIndex = 0;

        SelectionChangedEventArgs? eventArgs = null;
        selector.SelectionChanged += (_, e) => eventArgs = e;

        selector.SelectedIndex = 2;

        await Assert.That(eventArgs).IsNotNull();
        await Assert.That(eventArgs!.RemovedItems.Count).IsEqualTo(1);
        await Assert.That(eventArgs.RemovedItems[0]).IsEqualTo("A");
        await Assert.That(eventArgs.AddedItems[0]).IsEqualTo("C");
    }

    #endregion

    #region Container Selection State

    [Test]
    public async Task SettingSelectedIndex_UpdatesContainerIsSelected()
    {
        var selector = new TestSelector();
        var items = new ObservableCollection<string> { "A", "B", "C" };
        selector.ItemsSource = items;

        selector.SelectedIndex = 1;

        var container = selector.ContainerFromItem("B") as ListBoxItem;
        await Assert.That(container).IsNotNull();
        await Assert.That(container!.IsSelected).IsTrue();

        var otherContainer = selector.ContainerFromItem("A") as ListBoxItem;
        await Assert.That(otherContainer).IsNotNull();
        await Assert.That(otherContainer!.IsSelected).IsFalse();
    }

    [Test]
    public async Task ChangingSelection_DeselectsPreviousContainer()
    {
        var selector = new TestSelector();
        var items = new ObservableCollection<string> { "A", "B" };
        selector.ItemsSource = items;

        selector.SelectedIndex = 0;
        var containerA = selector.ContainerFromItem("A") as ListBoxItem;
        await Assert.That(containerA!.IsSelected).IsTrue();

        selector.SelectedIndex = 1;
        await Assert.That(containerA!.IsSelected).IsFalse();

        var containerB = selector.ContainerFromItem("B") as ListBoxItem;
        await Assert.That(containerB!.IsSelected).IsTrue();
    }

    #endregion

    #region NotifyItemClicked / NotifyContainerClicked

    [Test]
    public async Task NotifyItemClicked_SelectsItem()
    {
        var selector = new TestSelector();
        var items = new ObservableCollection<string> { "X", "Y", "Z" };
        selector.ItemsSource = items;

        selector.NotifyItemClicked("Y");

        await Assert.That(selector.SelectedIndex).IsEqualTo(1);
        await Assert.That(selector.SelectedItem).IsEqualTo("Y");
    }

    [Test]
    public async Task NotifyContainerClicked_SelectsItem()
    {
        var selector = new TestSelector();
        var items = new ObservableCollection<string> { "X", "Y", "Z" };
        selector.ItemsSource = items;

        var container = selector.ContainerFromItem("Z")!;
        selector.NotifyContainerClicked(container);

        await Assert.That(selector.SelectedIndex).IsEqualTo(2);
        await Assert.That(selector.SelectedItem).IsEqualTo("Z");
    }

    #endregion

    #region RefreshItems Preserves Selection

    [Test]
    public async Task RefreshItems_PreservesSelection_WhenItemStillExists()
    {
        var selector = new TestSelector();
        var items = new ObservableCollection<string> { "A", "B", "C" };
        selector.ItemsSource = items;
        selector.SelectedItem = "B";

        // Trigger a reset by reassigning the same source
        selector.ItemsSource = new ObservableCollection<string> { "A", "B", "C" };

        await Assert.That(selector.SelectedItem).IsEqualTo("B");
        await Assert.That(selector.SelectedIndex).IsEqualTo(1);
    }

    [Test]
    public async Task RefreshItems_ClearsSelection_WhenItemRemoved()
    {
        var selector = new TestSelector();
        var items = new ObservableCollection<string> { "A", "B", "C" };
        selector.ItemsSource = items;
        selector.SelectedItem = "B";

        // Replace with new source that doesn't contain "B"
        selector.ItemsSource = new ObservableCollection<string> { "A", "C" };

        await Assert.That(selector.SelectedItem).IsNull();
        await Assert.That(selector.SelectedIndex).IsEqualTo(-1);
    }

    #endregion

    #region Empty / Edge Cases

    [Test]
    public async Task SetSelectedIndex_OnEmptySource_RemainsMinusOne()
    {
        var selector = new TestSelector();
        selector.ItemsSource = new ObservableCollection<string>();

        selector.SelectedIndex = 0;

        // Out of range → clears
        await Assert.That(selector.SelectedItem).IsNull();
    }

    [Test]
    public async Task NoItemsSource_CanStillSelectFromItems()
    {
        var selector = new TestSelector();
        selector.Items.Add("Foo");
        selector.Items.Add("Bar");

        selector.SelectedItem = "Bar";

        await Assert.That(selector.SelectedIndex).IsEqualTo(1);
    }

    #endregion
}
