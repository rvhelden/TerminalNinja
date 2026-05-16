namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for the TextBox control covering:
/// - Default property values
/// - Text editing (insert, delete)
/// - Caret movement
/// - Selection
/// - Clipboard (internal)
/// - Rendering (single-line, multi-line, placeholder, cursor, selection highlight)
/// - MaxLength constraint
/// - IsReadOnly mode
/// - Multi-line (AcceptsReturn)
/// - XAML loading
/// </summary>
public class TextBoxTests
{
    #region Default Values

    [Test]
    public async Task Text_Default_IsEmptyString()
    {
        var tb = new TextBox();
        await Assert.That(tb.Text).IsEqualTo("");
    }

    [Test]
    public async Task IsReadOnly_Default_IsFalse()
    {
        var tb = new TextBox();
        await Assert.That(tb.IsReadOnly).IsFalse();
    }

    [Test]
    public async Task AcceptsReturn_Default_IsFalse()
    {
        var tb = new TextBox();
        await Assert.That(tb.AcceptsReturn).IsFalse();
    }

    [Test]
    public async Task MaxLength_Default_IsZero()
    {
        var tb = new TextBox();
        await Assert.That(tb.MaxLength).IsEqualTo(0);
    }

    [Test]
    public async Task Focusable_Default_IsTrue()
    {
        var tb = new TextBox();
        await Assert.That(tb.Focusable).IsTrue();
    }

    [Test]
    public async Task CaretIndex_Default_IsZero()
    {
        var tb = new TextBox();
        await Assert.That(tb.CaretIndex).IsEqualTo(0);
    }

    #endregion

    #region Text Editing

    [Test]
    public async Task OnKeyEvent_PrintableChar_InsertsAtCaret()
    {
        var tb = new TextBox();
        TypeChar(tb, 'H');
        TypeChar(tb, 'i');

        await Assert.That(tb.Text).IsEqualTo("Hi");
        await Assert.That(tb.CaretIndex).IsEqualTo(2);
    }

