namespace TerminalNinja.Tests.Xaml;

/// <summary>
/// Tests for AOT-safe System.Type resolution in XAML property values.
/// Covers DataTemplate.DataType, Style.TargetType, and all resolution
/// formats: simple name, prefixed name, and fully-qualified name.
/// </summary>
public class TypeResolutionTests
{
    // ─── Simple name resolution (default XAML namespace) ─────────

    [Test]
    public async Task DataType_SimpleName_ResolvesControlType()
    {
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <DataTemplate x:Key="T1" DataType="Button">
                        <TextBlock Text="test" />
                    </DataTemplate>
                </Window.Resources>
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml);
        var template = window.TryFindResource("T1") as DataTemplate;

        await Assert.That(template).IsNotNull();
        await Assert.That(template!.DataType).IsEqualTo(typeof(Button));
    }

    [Test]
    public async Task DataType_SimpleName_ResolvesTextBlock()
    {
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <DataTemplate x:Key="T1" DataType="TextBlock">
                        <TextBlock Text="test" />
                    </DataTemplate>
                </Window.Resources>
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml);
        var template = window.TryFindResource("T1") as DataTemplate;

        await Assert.That(template).IsNotNull();
        await Assert.That(template!.DataType).IsEqualTo(typeof(TextBlock));
    }

    [Test]
    public async Task DataType_SimpleName_ResolvesPrimitiveType()
    {
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <DataTemplate x:Key="T1" DataType="Color">
                        <TextBlock Text="test" />
                    </DataTemplate>
                </Window.Resources>
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml);
        var template = window.TryFindResource("T1") as DataTemplate;

        await Assert.That(template).IsNotNull();
        await Assert.That(template!.DataType).IsEqualTo(typeof(Color));
    }

    // ─── Fully-qualified name resolution ────────────────────────

    [Test]
    public async Task DataType_FullyQualifiedName_ResolvesType()
    {
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <DataTemplate x:Key="T1" DataType="TerminalNinja.Controls.Border">
                        <TextBlock Text="test" />
                    </DataTemplate>
                </Window.Resources>
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml);
        var template = window.TryFindResource("T1") as DataTemplate;

        await Assert.That(template).IsNotNull();
        await Assert.That(template!.DataType).IsEqualTo(typeof(Border));
    }

    // ─── Prefixed name resolution (clr-namespace xmlns) ─────────

    [Test]
    public async Task DataType_PrefixedName_ResolvesType()
    {
        // Use the TerminalNinja.Xaml.Binding namespace as a custom xmlns
        // to test prefix resolution with a known registered type
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:b="clr-namespace:TerminalNinja.Xaml.Binding;assembly=TerminalNinja">
                <Window.Resources>
                    <DataTemplate x:Key="T1" DataType="b:RelativeSource">
                        <TextBlock Text="test" />
                    </DataTemplate>
                </Window.Resources>
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml);
        var template = window.TryFindResource("T1") as DataTemplate;

        await Assert.That(template).IsNotNull();
        await Assert.That(template!.DataType).IsEqualTo(typeof(TerminalNinja.Xaml.Binding.RelativeSource));
    }

    // ─── Style.TargetType (another Type property) ───────────────

    [Test]
    public async Task StyleTargetType_SimpleName_ResolvesType()
    {
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <Style x:Key="S1" TargetType="TextBlock">
                        <Setter Property="Foreground" Value="Red" />
                    </Style>
                </Window.Resources>
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml);
        var style = window.TryFindResource("S1") as Style;

        await Assert.That(style).IsNotNull();
        await Assert.That(style!.TargetType).IsEqualTo(typeof(TextBlock));
    }

    [Test]
    public async Task StyleTargetType_FullyQualifiedName_ResolvesType()
    {
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <Style x:Key="S1" TargetType="TerminalNinja.Controls.Button">
                        <Setter Property="Foreground" Value="Red" />
                    </Style>
                </Window.Resources>
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml);
        var style = window.TryFindResource("S1") as Style;

        await Assert.That(style).IsNotNull();
        await Assert.That(style!.TargetType).IsEqualTo(typeof(Button));
    }

    // ─── Error cases ────────────────────────────────────────────

    [Test]
    public async Task DataType_UnknownType_ThrowsWithHelpfulMessage()
    {
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <DataTemplate x:Key="T1" DataType="NonExistentType">
                        <TextBlock Text="test" />
                    </DataTemplate>
                </Window.Resources>
            </Window>
            """;

        await Assert.That(() => TerminalXaml.Load<Window>(xaml))
            .ThrowsExactly<InvalidOperationException>()
            .And
            .HasMessageContaining("NonExistentType");
    }

    [Test]
    public async Task DataType_UnknownPrefix_ThrowsWithHelpfulMessage()
    {
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <DataTemplate x:Key="T1" DataType="bogus:Foo">
                        <TextBlock Text="test" />
                    </DataTemplate>
                </Window.Resources>
            </Window>
            """;

        await Assert.That(() => TerminalXaml.Load<Window>(xaml))
            .ThrowsExactly<InvalidOperationException>()
            .And
            .HasMessageContaining("bogus:Foo");
    }

    // ─── DataTemplate with DataType does not interfere with content ──

    [Test]
    public async Task DataType_WithTemplateContent_BothWork()
    {
        var xaml = """
            <Window xmlns="http://schemas.terminalninja.dev/xaml"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Window.Resources>
                    <DataTemplate x:Key="T1" DataType="Button">
                        <TextBlock Text="Template Content" Foreground="White" />
                    </DataTemplate>
                </Window.Resources>
            </Window>
            """;

        var window = TerminalXaml.Load<Window>(xaml);
        var template = window.TryFindResource("T1") as DataTemplate;

        await Assert.That(template).IsNotNull();
        await Assert.That(template!.DataType).IsEqualTo(typeof(Button));
        await Assert.That(template.TemplateContent).IsNotNull();

        var tb = template.TemplateContent as TextBlock;
        await Assert.That(tb).IsNotNull();
        await Assert.That(tb!.Text).IsEqualTo("Template Content");
    }
}
