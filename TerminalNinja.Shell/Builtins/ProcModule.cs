using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Threading;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Builtins;

/// <summary>
/// The <c>proc</c> module — current-process introspection and lifecycle control.
/// </summary>
public static class ProcModule
{
    /// <summary>Register the <c>proc</c> module into the default-environment builder.</summary>
    public static void Register(ImmutableDictionary<string, NValue>.Builder b)
    {
        BuiltinRegistry.RegisterModule(b, "proc",
            ("args", new NFunc(Args, 0)),
            ("pid", new NFunc(Pid, 0)),
            ("hostname", new NFunc(Hostname, 0)),
            ("user", new NFunc(User, 0)),
            ("home", new NFunc(Home, 0)),
            ("os", new NFunc(Os, 0)),
            ("arch", new NFunc(Arch, 0)),
            ("exit", new NFunc(Exit, 1)),
            ("sleep", new NFunc(Sleep, 1)));
    }

    private static NValue Args(NValue[] args)
    {
        if (args.Length != 0) throw new EvaluatorException($"proc.args expects 0 arguments, got {args.Length}");
        var cmdLine = System.Environment.GetCommandLineArgs();
        // Drop arg[0] (the executable path) so users get just the user-supplied tail.
        var b = ImmutableArray.CreateBuilder<NValue>(Math.Max(0, cmdLine.Length - 1));
        for (int i = 1; i < cmdLine.Length; i++) b.Add(new NString(cmdLine[i]));
        return new NList(b.MoveToImmutable());
    }

    private static NValue Pid(NValue[] args)
    {
        if (args.Length != 0) throw new EvaluatorException($"proc.pid expects 0 arguments, got {args.Length}");
        return new NInt(System.Environment.ProcessId);
    }

    private static NValue Hostname(NValue[] args)
    {
        if (args.Length != 0) throw new EvaluatorException($"proc.hostname expects 0 arguments, got {args.Length}");
        return new NString(System.Environment.MachineName);
    }

    private static NValue User(NValue[] args)
    {
        if (args.Length != 0) throw new EvaluatorException($"proc.user expects 0 arguments, got {args.Length}");
        return new NString(System.Environment.UserName);
    }

    private static NValue Home(NValue[] args)
    {
        if (args.Length != 0) throw new EvaluatorException($"proc.home expects 0 arguments, got {args.Length}");
        return new NString(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile));
    }

    private static NValue Os(NValue[] args)
    {
        if (args.Length != 0) throw new EvaluatorException($"proc.os expects 0 arguments, got {args.Length}");
        if (OperatingSystem.IsWindows()) return new NString("Windows");
        if (OperatingSystem.IsMacOS()) return new NString("macOS");
        if (OperatingSystem.IsLinux()) return new NString("Linux");
        if (OperatingSystem.IsFreeBSD()) return new NString("FreeBSD");
        return new NString(RuntimeInformation.OSDescription);
    }

    private static NValue Arch(NValue[] args)
    {
        if (args.Length != 0) throw new EvaluatorException($"proc.arch expects 0 arguments, got {args.Length}");
        return new NString(RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            Architecture.Arm64 => "arm64",
            Architecture.Wasm => "wasm",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
        });
    }

    private static NValue Exit(NValue[] args)
    {
        if (args.Length != 1) throw new EvaluatorException($"proc.exit expects 1 argument, got {args.Length}");
        if (args[0] is not NInt code) throw new EvaluatorException("proc.exit code must be an int");
        System.Environment.Exit((int)code.Value);
        return NUnit.Instance; // unreachable
    }

    private static NValue Sleep(NValue[] args)
    {
        if (args.Length != 1) throw new EvaluatorException($"proc.sleep expects 1 argument, got {args.Length}");
        if (args[0] is not NInt ms) throw new EvaluatorException("proc.sleep ms must be an int");
        if (ms.Value < 0) throw new EvaluatorException("proc.sleep ms must be non-negative");
        Thread.Sleep((int)Math.Min(ms.Value, int.MaxValue));
        return NUnit.Instance;
    }
}
