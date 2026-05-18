using TerminalNinja.Shell.Runtime;

namespace TerminalNinja.Shell.Repl;

/// <summary>
/// Reads <c>~/.ninjarc</c> at REPL startup and evaluates it against the live
/// environment. The rc file is a regular NinjaShell script: each top-level form
/// is run via <see cref="NinjaEvaluator.EvalScript"/>, so calls like
/// <c>alias.set("zz", fs.pwd)</c> and <c>key.bind("Ctrl+L", "clear")</c> mutate
/// the <c>NinjaConfig</c> through the side effects already wired into those
/// modules.
/// </summary>
/// <remarks>
/// Errors (missing file, parse error, runtime error) are reported to the
/// supplied <c>error</c> writer but never throw — a broken rc file must not
/// prevent the REPL from starting. The script does not see a different
/// environment than an interactive user would: it shares the same
/// <see cref="Env"/> instance.
/// </remarks>
public static class RcLoader
{
    /// <summary>The default rc path: <c>$HOME/.ninjarc</c>.</summary>
    public static string DefaultPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ninjarc");

    /// <summary>
    /// Load and evaluate <paramref name="path"/> if it exists. Silent on a
    /// missing file; writes a single-line message to <paramref name="error"/>
    /// on parse or runtime failure.
    /// </summary>
    public static void TryLoad(string path, Env env, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(error);

        if (!File.Exists(path)) return;

        string source;
        try { source = File.ReadAllText(path); }
        catch (Exception ex)
        {
            error.WriteLine($"ninjarc: failed to read '{path}': {ex.Message}");
            return;
        }

        try
        {
            NinjaEvaluator.EvalScript(source, env);
        }
        catch (EvaluatorException ex)
        {
            error.WriteLine($"ninjarc: runtime error: {ex.Message}");
        }
        catch (Exception ex)
        {
            error.WriteLine($"ninjarc: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
