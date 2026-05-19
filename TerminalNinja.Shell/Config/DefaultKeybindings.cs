namespace TerminalNinja.Shell.Config;

/// <summary>
/// Seeds a <see cref="NinjaConfig"/> with the line-editor keybindings that ship
/// out of the box. Currently a single entry: <c>Ctrl+E</c> → <c>edit-config</c>,
/// which opens <c>~/.ninjarc</c> in VS Code from anywhere in the REPL.
/// </summary>
/// <remarks>
/// Called from <c>NinjaRepl</c> before <c>RcLoader.TryLoad</c>, so a user's
/// rc file can rebind or unbind any default — <c>key.bind("Ctrl+E", "submit")</c>
/// in <c>~/.ninjarc</c> will overwrite the seeded mapping.
/// </remarks>
public static class DefaultKeybindings
{
    /// <summary>
    /// Bind the standard line-editor keybindings into <paramref name="config"/>.
    /// Safe to call multiple times — later calls overwrite earlier bindings on
    /// the same chord.
    /// </summary>
    public static void Seed(NinjaConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.BindKey("Ctrl+E", "edit-config");
    }
}
