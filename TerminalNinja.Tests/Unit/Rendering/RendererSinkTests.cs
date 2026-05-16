using TerminalNinja.Controls;
using TerminalNinja.Primitives;
using TerminalNinja.Rendering;

namespace TerminalNinja.Tests.Unit.Rendering;

/// <summary>
/// End-to-end tests proving the renderer is genuinely sink-agnostic.
/// Drives a control tree through <see cref="MemoryCellSink"/> and asserts
/// on the recorded cell writes without going through ANSI encoding.
/// </summary>
public class RendererSinkTests
{
    [Test]
    public async Task Present_DrivesSinkLifecycle()
    {
        var sink = new MemoryCellSink();
        using var renderer = new Renderer(sink, 10, 3);

        var border = new Border { Background = Color.Red };
        renderer.Draw(border);
        renderer.Present();

        await Assert.That(sink.BeginFrameCount).IsEqualTo(1);
        await Assert.That(sink.EndFrameCount).IsEqualTo(1);
        await Assert.That(sink.Writes.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Present_RecordsCellsWithCorrectColors()
    {
        var sink = new MemoryCellSink();
        using var renderer = new Renderer(sink, 4, 2);

        var border = new Border { Background = Color.Blue };
        renderer.Draw(border);
        renderer.Present();

        // The whole 4x2 surface should be filled with blue-background cells.
        // CellBuffer's _previous starts as all-zero (Background = (0,0,0,0)),
        // so every non-default cell shows up as a change.
        await Assert.That(sink.Writes.Count).IsEqualTo(4 * 2);
        foreach (var write in sink.Writes)
        {
            await Assert.That(write.Cell.Background).IsEqualTo(Color.Blue);
        }
    }

    [Test]
    public async Task Present_OnlyEmitsChangedCellsAcrossFrames()
    {
        var sink = new MemoryCellSink();
        using var renderer = new Renderer(sink, 8, 2);

        var border = new Border { Background = Color.Green };
        renderer.Draw(border);
        renderer.Present();
        var firstFrameWrites = sink.Writes.Count;

        // Second frame with identical content should produce zero writes.
        renderer.Clear();
        renderer.Draw(border);
        renderer.Present();
        var totalAfterSecondFrame = sink.Writes.Count;

        await Assert.That(firstFrameWrites).IsEqualTo(16);
        await Assert.That(totalAfterSecondFrame - firstFrameWrites).IsEqualTo(0);
        await Assert.That(sink.BeginFrameCount).IsEqualTo(2);
        await Assert.That(sink.EndFrameCount).IsEqualTo(2);
    }

    [Test]
    public async Task Resize_NotifiesSink()
    {
        var sink = new MemoryCellSink();
        using var renderer = new Renderer(sink, 10, 5);

        renderer.Resize(20, 8);

        await Assert.That(sink.LastResize).IsNotNull();
        await Assert.That(sink.LastResize!.Value.Width).IsEqualTo(20);
        await Assert.That(sink.LastResize!.Value.Height).IsEqualTo(8);
    }

    [Test]
    public async Task WriteReset_DrivesSinkResetThenFlush()
    {
        var sink = new MemoryCellSink();
        using var renderer = new Renderer(sink, 4, 2);

        renderer.WriteReset();

        await Assert.That(sink.ResetCount).IsEqualTo(1);
        await Assert.That(sink.EndFrameCount).IsEqualTo(1);
    }
}
