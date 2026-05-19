using System.Diagnostics;
using System.Text;
using TerminalNinja.Shell.Config;
using TerminalNinja.Shell.Language.Services;

namespace TerminalNinja.Shell.Repl;

/// <summary>
/// Minimal line editor that replaces <see cref="System.IO.TextReader.ReadLine"/>
/// for the interactive REPL. Supports character input, backspace, Enter, Tab
/// completion, Ctrl+C (abort line), and Ctrl+D (EOF). No arrow keys / no history
/// in this iteration — typing flows left-to-right with the cursor always at end.
/// </summary>
/// <remarks>
/// Re-renders the whole line on every change using two ANSI escapes
/// (<c>\r</c> + <c>\x1b[K</c>). Works on Windows Terminal, PowerShell, conhost
/// (Windows 10+), and any POSIX terminal. For older Windows consoles without
/// ANSI support the redraw still produces sensible visual results because
/// <c>\r</c> + reprinting overwrites the line.
/// </remarks>
public sealed class LineEditor
{
    private readonly IKeyReader _keys;
    private readonly TextWriter _output;
    private readonly Func<string, int, IReadOnlyList<CompletionItem>>? _completer;
    private readonly NinjaConfig? _config;
    private readonly Action<string> _openConfigEditor;

    private readonly StringBuilder _buffer = new();
    private string _prompt = "";
    private bool _showedListSinceLastEdit;

