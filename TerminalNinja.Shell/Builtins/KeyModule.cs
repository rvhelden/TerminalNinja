using System.Collections.Immutable;
using TerminalNinja.Shell.Config;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Builtins;

/// <summary>
/// The <c>key</c> module — runtime surface for binding REPL-line-editor chords
/// (<c>Ctrl+L</c>, <c>Alt+R</c>, <c>Shift+Tab</c>) to named actions. Chord syntax
/// is validated by <see cref="ChordParser"/>; the action name must be one of the
/// v1 supported actions (<c>clear</c>, <c>history-prev</c>, <c>history-next</c>,
/// <c>abort</c>, <c>submit</c>, <c>complete</c>).
/// </summary>
/// <remarks>
/// Bindings are consumed by the line editor (Task 5) — registering one here only
/// installs it into the <see cref="NinjaConfig"/>; the editor consults the snapshot
/// on each keystroke at read time.
/// </remarks>
public static class KeyModule
{
    private static readonly HashSet<string> SupportedActions =
        new(StringComparer.Ordinal)
        {
            "clear",
            "history-prev",
            "history-next",
            "abort",
            "submit",
            "complete",
        };

    /// <summary>Register the <c>key</c> module into <paramref name="b"/>, closing over <paramref name="config"/>.</summary>
    public static void Register(ImmutableDictionary<string, NValue>.Builder b, NinjaConfig config)
    {
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(config);

        BuiltinRegistry.RegisterModule(b, "key",
            ("bind", new NFunc(args => Bind(args, config), 2)),
            ("unbind", new NFunc(args => Unbind(args, config), 1)),
            ("list", new NFunc(args => List(args, config), 0)));
    }

    private static NValue Bind(NValue[] args, NinjaConfig config)
    {
        if (args.Length != 2) throw new EvaluatorException($"key.bind expects 2 arguments, got {args.Length}");
        if (args[0] is not NString chord) throw new EvaluatorException("key.bind: chord must be a string");
        if (args[1] is not NString action) throw new EvaluatorException("key.bind: action must be a string");
        if (!ChordParser.TryParse(chord.Value, out var canonical))
            throw new EvaluatorException($"key.bind: invalid chord '{chord.Value}'");
        if (!SupportedActions.Contains(action.Value))
            throw new EvaluatorException(
                $"key.bind: unknown action '{action.Value}'; supported: {string.Join(", ", SupportedActions)}");
        config.BindKey(canonical, action.Value);
        return NUnit.Instance;
    }

    private static NValue Unbind(NValue[] args, NinjaConfig config)
    {
        if (args.Length != 1) throw new EvaluatorException($"key.unbind expects 1 argument, got {args.Length}");
        if (args[0] is not NString chord) throw new EvaluatorException("key.unbind: chord must be a string");
        if (!ChordParser.TryParse(chord.Value, out var canonical))
            return new NBool(false);
        return new NBool(config.UnbindKey(canonical));
    }

    private static NValue List(NValue[] args, NinjaConfig config)
    {
        if (args.Length != 0) throw new EvaluatorException($"key.list expects 0 arguments, got {args.Length}");
        var b = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        foreach (var kv in config.Keybindings) b[kv.Key] = new NString(kv.Value);
        return new NRecord(b.ToImmutable());
    }
}

/// <summary>
/// Parses and canonicalises chord strings of the form
/// <c>[Modifier+]…Key</c>, where each modifier is one of <c>Ctrl</c>,
/// <c>Alt</c>, <c>Shift</c> (case-insensitive on input) and <c>Key</c> is a
/// non-empty token. Canonical output orders modifiers as
/// <c>Ctrl+Alt+Shift+Key</c> with PascalCase modifier names; the key portion
/// is preserved verbatim (after trimming).
/// </summary>
internal static class ChordParser
{
    public static bool TryParse(string input, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var parts = input.Split('+');
        if (parts.Length == 0) return false;
        bool ctrl = false, alt = false, shift = false;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var mod = parts[i].Trim();
            if (mod.Length == 0) return false;
            switch (mod.ToLowerInvariant())
            {
                case "ctrl": ctrl = true; break;
                case "alt": alt = true; break;
                case "shift": shift = true; break;
                default: return false;
            }
        }
        var key = parts[^1].Trim();
        if (key.Length == 0) return false;
        var b = new System.Text.StringBuilder();
        if (ctrl) b.Append("Ctrl+");
        if (alt) b.Append("Alt+");
        if (shift) b.Append("Shift+");
        b.Append(key);
        canonical = b.ToString();
        return true;
    }
}
