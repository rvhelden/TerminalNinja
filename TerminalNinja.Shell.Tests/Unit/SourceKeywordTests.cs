using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Parser;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Tests.Unit;

/// <summary>
/// Covers the <c>source("path")</c> keyword: top-level-only parsing, scope
/// extension across the include boundary, error surfaces (missing file, parse
/// error in sourced script, recursive include depth cap), and the in-expression
/// position being a syntax error rather than the default "unexpected token".
/// </summary>
public class SourceKeywordTests
{
    private static NValue RunScript(string source)
        => NinjaEvaluator.EvalScript(source, BuiltinRegistry.CreateDefaultEnv()).Value;

    private static string TempScript(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "ninja_src_" + Guid.NewGuid().ToString("N") + ".ninja");
        File.WriteAllText(path, content);
        return path;
    }

    private static string Escape(string p) => p.Replace("\\", "\\\\");

    [Test]
    public async Task Source_ScriptDefinesBindings_VisibleInCaller()
    {
        var included = TempScript("let from_include = 99\n");
        try
        {
            var caller = $"source(\"{Escape(included)}\")\nfrom_include + 1";
            await Assert.That(RunScript(caller)).IsEqualTo((NValue)new NInt(100));
        }
        finally { File.Delete(included); }
    }

    [Test]
    public async Task Source_LastFormValueIsReturned()
    {
        var included = TempScript("let x = 7\nx * 6\n");
        try
        {
            // The source statement itself returns the last form's value.
            await Assert.That(RunScript($"source(\"{Escape(included)}\")"))
                .IsEqualTo((NValue)new NInt(42));
        }
        finally { File.Delete(included); }
    }

    [Test]
    public async Task Source_EnvSetInIncludedScript_PersistsToCaller()
    {
        var k = "NINJA_SRC_TEST_" + Guid.NewGuid().ToString("N");
        var included = TempScript($"env.set(\"{k}\", \"hello\")\n");
        try
        {
            await Assert.That(RunScript($"source(\"{Escape(included)}\")\nenv.get(\"{k}\")"))
                .IsEqualTo((NValue)new NString("hello"));
        }
        finally
        {
            File.Delete(included);
            Environment.SetEnvironmentVariable(k, null);
        }
    }

    [Test]
    public async Task Source_MissingFile_ThrowsEvaluatorException()
    {
        var missing = Path.Combine(Path.GetTempPath(), "ninja_definitely_missing_" + Guid.NewGuid().ToString("N") + ".ninja");
        await Assert.That(() => RunScript($"source(\"{Escape(missing)}\")"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task Source_ParseErrorInIncludedScript_Surfaced()
    {
        var bad = TempScript("@illegal token");
        try
        {
            await Assert.That(() => RunScript($"source(\"{Escape(bad)}\")"))
                .ThrowsExactly<EvaluatorException>();
        }
        finally { File.Delete(bad); }
    }

    [Test]
    public async Task Source_PathExpressionIsEvaluated_NotJustLiteral()
    {
        var included = TempScript("let computed_value = 1234\n");
        try
        {
            // The path argument is any expression that evaluates to NString.
            var caller =
                $"let p = \"{Escape(included)}\"\n" +
                "source(p)\n" +
                "computed_value";
            await Assert.That(RunScript(caller)).IsEqualTo((NValue)new NInt(1234));
        }
        finally { File.Delete(included); }
    }

    [Test]
    public async Task Source_RecursiveInclude_HitsDepthCap()
    {
        // A file that sources itself — depth grows until the 32-deep cap throws.
        string selfSourcing = Path.Combine(Path.GetTempPath(), "ninja_self_" + Guid.NewGuid().ToString("N") + ".ninja");
        File.WriteAllText(selfSourcing, $"source(\"{Escape(selfSourcing)}\")");
        try
        {
            await Assert.That(() => RunScript($"source(\"{Escape(selfSourcing)}\")"))
                .ThrowsExactly<EvaluatorException>();
        }
        finally { File.Delete(selfSourcing); }
    }

    [Test]
    public async Task Source_InsideExpression_IsParserError()
    {
        // `source` is top-level only — used as an expression it's a syntax error,
        // not a runtime error.
        await Assert.That(() => RunScript("let x = source(\"foo\") in x"))
            .ThrowsExactly<ParserException>();
    }

    [Test]
    public async Task Source_InsidePipe_IsParserError()
    {
        await Assert.That(() => RunScript("[1, 2] | source(\"foo\")"))
            .ThrowsExactly<ParserException>();
    }

    [Test]
    public async Task Source_TwoLevelsDeep_BindingsFlowAllTheWayUp()
    {
        var inner = TempScript("let deep = 7\n");
        var middle = TempScript($"source(\"{Escape(inner)}\")\nlet middle_var = deep * 2\n");
        try
        {
            var caller = $"source(\"{Escape(middle)}\")\nmiddle_var + deep";
            await Assert.That(RunScript(caller)).IsEqualTo((NValue)new NInt(21));
        }
        finally
        {
            File.Delete(inner);
            File.Delete(middle);
        }
    }

    [Test]
    public async Task Source_EmptyFile_ReturnsUnit_NoBindingsAdded()
    {
        var empty = TempScript("");
        try
        {
            var v = RunScript($"source(\"{Escape(empty)}\")");
            await Assert.That(v is NUnit).IsTrue();
        }
        finally { File.Delete(empty); }
    }

    [Test]
    public async Task Source_NonStringPath_RaisesEvaluatorException()
    {
        // The path expression evaluates, then we check it's NString at runtime.
        await Assert.That(() => RunScript("source(42)")).ThrowsExactly<EvaluatorException>();
    }
}
