using System.Collections;
using System.Text;
using TerminalNinja.Controls;
using TerminalNinja.DependencySystem;
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

    /// <summary>Multi-line listing of the process environment variables (sorted by name).</summary>
    public string EnvText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "";

    /// <summary>Multi-line listing of the bindings in the NinjaShell evaluator's <see cref="Env"/>.</summary>
    public string ScopeText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "";

    private const int DefaultLeftWidth = 32;
    private const int DefaultRightWidth = 48;
    private const int DefaultEnvHeight = 12;

    private bool _filesVisible = true;
    private bool _envVisible = true;
    private bool _scopeVisible = true;

    // Cell-grid dimensions are set via StackPanel.SetFixedSize on the named elements after the
    // layout loads. We grab references from XamlLoadResult.NamedElements and stash them here
    // because the XAML loader doesn't run {Binding} expressions for attached properties — the
    // value is parsed as a literal integer at load time. Zeroing the size is also the only
    // working "hide" mechanism right now (UIElement.Visibility is declared but layout-inert).
    private DependencyObject? _filesPanel;
    private DependencyObject? _rightPanel;
    private DependencyObject? _envBody;
    private DependencyObject? _scopeBody;
    private DependencyObject? _envHeader;
    private DependencyObject? _scopeHeader;

    /// <summary>Footer hint string describing the toggle shortcuts.</summary>
    public string ShortcutHint { get; } = "F1 files   F2 env   F3 scope   F10 exit";

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

        Repl.AppendOutput($"NinjaShell UI  —  type expressions; F1/F2/F3 toggle panels; F10 exits.");
        Repl.CommandEntered += OnCommandEntered;

        RefreshPanels();
    }

    /// <summary>
    /// Wires up references to the named XAML elements whose <c>StackPanel.FixedSize</c>
    /// attached property the toggle methods mutate. Must be called once after the layout
    /// has been loaded; subsequent calls overwrite the references.
    /// </summary>
    public void BindElements(IReadOnlyDictionary<string, object> namedElements)
    {
        _filesPanel = namedElements.GetValueOrDefault("FilesPanel") as DependencyObject;
        _rightPanel = namedElements.GetValueOrDefault("RightPanel") as DependencyObject;
        _envHeader = namedElements.GetValueOrDefault("EnvHeader") as DependencyObject;
        _envBody = namedElements.GetValueOrDefault("EnvBody") as DependencyObject;
        _scopeHeader = namedElements.GetValueOrDefault("ScopeHeader") as DependencyObject;
        _scopeBody = namedElements.GetValueOrDefault("ScopeBody") as DependencyObject;
    }

    /// <summary>Toggles the files panel between hidden (FixedSize=0) and shown.</summary>
    public void ToggleFilesPanel()
    {
        _filesVisible = !_filesVisible;
        if (_filesPanel is not null)
        {
            StackPanel.SetFixedSize(_filesPanel, _filesVisible ? DefaultLeftWidth : 0);
        }
    }

    /// <summary>Toggles the env panel between hidden and shown.</summary>
    public void ToggleEnvPanel()
    {
        _envVisible = !_envVisible;
        if (_envHeader is not null) StackPanel.SetFixedSize(_envHeader, _envVisible ? 1 : 0);
        if (_envBody is not null) StackPanel.SetFixedSize(_envBody, _envVisible ? DefaultEnvHeight : 0);
        UpdateRightPanelVisibility();
    }

    /// <summary>Toggles the scope panel between hidden and shown.</summary>
    public void ToggleScopePanel()
    {
        _scopeVisible = !_scopeVisible;
        if (_scopeHeader is not null) StackPanel.SetFixedSize(_scopeHeader, _scopeVisible ? 1 : 0);
        // Scope body uses Stretch sizing — collapsing it requires swapping SizeMode. For now
        // we hide the header line and let the (empty) body take any leftover space; the
        // visible footprint is just the surrounding border. A follow-up could swap SizeMode
        // to Fixed=0 when both env and scope are off.
        UpdateRightPanelVisibility();
    }

    private void UpdateRightPanelVisibility()
    {
        if (_rightPanel is null) return;
        StackPanel.SetFixedSize(_rightPanel, (_envVisible || _scopeVisible) ? DefaultRightWidth : 0);
    }

    private void OnCommandEntered(string line)
    {
        try
        {
            var result = NinjaEvaluator.EvalScript(line, _env);
            _env = result.Env;
            var rendered = Printer.Format(result.Value);
            if (!string.IsNullOrEmpty(rendered))
            {
                Repl.AppendOutput(rendered);
            }
        }
        catch (Exception ex)
        {
            Repl.AppendOutput($"error: {ex.Message}");
        }

        RefreshPanels();
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
        var sb = new StringBuilder();
        foreach (DictionaryEntry kv in Environment.GetEnvironmentVariables())
        {
            sb.Append(kv.Key).Append('=').AppendLine(kv.Value?.ToString() ?? "");
        }

        // Sort lines for stable display — Environment.GetEnvironmentVariables enumerates in
        // hash order which jumps around between refreshes.
        var lines = sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Array.Sort(lines, StringComparer.OrdinalIgnoreCase);
        EnvText = string.Join('\n', lines);
    }

    private void RefreshScope()
    {
        var sb = new StringBuilder();
        foreach (var kv in _env.Bindings.OrderBy(b => b.Key, StringComparer.Ordinal))
        {
            // Hide internal bookkeeping bindings the bridges install (prefixed with double underscores).
            if (kv.Key.StartsWith("__", StringComparison.Ordinal)) continue;

            var formatted = Printer.Format(kv.Value);
            if (formatted.Length > 60) formatted = formatted[..57] + "...";
            sb.Append(kv.Key).Append(" = ").AppendLine(formatted);
        }
        ScopeText = sb.Length == 0 ? "(no bindings)" : sb.ToString();
    }
}
