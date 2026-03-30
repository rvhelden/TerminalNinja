namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for Grid layout container covering:
/// - Attached properties (Row, Column, RowSpan, ColumnSpan)
/// - Row/Column definitions with Pixel, Auto, and Star sizing
/// - Child positioning and rendering
/// - Edge cases (empty grid, out-of-bounds positions)
/// </summary>
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

    [After(Test)]
    public Task Cleanup()
    {
        _buffer.Dispose();
        return Task.CompletedTask;
    }

    #region Attached Properties

    [Test]
    public async Task GetRow_DefaultValue_ReturnsZero()
    {
        // Arrange
        var control = new global::TerminalNinja.Controls.Border();
        
        // Act
        var row = Grid.GetRow(control);
        
        // Assert
        await Assert.That(row).IsEqualTo(0);
    }

    [Test]
    public async Task SetRow_ValidValue_SetsRow()
    {
        // Arrange
        var control = new global::TerminalNinja.Controls.Border();
        
        // Act
        Grid.SetRow(control, 2);
        
        // Assert
        await Assert.That(Grid.GetRow(control)).IsEqualTo(2);
    }

    [Test]
    public async Task SetRow_NegativeValue_ClampsToZero()
    {
        // Arrange
        var control = new global::TerminalNinja.Controls.Border();
        
        // Act
        Grid.SetRow(control, -5);
        
        // Assert
        await Assert.That(Grid.GetRow(control)).IsEqualTo(0);
    }

    [Test]
    public async Task GetColumn_DefaultValue_ReturnsZero()
    {
        // Arrange
        var control = new global::TerminalNinja.Controls.Border();
        
        // Act
        var column = Grid.GetColumn(control);
        
        // Assert
        await Assert.That(column).IsEqualTo(0);
    }

    [Test]
    public async Task SetColumn_ValidValue_SetsColumn()
    {
        // Arrange
        var control = new global::TerminalNinja.Controls.Border();
        
        // Act
        Grid.SetColumn(control, 3);
        
        // Assert
        await Assert.That(Grid.GetColumn(control)).IsEqualTo(3);
    }

    [Test]
    public async Task GetRowSpan_DefaultValue_ReturnsOne()
    {
        // Arrange
        var control = new global::TerminalNinja.Controls.Border();
        
        // Act
        var rowSpan = Grid.GetRowSpan(control);
        
        // Assert
        await Assert.That(rowSpan).IsEqualTo(1);
    }

    [Test]
    public async Task SetRowSpan_ValidValue_SetsRowSpan()
    {
        // Arrange
        var control = new global::TerminalNinja.Controls.Border();
        
        // Act
        Grid.SetRowSpan(control, 2);
        
        // Assert
        await Assert.That(Grid.GetRowSpan(control)).IsEqualTo(2);
    }

    [Test]
    public async Task SetRowSpan_ZeroValue_ClampsToOne()
    {
        // Arrange
        var control = new global::TerminalNinja.Controls.Border();
        
        // Act
        Grid.SetRowSpan(control, 0);
        
        // Assert
        await Assert.That(Grid.GetRowSpan(control)).IsEqualTo(1);
    }

    [Test]
    public async Task GetColumnSpan_DefaultValue_ReturnsOne()
    {
        // Arrange
        var control = new global::TerminalNinja.Controls.Border();
        
        // Act
        var columnSpan = Grid.GetColumnSpan(control);
        
        // Assert
        await Assert.That(columnSpan).IsEqualTo(1);
    }

    [Test]
    public async Task SetColumnSpan_ValidValue_SetsColumnSpan()
    {
        // Arrange
        var control = new global::TerminalNinja.Controls.Border();
        
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
        var child = new global::TerminalNinja.Controls.Border { Background = Color.Red };
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
        var child1 = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        var child2 = new global::TerminalNinja.Controls.Border { Background = Color.Green };
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
        var child1 = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        var child2 = new global::TerminalNinja.Controls.Border { Background = Color.Blue };
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
        var child1 = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        var child2 = new global::TerminalNinja.Controls.Border { Background = Color.Green };
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
        var child1 = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        var child2 = new global::TerminalNinja.Controls.Border { Background = Color.Blue };
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
        var child1 = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        var child2 = new global::TerminalNinja.Controls.Border { Background = Color.Blue };
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
        var child = new global::TerminalNinja.Controls.Border { Background = Color.Cyan };
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
        var child = new global::TerminalNinja.Controls.Border { Background = Color.Magenta };
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
        var topLeft = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        var topRight = new global::TerminalNinja.Controls.Border { Background = Color.Green };
        var bottomLeft = new global::TerminalNinja.Controls.Border { Background = Color.Blue };
        var bottomRight = new global::TerminalNinja.Controls.Border { Background = Color.Yellow };
        
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
        var child = new global::TerminalNinja.Controls.Border { Background = Color.Red };
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

    #region RowSpacing and ColumnSpacing

    [Test]
    public async Task RowSpacing_DefaultValue_ReturnsZero()
    {
        // Arrange
        var grid = new Grid();
        
        // Assert
        await Assert.That(grid.RowSpacing).IsEqualTo(0);
    }

    [Test]
    public async Task ColumnSpacing_DefaultValue_ReturnsZero()
    {
        // Arrange
        var grid = new Grid();
        
        // Assert
        await Assert.That(grid.ColumnSpacing).IsEqualTo(0);
    }

    [Test]
    public async Task RowSpacing_SetNegative_ClampsToZero()
    {
        // Arrange
        var grid = new Grid();
        
        // Act
        grid.RowSpacing = -5;
        
        // Assert
        await Assert.That(grid.RowSpacing).IsEqualTo(0);
    }

    [Test]
    public async Task ColumnSpacing_SetNegative_ClampsToZero()
    {
        // Arrange
        var grid = new Grid();
        
        // Act
        grid.ColumnSpacing = -3;
        
        // Assert
        await Assert.That(grid.ColumnSpacing).IsEqualTo(0);
    }

    [Test]
    public async Task Render_WithRowSpacing_InsertsGapsBetweenRows()
    {
        // Arrange - two equal star rows in a 50x20 grid with RowSpacing=4
        // Available for rows = 20 - 4 (one gap) = 16, each row gets 8
        // Row 0: y=0..7, gap: y=8..11, Row 1: y=12..19
        var child1 = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        var child2 = new global::TerminalNinja.Controls.Border { Background = Color.Green };
        Grid.SetRow(child2, 1);
        
        var grid = new Grid { RowSpacing = 4 };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star() });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star() });
        grid.Children.Add(child1);
        grid.Children.Add(child2);
        
        var bounds = new Rect(0, 0, 50, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - Row 0 occupies y=0..7 (8 cells)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 7).Background).IsEqualTo(Color.Red);
        // Gap at y=8..11 should be default (not Red or Green)
        await Assert.That(_buffer.GetCell(0, 8).Background).IsNotEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 8).Background).IsNotEqualTo(Color.Green);
        // Row 1 occupies y=12..19 (8 cells)
        await Assert.That(_buffer.GetCell(0, 12).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(0, 19).Background).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task Render_WithColumnSpacing_InsertsGapsBetweenColumns()
    {
        // Arrange - two equal star columns in a 50x20 grid with ColumnSpacing=10
        // Available for columns = 50 - 10 (one gap) = 40, each column gets 20
        // Col 0: x=0..19, gap: x=20..29, Col 1: x=30..49
        var child1 = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        var child2 = new global::TerminalNinja.Controls.Border { Background = Color.Blue };
        Grid.SetColumn(child2, 1);
        
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star() });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star() });
        grid.Children.Add(child1);
        grid.Children.Add(child2);
        
        var bounds = new Rect(0, 0, 50, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - Col 0 occupies x=0..19
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(19, 0).Background).IsEqualTo(Color.Red);
        // Gap at x=20..29 should be default
        await Assert.That(_buffer.GetCell(20, 0).Background).IsNotEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(20, 0).Background).IsNotEqualTo(Color.Blue);
        // Col 1 occupies x=30..49
        await Assert.That(_buffer.GetCell(30, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(49, 0).Background).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task Render_WithBothSpacings_InsertsGapsInBothDirections()
    {
        // Arrange - 2x2 grid in 40x20 with RowSpacing=2, ColumnSpacing=4
        // Available height = 20 - 2 = 18, each row = 9
        // Available width  = 40 - 4 = 36, each col = 18
        // Row 0: y=0..8, gap: y=9..10, Row 1: y=11..19
        // Col 0: x=0..17, gap: x=18..21, Col 1: x=22..39
        var topLeft = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        var topRight = new global::TerminalNinja.Controls.Border { Background = Color.Green };
        var bottomLeft = new global::TerminalNinja.Controls.Border { Background = Color.Blue };
        var bottomRight = new global::TerminalNinja.Controls.Border { Background = Color.Yellow };
        
        Grid.SetColumn(topRight, 1);
        Grid.SetRow(bottomLeft, 1);
        Grid.SetRow(bottomRight, 1);
        Grid.SetColumn(bottomRight, 1);
        
        var grid = new Grid { RowSpacing = 2, ColumnSpacing = 4 };
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
        
        // Assert - top-left (x=0..17, y=0..8)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(17, 8).Background).IsEqualTo(Color.Red);
        // top-right (x=22..39, y=0..8)
        await Assert.That(_buffer.GetCell(22, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(39, 8).Background).IsEqualTo(Color.Green);
        // bottom-left (x=0..17, y=11..19)
        await Assert.That(_buffer.GetCell(0, 11).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(17, 19).Background).IsEqualTo(Color.Blue);
        // bottom-right (x=22..39, y=11..19)
        await Assert.That(_buffer.GetCell(22, 11).Background).IsEqualTo(Color.Yellow);
        await Assert.That(_buffer.GetCell(39, 19).Background).IsEqualTo(Color.Yellow);
        // Gap intersection (x=18, y=9) should be empty
        await Assert.That(_buffer.GetCell(18, 9).Background).IsNotEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(18, 9).Background).IsNotEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(18, 9).Background).IsNotEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(18, 9).Background).IsNotEqualTo(Color.Yellow);
    }

    [Test]
    public async Task Render_WithRowSpacing_ReducesAvailableSpaceForStarRows()
    {
        // Arrange - 3 star rows in 90 height with RowSpacing=5
        // Available = 90 - 5*2 = 80, each row = 80/3 ≈ 26, 26, 28 (last gets remainder)
        var child1 = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        var child2 = new global::TerminalNinja.Controls.Border { Background = Color.Green };
        var child3 = new global::TerminalNinja.Controls.Border { Background = Color.Blue };
        Grid.SetRow(child2, 1);
        Grid.SetRow(child3, 2);
        
        var grid = new Grid { RowSpacing = 5 };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star() });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star() });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star() });
        grid.Children.Add(child1);
        grid.Children.Add(child2);
        grid.Children.Add(child3);
        
        var bounds = new Rect(0, 0, 50, 90);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - Row 0: y=0..25 (26 cells)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 25).Background).IsEqualTo(Color.Red);
        // Gap: y=26..30
        await Assert.That(_buffer.GetCell(0, 26).Background).IsNotEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 26).Background).IsNotEqualTo(Color.Green);
        // Row 1: y=31..56 (26 cells)
        await Assert.That(_buffer.GetCell(0, 31).Background).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task Render_RowSpan_WithRowSpacing_IncludesGapsInSpannedHeight()
    {
        // Arrange - 2 rows (each 10px) with RowSpacing=4, child spans both rows
        // Total height for spanned child = 10 + 4 + 10 = 24
        // But available = 24 - 4 = 20, each row = 10
        // Spanned height = 10 + 4 + 10 = 24
        var child = new global::TerminalNinja.Controls.Border { Background = Color.Cyan };
        Grid.SetRowSpan(child, 2);
        
        var grid = new Grid { RowSpacing = 4 };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Pixel(10) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Pixel(10) });
        grid.Children.Add(child);
        
        var bounds = new Rect(0, 0, 50, 24);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - Child spans y=0..23 (10 + 4 spacing + 10 = 24)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Cyan);
        await Assert.That(_buffer.GetCell(0, 9).Background).IsEqualTo(Color.Cyan);
        // The spanned child covers the gap area too
        await Assert.That(_buffer.GetCell(0, 10).Background).IsEqualTo(Color.Cyan);
        await Assert.That(_buffer.GetCell(0, 13).Background).IsEqualTo(Color.Cyan);
        await Assert.That(_buffer.GetCell(0, 14).Background).IsEqualTo(Color.Cyan);
        await Assert.That(_buffer.GetCell(0, 23).Background).IsEqualTo(Color.Cyan);
    }

    [Test]
    public async Task Render_ColumnSpan_WithColumnSpacing_IncludesGapsInSpannedWidth()
    {
        // Arrange - 2 columns (each 20px) with ColumnSpacing=6, child spans both
        // Spanned width = 20 + 6 + 20 = 46
        var child = new global::TerminalNinja.Controls.Border { Background = Color.Magenta };
        Grid.SetColumnSpan(child, 2);
        
        var grid = new Grid { ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Pixel(20) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Pixel(20) });
        grid.Children.Add(child);
        
        var bounds = new Rect(0, 0, 46, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - Child spans x=0..45 (20 + 6 spacing + 20 = 46)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Magenta);
        await Assert.That(_buffer.GetCell(19, 0).Background).IsEqualTo(Color.Magenta);
        // The spanned child covers the gap area too
        await Assert.That(_buffer.GetCell(20, 0).Background).IsEqualTo(Color.Magenta);
        await Assert.That(_buffer.GetCell(25, 0).Background).IsEqualTo(Color.Magenta);
        await Assert.That(_buffer.GetCell(26, 0).Background).IsEqualTo(Color.Magenta);
        await Assert.That(_buffer.GetCell(45, 0).Background).IsEqualTo(Color.Magenta);
    }

    [Test]
    public async Task Render_SingleRow_RowSpacing_HasNoEffect()
    {
        // Arrange - single row with RowSpacing=10; spacing only between rows so no effect
        var child = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        
        var grid = new Grid { RowSpacing = 10 };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star() });
        grid.Children.Add(child);
        
        var bounds = new Rect(0, 0, 50, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - Child fills entire height (spacing has no effect with 1 row)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(0, 19).Background).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task Render_SingleColumn_ColumnSpacing_HasNoEffect()
    {
        // Arrange - single column with ColumnSpacing=10; spacing only between columns so no effect
        var child = new global::TerminalNinja.Controls.Border { Background = Color.Blue };
        
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star() });
        grid.Children.Add(child);
        
        var bounds = new Rect(0, 0, 50, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - Child fills entire width (spacing has no effect with 1 column)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(49, 0).Background).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task Render_WithColumnSpacing_PixelAndStar_ReducesStarSpace()
    {
        // Arrange - Pixel(10) + Star in 50 width with ColumnSpacing=5
        // Available = 50 - 5 = 45, Pixel takes 10, Star gets 35
        // Col 0: x=0..9, gap: x=10..14, Col 1: x=15..49
        var child1 = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        var child2 = new global::TerminalNinja.Controls.Border { Background = Color.Blue };
        Grid.SetColumn(child2, 1);
        
        var grid = new Grid { ColumnSpacing = 5 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Pixel(10) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star() });
        grid.Children.Add(child1);
        grid.Children.Add(child2);
        
        var bounds = new Rect(0, 0, 50, 20);
        
        // Act
        grid.Render(_buffer, bounds);
        
        // Assert - Col 0: x=0..9 (pixel 10)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(9, 0).Background).IsEqualTo(Color.Red);
        // Gap: x=10..14
        await Assert.That(_buffer.GetCell(10, 0).Background).IsNotEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(10, 0).Background).IsNotEqualTo(Color.Blue);
        // Col 1: x=15..49 (star gets remaining 35)
        await Assert.That(_buffer.GetCell(15, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(49, 0).Background).IsEqualTo(Color.Blue);
    }

    #endregion
}
