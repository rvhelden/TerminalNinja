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
        await Assert.That(buffer.GetCell(0, 0).Character).IsNotEqualTo('\0');
    }

    #endregion
}
