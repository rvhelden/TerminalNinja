namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Comprehensive tests for the ProgressBar control covering:
/// - Determinate rendering (horizontal and vertical) with half-block precision and gradient
/// - Value coercion (clamping to [Minimum, Maximum])
/// - Indeterminate mode (sliding block animation)
/// - ShowPercentage text overlay
/// - Alignment within parent bounds
/// - DependencyProperty invalidation
/// - Edge cases (zero-width, Max==Min, etc.)
/// </summary>
public class ProgressBarTests
{
    private CellBuffer _buffer = null!;
    private const int BufferWidth = 80;
    private const int BufferHeight = 24;

    private const char FullBlock = '\u2588';
    private const char LeftHalfBlock = '\u258C';
    private const char LowerHalfBlock = '\u2584';
    private const char TrackDot = '\u00B7';

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

    // ─── Determinate Horizontal Rendering ────────────────────────────

    #region Determinate Horizontal Rendering

    [Test]
    public async Task Render_ZeroPercent_ShowsOnlyTrackDots()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 0,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — all 10 cells should be track dots
        for (var x = 0; x < 10; x++)
        {
            var cell = _buffer.GetCell(x, 0);
            await Assert.That(cell.Codepoint).IsEqualTo(TrackDot);
            await Assert.That(cell.Foreground).IsEqualTo(pb.TrackForeground);
        }
    }

    [Test]
    public async Task Render_HundredPercent_ShowsOnlyFilledBlocks()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 100,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — all cells are full blocks with fg == bg (solid), first cell is exact Foreground
        await Assert.That(_buffer.GetCell(0, 0).Foreground).IsEqualTo(pb.Foreground);
        for (var x = 0; x < 10; x++)
        {
            var cell = _buffer.GetCell(x, 0);
            await Assert.That(cell.Codepoint).IsEqualTo(FullBlock);
            await Assert.That(cell.Foreground).IsEqualTo(cell.Background);
        }
    }

    [Test]
    public async Task Render_FiftyPercent_FillsHalfWithBlocksHalfWithDots()
    {
        // Arrange — 50% of 10 cells = 10 half-cells = 5 full cells, no boundary
        var pb = new ProgressBar
        {
            Value = 50,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — first 5 filled blocks, last 5 track dots
        for (var x = 0; x < 5; x++)
        {
            var cell = _buffer.GetCell(x, 0);
            await Assert.That(cell.Codepoint).IsEqualTo(FullBlock);
            await Assert.That(cell.Foreground).IsEqualTo(cell.Background);
        }
        for (var x = 5; x < 10; x++)
        {
            await Assert.That((char)_buffer.GetCell(x, 0).Codepoint).IsEqualTo(TrackDot);
        }
    }

    [Test]
    public async Task Render_SubCellPrecision_ShowsBoundaryHalfBlock()
    {
        // Arrange — 25% of 10 cells = 5 half-cells = 2 full + 1 boundary
        var pb = new ProgressBar
        {
            Value = 25,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — cells 0-1 filled blocks, cell 2 boundary half-block, cells 3-9 track dots
        for (var x = 0; x < 2; x++)
        {
            var cell = _buffer.GetCell(x, 0);
            await Assert.That(cell.Codepoint).IsEqualTo(FullBlock);
            await Assert.That(cell.Foreground).IsEqualTo(cell.Background);
        }

        var boundary = _buffer.GetCell(2, 0);
        await Assert.That(boundary.Codepoint).IsEqualTo(LeftHalfBlock);

        for (var x = 3; x < 10; x++)
        {
            await Assert.That((char)_buffer.GetCell(x, 0).Codepoint).IsEqualTo(TrackDot);
        }
    }

    [Test]
    public async Task Render_ExactCellBoundary_NoBoundaryChar()
    {
        // Arrange — 50% of 10 cells = 10 half-cells = 5 full cells, no half-block needed
        var pb = new ProgressBar
        {
            Value = 50,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — no half-block character; filled are full blocks, track are dots
        for (var x = 0; x < 5; x++)
        {
            await Assert.That((char)_buffer.GetCell(x, 0).Codepoint).IsEqualTo(FullBlock);
        }
        for (var x = 5; x < 10; x++)
        {
            await Assert.That((char)_buffer.GetCell(x, 0).Codepoint).IsEqualTo(TrackDot);
        }
    }

    [Test]
    public async Task Render_Gradient_FirstCellIsForegroundLastCellIsLighter()
    {
        // Arrange — 100% fill on a 10-cell bar
        var pb = new ProgressBar
        {
            Value = 100,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1),
            Foreground = new Color(86, 156, 214)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — first cell is exact Foreground, last cell is lighter (gradient)
        var firstCell = _buffer.GetCell(0, 0);
        var lastCell = _buffer.GetCell(9, 0);
        await Assert.That(firstCell.Foreground).IsEqualTo(pb.Foreground);

        // Last cell should be brighter (each channel closer to 255)
        await Assert.That(lastCell.Foreground.R).IsGreaterThan(firstCell.Foreground.R);
        await Assert.That(lastCell.Foreground.G).IsGreaterThan(firstCell.Foreground.G);
        await Assert.That(lastCell.Foreground.B).IsGreaterThan(firstCell.Foreground.B);
    }

    [Test]
    public async Task Render_FilledCells_HaveSolidBlockAppearance()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 100,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(5),
            Height = Size.Absolute(1),
            Foreground = Color.Cyan
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — all filled cells have fg == bg (solid block appearance)
        for (var x = 0; x < 5; x++)
        {
            var cell = _buffer.GetCell(x, 0);
            await Assert.That(cell.Codepoint).IsEqualTo(FullBlock);
            await Assert.That(cell.Foreground).IsEqualTo(cell.Background);
        }
        // First cell should be exact Foreground
        await Assert.That(_buffer.GetCell(0, 0).Foreground).IsEqualTo(Color.Cyan);
    }

    [Test]
    public async Task Render_TrackCells_ShowDotsWithTrackForeground()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 0,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(5),
            Height = Size.Absolute(1),
            TrackForeground = Color.Red
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — track cells show dots with TrackForeground color
        for (var x = 0; x < 5; x++)
        {
            var cell = _buffer.GetCell(x, 0);
            await Assert.That(cell.Codepoint).IsEqualTo(TrackDot);
            await Assert.That(cell.Foreground).IsEqualTo(Color.Red);
        }
    }

    [Test]
    public async Task Render_ForegroundColor_AppliedToFirstFilledCell()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 100,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1),
            Foreground = Color.Cyan
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — first cell (gradient start) is exact Foreground
        await Assert.That(_buffer.GetCell(0, 0).Foreground).IsEqualTo(Color.Cyan);
    }

    [Test]
    public async Task Render_TrackForegroundColor_AppliedToTrackDots()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 0,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1),
            TrackForeground = Color.Red
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert
        await Assert.That(_buffer.GetCell(0, 0).Foreground).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task Render_MultiRowBar_FillsAllRows()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 100,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(10),
            Height = Size.Absolute(3)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — all 3 rows should have filled blocks with solid appearance
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                var cell = _buffer.GetCell(x, y);
                await Assert.That(cell.Codepoint).IsEqualTo(FullBlock);
                await Assert.That(cell.Foreground).IsEqualTo(cell.Background);
            }
        }
    }

    [Test]
    public async Task Render_CustomRange_CalculatesCorrectFill()
    {
        // Arrange — range 10-20, value 15 = 50%
        var pb = new ProgressBar
        {
            Value = 15,
            Minimum = 10,
            Maximum = 20,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — 50% fill = 5 filled blocks, 5 track dots
        for (var x = 0; x < 5; x++)
        {
            await Assert.That((char)_buffer.GetCell(x, 0).Codepoint).IsEqualTo(FullBlock);
        }
        for (var x = 5; x < 10; x++)
        {
            await Assert.That((char)_buffer.GetCell(x, 0).Codepoint).IsEqualTo(TrackDot);
        }
    }

    #endregion

    // ─── Determinate Vertical Rendering ──────────────────────────────

    #region Determinate Vertical Rendering

    [Test]
    public async Task Render_VerticalZeroPercent_ShowsOnlyTrackDots()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 0,
            Minimum = 0,
            Maximum = 100,
            Orientation = Orientation.Vertical,
            Width = Size.Absolute(1),
            Height = Size.Absolute(10)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — all 10 rows should be track dots
        for (var y = 0; y < 10; y++)
        {
            var cell = _buffer.GetCell(0, y);
            await Assert.That(cell.Codepoint).IsEqualTo(TrackDot);
            await Assert.That(cell.Foreground).IsEqualTo(pb.TrackForeground);
        }
    }

    [Test]
    public async Task Render_VerticalHundredPercent_ShowsOnlyFilledBlocks()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 100,
            Minimum = 0,
            Maximum = 100,
            Orientation = Orientation.Vertical,
            Width = Size.Absolute(1),
            Height = Size.Absolute(10)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — all 10 rows should be filled blocks with solid appearance
        for (var y = 0; y < 10; y++)
        {
            var cell = _buffer.GetCell(0, y);
            await Assert.That(cell.Codepoint).IsEqualTo(FullBlock);
            await Assert.That(cell.Foreground).IsEqualTo(cell.Background);
        }
    }

    [Test]
    public async Task Render_VerticalFiftyPercent_FillsBottomHalf()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 50,
            Minimum = 0,
            Maximum = 100,
            Orientation = Orientation.Vertical,
            Width = Size.Absolute(1),
            Height = Size.Absolute(10)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — top 5 rows are track dots, bottom 5 rows are filled blocks
        for (var y = 0; y < 5; y++)
        {
            await Assert.That((char)_buffer.GetCell(0, y).Codepoint).IsEqualTo(TrackDot);
        }
        for (var y = 5; y < 10; y++)
        {
            var cell = _buffer.GetCell(0, y);
            await Assert.That(cell.Codepoint).IsEqualTo(FullBlock);
            await Assert.That(cell.Foreground).IsEqualTo(cell.Background);
        }
    }

    [Test]
    public async Task Render_VerticalSubCellPrecision_ShowsBoundaryHalfBlock()
    {
        // Arrange — 25% of 10 rows = 5 half-cells = 2 full + 1 boundary
        var pb = new ProgressBar
        {
            Value = 25,
            Minimum = 0,
            Maximum = 100,
            Orientation = Orientation.Vertical,
            Width = Size.Absolute(1),
            Height = Size.Absolute(10)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — top 7 rows track dots, row 7 boundary, bottom 2 filled
        for (var y = 0; y < 7; y++)
        {
            await Assert.That((char)_buffer.GetCell(0, y).Codepoint).IsEqualTo(TrackDot);
        }

        var boundary = _buffer.GetCell(0, 7);
        await Assert.That(boundary.Codepoint).IsEqualTo(LowerHalfBlock);

        for (var y = 8; y < 10; y++)
        {
            var cell = _buffer.GetCell(0, y);
            await Assert.That(cell.Codepoint).IsEqualTo(FullBlock);
            await Assert.That(cell.Foreground).IsEqualTo(cell.Background);
        }
    }

    [Test]
    public async Task Render_VerticalGradient_BottomIsForegroundTopIsLighter()
    {
        // Arrange — 100% fill on a 10-row bar
        var pb = new ProgressBar
        {
            Value = 100,
            Minimum = 0,
            Maximum = 100,
            Orientation = Orientation.Vertical,
            Width = Size.Absolute(1),
            Height = Size.Absolute(10),
            Foreground = new Color(86, 156, 214)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — bottom cell (y=9) is Foreground, top cell (y=0) is lighter
        var bottomCell = _buffer.GetCell(0, 9);
        var topCell = _buffer.GetCell(0, 0);
        await Assert.That(bottomCell.Foreground).IsEqualTo(pb.Foreground);
        await Assert.That(topCell.Foreground.R).IsGreaterThan(bottomCell.Foreground.R);
    }

    #endregion

    // ─── Value Coercion ──────────────────────────────────────────────

    #region Value Coercion

    [Test]
    public async Task Value_BelowMinimum_ClampedToMinimum()
    {
        // Arrange
        var pb = new ProgressBar { Minimum = 10, Maximum = 100 };

        // Act
        pb.Value = 5;

        // Assert
        await Assert.That(pb.Value).IsEqualTo(10.0);
    }

    [Test]
    public async Task Value_AboveMaximum_ClampedToMaximum()
    {
        // Arrange
        var pb = new ProgressBar { Minimum = 0, Maximum = 100 };

        // Act
        pb.Value = 150;

        // Assert
        await Assert.That(pb.Value).IsEqualTo(100.0);
    }

    [Test]
    public async Task Value_WithinRange_NotCoerced()
    {
        // Arrange
        var pb = new ProgressBar { Minimum = 0, Maximum = 100 };

        // Act
        pb.Value = 42;

        // Assert
        await Assert.That(pb.Value).IsEqualTo(42.0);
    }

    [Test]
    public async Task Value_MinimumRaised_ValueClamped()
    {
        // Arrange
        var pb = new ProgressBar { Minimum = 0, Maximum = 100, Value = 20 };

        // Act — raise minimum above current value
        pb.Minimum = 50;

        // Assert
        await Assert.That(pb.Value).IsEqualTo(50.0);
    }

    [Test]
    public async Task Value_MaximumLowered_ValueClamped()
    {
        // Arrange
        var pb = new ProgressBar { Minimum = 0, Maximum = 100, Value = 80 };

        // Act — lower maximum below current value
        pb.Maximum = 50;

        // Assert
        await Assert.That(pb.Value).IsEqualTo(50.0);
    }

    [Test]
    public async Task Value_SetToMinimum_ReturnsMinimum()
    {
        // Arrange & Act
        var pb = new ProgressBar { Minimum = 10, Maximum = 90, Value = 10 };

        // Assert
        await Assert.That(pb.Value).IsEqualTo(10.0);
    }

    [Test]
    public async Task Value_SetToMaximum_ReturnsMaximum()
    {
        // Arrange & Act
        var pb = new ProgressBar { Minimum = 10, Maximum = 90, Value = 90 };

        // Assert
        await Assert.That(pb.Value).IsEqualTo(90.0);
    }

    #endregion

    // ─── ShowPercentage ──────────────────────────────────────────────

    #region ShowPercentage

    [Test]
    public async Task Render_ShowPercentageTrue_DisplaysPercentText()
    {
        // Arrange — 50% on a 20-wide bar; text "50%" (3 chars) centered
        var pb = new ProgressBar
        {
            Value = 50,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(20),
            Height = Size.Absolute(1),
            ShowPercentage = true
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — find "50%" somewhere in the rendered row
        var rendered = new string(Enumerable.Range(0, 20)
            .Select(x => (char)_buffer.GetCell(x, 0).Codepoint).ToArray());
        await Assert.That(rendered).Contains("50%");
    }

    [Test]
    public async Task Render_ShowPercentageFalse_NoPercentText()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 50,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(20),
            Height = Size.Absolute(1),
            ShowPercentage = false
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — should NOT contain any percentage text
        var rendered = new string(Enumerable.Range(0, 20)
            .Select(x => (char)_buffer.GetCell(x, 0).Codepoint).ToArray());
        await Assert.That(rendered).DoesNotContain("50%");
    }

    [Test]
    public async Task Render_ShowPercentageZero_DisplaysZeroPercent()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 0,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(20),
            Height = Size.Absolute(1),
            ShowPercentage = true
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert
        var rendered = new string(Enumerable.Range(0, 20)
            .Select(x => (char)_buffer.GetCell(x, 0).Codepoint).ToArray());
        await Assert.That(rendered).Contains("0%");
    }

    [Test]
    public async Task Render_ShowPercentageHundred_DisplaysHundredPercent()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 100,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(20),
            Height = Size.Absolute(1),
            ShowPercentage = true
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert
        var rendered = new string(Enumerable.Range(0, 20)
            .Select(x => (char)_buffer.GetCell(x, 0).Codepoint).ToArray());
        await Assert.That(rendered).Contains("100%");
    }

    #endregion

    // ─── Indeterminate Mode ──────────────────────────────────────────

    #region Indeterminate Mode

    [Test]
    public async Task Render_Indeterminate_ShowsSlidingBlock()
    {
        // Arrange
        var pb = new ProgressBar
        {
            IsIndeterminate = true,
            Width = Size.Absolute(20),
            Height = Size.Absolute(1)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — there should be filled block cells and track dot cells
        var hasBlock = false;
        var hasDot = false;
        for (var x = 0; x < 20; x++)
        {
            var cell = _buffer.GetCell(x, 0);
            if (cell.Codepoint == FullBlock && cell.Foreground != pb.TrackForeground) hasBlock = true;
            if (cell.Codepoint == TrackDot) hasDot = true;
        }

        await Assert.That(hasBlock).IsTrue();
        await Assert.That(hasDot).IsTrue();

        // Cleanup
        pb.Dispose();
    }

    [Test]
    public async Task Render_IndeterminateVertical_ShowsSlidingBlock()
    {
        // Arrange
        var pb = new ProgressBar
        {
            IsIndeterminate = true,
            Orientation = Orientation.Vertical,
            Width = Size.Absolute(1),
            Height = Size.Absolute(20)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — there should be filled block cells and track dot cells
        var hasBlock = false;
        var hasDot = false;
        for (var y = 0; y < 20; y++)
        {
            var cell = _buffer.GetCell(0, y);
            if (cell.Codepoint == FullBlock && cell.Foreground != pb.TrackForeground) hasBlock = true;
            if (cell.Codepoint == TrackDot) hasDot = true;
        }

        await Assert.That(hasBlock).IsTrue();
        await Assert.That(hasDot).IsTrue();

        // Cleanup
        pb.Dispose();
    }

    [Test]
    public async Task IsIndeterminate_SetFalse_StopsAnimation()
    {
        // Arrange
        var pb = new ProgressBar { IsIndeterminate = true };

        // Act
        pb.IsIndeterminate = false;

        // Assert — should not throw and Value rendering should work
        pb.Value = 50;
        pb.Width = Size.Absolute(10);
        pb.Height = Size.Absolute(1);
        pb.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        var cell = _buffer.GetCell(0, 0);
        await Assert.That(cell.Codepoint).IsEqualTo(FullBlock);
        await Assert.That(cell.Foreground).IsEqualTo(pb.Foreground);

        // Cleanup
        pb.Dispose();
    }

    #endregion

    // ─── Layout and Alignment ────────────────────────────────────────

    #region Layout and Alignment

    [Test]
    public async Task GetPreferredSize_Horizontal_ReturnsCorrectDefaults()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Width = Size.Absolute(20),
            Height = Size.Absolute(1)
        };
        var parent = new Rect(0, 0, 80, 24);

        // Act
        var size = pb.GetPreferredSize(parent);

        // Assert
        await Assert.That(size.Width).IsEqualTo(20);
        await Assert.That(size.Height).IsEqualTo(1);
    }

    [Test]
    public async Task GetPreferredSize_Vertical_ReturnsCorrectDefaults()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Orientation = Orientation.Vertical,
            Width = Size.Absolute(1),
            Height = Size.Absolute(10)
        };
        var parent = new Rect(0, 0, 80, 24);

        // Act
        var size = pb.GetPreferredSize(parent);

        // Assert
        await Assert.That(size.Width).IsEqualTo(1);
        await Assert.That(size.Height).IsEqualTo(10);
    }

    [Test]
    public async Task GetPreferredSize_Stretch_UsesParentWidth()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Width = Size.Stretch,
            Height = Size.Absolute(1)
        };
        var parent = new Rect(0, 0, 60, 24);

        // Act
        var size = pb.GetPreferredSize(parent);

        // Assert
        await Assert.That(size.Width).IsEqualTo(60);
    }

    [Test]
    public async Task CalculateBounds_CenterAlignment_CentersInParent()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Width = Size.Absolute(20),
            Height = Size.Absolute(1),
            HorizontalAlignment = Alignment.Center,
            VerticalAlignment = Alignment.Center
        };
        var parent = new Rect(0, 0, 80, 24);

        // Act
        var bounds = pb.CalculateBounds(parent);

        // Assert — centered in 80-wide parent: (80-20)/2 = 30
        await Assert.That(bounds.X).IsEqualTo(30);
        // Centered vertically in 24: (24-1)/2 = 11
        await Assert.That(bounds.Y).IsEqualTo(11);
        await Assert.That(bounds.Width).IsEqualTo(20);
        await Assert.That(bounds.Height).IsEqualTo(1);
    }

    [Test]
    public async Task CalculateBounds_EndAlignment_PositionsAtEnd()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Width = Size.Absolute(20),
            Height = Size.Absolute(1),
            HorizontalAlignment = Alignment.End
        };
        var parent = new Rect(0, 0, 80, 24);

        // Act
        var bounds = pb.CalculateBounds(parent);

        // Assert — end of 80-wide parent: 80-20 = 60
        await Assert.That(bounds.X).IsEqualTo(60);
    }

    #endregion

    // ─── DependencyProperty Invalidation ─────────────────────────────

    #region DependencyProperty Invalidation

    [Test]
    public async Task Value_Changed_TriggersInvalidation()
    {
        // Arrange
        var pb = new ProgressBar();
        var invalidationCount = 0;
        pb.InvalidationCallback = () => invalidationCount++;

        // Act
        pb.Value = 42;

        // Assert
        await Assert.That(invalidationCount).IsEqualTo(1);
    }

    [Test]
    public async Task Value_SameValue_NoInvalidation()
    {
        // Arrange — Value defaults to 0.0
        var pb = new ProgressBar();
        var invalidationCount = 0;
        pb.InvalidationCallback = () => invalidationCount++;

        // Act — set to same value
        pb.Value = 0;

        // Assert — no change, no invalidation
        await Assert.That(invalidationCount).IsEqualTo(0);
    }

    [Test]
    public async Task Foreground_Changed_TriggersInvalidation()
    {
        // Arrange
        var pb = new ProgressBar();
        var invalidationCount = 0;
        pb.InvalidationCallback = () => invalidationCount++;

        // Act
        pb.Foreground = Color.Red;

        // Assert
        await Assert.That(invalidationCount).IsEqualTo(1);
    }

    [Test]
    public async Task TrackForeground_Changed_TriggersInvalidation()
    {
        // Arrange
        var pb = new ProgressBar();
        var invalidationCount = 0;
        pb.InvalidationCallback = () => invalidationCount++;

        // Act
        pb.TrackForeground = Color.Blue;

        // Assert
        await Assert.That(invalidationCount).IsEqualTo(1);
    }

    [Test]
    public async Task Orientation_Changed_TriggersInvalidation()
    {
        // Arrange
        var pb = new ProgressBar();
        var invalidationCount = 0;
        pb.InvalidationCallback = () => invalidationCount++;

        // Act
        pb.Orientation = Orientation.Vertical;

        // Assert
        await Assert.That(invalidationCount).IsEqualTo(1);
    }

    #endregion

    // ─── Edge Cases ──────────────────────────────────────────────────

    #region Edge Cases

    [Test]
    public async Task Render_ZeroWidthBounds_NoException()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 50,
            Width = Size.Absolute(0),
            Height = Size.Absolute(1)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act & Assert — should not throw
        await Assert.That(() => pb.Render(_buffer, bounds)).ThrowsNothing();
    }

    [Test]
    public async Task Render_ZeroHeightBounds_NoException()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 50,
            Width = Size.Absolute(10),
            Height = Size.Absolute(0)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act & Assert — should not throw
        await Assert.That(() => pb.Render(_buffer, bounds)).ThrowsNothing();
    }

    [Test]
    public async Task Render_MaximumEqualsMinimum_NoException()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Minimum = 50,
            Maximum = 50,
            Value = 50,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act & Assert — should not throw
        pb.Render(_buffer, bounds);

        // When range is zero, fill should be 0; all track dots
        for (var x = 0; x < 10; x++)
        {
            var cell = _buffer.GetCell(x, 0);
            await Assert.That(cell.Codepoint).IsEqualTo(TrackDot);
            await Assert.That(cell.Foreground).IsEqualTo(pb.TrackForeground);
        }
    }

    [Test]
    public async Task Render_BoundsOutsideBuffer_NoException()
    {
        // Arrange — bounds completely outside buffer area
        var pb = new ProgressBar
        {
            Value = 50,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1)
        };
        var bounds = new Rect(100, 100, 10, 1);

        // Act & Assert — should not throw
        await Assert.That(() => pb.Render(_buffer, bounds)).ThrowsNothing();
    }

    [Test]
    public async Task Render_PartiallyOutsideBuffer_ClipsCorrectly()
    {
        // Arrange — bar starts at x=75, extends past 80-wide buffer
        var pb = new ProgressBar
        {
            Value = 100,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1),
            HorizontalAlignment = Alignment.Start
        };
        var bounds = new Rect(75, 0, 10, 1);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — only x=75..79 should be rendered (clipped at buffer edge), all filled blocks
        for (var x = 75; x < 80; x++)
        {
            var cell = _buffer.GetCell(x, 0);
            await Assert.That(cell.Codepoint).IsEqualTo(FullBlock);
            await Assert.That(cell.Foreground).IsEqualTo(cell.Background);
        }
    }

    #endregion

    // ─── Default Property Values ─────────────────────────────────────

    #region Default Property Values

    [Test]
    public async Task DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var pb = new ProgressBar();

        // Assert
        await Assert.That(pb.Minimum).IsEqualTo(0.0);
        await Assert.That(pb.Maximum).IsEqualTo(100.0);
        await Assert.That(pb.Value).IsEqualTo(0.0);
        await Assert.That(pb.IsIndeterminate).IsFalse();
        await Assert.That(pb.Orientation).IsEqualTo(Orientation.Horizontal);
        await Assert.That(pb.Foreground).IsEqualTo(new Color(86, 156, 214));
        await Assert.That(pb.Background).IsEqualTo(Color.Transparent);
        await Assert.That(pb.TrackForeground).IsEqualTo(new Color(60, 60, 60));
        await Assert.That(pb.ShowPercentage).IsFalse();
    }

    #endregion

    // ─── Dispose ─────────────────────────────────────────────────────

    #region Dispose

    [Test]
    public async Task Dispose_WithoutAnimation_DoesNotThrow()
    {
        // Arrange
        var pb = new ProgressBar();

        // Act & Assert
        await Assert.That(() => pb.Dispose()).ThrowsNothing();
    }

    [Test]
    public async Task Dispose_WithAnimation_StopsTimer()
    {
        // Arrange
        var pb = new ProgressBar { IsIndeterminate = true };

        // Act
        pb.Dispose();

        // Assert — after dispose, rendering should still work (just no animation)
        pb.IsIndeterminate = false;
        pb.Value = 50;
        pb.Width = Size.Absolute(10);
        pb.Height = Size.Absolute(1);
        pb.Render(_buffer, new Rect(0, 0, BufferWidth, BufferHeight));

        var cell = _buffer.GetCell(0, 0);
        await Assert.That(cell.Codepoint).IsEqualTo(FullBlock);
        await Assert.That(cell.Foreground).IsEqualTo(pb.Foreground);
    }

    #endregion
}
