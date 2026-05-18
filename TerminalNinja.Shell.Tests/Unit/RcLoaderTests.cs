using TerminalNinja.Shell.Builtins;
using TerminalNinja.Shell.Config;
using TerminalNinja.Shell.Repl;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Tests.Unit;

public class RcLoaderTests
{
    [Test]
    public async Task TryLoad_MissingFile_DoesNothing()
    {
        var config = NinjaConfig.Empty();
        var env = BuiltinRegistry.CreateDefaultEnvWith(config);
        var error = new StringWriter();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ninjarc");
        // Path deliberately does not exist.
        RcLoader.TryLoad(path, env, error);
        await Assert.That(config.Aliases.Count).IsEqualTo(0);
        await Assert.That(error.ToString()).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task TryLoad_ValidScript_PopulatesConfig()
    {
        var config = NinjaConfig.Empty();
        var env = BuiltinRegistry.CreateDefaultEnvWith(config);
        var error = new StringWriter();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ninjarc");
        try
        {
            File.WriteAllText(path, "alias.set(\"zz\", fs.pwd)\n");
            RcLoader.TryLoad(path, env, error);
            await Assert.That(config.Aliases.ContainsKey("zz")).IsTrue();
            var stored = config.Aliases["zz"];
            if (env.Lookup("fs") is not NRecord fs) throw new InvalidOperationException();
            var expected = fs.Fields["pwd"];
            await Assert.That(stored is NFunc nf && expected is NFunc en && ReferenceEquals(nf, en)).IsTrue();
            await Assert.That(error.ToString()).IsEqualTo(string.Empty);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task TryLoad_SyntaxError_WritesStderr_LeavesConfigUntouched()
    {
        var config = NinjaConfig.Empty();
        var env = BuiltinRegistry.CreateDefaultEnvWith(config);
        var error = new StringWriter();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ninjarc");
        try
        {
            File.WriteAllText(path, "alias.set(\"oops\",\n");  // unterminated call
            RcLoader.TryLoad(path, env, error);
            await Assert.That(config.Aliases.ContainsKey("oops")).IsFalse();
            await Assert.That(error.ToString().Length).IsGreaterThan(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task TryLoad_RuntimeError_WritesStderr_DoesNotThrow()
    {
        var config = NinjaConfig.Empty();
        var env = BuiltinRegistry.CreateDefaultEnvWith(config);
        var error = new StringWriter();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ninjarc");
        try
        {
            // Calling alias.set with a non-callable value is a runtime error, not a parse error.
            File.WriteAllText(path, "alias.set(\"bad\", \"not a function\")\n");
            RcLoader.TryLoad(path, env, error);
            await Assert.That(config.Aliases.ContainsKey("bad")).IsFalse();
            await Assert.That(error.ToString().Length).IsGreaterThan(0);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
