using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A flattened, renderable text segment with resolved styles.
/// Produced by <see cref="Inline.CollectRuns"/> for efficient rendering.
/// </summary>
public readonly record struct InlineRun(string Text, Color Foreground, Color Background, TextDecorations Decorations);
