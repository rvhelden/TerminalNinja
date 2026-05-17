using TerminalNinja.App;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;
using TerminalNinja.Rendering;

namespace TerminalNinja.Tests.Unit.App;

/// <summary>
/// Tests for the <see cref="ApplicationOptions.RendererOverride"/> + <see cref="ApplicationOptions.SuppressConsoleSetup"/>
/// combination — the pieces TerminalNinja.Skia uses to host the cell pipeline inside a
/// regular <see cref="Application"/>, which is what publishes
/// <see cref="Application.Current"/> for controls that depend on it.
/// </summary>
public class RendererOverrideTests
{
    [Test]
    public async Task Application_WithRendererOverride_UsesProvidedRenderer()
    {
        var sink = new MemoryCellSink();
        var injected = new Renderer(sink, 40, 12);

        using var app = new Application(new ApplicationOptions
        {
            RendererOverride = injected,
            SuppressConsoleSetup = true,
            EnableMouseTracking = false,
        });

        await Assert.That(app.Renderer).IsSameReferenceAs(injected);
        await Assert.That(app.Renderer.Width).IsEqualTo(40);
        await Assert.That(app.Renderer.Height).IsEqualTo(12);
    }

    [Test]
    public async Task Application_WithRendererOverride_HasFocusManagerAndResources()
    {
        // Controls (Window, Popup, RadioButton, FrameworkElement resource lookup) all read
        // Application.Current. We can't assert on the global Current directly without test
        // ordering races (the static is shared), but every Application instance — including
        // the one a renderer-override host creates internally — owns a FocusManager and a
        // Resources dictionary that downstream controls navigate through.
        var sink = new MemoryCellSink();
        var injected = new Renderer(sink, 20, 5);

        using var app = new Application(new ApplicationOptions
        {
            RendererOverride = injected,
            SuppressConsoleSetup = true,
        });

        await Assert.That(app.FocusManager).IsNotNull();
        await Assert.That(app.Resources).IsNotNull();
    }

    [Test]
    public async Task Application_WithRendererOverride_ResourcesAvailable()
    {
        var sink = new MemoryCellSink();
        var injected = new Renderer(sink, 20, 5);

        using var app = new Application(new ApplicationOptions
        {
            RendererOverride = injected,
            SuppressConsoleSetup = true,
        });

        app.Resources["foo"] = "bar";

        await Assert.That(app.Resources.TryGetValue("foo", out var value)).IsTrue();
        await Assert.That(value).IsEqualTo("bar");
    }

    [Test]
    public async Task Application_WithRendererOverride_ThemeLoadsBuiltIn()
    {
        var sink = new MemoryCellSink();
        var injected = new Renderer(sink, 20, 5);

        using var app = new Application(new ApplicationOptions
        {
            RendererOverride = injected,
            SuppressConsoleSetup = true,
        });

        app.ThemeName = "Dark";

        await Assert.That(app.ThemeName).IsEqualTo("Dark");
        await Assert.That(app.Resources.MergedDictionaries.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Application_ProcessTick_DrivesInjectedRenderer()
    {
        var sink = new MemoryCellSink();
        var injected = new Renderer(sink, 20, 3);

        using var app = new Application(new ApplicationOptions
        {
            RendererOverride = injected,
            SuppressConsoleSetup = true,
            EnableMouseTracking = false,
        });

        app.RootControl = new Border { Background = Color.Red };
        var drewSomething = app.ProcessTick();

        await Assert.That(drewSomething).IsTrue();
        await Assert.That(sink.BeginFrameCount).IsEqualTo(1);
        await Assert.That(sink.EndFrameCount).IsEqualTo(1);
        await Assert.That(sink.Writes.Count).IsGreaterThan(0);
    }
}