    [Test]
    public async Task OnKeyEvent_Backspace_DeletesCharBeforeCaret()
    {
        var tb = new TextBox { Text = "Hello" };
        tb.CaretIndex = 5;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.Backspace, '\0', false, false, false));

        await Assert.That(tb.Text).IsEqualTo("Hell");
        await Assert.That(tb.CaretIndex).IsEqualTo(4);
    }

    [Test]
    public async Task OnKeyEvent_Backspace_AtStart_DoesNothing()
    {
        var tb = new TextBox { Text = "Hello" };
        tb.CaretIndex = 0;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.Backspace, '\0', false, false, false));

        await Assert.That(tb.Text).IsEqualTo("Hello");
        await Assert.That(tb.CaretIndex).IsEqualTo(0);
    }

    [Test]
    public async Task OnKeyEvent_Delete_DeletesCharAfterCaret()
    {
        var tb = new TextBox { Text = "Hello" };
        tb.CaretIndex = 0;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.Delete, '\0', false, false, false));

        await Assert.That(tb.Text).IsEqualTo("ello");
        await Assert.That(tb.CaretIndex).IsEqualTo(0);
    }

    [Test]
    public async Task OnKeyEvent_Delete_AtEnd_DoesNothing()
    {
        var tb = new TextBox { Text = "Hello" };
        tb.CaretIndex = 5;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.Delete, '\0', false, false, false));

        await Assert.That(tb.Text).IsEqualTo("Hello");
    }

    [Test]
    public async Task OnKeyEvent_PrintableChar_WhenReadOnly_DoesNotInsert()
    {
        var tb = new TextBox { Text = "Hello", IsReadOnly = true };
        tb.CaretIndex = 5;

        TypeChar(tb, '!');

        await Assert.That(tb.Text).IsEqualTo("Hello");
    }

    [Test]
    public async Task MaxLength_PreventsExcessInput()
    {
        var tb = new TextBox { MaxLength = 5 };
        for (var i = 0; i < 10; i++)
        {
            TypeChar(tb, (char)('A' + i));
        }

        await Assert.That(tb.Text).IsEqualTo("ABCDE");
        await Assert.That(tb.Text.Length).IsEqualTo(5);
    }

    [Test]
    public async Task InsertText_WithSelection_ReplacesSelectedText()
    {
        var tb = new TextBox { Text = "Hello World" };
        tb.CaretIndex = 0;

        // Select "Hello"
        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', true, false, false));
        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', true, false, false));
        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', true, false, false));
        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', true, false, false));
        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', true, false, false));

        // Type replacement
        TypeChar(tb, 'B');
        TypeChar(tb, 'y');
        TypeChar(tb, 'e');

        await Assert.That(tb.Text).IsEqualTo("Bye World");
    }

    [Test]
    public async Task OnKeyEvent_CtrlBackspace_DeletesWordBackward()
    {
        var tb = new TextBox { Text = "Hello World" };
        tb.CaretIndex = 11;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.Backspace, '\0', false, false, true));

        await Assert.That(tb.Text).IsEqualTo("Hello ");
    }

    #endregion

    #region Caret Movement

    [Test]
    public async Task OnKeyEvent_LeftArrow_MovesCaretLeft()
    {
        var tb = new TextBox { Text = "Hello" };
        tb.CaretIndex = 3;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.LeftArrow, '\0', false, false, false));

        await Assert.That(tb.CaretIndex).IsEqualTo(2);
    }

    [Test]
    public async Task OnKeyEvent_RightArrow_MovesCaretRight()
    {
        var tb = new TextBox { Text = "Hello" };
        tb.CaretIndex = 2;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));

        await Assert.That(tb.CaretIndex).IsEqualTo(3);
    }

    [Test]
    public async Task OnKeyEvent_Home_MovesCaretToStart()
    {
        var tb = new TextBox { Text = "Hello" };
        tb.CaretIndex = 3;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.Home, '\0', false, false, false));

        await Assert.That(tb.CaretIndex).IsEqualTo(0);
    }

    [Test]
    public async Task OnKeyEvent_End_MovesCaretToEnd()
    {
        var tb = new TextBox { Text = "Hello" };
        tb.CaretIndex = 0;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.End, '\0', false, false, false));

        await Assert.That(tb.CaretIndex).IsEqualTo(5);
    }

    [Test]
    public async Task OnKeyEvent_CtrlLeft_MovesToPreviousWord()
    {
        var tb = new TextBox { Text = "Hello World" };
        tb.CaretIndex = 11;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.LeftArrow, '\0', false, false, true));

        await Assert.That(tb.CaretIndex).IsEqualTo(6);
    }

    [Test]
    public async Task OnKeyEvent_CtrlRight_MovesToNextWord()
    {
        var tb = new TextBox { Text = "Hello World" };
        tb.CaretIndex = 0;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, true));

        await Assert.That(tb.CaretIndex).IsEqualTo(6);
    }

    [Test]
    public async Task LeftArrow_AtStart_StaysAtZero()
    {
        var tb = new TextBox { Text = "Hello" };
        tb.CaretIndex = 0;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.LeftArrow, '\0', false, false, false));

        await Assert.That(tb.CaretIndex).IsEqualTo(0);
    }

    [Test]
    public async Task RightArrow_AtEnd_StaysAtEnd()
    {
        var tb = new TextBox { Text = "Hello" };
        tb.CaretIndex = 5;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));

        await Assert.That(tb.CaretIndex).IsEqualTo(5);
    }

    #endregion

    #region Selection

    [Test]
    public async Task OnKeyEvent_ShiftRight_SelectsCharacter()
    {
        var tb = new TextBox { Text = "Hello" };
        tb.CaretIndex = 0;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', true, false, false));

        await Assert.That(tb.SelectionLength).IsEqualTo(1);
        await Assert.That(tb.SelectedText).IsEqualTo("H");
    }

    [Test]
    public async Task OnKeyEvent_ShiftHome_SelectsToStart()
    {
        var tb = new TextBox { Text = "Hello" };
        tb.CaretIndex = 3;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.Home, '\0', true, false, false));

        await Assert.That(tb.SelectionStart).IsEqualTo(0);
        await Assert.That(tb.SelectionLength).IsEqualTo(3);
        await Assert.That(tb.SelectedText).IsEqualTo("Hel");
    }

    [Test]
    public async Task OnKeyEvent_CtrlA_SelectsAllText()
    {
        var tb = new TextBox { Text = "Hello World" };

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.A, '\0', false, false, true));

        await Assert.That(tb.SelectionStart).IsEqualTo(0);
        await Assert.That(tb.SelectionLength).IsEqualTo(11);
        await Assert.That(tb.SelectedText).IsEqualTo("Hello World");
    }

    [Test]
    public async Task NavigationWithoutShift_ClearsSelection()
    {
        var tb = new TextBox { Text = "Hello" };
        tb.CaretIndex = 0;

        // Select first 3 chars
        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', true, false, false));
        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', true, false, false));
        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', true, false, false));

        await Assert.That(tb.SelectionLength).IsEqualTo(3);

        // Navigate without shift — should clear selection
        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));

        await Assert.That(tb.SelectionLength).IsEqualTo(0);
    }

    #endregion

    #region Clipboard

    [Test]
    public async Task OnKeyEvent_CtrlC_CopiesSelectedText()
    {
        var tb = new TextBox { Text = "Hello World" };
        tb.CaretIndex = 0;

        // Select "Hello"
        for (var i = 0; i < 5; i++)
            tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', true, false, false));

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.C, '\0', false, false, true));

        // Paste into a new TextBox to verify clipboard content
        var tb2 = new TextBox();
        tb2.OnKeyEvent(new KeyEvent(ConsoleKey.V, '\0', false, false, true));

        await Assert.That(tb2.Text).IsEqualTo("Hello");
    }

    [Test]
    public async Task OnKeyEvent_CtrlX_CutsSelectedText()
    {
        var tb = new TextBox { Text = "Hello World" };
        tb.CaretIndex = 0;

        // Select "Hello"
        for (var i = 0; i < 5; i++)
            tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', true, false, false));

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.X, '\0', false, false, true));

        await Assert.That(tb.Text).IsEqualTo(" World");

        // Paste into another TextBox
        var tb2 = new TextBox();
        tb2.OnKeyEvent(new KeyEvent(ConsoleKey.V, '\0', false, false, true));
        await Assert.That(tb2.Text).IsEqualTo("Hello");
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_EmptyText_ShowsPlaceholder()
    {
        var tb = new TextBox
        {
            PlaceholderText = "Enter text...",
            PlaceholderForeground = Color.DarkGray
        };

        using var buffer = new CellBuffer(30, 3);
        tb.Render(buffer, new Rect(0, 0, 30, 3));

        // Placeholder text starts after border (1 cell)
        await Assert.That(buffer.GetCell(1, 1).Codepoint).IsEqualTo('E');
        await Assert.That(buffer.GetCell(1, 1).Foreground).IsEqualTo(Color.DarkGray);
    }

    [Test]
    public async Task Render_WithText_ShowsText()
    {
        var tb = new TextBox { Text = "Hello" };

        using var buffer = new CellBuffer(20, 3);
        tb.Render(buffer, new Rect(0, 0, 20, 3));

        // Text starts after border (1 cell)
        await Assert.That(buffer.GetCell(1, 1).Codepoint).IsEqualTo('H');
        await Assert.That(buffer.GetCell(5, 1).Codepoint).IsEqualTo('o');
    }

    [Test]
    public async Task Render_Focused_ShowsCursor()
    {
        var tb = new TextBox { Text = "Hello" };
        tb.CaretIndex = 0;

        // Simulate focus
        tb.IsFocused = true;

        using var buffer = new CellBuffer(20, 3);
        tb.Render(buffer, new Rect(0, 0, 20, 3));

        // The caret cell should have Inverse decoration
        var caretCell = buffer.GetCell(1, 1);
        await Assert.That((caretCell.Decorations & TextDecorations.Inverse) != 0).IsTrue();
    }

    [Test]
    public async Task Render_WithSelection_HighlightsSelectedText()
    {
        var tb = new TextBox
        {
            Text = "Hello",
            SelectionBackground = Color.Blue,
            SelectionForeground = Color.Yellow
        };
        tb.CaretIndex = 0;

        // Select first 3 chars
        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', true, false, false));
        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', true, false, false));
        tb.OnKeyEvent(new KeyEvent(ConsoleKey.RightArrow, '\0', true, false, false));

        using var buffer = new CellBuffer(20, 3);
        tb.Render(buffer, new Rect(0, 0, 20, 3));

        // Selected chars should use selection colors
        var selectedCell = buffer.GetCell(1, 1); // 'H' at position 1
        await Assert.That(selectedCell.Background).IsEqualTo(Color.Blue);
        await Assert.That(selectedCell.Foreground).IsEqualTo(Color.Yellow);

        // Unselected chars should use normal colors
        var normalCell = buffer.GetCell(4, 1); // 'l' at position 4 (index 3)
        await Assert.That(normalCell.Background).IsNotEqualTo(Color.Blue);
    }

    [Test]
    public async Task Render_TextLongerThanViewport_ScrollsToShowCaret()
    {
        var tb = new TextBox { Text = "A very long text that exceeds the viewport width" };
        tb.CaretIndex = tb.Text.Length; // Caret at end

        using var buffer = new CellBuffer(15, 3);
        tb.Render(buffer, new Rect(0, 0, 15, 3));

        // The last character of the text should be visible near the right edge
        // The viewport width is 15 - 2 (border) = 13 chars of text
        // The end of text should be visible
        var lastCharCell = buffer.GetCell(13, 1);
        await Assert.That(lastCharCell.Codepoint).IsNotEqualTo('\0');
    }

    [Test]
    public async Task Render_DrawsBorder()
    {
        var tb = new TextBox();

        using var buffer = new CellBuffer(20, 3);
        tb.Render(buffer, new Rect(0, 0, 20, 3));

        // Top-left corner should be a rounded border character
        var corner = buffer.GetCell(0, 0);
        await Assert.That(corner.Codepoint).IsNotEqualTo(' ');
        await Assert.That(corner.Codepoint).IsNotEqualTo('\0');
    }

    #endregion

    #region Multi-line (AcceptsReturn)

    [Test]
    public async Task OnKeyEvent_Enter_WhenAcceptsReturn_InsertsNewline()
    {
        var tb = new TextBox { AcceptsReturn = true, Text = "Hello" };
        tb.CaretIndex = 5;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(tb.Text).IsEqualTo("Hello\n");
        await Assert.That(tb.CaretIndex).IsEqualTo(6);
    }

    [Test]
    public async Task OnKeyEvent_Enter_WhenNotAcceptsReturn_DoesNotInsert()
    {
        var tb = new TextBox { Text = "Hello" };
        tb.CaretIndex = 5;

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(tb.Text).IsEqualTo("Hello");
    }

    [Test]
    public async Task OnKeyEvent_UpArrow_WhenMultiLine_MovesCaretUp()
    {
        var tb = new TextBox { AcceptsReturn = true, Text = "Line1\nLine2" };
        tb.CaretIndex = 8; // 'n' in "Line2"

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.UpArrow, '\0', false, false, false));

        // Should move to line 0, column 2
        await Assert.That(tb.CaretIndex).IsEqualTo(2);
    }

    [Test]
    public async Task OnKeyEvent_DownArrow_WhenMultiLine_MovesCaretDown()
    {
        var tb = new TextBox { AcceptsReturn = true, Text = "Line1\nLine2" };
        tb.CaretIndex = 2; // 'n' in "Line1"

        tb.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        // Should move to line 1, column 2
        await Assert.That(tb.CaretIndex).IsEqualTo(8);
    }

    [Test]
    public async Task Render_MultiLine_ShowsMultipleLines()
    {
        var tb = new TextBox
        {
            AcceptsReturn = true,
            Text = "AAA\nBBB",
            Height = Size.Absolute(5)
        };

        using var buffer = new CellBuffer(20, 5);
        tb.Render(buffer, new Rect(0, 0, 20, 5));

        // Line 1: "AAA" starts at (1, 1)
        await Assert.That(buffer.GetCell(1, 1).Codepoint).IsEqualTo('A');
        // Line 2: "BBB" starts at (1, 2)
        await Assert.That(buffer.GetCell(1, 2).Codepoint).IsEqualTo('B');
    }

    #endregion

    #region Events

    [Test]
    public async Task TextChanged_RaisedOnTextModification()
    {
        var tb = new TextBox();
        var raised = false;
        string? newText = null;

        tb.TextChanged += (_, e) =>
        {
            raised = true;
            newText = e.NewText;
        };

        TypeChar(tb, 'A');

        await Assert.That(raised).IsTrue();
        await Assert.That(newText).IsEqualTo("A");
    }

    [Test]
    public async Task TextChanged_IncludesOldText()
    {
        var tb = new TextBox { Text = "Hello" };
        string? oldText = null;

        tb.TextChanged += (_, e) => oldText = e.OldText;

        tb.CaretIndex = 5;
        TypeChar(tb, '!');

        await Assert.That(oldText).IsEqualTo("Hello");
    }

    #endregion

    #region XAML Loading

    [Test]
    public async Task Xaml_TextBox_ParsesProperties()
    {
        var xaml = """
            <TextBox xmlns="http://schemas.terminalninja.dev/xaml"
                     Text="Hello"
                     IsReadOnly="True"
                     MaxLength="100"
                     PlaceholderText="Enter text..." />
            """;

        var tb = TerminalXaml.Load<TextBox>(xaml);

        await Assert.That(tb.Text).IsEqualTo("Hello");
        await Assert.That(tb.IsReadOnly).IsTrue();
        await Assert.That(tb.MaxLength).IsEqualTo(100);
        await Assert.That(tb.PlaceholderText).IsEqualTo("Enter text...");
    }

    [Test]
    public async Task Xaml_TextBox_AcceptsReturn()
    {
        var xaml = """
            <TextBox xmlns="http://schemas.terminalninja.dev/xaml"
                     AcceptsReturn="True"
                     TextWrapping="Wrap" />
            """;

        var tb = TerminalXaml.Load<TextBox>(xaml);

        await Assert.That(tb.AcceptsReturn).IsTrue();
        await Assert.That(tb.TextWrapping).IsEqualTo(TextWrapping.Wrap);
    }

    #endregion

    #region Helpers

    private static void TypeChar(TextBox tb, char c)
    {
        tb.OnKeyEvent(new KeyEvent(ConsoleKey.A, c, false, false, false));
    }

    #endregion
}
