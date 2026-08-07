using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.IO;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A modal dialog for browsing and selecting files from the filesystem.
/// Shows a list of directories and files with keyboard navigation.
/// Press <c>/</c> to activate fuzzy search — type to filter entries in real-time.
/// Click to select, double-click to open. Scroll wheel navigates the list.
/// Use <see cref="ShowAsync"/> for a convenient static API.
/// </summary>
public sealed class FilePicker : Window
{
    private readonly IFileSystem _fileSystem;
    private string _currentPath;
    private readonly List<string> _entries = [];
    private int _selectedIndex;
    private int _scrollOffset;

    // ─── Fuzzy search state ──────────────────────────────────────────
    private bool _searchMode;
    private string _searchQuery = "";
    private List<string> _filteredEntries = [];

    // ─── Mouse state ─────────────────────────────────────────────────
    private Rect _lastBounds;
    private DateTime _lastClickTime;
    private int _lastClickY = -1;

    public FilePicker(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new RealFileSystem();
        _currentPath = _fileSystem.GetCurrentDirectory();
        DefaultStyleKey = typeof(FilePicker);
        // This dialog drives its own list from OnKeyEvent, so focus must stay on the window.
        FocusesContentOnOpen = false;
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

    /// <summary>Whether the picker is currently in fuzzy search mode.</summary>
    public bool IsSearchMode => _searchMode;

    /// <summary>The current fuzzy search query.</summary>
    public string SearchQuery => _searchQuery;

    // ─── Visible entries ─────────────────────────────────────────────

    private List<string> VisibleEntries => _searchMode ? _filteredEntries : _entries;

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

        ExitSearch();
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
        var visible = VisibleEntries;
        if (_selectedIndex < 0 || _selectedIndex >= visible.Count) return;

        var entry = visible[_selectedIndex];
        var wasSearching = _searchMode;
        if (wasSearching) ExitSearch();

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

    // ─── Fuzzy Search ────────────────────────────────────────────────

    private void EnterSearch()
    {
        _searchMode = true;
        _searchQuery = "";
        ApplySearch();
    }

    private void ExitSearch()
    {
        _searchMode = false;
        _searchQuery = "";
        _filteredEntries.Clear();
        _selectedIndex = 0;
        _scrollOffset = 0;
    }

    private void ApplySearch()
    {
        _filteredEntries.Clear();

        // ".." is always available as the first entry
        _filteredEntries.Add("..");

        if (_searchQuery.Length == 0)
        {
            // Empty query — show all entries (except ".." which is already added)
            for (var i = 1; i < _entries.Count; i++)
                _filteredEntries.Add(_entries[i]);
        }
        else
        {
            // Score and filter entries (skip ".." at index 0)
            var scored = new List<(string Entry, int Score)>();
            for (var i = 1; i < _entries.Count; i++)
            {
                var score = FuzzyScore(_searchQuery, EntryDisplayName(_entries[i]));
                if (score >= 0)
                    scored.Add((_entries[i], score));
            }

            // Sort by score descending (best matches first)
            scored.Sort((a, b) => b.Score.CompareTo(a.Score));
            foreach (var (entry, _) in scored)
                _filteredEntries.Add(entry);
        }

        _selectedIndex = _filteredEntries.Count > 1 ? 1 : 0; // Select first match (skip "..")
        _scrollOffset = 0;
    }

    /// <summary>
    /// Extracts the display name from an entry string for fuzzy matching.
    /// Strips the folder icon prefix, leading whitespace, and trailing slash.
    /// </summary>
    internal static string EntryDisplayName(string entry)
    {
        if (entry.StartsWith("\uF07B "))
            return entry[2..].TrimEnd('/'); // strip icon + trailing /
        return entry.TrimStart(); // strip leading spaces for files
    }

    /// <summary>
    /// Scores a fuzzy match of <paramref name="query"/> against <paramref name="target"/>.
    /// Returns -1 if the query does not match. Higher scores indicate better matches.
    /// Rewards consecutive character matches and matches at word boundaries.
    /// </summary>
    internal static int FuzzyScore(string query, string target)
    {
        if (query.Length == 0) return 0;
        if (target.Length == 0) return -1;

        var qi = 0;
        var score = 0;
        var consecutive = 0;

        for (var ti = 0; ti < target.Length && qi < query.Length; ti++)
        {
            if (char.ToLowerInvariant(target[ti]) == char.ToLowerInvariant(query[qi]))
            {
                qi++;
                consecutive++;
                score += consecutive * 2; // Reward consecutive runs: 2 + 4 + 6 + ...

                // Bonus for word boundaries (start, after separator, uppercase in camelCase)
                if (ti == 0 || target[ti - 1] is '/' or '\\' or '.' or '_' or '-' or ' '
                    || (char.IsUpper(target[ti]) && ti > 0 && char.IsLower(target[ti - 1])))
                {
                    score += 3;
                }
            }
            else
            {
                consecutive = 0;
            }
        }

        // All query chars consumed? If not, no match.
        if (qi < query.Length) return -1;

        // Bonus for shorter targets (more precise matches)
        score += Math.Max(0, 20 - target.Length);

        return score;
    }

    // ─── List geometry helpers ────────────────────────────────────────

    private int ListY => _lastBounds.Y + 3; // border + path + separator
    private int ListHeight => _lastBounds.Height - 5; // border + path + sep + sep + hint

    /// <summary>
    /// Converts a screen Y coordinate to a visible entry index, or -1 if outside the list.
    /// </summary>
    private int HitTestRow(int screenY)
    {
        var row = screenY - ListY;
        if (row < 0 || row >= ListHeight) return -1;
        var idx = _scrollOffset + row;
        return idx < VisibleEntries.Count ? idx : -1;
    }

    // ─── Rendering ───────────────────────────────────────────────────

    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        _lastBounds = bounds;
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
            SetCharSafe(buffer, x, pathY + 1, '\u2500', DimColor(Foreground), Background);

        // File list
        var visible = VisibleEntries;
        var listY = pathY + 2;
        var listHeight = bounds.Height - 5; // border + path + sep + bottom

        // Ensure selected visible
        if (_selectedIndex < _scrollOffset) _scrollOffset = _selectedIndex;
        if (_selectedIndex >= _scrollOffset + listHeight) _scrollOffset = _selectedIndex - listHeight + 1;

        for (var i = 0; i < listHeight && _scrollOffset + i < visible.Count; i++)
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

            var text = visible[idx];
            for (var c = 0; c < text.Length && c < bounds.Width - 2; c++)
                SetCharSafe(buffer, bounds.X + 1 + c, y, text[c], fg, bg);
        }

