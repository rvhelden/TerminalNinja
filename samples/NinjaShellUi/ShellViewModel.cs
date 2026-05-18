using System.Collections;
using System.Collections.ObjectModel;
using System.Text;
using TerminalNinja.Controls;
using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.PowerShell;
using TerminalNinja.Shell.Repl;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;
using TerminalNinja.Xaml.Mvvm;

namespace NinjaShellUi;

/// <summary>
/// Backs <c>ShellLayout.xaml</c>: owns the <see cref="NinjaShellUi.ReplView"/> the layout
/// hosts, the live <see cref="Env"/> the evaluator threads through each command, and the
/// three side-panel summaries (files, env, scope) the user toggles with F1/F2/F3.
/// </summary>
/// <remarks>
/// <para>
/// The view model evaluates each REPL command synchronously on the UI thread: NinjaEvaluator
/// is pure tree-walk over an immutable <see cref="Env"/>, so even pathological scripts can't
/// deadlock the renderer — they at worst hang for as long as the user-supplied code takes to
/// finish. If that becomes a real problem the call site is the right place to push to a
/// worker (we deliberately don't async-ify here so the panels show coherent post-eval state).
/// </para>
/// </remarks>
public sealed class ShellViewModel : ViewModelBase
{
    private Env _env;

    /// <summary>The custom REPL surface bound into the layout's centre cell.</summary>
    public ReplView Repl { get; }

    /// <summary>Editable list bound into the env panel. Items reference <see cref="EnvEntries"/>.</summary>
    public EditableKeyValueList EnvList { get; }

    /// <summary>Editable list bound into the scope panel. Items reference <see cref="ScopeEntries"/>.</summary>
    public EditableKeyValueList ScopeList { get; }

    /// <summary>The current working directory the file panel reflects.</summary>
    public string CwdDisplay
    {
        get;
        private set => SetProperty(ref field, value);
    } = "";

    /// <summary>Multi-line listing of the current working directory's entries.</summary>
    public string FilesText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "";

    /// <summary>
    /// Process environment variables, sorted by name. Bound to the env panel's editable list —
    /// committing a row writes back through <see cref="Environment.SetEnvironmentVariable(string, string?)"/>.
    /// </summary>
    public ObservableCollection<KeyValueEntry> EnvEntries { get; } = new();

    /// <summary>
    /// NinjaShell evaluator bindings (process scope). Bound to the scope panel's editable list —
    /// committing a row re-evaluates the new text as a NinjaShell expression and rebinds the
    /// name, or reports a parse error in the REPL.
    /// </summary>
    public ObservableCollection<KeyValueEntry> ScopeEntries { get; } = new();

    /// <summary>Visibility of the files panel (F1).</summary>
    public Visibility FilesPanelVisibility
    {
        get;
        private set => SetProperty(ref field, value);
    } = Visibility.Visible;

    /// <summary>Visibility of the env panel (F2).</summary>
    public Visibility EnvPanelVisibility
    {
        get;
        private set => SetProperty(ref field, value);
    } = Visibility.Visible;

    /// <summary>Visibility of the scope panel (F3).</summary>
    public Visibility ScopePanelVisibility
    {
        get;
        private set => SetProperty(ref field, value);
    } = Visibility.Visible;

    /// <summary>
    /// Visibility of the entire right-hand column (env + scope). Collapses when both inner
    /// panels are hidden so the centre REPL can grow into the freed space.
    /// </summary>
    public Visibility RightPanelVisibility
    {
        get;
        private set => SetProperty(ref field, value);
    } = Visibility.Visible;

    /// <summary>Footer hint string describing the toggle shortcuts.</summary>
    public string ShortcutHint { get; } = "F1 files   F2 env   F3 scope   Tab focus   Enter edit   F10 exit";

