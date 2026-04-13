using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.IO;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A modal dialog for browsing and selecting a folder from the filesystem.
/// Shows only directories (no files). Use <see cref="ShowAsync"/> for a convenient static API.
/// </summary>
public sealed class FolderPicker : Window
{
    private readonly IFileSystem _fileSystem;
    private string _currentPath;
    private readonly List<string> _entries = [];
    private int _selectedIndex;
    private int _scrollOffset;

    public FolderPicker(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new RealFileSystem();
        _currentPath = _fileSystem.GetCurrentDirectory();
        DefaultStyleKey = typeof(FolderPicker);
        Title = "Select Folder";
        Width = Size.Absolute(50);
        Height = Size.Absolute(16);
        RefreshEntries();
    }

    /// <summary>Gets or sets the initial directory path.</summary>
    public string InitialDirectory
    {
        get => _currentPath;
        set
        {
            if (_fileSystem.DirectoryExists(value))
            {
                _currentPath = value;
                RefreshEntries();
            }
        }
    }

    /// <summary>Gets the selected folder path after the dialog closes, or null if cancelled.</summary>
    public string? SelectedPath { get; private set; }

    private void RefreshEntries()
    {
        _entries.Clear();
        _entries.Add("..");

        foreach (var dir in _fileSystem.GetDirectories(_currentPath).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            _entries.Add("\uF07B " + dir + "/");
        }

        _selectedIndex = 0;
        _scrollOffset = 0;
        InvalidateVisual();
    }

    private void NavigateToEntry()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _entries.Count) return;

        var entry = _entries[_selectedIndex];
        if (entry == "..")
        {
            var parent = _fileSystem.GetDirectoryName(_currentPath);
            if (parent != null) { _currentPath = parent; RefreshEntries(); }
        }
        else
        {
            var dirName = entry.Replace("\uF07B ", "").TrimEnd('/');
            var newPath = Path.Combine(_currentPath, dirName);
            if (_fileSystem.DirectoryExists(newPath)) { _currentPath = newPath; RefreshEntries(); }
        }
    }

    // ─── Rendering ───────────────────────────────────────────────────

    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;

        buffer.FillRect(clipped, new Cell(' ', Foreground, Background));

        var border = Styling.BorderStyle.Rounded(Foreground);
        DrawBorder(buffer, bounds, border.Chars);

        var titleText = $" {Title} ";
        var titleX = bounds.X + (bounds.Width - titleText.Length) / 2;
        for (var i = 0; i < titleText.Length; i++)
            SetCharSafe(buffer, titleX + i, bounds.Y, titleText[i], Foreground, Background);

        var pathY = bounds.Y + 1;
        var pathText = _currentPath.Length > bounds.Width - 4 ? "..." + _currentPath[^(bounds.Width - 7)..] : _currentPath;
        for (var i = 0; i < pathText.Length && i < bounds.Width - 2; i++)
            SetCharSafe(buffer, bounds.X + 1 + i, pathY, pathText[i], Foreground, Background);

        for (var x = bounds.X + 1; x < bounds.Right - 1; x++)
            SetCharSafe(buffer, x, pathY + 1, '─', DimColor(Foreground), Background);

        var listY = pathY + 2;
        var listHeight = bounds.Height - 5;

        if (_selectedIndex < _scrollOffset) _scrollOffset = _selectedIndex;
        if (_selectedIndex >= _scrollOffset + listHeight) _scrollOffset = _selectedIndex - listHeight + 1;

        for (var i = 0; i < listHeight && _scrollOffset + i < _entries.Count; i++)
        {
            var idx = _scrollOffset + i;
            var y = listY + i;
            var isSelected = idx == _selectedIndex;
            var fg = isSelected ? Background : Foreground;
            var bg = isSelected ? Foreground : Background;

            if (isSelected)
                buffer.FillRect(new Rect(bounds.X + 1, y, bounds.Width - 2, 1), new Cell(' ', fg, bg));

            var text = _entries[idx];
            for (var c = 0; c < text.Length && c < bounds.Width - 2; c++)
                SetCharSafe(buffer, bounds.X + 1 + c, y, text[c], fg, bg);
        }

        var btnY = bounds.Bottom - 2;
        for (var x = bounds.X + 1; x < bounds.Right - 1; x++)
            SetCharSafe(buffer, x, btnY - 1, '─', DimColor(Foreground), Background);

        var okText = "[ Select ]";
        var cancelText = "[ Cancel ]";
        var btnX = bounds.X + bounds.Width / 2 - (okText.Length + cancelText.Length + 2) / 2;
        for (var i = 0; i < okText.Length; i++)
            SetCharSafe(buffer, btnX + i, btnY, okText[i], Foreground, Background);
        for (var i = 0; i < cancelText.Length; i++)
            SetCharSafe(buffer, btnX + okText.Length + 2 + i, btnY, cancelText[i], Foreground, Background);
    }

    // ─── Input ───────────────────────────────────────────────────────

    public override void OnKeyEvent(KeyEvent e)
    {
        switch (e.Key)
        {
            case ConsoleKey.UpArrow:
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                break;
            case ConsoleKey.DownArrow:
                _selectedIndex = Math.Min(_entries.Count - 1, _selectedIndex + 1);
                break;
            case ConsoleKey.Enter:
                NavigateToEntry();
                break;
            case ConsoleKey.Backspace:
                var parent = _fileSystem.GetDirectoryName(_currentPath);
                if (parent != null) { _currentPath = parent; RefreshEntries(); }
                break;
            case ConsoleKey.Spacebar:
                // Select current directory
                SelectedPath = _currentPath;
                DialogResult = true;
                break;
            case ConsoleKey.Escape:
                SelectedPath = null;
                DialogResult = false;
                break;
        }
        InvalidateVisual();
    }

    /// <summary>
    /// Shows a folder picker dialog and returns the selected folder path, or null if cancelled.
    /// </summary>
    public static async Task<string?> ShowAsync(string? initialDirectory = null, IFileSystem? fileSystem = null)
    {
        var picker = new FolderPicker(fileSystem);
        if (initialDirectory != null) picker.InitialDirectory = initialDirectory;
        await picker.ShowDialogAsync();
        return picker.SelectedPath;
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static void SetCharSafe(CellBuffer buf, int x, int y, char c, Color fg, Color bg)
    {
        if (x >= 0 && x < buf.Width && y >= 0 && y < buf.Height) buf.SetChar(x, y, c, fg, bg);
    }

    private void DrawBorder(CellBuffer buf, Rect b, Styling.BorderChars ch)
    {
        for (var x = b.X + 1; x < b.Right - 1; x++) { SetCharSafe(buf, x, b.Y, ch.Horizontal, Foreground, Background); SetCharSafe(buf, x, b.Bottom - 1, ch.Horizontal, Foreground, Background); }
        for (var y = b.Y + 1; y < b.Bottom - 1; y++) { SetCharSafe(buf, b.X, y, ch.Vertical, Foreground, Background); SetCharSafe(buf, b.Right - 1, y, ch.Vertical, Foreground, Background); }
        SetCharSafe(buf, b.X, b.Y, ch.TopLeft, Foreground, Background);
        SetCharSafe(buf, b.Right - 1, b.Y, ch.TopRight, Foreground, Background);
        SetCharSafe(buf, b.X, b.Bottom - 1, ch.BottomLeft, Foreground, Background);
        SetCharSafe(buf, b.Right - 1, b.Bottom - 1, ch.BottomRight, Foreground, Background);
    }

    private static Color DimColor(Color c) => new((byte)(c.R / 2), (byte)(c.G / 2), (byte)(c.B / 2));
}
