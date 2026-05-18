using TerminalNinja.Shell.Config;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Tests.Unit;

public class NinjaConfigTests
{
    private static NFunc DummyFunc(int arity = 0) => new(_ => NUnit.Instance, arity);

    [Test]
    public async Task Empty_HasNoAliasesOrKeybindings()
    {
        var c = NinjaConfig.Empty();
        await Assert.That(c.Aliases.Count).IsEqualTo(0);
        await Assert.That(c.Keybindings.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SetAlias_AddsBinding()
    {
        var c = NinjaConfig.Empty();
        var fn = DummyFunc();
        c.SetAlias("cd", fn);
        await Assert.That(c.Aliases.Count).IsEqualTo(1);
        await Assert.That(c.Aliases["cd"] is NFunc nf && ReferenceEquals(nf, fn)).IsTrue();
    }

    [Test]
    public async Task SetAlias_Overwrites_ExistingBinding()
    {
        var c = NinjaConfig.Empty();
        var fn1 = DummyFunc(0);
        var fn2 = DummyFunc(1);
        c.SetAlias("cd", fn1);
        c.SetAlias("cd", fn2);
        await Assert.That(c.Aliases["cd"] is NFunc nf && ReferenceEquals(nf, fn2)).IsTrue();
    }

    [Test]
    public async Task SetAlias_NullName_Throws()
    {
        var c = NinjaConfig.Empty();
        await Assert.That(() => c.SetAlias(null!, DummyFunc())).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task SetAlias_EmptyName_Throws()
    {
        var c = NinjaConfig.Empty();
        await Assert.That(() => c.SetAlias("", DummyFunc())).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task SetAlias_WhitespaceName_Throws()
    {
        var c = NinjaConfig.Empty();
        await Assert.That(() => c.SetAlias("   ", DummyFunc())).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task SetAlias_NullValue_Throws()
    {
        var c = NinjaConfig.Empty();
        await Assert.That(() => c.SetAlias("cd", null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task SetAlias_NonCallable_Throws()
    {
        var c = NinjaConfig.Empty();
        await Assert.That(() => c.SetAlias("cd", (NValue)new NString("fs.cd")))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task RemoveAlias_ReturnsTrue_WhenPresent()
    {
        var c = NinjaConfig.Empty();
        c.SetAlias("cd", DummyFunc());
        await Assert.That(c.RemoveAlias("cd")).IsTrue();
        await Assert.That(c.Aliases.ContainsKey("cd")).IsFalse();
    }

    [Test]
    public async Task RemoveAlias_ReturnsFalse_WhenAbsent()
    {
        var c = NinjaConfig.Empty();
        await Assert.That(c.RemoveAlias("nope")).IsFalse();
    }

    [Test]
    public async Task TryGetAlias_FindsExisting()
    {
        var c = NinjaConfig.Empty();
        var fn = DummyFunc();
        c.SetAlias("ls", fn);
        var ok = c.TryGetAlias("ls", out var found);
        await Assert.That(ok).IsTrue();
        await Assert.That(found is NFunc nf && ReferenceEquals(nf, fn)).IsTrue();
    }

    [Test]
    public async Task TryGetAlias_ReturnsFalse_ForUnknown()
    {
        var c = NinjaConfig.Empty();
        var ok = c.TryGetAlias("missing", out _);
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task Aliases_Snapshot_DoesNotReflect_LaterMutation()
    {
        var c = NinjaConfig.Empty();
        c.SetAlias("a", DummyFunc());
        var snapshot = c.Aliases;
        c.SetAlias("b", DummyFunc());
        await Assert.That(snapshot.Count).IsEqualTo(1);
        await Assert.That(snapshot.ContainsKey("a")).IsTrue();
        await Assert.That(snapshot.ContainsKey("b")).IsFalse();
    }

    [Test]
    public async Task BindKey_AddsKeybinding()
    {
        var c = NinjaConfig.Empty();
        c.BindKey("Ctrl+L", "clear");
        await Assert.That(c.Keybindings["Ctrl+L"]).IsEqualTo("clear");
    }

    [Test]
    public async Task BindKey_NullChord_Throws()
    {
        var c = NinjaConfig.Empty();
        await Assert.That(() => c.BindKey(null!, "clear")).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task BindKey_EmptyChord_Throws()
    {
        var c = NinjaConfig.Empty();
        await Assert.That(() => c.BindKey("", "clear")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task BindKey_NullAction_Throws()
    {
        var c = NinjaConfig.Empty();
        await Assert.That(() => c.BindKey("Ctrl+L", null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task BindKey_EmptyAction_Throws()
    {
        var c = NinjaConfig.Empty();
        await Assert.That(() => c.BindKey("Ctrl+L", "")).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task UnbindKey_ReturnsTrue_WhenPresent()
    {
        var c = NinjaConfig.Empty();
        c.BindKey("Ctrl+L", "clear");
        await Assert.That(c.UnbindKey("Ctrl+L")).IsTrue();
        await Assert.That(c.Keybindings.ContainsKey("Ctrl+L")).IsFalse();
    }

    [Test]
    public async Task UnbindKey_ReturnsFalse_WhenAbsent()
    {
        var c = NinjaConfig.Empty();
        await Assert.That(c.UnbindKey("Ctrl+X")).IsFalse();
    }

    [Test]
    public async Task TryGetAction_FindsExisting()
    {
        var c = NinjaConfig.Empty();
        c.BindKey("Ctrl+R", "history-prev");
        var ok = c.TryGetAction("Ctrl+R", out var action);
        await Assert.That(ok).IsTrue();
        await Assert.That(action).IsEqualTo("history-prev");
    }

    [Test]
    public async Task TryGetAction_ReturnsFalse_ForUnknown()
    {
        var c = NinjaConfig.Empty();
        var ok = c.TryGetAction("Ctrl+Q", out _);
        await Assert.That(ok).IsFalse();
    }

    [Test]
    public async Task ConcurrentSetAlias_AllWritesObserved()
    {
        var c = NinjaConfig.Empty();
        const int writers = 16;
        const int perWriter = 25;
        var tasks = new Task[writers];
        for (int w = 0; w < writers; w++)
        {
            int wi = w;
            tasks[w] = Task.Run(() =>
            {
                for (int i = 0; i < perWriter; i++) c.SetAlias($"w{wi}_k{i}", DummyFunc());
            });
        }
        await Task.WhenAll(tasks);
        await Assert.That(c.Aliases.Count).IsEqualTo(writers * perWriter);
    }
}