    /// <summary>Create an editor.</summary>
    /// <param name="keys">Input source. Pass <see cref="ConsoleKeyReader"/> in production.</param>
    /// <param name="output">Where to render. Pass <see cref="Console.Out"/> in production.</param>
    /// <param name="completer">Tab completion provider — given (line, cursor), returns suggestions. <c>null</c> disables Tab handling.</param>
    /// <param name="config">Optional REPL config; when non-null, the editor consults <see cref="NinjaConfig.Keybindings"/> on each keystroke and dispatches the named action (<c>clear</c>, <c>submit</c>, <c>abort</c>, <c>complete</c>, <c>history-prev</c>, <c>history-next</c>, <c>edit-config</c>) before the hardcoded handling runs.</param>
    /// <param name="openConfigEditor">Launcher invoked with the rc-file path when the <c>edit-config</c> action fires. <c>null</c> uses a default that runs <c>code &lt;path&gt;</c> via the OS shell.</param>
    public LineEditor(
        IKeyReader keys,
        TextWriter output,
        Func<string, int, IReadOnlyList<CompletionItem>>? completer = null,
        NinjaConfig? config = null,
        Action<string>? openConfigEditor = null)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(output);
        _keys = keys;
        _output = output;
        _completer = completer;
        _config = config;
        _openConfigEditor = openConfigEditor ?? (path => LaunchVsCode(path, _output));
    }

    /// <summary>The result of one <see cref="ReadLine"/> call.</summary>
    public enum ReadResult
    {
        /// <summary>User pressed Enter; the line is in <see cref="LineEditorResult.Text"/>.</summary>
        EnteredLine,
        /// <summary>User pressed Ctrl+D / Ctrl+Z on an empty buffer (EOF).</summary>
        Eof,
        /// <summary>User pressed Ctrl+C; the line is abandoned.</summary>
        Aborted,
    }

    /// <summary>
    /// Render <paramref name="prompt"/> and read keystrokes until Enter, EOF, or
    /// Ctrl+C. Returns what happened plus the line text (when applicable).
    /// </summary>
    public LineEditorResult ReadLine(string prompt)
    {
        _prompt = prompt;
        _buffer.Clear();
        _showedListSinceLastEdit = false;
        Redraw();

        while (true)
        {
            var key = _keys.ReadKey();

            // User-configured keybindings get first pass. Only chords carrying Ctrl
            // or Alt are eligible — Shift-only chords stay reserved for normal text
            // input (Shift+letter must keep producing capitals).
            if (_config is not null && TryBuildChord(key, out var chord)
                && _config.TryGetAction(chord, out var action))
            {
                var dispatched = DispatchKeyAction(action);
                if (dispatched is { } result) return result;
                continue;
            }

            // Ctrl+C → abandon the line.
            if (key.Key == ConsoleKey.C && (key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                _output.WriteLine();
                return new LineEditorResult(ReadResult.Aborted, string.Empty);
            }

            // Ctrl+D / Ctrl+Z on an empty buffer → EOF.
            if ((key.Key == ConsoleKey.D && (key.Modifiers & ConsoleModifiers.Control) != 0)
                || key.Key == ConsoleKey.Z && (key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                if (_buffer.Length == 0)
                {
                    _output.WriteLine();
                    return new LineEditorResult(ReadResult.Eof, string.Empty);
                }
                // On a non-empty buffer, ignore — keep typing.
                continue;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                _output.WriteLine();
                return new LineEditorResult(ReadResult.EnteredLine, _buffer.ToString());
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (_buffer.Length > 0)
                {
                    _buffer.Length -= 1;
                    _showedListSinceLastEdit = false;
                    Redraw();
                }
                continue;
            }

            if (key.Key == ConsoleKey.Tab)
            {
                HandleTab();
                continue;
            }

            if (key.KeyChar >= ' ' && key.KeyChar != '\x7f')
            {
                _buffer.Append(key.KeyChar);
                _showedListSinceLastEdit = false;
                Redraw();
                continue;
            }

            // Any other control key — ignore in MVP.
        }
    }

    private void HandleTab()
    {
        if (_completer is null) return;
        var line = _buffer.ToString();
        var items = _completer(line, line.Length);
        if (items.Count == 0) return;

        var prefix = GetIdentifierPrefixBeforeCursor();
        if (items.Count == 1)
        {
            var only = items[0].Label;
            if (only.StartsWith(prefix, StringComparison.Ordinal))
            {
                _buffer.Append(only.AsSpan(prefix.Length));
                _showedListSinceLastEdit = false;
                Redraw();
            }
            return;
        }

        var common = LongestCommonPrefix(items);
        if (common.Length > prefix.Length)
        {
            _buffer.Append(common.AsSpan(prefix.Length));
            _showedListSinceLastEdit = false;
            Redraw();
            return;
        }

        // Common prefix didn't extend — show the menu (once per Tab burst).
        if (!_showedListSinceLastEdit)
        {
            ShowCompletionList(items);
            _showedListSinceLastEdit = true;
        }
    }

    private string GetIdentifierPrefixBeforeCursor()
    {
        int start = _buffer.Length;
        while (start > 0)
        {
            char c = _buffer[start - 1];
            if (char.IsLetterOrDigit(c) || c == '_') start--;
            else break;
        }
        return _buffer.ToString(start, _buffer.Length - start);
    }

    private static string LongestCommonPrefix(IReadOnlyList<CompletionItem> items)
    {
        if (items.Count == 0) return string.Empty;
        var first = items[0].Label;
        int n = first.Length;
        for (int i = 1; i < items.Count; i++)
        {
            var label = items[i].Label;
            int max = Math.Min(n, label.Length);
            int k = 0;
            while (k < max && first[k] == label[k]) k++;
            n = k;
            if (n == 0) break;
        }
        return first.Substring(0, n);
    }

    private void ShowCompletionList(IReadOnlyList<CompletionItem> items)
    {
        _output.WriteLine();
        var sb = new StringBuilder();
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0) sb.Append("  ");
            sb.Append(items[i].Label);
        }
        _output.WriteLine(sb.ToString());
        Redraw();
    }

    private static bool TryBuildChord(ConsoleKeyInfo key, out string chord)
    {
        chord = string.Empty;
        bool ctrl = (key.Modifiers & ConsoleModifiers.Control) != 0;
        bool alt = (key.Modifiers & ConsoleModifiers.Alt) != 0;
        bool shift = (key.Modifiers & ConsoleModifiers.Shift) != 0;
        // Pure Shift (or no modifiers at all) flows through as ordinary input so
        // capital letters and shifted symbols still type normally.
        if (!ctrl && !alt) return false;
        chord = ChordKey.Format(ctrl, alt, shift, key.Key.ToString());
        return true;
    }

    private LineEditorResult? DispatchKeyAction(string action)
    {
        switch (action)
        {
            case "submit":
                _output.WriteLine();
                return new LineEditorResult(ReadResult.EnteredLine, _buffer.ToString());
            case "abort":
                _output.WriteLine();
                return new LineEditorResult(ReadResult.Aborted, string.Empty);
            case "clear":
                _output.Write("\x1b[2J\x1b[H");
                _buffer.Clear();
                _showedListSinceLastEdit = false;
                Redraw();
                return null;
            case "complete":
                HandleTab();
                return null;
            case "edit-config":
                _output.WriteLine();
                _openConfigEditor(RcLoader.DefaultPath());
                Redraw();
                return null;
            case "history-prev":
            case "history-next":
                // No history infrastructure in this minimal editor; the action
                // is recognised so KeyModule accepts the binding, but the
                // editor cannot act on it until history support lands.
                return null;
            default:
                // Unknown actions slip through to the hardcoded handlers below.
                return null;
        }
    }

    private void Redraw()
    {
        _output.Write("\r\x1b[K");
        _output.Write(_prompt);
        _output.Write(_buffer.ToString());
        _output.Flush();
    }

    private static void LaunchVsCode(string path, TextWriter output)
    {
        // UseShellExecute lets the OS resolve `code` / `code.cmd` through PATH or
        // file associations — the same way a user typing `code .ninjarc` in their
        // terminal would. The launched process is detached; we don't wait on it.
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "code",
                Arguments = $"\"{path}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            output.WriteLine($"ninja: failed to open '{path}' in vscode: {ex.Message}");
            output.WriteLine("ninja: ensure VS Code's `code` CLI is on PATH (Command Palette → Shell Command: Install 'code' command).");
        }
    }
}

/// <summary>Result of a <see cref="LineEditor.ReadLine"/> call.</summary>
public readonly record struct LineEditorResult(LineEditor.ReadResult Result, string Text);
