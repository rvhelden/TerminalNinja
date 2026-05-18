namespace TerminalNinja.Highlighting;

/// <summary>
/// Global lookup table for <see cref="ISyntaxHighlighter"/>s keyed by their
/// <see cref="ISyntaxHighlighter.Language"/> identifier. Pre-seeded with the built-in
/// highlighters; libraries that ship their own (e.g. <c>TerminalNinja.Shell.Language</c>'s
/// NinjaShell highlighter) register on first access from their module initializer.
/// </summary>
/// <remarks>
/// Thread safety: registration is <see cref="System.Threading.ReaderWriterLockSlim"/>-free
/// because the registry is intended to be populated at startup and then read-only for the
/// app's lifetime. Concurrent registers are safe through the lock but concurrent register +
/// lookup races aren't guarded — register everything from module initializers before any
/// editor goes live.
/// </remarks>
public static class SyntaxHighlighterRegistry
{
    private static readonly Lock _lock = new();
    private static readonly Dictionary<string, ISyntaxHighlighter> _highlighters = new(StringComparer.Ordinal);

    static SyntaxHighlighterRegistry()
    {
        Register(new NinjaSyntaxHighlighter());
        Register(new JsonSyntaxHighlighter());
        Register(new XmlSyntaxHighlighter());
    }

    /// <summary>Adds or replaces the highlighter registered under
    /// <see cref="ISyntaxHighlighter.Language"/>.</summary>
    public static void Register(ISyntaxHighlighter highlighter)
    {
        ArgumentNullException.ThrowIfNull(highlighter);
        lock (_lock)
        {
            _highlighters[highlighter.Language] = highlighter;
        }
    }

    /// <summary>Looks up the highlighter for <paramref name="language"/>; returns
    /// <see langword="null"/> if no highlighter is registered.</summary>
    public static ISyntaxHighlighter? Get(string language)
    {
        ArgumentNullException.ThrowIfNull(language);
        lock (_lock)
        {
            return _highlighters.TryGetValue(language, out var h) ? h : null;
        }
    }

    /// <summary>True when a highlighter exists for <paramref name="language"/>.</summary>
    public static bool TryGet(string language, out ISyntaxHighlighter highlighter)
    {
        ArgumentNullException.ThrowIfNull(language);
        lock (_lock)
        {
            if (_highlighters.TryGetValue(language, out var h))
            {
                highlighter = h;
                return true;
            }
        }
        highlighter = null!;
        return false;
    }

    /// <summary>Snapshot of currently registered language ids, sorted alphabetically.</summary>
    public static IReadOnlyList<string> Languages
    {
        get
        {
            lock (_lock)
            {
                var keys = new string[_highlighters.Count];
                _highlighters.Keys.CopyTo(keys, 0);
                Array.Sort(keys, StringComparer.Ordinal);
                return keys;
            }
        }
    }
}
