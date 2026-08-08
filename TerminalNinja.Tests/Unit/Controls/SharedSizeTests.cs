namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for Auto sizing measuring its content, and for SharedSizeGroup /
/// Grid.IsSharedSizeScope aligning columns across separate grids.
/// </summary>
public class SharedSizeTests
{
    private CellBuffer _buffer = null!;

    [Before(Test)]
    public Task Setup()
    {
        _buffer = new CellBuffer(120, 40);
        return Task.CompletedTask;
    }

    [After(Test)]
    public Task Cleanup()
    {
        _buffer.Dispose();
        return Task.CompletedTask;
    }

    private static Grid TwoColumnGrid(string first, string? group = null)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = group });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star() });

        var left = new TextBlock { Text = first };
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        var right = new TextBlock { Text = "value" };
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        return grid;
    }

    [Test]
    public async Task AutoColumn_SizesToItsContent()
    {
        var grid = TwoColumnGrid("enter");

        grid.Render(_buffer, new Rect(0, 0, 60, 5));

        // "enter" is five cells. Before Auto measured anything this was MinWidth, i.e. zero.
        await Assert.That(grid.ColumnDefinitions[0].ActualWidth).IsEqualTo(5);
        await Assert.That(grid.ColumnDefinitions[1].ActualWidth).IsEqualTo(55);
    }

    [Test]
    public async Task AutoRow_SizesToItsContent()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star() });

        var top = new TextBlock { Text = "one line" };
        Grid.SetRow(top, 0);
        grid.Children.Add(top);

        grid.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(grid.RowDefinitions[0].ActualHeight).IsEqualTo(1);
        await Assert.That(grid.RowDefinitions[1].ActualHeight).IsEqualTo(9);
    }

    [Test]
    public async Task AutoColumn_IgnoresCollapsedAndSpannedChildren()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star() });

        var visible = new TextBlock { Text = "ab" };
        Grid.SetColumn(visible, 0);
        grid.Children.Add(visible);

        var collapsed = new TextBlock { Text = "a much longer string", Visibility = Visibility.Collapsed };
        Grid.SetColumn(collapsed, 0);
        grid.Children.Add(collapsed);

        var spanning = new TextBlock { Text = "an even longer spanning string" };
        Grid.SetColumn(spanning, 0);
        Grid.SetColumnSpan(spanning, 2);
        grid.Children.Add(spanning);

        grid.Render(_buffer, new Rect(0, 0, 60, 5));

        await Assert.That(grid.ColumnDefinitions[0].ActualWidth).IsEqualTo(2);
    }

    [Test]
    public async Task SharedSizeGroup_MakesTwoGridsAgreeOnTheWidestColumn()
    {
        var host = new StackPanel();
        Grid.SetIsSharedSizeScope(host, true);

        var narrow = TwoColumnGrid("esc", "keys");
        var wide = TwoColumnGrid("backspace", "keys");
        host.Children.Add(narrow);
        host.Children.Add(wide);

        // Twice: the first pass is where each grid publishes what it wants, and a grid that laid
        // out before the wider one had voted cannot know about it yet.
        host.Render(_buffer, new Rect(0, 0, 60, 10));
        host.Render(_buffer, new Rect(0, 0, 60, 10));

        await Assert.That(narrow.ColumnDefinitions[0].ActualWidth).IsEqualTo(9);
        await Assert.That(wide.ColumnDefinitions[0].ActualWidth).IsEqualTo(9);
    }

    [Test]
    public async Task SharedSizeGroup_WithoutAScope_SizesToItsOwnContent()
    {
        var host = new StackPanel();

        var narrow = TwoColumnGrid("esc", "keys");
        var wide = TwoColumnGrid("backspace", "keys");
        host.Children.Add(narrow);
        host.Children.Add(wide);

        host.Render(_buffer, new Rect(0, 0, 60, 10));
        host.Render(_buffer, new Rect(0, 0, 60, 10));

        // No scope means no group. Each falls back to Auto rather than silently doing nothing.
        await Assert.That(narrow.ColumnDefinitions[0].ActualWidth).IsEqualTo(3);
        await Assert.That(wide.ColumnDefinitions[0].ActualWidth).IsEqualTo(9);
    }

    [Test]
    public async Task SharedSizeGroup_DoesNotLeakBetweenScopes()
    {
        var left = new StackPanel();
        Grid.SetIsSharedSizeScope(left, true);
        var leftGrid = TwoColumnGrid("esc", "keys");
        left.Children.Add(leftGrid);

        var right = new StackPanel();
        Grid.SetIsSharedSizeScope(right, true);
        var rightGrid = TwoColumnGrid("backspace", "keys");
        right.Children.Add(rightGrid);

        var root = new StackPanel();
        root.Children.Add(left);
        root.Children.Add(right);

        root.Render(_buffer, new Rect(0, 0, 60, 20));
        root.Render(_buffer, new Rect(0, 0, 60, 20));

        // Same group name, different scopes: the wide one must not widen the narrow one.
        await Assert.That(leftGrid.ColumnDefinitions[0].ActualWidth).IsEqualTo(3);
        await Assert.That(rightGrid.ColumnDefinitions[0].ActualWidth).IsEqualTo(9);
    }

    [Test]
    public async Task SharedSizeGroup_ShrinksWhenTheWidestContentShrinks()
    {
        var host = new StackPanel();
        Grid.SetIsSharedSizeScope(host, true);

        var first = TwoColumnGrid("esc", "keys");
        var second = TwoColumnGrid("backspace", "keys");
        host.Children.Add(first);
        host.Children.Add(second);

        host.Render(_buffer, new Rect(0, 0, 60, 10));
        host.Render(_buffer, new Rect(0, 0, 60, 10));
        await Assert.That(first.ColumnDefinitions[0].ActualWidth).IsEqualTo(9);

        ((TextBlock)second.Children[0]).Text = "up";

        host.Render(_buffer, new Rect(0, 0, 60, 10));
        host.Render(_buffer, new Rect(0, 0, 60, 10));

        // The group must follow the content down, not stay stuck at its high-water mark.
        await Assert.That(first.ColumnDefinitions[0].ActualWidth).IsEqualTo(3);
        await Assert.That(second.ColumnDefinitions[0].ActualWidth).IsEqualTo(3);
    }

    [Test]
    public async Task SharedSizeGroup_OverridesAStarWidth()
    {
        var host = new StackPanel();
        Grid.SetIsSharedSizeScope(host, true);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star(), SharedSizeGroup = "keys" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star() });

        var left = new TextBlock { Text = "enter" };
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);
        host.Children.Add(grid);

        host.Render(_buffer, new Rect(0, 0, 60, 10));
        host.Render(_buffer, new Rect(0, 0, 60, 10));

        // Sharing a proportional width means nothing, so the group sizes it to content instead.
        await Assert.That(grid.ColumnDefinitions[0].ActualWidth).IsEqualTo(5);
    }
}
