namespace TerminalNinja.Tests.Unit.Controls;

public class FolderPickerTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "testroot");
    private static readonly string DocsDir = Path.Combine(Root, "Documents");

    private static MockFileSystem CreateMockFs()
    {
        var fs = new MockFileSystem(Root);
        fs.AddDirectory(Root, "Documents");
        fs.AddDirectory(Root, "Downloads");
        fs.AddDirectory(DocsDir, "Work");
        return fs;
    }

    #region Default Values

    [Test]
    public async Task Title_Default_IsSelectFolder()
    {
        var fp = new FolderPicker(CreateMockFs());
        await Assert.That(fp.Title).IsEqualTo("Select Folder");
    }

    [Test]
    public async Task SelectedPath_Default_IsNull()
    {
        var fp = new FolderPicker(CreateMockFs());
        await Assert.That(fp.SelectedPath).IsNull();
    }

    #endregion

    #region Navigation

    [Test]
    public async Task Enter_OnDirectory_NavigatesIn()
    {
        var fp = new FolderPicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.DownArrow, '\0', false, false, false)); // Documents/
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(fp.InitialDirectory).IsEqualTo(DocsDir);
    }

    [Test]
    public async Task Backspace_GoesUp()
    {
        var fs = CreateMockFs();
        var fp = new FolderPicker(fs) { InitialDirectory = DocsDir };

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Backspace, '\0', false, false, false));

        await Assert.That(fp.InitialDirectory).IsEqualTo(Root);
    }

    [Test]
    public async Task Space_SelectsCurrentDirectory()
    {
        var fp = new FolderPicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Spacebar, ' ', false, false, false));

        await Assert.That(fp.SelectedPath).IsEqualTo(Root);
        await Assert.That(fp.DialogResult).IsNotNull();
        await Assert.That(fp.DialogResult!.Value).IsTrue();
    }

    [Test]
    public async Task Escape_Cancels()
    {
        var fp = new FolderPicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Escape, '\0', false, false, false));

        await Assert.That(fp.SelectedPath).IsNull();
        await Assert.That(fp.DialogResult).IsNotNull();
        await Assert.That(fp.DialogResult!.Value).IsFalse();
    }

    #endregion

    #region No Files Shown

    [Test]
    public async Task Entries_DoNotContainFiles()
    {
        var fs = new MockFileSystem(Root);
        fs.AddDirectory(Root, "docs");
        fs.AddFile(Root, "secret.txt"); // should NOT appear

        var fp = new FolderPicker(fs);

        // Render to populate
        using var buffer = new CellBuffer(60, 18);
        fp.Render(buffer, new Rect(0, 0, 60, 18));

        // Check that "secret.txt" doesn't appear in any cell
        var found = false;
        for (var y = 0; y < 18; y++)
        {
            if (buffer.GetCell(2, y).Character == 's' && buffer.GetCell(3, y).Character == 'e')
                found = true;
        }
        await Assert.That(found).IsFalse();
    }

    #endregion

    #region Rendering

    [Test]
    public async Task Render_DoesNotThrow()
    {
        var fp = new FolderPicker(CreateMockFs());

        using var buffer = new CellBuffer(60, 18);
        fp.Render(buffer, new Rect(0, 0, 60, 18));

        await Assert.That(buffer.GetCell(0, 0).Character).IsNotEqualTo('\0');
    }

    #endregion

    #region Search Mode

    [Test]
    public async Task Slash_EntersSearchMode()
    {
        var fp = new FolderPicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Oem2, '/', false, false, false));

        await Assert.That(fp.IsSearchMode).IsTrue();
        await Assert.That(fp.SearchQuery).IsEqualTo("");
    }

    [Test]
    public async Task Escape_InSearchMode_ExitsSearch()
    {
        var fp = new FolderPicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Oem2, '/', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Escape, '\0', false, false, false));

        await Assert.That(fp.IsSearchMode).IsFalse();
    }

    [Test]
    public async Task Typing_InSearchMode_UpdatesQuery()
    {
        var fp = new FolderPicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Oem2, '/', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.D, 'd', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.O, 'o', false, false, false));

        await Assert.That(fp.SearchQuery).IsEqualTo("do");
    }

    [Test]
    public async Task Backspace_EmptyQuery_ExitsSearch()
    {
        var fp = new FolderPicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Oem2, '/', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Backspace, '\0', false, false, false));

        await Assert.That(fp.IsSearchMode).IsFalse();
    }

    [Test]
    public async Task Search_EnterOnDirectory_NavigatesIn()
    {
        var fp = new FolderPicker(CreateMockFs());

        // Search for "doc" — should match "Documents/"
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Oem2, '/', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.D, 'd', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.O, 'o', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.C, 'c', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(fp.IsSearchMode).IsFalse();
        await Assert.That(fp.InitialDirectory).IsEqualTo(DocsDir);
    }

    [Test]
    public async Task Render_InSearchMode_DoesNotThrow()
    {
        var fp = new FolderPicker(CreateMockFs());

        fp.OnKeyEvent(new KeyEvent(ConsoleKey.Oem2, '/', false, false, false));
        fp.OnKeyEvent(new KeyEvent(ConsoleKey.D, 'd', false, false, false));

        using var buffer = new CellBuffer(60, 18);
        fp.Render(buffer, new Rect(0, 0, 60, 18));

        await Assert.That(buffer.GetCell(0, 0).Character).IsNotEqualTo('\0');
    }

    #endregion

    #region Mouse

    [Test]
    public async Task LeftClick_SelectsRow()
    {
        var fp = new FolderPicker(CreateMockFs());

        using var buffer = new CellBuffer(60, 18);
        fp.Render(buffer, new Rect(0, 0, 60, 18));

        // Click on row 1 (first directory after "..")
        fp.OnMouseEvent(new MouseEvent(10, 4, MouseButton.Left, MouseAction.Press));

        await Assert.That(fp.SelectedPath).IsNull(); // clicked, not activated
    }

    [Test]
    public async Task DoubleClick_NavigatesIntoDirectory()
    {
        var fp = new FolderPicker(CreateMockFs());

        using var buffer = new CellBuffer(60, 18);
        fp.Render(buffer, new Rect(0, 0, 60, 18));

        // Double-click on row 1 (Documents/) — Y=4 (listY=3, row 1 = Y 4)
        fp.OnMouseEvent(new MouseEvent(10, 4, MouseButton.Left, MouseAction.Press));
        fp.OnMouseEvent(new MouseEvent(10, 4, MouseButton.Left, MouseAction.Press));

        await Assert.That(fp.InitialDirectory).IsEqualTo(DocsDir);
    }

    [Test]
    public async Task ScrollDown_MovesSelection()
    {
        var fp = new FolderPicker(CreateMockFs());

        using var buffer = new CellBuffer(60, 18);
        fp.Render(buffer, new Rect(0, 0, 60, 18));

        fp.OnMouseEvent(new MouseEvent(10, 5, MouseButton.None, MouseAction.ScrollDown));

        await Assert.That(fp.SelectedPath).IsNull();
    }

    #endregion
}
