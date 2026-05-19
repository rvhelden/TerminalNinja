using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Tests.Unit;

public class EnvModuleTests
{
    private static NValue Run(string source)
        => NinjaEvaluator.EvalSource(source, BuiltinRegistry.CreateDefaultEnv()).Value;

    // Tests use a uniquely-prefixed variable name to avoid clobbering anything
    // the user might have set in the shell that hosts the test runner.
    private const string Prefix = "NINJA_TEST_ENV_";

    private static string Key(string name) => Prefix + name;

    [Test]
    public async Task EnvGet_ExistingVar_ReturnsValue()
    {
        var k = Key("get_basic");
        Environment.SetEnvironmentVariable(k, "hello");
        try
        {
            await Assert.That(Run($"env.get(\"{k}\")"))
                .IsEqualTo((NValue)new NString("hello"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(k, null);
        }
    }

    [Test]
    public async Task EnvGet_MissingVar_NoDefault_Throws()
    {
        var k = Key("get_missing");
        Environment.SetEnvironmentVariable(k, null);
        await Assert.That(() => Run($"env.get(\"{k}\")"))
            .ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task EnvGet_MissingVar_WithDefault_ReturnsDefault()
    {
        var k = Key("get_default");
        Environment.SetEnvironmentVariable(k, null);
        await Assert.That(Run($"env.get(\"{k}\", \"fallback\")"))
            .IsEqualTo((NValue)new NString("fallback"));
    }

    [Test]
    public async Task EnvHas_ReportsExistence()
    {
        var k = Key("has");
        Environment.SetEnvironmentVariable(k, "v");
        try
        {
            await Assert.That(Run($"env.has(\"{k}\")")).IsEqualTo((NValue)new NBool(true));
        }
        finally
        {
            Environment.SetEnvironmentVariable(k, null);
        }
        await Assert.That(Run($"env.has(\"{k}\")")).IsEqualTo((NValue)new NBool(false));
    }

    [Test]
    public async Task EnvSet_NewVar_ReturnsUnit_ThenReadsBack()
    {
        var k = Key("set_new");
        Environment.SetEnvironmentVariable(k, null);
        try
        {
            var prev = Run($"env.set(\"{k}\", \"hello\")");
            await Assert.That(prev is NUnit).IsTrue();
            await Assert.That(Environment.GetEnvironmentVariable(k)).IsEqualTo("hello");
        }
        finally
        {
            Environment.SetEnvironmentVariable(k, null);
        }
    }

    [Test]
    public async Task EnvSet_ExistingVar_ReturnsPreviousValue()
    {
        var k = Key("set_overwrite");
        Environment.SetEnvironmentVariable(k, "original");
        try
        {
            var prev = Run($"env.set(\"{k}\", \"new\")");
            await Assert.That(prev).IsEqualTo((NValue)new NString("original"));
            await Assert.That(Environment.GetEnvironmentVariable(k)).IsEqualTo("new");
        }
        finally
        {
            Environment.SetEnvironmentVariable(k, null);
        }
    }

    [Test]
    public async Task EnvSet_SaveRestoreIdiom_Works()
    {
        var k = Key("save_restore");
        Environment.SetEnvironmentVariable(k, "before");
        try
        {
            var script = $"let prev = env.set(\"{k}\", \"during\") in let _ = env.set(\"{k}\", prev) in env.get(\"{k}\")";
            await Assert.That(Run(script)).IsEqualTo((NValue)new NString("before"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(k, null);
        }
    }

    [Test]
    public async Task EnvSet_BulkRecord_AppliesAllReturnsPreviousMap()
    {
        var k1 = Key("bulk1");
        var k2 = Key("bulk2");
        Environment.SetEnvironmentVariable(k1, "old1");
        Environment.SetEnvironmentVariable(k2, null);
        try
        {
            var script = $"env.set({{ \"{k1}\": \"new1\", \"{k2}\": \"new2\" }})";
            var prev = Run(script);
            if (prev is not NRecord rec) throw new InvalidOperationException("expected record");
            await Assert.That(rec.Fields[k1]).IsEqualTo((NValue)new NString("old1"));
            await Assert.That(rec.Fields[k2] is NUnit).IsTrue();
            await Assert.That(Environment.GetEnvironmentVariable(k1)).IsEqualTo("new1");
            await Assert.That(Environment.GetEnvironmentVariable(k2)).IsEqualTo("new2");
        }
        finally
        {
            Environment.SetEnvironmentVariable(k1, null);
            Environment.SetEnvironmentVariable(k2, null);
        }
    }

    [Test]
    public async Task EnvUnset_RemovesVarAndReturnsPrevious()
    {
        var k = Key("unset");
        Environment.SetEnvironmentVariable(k, "doomed");
        try
        {
            var prev = Run($"env.unset(\"{k}\")");
            await Assert.That(prev).IsEqualTo((NValue)new NString("doomed"));
            await Assert.That(Environment.GetEnvironmentVariable(k)).IsNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable(k, null);
        }
    }

    [Test]
    public async Task EnvAll_ReturnsRecordWithEveryVar()
    {
        var k = Key("all_probe");
        Environment.SetEnvironmentVariable(k, "x");
        try
        {
            var v = Run("env.all()");
            if (v is not NRecord rec) throw new InvalidOperationException("expected record");
            await Assert.That(rec.Fields.ContainsKey(k)).IsTrue();
            await Assert.That(rec.Fields[k]).IsEqualTo((NValue)new NString("x"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(k, null);
        }
    }

    [Test]
    public async Task EnvSet_EmptyName_Throws()
    {
        await Assert.That(() => Run("env.set(\"\", \"v\")")).ThrowsExactly<EvaluatorException>();
    }

    [Test]
    public async Task EnvSet_NameWithEquals_Throws()
    {
        await Assert.That(() => Run("env.set(\"bad=name\", \"v\")")).ThrowsExactly<EvaluatorException>();
    }
}
