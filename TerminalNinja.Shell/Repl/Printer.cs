using System.Collections.Immutable;
using System.Text;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Repl;

/// <summary>
/// Renders an <see cref="NValue"/> for the REPL: scalars raw, list-of-records as
/// an aligned ASCII table, other lists/records compact via <see cref="NValueOps.FormatForDisplay"/>.
/// </summary>
/// <remarks>
/// Two record-level conventions shape the output:
/// <list type="bullet">
///   <item><c>__display</c> — a string field whose value names another field. Used
///   only when a single record is printed on its own: the named field's text becomes
///   the record's representation. <c>fs.ls()</c> entries use this so an individual
///   entry prints as its full path.</item>
///   <item><c>__columns</c> — a list of strings naming the columns (and their order)
///   to surface when this record participates in a table. Read from the first row
///   only; falls back to the key-union of every row when absent or malformed. Used
///   by <c>fs.ls()</c> to pin <c>Icon Name Type SizeText</c> as the default view
///   even though the records carry richer data underneath.</item>
/// </list>
/// </remarks>
public static class Printer
{
    /// <summary>Convention key used to mark a record's canonical text field.</summary>
    public const string DisplayFieldKey = "__display";

    /// <summary>Convention key used to pin the default column order of a record table.</summary>
    public const string ColumnsFieldKey = "__columns";

    /// <summary>Render <paramref name="v"/> for display in the REPL.</summary>
    public static string Format(NValue v)
    {
        if (v is NUnit) return string.Empty;
        if (v is NRecord rec && TryFormatRecordViaDisplayField(rec, out var displayed))
        {
            return displayed;
        }
        if (v is NList list && IsTableShaped(list)) return FormatRecordTable(list);
        if (v is NList strList && IsStringListShaped(strList)) return FormatStringList(strList);
        if (v is NSeq seq)
        {
            // Display is a sink — materialise once into an NList so the
            // table-shape detector can inspect the rows.
            var materialised = new NList(ImmutableArray.CreateRange(seq.Items));
            if (IsTableShaped(materialised)) return FormatRecordTable(materialised);
            if (IsStringListShaped(materialised)) return FormatStringList(materialised);
            return NValueOps.FormatForDisplay(materialised);
        }
        // Multi-line strings are treated as pre-formatted output (obj.dump, obj.table,
        // format_table, JSON pretty-print) and surface without the standard "…" quote
        // wrap that single-line strings get. Single-line strings keep the quotes so
        // they're distinguishable from identifiers / numbers in the REPL.
        if (v is NString s && (s.Value.Contains('\n') || s.Value.Contains('\r')))
            return s.Value;
        return NValueOps.FormatForDisplay(v);
    }

    private static bool TryFormatRecordViaDisplayField(NRecord rec, out string result)
    {
        result = string.Empty;
        if (!rec.Fields.TryGetValue(DisplayFieldKey, out var v) || v is not NString s) return false;
        if (!rec.Fields.TryGetValue(s.Value, out var target)) return false;
        result = NValueOps.FormatForInterpolation(target);
        return true;
    }

    /// <summary>
    /// Try to read a <see cref="ColumnsFieldKey"/> hint off <paramref name="row"/>.
    /// Must be a non-empty list of strings; anything else returns null so
    /// <see cref="FormatRecordTable"/> falls back to the key-union default.
    /// </summary>
    private static string[]? TryReadColumnHint(NValue row)
    {
        if (row is not NRecord rec) return null;
        if (!rec.Fields.TryGetValue(ColumnsFieldKey, out var raw)) return null;
        if (raw is not NList l || l.Items.Length == 0) return null;
        var cols = new string[l.Items.Length];
        for (int i = 0; i < l.Items.Length; i++)
        {
            if (l.Items[i] is not NString s) return null;
            cols[i] = s.Value;
        }
        return cols;
    }

    /// <summary>
    /// True when <paramref name="list"/> is a non-empty list whose items are all
    /// records. Key sets are NOT required to match — ragged tables render fine
    /// with blanks for absent cells.
    /// </summary>
    public static bool IsTableShaped(NList list)
    {
        if (list.Items.Length == 0) return false;
        for (int i = 0; i < list.Items.Length; i++)
            if (list.Items[i] is not NRecord) return false;
        return true;
    }

    /// <summary>
    /// True when <paramref name="list"/> is a non-empty list whose items are all
    /// strings. Used by <see cref="Format"/> and <c>obj.table</c> so a one-column
    /// projection (<c>fs.ls() | select(x =&gt; $"…{x.Name}")</c>) prints as one
    /// string per line rather than the bracketed <c>["a", "b"]</c> array form.
    /// </summary>
    public static bool IsStringListShaped(NList list)
    {
        if (list.Items.Length == 0) return false;
        for (int i = 0; i < list.Items.Length; i++)
            if (list.Items[i] is not NString) return false;
        return true;
    }

