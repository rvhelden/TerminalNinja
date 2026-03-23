namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for Window control covering:
/// - Title property
/// - Content property and parent relationship
/// - Width/Height sizing
/// - Rendering (delegates to Content)
/// - Show/Close integration with Application
/// </summary>
public class WindowTests
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

    #region Title Property

    [Test]
    public async Task Title_DefaultValue_IsEmpty()
    {
        // Arrange
        var window = new Window();
        
        // Assert
        await Assert.That(window.Title).IsEqualTo("");
    }

    [Test]
    public async Task Title_SetValue_UpdatesTitle()
    {
        // Arrange
        var window = new Window();
        
        // Act
        window.Title = "My Window";
        
        // Assert
        await Assert.That(window.Title).IsEqualTo("My Window");
    }

    #endregion

    #region Content Property

    [Test]
    public async Task Content_DefaultValue_IsNull()
    {
        // Arrange
        var window = new Window();
        
        // Assert
        await Assert.That(window.Content).IsNull();
    }

    [Test]
    public async Task Content_SetValue_UpdatesContent()
    {
        // Arrange
        var window = new Window();
        var content = new global::TerminalNinja.Controls.Border();
        
        // Act
        window.Content = content;
        
        // Assert
        await Assert.That(window.Content).IsEqualTo(content);
    }

    [Test]
    public async Task Content_SetValue_SetsParentRelationship()
    {
        // Arrange
        var window = new Window();
        var content = new global::TerminalNinja.Controls.Border();
        
        // Act
        window.Content = content;
        
        // Assert
        await Assert.That(content.Parent).IsEqualTo(window);
    }

    [Test]
    public async Task Content_ReplaceContent_ClearsOldParent()
    {
        // Arrange
        var window = new Window();
        var oldContent = new global::TerminalNinja.Controls.Border();
        var newContent = new global::TerminalNinja.Controls.Border();
        window.Content = oldContent;
        
        // Act
        window.Content = newContent;
        
        // Assert
        await Assert.That(oldContent.Parent).IsNull();
        await Assert.That(newContent.Parent).IsEqualTo(window);
    }

    [Test]
    public async Task Content_SetNull_ClearsParent()
    {
        // Arrange
        var window = new Window();
        var content = new global::TerminalNinja.Controls.Border();
        window.Content = content;
        
        // Act
        window.Content = null;
        
        // Assert
        await Assert.That(content.Parent).IsNull();
    }

    #endregion

    #region Width and Height

    [Test]
    public async Task Width_DefaultValue_IsStretch()
    {
        // Arrange
        var window = new Window();
        
        // Assert
        await Assert.That(window.Width).IsEqualTo(Size.Stretch);
    }

    [Test]
    public async Task Height_DefaultValue_IsStretch()
    {
        // Arrange
        var window = new Window();
        
        // Assert
        await Assert.That(window.Height).IsEqualTo(Size.Stretch);
    }

    [Test]
    public async Task CalculateBounds_WithStretch_FillsParent()
    {
        // Arrange
        var window = new Window();
        var parent = new Rect(0, 0, 100, 50);
        
        // Act
        var bounds = window.CalculateBounds(parent);
        
        // Assert
        await Assert.That(bounds.Width).IsEqualTo(100);
        await Assert.That(bounds.Height).IsEqualTo(50);
    }

    [Test]
    public async Task CalculateBounds_WithAbsoluteSize_UsesExactSize()
    {
        // Arrange
        var window = new Window
        {
            Width = Size.Absolute(80),
            Height = Size.Absolute(30)
        };
        var parent = new Rect(0, 0, 100, 50);
        
        // Act
        var bounds = window.CalculateBounds(parent);
        
        // Assert
        await Assert.That(bounds.Width).IsEqualTo(80);
        await Assert.That(bounds.Height).IsEqualTo(30);
    }

    #endregion

    #region GetPreferredSize

    [Test]
    public async Task GetPreferredSize_NoContent_ReturnsZero()
    {
        // Arrange
        var window = new Window();
        var parent = new Rect(0, 0, 100, 50);
        
        // Act
        var size = window.GetPreferredSize(parent);
        
        // Assert
        await Assert.That(size.Width).IsEqualTo(0);
        await Assert.That(size.Height).IsEqualTo(0);
    }

    [Test]
    public async Task GetPreferredSize_WithContent_ReturnsContentPreferredSize()
    {
        // Arrange
        var content = new global::TerminalNinja.Controls.Border
        {
            Width = Size.Absolute(60),
            Height = Size.Absolute(30)
        };
        var window = new Window { Content = content };
        var parent = new Rect(0, 0, 100, 50);
        
        // Act
        var size = window.GetPreferredSize(parent);
        
        // Assert
        await Assert.That(size.Width).IsEqualTo(60);
        await Assert.That(size.Height).IsEqualTo(30);
    }

    #endregion

    #region Render

    [Test]
    public async Task Render_NoContent_DoesNothing()
    {
        // Arrange
        var window = new Window();
        var bounds = new Rect(0, 0, 100, 50);
        
        // Act
        window.Render(_buffer, bounds);
        
        // Assert - Buffer should remain empty (all cells black)
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Black);
    }

    [Test]
    public async Task Render_WithContent_RendersContentWithinBounds()
    {
        // Arrange
        var content = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        var window = new Window { Content = content };
        var bounds = new Rect(5, 10, 50, 20);
        
        // Act
        window.Render(_buffer, bounds);
        
        // Assert - Content fills window bounds
        await Assert.That(_buffer.GetCell(5, 10).Background).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(54, 29).Background).IsEqualTo(Color.Red);
        // Outside bounds should still be black
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Black);
    }

    [Test]
    public async Task Render_WithAbsoluteSize_ContentUsesWindowBounds()
    {
        // Arrange
        var content = new global::TerminalNinja.Controls.Border { Background = Color.Blue };
        var window = new Window
        {
            Content = content,
            Width = Size.Absolute(30),
            Height = Size.Absolute(10)
        };
        var parent = new Rect(0, 0, 100, 50);
        
        // Act
        window.Render(_buffer, parent);
        
        // Assert - Content fills 30x10 starting at 0,0
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(29, 9).Background).IsEqualTo(Color.Blue);
        // Outside window bounds should be black
        await Assert.That(_buffer.GetCell(30, 0).Background).IsEqualTo(Color.Black);
        await Assert.That(_buffer.GetCell(0, 10).Background).IsEqualTo(Color.Black);
    }

    [Test]
    public async Task Render_AfterResize_ContentStaysWithinWindowBounds()
    {
        // Arrange - Window with fixed 30x10 size
        var content = new global::TerminalNinja.Controls.Border { Background = Color.Blue };
        var window = new Window
        {
            Content = content,
            Width = Size.Absolute(30),
            Height = Size.Absolute(10)
        };

        // Initial render at 100x50 (simulates initial console size)
        var initialParent = new Rect(0, 0, 100, 50);
        window.Render(_buffer, initialParent);

        // Verify initial render is correct
        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(29, 9).Background).IsEqualTo(Color.Blue);
        await Assert.That(_buffer.GetCell(30, 0).Background).IsEqualTo(Color.Black);

        // Act - Simulate resize: create new larger buffer and re-render with larger parent bounds
        using var resizedBuffer = new CellBuffer(120, 60);
        var resizedParent = new Rect(0, 0, 120, 60);
        window.Render(resizedBuffer, resizedParent);

        // Assert - Content should still be constrained to 30x10
        await Assert.That(resizedBuffer.GetCell(0, 0).Background).IsEqualTo(Color.Blue);
        await Assert.That(resizedBuffer.GetCell(29, 9).Background).IsEqualTo(Color.Blue);
        // Outside the 30x10 window bounds should be black (not blue)
        await Assert.That(resizedBuffer.GetCell(30, 0).Background).IsEqualTo(Color.Black);
        await Assert.That(resizedBuffer.GetCell(0, 10).Background).IsEqualTo(Color.Black);
        await Assert.That(resizedBuffer.GetCell(119, 59).Background).IsEqualTo(Color.Black);
    }

    [Test]
    public async Task Render_WithXamlLoadedWindow_RespectsWidthHeightAfterResize()
    {
        // Arrange - Load Window from XAML with Width="80" Height="24"
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    Title="Test"
                    Width="80"
                    Height="24">
                <Border Background="Green" />
            </Window>
            """;
        var window = TerminalXaml.Load<Window>(xaml);

        // Verify Width/Height were set correctly from XAML
        await Assert.That(window.Width).IsEqualTo(Size.Absolute(80));
        await Assert.That(window.Height).IsEqualTo(Size.Absolute(24));

        // Initial render at 80x24 (console matches window size)
        using var buffer = new CellBuffer(80, 24);
        window.Render(buffer, new Rect(0, 0, 80, 24));
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(79, 23).Background).IsEqualTo(Color.Green);

        // Act - Simulate resize to larger console (120x40)
        using var buffer2 = new CellBuffer(120, 40);
        window.Render(buffer2, new Rect(0, 0, 120, 40));

        // Assert - Content should stay within 80x24
        await Assert.That(buffer2.GetCell(0, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer2.GetCell(79, 23).Background).IsEqualTo(Color.Green);
        // Outside window bounds should be black
        await Assert.That(buffer2.GetCell(80, 0).Background).IsEqualTo(Color.Black);
        await Assert.That(buffer2.GetCell(0, 24).Background).IsEqualTo(Color.Black);
        await Assert.That(buffer2.GetCell(119, 39).Background).IsEqualTo(Color.Black);
    }

    #endregion

    #region Resources

    [Test]
    public async Task Resources_DefaultValue_IsNotNull()
    {
        // Arrange
        var window = new Window();
        
        // Assert
        await Assert.That(window.Resources).IsNotNull();
    }

    [Test]
    public async Task Resources_AddResource_CanBeRetrieved()
    {
        // Arrange
        var window = new Window();
        
        // Act
        window.Resources.Add("MyColor", Color.Cyan);
        
        // Assert
        await Assert.That(window.TryFindResource("MyColor")).IsEqualTo(Color.Cyan);
    }

    [Test]
    public async Task TryFindResource_ChildFindsParentResource()
    {
        // Arrange
        var window = new Window();
        window.Resources.Add("MyColor", Color.Magenta);
        
        var content = new TextBlock { Text = "Test" };
        window.Content = content;
        
        // Act - Content should find resource defined in Window
        var result = content.TryFindResource("MyColor");
        
        // Assert
        await Assert.That(result).IsEqualTo(Color.Magenta);
    }

    #endregion
}
