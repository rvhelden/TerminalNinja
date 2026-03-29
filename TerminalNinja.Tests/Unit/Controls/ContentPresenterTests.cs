namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for the ContentPresenter control covering:
/// - Content as UIElement (passthrough rendering)
/// - Content as string (auto-wrapped in TextBlock)
/// - Content as arbitrary object (ToString() fallback)
/// - ContentTemplate (DataTemplate) usage
/// - DataContext synchronization (Content → DataContext)
/// - Visual child lifecycle (parent management)
/// - GetLogicalChildren / GetChildrenWithBounds
/// - GetPreferredSize delegation
/// - Null content handling
/// </summary>
public class ContentPresenterTests
{
    #region Content — UIElement

    [Test]
    public async Task Content_UIElement_RendersDirectly()
    {
        // Arrange
        var cp = new ContentPresenter();
        var tb = new TextBlock { Text = "Hello" };

        // Act
        cp.Content = tb;
        using var buffer = new CellBuffer(20, 5);
        cp.Render(buffer, new Rect(0, 0, 20, 5));

        // Assert — 'H' should appear at (0,0)
        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('H');
        await Assert.That(buffer.GetCell(4, 0).Character).IsEqualTo('o');
    }

    [Test]
    public async Task Content_UIElement_ReturnsAsVisualChild()
    {
        // Arrange
        var cp = new ContentPresenter();
        var border = new Border();

        // Act
        cp.Content = border;

        // Assert — the border should appear in GetChildrenWithBounds
        var children = cp.GetChildrenWithBounds(new Rect(0, 0, 10, 10)).ToList();
        await Assert.That(children.Count).IsEqualTo(1);
        await Assert.That(children[0].Child).IsEqualTo(border);
    }

    [Test]
    public async Task Content_UIElement_AppearsInLogicalChildren()
    {
        // Arrange
        var cp = new ContentPresenter();
        var tb = new TextBlock { Text = "Test" };

        // Act
        cp.Content = tb;

        // Assert
        var logical = cp.GetLogicalChildren().ToList();
        await Assert.That(logical.Count).IsEqualTo(1);
        await Assert.That(logical[0]).IsEqualTo(tb);
    }

    [Test]
    public async Task Content_UIElement_DoesNotOverwriteExistingParent()
    {
        // When used inside ContentControl, the ContentControl already sets
        // the UIElement's Parent. ContentPresenter should NOT overwrite it.
        var cp = new ContentPresenter();
        var tb = new TextBlock { Text = "Test" };
        var fakeParent = new Border();
        tb.Parent = fakeParent;

        // Act
        cp.Content = tb;

        // Assert — parent should still be the original parent, not cp
        await Assert.That(tb.Parent).IsEqualTo(fakeParent);
    }

    [Test]
    public async Task Content_UIElement_Replaced_OldStillRenderable()
    {
        // Replacing UIElement content should not null out old element's parent
        // (parent ownership belongs to the ContentControl, not ContentPresenter)
        var cp = new ContentPresenter();
        var old = new TextBlock { Text = "Old" };
        var fakeParent = new Border();
        old.Parent = fakeParent;
        cp.Content = old;

        // Act
        var newTb = new TextBlock { Text = "New" };
        cp.Content = newTb;

        // Assert — old's parent is unchanged (we didn't own it)
        await Assert.That(old.Parent).IsEqualTo(fakeParent);
    }

    #endregion

    #region Content — String

    [Test]
    public async Task Content_String_RendersAsTextBlock()
    {
        // Arrange
        var cp = new ContentPresenter();

        // Act
        cp.Content = "Hello World";
        using var buffer = new CellBuffer(20, 5);
        cp.Render(buffer, new Rect(0, 0, 20, 5));

        // Assert — 'H' should appear at (0,0)
        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('H');
        await Assert.That(buffer.GetCell(4, 0).Character).IsEqualTo('o');
    }

    [Test]
    public async Task Content_String_CreatesTextBlockChild()
    {
        // Arrange
        var cp = new ContentPresenter();

        // Act
        cp.Content = "Test Text";

        // Assert — visual child should be a TextBlock
        var children = cp.GetChildrenWithBounds(new Rect(0, 0, 10, 10)).ToList();
        await Assert.That(children.Count).IsEqualTo(1);
        await Assert.That(children[0].Child).IsTypeOf<TextBlock>();

        var tb = (TextBlock)children[0].Child;
        await Assert.That(tb.Text).IsEqualTo("Test Text");
    }

