using TerminalNinja.Aot;
using TerminalNinja.Xaml.Binding;

namespace TerminalNinja.Tests.Xaml;

/// <summary>
/// Tests that Run binding inside DataTemplate works end-to-end.
/// Covers two fixes:
///   1. DataTemplate.CloneControl now clones InlineCollection children (Run, Span)
///   2. FrameworkElement.OnDataContextChanged now propagates pending binding
///      activation to logical children (e.g., Run inside TextBlock)
/// </summary>
public class RunBindingTests
{
    // ─── Test data class ────────────────────────────────────────────

    [BindableObject]
    internal class LogItem
    {
        public string Time { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    // ─── Bug 1: DataTemplate cloning preserves Run inlines ──────────

    [Test]
    public async Task DataTemplate_Clone_PreservesRunInlines()
    {
        // Arrange — prototype TextBlock with a Run child
        var prototype = new TextBlock();
        var run = new Run { Text = "Hello" };
        prototype.Inlines.Add(run);

        var template = new DataTemplate { TemplateContent = prototype };

        // Act — clone via CreateContent
        var cloned = template.CreateContent() as TextBlock;

        // Assert — cloned TextBlock has the Run inline
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned!.Inlines.Count).IsEqualTo(1);
        await Assert.That(cloned.Inlines[0]).IsTypeOf<Run>();
        await Assert.That(((Run)cloned.Inlines[0]).Text).IsEqualTo("Hello");
    }

    [Test]
    public async Task DataTemplate_Clone_PreservesRunPendingBindings()
    {
        // Arrange — prototype TextBlock with a Run that has a pending binding
        var prototype = new TextBlock();
        var run = new Run();
        run.AddPendingBinding(new ElementBinding("Text", "Time", BindingMode.OneWay, null, null));
        prototype.Inlines.Add(run);

        var template = new DataTemplate { TemplateContent = prototype };

        // Act — clone
        var cloned = template.CreateContent() as TextBlock;

        // Assert — the cloned Run has the pending binding
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned!.Inlines.Count).IsEqualTo(1);

        var clonedRun = cloned.Inlines[0] as Run;
        await Assert.That(clonedRun).IsNotNull();
        await Assert.That(clonedRun!.PendingBindings).IsNotNull();
        await Assert.That(clonedRun.PendingBindings!.Count).IsEqualTo(1);
        await Assert.That(clonedRun.PendingBindings[0].Path).IsEqualTo("Time");
    }

    // ─── Bug 2: DataContext propagation activates Run bindings ──────

    [Test]
    public async Task Run_PendingBinding_ActivatesWhenParentDataContextSet()
    {
        // Arrange — TextBlock with a Run that has a pending binding on Text
        var textBlock = new TextBlock();
        var run = new Run();
        run.AddPendingBinding(new ElementBinding("Text", "Time", BindingMode.OneWay, null, null));
        textBlock.Inlines.Add(run);

        // Act — setting DataContext on the parent TextBlock should propagate
        // to the Run child and activate its pending binding
        var item = new LogItem { Time = "12:34:56" };
        textBlock.DataContext = item;

        // Assert — Run.Text should be set from the binding
        await Assert.That(run.Text).IsEqualTo("12:34:56");
    }

    [Test]
    public async Task Run_PendingBinding_IgnoredWhenChildHasExplicitDataContext()
    {
        // Arrange — TextBlock with a Run that has its own explicit DataContext
        var textBlock = new TextBlock();
        var run = new Run();
        run.AddPendingBinding(new ElementBinding("Text", "Time", BindingMode.OneWay, null, null));
        textBlock.Inlines.Add(run);

        // Give the Run its own DataContext first
        var runItem = new LogItem { Time = "Run's own DC" };
        run.DataContext = runItem;

        // Act — set parent DataContext (should NOT override the Run's explicit DC)
        var parentItem = new LogItem { Time = "Parent DC" };
        textBlock.DataContext = parentItem;

        // Assert — Run should still show its own DataContext value
        await Assert.That(run.Text).IsEqualTo("Run's own DC");
    }

    // ─── End-to-end: ItemsControl with DataTemplate + Run binding ───

    [Test]
    public async Task ItemsControl_DataTemplate_RunBinding_EndToEnd()
    {
        // Arrange — DataTemplate: <TextBlock><Run Text="{Binding Time}" /></TextBlock>
        var prototype = new TextBlock();
        var run = new Run();
        run.AddPendingBinding(new ElementBinding("Text", "Time", BindingMode.OneWay, null, null));
        prototype.Inlines.Add(run);

        var template = new DataTemplate { TemplateContent = prototype };
        var itemsControl = new ItemsControl { ItemTemplate = template };

        // Act — set ItemsSource with data items
        var items = new List<LogItem>
        {
            new() { Time = "10:00:00" },
            new() { Time = "10:05:00" },
            new() { Time = "10:10:00" }
        };
        itemsControl.ItemsSource = items;

        // Assert — each generated TextBlock should have a Run with the bound time
        var children = itemsControl.ItemsPanel.Children;
        await Assert.That(children.Count).IsEqualTo(3);

        for (var i = 0; i < 3; i++)
        {
            var tb = children[i] as TextBlock;
            await Assert.That(tb).IsNotNull();
            await Assert.That(tb!.Inlines.Count).IsEqualTo(1);

            var boundRun = tb.Inlines[0] as Run;
            await Assert.That(boundRun).IsNotNull();
            await Assert.That(boundRun!.Text).IsEqualTo(items[i].Time);
        }
    }

    // ─── Nested Span > Run binding ──────────────────────────────────

    [Test]
    public async Task DataTemplate_Clone_PreservesNestedSpanRunInlines()
    {
        // Arrange — prototype: <TextBlock><Span><Run Text="Nested" /></Span></TextBlock>
        var prototype = new TextBlock();
        var span = new Span();
        var run = new Run { Text = "Nested" };
        span.Inlines.Add(run);
        prototype.Inlines.Add(span);

        var template = new DataTemplate { TemplateContent = prototype };

        // Act — clone
        var cloned = template.CreateContent() as TextBlock;

        // Assert — cloned tree has Span > Run with correct text
        await Assert.That(cloned).IsNotNull();
        await Assert.That(cloned!.Inlines.Count).IsEqualTo(1);
        await Assert.That(cloned.Inlines[0]).IsTypeOf<Span>();

        var clonedSpan = (Span)cloned.Inlines[0];
        await Assert.That(clonedSpan.Inlines.Count).IsEqualTo(1);
        await Assert.That(clonedSpan.Inlines[0]).IsTypeOf<Run>();
        await Assert.That(((Run)clonedSpan.Inlines[0]).Text).IsEqualTo("Nested");
    }

    [Test]
    public async Task Span_Run_PendingBinding_ActivatesFromGrandparentDataContext()
    {
        // Arrange — TextBlock > Span > Run with pending binding
        var textBlock = new TextBlock();
        var span = new Span();
        var run = new Run();
        run.AddPendingBinding(new ElementBinding("Text", "Message", BindingMode.OneWay, null, null));
        span.Inlines.Add(run);
        textBlock.Inlines.Add(span);

        // Act — set DataContext on grandparent TextBlock
        var item = new LogItem { Message = "Deep propagation" };
        textBlock.DataContext = item;

        // Assert — Run.Text should be set from the deeply propagated binding
        await Assert.That(run.Text).IsEqualTo("Deep propagation");
    }
}
