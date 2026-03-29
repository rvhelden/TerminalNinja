namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Comprehensive tests for the ProgressBar control covering:
/// - Determinate rendering (horizontal and vertical)
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
    public async Task Render_ZeroPercent_ShowsOnlyTrackCharacters()
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

        // Assert — all 10 cells should be the track character
        for (var x = 0; x < 10; x++)
        {
            await Assert.That(_buffer.GetCell(x, 0).Character).IsEqualTo(pb.TrackCharacter);
        }
    }

    [Test]
    public async Task Render_HundredPercent_ShowsOnlyBarCharacters()
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

        // Assert — all 10 cells should be the bar character
        for (var x = 0; x < 10; x++)
        {
            await Assert.That(_buffer.GetCell(x, 0).Character).IsEqualTo(pb.BarCharacter);
        }
    }

    [Test]
    public async Task Render_FiftyPercent_FillsHalfWithBarCharacter()
    {
        // Arrange
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

        // Assert — first 5 should be bar character, last 5 should be track character
        for (var x = 0; x < 5; x++)
        {
            await Assert.That(_buffer.GetCell(x, 0).Character).IsEqualTo(pb.BarCharacter);
        }
        for (var x = 5; x < 10; x++)
        {
            await Assert.That(_buffer.GetCell(x, 0).Character).IsEqualTo(pb.TrackCharacter);
        }
    }

    [Test]
    public async Task Render_CustomCharacters_UsesSpecifiedBarAndTrackChars()
    {
        // Arrange
        var pb = new ProgressBar
        {
            Value = 50,
            Minimum = 0,
            Maximum = 100,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1),
            BarCharacter = '#',
            TrackCharacter = '-'
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert
        for (var x = 0; x < 5; x++)
        {
            await Assert.That(_buffer.GetCell(x, 0).Character).IsEqualTo('#');
        }
        for (var x = 5; x < 10; x++)
        {
            await Assert.That(_buffer.GetCell(x, 0).Character).IsEqualTo('-');
        }
    }

    [Test]
    public async Task Render_ForegroundColor_AppliedToFilledPortion()
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

        // Assert — foreground color used for the bar character's foreground
        await Assert.That(_buffer.GetCell(0, 0).Foreground).IsEqualTo(Color.Cyan);
    }

    [Test]
    public async Task Render_TrackForegroundColor_AppliedToTrackPortion()
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

        // Assert — track char uses TrackForeground as its foreground color
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

        // Assert — all 3 rows should have bar characters
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                await Assert.That(_buffer.GetCell(x, y).Character).IsEqualTo(pb.BarCharacter);
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

        // Assert — 50% fill = 5 filled cells
        for (var x = 0; x < 5; x++)
        {
            await Assert.That(_buffer.GetCell(x, 0).Character).IsEqualTo(pb.BarCharacter);
        }
        for (var x = 5; x < 10; x++)
        {
            await Assert.That(_buffer.GetCell(x, 0).Character).IsEqualTo(pb.TrackCharacter);
        }
    }

    #endregion

    // ─── Determinate Vertical Rendering ──────────────────────────────

    #region Determinate Vertical Rendering

    [Test]
    public async Task Render_VerticalZeroPercent_ShowsOnlyTrackCharacters()
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

        // Assert — all 10 rows should be track character
        for (var y = 0; y < 10; y++)
        {
            await Assert.That(_buffer.GetCell(0, y).Character).IsEqualTo(pb.TrackCharacter);
        }
    }

    [Test]
    public async Task Render_VerticalHundredPercent_ShowsOnlyBarCharacters()
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

        // Assert — all 10 rows should be bar character
        for (var y = 0; y < 10; y++)
        {
            await Assert.That(_buffer.GetCell(0, y).Character).IsEqualTo(pb.BarCharacter);
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

        // Assert — top 5 rows are track, bottom 5 rows are bar (fills from bottom)
        for (var y = 0; y < 5; y++)
        {
            await Assert.That(_buffer.GetCell(0, y).Character).IsEqualTo(pb.TrackCharacter);
        }
        for (var y = 5; y < 10; y++)
        {
            await Assert.That(_buffer.GetCell(0, y).Character).IsEqualTo(pb.BarCharacter);
        }
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
            .Select(x => _buffer.GetCell(x, 0).Character).ToArray());
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

        // Assert — should NOT contain any digit characters (only bar/track chars)
        var rendered = new string(Enumerable.Range(0, 20)
            .Select(x => _buffer.GetCell(x, 0).Character).ToArray());
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
            .Select(x => _buffer.GetCell(x, 0).Character).ToArray());
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
            .Select(x => _buffer.GetCell(x, 0).Character).ToArray());
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

        // Assert — there should be at least one bar character and at least one track character
        var hasBar = false;
        var hasTrack = false;
        for (var x = 0; x < 20; x++)
        {
            var ch = _buffer.GetCell(x, 0).Character;
            if (ch == pb.BarCharacter) hasBar = true;
            if (ch == pb.TrackCharacter) hasTrack = true;
        }

        await Assert.That(hasBar).IsTrue();
        await Assert.That(hasTrack).IsTrue();

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

        // Assert — there should be at least one bar character and at least one track character
        var hasBar = false;
        var hasTrack = false;
        for (var y = 0; y < 20; y++)
        {
            var ch = _buffer.GetCell(0, y).Character;
            if (ch == pb.BarCharacter) hasBar = true;
            if (ch == pb.TrackCharacter) hasTrack = true;
        }

        await Assert.That(hasBar).IsTrue();
        await Assert.That(hasTrack).IsTrue();

        // Cleanup
        pb.Dispose();
    }

    [Test]
    public async Task Render_Indeterminate_ShowsAccentGradient()
    {
        // Arrange — 40-wide bar; body = max(1, 40*0.15) = 6, plus 2 accents = 8 total
        var pb = new ProgressBar
        {
            IsIndeterminate = true,
            Width = Size.Absolute(40),
            Height = Size.Absolute(1)
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — the rendered row should contain the accent character (▪) as gradient edges
        var hasAccent = false;
        for (var x = 0; x < 40; x++)
        {
            if (_buffer.GetCell(x, 0).Character == pb.IndeterminateAccentCharacter)
            {
                hasAccent = true;
                break;
            }
        }
        await Assert.That(hasAccent).IsTrue();

        // Cleanup
        pb.Dispose();
    }

    [Test]
    public async Task Render_IndeterminateCustomAccent_UsesCustomChar()
    {
        // Arrange
        var pb = new ProgressBar
        {
            IsIndeterminate = true,
            Width = Size.Absolute(40),
            Height = Size.Absolute(1),
            IndeterminateAccentCharacter = '·'
        };
        var bounds = new Rect(0, 0, BufferWidth, BufferHeight);

        // Act
        pb.Render(_buffer, bounds);

        // Assert — should contain the custom accent character
        var hasAccent = false;
        for (var x = 0; x < 40; x++)
        {
            if (_buffer.GetCell(x, 0).Character == '·')
            {
                hasAccent = true;
                break;
            }
        }
        await Assert.That(hasAccent).IsTrue();

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

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo(pb.BarCharacter);

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

        // When range is zero, fillWidth should be 0; all track characters
        for (var x = 0; x < 10; x++)
        {
            await Assert.That(_buffer.GetCell(x, 0).Character).IsEqualTo(pb.TrackCharacter);
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

        // Assert — only x=75..79 should be rendered (clipped at buffer edge)
        for (var x = 75; x < 80; x++)
        {
            await Assert.That(_buffer.GetCell(x, 0).Character).IsEqualTo(pb.BarCharacter);
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
        await Assert.That(pb.BarCharacter).IsEqualTo('\u2501');  // ━
        await Assert.That(pb.TrackCharacter).IsEqualTo('\u2500'); // ─
        await Assert.That(pb.IndeterminateAccentCharacter).IsEqualTo('\u2578'); // ╸
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

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo(pb.BarCharacter);
    }

    #endregion
}
