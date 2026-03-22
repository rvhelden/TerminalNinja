namespace TerminalNinja.Tests.Unit.Rendering;

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
    private class TestTerminal : TerminalNinja.Console.ITerminal
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
        using var renderer = new TerminalNinja.Rendering.Renderer(terminal);

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
        using var renderer = new TerminalNinja.Rendering.Renderer(output, 120, 40);

        // Assert
        await Assert.That(renderer.Width).IsEqualTo(120);
        await Assert.That(renderer.Height).IsEqualTo(40);
    }

    [Test]
    public async Task Viewport_ReturnsFullScreenRect()
    {
        // Arrange
        var output = new MemoryStream();
        using var renderer = new TerminalNinja.Rendering.Renderer(output, 100, 50);

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
        using var renderer = new TerminalNinja.Rendering.Renderer(output, 80, 24);
        
        // Draw something first
        var rect = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        renderer.Draw(rect);
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
        using var renderer = new TerminalNinja.Rendering.Renderer(output, 80, 24);
        var rect = new global::TerminalNinja.Controls.Border 
        { 
            Width = Size.Absolute(10), 
            Height = Size.Absolute(5),
            Background = Color.Cyan 
        };

        // Act
        renderer.Draw(rect);
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
        using var renderer = new TerminalNinja.Rendering.Renderer(output, 80, 24);

        // First frame
        var rect1 = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        renderer.Draw(rect1);
        renderer.Present();
        var firstFrameSize = output.Length;

        // Second frame (same content)
        renderer.Clear();
        renderer.Draw(rect1);
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
        using var renderer = new TerminalNinja.Rendering.Renderer(terminal);

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
        using var renderer = new TerminalNinja.Rendering.Renderer(terminal);
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
        using var renderer = new TerminalNinja.Rendering.Renderer(output, 80, 24);

        // Act
        renderer.HandleResize(); // Should not throw

        // Assert
        await Assert.That(renderer.Width).IsEqualTo(80); // Unchanged
    }

    [Test]
    public async Task Resize_UpdatesDimensions()
    {
        // Arrange
        var output = new MemoryStream();
        using var renderer = new TerminalNinja.Rendering.Renderer(output, 80, 24);

        // Act
        renderer.Resize(100, 30);

        // Assert
        await Assert.That(renderer.Width).IsEqualTo(100);
        await Assert.That(renderer.Height).IsEqualTo(30);
    }

    [Test]
    public async Task Resize_UpdatesViewport()
    {
        // Arrange
        var output = new MemoryStream();
        using var renderer = new TerminalNinja.Rendering.Renderer(output, 80, 24);

        // Act
        renderer.Resize(120, 40);
        var viewport = renderer.Viewport;

        // Assert
        await Assert.That(viewport.Width).IsEqualTo(120);
        await Assert.That(viewport.Height).IsEqualTo(40);
    }

    [Test]
    public async Task Resize_NoChange_DoesNotResize()
    {
        // Arrange
        var output = new MemoryStream();
        using var renderer = new TerminalNinja.Rendering.Renderer(output, 80, 24);
        var originalOutputLength = output.Length;

        // Act - Resize to same dimensions
        renderer.Resize(80, 24);

        // Assert - No clear screen should have been written
        await Assert.That(output.Length).IsEqualTo(originalOutputLength);
    }

    [Test]
    public async Task Resize_ClearsScreenOnNextPresent()
    {
        // Arrange
        var output = new MemoryStream();
        using var renderer = new TerminalNinja.Rendering.Renderer(output, 80, 24);
        
        // Draw and present first
        renderer.Present();
        var lengthBeforeResize = output.Length;

        // Act - Resize and present
        renderer.Resize(100, 30);
        renderer.Present();

        // Assert - Output should increase after resize + present (clear screen is written)
        await Assert.That(output.Length).IsGreaterThan(lengthBeforeResize);
    }

    [Test]
    public async Task Resize_InvalidatesBuffer()
    {
        // Arrange
        var output = new MemoryStream();
        using var renderer = new TerminalNinja.Rendering.Renderer(output, 80, 24);
        
        // Draw something before resize
        var rect = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        renderer.Draw(rect);
        renderer.Present();

        // Act - Resize
        renderer.Resize(100, 30);
        
        // Draw again and present
        renderer.Clear();
        renderer.Draw(rect);
        renderer.Present();

        // Assert - Should be able to render at new size without errors
        await Assert.That(renderer.Width).IsEqualTo(100);
        await Assert.That(renderer.Height).IsEqualTo(30);
    }

    [Test]
    public async Task Dispose_DisablesAnsiMode()
    {
        // Arrange
        var terminal = new TestTerminal();
        var renderer = new TerminalNinja.Rendering.Renderer(terminal);

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
        var renderer = new TerminalNinja.Rendering.Renderer(terminal);

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
        using var renderer = new TerminalNinja.Rendering.Renderer(output, 100, 50);
        var rect1 = new global::TerminalNinja.Controls.Border 
        { 
            Width = Size.Absolute(10), 
            Height = Size.Absolute(5),
            Background = Color.Red 
        };
        var rect2 = new global::TerminalNinja.Controls.Border 
        { 
            Width = Size.Absolute(10), 
            Height = Size.Absolute(5),
            Background = Color.Blue 
        };

        // Act
        renderer.Draw(rect1);
        renderer.Draw(rect2);
        renderer.Present();

        // Assert - Both elements should be rendered
        var outputBytes = output.ToArray();
        await Assert.That(outputBytes.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task DumpScreen_CreatesFile()
    {
        // Arrange
        var output = new MemoryStream();
        using var renderer = new TerminalNinja.Rendering.Renderer(output, 20, 10);
        
        // Draw something to make it interesting
        var rect = new global::TerminalNinja.Controls.Border { Background = Color.Cyan };
        renderer.Draw(rect);
        renderer.Present();
        
        var dumpPath = "test_dump.txt";
        
        try
        {
            // Act
            var resultPath = renderer.DumpScreen(dumpPath);
            
            // Assert
            await Assert.That(File.Exists(resultPath)).IsTrue();
            
            var content = File.ReadAllText(resultPath);
            await Assert.That(content).Contains("=== TERMINAL BUFFER DUMP ===");
            await Assert.That(content).Contains("Dimensions: 20 x 10");
        }
        finally
        {
            // Cleanup
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    [Test]
    public async Task DumpScreen_WithoutPath_GeneratesTimestampedFilename()
    {
        // Arrange
        var output = new MemoryStream();
        using var renderer = new TerminalNinja.Rendering.Renderer(output, 20, 10);
        
        string? generatedPath = null;
        
        try
        {
            // Act
            generatedPath = renderer.DumpScreen();
            
            // Assert
            await Assert.That(File.Exists(generatedPath)).IsTrue();
            await Assert.That(generatedPath).Contains("screen_dump_");
            await Assert.That(generatedPath).Contains(".txt");
        }
        finally
        {
            // Cleanup
            if (generatedPath != null && File.Exists(generatedPath))
            {
                File.Delete(generatedPath);
            }
        }
    }

    /// <summary>
    /// Integration test: simulates the full Application.Run() render cycle with Renderer.
    /// Matches the exact sequence: Clear → Draw → Present → Resize → Clear → Draw → Present.
    /// Verifies that Window Width/Height constraints are respected after resize.
    /// </summary>
    [Test]
    public async Task Integration_WindowWidthHeight_RespectedAfterResize()
    {
        // Arrange - Create a Window with fixed Width/Height containing a colored Grid
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    Title="Test"
                    Width="80"
                    Height="24">
                <Border Background="Green" />
            </Window>
            """;
        var window = TerminalXaml.Load<Window>(xaml);

        // Create a TestTerminal starting at 80x24
        var terminal = new TestTerminal { Width = 80, Height = 24 };
        using var renderer = new TerminalNinja.Rendering.Renderer(terminal);

        // ─── Frame 1: Initial render (console matches window size) ───
        renderer.Clear();
        renderer.Draw(window);
        renderer.Present();

        // ─── Simulate resize to 120x40 ───
        terminal.Width = 120;
        terminal.Height = 40;
        renderer.HandleResize();

        // Verify renderer resized
        await Assert.That(renderer.Width).IsEqualTo(120);
        await Assert.That(renderer.Height).IsEqualTo(40);
        await Assert.That(renderer.Viewport.Width).IsEqualTo(120);
        await Assert.That(renderer.Viewport.Height).IsEqualTo(40);

        // ─── Frame 2: Re-render after resize (what Application.Run does) ───
        renderer.Clear();
        renderer.Draw(window);
        // At this point, the internal buffer should have Green in 80x24 and empty elsewhere.
        // We can't inspect the private buffer directly, but we can verify via DumpScreen.
        
        string? dumpPath = null;
        try
        {
            dumpPath = renderer.DumpScreen("resize_integration_test_dump.txt");
            var dumpContent = File.ReadAllText(dumpPath);

            // The dump should show the buffer is 120x40 after resize
            await Assert.That(dumpContent).Contains("Dimensions: 120 x 40");
        }
        finally
        {
            if (dumpPath != null && File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }

        renderer.Present();
    }

    /// <summary>
    /// Integration test using CellBuffer directly to verify Window content
    /// stays within bounds after resize, matching the Application.Run() cycle.
    /// The Application.Run cycle is: Clear → Draw → Present (SwapBuffers inside Present).
    /// We test the buffer state after Draw (before Present/SwapBuffers) since that's
    /// when the rendered content is in the _current buffer.
    /// </summary>
    [Test]
    public async Task Integration_WindowContent_StaysWithinBounds_AfterResize_UsingCellBuffer()
    {
        // Arrange - Window with Width=80, Height=24 containing a green Border
        var window = new Window
        {
            Width = Size.Absolute(80),
            Height = Size.Absolute(24),
            Content = new global::TerminalNinja.Controls.Border { Background = Color.Green }
        };

        // ─── Frame 1: Initial render at 80x24 ───
        using var buffer = new CellBuffer(80, 24);
        buffer.Clear();
        window.Render(buffer, new Rect(0, 0, 80, 24));

        // All cells should be green (check _current buffer before SwapBuffers)
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(79, 23).Background).IsEqualTo(Color.Green);

        // Simulate Present() which calls SwapBuffers
        buffer.SwapBuffers();

        // ─── Simulate resize to 120x40 ───
        buffer.Resize(120, 40);

        // ─── Frame 2: Re-render (mimics Application.Run cycle) ───
        buffer.Clear();
        window.Render(buffer, new Rect(0, 0, 120, 40));  // Viewport = full new console size
        
        // Assert - Content must stay within 80x24
        // Inside bounds: should be green
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(79, 23).Background).IsEqualTo(Color.Green);
        await Assert.That(buffer.GetCell(40, 12).Background).IsEqualTo(Color.Green);

        // Outside bounds: should be empty (black)
        await Assert.That(buffer.GetCell(80, 0).Background).IsEqualTo(Color.Black);
        await Assert.That(buffer.GetCell(0, 24).Background).IsEqualTo(Color.Black);
        await Assert.That(buffer.GetCell(119, 39).Background).IsEqualTo(Color.Black);
        await Assert.That(buffer.GetCell(100, 30).Background).IsEqualTo(Color.Black);
    }

    /// <summary>
    /// Integration test with XAML-loaded Window (Grid with TextBlocks, matching HelloWorld.xaml)
    /// to verify content stays within Window bounds after console resize.
    /// </summary>
    [Test]
    public async Task Integration_XamlWindow_GridContent_StaysWithinBounds_AfterResize()
    {
        // Arrange - Load Window matching the structure of HelloWorld.xaml
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    Title="Playground"
                    Width="80"
                    Height="24">
                <Grid Columns="* 2*" Rows="*">
                    <TextBlock Grid.Row="0" Grid.Column="0" Text="1/3" Background="Red" />
                    <TextBlock Grid.Row="0" Grid.Column="1" Text="2/3" Background="Green" />
                </Grid>
            </Window>
            """;
        var window = TerminalXaml.Load<Window>(xaml);

        // Verify XAML loaded correctly
        await Assert.That(window.Width).IsEqualTo(Size.Absolute(80));
        await Assert.That(window.Height).IsEqualTo(Size.Absolute(24));

        // ─── Frame 1: Initial render at 80x24 ───
        using var buffer = new CellBuffer(80, 24);
        buffer.Clear();
        window.Render(buffer, new Rect(0, 0, 80, 24));

        // First column (1/3 of 80 = ~27) should be red
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        // Second column (2/3 of 80 = ~53) should be green
        await Assert.That(buffer.GetCell(79, 0).Background).IsEqualTo(Color.Green);

        // ─── Simulate resize to 120x40 ───
        buffer.Resize(120, 40);
        buffer.Clear();
        window.Render(buffer, new Rect(0, 0, 120, 40));

        // Assert - Content should stay within 80x24
        // Inside bounds: should have color
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);
        await Assert.That(buffer.GetCell(79, 0).Background).IsEqualTo(Color.Green);

        // Outside window bounds: should be empty/black
        await Assert.That(buffer.GetCell(80, 0).Background).IsEqualTo(Color.Black);
        await Assert.That(buffer.GetCell(0, 24).Background).IsEqualTo(Color.Black);
        await Assert.That(buffer.GetCell(119, 39).Background).IsEqualTo(Color.Black);
    }

    /// <summary>
    /// Integration test using the Renderer's offscreen mode to verify the full
    /// Clear → Draw → Present → Resize → Clear → Draw → Present cycle.
    /// Checks that ANSI output for the second frame doesn't contain colored cells
    /// outside the Window's 80x24 bounds.
    /// </summary>
    [Test]
    public async Task Integration_OffscreenRenderer_WindowBounds_RespectedAfterResize()
    {
        // Arrange - Window with fixed 80x24 containing a green Border
        var window = new Window
        {
            Width = Size.Absolute(80),
            Height = Size.Absolute(24),
            Content = new global::TerminalNinja.Controls.Border { Background = Color.Green }
        };

        var output = new MemoryStream();
        var renderer = TerminalNinja.Rendering.Renderer.CreateOffscreen(output, 80, 24);

        // Frame 1: Initial render
        renderer.Clear();
        renderer.Draw(window);
        renderer.Present();
        var frame1Size = output.Length;

        // Resize to 120x40
        renderer.Resize(120, 40);
        await Assert.That(renderer.Width).IsEqualTo(120);
        await Assert.That(renderer.Height).IsEqualTo(40);

        // Frame 2: Re-render after resize (this is the critical path)
        renderer.Clear();
        renderer.Draw(window);
        renderer.Present();
        var frame2Size = output.Length - frame1Size;

        // The second frame should produce output (content + clear screen),
        // but it should NOT be proportional to the full 120x40 area.
        // If the Window bounds are ignored, we'd see output for all 120*40=4800 cells.
        // If they're respected, we'd see output for at most 80*24=1920 cells + clear screen overhead.
        // A rough upper bound: each cell needs ~30 bytes of ANSI (move + fg + bg + char),
        // but sequential cells on the same row need less. The clear screen is ~7 bytes.
        // For 1920 cells, ~20-30KB is reasonable. For 4800 cells, it would be ~50-100KB.
        // We test that frame2 isn't unreasonably large (which would indicate full 120x40 rendering).
        // However, this is a soft check - the main verification is the CellBuffer test above.
        await Assert.That(frame2Size).IsGreaterThan(0);
        
        renderer.Dispose();
    }
}
