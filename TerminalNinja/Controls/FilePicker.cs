using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.IO;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A modal dialog for browsing and selecting files from the filesystem.
/// Shows a list of directories and files with keyboard navigation.
/// Use <see cref="ShowAsync"/> for a convenient static API.
/// </summary>
public sealed class FilePicker : Window
{
    private readonly IFileSystem _fileSystem;
    private string _currentPath;
    private readonly List<string> _entries = [];
    private int _selectedIndex;
    private int _scrollOffset;

    public FilePicker(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new RealFileSystem();
        _currentPath = _fileSystem.GetCurrentDirectory();
        DefaultStyleKey = typeof(FilePicker);
        Title = "Select File";
        Width = Size.Absolute(50);
        Height = Size.Absolute(18);
        RefreshEntries();
    }

    // ─── Properties ──────────────────────────────────────────────────

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

    /// <summary>Gets the selected file path after the dialog closes, or null if cancelled.</summary>
    public string? SelectedPath { get; private set; }

    /// <summary>Gets or sets a file filter (e.g., "*.txt"). Null means all files.</summary>
    public string? Filter { get; set; }

    // ─── Entry Management ────────────────────────────────────────────

    private void RefreshEntries()
    {
        _entries.Clear();
        _entries.Add("..");

        foreach (var dir in _fileSystem.GetDirectories(_currentPath).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            _entries.Add("\uF07B " + dir + "/"); // nf-fa-folder
        }

        var files = _fileSystem.GetFiles(_currentPath).OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (Filter == null || MatchesFilter(file, Filter))
                _entries.Add("  " + file);
        }

        _selectedIndex = 0;
        _scrollOffset = 0;
        InvalidateVisual();
    }

    private static bool MatchesFilter(string fileName, string filter)
    {
        if (string.IsNullOrEmpty(filter) || filter == "*.*" || filter == "*") return true;
        var ext = filter.StartsWith("*.") ? filter[1..] : filter;
        return fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
    }

    private void NavigateToEntry()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _entries.Count) return;

        var entry = _entries[_selectedIndex];
        if (entry == "..")
        {
            var parent = _fileSystem.GetDirectoryName(_currentPath);
            if (parent != null)
            {
                _currentPath = parent;
                RefreshEntries();
            }
        }
        else if (entry.Contains('/'))
        {
            // Directory entry — strip icon prefix and trailing /
            var dirName = entry.Replace("\uF07B ", "").TrimEnd('/');
            var newPath = Path.Combine(_currentPath, dirName);
            if (_fileSystem.DirectoryExists(newPath))
            {
                _currentPath = newPath;
                RefreshEntries();
            }
        }
        else
        {
            // File entry — select and close
            var fileName = entry.TrimStart();
            SelectedPath = Path.Combine(_currentPath, fileName);
            DialogResult = true;
        }
    }

    // ─── Rendering ───────────────────────────────────────────────────

    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;

        buffer.FillRect(clipped, new Cell(' ', Foreground, Background));

        // Border
        var border = Styling.BorderStyle.Rounded(Foreground);
        DrawBorder(buffer, bounds, border.Chars);

        // Title
        var titleText = $" {Title} ";
        var titleX = bounds.X + (bounds.Width - titleText.Length) / 2;
        for (var i = 0; i < titleText.Length; i++)
            SetCharSafe(buffer, titleX + i, bounds.Y, titleText[i], Foreground, Background);

        // Path
        var pathY = bounds.Y + 1;
        var pathText = _currentPath.Length > bounds.Width - 4 ? "..." + _currentPath[^(bounds.Width - 7)..] : _currentPath;
        for (var i = 0; i < pathText.Length && i < bounds.Width - 2; i++)
            SetCharSafe(buffer, bounds.X + 1 + i, pathY, pathText[i], Foreground, Background);

        // Separator
        for (var x = bounds.X + 1; x < bounds.Right - 1; x++)
            SetCharSafe(buffer, x, pathY + 1, '─', DimColor(Foreground), Background);

        // File list
        var listY = pathY + 2;
        var listHeight = bounds.Height - 5; // border + path + sep + buttons

        // Ensure selected visible
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
            {
                var rowRect = new Rect(bounds.X + 1, y, bounds.Width - 2, 1);
                buffer.FillRect(rowRect, new Cell(' ', fg, bg));
            }

            var text = _entries[idx];
            for (var c = 0; c < text.Length && c < bounds.Width - 2; c++)
                SetCharSafe(buffer, bounds.X + 1 + c, y, text[c], fg, bg);
        }

        // Bottom separator + buttons
        var btnY = bounds.Bottom - 2;
        for (var x = bounds.X + 1; x < bounds.Right - 1; x++)
            SetCharSafe(buffer, x, btnY - 1, '─', DimColor(Foreground), Background);

        var okText = "[ OK ]";
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
            case ConsoleKey.Home:
                _selectedIndex = 0;
                break;
            case ConsoleKey.End:
                _selectedIndex = _entries.Count - 1;
                break;
            case ConsoleKey.Enter:
                NavigateToEntry();
                break;
            case ConsoleKey.Backspace:
                var parent = _fileSystem.GetDirectoryName(_currentPath);
                if (parent != null) { _currentPath = parent; RefreshEntries(); }
                break;
            case ConsoleKey.Escape:
                SelectedPath = null;
                DialogResult = false;
                break;
        }
        InvalidateVisual();
    }

    // ─── Static API ──────────────────────────────────────────────────

    /// <summary>
    /// Shows a file picker dialog and returns the selected file path, or null if cancelled.
    /// </summary>
    public static async Task<string?> ShowAsync(string? initialDirectory = null, string? filter = null, IFileSystem? fileSystem = null)
    {
        var picker = new FilePicker(fileSystem);
        if (initialDirectory != null) picker.InitialDirectory = initialDirectory;
        if (filter != null) picker.Filter = filter;
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
