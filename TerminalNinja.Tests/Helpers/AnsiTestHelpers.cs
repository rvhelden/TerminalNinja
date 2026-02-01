using System.Text;
using TerminalNinja.Ansi;
using TerminalNinja.Buffers;
using TerminalNinja.Elements;
using TerminalNinja.Primitives;

namespace TerminalNinja.Tests.Helpers;

/// <summary>
/// Shared helper methods for testing ANSI output and rendering.
/// </summary>
public static class AnsiTestHelpers
{
    /// <summary>
    /// Captures ANSI output from an AnsiWriter operation.
    /// </summary>
    /// <param name="action">The action to perform on the AnsiWriter.</param>
    /// <returns>The captured ANSI output as a string.</returns>
    public static string CaptureAnsiOutput(Action<AnsiWriter> action)
    {
        using var stream = new MemoryStream();
        using var writer = new AnsiWriter(stream);
        action(writer);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    
    /// <summary>
    /// Renders an element to a buffer and captures the ANSI output.
    /// </summary>
    /// <param name="element">The element to render.</param>
    /// <param name="width">Buffer width.</param>
    /// <param name="height">Buffer height.</param>
    /// <returns>The ANSI output as a string.</returns>
    public static string RenderElementToAnsi(IElement element, int width, int height)
    {
        var buffer = new CellBuffer(width, height);
        var viewport = new Rect(0, 0, width, height);
        element.Render(buffer, viewport);
        
        using var stream = new MemoryStream();
        using var writer = new AnsiWriter(stream);
        
        foreach (var change in buffer.GetChanges())
        {
            writer.WriteCell(change.X, change.Y, change.Cell);
        }
        
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    
    /// <summary>
    /// Counts occurrences of a substring in text.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <param name="pattern">The pattern to count.</param>
    /// <returns>The number of occurrences.</returns>
    public static int CountOccurrences(string text, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return 0;
        
        int count = 0;
        int index = 0;
        
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        
        return count;
    }
}
