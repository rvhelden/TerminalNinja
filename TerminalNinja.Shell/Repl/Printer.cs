using System.Collections.Immutable;
using System.Text;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Repl;

/// <summary>
/// Renders an <see cref="NValue"/> for the REPL: scalars raw, list-of-records as
/// an aligned ASCII table, other lists/records compact via <see cref="NValueOps.FormatForDisplay"/>.
/// </summary>
public static class Printer
{
    /// <summary>Render <paramref name="v"/> for display in the REPL.</summary>
    public static string Format(NValue v)
    {
        if (v is NUnit) return string.Empty;
        if (v is NList list && IsTableShaped(list)) return FormatRecordTable(list);
        if (v is NSeq seq)
        {
            // Display is a sink — materialise once into an NList so the
            // table-shape detector can inspect the rows.
            var materialised = new NList(ImmutableArray.CreateRange(seq.Items));
            if (IsTableShaped(materialised)) return FormatRecordTable(materialised);
            return NValueOps.FormatForDisplay(materialised);
        }
        return NValueOps.FormatForDisplay(v);
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
    /// Format a list of records as an aligned ASCII table. Header order is the
    /// union of every record's keys (preserving first-seen order). Cells whose
    /// value is missing or <see cref="NUnit"/> render as a blank string.
    /// </summary>
    public static string FormatRecordTable(NList list)
    {
        if (list.Items.Length == 0) return "(empty)";
        if (list.Items[0] is not NRecord) return NValueOps.FormatForDisplay(list);

        // Union of keys across all records, first-seen order preserved.
        var keysOrder = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int r = 0; r < list.Items.Length; r++)
        {
            if (list.Items[r] is not NRecord rec) continue;
            foreach (var k in rec.Fields.Keys)
                if (seen.Add(k)) keysOrder.Add(k);
        }
        var keys = keysOrder.ToArray();
        int colCount = keys.Length;
        var widths = new int[colCount];
        for (int c = 0; c < colCount; c++) widths[c] = keys[c].Length;

        var rows = new string[list.Items.Length][];
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
            }
            for (int c = 0; c < colCount; c++)
                if (rows[r][c].Length > widths[c]) widths[c] = rows[r][c].Length;
        }

        var sb = new StringBuilder();
        AppendRow(sb, keys, widths);
        AppendSeparator(sb, widths);
        for (int r = 0; r < rows.Length; r++)
            AppendRow(sb, rows[r], widths);
        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static void AppendRow(StringBuilder sb, string[] cells, int[] widths)
    {
        for (int c = 0; c < cells.Length; c++)
        {
            if (c > 0) sb.Append("  ");
            sb.Append(cells[c].PadRight(widths[c]));
        }
        sb.AppendLine();
    }

    private static void AppendSeparator(StringBuilder sb, int[] widths)
    {
        for (int c = 0; c < widths.Length; c++)
        {
            if (c > 0) sb.Append("  ");
            sb.Append(new string('-', widths[c]));
        }
        sb.AppendLine();
    }
}