    [Test]
    public async Task Content_String_GeneratedTextBlockHasParent()
    {
        // The generated TextBlock's parent should be the ContentPresenter
        var cp = new ContentPresenter();
        cp.Content = "Parented";

        var children = cp.GetChildrenWithBounds(new Rect(0, 0, 10, 10)).ToList();
        var tb = (TextBlock)children[0].Child;
        await Assert.That(tb.Parent).IsEqualTo(cp);
    }

    [Test]
    public async Task Content_EmptyString_RendersEmptyTextBlock()
    {
        var cp = new ContentPresenter();
        cp.Content = "";

        var children = cp.GetChildrenWithBounds(new Rect(0, 0, 10, 10)).ToList();
        await Assert.That(children.Count).IsEqualTo(1);
        await Assert.That(children[0].Child).IsTypeOf<TextBlock>();
    }

    #endregion

    #region Content — Object (ToString fallback)

    [Test]
    public async Task Content_Object_RendersViaToString()
    {
        // Arrange
        var cp = new ContentPresenter();
        var item = 42; // int.ToString() → "42"

        // Act
        cp.Content = item;
        using var buffer = new CellBuffer(20, 5);
        cp.Render(buffer, new Rect(0, 0, 20, 5));

        // Assert
        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('4');
        await Assert.That(buffer.GetCell(1, 0).Character).IsEqualTo('2');
    }

    [Test]
    public async Task Content_CustomObject_UsesToString()
    {
        var cp = new ContentPresenter();
        var obj = new TestDisplayObject("Custom Display");
        cp.Content = obj;

        var children = cp.GetChildrenWithBounds(new Rect(0, 0, 20, 10)).ToList();
        await Assert.That(children.Count).IsEqualTo(1);
        await Assert.That(children[0].Child).IsTypeOf<TextBlock>();

        var tb = (TextBlock)children[0].Child;
        await Assert.That(tb.Text).IsEqualTo("Custom Display");
    }

    #endregion

    #region ContentTemplate (DataTemplate)

    [Test]
    public async Task ContentTemplate_UsedForNonUIElementContent()
    {
        // Arrange
        var cp = new ContentPresenter();
        var template = new DataTemplate
        {
            TemplateFactory = () => new TextBlock { Text = "Templated!" }
        };
        cp.ContentTemplate = template;

        // Act
        cp.Content = "ignored by template factory";

        // Assert — the template content should be used
        var children = cp.GetChildrenWithBounds(new Rect(0, 0, 20, 10)).ToList();
        await Assert.That(children.Count).IsEqualTo(1);
        await Assert.That(children[0].Child).IsTypeOf<TextBlock>();

        var tb = (TextBlock)children[0].Child;
        await Assert.That(tb.Text).IsEqualTo("Templated!");
    }

    [Test]
    public async Task ContentTemplate_SetsDataContextOnCreatedContent()
    {
        var cp = new ContentPresenter();
        var dataItem = "MyData";
        var template = new DataTemplate
        {
            TemplateFactory = () => new TextBlock()
        };
        cp.ContentTemplate = template;
        cp.Content = dataItem;

        var children = cp.GetChildrenWithBounds(new Rect(0, 0, 10, 10)).ToList();
        var tb = (TextBlock)children[0].Child;
        await Assert.That(tb.DataContext).IsEqualTo("MyData");
    }

    [Test]
    public async Task ContentTemplate_UIElementContent_UsedDirectly()
    {
        // When Content is a UIElement, it should be used directly even if
        // ContentTemplate is set (UIElement passthrough has priority)
        var cp = new ContentPresenter();
        var tb = new TextBlock { Text = "Direct" };
        var template = new DataTemplate
        {
            TemplateFactory = () => new TextBlock { Text = "Templated" }
        };
        cp.ContentTemplate = template;
        cp.Content = tb;

        var children = cp.GetChildrenWithBounds(new Rect(0, 0, 10, 10)).ToList();
        await Assert.That(children.Count).IsEqualTo(1);
        await Assert.That(children[0].Child).IsEqualTo(tb);
    }