        // Bottom separator
        var btnY = bounds.Bottom - 2;
        for (var x = bounds.X + 1; x < bounds.Right - 1; x++)
            SetCharSafe(buffer, x, btnY - 1, '\u2500', DimColor(Foreground), Background);

        // Bottom bar: search prompt or hint text
        if (_searchMode)
        {
            var searchText = $"/ {_searchQuery}\u2588"; // block cursor
            var countText = $" ({visible.Count - 1})"; // exclude ".."
            var maxQuery = bounds.Width - 2 - countText.Length;
            if (searchText.Length > maxQuery)
                searchText = searchText[..maxQuery];

            for (var i = 0; i < searchText.Length; i++)
                SetCharSafe(buffer, bounds.X + 1 + i, btnY, searchText[i], Foreground, Background);
            for (var i = 0; i < countText.Length; i++)
                SetCharSafe(buffer, bounds.Right - 1 - countText.Length + i, btnY, countText[i], DimColor(Foreground), Background);
        }
        else
        {
            var hintText = " / search  \u23ce select  esc cancel";
            for (var i = 0; i < hintText.Length && i < bounds.Width - 2; i++)
                SetCharSafe(buffer, bounds.X + 1 + i, btnY, hintText[i], DimColor(Foreground), Background);
        }
    }

    // ─── Input ───────────────────────────────────────────────────────

    public override bool OnKeyEvent(KeyEvent e)
    {
        if (_searchMode)
        {
            HandleSearchInput(e);
        }
        else
        {
            HandleNormalInput(e);
        }
        InvalidateVisual();

        // A modal picker owns the keyboard for as long as it is open — it browses with the
        // arrows, filters on any character and closes on Escape — so every key is claimed.
        return true;
    }

    public override void OnMouseEvent(MouseEvent e)
    {
        switch (e.Action)
        {
            case MouseAction.Press when e.Button == MouseButton.Left:
            {
                var idx = HitTestRow(e.Y);
                if (idx < 0) break;

                var now = DateTime.UtcNow;
                if ((now - _lastClickTime).TotalMilliseconds < 500 && e.Y == _lastClickY)
                {
                    // Double-click — activate the entry
                    _selectedIndex = idx;
                    NavigateToEntry();
                    _lastClickTime = DateTime.MinValue;
                }
                else
                {
                    // Single click — select the row
                    _selectedIndex = idx;
                    _lastClickTime = now;
                    _lastClickY = e.Y;
                }
                break;
            }

            case MouseAction.ScrollUp:
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                break;

            case MouseAction.ScrollDown:
                _selectedIndex = Math.Min(VisibleEntries.Count - 1, _selectedIndex + 1);
                break;
        }

        InvalidateVisual();
    }

    private void HandleNormalInput(KeyEvent e)
    {
        switch (e)
        {
            case { Key: ConsoleKey.UpArrow }:
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                break;
            case { Key: ConsoleKey.DownArrow }:
                _selectedIndex = Math.Min(_entries.Count - 1, _selectedIndex + 1);
                break;
            case { Key: ConsoleKey.Home }:
                _selectedIndex = 0;
                break;
            case { Key: ConsoleKey.End }:
                _selectedIndex = _entries.Count - 1;
                break;
            case { Key: ConsoleKey.Enter }:
                NavigateToEntry();
                break;
            case { Key: ConsoleKey.Backspace }:
                var parent = _fileSystem.GetDirectoryName(_currentPath);
                if (parent != null) { _currentPath = parent; RefreshEntries(); }
                break;
            case { Key: ConsoleKey.Escape }:
                SelectedPath = null;
                DialogResult = false;
                break;
            default:
                // '/' activates search (only when no modifiers)
                if (e.KeyChar == '/' && !e.HasModifiers)
                    EnterSearch();
                break;
        }
    }

    private void HandleSearchInput(KeyEvent e)
    {
        var visible = VisibleEntries;

        switch (e)
        {
            case { Key: ConsoleKey.Escape }:
                ExitSearch();
                break;
            case { Key: ConsoleKey.Enter }:
                NavigateToEntry();
                break;
            case { Key: ConsoleKey.UpArrow }:
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                break;
            case { Key: ConsoleKey.DownArrow }:
                _selectedIndex = Math.Min(visible.Count - 1, _selectedIndex + 1);
                break;
            case { Key: ConsoleKey.Backspace }:
                if (_searchQuery.Length > 0)
                {
                    _searchQuery = _searchQuery[..^1];
                    ApplySearch();
                }
                else
                {
                    ExitSearch();
                }
                break;
            default:
                // Append printable characters to the search query
                if (e.KeyChar >= ' ')
                {
                    _searchQuery += e.KeyChar;
                    ApplySearch();
                }
                break;
        }
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
