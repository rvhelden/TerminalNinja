namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for TextBlock rendering with inline elements (Run, Span),
/// GetPreferredSize with inlines, XAML loading of inline content,
/// mixed content XAML, wrapping/trimming with inlines, and TextDecorations DP.
/// </summary>
public class TextBlockInlineTests
{
    private CellBuffer _buffer = null!;
    private const int BufferWidth = 40;
    private const int BufferHeight = 10;

    [Before(Test)]
    public Task Setup()
    {
        _buffer = new CellBuffer(BufferWidth, BufferHeight);
        return Task.CompletedTask;
    }

    // ─── HasInlines behavior ────────────────────────────────────────

    [Test]
    public async Task TextBlock_HasInlines_FalseByDefault()
    {
        var tb = new TextBlock { Text = "Hello" };

        // HasInlines is internal, but we can test by checking that Text renders normally
        var buffer = new CellBuffer(20, 5);
        tb.Render(buffer, new Rect(0, 0, 20, 5));

        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('H');
    }

    [Test]
    public async Task TextBlock_Inlines_TakePrecedenceOverText()
    {
        var tb = new TextBlock
        {
            Text = "Should not render",
            Width = Size.Absolute(20),
            Height = Size.Absolute(1)
        };
        tb.Inlines.Add(new Run { Text = "Inline wins" });

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('I');
        await Assert.That(_buffer.GetCell(1, 0).Character).IsEqualTo('n');
        await Assert.That(_buffer.GetCell(7, 0).Character).IsEqualTo('w');
    }

    // ─── Basic inline rendering ─────────────────────────────────────

    [Test]
    public async Task Render_SingleRun_DisplaysText()
    {
        var tb = new TextBlock
        {
            Width = Size.Absolute(20),
            Height = Size.Absolute(1)
        };
        tb.Inlines.Add(new Run { Text = "Hello" });

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('H');
        await Assert.That(_buffer.GetCell(1, 0).Character).IsEqualTo('e');
        await Assert.That(_buffer.GetCell(2, 0).Character).IsEqualTo('l');
        await Assert.That(_buffer.GetCell(3, 0).Character).IsEqualTo('l');
        await Assert.That(_buffer.GetCell(4, 0).Character).IsEqualTo('o');
    }

    [Test]
    public async Task Render_MultipleRuns_DisplaysConcatenated()
    {
        var tb = new TextBlock
        {
            Width = Size.Absolute(20),
            Height = Size.Absolute(1)
        };
        tb.Inlines.Add(new Run { Text = "Hel" });
        tb.Inlines.Add(new Run { Text = "lo" });

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('H');
        await Assert.That(_buffer.GetCell(4, 0).Character).IsEqualTo('o');
    }

