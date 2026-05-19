using TerminalNinja.Shell.Repl;

namespace TerminalNinja.Shell.Tests.Unit;

public class NinjaReplAliasTests
{
    private static (string stdout, string stderr) RunRepl(string script)
    {
        // Feed the script + an explicit exit so the REPL terminates.
        var input = new StringReader(script + "\nexit\n");
        var output = new StringWriter();
        var error = new StringWriter();
        var repl = new NinjaRepl(input, output, error);
        repl.Run();
        return (output.ToString(), error.ToString());
    }

    [Test]
    public async Task DefaultAlias_Pwd_PrintsCurrentDirectory()
    {
        var (stdout, _) = RunRepl("pwd");
        await Assert.That(stdout.Contains(Directory.GetCurrentDirectory())).IsTrue();
    }

    [Test]
    public async Task UserAlias_AfterSet_Works()
    {
        // After alias.set, typing `isd <path>` should call fs.is_dir(path)
        // and render its NBool result through the REPL printer.
        var (stdout, _) = RunRepl("alias.set(\"isd\", fs.is_dir)\nisd \".\"");
        await Assert.That(stdout.Contains("true")).IsTrue();
    }

    [Test]
    public async Task LambdaAlias_PassesTokenAsString()
    {
        var (stdout, _) = RunRepl("alias.set(\"isd\", path => fs.is_dir(path))\nisd \".\"");
        await Assert.That(stdout.Contains("true")).IsTrue();
    }

    [Test]
    public async Task ExpressionForm_NotIntercepted()
    {
        // Calling fs.cd directly with parens should NOT go through the interceptor;
        // it should reach the evaluator and fail because the dir does not exist.
        var (_, stderr) = RunRepl("fs.cd(\"___never_a_real_dir_xyz_42___\")");
        await Assert.That(stderr.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task LetBindingShadowingAliasName_NotIntercepted()
    {
        var (stdout, _) = RunRepl("let cd = 1 in cd");
        await Assert.That(stdout.Contains("1")).IsTrue();
    }

    [Test]
    public async Task QuotedArg_KeepsSpacesAsSingleArg()
    {
        // Single quoted token must reach fs.is_dir as ONE arg. A non-existent
        // directory still parses correctly: arity-OK call returns NBool false.
        // If the tokenizer split on spaces inside quotes the call would arrive
        // with 3 args and fs.is_dir would throw "expects 1 argument, got 3".
        var (stdout, stderr) = RunRepl("alias.set(\"isd\", fs.is_dir)\nisd \"no such dir here\"");
        await Assert.That(stdout.Contains("false")).IsTrue();
        await Assert.That(stderr.Contains("got 3")).IsFalse();
    }

    [Test]
    public async Task AliasArityMismatch_SurfacesEvaluatorException()
    {
        // cd takes exactly 1 arg; bare `cd` invokes with 0, fs.cd throws.
        var (_, stderr) = RunRepl("cd");
        await Assert.That(stderr.Contains("fs.cd")).IsTrue();
    }
}
