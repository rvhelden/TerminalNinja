using TerminalNinja.Primitives;

namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for Grid layout container covering:
/// - Attached properties (Row, Column, RowSpan, ColumnSpan)
/// - Row/Column definitions with Pixel, Auto, and Star sizing
/// - Child positioning and rendering
/// - Edge cases (empty grid, out-of-bounds positions)
/// </summary>
/// <remarks>
/// NotInParallel is required because Grid uses AttachablePropertyServices which
/// uses a static Dictionary that is not thread-safe for concurrent modifications.
/// </remarks>
[NotInParallel]
public class GridTests
{
    private CellBuffer _buffer = null!;
    private const int BufferWidth = 100;
    private const int BufferHeight = 50;

    [Before(Test)]
    public Task Setup()
    {
        _buffer = new CellBuffer(BufferWidth, BufferHeight);
        return Task.CompletedTask;
    }

    #region Attached Properties

    [Test]
    public async Task GetRow_DefaultValue_ReturnsZero()
    {
        // Arrange
        var control = new Rectangle();
        
        // Act
        var row = Grid.GetRow(control);
        
        // Assert
        await Assert.That(row).IsEqualTo(0);
    }

    [Test]
    public async Task SetRow_ValidValue_SetsRow()
    {
        // Arrange
        var control = new Rectangle();
        
        // Act
        Grid.SetRow(control, 2);
        
        // Assert
        await Assert.That(Grid.GetRow(control)).IsEqualTo(2);
    }

    [Test]
    public async Task SetRow_NegativeValue_ClampsToZero()
    {
        // Arrange
        var control = new Rectangle();
        
        // Act
        Grid.SetRow(control, -5);
        
        // Assert
        await Assert.That(Grid.GetRow(control)).IsEqualTo(0);
    }

    [Test]
    public async Task GetColumn_DefaultValue_ReturnsZero()
    {
        // Arrange
        var control = new Rectangle();
        
        // Act
        var column = Grid.GetColumn(control);
        
        // Assert
        await Assert.That(column).IsEqualTo(0);
    }

    [Test]
    public async Task SetColumn_ValidValue_SetsColumn()
    {
        // Arrange
        var control = new Rectangle();
        
        // Act
        Grid.SetColumn(control, 3);
        
        // Assert
        await Assert.That(Grid.GetColumn(control)).IsEqualTo(3);
    }

    [Test]
    public async Task GetRowSpan_DefaultValue_ReturnsOne()
    {
        // Arrange
        var control = new Rectangle();
        
        // Act
        var rowSpan = Grid.GetRowSpan(control);
        
        // Assert
        await Assert.That(rowSpan).IsEqualTo(1);
    }

    [Test]
    public async Task SetRowSpan_ValidValue_SetsRowSpan()
    {
        // Arrange
        var control = new Rectangle();
        
        // Act
        Grid.SetRowSpan(control, 2);
        
        // Assert
        await Assert.That(Grid.GetRowSpan(control)).IsEqualTo(2);
    }

    [Test]
    public async Task SetRowSpan_ZeroValue_ClampsToOne()
    {
        // Arrange
        var control = new Rectangle();
        
        // Act
        Grid.SetRowSpan(control, 0);
        
        // Assert
        await Assert.That(Grid.GetRowSpan(control)).IsEqualTo(1);
    }

    [Test]
    public async Task GetColumnSpan_DefaultValue_ReturnsOne()
    {
        // Arrange
        var control = new Rectangle();
        
        // Act
        var columnSpan = Grid.GetColumnSpan(control);
        
        // Assert
        await Assert.That(columnSpan).IsEqualTo(1);
    }

    [Test]
    public async Task SetColumnSpan_ValidValue_SetsColumnSpan()
    {
        // Arrange
        var control = new Rectangle();
        
        // Act
        Grid.SetColumnSpan(control, 3);
        
        // Assert
        await Assert.That(Grid.GetColumnSpan(control)).IsEqualTo(3);
    }

    [Test]
    public async Task AttachedProperties_NullElement_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => Grid.GetRow(null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => Grid.SetRow(null!, 1)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => Grid.GetColumn(null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => Grid.SetColumn(null!, 1)).ThrowsExactly<ArgumentNullException>();
    }

    #endregion

    #region RowDefinition and ColumnDefinition

    [Test]
    public async Task RowDefinition_DefaultHeight_IsStar()
    {
        // Arrange
        var row = new RowDefinition();
        
        // Assert
        await Assert.That(row.Height.IsStar).IsTrue();
    }

