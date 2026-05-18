using TerminalNinja.Shell.Config;
using TerminalNinja.Shell.Repl;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Tests.Unit;

public class AliasInterceptorTests
{
    private static NFunc Marker() => new(_ => NUnit.Instance, -1);

    private static NinjaConfig WithAliases(params (string name, NFunc fn)[] pairs)
    {
        var c = NinjaConfig.Empty();
        foreach (var (name, fn) in pairs) c.SetAlias(name, fn);
        return c;
    }

    [Test]
    public async Task BareAlias_NoArgs_Intercepts()
    {
        var fn = Marker();
        var c = WithAliases(("cd", fn));
        var ok = AliasInterceptor.TryIntercept("cd", c, out var inv);
        await Assert.That(ok).IsTrue();
        await Assert.That(inv.Name).IsEqualTo("cd");
        await Assert.That(inv.Func is NFunc nf && ReferenceEquals(nf, fn)).IsTrue();
        await Assert.That(inv.Args.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AliasWithSingleArg_Intercepts()
    {
        var fn = Marker();
        var c = WithAliases(("cd", fn));
        var ok = AliasInterceptor.TryIntercept("cd foo", c, out var inv);
        await Assert.That(ok).IsTrue();
        await Assert.That(inv.Args.Length).IsEqualTo(1);
        await Assert.That(inv.Args[0] is NString s && s.Value == "foo").IsTrue();
    }

    [Test]
    public async Task AliasWithQuotedArg_KeepsSpacesInOneArg()
    {
        var c = WithAliases(("cd", Marker()));
        var ok = AliasInterceptor.TryIntercept("cd \"my docs\"", c, out var inv);
        await Assert.That(ok).IsTrue();
        await Assert.That(inv.Args.Length).IsEqualTo(1);
        await Assert.That(inv.Args[0] is NString s && s.Value == "my docs").IsTrue();
    }

    [Test]
    public async Task AliasWithMultipleArgs_Intercepts()
    {
        var c = WithAliases(("ls", Marker()));
        var ok = AliasInterceptor.TryIntercept("ls /tmp -r", c, out var inv);
        await Assert.That(ok).IsTrue();
        await Assert.That(inv.Args.Length).IsEqualTo(2);
        await Assert.That(inv.Args[0] is NString s1 && s1.Value == "/tmp").IsTrue();
        await Assert.That(inv.Args[1] is NString s2 && s2.Value == "-r").IsTrue();
    }

    [Test]
    public async Task UnknownAlias_DoesNotIntercept()
    {
        var c = WithAliases(("cd", Marker()));
        var ok = AliasInterceptor.TryIntercept("unknown foo", c, out _);
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task EmptyAliasMap_DoesNotIntercept()
    {
        var c = NinjaConfig.Empty();
        var ok = AliasInterceptor.TryIntercept("cd foo", c, out _);
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task EmptyLine_DoesNotIntercept()
    {
        var c = WithAliases(("cd", Marker()));
        var ok = AliasInterceptor.TryIntercept("", c, out _);
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task WhitespaceOnly_DoesNotIntercept()
    {
        var c = WithAliases(("cd", Marker()));
        var ok = AliasInterceptor.TryIntercept("   ", c, out _);
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task LeadingWhitespace_StillIntercepts()
    {
        var fn = Marker();
        var c = WithAliases(("cd", fn));
        var ok = AliasInterceptor.TryIntercept("   cd foo", c, out var inv);
        await Assert.That(ok).IsTrue();
        await Assert.That(inv.Args.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AliasFollowedByOpenParen_DoesNotIntercept()
    {
        var c = WithAliases(("cd", Marker()));
        var ok = AliasInterceptor.TryIntercept("cd(\"foo\")", c, out _);
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task AliasFollowedByDot_DoesNotIntercept()
    {
        var c = WithAliases(("cd", Marker()));
        var ok = AliasInterceptor.TryIntercept("cd.something", c, out _);
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task AliasFollowedByEquals_DoesNotIntercept()
    {
        var c = WithAliases(("cd", Marker()));
        var ok = AliasInterceptor.TryIntercept("cd = 1", c, out _);
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task LineStartsWithKeyword_DoesNotIntercept_EvenIfAliasNameAppearsLater()
    {
        var c = WithAliases(("cd", Marker()));
        // First token is 'let', not 'cd'.
        var ok = AliasInterceptor.TryIntercept("let cd = 1 in cd", c, out _);
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task UnquotedPipe_SplitsHeadFromPipelineTail()
    {
        var fn = Marker();
        var c = WithAliases(("cd", fn));
        var ok = AliasInterceptor.TryIntercept("cd foo | print", c, out var inv);
        await Assert.That(ok).IsTrue();
        await Assert.That(inv.Args.Length).IsEqualTo(1);
        await Assert.That(inv.Args[0] is NString s && s.Value == "foo").IsTrue();
        await Assert.That(inv.PipelineTail).IsEqualTo("print");
    }

    [Test]
    public async Task PipelineTail_PreservesMultiStagePipeline()
    {
        var c = WithAliases(("ls", Marker()));
        var ok = AliasInterceptor.TryIntercept("ls | where(x => x) | select(x => x)", c, out var inv);
        await Assert.That(ok).IsTrue();
        await Assert.That(inv.Args.Length).IsEqualTo(0);
        // Only the first `|` splits — every later stage stays in the tail so the parser
        // sees the full pipeline expression verbatim.
        await Assert.That(inv.PipelineTail).IsEqualTo("where(x => x) | select(x => x)");
    }

    [Test]
    public async Task BareAlias_NoTail_LeavesPipelineTailNull()
    {
        var c = WithAliases(("cd", Marker()));
        var ok = AliasInterceptor.TryIntercept("cd foo", c, out var inv);
        await Assert.That(ok).IsTrue();
        await Assert.That(inv.PipelineTail).IsNull();
    }

    [Test]
    public async Task DoublePipe_NotTreatedAsSplit()
    {
        // `||` is logical-or, not a pipeline operator — `cd foo || bar` is an expression
        // and must fall through to the parser instead of being intercepted.
        var c = WithAliases(("cd", Marker()));
        var ok = AliasInterceptor.TryIntercept("cd foo || bar", c, out _);
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task DanglingPipe_DoesNotIntercept()
    {
        // No tail after `|` means a real syntax error — bail to the parser so the user
        // gets a proper diagnostic instead of silent no-op behavior.
        var c = WithAliases(("cd", Marker()));
        var ok = AliasInterceptor.TryIntercept("cd foo |   ", c, out _);
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task UnquotedFatArrowInArgs_AbortsIntercept()
    {
        var c = WithAliases(("cd", Marker()));
        var ok = AliasInterceptor.TryIntercept("cd foo => bar", c, out _);
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task QuotedPipeInArgs_StillIntercepts()
    {
        var fn = Marker();
        var c = WithAliases(("echo", fn));
        var ok = AliasInterceptor.TryIntercept("echo \"a | b\"", c, out var inv);
        await Assert.That(ok).IsTrue();
        await Assert.That(inv.Args[0] is NString s && s.Value == "a | b").IsTrue();
    }

    [Test]
    public async Task UnterminatedQuote_DoesNotIntercept()
    {
        var c = WithAliases(("cd", Marker()));
        var ok = AliasInterceptor.TryIntercept("cd \"oops", c, out _);
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task EscapedQuoteArg_PreservesLiteralQuote()
    {
        var fn = Marker();
        var c = WithAliases(("echo", fn));
        var ok = AliasInterceptor.TryIntercept("echo \"a\\\"b\"", c, out var inv);
        await Assert.That(ok).IsTrue();
        await Assert.That(inv.Args[0] is NString s && s.Value == "a\"b").IsTrue();
    }

    [Test]
    public async Task FirstTokenIsExpressionOperator_DoesNotIntercept()
    {
        var c = WithAliases(("cd", Marker()));
        // Identifier-shaped first-token requirement: digits are not identifier-start.
        var ok = AliasInterceptor.TryIntercept("123 cd", c, out _);
        await Assert.That(ok).IsFalse();
    }
}
