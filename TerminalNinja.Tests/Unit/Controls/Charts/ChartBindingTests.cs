using System.Collections.ObjectModel;
using TerminalNinja.Controls.Charts;
using TerminalNinja.Xaml.Mvvm;

namespace TerminalNinja.Tests.Unit.Controls.Charts;

/// <summary>
/// Verifies charts can be driven by data binding a collection from a ViewModel, and that
/// mutating the bound <see cref="ObservableCollection{T}"/> is reflected on the next render.
/// </summary>
public class ChartBindingTests
{
    private const int W = 40;
    private const int H = 15;

    internal sealed class ChartViewModel : ViewModelBase
    {
        public ObservableCollection<ChartSeries> Data { get; } = [];
    }

    private static ChartSeries MakeSeries(Color color, params double[] values)
    {
        var s = new ChartSeries { Color = color };
        foreach (var v in values)
        {
            s.Values.Add(new ChartDataPoint { Value = v });
        }

        return s;
    }

    private static bool HasForeground(CellBuffer buffer, Color color)
    {
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                if (buffer.GetCell(x, y).Foreground == color)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [Test]
    public async Task BarChart_SeriesSourceBinding_RendersBoundData()
    {
        var vm = new ChartViewModel();
        vm.Data.Add(MakeSeries(new Color(200, 10, 10), 10, 20, 30));

        const string xaml = """
            <BarChart xmlns="http://schemas.terminalninja.dev/xaml"
                      SeriesSource="{Binding Data}" ShowAxes="False" ShowLegend="False" />
            """;
        var chart = TerminalXaml.Load<BarChart>(xaml, vm);

        // The binding should have delivered the ViewModel's collection to SeriesSource.
        await Assert.That((object?)chart.SeriesSource).IsSameReferenceAs(vm.Data);

        using var buffer = new CellBuffer(W, H);
        chart.Render(buffer, new Rect(0, 0, W, H));

        await Assert.That(HasForeground(buffer, new Color(200, 10, 10))).IsTrue();
    }

    [Test]
    public async Task BarChart_AddingToBoundCollection_IsReflectedOnNextRender()
    {
        var vm = new ChartViewModel();
        vm.Data.Add(MakeSeries(new Color(200, 10, 10), 20, 20, 20));

        const string xaml = """
            <BarChart xmlns="http://schemas.terminalninja.dev/xaml"
                      BarMode="Stacked" SeriesSource="{Binding Data}" ShowAxes="False" ShowLegend="False" />
            """;
        var chart = TerminalXaml.Load<BarChart>(xaml, vm);

        using (var before = new CellBuffer(W, H))
        {
            chart.Render(before, new Rect(0, 0, W, H));
            await Assert.That(HasForeground(before, new Color(10, 10, 200))).IsFalse();
        }

        // Mutate the bound collection — the chart subscribed to CollectionChanged.
        vm.Data.Add(MakeSeries(new Color(10, 10, 200), 20, 20, 20));

        using var after = new CellBuffer(W, H);
        chart.Render(after, new Rect(0, 0, W, H));

        // The newly added series' color now appears in the stacked bars.
        await Assert.That(HasForeground(after, new Color(10, 10, 200))).IsTrue();
    }

    [Test]
    public async Task LineChart_SeriesSourceBinding_RendersBoundData()
    {
        var vm = new ChartViewModel();
        vm.Data.Add(MakeSeries(new Color(20, 220, 40), 1, 5, 2, 8, 3));

        const string xaml = """
            <LineChart xmlns="http://schemas.terminalninja.dev/xaml"
                       SeriesSource="{Binding Data}" ShowAxes="False" />
            """;
        var chart = TerminalXaml.Load<LineChart>(xaml, vm);

        await Assert.That((object?)chart.SeriesSource).IsSameReferenceAs(vm.Data);

        using var buffer = new CellBuffer(W, H);
        chart.Render(buffer, new Rect(0, 0, W, H));

        // Braille glyph drawn in the bound series color.
        var sawLine = false;
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x++)
            {
                var cell = buffer.GetCell(x, y);
                if (cell.Codepoint is >= 0x2800 and <= 0x28FF && cell.Foreground == new Color(20, 220, 40))
                {
                    sawLine = true;
                }
            }
        }

        await Assert.That(sawLine).IsTrue();
    }
}