    [Test]
    public async Task RowDefinition_SetHeight_UpdatesHeight()
    {
        // Arrange
        var row = new RowDefinition();
        
        // Act
        row.Height = GridLength.Pixel(10);
        
        // Assert
        await Assert.That(row.Height.IsAbsolute).IsTrue();
        await Assert.That(row.Height.Value).IsEqualTo(10);
    }

    [Test]
    public async Task ColumnDefinition_DefaultWidth_IsStar()
    {
        // Arrange
        var col = new ColumnDefinition();
        
        // Assert
        await Assert.That(col.Width.IsStar).IsTrue();
    }

    [Test]
    public async Task ColumnDefinition_SetWidth_UpdatesWidth()
    {
        // Arrange
        var col = new ColumnDefinition();
        
        // Act
        col.Width = GridLength.Pixel(20);
        
        // Assert
        await Assert.That(col.Width.IsAbsolute).IsTrue();
        await Assert.That(col.Width.Value).IsEqualTo(20);
    }

    [Test]
    public async Task RowDefinition_MinMaxHeight_ClampsValues()
    {
        // Arrange
        var row = new RowDefinition();
        
        // Act
        row.MinHeight = -5;
        row.MaxHeight = 100;
        
        // Assert
        await Assert.That(row.MinHeight).IsEqualTo(0); // Clamped to 0
        await Assert.That(row.MaxHeight).IsEqualTo(100);
    }

    #endregion

    #region Render - Basic Layout

    [Test]
    public void Render_EmptyGrid_DoesNotCrash()
    {
        // Arrange
        var grid = new Grid();
        var bounds = new Rect(0, 0, 100, 50);
        
        // Act & Assert - No exception thrown
        grid.Render(_buffer, bounds);
    }