    [Test]
    public async Task ContentTemplate_ChangingTemplate_RegeneratesChild()
    {
        var cp = new ContentPresenter();
        cp.Content = "SomeData";

        // Initial — no template, falls back to TextBlock with "SomeData"
        var children1 = cp.GetChildrenWithBounds(new Rect(0, 0, 10, 10)).ToList();
        var tb1 = (TextBlock)children1[0].Child;
        await Assert.That(tb1.Text).IsEqualTo("SomeData");

        // Set template
        cp.ContentTemplate = new DataTemplate
        {
            TemplateFactory = () => new TextBlock { Text = "Template Output" }
        };

        var children2 = cp.GetChildrenWithBounds(new Rect(0, 0, 10, 10)).ToList();
        var tb2 = (TextBlock)children2[0].Child;
        await Assert.That(tb2.Text).IsEqualTo("Template Output");
    }

    #endregion

    #region DataContext Synchronization

    [Test]
    public async Task Content_SetsDataContext()
    {
        var cp = new ContentPresenter();
        var data = "MyDataContext";
        cp.Content = data;

        await Assert.That(cp.DataContext).IsEqualTo("MyDataContext");
    }

    [Test]
    public async Task Content_Null_ClearsDataContext()
    {
        var cp = new ContentPresenter();
        cp.Content = "Something";
        await Assert.That(cp.DataContext).IsNotNull();

        cp.Content = null;
        await Assert.That(cp.DataContext).IsNull();
    }

    [Test]
    public async Task Content_UIElement_StillSetsDataContext()
    {
        var cp = new ContentPresenter();
        var tb = new TextBlock { Text = "Hi" };
        cp.Content = tb;

        // DataContext should be the UIElement itself (WPF convention)
        await Assert.That(cp.DataContext).IsEqualTo(tb);
    }

    #endregion

    #region Null Content