    /// <summary>Creates a view model with a fresh evaluator environment and a focused REPL.</summary>
    public ShellViewModel()
    {
        _env = BuiltinRegistry.CreateDefaultEnv();
        if (PwshBridge.IsAvailable)
        {
            _env = PwshBridge.Install(_env);
        }

        Repl = new ReplView
        {
            Foreground = new TerminalNinja.Primitives.Color(0xCD, 0xD6, 0xF4),
            Background = new TerminalNinja.Primitives.Color(0x1E, 0x1E, 0x2E),
        };

        Repl.AppendOutput("NinjaShell UI  —  type expressions in the REPL; Tab to focus a side panel;");
        Repl.AppendOutput("Enter on a row to edit; F1/F2/F3 toggle panels; F10 exits.");
        Repl.Scope = SnapshotScope(_env);
        Repl.CommandEntered += OnCommandEntered;

        EnvList = new EditableKeyValueList
        {
            ItemsSource = EnvEntries,
            Foreground = new TerminalNinja.Primitives.Color(0xA6, 0xE3, 0xA1),
            Background = new TerminalNinja.Primitives.Color(0x1E, 0x1E, 0x2E),
        };
        EnvList.ItemCommitted += OnEnvEntryCommitted;

        ScopeList = new EditableKeyValueList
        {
            ItemsSource = ScopeEntries,
            Foreground = new TerminalNinja.Primitives.Color(0x94, 0xE2, 0xD5),
            Background = new TerminalNinja.Primitives.Color(0x1E, 0x1E, 0x2E),
        };
        ScopeList.ItemCommitted += OnScopeEntryCommitted;

        RefreshPanels();
    }

    /// <summary>Toggles the files panel between collapsed and visible.</summary>
    public void ToggleFilesPanel() => FilesPanelVisibility = Flip(FilesPanelVisibility);

    /// <summary>Toggles the env panel between collapsed and visible.</summary>
    public void ToggleEnvPanel()
    {
        EnvPanelVisibility = Flip(EnvPanelVisibility);
        UpdateRightPanelVisibility();
    }

    /// <summary>Toggles the scope panel between collapsed and visible.</summary>
    public void ToggleScopePanel()
    {
        ScopePanelVisibility = Flip(ScopePanelVisibility);
        UpdateRightPanelVisibility();
    }

    private static Visibility Flip(Visibility v) =>
        v == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    private void UpdateRightPanelVisibility()
    {
        // Both inner panels collapsed → drop the wrapper too, otherwise show.
        var bothCollapsed = EnvPanelVisibility == Visibility.Collapsed
            && ScopePanelVisibility == Visibility.Collapsed;
        RightPanelVisibility = bothCollapsed ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Commits an env-panel edit: writes the new value back into the process environment.
    /// On Windows / Unix the change is process-scoped — child processes inherit it but the OS
    /// shell that spawned us is unaffected.
    /// </summary>
    public void OnEnvEntryCommitted(KeyValueEntry entry)
    {
        try
        {
            Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            Repl.AppendOutput($"env: set {entry.Key}");
        }
        catch (Exception ex)
        {
            Repl.AppendOutput($"env: failed to set {entry.Key}: {ex.Message}");
        }
        // Re-pull env vars so the entry list stays sorted / canonical (in case the OS rejected
        // the value or normalised it).
        RefreshEnvVars();
    }

    /// <summary>
    /// Commits a scope-panel edit: parses the new value as a NinjaShell expression and rebinds
    /// the name in-place. Closures that captured the original <see cref="EnvRef"/> see the new
    /// value because <see cref="Env.TrySetBindingValue"/> mutates the slot rather than producing
    /// a new <see cref="Env"/>.
    /// </summary>
    public void OnScopeEntryCommitted(KeyValueEntry entry)
    {
        try
        {
            var result = NinjaEvaluator.EvalSource(entry.Value, _env);
            // EvalSource doesn't extend the env unless the source is `let …`; we want a
            // straight value, so we rebind via the slot directly.
            if (!_env.TrySetBindingValue(entry.Key, result.Value))
            {
                // Name disappeared from the env between selection and commit — unusual but
                // possible if a REPL command unbinds it. Fall back to extending.
                _env = _env.Extend(entry.Key, result.Value);
            }
            Repl.AppendOutput($"scope: {entry.Key} = {Printer.Format(result.Value)}");
        }
        catch (Exception ex)
        {
            Repl.AppendOutput($"scope: parse error setting '{entry.Key}': {ex.Message}");
        }
        RefreshScope();
    }

    private void OnCommandEntered(string line)
    {
        try
        {
            var result = NinjaEvaluator.EvalScript(line, _env);
            _env = result.Env;
            // Keep the REPL's scope snapshot in sync so mouse hover on an identifier
            // shows live shape + data, not stale or missing info.
            Repl.Scope = SnapshotScope(_env);

            var rendered = Printer.Format(result.Value);
            if (!string.IsNullOrEmpty(rendered))
            {
                // AppendResult stores the produced NValue alongside the rendered text,
                // making each output line a mouse-hover target with shape + data.
                Repl.AppendResult(rendered, result.Value);
            }
        }
        catch (Exception ex)
        {
            Repl.AppendOutput($"error: {ex.Message}");
        }

        RefreshPanels();
    }

    /// <summary>Materialise an env into a plain dictionary for the REPL's hover lookups.</summary>
    private static IReadOnlyDictionary<string, NValue> SnapshotScope(Env env)
    {
        var d = new Dictionary<string, NValue>(StringComparer.Ordinal);
        foreach (var kv in env.Bindings)
            d[kv.Key] = kv.Value;
        return d;
    }

    private void RefreshPanels()
    {
        RefreshCwd();
        RefreshEnvVars();
        RefreshScope();
    }

    private void RefreshCwd()
    {
        var cwd = Environment.CurrentDirectory;
        CwdDisplay = cwd;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine(cwd);
            sb.AppendLine();

            var dir = new DirectoryInfo(cwd);
            foreach (var sub in dir.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append("> ").AppendLine(sub.Name + "/");
            }
            foreach (var file in dir.EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append("  ").AppendLine(file.Name);
            }

            FilesText = sb.ToString();
        }
        catch (Exception ex)
        {
            FilesText = $"(unable to list directory: {ex.Message})";
        }
    }

