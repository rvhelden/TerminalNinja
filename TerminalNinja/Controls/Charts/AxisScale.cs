using System.Globalization;

namespace TerminalNinja.Controls.Charts;

/// <summary>
/// Computes a "nice" numeric axis (rounded bounds and evenly spaced ticks) for a range
/// of data, and maps values into a normalized 0..1 position along that axis. Shared by
/// <see cref="BarChart"/>, <see cref="LineChart"/>, and the time axis of
/// <see cref="TraceChart"/>. Based on Paul Heckbert's "nice numbers" algorithm.
/// </summary>
public readonly struct AxisScale
{
    private AxisScale(double min, double max, double step, IReadOnlyList<double> ticks)
    {
        Min = min;
        Max = max;
        Step = step;
        Ticks = ticks;
    }

    /// <summary>Lower bound of the axis (rounded down to a nice value).</summary>
    public double Min { get; }

    /// <summary>Upper bound of the axis (rounded up to a nice value).</summary>
    public double Max { get; }

    /// <summary>Spacing between adjacent ticks.</summary>
    public double Step { get; }

    /// <summary>The tick values from <see cref="Min"/> to <see cref="Max"/> inclusive.</summary>
    public IReadOnlyList<double> Ticks { get; }

    /// <summary>
    /// Maps a value to its normalized position in [0, 1] along the axis, where 0 is
    /// <see cref="Min"/> and 1 is <see cref="Max"/>. Returns 0 for a degenerate axis.
    /// </summary>
    public double Normalize(double value)
    {
        var span = Max - Min;
        return span > 0 ? (value - Min) / span : 0.0;
    }

    /// <summary>
    /// Builds a nice axis covering <paramref name="dataMin"/>..<paramref name="dataMax"/>
    /// with roughly <paramref name="maxTicks"/> ticks. Handles reversed, equal, and
    /// non-finite inputs gracefully.
    /// </summary>
    public static AxisScale Create(double dataMin, double dataMax, int maxTicks = 5)
    {
        if (!double.IsFinite(dataMin) || !double.IsFinite(dataMax))
        {
            dataMin = 0;
            dataMax = 1;
        }

        if (dataMin > dataMax)
        {
            (dataMin, dataMax) = (dataMax, dataMin);
        }

        if (dataMin == dataMax)
        {
            // Pad a flat range so the axis has non-zero extent.
            if (dataMin == 0)
            {
                dataMax = 1;
            }
            else
            {
                var pad = Math.Abs(dataMin) * 0.5;
                dataMin -= pad;
                dataMax += pad;
            }
        }

        maxTicks = Math.Max(2, maxTicks);

        var range = NiceNumber(dataMax - dataMin, round: false);
        var step = NiceNumber(range / (maxTicks - 1), round: true);
        var niceMin = Math.Floor(dataMin / step) * step;
        var niceMax = Math.Ceiling(dataMax / step) * step;

        var ticks = new List<double>();
        // Guard against runaway loops from pathological steps.
        var maxCount = maxTicks * 4;
        for (var v = niceMin; v <= niceMax + step * 0.5 && ticks.Count < maxCount; v += step)
        {
            // Snap values very close to zero to exactly zero to avoid "-0" and fp dust.
            ticks.Add(Math.Abs(v) < step * 1e-9 ? 0.0 : v);
        }

        return new AxisScale(niceMin, niceMax, step, ticks);
    }

    /// <summary>
    /// Formats a tick value for display, trimming trailing zeros and using compact
    /// k/M/G suffixes for large magnitudes.
    /// </summary>
    public static string FormatTick(double value)
    {
        var abs = Math.Abs(value);
        if (abs >= 1_000_000_000)
        {
            return Trim(value / 1_000_000_000) + "G";
        }

        if (abs >= 1_000_000)
        {
            return Trim(value / 1_000_000) + "M";
        }

        if (abs >= 1_000)
        {
            return Trim(value / 1_000) + "k";
        }

        return Trim(value);
    }

    private static string Trim(double value)
    {
        // Up to two decimals, no trailing zeros.
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Returns a "nice" number approximately equal to <paramref name="value"/>. When
    /// <paramref name="round"/> is true the result is rounded to the nearest nice value;
    /// otherwise it is rounded up. Nice values are 1, 2, 5, 10 × 10ⁿ.
    /// </summary>
    private static double NiceNumber(double value, bool round)
    {
        if (value <= 0)
        {
            return 1;
        }

        var exponent = Math.Floor(Math.Log10(value));
        var fraction = value / Math.Pow(10, exponent);

        double niceFraction;
        if (round)
        {
            niceFraction = fraction < 1.5 ? 1 : fraction < 3 ? 2 : fraction < 7 ? 5 : 10;
        }
        else
        {
            niceFraction = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
        }

        return niceFraction * Math.Pow(10, exponent);
    }
}