    [Test]
    public async Task Content_Null_NoVisualChild()
    {
        var cp = new ContentPresenter();
        cp.Content = null;

        var children = cp.GetChildrenWithBounds(new Rect(0, 0, 10, 10)).ToList();
        await Assert.That(children.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Content_Null_RenderDoesNothing()
    {
        var cp = new ContentPresenter();
        cp.Content = null;

        using var buffer = new CellBuffer(10, 5);
        // Fill with a marker so we can verify nothing was written
        buffer.FillRect(new Rect(0, 0, 10, 5), new Cell('Z', Color.White, Color.Black));

        // Should not throw, and should not overwrite anything
        cp.Render(buffer, new Rect(0, 0, 10, 5));

        // Buffer should still have marker cells
        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('Z');
    }

    [Test]
    public async Task Content_Null_PreferredSizeIsZero()
    {
        var cp = new ContentPresenter();
        cp.Content = null;

        var size = cp.GetPreferredSize(new Rect(0, 0, 20, 10));
        await Assert.That(size.Width).IsEqualTo(0);
        await Assert.That(size.Height).IsEqualTo(0);
    }

    #endregion

    #region GetPreferredSize

    [Test]
    public async Task GetPreferredSize_DelegatesToVisualChild()
    {
        var cp = new ContentPresenter();
        cp.Content = new TextBlock { Text = "ABCDE" };

        var size = cp.GetPreferredSize(new Rect(0, 0, 20, 10));
        // TextBlock preferred width = text length = 5
        await Assert.That(size.Width).IsEqualTo(5);
    }

    [Test]
    public async Task GetPreferredSize_StringContent_MatchesTextLength()
    {
        var cp = new ContentPresenter();
        cp.Content = "Test";

        var size = cp.GetPreferredSize(new Rect(0, 0, 20, 10));
        await Assert.That(size.Width).IsEqualTo(4);
    }

    #endregion

    #region CalculateBounds

    [Test]
    public async Task CalculateBounds_ReturnsParentBounds()
    {
        var cp = new ContentPresenter();
        var parent = new Rect(5, 10, 30, 20);

        var bounds = cp.CalculateBounds(parent);

        await Assert.That(bounds.X).IsEqualTo(5);
        await Assert.That(bounds.Y).IsEqualTo(10);
        await Assert.That(bounds.Width).IsEqualTo(30);
        await Assert.That(bounds.Height).IsEqualTo(20);
    }

    #endregion

    #region ContentControl Integration

    [Test]
    public async Task ContentControl_StringContent_RendersViaContentPresenter()
    {
        // ContentControl with string content should render it as text
        var cc = new ContentControl();
        cc.Content = "Hello CC";

        using var buffer = new CellBuffer(20, 5);
        cc.Render(buffer, new Rect(0, 0, 20, 5));

        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('H');
        await Assert.That(buffer.GetCell(7, 0).Character).IsEqualTo('C');
    }

    [Test]
    public async Task ContentControl_UIElementContent_RendersDirectly()
    {
        var cc = new ContentControl();
        cc.Content = new TextBlock { Text = "UIE" };

        using var buffer = new CellBuffer(20, 5);
        cc.Render(buffer, new Rect(0, 0, 20, 5));

        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('U');
        await Assert.That(buffer.GetCell(1, 0).Character).IsEqualTo('I');
        await Assert.That(buffer.GetCell(2, 0).Character).IsEqualTo('E');
    }

    [Test]
    public async Task ContentControl_ContentTemplate_AppliedViaContentPresenter()
    {
        var cc = new ContentControl();
        cc.ContentTemplate = new DataTemplate
        {
            TemplateFactory = () => new TextBlock { Text = "From Template" }
        };
        cc.Content = "data";

        using var buffer = new CellBuffer(20, 5);
        cc.Render(buffer, new Rect(0, 0, 20, 5));

        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('F');
    }

    [Test]
    public async Task ContentControl_UIElementContent_ParentIsContentControl()
    {
        // Parent of UIElement Content should be the ContentControl, not ContentPresenter
        var cc = new ContentControl();
        var tb = new TextBlock { Text = "Parented" };
        cc.Content = tb;

        await Assert.That(tb.Parent).IsEqualTo(cc);
    }

    [Test]
    public async Task ContentControl_ObjectContent_HasContent()
    {
        var cc = new ContentControl();
        cc.Content = 42;

        await Assert.That(cc.HasContent).IsTrue();
        await Assert.That(cc.Content).IsEqualTo(42);
    }

    [Test]
    public async Task ContentControl_NullContent_HasContentFalse()
    {
        var cc = new ContentControl();
        cc.Content = null;

        await Assert.That(cc.HasContent).IsFalse();
    }

    #endregion

    #region Window Integration

    [Test]
    public async Task Window_StringContent_Renders()
    {
        var window = new Window();
        window.Content = "Window Text";

        using var buffer = new CellBuffer(20, 5);
        window.Render(buffer, new Rect(0, 0, 20, 5));

        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('W');
    }

    [Test]
    public async Task Window_UIElementContent_Renders()
    {
        var window = new Window();
        window.Content = new TextBlock { Text = "WinUI" };

        using var buffer = new CellBuffer(20, 5);
        window.Render(buffer, new Rect(0, 0, 20, 5));

        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('W');
        await Assert.That(buffer.GetCell(4, 0).Character).IsEqualTo('I');
    }

    #endregion

    #region ListBoxItem Integration

    [Test]
    public async Task ListBoxItem_StringContent_RendersText()
    {
        var lbi = new ListBoxItem();
        lbi.Content = new TextBlock { Text = "Item" };

        using var buffer = new CellBuffer(20, 3);
        lbi.Render(buffer, new Rect(0, 0, 20, 1));

        await Assert.That(buffer.GetCell(0, 0).Character).IsEqualTo('I');
        await Assert.That(buffer.GetCell(3, 0).Character).IsEqualTo('m');
    }

    [Test]
    public async Task ListBoxItem_SelectedState_OverridesTextBlockColors()
    {
        var lbi = new ListBoxItem
        {
            IsSelected = true,
            SelectedBackground = Color.Red,
            SelectedForeground = Color.Green
        };
        lbi.Content = new TextBlock { Text = "X" };

        using var buffer = new CellBuffer(10, 1);
        lbi.Render(buffer, new Rect(0, 0, 10, 1));

        // Position 0 has the selection indicator '▌'
        var indicatorCell = buffer.GetCell(0, 0);
        await Assert.That(indicatorCell.Character).IsEqualTo('\u258C'); // ▌
        await Assert.That(indicatorCell.Foreground).IsEqualTo(Color.Green);
        await Assert.That(indicatorCell.Background).IsEqualTo(Color.Red);

        // The text content starts at position 1 (offset by the indicator)
        var cell = buffer.GetCell(1, 0);
        await Assert.That(cell.Character).IsEqualTo('X');
        await Assert.That(cell.Foreground).IsEqualTo(Color.Green);
        await Assert.That(cell.Background).IsEqualTo(Color.Red);
    }

    #endregion

    // ─── Test helpers ────────────────────────────────────────────────

    private sealed class TestDisplayObject
    {
        private readonly string _display;
        public TestDisplayObject(string display) => _display = display;
        public override string ToString() => _display;
    }
}