    private void RefreshEnvVars()
    {
        // Rebuild the collection in sorted order. Process env vars are usually < ~200 entries —
        // wiping and refilling is cheap and avoids juggling diff logic to keep the
        // ObservableCollection identity-stable across refreshes.
        EnvEntries.Clear();
        var sorted = new List<KeyValueEntry>();
        foreach (DictionaryEntry kv in Environment.GetEnvironmentVariables())
        {
            var key = kv.Key?.ToString() ?? "";
            var value = kv.Value?.ToString() ?? "";
            sorted.Add(new KeyValueEntry(key, value));
        }
        sorted.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Key, b.Key));
        foreach (var entry in sorted)
        {
            EnvEntries.Add(entry);
        }
    }

    private void RefreshScope()
    {
        ScopeEntries.Clear();
        foreach (var kv in _env.Bindings.OrderBy(b => b.Key, StringComparer.Ordinal))
        {
            // Hide internal bookkeeping bindings the bridges install (prefixed with double underscores).
            if (kv.Key.StartsWith("__", StringComparison.Ordinal)) continue;

            var formatted = Printer.Format(kv.Value);
            if (formatted.Length > 80) formatted = formatted[..77] + "...";

            // Mark functions and records as read-only — editing them as a flat NinjaShell
            // expression usually fails to parse back, and the resulting confusion outweighs
            // the rare case of legitimately wanting to replace a function binding.
            var typeHint = DescribeType(kv.Value);
            // Only let leaf scalar values be edited as flat NinjaShell expressions.
            // Functions and records are shown read-only with a type hint.
            var editable = IsEditableScalar(kv.Value);
            ScopeEntries.Add(new KeyValueEntry(kv.Key, formatted, hint: $"({typeHint})", editable: editable));
        }
    }

    private static bool IsEditableScalar(NValue v) => v switch
    {
        NInt => true,
        NFloat => true,
        NString => true,
        NBool => true,
        NUnit => true,
        _ => false,
    };

    private static string DescribeType(NValue v) => v switch
    {
        NInt => "int",
        NFloat => "float",
        NString => "string",
        NBool => "bool",
        NUnit => "unit",
        NList => "list",
        NRecord => "record",
        NVariant => "variant",
        NSeq => "seq",
        NFunc => "fn",
        _ => "?",
    };
}