    /// <summary>
    /// Render a list of strings as one row per element, no brackets, no quotes.
    /// Caller is expected to have validated via <see cref="IsStringListShaped"/>.
    /// </summary>
    public static string FormatStringList(NList list)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < list.Items.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(list.Items[i] is NString s ? s.Value : NValueOps.FormatForInterpolation(list.Items[i]));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Format a list of records as an aligned ASCII table. Column selection comes
    /// from the first record's <see cref="ColumnsFieldKey"/> hint when present
    /// (lets producers like <c>fs.ls()</c> surface a curated default view); when
    /// absent or malformed, falls back to the union of every record's keys with
    /// first-seen order preserved. Convention-hidden <c>__*</c> fields are always
    /// skipped from the union path. Cells whose value is missing or
    /// <see cref="NUnit"/> render as a blank string.
    /// </summary>
    public static string FormatRecordTable(NList list)
    {
        if (list.Items.Length == 0) return "(empty)";
        if (list.Items[0] is not NRecord) return NValueOps.FormatForDisplay(list);

        // First check for an explicit column hint on the first row. If it's well-formed
        // we trust it verbatim — including the order — and skip the union pass. A
        // malformed hint (wrong type, empty list, non-string element) silently falls
        // back to the union behavior so producers can't accidentally break printing.
        string[]? hint = TryReadColumnHint(list.Items[0]);
        string[] keys;
        if (hint is not null)
        {
            keys = hint;
        }
        else
        {
            // Union of keys across all records, first-seen order preserved. Fields whose
            // name begins with '__' are convention-hidden — they carry metadata like the
            // __display marker that selects a default field for non-tabular rendering, and
            // surfacing them as columns just clutters the table.
            var keysOrder = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int r = 0; r < list.Items.Length; r++)
            {
                if (list.Items[r] is not NRecord rec) continue;
                foreach (var k in rec.Fields.Keys)
                {
                    if (k.StartsWith("__", StringComparison.Ordinal)) continue;
                    if (seen.Add(k)) keysOrder.Add(k);
                }
            }
            keys = keysOrder.ToArray();
        }
        int colCount = keys.Length;
        var widths = new int[colCount];
        for (int c = 0; c < colCount; c++) widths[c] = keys[c].Length;

        var rows = new string[list.Items.Length][];
        var rowStyles = new string?[list.Items.Length];
        for (int r = 0; r < list.Items.Length; r++)
        {
            rows[r] = new string[colCount];
            if (list.Items[r] is not NRecord rec)
            {
                for (int c = 0; c < colCount; c++) rows[r][c] = "?";
            }
            else
            {
                for (int c = 0; c < colCount; c++)
                {
                    if (rec.Fields.TryGetValue(keys[c], out var v) && v is not NUnit)
                        rows[r][c] = NValueOps.FormatForInterpolation(v);
                    else
                        rows[r][c] = string.Empty;
                }
                if (rec.Fields.TryGetValue("__row_style", out var styleVal) && styleVal is NString styleStr)
                    rowStyles[r] = styleStr.Value;
            }
            // Width math uses VisualWidth so cells with embedded SGR escapes
            // (e.g. fs.ls's colored icons) don't bloat their column to the size
            // of the escape sequence.
            for (int c = 0; c < colCount; c++)
            {
                var vw = VisualWidth(rows[r][c]);
                if (vw > widths[c]) widths[c] = vw;
            }
        }

        var sb = new StringBuilder();
        AppendBorderLine(sb, widths, '╭', '┬', '╮');
        AppendDataLine(sb, keys, widths, rowStyle: null);
        AppendBorderLine(sb, widths, '├', '┼', '┤');
        for (int r = 0; r < rows.Length; r++)
            AppendDataLine(sb, rows[r], widths, rowStyles[r]);
        AppendBorderLine(sb, widths, '╰', '┴', '╯');
        return sb.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>
    /// Number of visible columns in <paramref name="s"/>: any character outside an
    /// SGR escape (<c>ESC [ … m</c>) counts; characters inside one don't. Used by
    /// <see cref="FormatRecordTable"/> to keep colored cells aligned with the rest
    /// of their column.
    /// </summary>
    private static int VisualWidth(string s)
    {
        int width = 0;
        int i = 0;
        while (i < s.Length)
        {
            if (s[i] == 0x1B && i + 1 < s.Length && s[i + 1] == '[')
            {
                int end = s.IndexOf('m', i + 2);
                if (end < 0) { width += s.Length - i; break; }
                i = end + 1;
                continue;
            }
            width++;
            i++;
        }
        return width;
    }

    // Cyan, matching the HoverBox border so the REPL's two framed surfaces read
    // as a family. Emitted as a 24-bit SGR; AnsiSgr resolves it to a Color, and
    // \e[39m resets just the foreground so any surrounding state (e.g. a row's
    // \e[2m dim wrap) survives.
    private const string BorderColor = "\x1b[38;2;137;220;235m";
    private const string ResetFg = "\x1b[39m";

    private static void AppendBorderLine(StringBuilder sb, int[] widths, char left, char join, char right)
    {
        sb.Append(BorderColor);
        sb.Append(left);
        for (int c = 0; c < widths.Length; c++)
        {
            if (c > 0) sb.Append(join);
            // +2 covers the single-space gutter on each side of the cell content
            // (`│ value │`); the run of '─' fills the entire slot so corners and
            // T-junctions align with the data rows.
            sb.Append('─', widths[c] + 2);
        }
        sb.Append(right);
        sb.Append(ResetFg);
        sb.AppendLine();
    }

    private static void AppendDataLine(StringBuilder sb, string[] cells, int[] widths, string? rowStyle)
    {
        if (rowStyle == "dim") sb.Append("\x1b[2m");
        for (int c = 0; c < cells.Length; c++)
        {
            sb.Append(BorderColor).Append('│').Append(ResetFg);
            sb.Append(' ');
            // PadRight on visual width: append the cell verbatim (preserving any
            // embedded SGR escapes), then top up with spaces so the column lines
            // up with the rest of its column despite the invisible escape bytes.
            var cell = cells[c];
            sb.Append(cell);
            var pad = widths[c] - VisualWidth(cell);
            if (pad > 0) sb.Append(' ', pad);
            sb.Append(' ');
        }
        sb.Append(BorderColor).Append('│').Append(ResetFg);
        if (rowStyle == "dim") sb.Append("\x1b[22m");
        sb.AppendLine();
    }
}
