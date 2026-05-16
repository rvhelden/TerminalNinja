using TerminalNinja.Primitives;

namespace TerminalNinja.Rendering;

/// <summary>
/// Optional capability on top of <see cref="ICellSink"/>: receive pre-flattened styled
/// text runs alongside the per-cell stream. Sinks that implement this can shape the run
/// via HarfBuzz (or any other shaping engine) to produce ligatures and properly placed
/// complex-script glyphs that a per-cell renderer cannot.
/// </summary>
/// <remarks>
/// <para>
/// Lifecycle: a renderer that knows it's talking to an <see cref="IShapedRunSink"/>
/// invokes <see cref="WriteRun"/> during the same phase it writes individual cells.
/// The sink is expected to <em>queue</em> the run, mark the cells it covers, and emit
/// shaped glyphs during <see cref="ICellSink.EndFrame"/>. Per-cell <see cref="ICellSink.WriteCell"/>
/// calls for cells inside a queued run must still paint the background, but should
/// suppress their own glyph rendering to avoid double-painting under the shaped glyphs.
/// </para>
/// <para>
/// <see cref="WriteRun"/> takes a span of UTF-16 code units (not codepoints) because the
/// underlying shapers (<c>SKShaper</c> / HarfBuzz) consume UTF-16. Source code units
/// must include any surrogate pairs intact.
/// </para>
/// </remarks>
public interface IShapedRunSink : ICellSink
{
    /// <summary>
    /// Queue a styled text run starting at cell coordinate (<paramref name="x"/>, <paramref name="y"/>).
    /// The sink shapes the run during <see cref="ICellSink.EndFrame"/> and draws glyphs at
    /// cell-aligned origins. The run spans as many cells as its rendered cell width — wide
    /// codepoints occupy two cells, ASCII / narrow codepoints one.
    /// </summary>
    /// <param name="x">Leading cell column.</param>
    /// <param name="y">Cell row.</param>
    /// <param name="text">UTF-16 code units of the run (preserves surrogate pairs).</param>
    /// <param name="fg">Foreground color used for glyphs.</param>
    /// <param name="bg">Background color. Painted per-cell via <see cref="ICellSink.WriteCell"/>; passed
    /// here so the shaped path can recover the run's style for caching purposes.</param>
    /// <param name="decorations">Underline / strikethrough / inverse / etc. Applied alongside the shaped glyphs.</param>
    void WriteRun(int x, int y, ReadOnlySpan<char> text, Color fg, Color bg, TextDecorations decorations);
}
