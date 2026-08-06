using TerminalNinja.Controls.Charts;

namespace TerminalNinja.Tests.Unit.Controls.Charts;

/// <summary>
/// Tests for <see cref="AxisScale"/> nice-number range, tick generation, and formatting.
/// </summary>
public class AxisScaleTests
{
    [Test]
    public async Task Create_RoundsBoundsToNiceValues()
    {
        var scale = AxisScale.Create(0, 97, maxTicks: 5);

        await Assert.That(scale.Min).IsEqualTo(0.0);
        await Assert.That(scale.Max).IsGreaterThanOrEqualTo(97.0);
        // 0..100 by 20 is the nice fit for ~5 ticks.
        await Assert.That(scale.Max).IsEqualTo(100.0);
        await Assert.That(scale.Step).IsEqualTo(20.0);
    }

    [Test]
    public async Task Create_TicksSpanMinToMaxInclusive()
    {
        var scale = AxisScale.Create(0, 100, maxTicks: 5);

        await Assert.That(scale.Ticks[0]).IsEqualTo(0.0);
        await Assert.That(scale.Ticks[^1]).IsEqualTo(100.0);
    }

    [Test]
    public async Task Normalize_MapsMinToZeroAndMaxToOne()
    {
        var scale = AxisScale.Create(0, 100, maxTicks: 5);

        await Assert.That(scale.Normalize(scale.Min)).IsEqualTo(0.0);
        await Assert.That(scale.Normalize(scale.Max)).IsEqualTo(1.0);
        await Assert.That(scale.Normalize((scale.Min + scale.Max) / 2)).IsEqualTo(0.5);
    }

    [Test]
    public async Task Create_ReversedInput_IsNormalized()
    {
        var scale = AxisScale.Create(100, 0, maxTicks: 5);

        await Assert.That(scale.Min).IsLessThan(scale.Max);
    }

    [Test]
    public async Task Create_FlatRange_ProducesNonZeroExtent()
    {
        var scale = AxisScale.Create(5, 5, maxTicks: 5);

        await Assert.That(scale.Max).IsGreaterThan(scale.Min);
    }

    [Test]
    public async Task Create_NonFiniteInput_FallsBackToUnitRange()
    {
        var scale = AxisScale.Create(double.NaN, double.PositiveInfinity, maxTicks: 5);

        await Assert.That(double.IsFinite(scale.Min)).IsTrue();
        await Assert.That(double.IsFinite(scale.Max)).IsTrue();
        await Assert.That(scale.Max).IsGreaterThan(scale.Min);
    }

    [Test]
    [Arguments(0.0, "0")]
    [Arguments(1500.0, "1.5k")]
    [Arguments(2_000_000.0, "2M")]
    [Arguments(42.5, "42.5")]
    [Arguments(3_000_000_000.0, "3G")]
    public async Task FormatTick_UsesCompactSuffixes(double value, string expected)
    {
        await Assert.That(AxisScale.FormatTick(value)).IsEqualTo(expected);
    }
}
