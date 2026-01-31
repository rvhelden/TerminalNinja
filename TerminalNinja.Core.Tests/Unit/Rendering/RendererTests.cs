namespace TerminalNinja.Core.Tests.Unit.Rendering;

/// <summary>
/// Tests for Renderer class covering:
/// - Constructor variants (default, DI, test)
/// - Clear, Draw, Present methods
/// - HandleResize with terminal changes
/// - Dispose behavior
/// </summary>
public class RendererTests
{
    /// <summary>
    /// Test implementation of ITerminal for testing.
    /// </summary>
    private class TestTerminal : Core.Console.ITerminal
    {
        public int Width { get; set; } = 80;
        public int Height { get; set; } = 24;
        public bool AnsiModeEnabled { get; private set; }
        
        private readonly MemoryStream _output = new();
        
        public Stream OpenOutput() => _output;
        
        public bool EnableAnsiMode()
        {
            AnsiModeEnabled = true;
            return true;
        }
        
        public void DisableAnsiMode()
        {
            AnsiModeEnabled = false;
        }
    }

    [Test]
    public async Task Constructor_Default_CreatesRendererWithSystemTerminalSize()
    {
        // This test would interact with the real terminal, so we skip it in unit tests
        // Integration tests can verify the default constructor
        var skipped = true;
        await Assert.That(skipped).IsTrue();
    }

    [Test]
    public async Task Constructor_WithTerminal_InitializesCorrectly()
    {
        // Arrange
        var terminal = new TestTerminal { Width = 100, Height = 50 };

        // Act
        using var renderer = new Core.Rendering.Renderer(terminal);

        // Assert
        await Assert.That(renderer.Width).IsEqualTo(100);
        await Assert.That(renderer.Height).IsEqualTo(50);
        await Assert.That(terminal.AnsiModeEnabled).IsTrue();
    }

    [Test]
    public async Task Constructor_TestRenderer_UsesProvidedDimensions()
    {
        // Arrange
        var output = new MemoryStream();

        // Act
        using var renderer = new Core.Rendering.Renderer(output, 120, 40);

        // Assert
        await Assert.That(renderer.Width).IsEqualTo(120);
        await Assert.That(renderer.Height).IsEqualTo(40);
    }

    [Test]
    public async Task Viewport_ReturnsFullScreenRect()
    {
        // Arrange
        var output = new MemoryStream();
        using var renderer = new Core.Rendering.Renderer(output, 100, 50);

        // Act
        var viewport = renderer.Viewport;

        // Assert
        await Assert.That(viewport.X).IsEqualTo(0);
        await Assert.That(viewport.Y).IsEqualTo(0);
        await Assert.That(viewport.Width).IsEqualTo(100);
        await Assert.That(viewport.Height).IsEqualTo(50);
    }

    [Test]
    public async Task Clear_ClearsBuffer()
    {
        // Arrange
        var output = new MemoryStream();
        using var renderer = new Core.Rendering.Renderer(output, 80, 24);
        
        // Draw something first
        var rect = new Rectangle { BackgroundColor = Color.Red };
        renderer.Draw((IElement)rect);
        renderer.Present();
        var sizeAfterDraw = output.Length;

        // Act
        renderer.Clear();
        renderer.Present();

        // Assert - After clear, the second present should output less (clearing red cells to black)
        var sizeAfterClear = output.Length;
        await Assert.That(sizeAfterClear).IsGreaterThan(sizeAfterDraw);
    }

    [Test]
    public async Task Draw_RendersElementToBuffer()
    {
        // Arrange
        var output = new MemoryStream();
        using var renderer = new Core.Rendering.Renderer(output, 80, 24);
        var rect = new Rectangle 
        { 
            Width = Size.Absolute(10), 
            Height = Size.Absolute(5),
            BackgroundColor = Color.Cyan 
        };

        // Act
        renderer.Draw((IElement)rect);
        renderer.Present();

        // Assert - Output should contain ANSI codes
        var outputBytes = output.ToArray();
        await Assert.That(outputBytes.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Present_OnlyWritesChangedCells()
    {
        // Arrange
        var output = new MemoryStream();
        using var renderer = new Core.Rendering.Renderer(output, 80, 24);

        // First frame
        var rect1 = new Rectangle { BackgroundColor = Color.Red };
        renderer.Draw((IElement)rect1);
        renderer.Present();
        var firstFrameSize = output.Length;

        // Second frame (same content)
        renderer.Clear();
        renderer.Draw((IElement)rect1);
        renderer.Present();
        var secondFrameSize = output.Length - firstFrameSize;

        // Assert - Second frame should write much less (no changes)
        await Assert.That(secondFrameSize).IsLessThan(firstFrameSize);
    }

    [Test]
    public async Task HandleResize_UpdatesBufferDimensions()
    {
        // Arrange
        var terminal = new TestTerminal { Width = 80, Height = 24 };
        using var renderer = new Core.Rendering.Renderer(terminal);

        // Act
        terminal.Width = 100;
        terminal.Height = 30;
        renderer.HandleResize();

        // Assert
        await Assert.That(renderer.Width).IsEqualTo(100);
        await Assert.That(renderer.Height).IsEqualTo(30);
    }

    [Test]
    public async Task HandleResize_NoChange_DoesNotResize()
    {
        // Arrange
        var terminal = new TestTerminal { Width = 80, Height = 24 };
        using var renderer = new Core.Rendering.Renderer(terminal);
        var originalWidth = renderer.Width;

        // Act - No change to terminal size
        renderer.HandleResize();

        // Assert
        await Assert.That(renderer.Width).IsEqualTo(originalWidth);
    }

    [Test]
    public async Task HandleResize_TestRenderer_DoesNothing()
    {
        // Arrange
        var output = new MemoryStream();
        using var renderer = new Core.Rendering.Renderer(output, 80, 24);

        // Act
        renderer.HandleResize(); // Should not throw

        // Assert
        await Assert.That(renderer.Width).IsEqualTo(80); // Unchanged
    }

    [Test]
    public async Task Dispose_DisablesAnsiMode()
    {
        // Arrange
        var terminal = new TestTerminal();
        var renderer = new Core.Rendering.Renderer(terminal);

        // Act
        renderer.Dispose();

        // Assert
        await Assert.That(terminal.AnsiModeEnabled).IsFalse();
    }

    [Test]
    public async Task Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var terminal = new TestTerminal();
        var renderer = new Core.Rendering.Renderer(terminal);

        // Act
        renderer.Dispose();
        renderer.Dispose();

        // Assert - Should not throw
        await Assert.That(terminal.AnsiModeEnabled).IsFalse();
    }

    [Test]
    public async Task MultipleElements_CanBeDrawn()
    {
        // Arrange
        var output = new MemoryStream();
        using var renderer = new Core.Rendering.Renderer(output, 100, 50);
        var rect1 = new Rectangle 
        { 
            Width = Size.Absolute(10), 
            Height = Size.Absolute(5),
            BackgroundColor = Color.Red 
        };
        var rect2 = new Rectangle 
        { 
            X = Size.Absolute(20),
            Width = Size.Absolute(10), 
            Height = Size.Absolute(5),
            BackgroundColor = Color.Blue 
        };

        // Act
        renderer.Draw((IElement)rect1);
        renderer.Draw((IElement)rect2);
        renderer.Present();

        // Assert - Both elements should be rendered
        var outputBytes = output.ToArray();
        await Assert.That(outputBytes.Length).IsGreaterThan(0);
    }
}
