using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Skia;

/// <summary>
/// Scrollback for <see cref="ReplView"/>. Pairs an append-only line list with a
/// per-line value registry so mouse hover over an output row can resolve back
/// to the originating <see cref="NValue"/>.
/// </summary>
internal sealed class OutputLog
{
    private readonly List<string> _lines = new(capacity: 256);
    private readonly Dictionary<int, NValue> _results = new();

    public int LineCount => _lines.Count;

    public IReadOnlyList<string> Lines => _lines;

    /// <summary>
    /// Append <paramref name="text"/>, splitting on <c>\n</c> and trimming trailing <c>\r</c>.
    /// When <paramref name="value"/> has a value, every produced line is associated with
    /// it so <see cref="TryGetValueAt"/> resolves to the same payload.
    /// </summary>
    /// <returns>The index of the first appended line, or -1 when nothing was added.</returns>
    public int Append(string? text, NValue? value = null)
    {
        if (string.IsNullOrEmpty(text)) return -1;

        int firstLineIndex = _lines.Count;
        foreach (var line in text.Split('\n'))
        {
            _lines.Add(line.TrimEnd('\r'));
        }

        if (value.HasValue)
        {
            var resolved = value.Value;
            for (int i = firstLineIndex; i < _lines.Count; i++)
                _results[i] = resolved;
        }

        return firstLineIndex;
    }

    public bool TryGetValueAt(int lineIndex, out NValue value)
        => _results.TryGetValue(lineIndex, out value!);

    public void Clear()
    {
        _lines.Clear();
        _results.Clear();
    }
}
