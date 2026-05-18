using TerminalNinja.Shell.Config;

namespace TerminalNinja.Shell.Tests.Unit;

public class DefaultKeybindingsTests
{
    [Test]
    public async Task Seed_BindsCtrlE_ToEditConfig()
    {
        var config = NinjaConfig.Empty();
        DefaultKeybindings.Seed(config);
        await Assert.That(config.Keybindings.ContainsKey("Ctrl+E")).IsTrue();
        await Assert.That(config.Keybindings["Ctrl+E"]).IsEqualTo("edit-config");
    }

    [Test]
    public async Task Seed_IsIdempotent()
    {
        var config = NinjaConfig.Empty();
        DefaultKeybindings.Seed(config);
        DefaultKeybindings.Seed(config);
        await Assert.That(config.Keybindings.Count).IsEqualTo(1);
        await Assert.That(config.Keybindings["Ctrl+E"]).IsEqualTo("edit-config");
    }
}
