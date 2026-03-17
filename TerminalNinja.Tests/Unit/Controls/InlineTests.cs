namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for the inline text model: Run, Span, InlineCollection, InlineRun.
/// Covers CollectRuns with style inheritance, InlineCollection parent management,
/// and the InlineRun record struct.
/// </summary>
public class InlineTests
{
    // ─── InlineRun record struct ────────────────────────────────────

    [Test]
    public async Task InlineRun_RecordStruct_StoresAllProperties()
    {
        var run = new InlineRun("Hello", Color.Red, Color.Blue, TextDecorations.Bold);

        await Assert.That(run.Text).IsEqualTo("Hello");
        await Assert.That(run.Foreground).IsEqualTo(Color.Red);
        await Assert.That(run.Background).IsEqualTo(Color.Blue);
        await Assert.That(run.Decorations).IsEqualTo(TextDecorations.Bold);
    }

    [Test]
    public async Task InlineRun_Equality_SameValues_AreEqual()
    {
        var a = new InlineRun("Hi", Color.White, Color.Black, TextDecorations.None);
        var b = new InlineRun("Hi", Color.White, Color.Black, TextDecorations.None);

        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task InlineRun_Equality_DifferentText_AreNotEqual()
    {
        var a = new InlineRun("Hi", Color.White, Color.Black, TextDecorations.None);
        var b = new InlineRun("Bye", Color.White, Color.Black, TextDecorations.None);

        await Assert.That(a).IsNotEqualTo(b);
    }

    // ─── Run.CollectRuns ────────────────────────────────────────────

    [Test]
    public async Task Run_CollectRuns_EmptyText_ProducesNoRuns()
    {
        var run = new Run { Text = "" };
        var runs = new List<InlineRun>();

        run.CollectRuns(runs, Color.White, Color.Black, TextDecorations.None);

        await Assert.That(runs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Run_CollectRuns_NullText_ProducesNoRuns()
    {
        // Default Text is "" (empty string from DP default), but test the behavior
        var run = new Run();
        var runs = new List<InlineRun>();

        run.CollectRuns(runs, Color.White, Color.Black, TextDecorations.None);

        await Assert.That(runs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Run_CollectRuns_WithText_ProducesSingleRun()
    {
        var run = new Run { Text = "Hello" };
        var runs = new List<InlineRun>();

        run.CollectRuns(runs, Color.White, Color.Black, TextDecorations.None);

        await Assert.That(runs.Count).IsEqualTo(1);
        await Assert.That(runs[0].Text).IsEqualTo("Hello");
    }

    [Test]
    public async Task Run_CollectRuns_InheritsColors_WhenTransparent()
    {
        // Run with no explicit Foreground/Background (defaults to Transparent)
        var run = new Run { Text = "Hi" };
        var runs = new List<InlineRun>();

        run.CollectRuns(runs, Color.Green, Color.Blue, TextDecorations.None);

        await Assert.That(runs[0].Foreground).IsEqualTo(Color.Green);
        await Assert.That(runs[0].Background).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task Run_CollectRuns_OverridesColors_WhenExplicitlySet()
    {
        var run = new Run
        {
            Text = "Hi",
            Foreground = Color.Red,
            Background = Color.Yellow
        };
        var runs = new List<InlineRun>();

        run.CollectRuns(runs, Color.Green, Color.Blue, TextDecorations.None);

        await Assert.That(runs[0].Foreground).IsEqualTo(Color.Red);
        await Assert.That(runs[0].Background).IsEqualTo(Color.Yellow);
    }

    [Test]
    public async Task Run_CollectRuns_InheritsDecorations_WhenNone()
    {
        // Run with no decorations set — should inherit from parent
        var run = new Run { Text = "Hi" };
        var runs = new List<InlineRun>();

        run.CollectRuns(runs, Color.White, Color.Black, TextDecorations.Bold);

        await Assert.That(runs[0].Decorations).IsEqualTo(TextDecorations.Bold);
    }

    [Test]
    public async Task Run_CollectRuns_MergesDecorations_WithInherited()
    {
        var run = new Run
        {
            Text = "Hi",
            TextDecorations = TextDecorations.Italic
        };
        var runs = new List<InlineRun>();

        run.CollectRuns(runs, Color.White, Color.Black, TextDecorations.Bold);

        // Should be Bold | Italic (OR merge)
        await Assert.That(runs[0].Decorations).IsEqualTo(TextDecorations.Bold | TextDecorations.Italic);
    }

    [Test]
    public async Task Run_CollectRuns_ExplicitDecorations_StandAlone()
    {
        var run = new Run
        {
            Text = "Hi",
            TextDecorations = TextDecorations.Underline
        };
        var runs = new List<InlineRun>();

        run.CollectRuns(runs, Color.White, Color.Black, TextDecorations.None);

        await Assert.That(runs[0].Decorations).IsEqualTo(TextDecorations.Underline);
    }

    // ─── Span.CollectRuns ───────────────────────────────────────────

    [Test]
    public async Task Span_CollectRuns_EmptySpan_ProducesNoRuns()
    {
        var span = new Span();
        var runs = new List<InlineRun>();

        span.CollectRuns(runs, Color.White, Color.Black, TextDecorations.None);

        await Assert.That(runs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Span_CollectRuns_SingleChild_DelegatesCorrectly()
    {
        var span = new Span();
        span.Inlines.Add(new Run { Text = "Hello" });
        var runs = new List<InlineRun>();

        span.CollectRuns(runs, Color.White, Color.Black, TextDecorations.None);

        await Assert.That(runs.Count).IsEqualTo(1);
        await Assert.That(runs[0].Text).IsEqualTo("Hello");
        await Assert.That(runs[0].Foreground).IsEqualTo(Color.White);
    }

    [Test]
    public async Task Span_CollectRuns_MultipleChildren_ProducesMultipleRuns()
    {
        var span = new Span();
        span.Inlines.Add(new Run { Text = "Hello " });
        span.Inlines.Add(new Run { Text = "World" });
        var runs = new List<InlineRun>();

        span.CollectRuns(runs, Color.White, Color.Black, TextDecorations.None);

        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs[0].Text).IsEqualTo("Hello ");
        await Assert.That(runs[1].Text).IsEqualTo("World");
    }

    [Test]
    public async Task Span_CollectRuns_PropagatesStyleToChildren()
    {
        var span = new Span
        {
            Foreground = Color.Red,
            TextDecorations = TextDecorations.Bold
        };
        span.Inlines.Add(new Run { Text = "Hi" });
        var runs = new List<InlineRun>();

        span.CollectRuns(runs, Color.White, Color.Black, TextDecorations.None);

        // Child should see Span's resolved foreground (Red overrides White) and decorations (Bold | None)
        await Assert.That(runs[0].Foreground).IsEqualTo(Color.Red);
        await Assert.That(runs[0].Decorations).IsEqualTo(TextDecorations.Bold);
    }

    [Test]
    public async Task Span_CollectRuns_ChildOverridesSpanColors()
    {
        var span = new Span { Foreground = Color.Red };
        span.Inlines.Add(new Run { Text = "Hi", Foreground = Color.Blue });
        var runs = new List<InlineRun>();

        span.CollectRuns(runs, Color.White, Color.Black, TextDecorations.None);

        // Child's explicit Blue overrides Span's Red
        await Assert.That(runs[0].Foreground).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task Span_CollectRuns_NestedSpans_PropagateStyles()
    {
        var outerSpan = new Span { Foreground = Color.Red };
        var innerSpan = new Span { TextDecorations = TextDecorations.Italic };
        innerSpan.Inlines.Add(new Run { Text = "Deep" });
        outerSpan.Inlines.Add(innerSpan);

        var runs = new List<InlineRun>();
        outerSpan.CollectRuns(runs, Color.White, Color.Black, TextDecorations.Bold);

        await Assert.That(runs.Count).IsEqualTo(1);
        await Assert.That(runs[0].Text).IsEqualTo("Deep");
        await Assert.That(runs[0].Foreground).IsEqualTo(Color.Red); // From outer span
        // Decorations: inherited Bold | outer span None (OR'd = Bold) | inner span Italic (OR'd = Bold|Italic)
        await Assert.That(runs[0].Decorations).IsEqualTo(TextDecorations.Bold | TextDecorations.Italic);
    }

    [Test]
    public async Task Span_CollectRuns_DeeplyNested_ThreeLevels()
    {
        var outer = new Span { Foreground = Color.Red };
        var middle = new Span { Background = Color.Green };
        var inner = new Span { TextDecorations = TextDecorations.Underline };
        inner.Inlines.Add(new Run { Text = "X" });
        middle.Inlines.Add(inner);
        outer.Inlines.Add(middle);

        var runs = new List<InlineRun>();
        outer.CollectRuns(runs, Color.White, Color.Black, TextDecorations.None);

        await Assert.That(runs[0].Text).IsEqualTo("X");
        await Assert.That(runs[0].Foreground).IsEqualTo(Color.Red); // From outer
        await Assert.That(runs[0].Background).IsEqualTo(Color.Green); // From middle
        await Assert.That(runs[0].Decorations).IsEqualTo(TextDecorations.Underline); // From inner
    }

    [Test]
    public async Task Span_CollectRuns_MixedChildrenWithEmptyRun_SkipsEmpty()
    {
        var span = new Span();
        span.Inlines.Add(new Run { Text = "A" });
        span.Inlines.Add(new Run { Text = "" }); // empty — skipped
        span.Inlines.Add(new Run { Text = "B" });

        var runs = new List<InlineRun>();
        span.CollectRuns(runs, Color.White, Color.Black, TextDecorations.None);

        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs[0].Text).IsEqualTo("A");
        await Assert.That(runs[1].Text).IsEqualTo("B");
    }

    [Test]
    public async Task Span_GetLogicalChildren_ReturnsInlines()
    {
        var span = new Span();
        var run1 = new Run { Text = "A" };
        var run2 = new Run { Text = "B" };
        span.Inlines.Add(run1);
        span.Inlines.Add(run2);

        var children = span.GetLogicalChildren().ToList();

        await Assert.That(children.Count).IsEqualTo(2);
        await Assert.That(children[0]).IsEqualTo(run1);
        await Assert.That(children[1]).IsEqualTo(run2);
    }

    [Test]
    public async Task Span_GetLogicalChildren_EmptySpan_ReturnsEmpty()
    {
        var span = new Span();

        var children = span.GetLogicalChildren().ToList();

        await Assert.That(children.Count).IsEqualTo(0);
    }

    // ─── InlineCollection ───────────────────────────────────────────

    [Test]
    public async Task InlineCollection_Add_SetsParent()
    {
        var textBlock = new TextBlock();
        var run = new Run { Text = "Test" };

        textBlock.Inlines.Add(run);

        await Assert.That(run.Parent).IsEqualTo(textBlock);
    }

    [Test]
    public async Task InlineCollection_Add_Null_ThrowsArgumentNullException()
    {
        var textBlock = new TextBlock();

        await Assert.That(() => textBlock.Inlines.Add(null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task InlineCollection_Remove_ClearsParent()
    {
        var textBlock = new TextBlock();
        var run = new Run { Text = "Test" };
        textBlock.Inlines.Add(run);

        textBlock.Inlines.Remove(run);

        await Assert.That(run.Parent).IsNull();
    }

    [Test]
    public async Task InlineCollection_Remove_NonExistent_ReturnsFalse()
    {
        var textBlock = new TextBlock();
        var run = new Run { Text = "Test" };

        var result = textBlock.Inlines.Remove(run);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task InlineCollection_Clear_ClearsAllParents()
    {
        var textBlock = new TextBlock();
        var run1 = new Run { Text = "A" };
        var run2 = new Run { Text = "B" };
        textBlock.Inlines.Add(run1);
        textBlock.Inlines.Add(run2);

        textBlock.Inlines.Clear();

        await Assert.That(run1.Parent).IsNull();
        await Assert.That(run2.Parent).IsNull();
        await Assert.That(textBlock.Inlines.Count).IsEqualTo(0);
    }

    [Test]
    public async Task InlineCollection_Insert_SetsParent()
    {
        var textBlock = new TextBlock();
        var run = new Run { Text = "Inserted" };

        textBlock.Inlines.Insert(0, run);

        await Assert.That(run.Parent).IsEqualTo(textBlock);
        await Assert.That(textBlock.Inlines[0]).IsEqualTo(run);
    }

    [Test]
    public async Task InlineCollection_Insert_Null_ThrowsArgumentNullException()
    {
        var textBlock = new TextBlock();

        await Assert.That(() => textBlock.Inlines.Insert(0, null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task InlineCollection_RemoveAt_ClearsParent()
    {
        var textBlock = new TextBlock();
        var run = new Run { Text = "Test" };
        textBlock.Inlines.Add(run);

        textBlock.Inlines.RemoveAt(0);

        await Assert.That(run.Parent).IsNull();
        await Assert.That(textBlock.Inlines.Count).IsEqualTo(0);
    }

    [Test]
    public async Task InlineCollection_Indexer_Set_UpdatesParent()
    {
        var textBlock = new TextBlock();
        var oldRun = new Run { Text = "Old" };
        var newRun = new Run { Text = "New" };
        textBlock.Inlines.Add(oldRun);

        textBlock.Inlines[0] = newRun;

        await Assert.That(oldRun.Parent).IsNull();
        await Assert.That(newRun.Parent).IsEqualTo(textBlock);
    }

    [Test]
    public async Task InlineCollection_Indexer_Set_Null_ThrowsArgumentNullException()
    {
        var textBlock = new TextBlock();
        textBlock.Inlines.Add(new Run { Text = "Test" });

        await Assert.That(() => textBlock.Inlines[0] = null!)
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task InlineCollection_Count_ReturnsCorrectValue()
    {
        var textBlock = new TextBlock();
        textBlock.Inlines.Add(new Run { Text = "A" });
        textBlock.Inlines.Add(new Run { Text = "B" });
        textBlock.Inlines.Add(new Run { Text = "C" });

        await Assert.That(textBlock.Inlines.Count).IsEqualTo(3);
    }

    [Test]
    public async Task InlineCollection_Contains_ReturnsTrueForExistingItem()
    {
        var textBlock = new TextBlock();
        var run = new Run { Text = "Test" };
        textBlock.Inlines.Add(run);

        await Assert.That(textBlock.Inlines.Contains(run)).IsTrue();
    }

    [Test]
    public async Task InlineCollection_Contains_ReturnsFalseForNonExistingItem()
    {
        var textBlock = new TextBlock();
        var run = new Run { Text = "Test" };

        await Assert.That(textBlock.Inlines.Contains(run)).IsFalse();
    }

    [Test]
    public async Task InlineCollection_IndexOf_ReturnsCorrectIndex()
    {
        var textBlock = new TextBlock();
        var run1 = new Run { Text = "A" };
        var run2 = new Run { Text = "B" };
        textBlock.Inlines.Add(run1);
        textBlock.Inlines.Add(run2);

        await Assert.That(textBlock.Inlines.IndexOf(run2)).IsEqualTo(1);
    }

    [Test]
    public async Task InlineCollection_IndexOf_NonExistingItem_ReturnsMinusOne()
    {
        var textBlock = new TextBlock();
        var run = new Run { Text = "Missing" };

        await Assert.That(textBlock.Inlines.IndexOf(run)).IsEqualTo(-1);
    }

    [Test]
    public async Task InlineCollection_IsReadOnly_ReturnsFalse()
    {
        var textBlock = new TextBlock();

        await Assert.That(textBlock.Inlines.IsReadOnly).IsFalse();
    }

    [Test]
    public async Task InlineCollection_Enumerate_ReturnsAllItems()
    {
        var textBlock = new TextBlock();
        var run1 = new Run { Text = "A" };
        var run2 = new Run { Text = "B" };
        textBlock.Inlines.Add(run1);
        textBlock.Inlines.Add(run2);

        var items = textBlock.Inlines.ToList();

        await Assert.That(items.Count).IsEqualTo(2);
        await Assert.That(items[0]).IsEqualTo(run1);
        await Assert.That(items[1]).IsEqualTo(run2);
    }

    // ─── IList (non-generic) for XAML loader ────────────────────────

    [Test]
    public async Task InlineCollection_IListAdd_AcceptsInline()
    {
        var textBlock = new TextBlock();
        var ilist = (System.Collections.IList)textBlock.Inlines;
        var run = new Run { Text = "ViaIList" };

        var index = ilist.Add(run);

        await Assert.That(index).IsEqualTo(0);
        await Assert.That(textBlock.Inlines.Count).IsEqualTo(1);
        await Assert.That(run.Parent).IsEqualTo(textBlock);
    }

    [Test]
    public async Task InlineCollection_IListAdd_NonInline_ThrowsArgumentException()
    {
        var textBlock = new TextBlock();
        var ilist = (System.Collections.IList)textBlock.Inlines;

        await Assert.That(() => ilist.Add("not an inline"))
            .ThrowsExactly<ArgumentException>();
    }

    // ─── Inline base class behavior ─────────────────────────────────

    [Test]
    public async Task Inline_GetPreferredSize_ReturnsDefault()
    {
        var run = new Run { Text = "Test" };

        var size = run.GetPreferredSize(new Rect(0, 0, 100, 50));

        await Assert.That(size.Width).IsEqualTo(0);
        await Assert.That(size.Height).IsEqualTo(0);
    }

    [Test]
    public async Task Inline_CalculateBounds_ReturnsDefault()
    {
        var run = new Run { Text = "Test" };

        var bounds = run.CalculateBounds(new Rect(0, 0, 100, 50));

        await Assert.That(bounds.Width).IsEqualTo(0);
        await Assert.That(bounds.Height).IsEqualTo(0);
    }

    [Test]
    public async Task Inline_Render_DoesNothing()
    {
        var run = new Run { Text = "Test", Foreground = Color.Red };
        var buffer = new CellBuffer(10, 5);

        // Should not throw and should not modify buffer
        run.Render(buffer, new Rect(0, 0, 10, 5));

        // Buffer should still be empty cells
        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo(' ');
    }

    // ─── Inline default DP values ───────────────────────────────────

    [Test]
    public async Task Run_DefaultForeground_IsTransparent()
    {
        var run = new Run();

        await Assert.That(run.Foreground).IsEqualTo(Color.Transparent);
    }

    [Test]
    public async Task Run_DefaultBackground_IsTransparent()
    {
        var run = new Run();

        await Assert.That(run.Background).IsEqualTo(Color.Transparent);
    }

    [Test]
    public async Task Run_DefaultTextDecorations_IsNone()
    {
        var run = new Run();

        await Assert.That(run.TextDecorations).IsEqualTo(TextDecorations.None);
    }

    [Test]
    public async Task Run_DefaultText_IsEmpty()
    {
        var run = new Run();

        await Assert.That(run.Text).IsEqualTo("");
    }
}
