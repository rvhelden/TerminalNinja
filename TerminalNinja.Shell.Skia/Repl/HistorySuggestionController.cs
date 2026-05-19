namespace TerminalNinja.Shell.Skia;

/// <summary>
/// Fish-shell-style inline autosuggestion: as the user types a prefix, surface the most
/// recently-used history entry that starts with it as ghost text after the cursor.
/// Tab accepts the whole suggestion, Right arrow accepts one word, Up/Down arrows cycle
/// alternative matches. Cycling preserves the prefix so the user can flip between
/// candidates without losing what they typed.
/// </summary>
internal sealed class HistorySuggestionController
{
    private readonly InputHistory _history;
    private string _prefix = string.Empty;
    private IReadOnlyList<string> _candidates = Array.Empty<string>();
    private int _index;

    public HistorySuggestionController(InputHistory history)
    {
        _history = history;
    }

    public bool HasSuggestion => _candidates.Count > 0;

    /// <summary>The full history entry currently surfaced as the suggestion, or null.</summary>
    public string? CurrentSuggestion => HasSuggestion ? _candidates[_index] : null;

    /// <summary>The portion of <see cref="CurrentSuggestion"/> after the current prefix — what gets painted as ghost text.</summary>
    public string GhostSuffix
    {
        get
        {
            var s = CurrentSuggestion;
            return s is null ? string.Empty : s[_prefix.Length..];
        }
    }

    /// <summary>
    /// Recompute candidates against <paramref name="prefix"/>. When the previously-shown
    /// suggestion still matches the new prefix it stays selected, so the ghost text doesn't
    /// jump around as the user types into the existing match.
    /// </summary>
    public void Refresh(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            Clear();
            return;
        }

        var previous = CurrentSuggestion;
        var matches = _history.FindByPrefix(prefix);
        _prefix = prefix;
        _candidates = matches;
        _index = 0;
        if (previous is not null)
        {
            for (var i = 0; i < matches.Count; i++)
            {
                if (matches[i] == previous) { _index = i; break; }
            }
        }
    }

    /// <summary>Step to the next (<c>+1</c>) or previous (<c>-1</c>) candidate, wrapping at the ends.</summary>
    public void Cycle(int direction)
    {
        if (_candidates.Count == 0) return;
        var count = _candidates.Count;
        _index = ((_index + direction) % count + count) % count;
    }

    public void Clear()
    {
        _prefix = string.Empty;
        _candidates = Array.Empty<string>();
        _index = 0;
    }

    /// <summary>Returns the full text to load into the buffer when the user accepts the whole suggestion, or null.</summary>
    public string? AcceptAll() => CurrentSuggestion;

    /// <summary>
    /// Returns the text to load when the user accepts only the next word of the ghost.
    /// "Next word" = leading whitespace + the run of non-whitespace + one trailing space,
    /// so successive Right-arrow presses naturally walk space-delimited tokens.
    /// Null when there's no ghost text to consume.
    /// </summary>
    public string? AcceptNextWord()
    {
        var s = CurrentSuggestion;
        if (s is null) return null;
        var suffix = s[_prefix.Length..];
        if (suffix.Length == 0) return null;
        var slice = NextWordSlice(suffix);
        return _prefix + slice;
    }

    private static string NextWordSlice(string suffix)
    {
        var i = 0;
        while (i < suffix.Length && char.IsWhiteSpace(suffix[i])) i++;
        while (i < suffix.Length && !char.IsWhiteSpace(suffix[i])) i++;
        if (i < suffix.Length) i++;
        return suffix[..i];
    }
}
