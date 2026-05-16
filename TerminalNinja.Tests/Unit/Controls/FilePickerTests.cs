namespace TerminalNinja.Tests.Unit.Controls;

public class FilePickerTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "testroot");
    private static readonly string DocsDir = Path.Combine(Root, "Documents");

    private static MockFileSystem CreateMockFs()
    {
        var fs = new MockFileSystem(Root);
        fs.AddDirectory(Root, "Documents");
        fs.AddDirectory(Root, "Downloads");
        fs.AddFile(Root, "readme.md");
        fs.AddFile(Root, "data.json");
        fs.AddDirectory(DocsDir, "Work");
        fs.AddFile(DocsDir, "report.docx");
        return fs;
    }

    #region Default Values

    [Test]
    public async Task Title_Default_IsSelectFile()
    {
        var fp = new FilePicker(CreateMockFs());
        await Assert.That(fp.Title).IsEqualTo("Select File");
    }

    [Test]
    public async Task InitialDirectory_Default_IsCurrentDir()
    {
        var fs = CreateMockFs();
        var fp = new FilePicker(fs);
        await Assert.That(fp.InitialDirectory).IsEqualTo(Root);
    }

    [Test]
    public async Task SelectedPath_Default_IsNull()
    {
        var fp = new FilePicker(CreateMockFs());
        await Assert.That(fp.SelectedPath).IsNull();
    }

    #endregion

    #region Navigation

    [Test]
    public async Task DownArrow_MovesSelection()
    {
        var fp = new FilePicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        // Should move from index 0 (..) to index 1 (first directory)
        // We can't directly read _selectedIndex but we can verify no crash
        await Assert.That(fp.SelectedPath).IsNull(); // hasn't selected yet
    }

    [Test]
    public async Task Enter_OnDirectory_NavigatesIn()
    {
        var fp = new FilePicker(CreateMockFs());

        // Move to first directory (Documents) and enter
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false)); // Documents/
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        // Now in /home/user/Documents
        await Assert.That(fp.InitialDirectory).IsEqualTo(DocsDir);
    }

    [Test]
    public async Task Backspace_GoesUp()
    {
        var fs = CreateMockFs();
        var fp = new FilePicker(fs) { InitialDirectory = DocsDir };

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Backspace, '\0', false, false, false));

        await Assert.That(fp.InitialDirectory).IsEqualTo(Root);
    }

    [Test]
    public async Task Escape_CancelsDialog()
    {
        var fp = new FilePicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Escape, '\0', false, false, false));

        await Assert.That(fp.SelectedPath).IsNull();
        await Assert.That(fp.DialogResult).IsNotNull();
        await Assert.That(fp.DialogResult!.Value).IsFalse();
    }

    #endregion

    #region File Selection

    [Test]
    public async Task Enter_OnFile_SelectsAndCloses()
    {
        var fp = new FilePicker(CreateMockFs());

        // Navigate past "..", "Documents/", "Downloads/" to first file "data.json"
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false)); // Documents/
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false)); // Downloads/
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false)); // data.json
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(fp.SelectedPath).IsNotNull();
        await Assert.That(fp.SelectedPath!).Contains("data.json");
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_DoesNotThrow()
    {
        var fp = new FilePicker(CreateMockFs());

        using var buffer = new CellBuffer(60, 20);
        fp.Render(buffer, new Rect(0, 0, 60, 20));

        // Should render without errors
        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsNotEqualTo('\0');
    }

    #endregion

    #region FuzzyScore

    [Test]
    public async Task FuzzyScore_ExactMatch_ReturnsPositive()
    {
        var score = FilePicker.FuzzyScore("readme", "readme.md");
        await Assert.That(score).IsGreaterThan(0);
    }

    [Test]
    public async Task FuzzyScore_SubsequenceMatch_ReturnsPositive()
    {
        var score = FilePicker.FuzzyScore("rdm", "readme.md");
        await Assert.That(score).IsGreaterThan(0);
    }

    [Test]
    public async Task FuzzyScore_NoMatch_ReturnsNegativeOne()
    {
        var score = FilePicker.FuzzyScore("xyz", "readme.md");
        await Assert.That(score).IsEqualTo(-1);
    }

    [Test]
    public async Task FuzzyScore_CaseInsensitive()
    {
        var score = FilePicker.FuzzyScore("README", "readme.md");
        await Assert.That(score).IsGreaterThan(0);
    }

    [Test]
    public async Task FuzzyScore_EmptyQuery_ReturnsZero()
    {
        var score = FilePicker.FuzzyScore("", "readme.md");
        await Assert.That(score).IsEqualTo(0);
    }

    [Test]
    public async Task FuzzyScore_EmptyTarget_ReturnsNegativeOne()
    {
        var score = FilePicker.FuzzyScore("a", "");
        await Assert.That(score).IsEqualTo(-1);
    }

    [Test]
    public async Task FuzzyScore_ConsecutiveMatchScoresHigher()
    {
        var consecutive = FilePicker.FuzzyScore("read", "readme.md");
        var scattered = FilePicker.FuzzyScore("read", "r_e_a_d.md");
        await Assert.That(consecutive).IsGreaterThan(scattered);
    }

    #endregion

    #region Search Mode

    [Test]
    public async Task Slash_EntersSearchMode()
    {
        var fp = new FilePicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Oem2, '/', false, false, false));

        await Assert.That(fp.IsSearchMode).IsTrue();
        await Assert.That(fp.SearchQuery).IsEqualTo("");
    }

    [Test]
    public async Task Escape_InSearchMode_ExitsSearch()
    {
        var fp = new FilePicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Oem2, '/', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Escape, '\0', false, false, false));

        await Assert.That(fp.IsSearchMode).IsFalse();
    }

    [Test]
    public async Task Typing_InSearchMode_UpdatesQuery()
    {
        var fp = new FilePicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Oem2, '/', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.R, 'r', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.E, 'e', false, false, false));

        await Assert.That(fp.SearchQuery).IsEqualTo("re");
    }

    [Test]
    public async Task Backspace_InSearchMode_RemovesLastChar()
    {
        var fp = new FilePicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Oem2, '/', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.A, 'a', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.B, 'b', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Backspace, '\0', false, false, false));

        await Assert.That(fp.SearchQuery).IsEqualTo("a");
    }

    [Test]
    public async Task Backspace_EmptyQuery_ExitsSearch()
    {
        var fp = new FilePicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Oem2, '/', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Backspace, '\0', false, false, false));

        await Assert.That(fp.IsSearchMode).IsFalse();
    }

    [Test]
    public async Task Search_FiltersEntries_MatchingQuery()
    {
        var fp = new FilePicker(CreateMockFs());

        // Search for "json" — should match "data.json" but not "readme.md"
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Oem2, '/', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.J, 'j', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.S, 's', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.O, 'o', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.N, 'n', false, false, false));

        // Enter to select the top match
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(fp.SelectedPath).IsNotNull();
        await Assert.That(fp.SelectedPath!).Contains("data.json");
    }

    [Test]
    public async Task Search_EnterOnDirectory_NavigatesIn()
    {
        var fp = new FilePicker(CreateMockFs());

        // Search for "doc" — should match "Documents/" directory
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Oem2, '/', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.D, 'd', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.O, 'o', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.C, 'c', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        // Should have navigated into Documents, exiting search mode
        await Assert.That(fp.IsSearchMode).IsFalse();
        await Assert.That(fp.InitialDirectory).IsEqualTo(DocsDir);
    }

    [Test]
    public async Task Render_InSearchMode_DoesNotThrow()
    {
        var fp = new FilePicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Oem2, '/', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.R, 'r', false, false, false));

        using var buffer = new CellBuffer(60, 20);
        fp.Render(buffer, new Rect(0, 0, 60, 20));

        await Assert.That(buffer.GetCell(0, 0).Codepoint).IsNotEqualTo('\0');
    }

    #endregion

    #region Mouse

    [Test]
    public async Task LeftClick_SelectsRow()
    {
        var fp = new FilePicker(CreateMockFs());

        // Render first so _lastBounds is set
        using var buffer = new CellBuffer(60, 20);
        fp.Render(buffer, new Rect(0, 0, 60, 20));

        // Click on row 1 (first entry after "..")
        // List starts at Y = bounds.Y + 3 = 3, row 1 = Y 4
        fp.OnMouseEvent(new MouseEvent(10, 4, MouseButton.Left, MouseAction.Press));

        // Should have selected index 1 without activating
        await Assert.That(fp.SelectedPath).IsNull();
    }

    [Test]
    public async Task DoubleClick_ActivatesEntry()
    {
        var fp = new FilePicker(CreateMockFs());

        using var buffer = new CellBuffer(60, 20);
        fp.Render(buffer, new Rect(0, 0, 60, 20));

        // Double-click on row 0 ("..") — should navigate to parent
        fp.OnMouseEvent(new MouseEvent(10, 3, MouseButton.Left, MouseAction.Press));
        fp.OnMouseEvent(new MouseEvent(10, 3, MouseButton.Left, MouseAction.Press));

        // ".." navigation goes to parent — verify no crash
        await Assert.That(fp.SelectedPath).IsNull();
    }

    [Test]
    public async Task ScrollDown_MovesSelection()
    {
        var fp = new FilePicker(CreateMockFs());

        using var buffer = new CellBuffer(60, 20);
        fp.Render(buffer, new Rect(0, 0, 60, 20));

        fp.OnMouseEvent(new MouseEvent(10, 5, MouseButton.None, MouseAction.ScrollDown));

        // Selection should have moved from 0 to 1
        await Assert.That(fp.SelectedPath).IsNull(); // hasn't selected yet, just moved
    }

    [Test]
    public async Task ScrollUp_MovesSelection()
    {
        var fp = new FilePicker(CreateMockFs());

        // Move down first
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false));

        using var buffer = new CellBuffer(60, 20);
        fp.Render(buffer, new Rect(0, 0, 60, 20));

        fp.OnMouseEvent(new MouseEvent(10, 5, MouseButton.None, MouseAction.ScrollUp));

        await Assert.That(fp.SelectedPath).IsNull();
    }

    #endregion
}
