namespace NinjaShellUi;

/// <summary>
/// Up/Down history navigation for <see cref="ReplView"/>. The view owns the live
/// <see cref="InputBuffer"/>; this type just remembers submitted entries and tracks
/// the navigation cursor. <see cref="Navigate"/> returns the entry the caller should
/// load (or <c>null</c> when the user has navigated past the newest entry, signalling
/// "restore an empty buffer").
/// </summary>
internal sealed class InputHistory
{
    private readonly List<string> _entries = new();
    private int _index = -1;

    public int Count => _entries.Count;

    /// <summary>Append a submitted line and reset the navigation cursor.</summary>
    public void Push(string line)
    {
        _entries.Add(line);
        _index = -1;
    }

    /// <summary>Reset the navigation cursor without dropping entries.</summary>
    public void ResetCursor() => _index = -1;

    /// <summary>
    /// Walk the history list. <paramref name="direction"/> is -1 (older) or +1 (newer).
    /// Returns the entry text to load, or <c>null</c> when navigation lands past the
    /// newest entry (caller restores an empty buffer). Returns <c>null</c> for an empty
    /// history too — the caller is free to interpret that as "no-op".
    /// </summary>
    public string? Navigate(int direction)
    {
        if (_entries.Count == 0) return null;

        if (_index == -1 && direction < 0)
        {
            _index = _entries.Count - 1;
        }
        else if (_index >= 0)
        {
            _index = Math.Clamp(_index + direction, -1, _entries.Count - 1);
        }

        return _index >= 0 && _index < _entries.Count ? _entries[_index] : null;
    }
}
