using System.Collections.Immutable;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Config;

/// <summary>
/// Mutable holder for REPL-scoped configuration that lives across an interactive
/// session: shell-mode aliases (<c>cd</c> → <c>fs.cd</c>) and line-editor
/// keybindings (<c>Ctrl+L</c> → <c>clear</c>). The underlying dictionaries are
/// <see cref="ImmutableDictionary{TKey,TValue}"/> swapped atomically via
/// <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/>, so the
/// <see cref="Aliases"/> and <see cref="Keybindings"/> snapshots are safe to
/// enumerate without locking even while another thread mutates them.
/// </summary>
public sealed class NinjaConfig
{
    private ImmutableDictionary<string, NValue> _aliases =
        ImmutableDictionary<string, NValue>.Empty.WithComparers(StringComparer.Ordinal);
    private ImmutableDictionary<string, string> _keybindings =
        ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>Current alias table — a stable snapshot, safe to enumerate.</summary>
    public IReadOnlyDictionary<string, NValue> Aliases => _aliases;

    /// <summary>Current keybinding table — a stable snapshot, safe to enumerate.</summary>
    public IReadOnlyDictionary<string, string> Keybindings => _keybindings;

    /// <summary>Create a fresh, empty configuration.</summary>
    public static NinjaConfig Empty() => new();

    /// <summary>Register or overwrite an alias mapping <paramref name="name"/> to a callable.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="callable"/> is not an <see cref="NFunc"/>.</exception>
    public void SetAlias(string name, NValue callable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (callable is null) throw new ArgumentNullException(nameof(callable));
        if (callable is not NFunc)
            throw new ArgumentException("alias value must be a function (NFunc)", nameof(callable));
        while (true)
        {
            var current = _aliases;
            var next = current.SetItem(name, callable);
            if (ReferenceEquals(current, next)) return;
            if (ReferenceEquals(Interlocked.CompareExchange(ref _aliases, next, current), current)) return;
        }
    }

    /// <summary>Remove an alias; returns true if a binding existed.</summary>
    public bool RemoveAlias(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        while (true)
        {
            var current = _aliases;
            if (!current.ContainsKey(name)) return false;
            var next = current.Remove(name);
            if (ReferenceEquals(Interlocked.CompareExchange(ref _aliases, next, current), current)) return true;
        }
    }

    /// <summary>Look up an alias; returns true if a binding exists.</summary>
    public bool TryGetAlias(string name, out NValue callable)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _aliases.TryGetValue(name, out callable!);
    }

    /// <summary>Bind a key chord (e.g. <c>"Ctrl+L"</c>) to a named REPL action (e.g. <c>"clear"</c>).</summary>
    public void BindKey(string chord, string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chord);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        while (true)
        {
            var current = _keybindings;
            var next = current.SetItem(chord, action);
            if (ReferenceEquals(current, next)) return;
            if (ReferenceEquals(Interlocked.CompareExchange(ref _keybindings, next, current), current)) return;
        }
    }

    /// <summary>Remove a keybinding; returns true if a binding existed.</summary>
    public bool UnbindKey(string chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        while (true)
        {
            var current = _keybindings;
            if (!current.ContainsKey(chord)) return false;
            var next = current.Remove(chord);
            if (ReferenceEquals(Interlocked.CompareExchange(ref _keybindings, next, current), current)) return true;
        }
    }

    /// <summary>Look up the action bound to <paramref name="chord"/>; returns true if a binding exists.</summary>
    public bool TryGetAction(string chord, out string action)
    {
        ArgumentNullException.ThrowIfNull(chord);
        return _keybindings.TryGetValue(chord, out action!);
    }
}
