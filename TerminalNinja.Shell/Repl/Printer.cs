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

    /// <summary>True when <paramref name="list"/> is a non-empty list of records sharing the same key set.</summary>
    public static bool IsTableShaped(NList list)
    {
        if (list.Items.Length == 0) return false;
        if (list.Items[0] is not NRecord first) return false;
        var keys = first.Fields.Keys;
        for (int i = 1; i < list.Items.Length; i++)
        {
            if (list.Items[i] is not NRecord rec) return false;
            if (rec.Fields.Count != first.Fields.Count) return false;
            foreach (var k in keys)
                if (!rec.Fields.ContainsKey(k)) return false;
        }
        return true;
    }

    /// <summary>Format a list of records as an aligned ASCII table.</summary>
    public static string FormatRecordTable(NList list)
    {
        if (list.Items.Length == 0) return "(empty)";
        if (list.Items[0] is not NRecord first) return NValueOps.FormatForDisplay(list);

        var keys = first.Fields.Keys.ToArray();
        int colCount = keys.Length;
        var widths = new int[colCount];
        for (int c = 0; c < colCount; c++) widths[c] = keys[c].Length;

        var rows = new string[list.Items.Length][];
        for (int r = 0; r < list.Items.Length; r++)
        {
            if (list.Items[r] is not NRecord rec)
            {
                rows[r] = new string[colCount];
                for (int c = 0; c < colCount; c++) rows[r][c] = "?";
                continue;
            }
            rows[r] = new string[colCount];
            for (int c = 0; c < colCount; c++)
            {
                rows[r][c] = rec.Fields.TryGetValue(keys[c], out var v)
                    ? NValueOps.FormatForInterpolation(v)
                    : string.Empty;
                if (rows[r][c].Length > widths[c]) widths[c] = rows[r][c].Length;
            }
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
