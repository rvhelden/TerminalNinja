using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.PowerShell;

/// <summary>
/// Spawns a PowerShell subprocess per <c>pwsh { ... }</c> block and marshals its
/// output back into <see cref="NValue"/>s via <see cref="JsonToNValue"/>. JSON
/// channel only (CLIXML is a follow-up).
/// </summary>
public static class PwshBridge
{
    private static readonly object _resolveLock = new();
    private static string? _resolvedPath;
    private static bool _resolveAttempted;

    /// <summary>The resolved path to <c>pwsh</c>, or <c>null</c> if no PowerShell host is on PATH.</summary>
    public static string? ResolvedPath
    {
        get
        {
            lock (_resolveLock)
            {
                if (!_resolveAttempted)
                {
                    _resolvedPath = ResolvePwsh();
                    _resolveAttempted = true;
                }
                return _resolvedPath;
            }
        }
    }

    /// <summary>True when a PowerShell host is available on PATH.</summary>
    public static bool IsAvailable => ResolvedPath != null;

    /// <summary>Reset the cached executable resolution. Test-only.</summary>
    internal static void ResetResolverForTests()
    {
        lock (_resolveLock)
        {
            _resolvedPath = null;
            _resolveAttempted = false;
        }
    }

    /// <summary>
    /// Execute a PowerShell script body and parse its JSON output. Stderr or a
    /// non-zero exit is surfaced as <see cref="NVariant"/>(<c>"Error"</c>, [<see cref="NString"/>]).
    /// </summary>
    public static NValue Execute(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var pwsh = ResolvedPath ?? throw new EvaluatorException("no PowerShell host found on PATH");

        var psi = new ProcessStartInfo(pwsh)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NoLogo");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-OutputFormat");
        psi.ArgumentList.Add("Text");
        psi.ArgumentList.Add("-EncodedCommand");
        psi.ArgumentList.Add(EncodeCommand(body));

        using var proc = Process.Start(psi) ?? throw new EvaluatorException("failed to start PowerShell host");

        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            var msg = stderr.Length > 0 ? stderr.TrimEnd() : $"pwsh exited with code {proc.ExitCode}";
            return new NVariant("Error", ImmutableArray.Create<NValue>(new NString(msg)));
        }
        if (stderr.Length > 0)
        {
            // Non-fatal: stdout still ran, but PowerShell wrote warnings/errors to stderr.
            // Surface as Error variant so the caller can decide.
            return new NVariant("Error", ImmutableArray.Create<NValue>(new NString(stderr.TrimEnd())));
        }

        try
        {
            return JsonToNValue.Parse(stdout);
        }
        catch (Exception ex)
        {
            throw new EvaluatorException($"could not parse pwsh JSON output: {ex.Message}\nRaw:\n{stdout}", ex);
        }
    }

    /// <summary>Install the bridge into <paramref name="env"/> under the name <c>__pwsh_bridge__</c>.</summary>
    public static Env Install(Env env)
    {
        ArgumentNullException.ThrowIfNull(env);
        var bridge = new NFunc(args =>
        {
            if (args.Length != 1 || args[0] is not NString body)
                throw new EvaluatorException("__pwsh_bridge__ expects exactly one string argument");
            return Execute(body.Value);
        }, 1);
        return env.Extend("__pwsh_bridge__", bridge);
    }

    private static string EncodeCommand(string body)
    {
        // Wrap the user payload in `@(...)` so empty / scalar / pipeline outputs
        // all coerce to an array before ConvertTo-Json, giving the caller a
        // predictable top-level shape (JSON array or null) regardless of what
        // the block emits.
        var script =
            $"$result = & {{ {body} }}\n" +
            "if ($null -eq $result) { 'null' } " +
            "elseif ($result -is [System.Collections.IEnumerable] -and -not ($result -is [string])) { ConvertTo-Json -InputObject @($result) -Depth 8 -Compress } " +
            "else { ConvertTo-Json -InputObject $result -Depth 8 -Compress }";

        var bytes = Encoding.Unicode.GetBytes(script);
        return Convert.ToBase64String(bytes);
    }

    private static string? ResolvePwsh()
    {
        string[] candidates = OperatingSystem.IsWindows()
            ? new[] { "pwsh.exe", "pwsh", "powershell.exe" }
            : new[] { "pwsh" };

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return null;

        var entries = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var name in candidates)
        {
            foreach (var dir in entries)
            {
                string full;
                try
                {
                    full = Path.Combine(dir.Trim(), name);
                }
                catch (ArgumentException)
                {
                    continue;
                }
                if (File.Exists(full)) return full;
            }
        }
        return null;
    }
}