    [Test]
    public async Task Render_RunWithExplicitForeground_UsesRunColor()
    {
        var tb = new TextBlock
        {
            Foreground = Color.White,
            Width = Size.Absolute(20),
            Height = Size.Absolute(1)
        };
        tb.Inlines.Add(new Run { Text = "A", Foreground = Color.Red });
        tb.Inlines.Add(new Run { Text = "B" }); // inherits White from TextBlock

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('A');
        await Assert.That(_buffer.GetCell(0, 0).Foreground).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(1, 0).Character).IsEqualTo('B');
        await Assert.That(_buffer.GetCell(1, 0).Foreground).IsEqualTo(Color.White);
    }

    [Test]
    public async Task Render_RunWithExplicitBackground_UsesRunBackground()
    {
        var tb = new TextBlock
        {
            Width = Size.Absolute(20),
            Height = Size.Absolute(1)
        };
        tb.Inlines.Add(new Run { Text = "X", Background = Color.Green });

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 0).Background).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task Render_RunWithTextDecorations_SetsDecorations()
    {
        var tb = new TextBlock
        {
            Width = Size.Absolute(20),
            Height = Size.Absolute(1)
        };
        tb.Inlines.Add(new Run { Text = "B", TextDecorations = TextDecorations.Bold });

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('B');
        await Assert.That(_buffer.GetCell(0, 0).Decorations).IsEqualTo(TextDecorations.Bold);
    }

    [Test]
    public async Task Render_TextBlockDecorations_InheritedByRuns()
    {
        var tb = new TextBlock
        {
            TextDecorations = TextDecorations.Underline,
            Width = Size.Absolute(20),
            Height = Size.Absolute(1)
        };
        tb.Inlines.Add(new Run { Text = "U" });

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 0).Decorations).IsEqualTo(TextDecorations.Underline);
    }

    [Test]
    public async Task Render_RunDecorations_MergeWithTextBlockDecorations()
    {
        var tb = new TextBlock
        {
            TextDecorations = TextDecorations.Bold,
            Width = Size.Absolute(20),
            Height = Size.Absolute(1)
        };
        tb.Inlines.Add(new Run { Text = "X", TextDecorations = TextDecorations.Italic });

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 0).Decorations)
            .IsEqualTo(TextDecorations.Bold | TextDecorations.Italic);
    }

    // ─── Span rendering ─────────────────────────────────────────────

    [Test]
    public async Task Render_SpanWithChildren_DisplaysAllRuns()
    {
        var tb = new TextBlock
        {
            Width = Size.Absolute(20),
            Height = Size.Absolute(1)
        };
        var span = new Span { Foreground = Color.Red };
        span.Inlines.Add(new Run { Text = "AB" });
        span.Inlines.Add(new Run { Text = "CD" });
        tb.Inlines.Add(span);

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('A');
        await Assert.That(_buffer.GetCell(0, 0).Foreground).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(2, 0).Character).IsEqualTo('C');
        await Assert.That(_buffer.GetCell(2, 0).Foreground).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task Render_MixedRunsAndSpans_DisplaysCorrectly()
    {
        var tb = new TextBlock
        {
            Foreground = Color.White,
            Width = Size.Absolute(20),
            Height = Size.Absolute(1)
        };
        tb.Inlines.Add(new Run { Text = "A" });
        var span = new Span { Foreground = Color.Green };
        span.Inlines.Add(new Run { Text = "B" });
        tb.Inlines.Add(span);
        tb.Inlines.Add(new Run { Text = "C" });

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('A');
        await Assert.That(_buffer.GetCell(0, 0).Foreground).IsEqualTo(Color.White);
        await Assert.That(_buffer.GetCell(1, 0).Character).IsEqualTo('B');
        await Assert.That(_buffer.GetCell(1, 0).Foreground).IsEqualTo(Color.Green);
        await Assert.That(_buffer.GetCell(2, 0).Character).IsEqualTo('C');
        await Assert.That(_buffer.GetCell(2, 0).Foreground).IsEqualTo(Color.White);
    }

    // ─── NoWrap trimming with inlines ───────────────────────────────

    [Test]
    public async Task Render_InlinesTruncated_WhenExceedsWidth()
    {
        var tb = new TextBlock
        {
            Width = Size.Absolute(5),
            Height = Size.Absolute(1)
        };
        tb.Inlines.Add(new Run { Text = "Hello World" }); // 11 chars, only 5 fit

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('H');
        await Assert.That(_buffer.GetCell(4, 0).Character).IsEqualTo('o');
        // Character at position 5 should still be empty space (not 'W')
        await Assert.That(_buffer.GetCell(5, 0).Character).IsEqualTo(' ');
    }

    [Test]
    public async Task Render_InlinesWithEllipsis_ShowsEllipsis()
    {
        var tb = new TextBlock
        {
            TextTrimming = TextTrimming.Ellipsis,
            Width = Size.Absolute(8),
            Height = Size.Absolute(1)
        };
        tb.Inlines.Add(new Run { Text = "Hello World" }); // 11 chars > 8

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        // First 5 chars then "..."
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('H');
        await Assert.That(_buffer.GetCell(4, 0).Character).IsEqualTo('o');
        await Assert.That(_buffer.GetCell(5, 0).Character).IsEqualTo('.');
        await Assert.That(_buffer.GetCell(6, 0).Character).IsEqualTo('.');
        await Assert.That(_buffer.GetCell(7, 0).Character).IsEqualTo('.');
    }

    // ─── Alignment with inlines ─────────────────────────────────────

    [Test]
    public async Task Render_InlinesCenterAligned_OffsetsCorrectly()
    {
        var tb = new TextBlock
        {
            HorizontalTextAlignment = TextAlignment.Center,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1)
        };
        tb.Inlines.Add(new Run { Text = "Hi" }); // 2 chars, centered in 10 = offset 4

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(4, 0).Character).IsEqualTo('H');
        await Assert.That(_buffer.GetCell(5, 0).Character).IsEqualTo('i');
    }

    [Test]
    public async Task Render_InlinesEndAligned_RightJustified()
    {
        var tb = new TextBlock
        {
            HorizontalTextAlignment = TextAlignment.End,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1)
        };
        tb.Inlines.Add(new Run { Text = "Hi" }); // 2 chars, end-aligned = offset 8

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(8, 0).Character).IsEqualTo('H');
        await Assert.That(_buffer.GetCell(9, 0).Character).IsEqualTo('i');
    }

    // ─── Wrap mode with inlines ─────────────────────────────────────

    [Test]
    public async Task Render_InlinesWrapped_WrapsAtWidth()
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Width = Size.Absolute(5),
            Height = Size.Absolute(3)
        };
        tb.Inlines.Add(new Run { Text = "ABCDEFGH" }); // 8 chars at width 5 => 2 lines: ABCDE, FGH

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        // First line
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('A');
        await Assert.That(_buffer.GetCell(4, 0).Character).IsEqualTo('E');
        // Second line
        await Assert.That(_buffer.GetCell(0, 1).Character).IsEqualTo('F');
        await Assert.That(_buffer.GetCell(2, 1).Character).IsEqualTo('H');
    }

    [Test]
    public async Task Render_InlinesWrapped_PreservesPerRunStyling()
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Color.White,
            Width = Size.Absolute(3),
            Height = Size.Absolute(3)
        };
        // "AB" in red + "CD" in blue → "ABC" on line 1, "D" on line 2
        tb.Inlines.Add(new Run { Text = "AB", Foreground = Color.Red });
        tb.Inlines.Add(new Run { Text = "CD", Foreground = Color.Blue });

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        // Line 1: A=Red, B=Red, C=Blue
        await Assert.That(_buffer.GetCell(0, 0).Foreground).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(1, 0).Foreground).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(2, 0).Foreground).IsEqualTo(Color.Blue);
        // Line 2: D=Blue
        await Assert.That(_buffer.GetCell(0, 1).Character).IsEqualTo('D');
        await Assert.That(_buffer.GetCell(0, 1).Foreground).IsEqualTo(Color.Blue);
    }

    // ─── GetPreferredSize with inlines ──────────────────────────────

    [Test]
    public async Task GetPreferredSize_WithInlines_ReturnsTotalLength()
    {
        var tb = new TextBlock();
        tb.Inlines.Add(new Run { Text = "Hello" });
        tb.Inlines.Add(new Run { Text = " World" });

        var size = tb.GetPreferredSize(new Rect(0, 0, 100, 50));

        await Assert.That(size.Width).IsEqualTo(11); // "Hello World" = 11
        await Assert.That(size.Height).IsEqualTo(1);
    }

    [Test]
    public async Task GetPreferredSize_WithPadding_IncludesPadding()
    {
        var tb = new TextBlock { Padding = new Thickness(2, 1, 2, 1) };
        tb.Inlines.Add(new Run { Text = "Hi" }); // 2 chars

        var size = tb.GetPreferredSize(new Rect(0, 0, 100, 50));

        await Assert.That(size.Width).IsEqualTo(6); // 2 + 2 + 2 = 6
        await Assert.That(size.Height).IsEqualTo(3); // 1 + 1 + 1 = 3
    }

    [Test]
    public async Task GetPreferredSize_EmptyInlines_ReturnsPaddingOnly()
    {
        var tb = new TextBlock { Padding = new Thickness(1) };
        tb.Inlines.Add(new Run { Text = "" }); // empty

        var size = tb.GetPreferredSize(new Rect(0, 0, 100, 50));

        await Assert.That(size.Width).IsEqualTo(2); // 1 + 1 padding
        await Assert.That(size.Height).IsEqualTo(2); // 1 + 1 padding
    }

    [Test]
    public async Task GetPreferredSize_NoInlines_UsesTextProperty()
    {
        var tb = new TextBlock { Text = "Test" };

        var size = tb.GetPreferredSize(new Rect(0, 0, 100, 50));

        await Assert.That(size.Width).IsEqualTo(4);
        await Assert.That(size.Height).IsEqualTo(1);
    }

    // ─── TextDecorations DP on TextBlock (no inlines) ───────────────

    [Test]
    public async Task TextBlock_TextDecorations_DefaultIsNone()
    {
        var tb = new TextBlock();

        await Assert.That(tb.TextDecorations).IsEqualTo(TextDecorations.None);
    }

    [Test]
    public async Task TextBlock_TextDecorations_RendersWithDecorations()
    {
        var tb = new TextBlock
        {
            Text = "Bold",
            TextDecorations = TextDecorations.Bold,
            Width = Size.Absolute(10),
            Height = Size.Absolute(1)
        };

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('B');
        await Assert.That(_buffer.GetCell(0, 0).Decorations).IsEqualTo(TextDecorations.Bold);
        await Assert.That(_buffer.GetCell(1, 0).Decorations).IsEqualTo(TextDecorations.Bold);
    }

    [Test]
    public async Task TextBlock_TextDecorations_CombinedFlags()
    {
        var tb = new TextBlock
        {
            Text = "X",
            TextDecorations = TextDecorations.Bold | TextDecorations.Underline,
            Width = Size.Absolute(5),
            Height = Size.Absolute(1)
        };

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 0).Decorations)
            .IsEqualTo(TextDecorations.Bold | TextDecorations.Underline);
    }

    // ─── GetLogicalChildren ─────────────────────────────────────────

    [Test]
    public async Task TextBlock_GetLogicalChildren_ReturnsInlines()
    {
        var tb = new TextBlock();
        var run1 = new Run { Text = "A" };
        var run2 = new Run { Text = "B" };
        tb.Inlines.Add(run1);
        tb.Inlines.Add(run2);

        var children = tb.GetLogicalChildren().ToList();

        await Assert.That(children.Count).IsEqualTo(2);
        await Assert.That(children[0]).IsEqualTo(run1);
        await Assert.That(children[1]).IsEqualTo(run2);
    }

    [Test]
    public async Task TextBlock_GetLogicalChildren_NoInlines_ReturnsEmpty()
    {
        var tb = new TextBlock { Text = "Just text" };

        var children = tb.GetLogicalChildren().ToList();

        await Assert.That(children.Count).IsEqualTo(0);
    }

    // ─── XAML loading with inlines ──────────────────────────────────

    [Test]
    public async Task XamlLoad_RunElement_CreatesInline()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml">
                <Run Text="Hello" />
            </TextBlock>
            """;

        var tb = TerminalXaml.Load<TextBlock>(xaml);

        await Assert.That(tb.Inlines.Count).IsEqualTo(1);
        var run = tb.Inlines[0] as Run;
        await Assert.That(run).IsNotNull();
        await Assert.That(run!.Text).IsEqualTo("Hello");
    }

    [Test]
    public async Task XamlLoad_MultipleRuns_CreatesMultipleInlines()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml">
                <Run Text="Hello" />
                <Run Text="World" />
            </TextBlock>
            """;

        var tb = TerminalXaml.Load<TextBlock>(xaml);

        await Assert.That(tb.Inlines.Count).IsEqualTo(2);
        await Assert.That(((Run)tb.Inlines[0]).Text).IsEqualTo("Hello");
        await Assert.That(((Run)tb.Inlines[1]).Text).IsEqualTo("World");
    }

    [Test]
    public async Task XamlLoad_RunWithForeground_SetsColor()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml">
                <Run Text="Red" Foreground="Red" />
            </TextBlock>
            """;

        var tb = TerminalXaml.Load<TextBlock>(xaml);

        var run = (Run)tb.Inlines[0];
        await Assert.That(run.Foreground).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task XamlLoad_RunWithTextDecorations_SetsDecorations()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml">
                <Run Text="Bold" TextDecorations="Bold" />
            </TextBlock>
            """;

        var tb = TerminalXaml.Load<TextBlock>(xaml);

        var run = (Run)tb.Inlines[0];
        await Assert.That(run.TextDecorations).IsEqualTo(TextDecorations.Bold);
    }

    [Test]
    public async Task XamlLoad_SpanWithRuns_CreatesNestedStructure()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml">
                <Span Foreground="Green">
                    <Run Text="Inside Span" />
                </Span>
            </TextBlock>
            """;

        var tb = TerminalXaml.Load<TextBlock>(xaml);

        await Assert.That(tb.Inlines.Count).IsEqualTo(1);
        var span = tb.Inlines[0] as Span;
        await Assert.That(span).IsNotNull();
        await Assert.That(span!.Foreground).IsEqualTo(Color.Green);
        await Assert.That(span.Inlines.Count).IsEqualTo(1);
        await Assert.That(((Run)span.Inlines[0]).Text).IsEqualTo("Inside Span");
    }

    [Test]
    public async Task XamlLoad_MixedContent_TextAndRun()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml">Hello <Run Text="World" /></TextBlock>
            """;

        var tb = TerminalXaml.Load<TextBlock>(xaml);

        // "Hello " is converted to a Run by XamlLoader, "World" is the explicit Run
        await Assert.That(tb.Inlines.Count).IsEqualTo(2);
        await Assert.That(((Run)tb.Inlines[0]).Text).IsEqualTo("Hello ");
        await Assert.That(((Run)tb.Inlines[1]).Text).IsEqualTo("World");
    }

    [Test]
    public async Task XamlLoad_MixedContent_RunAndText()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"><Run Text="Hello" /> World</TextBlock>
            """;

        var tb = TerminalXaml.Load<TextBlock>(xaml);

        await Assert.That(tb.Inlines.Count).IsEqualTo(2);
        await Assert.That(((Run)tb.Inlines[0]).Text).IsEqualTo("Hello");
        await Assert.That(((Run)tb.Inlines[1]).Text).IsEqualTo(" World");
    }

    [Test]
    public async Task XamlLoad_MixedContent_TextBetweenRuns()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"><Run Foreground="Red" Text="A" /> middle <Run Foreground="Blue" Text="B" /></TextBlock>
            """;

        var tb = TerminalXaml.Load<TextBlock>(xaml);

        await Assert.That(tb.Inlines.Count).IsEqualTo(3);
        await Assert.That(((Run)tb.Inlines[0]).Text).IsEqualTo("A");
        await Assert.That(((Run)tb.Inlines[0]).Foreground).IsEqualTo(Color.Red);
        await Assert.That(((Run)tb.Inlines[1]).Text).IsEqualTo(" middle ");
        await Assert.That(((Run)tb.Inlines[2]).Text).IsEqualTo("B");
        await Assert.That(((Run)tb.Inlines[2]).Foreground).IsEqualTo(Color.Blue);
    }

    // ─── End-to-end: XAML load + render ─────────────────────────────

    [Test]
    public async Task XamlLoadAndRender_RunWithForeground_RendersWithColor()
    {
        var xaml = """
            <TextBlock xmlns="http://schemas.terminalninja.dev/xaml"
                       Width="20" Height="1">
                <Run Text="Hi" Foreground="Red" />
            </TextBlock>
            """;

        var tb = TerminalXaml.Load<TextBlock>(xaml);
        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo('H');
        await Assert.That(_buffer.GetCell(0, 0).Foreground).IsEqualTo(Color.Red);
        await Assert.That(_buffer.GetCell(1, 0).Character).IsEqualTo('i');
        await Assert.That(_buffer.GetCell(1, 0).Foreground).IsEqualTo(Color.Red);
    }

    // ─── Empty inlines edge cases ───────────────────────────────────

    [Test]
    public async Task Render_AllEmptyRuns_RendersNothing()
    {
        var tb = new TextBlock
        {
            Width = Size.Absolute(10),
            Height = Size.Absolute(1)
        };
        tb.Inlines.Add(new Run { Text = "" });
        tb.Inlines.Add(new Run { Text = "" });

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        // Buffer should still be default empty cells
        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo(' ');
    }

    [Test]
    public async Task Render_EmptySpan_RendersNothing()
    {
        var tb = new TextBlock
        {
            Width = Size.Absolute(10),
            Height = Size.Absolute(1)
        };
        tb.Inlines.Add(new Span()); // empty span

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 0).Character).IsEqualTo(' ');
    }

    // ─── Vertical alignment with inlines ────────────────────────────

    [Test]
    public async Task Render_InlinesVerticalCenter_OffsetsY()
    {
        var tb = new TextBlock
        {
            VerticalTextAlignment = TextAlignment.Center,
            Width = Size.Absolute(10),
            Height = Size.Absolute(5) // 5 lines, text at line 2 (5/2 = 2)
        };
        tb.Inlines.Add(new Run { Text = "X" });

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 2).Character).IsEqualTo('X');
    }

    [Test]
    public async Task Render_InlinesVerticalEnd_OffsetsToBottom()
    {
        var tb = new TextBlock
        {
            VerticalTextAlignment = TextAlignment.End,
            Width = Size.Absolute(10),
            Height = Size.Absolute(5) // text at line 4
        };
        tb.Inlines.Add(new Run { Text = "X" });

        tb.Render(_buffer, new Rect(0, 0, 40, 10));

        await Assert.That(_buffer.GetCell(0, 4).Character).IsEqualTo('X');
    }
}