    [Test]
    public async Task Render_SingleChild_FillsEntireGrid()
    {
        // Arrange
        var child = new Rectangle { BackgroundColor = Color.Red };
        var grid = new Grid();
        grid.Children.Add(child);
        var bounds = new Rect(0, 0, 50, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - Child fills entire grid
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(49, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 19).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(49, 19).Background).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task Render_TwoRows_SplitsVertically()
    {
        // Arrange
        var child1 = new Rectangle { BackgroundColor = Color.Red };
        var child2 = new Rectangle { BackgroundColor = Color.Green };
        Grid.SetRow(child2, 1);
        
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star() });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star() });
        grid.Children.Add(child1);
        grid.Children.Add(child2);
        
        var bounds = new Rect(0, 0, 50, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - Each row gets 10 height (20/2)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 9).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 10).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 19).Background).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task Render_TwoColumns_SplitsHorizontally()
    {
        // Arrange
        var child1 = new Rectangle { BackgroundColor = Color.Red };
        var child2 = new Rectangle { BackgroundColor = Color.Blue };
        Grid.SetColumn(child2, 1);
        
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star() });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star() });
        grid.Children.Add(child1);
        grid.Children.Add(child2);
        
        var bounds = new Rect(0, 0, 50, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - Each column gets 25 width (50/2)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(24, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(25, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(49, 0).Background).IsEqualTo(Color.Blue);
    }

    #endregion

    #region Render - Pixel Sizing

    [Test]
    public async Task Render_PixelRow_UsesExactSize()
    {
        // Arrange
        var child1 = new Rectangle { BackgroundColor = Color.Red };
        var child2 = new Rectangle { BackgroundColor = Color.Green };
        Grid.SetRow(child2, 1);
        
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Pixel(5) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star() });
        grid.Children.Add(child1);
        grid.Children.Add(child2);
        
        var bounds = new Rect(0, 0, 50, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - First row is 5, second row is 15
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 4).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 5).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 19).Background).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task Render_PixelColumn_UsesExactSize()
    {
        // Arrange
        var child1 = new Rectangle { BackgroundColor = Color.Red };
        var child2 = new Rectangle { BackgroundColor = Color.Blue };
        Grid.SetColumn(child2, 1);
        
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Pixel(10) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star() });
        grid.Children.Add(child1);
        grid.Children.Add(child2);
        
        var bounds = new Rect(0, 0, 50, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - First column is 10, second column is 40
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(9, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(10, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(49, 0).Background).IsEqualTo(Color.Blue);
    }

    #endregion

    #region Render - Star Sizing with Weights

    [Test]
    public async Task Render_WeightedStarColumns_DistributesProportionally()
    {
        // Arrange
        var child1 = new Rectangle { BackgroundColor = Color.Red };
        var child2 = new Rectangle { BackgroundColor = Color.Blue };
        Grid.SetColumn(child2, 1);
        
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star(1) }); // 1/3
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star(2) }); // 2/3
        grid.Children.Add(child1);
        grid.Children.Add(child2);
        
        var bounds = new Rect(0, 0, 90, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - First column: 30 (90*1/3), Second column: 60 (90*2/3)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(29, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(30, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(89, 0).Background).IsEqualTo(Color.Blue);
    }

    #endregion

    #region Render - RowSpan and ColumnSpan

    [Test]
    public async Task Render_RowSpan_ChildSpansMultipleRows()
    {
        // Arrange
        var child = new Rectangle { BackgroundColor = Color.Cyan };
        Grid.SetRowSpan(child, 2);
        
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Pixel(10) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Pixel(10) });
        grid.Children.Add(child);
        
        var bounds = new Rect(0, 0, 50, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - Child spans both rows (y=0 to y=19)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Cyan);
        await Assert.That(_buffer.GetCell(0, 10).Background).IsEqualTo(Color.Cyan);
        await Assert.That(_buffer.GetCell(0, 19).Background).IsEqualTo(Color.Cyan);
    }

    [Test]
    public async Task Render_ColumnSpan_ChildSpansMultipleColumns()
    {
        // Arrange
        var child = new Rectangle { BackgroundColor = Color.Magenta };
        Grid.SetColumnSpan(child, 2);
        
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Pixel(20) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Pixel(30) });
        grid.Children.Add(child);
        
        var bounds = new Rect(0, 0, 50, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - Child spans both columns (x=0 to x=49)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Magenta);
        await Assert.That(_buffer.GetCell(20, 0).Background).IsEqualTo(Color.Magenta);
        await Assert.That(_buffer.GetCell(49, 0).Background).IsEqualTo(Color.Magenta);
    }

    #endregion

    #region Render - Complex Grid

    [Test]
    public async Task Render_2x2Grid_PositionsChildrenCorrectly()
    {
        // Arrange
        var topLeft = new Rectangle { BackgroundColor = Color.Red };
        var topRight = new Rectangle { BackgroundColor = Color.Green };
        var bottomLeft = new Rectangle { BackgroundColor = Color.Blue };
        var bottomRight = new Rectangle { BackgroundColor = Color.Yellow };
        
        Grid.SetRow(topLeft, 0);
        Grid.SetColumn(topLeft, 0);
        Grid.SetRow(topRight, 0);
        Grid.SetColumn(topRight, 1);
        Grid.SetRow(bottomLeft, 1);
        Grid.SetColumn(bottomLeft, 0);
        Grid.SetRow(bottomRight, 1);
        Grid.SetColumn(bottomRight, 1);
        
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star() });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star() });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star() });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star() });
        grid.Children.Add(topLeft);
        grid.Children.Add(topRight);
        grid.Children.Add(bottomLeft);
        grid.Children.Add(bottomRight);
        
        var bounds = new Rect(0, 0, 40, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - Each cell is 20x10
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);      // top-left
        await Assert.That(_buffer.GetCell(20, 0).Background).IsEqualTo(Color.Green);   // top-right
        await Assert.That(_buffer.GetCell(0, 10).Background).IsEqualTo(Color.Blue);    // bottom-left
        await Assert.That(_buffer.GetCell(20, 10).Background).IsEqualTo(Color.Yellow); // bottom-right
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task Render_ChildRowExceedsDefinitions_ClampsToLastRow()
    {
        // Arrange
        var child = new Rectangle { BackgroundColor = Color.Red };
        Grid.SetRow(child, 10); // Way beyond defined rows
        
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star() });
        grid.Children.Add(child);
        
        var bounds = new Rect(0, 0, 50, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - Child is clamped to row 0 (only row available)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task CalculateBounds_ReturnsParentBounds()
    {
        // Arrange
        var grid = new Grid();
        var parent = new Rect(10, 20, 80, 40);
        
        // Act
        var bounds = grid.CalculateBounds(parent);
        
        // Assert
        await Assert.That(bounds).IsEqualTo(parent);
    }

    [Test]
    public async Task GetPreferredSize_ReturnsParentSize()
    {
        // Arrange
        var grid = new Grid();
        var parent = new Rect(0, 0, 100, 50);
        
        // Act
        var size = grid.GetPreferredSize(parent);
        
        // Assert
        await Assert.That(size.Width).IsEqualTo(100);
        await Assert.That(size.Height).IsEqualTo(50);
    }

    #endregion
}
