using System.Diagnostics;
using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Tests.Unit.Shell;

public class ProcModuleTests
{
    private static NValue Run(string source)
        => NinjaEvaluator.EvalSource(source, BuiltinRegistry.CreateDefaultEnv()).Value;

    [Test]
    public async Task ProcPid_ReturnsCurrentProcessId()
    {
        var v = Run("proc.pid()");
        if (v is not NInt pid) throw new InvalidOperationException("expected NInt");
        await Assert.That(pid.Value).IsEqualTo(System.Environment.ProcessId);
    }

    [Test]
    public async Task ProcHostname_ReturnsNonEmptyString()
    {
        var v = Run("proc.hostname()");
        if (v is not NString s) throw new InvalidOperationException();
        await Assert.That(s.Value.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task ProcUser_ReturnsNonEmptyString()
    {
        var v = Run("proc.user()");
        if (v is not NString s) throw new InvalidOperationException();
        await Assert.That(s.Value.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task ProcHome_ReturnsExistingDirectory()
    {
        var v = Run("proc.home()");
        if (v is not NString s) throw new InvalidOperationException();
        await Assert.That(Directory.Exists(s.Value)).IsTrue();
    }

    [Test]
    public async Task ProcOs_ReportsKnownPlatform()
    {
        var v = Run("proc.os()");
        if (v is not NString s) throw new InvalidOperationException();
        var known = new[] { "Windows", "Linux", "macOS", "FreeBSD" };
        await Assert.That(known).Contains(s.Value);
    }

    [Test]
    public async Task ProcArch_ReportsKnownArchitecture()
    {
        var v = Run("proc.arch()");
        if (v is not NString s) throw new InvalidOperationException();
        var known = new[] { "x64", "x86", "arm", "arm64", "wasm" };
        await Assert.That(known).Contains(s.Value);
    }

    [Test]
    public async Task ProcArgs_ReturnsListOfStrings()
    {
        var v = Run("proc.args()");
        if (v is not NList list) throw new InvalidOperationException("expected NList");
        // We can't predict the test runner's exact args, but every entry must be an NString.
        foreach (var item in list.Items)
            await Assert.That(item is NString).IsTrue();
    }

    [Test]
    public async Task ProcSleep_PausesAndReturnsUnit()
    {
        var sw = Stopwatch.StartNew();
        var v = Run("proc.sleep(50)");
        sw.Stop();
        await Assert.That(v is NUnit).IsTrue();
        await Assert.That(sw.ElapsedMilliseconds).IsGreaterThanOrEqualTo(45);
    }

    [Test]
    public async Task ProcSleep_NegativeMs_Throws()
    {
        await Assert.That(() => Run("proc.sleep(-1)")).ThrowsExactly<EvaluatorException>();
    }

    // proc.exit is intentionally not unit-tested — it calls Environment.Exit
    // which would terminate the test runner. Smoke-tested manually with
    // `ninja -c "proc.exit(7)"; echo $LASTEXITCODE` → 7.
}
