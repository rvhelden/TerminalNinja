using TerminalNinja.Shell.Runtime;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Tests.Unit;

/// <summary>
/// Tests for <see cref="Env"/>, the immutable lexical environment that backs NinjaShell's
/// evaluator. The class also exposes a small mutation hook (<see cref="Env.TrySetBindingValue"/>)
/// for tooling that wants to overwrite an existing binding without producing a new Env.
/// </summary>
public class EnvTests
{
    [Test]
    public async Task TrySetBindingValue_ExistingBinding_OverwritesSlot()
    {
        var env = Env.Empty.Extend("x", new NInt(1));

        var ok = env.TrySetBindingValue("x", new NInt(42));

        await Assert.That(ok).IsTrue();
        await Assert.That(env.Lookup("x")).IsEqualTo<NValue>(new NInt(42));
    }

    [Test]
    public async Task TrySetBindingValue_MissingBinding_ReturnsFalse()
    {
        var env = Env.Empty.Extend("x", new NInt(1));

        var ok = env.TrySetBindingValue("does_not_exist", new NInt(0));

        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task TrySetBindingValue_PreservesEnvIdentity()
    {
        // The whole point of this API vs evaluating `let x = …`: callers' references to the
        // Env keep pointing at the same instance, so subscribers don't need to rewire.
        var env = Env.Empty.Extend("x", new NInt(1));
        var same = env;

        env.TrySetBindingValue("x", new NInt(99));

        await Assert.That(ReferenceEquals(env, same)).IsTrue();
        await Assert.That(same.Lookup("x")).IsEqualTo<NValue>(new NInt(99));
    }

    [Test]
    public async Task Bindings_EnumeratesAllNames()
    {
        var env = Env.Empty
            .Extend("a", new NInt(1))
            .Extend("b", new NString("two"));

        var names = env.Bindings.Select(b => b.Key).OrderBy(s => s).ToArray();

        await Assert.That(names.Length).IsEqualTo(2);
        await Assert.That(names[0]).IsEqualTo("a");
        await Assert.That(names[1]).IsEqualTo("b");
    }
}
