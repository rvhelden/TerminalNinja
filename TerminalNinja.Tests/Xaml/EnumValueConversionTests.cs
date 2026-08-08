namespace TerminalNinja.Tests.Xaml;

/// <summary>
/// TerminalNinja's enums do not use WPF's spellings, and XAML attribute values are converted at
/// load time — so a WPF spelling used to be a runtime exception when the screen opened, not a
/// build error. These cover the WPF aliases and the failure message for a value that is simply wrong.
/// </summary>
public class EnumValueConversionTests
{
    private static string TextBlockXaml(string attributes) =>
        $"""<TextBlock xmlns="http://schemas.terminalninja.dev/xaml" Text="Hi" {attributes} />""";

    // ─── WPF aliases ────────────────────────────────────────────────

    [Test]
    public async Task Load_TextTrimmingCharacterEllipsis_MapsToEllipsis()
    {
        var textBlock = TerminalXaml.Load<TextBlock>(TextBlockXaml("""TextTrimming="CharacterEllipsis" """));

        await Assert.That(textBlock.TextTrimming).IsEqualTo(TextTrimming.Ellipsis);
    }

    [Test]
    public async Task Load_TextTrimmingWordEllipsis_MapsToEllipsis()
    {
        var textBlock = TerminalXaml.Load<TextBlock>(TextBlockXaml("""TextTrimming="WordEllipsis" """));

        await Assert.That(textBlock.TextTrimming).IsEqualTo(TextTrimming.Ellipsis);
    }

    [Test]
    public async Task Load_TextTrimmingClip_MapsToNone()
    {
        var textBlock = TerminalXaml.Load<TextBlock>(TextBlockXaml("""TextTrimming="Clip" """));

        await Assert.That(textBlock.TextTrimming).IsEqualTo(TextTrimming.None);
    }

    [Test]
    public async Task Load_TextAlignmentLeft_MapsToStart()
    {
        var textBlock = TerminalXaml.Load<TextBlock>(TextBlockXaml("""HorizontalTextAlignment="Left" """));

        await Assert.That(textBlock.HorizontalTextAlignment).IsEqualTo(TextAlignment.Start);
    }

    [Test]
    public async Task Load_TextAlignmentRight_MapsToEnd()
    {
        var textBlock = TerminalXaml.Load<TextBlock>(TextBlockXaml("""HorizontalTextAlignment="Right" """));

        await Assert.That(textBlock.HorizontalTextAlignment).IsEqualTo(TextAlignment.End);
    }

    [Test]
    public async Task Load_AlignmentLeftAndBottom_MapToStartAndEnd()
    {
        var textBlock = TerminalXaml.Load<TextBlock>(
            TextBlockXaml("""HorizontalAlignment="Left" VerticalAlignment="Bottom" """));

        await Assert.That(textBlock.HorizontalAlignment).IsEqualTo(Alignment.Start);
        await Assert.That(textBlock.VerticalAlignment).IsEqualTo(Alignment.End);
    }

    [Test]
    public async Task Load_AlignmentRightAndTop_MapToEndAndStart()
    {
        var textBlock = TerminalXaml.Load<TextBlock>(
            TextBlockXaml("""HorizontalAlignment="Right" VerticalAlignment="Top" """));

        await Assert.That(textBlock.HorizontalAlignment).IsEqualTo(Alignment.End);
        await Assert.That(textBlock.VerticalAlignment).IsEqualTo(Alignment.Start);
    }

    [Test]
    public async Task Load_TextWrappingWrapWithOverflow_MapsToWrap()
    {
        var textBlock = TerminalXaml.Load<TextBlock>(TextBlockXaml("""TextWrapping="WrapWithOverflow" """));

        await Assert.That(textBlock.TextWrapping).IsEqualTo(TextWrapping.Wrap);
    }

    // ─── Canonical spellings and case-insensitivity ─────────────────

    [Test]
    public async Task Load_CanonicalSpellings_StillWork()
    {
        var textBlock = TerminalXaml.Load<TextBlock>(TextBlockXaml(
            """TextTrimming="Ellipsis" HorizontalTextAlignment="End" HorizontalAlignment="Center" TextWrapping="Wrap" """));

        await Assert.That(textBlock.TextTrimming).IsEqualTo(TextTrimming.Ellipsis);
        await Assert.That(textBlock.HorizontalTextAlignment).IsEqualTo(TextAlignment.End);
        await Assert.That(textBlock.HorizontalAlignment).IsEqualTo(Alignment.Center);
        await Assert.That(textBlock.TextWrapping).IsEqualTo(TextWrapping.Wrap);
    }

    [Test]
    public async Task Load_CanonicalSpellingsAreCaseInsensitive()
    {
        var textBlock = TerminalXaml.Load<TextBlock>(TextBlockXaml(
            """TextTrimming="ellipsis" HorizontalTextAlignment="cENTER" """));

        await Assert.That(textBlock.TextTrimming).IsEqualTo(TextTrimming.Ellipsis);
        await Assert.That(textBlock.HorizontalTextAlignment).IsEqualTo(TextAlignment.Center);
    }

    [Test]
    public async Task Load_AliasesAreCaseInsensitive()
    {
        var textBlock = TerminalXaml.Load<TextBlock>(TextBlockXaml(
            """TextTrimming="characterellipsis" HorizontalTextAlignment="LEFT" """));

        await Assert.That(textBlock.TextTrimming).IsEqualTo(TextTrimming.Ellipsis);
        await Assert.That(textBlock.HorizontalTextAlignment).IsEqualTo(TextAlignment.Start);
    }

    // ─── The failure message ────────────────────────────────────────

    [Test]
    public async Task Load_InvalidEnumValue_MessageNamesPropertyValueAndAcceptedValues()
    {
        var exception = await Assert.That(() => TerminalXaml.Load<TextBlock>(TextBlockXaml("""TextTrimming="Nonsense" """)))
            .ThrowsExactly<ArgumentException>();

        await Assert.That(exception!.Message).Contains("Nonsense");
        await Assert.That(exception.Message).Contains("TextTrimming");
        await Assert.That(exception.Message).Contains("None");
        await Assert.That(exception.Message).Contains("Ellipsis");
        await Assert.That(exception.Message).Contains("CharacterEllipsis");
    }

    [Test]
    public async Task Load_InvalidEnumValue_MessageNamesTheOwningType()
    {
        var exception = await Assert.That(() => TerminalXaml.Load<TextBlock>(TextBlockXaml("""HorizontalAlignment="Middle" """)))
            .ThrowsExactly<ArgumentException>();

        await Assert.That(exception!.Message).Contains("TextBlock.HorizontalAlignment");
        await Assert.That(exception.Message).Contains("Start, Center, End");
    }

    [Test]
    public async Task Load_InvalidEnumValueCloseToAMember_SuggestsIt()
    {
        var exception = await Assert.That(() => TerminalXaml.Load<TextBlock>(TextBlockXaml("""TextWrapping="Wrapping" """)))
            .ThrowsExactly<ArgumentException>();

        await Assert.That(exception!.Message).Contains("Did you mean 'Wrap'?");
    }

    // ─── The same rules through a style setter ──────────────────────

    [Test]
    public async Task Style_SetterWithWpfAlias_MapsToTheRightMember()
    {
        var xaml = """
            <Border xmlns="http://schemas.terminalninja.dev/xaml">
                <Border.Resources>
                    <Style TargetType="TextBlock">
                        <Setter Property="TextTrimming" Value="CharacterEllipsis" />
                        <Setter Property="HorizontalTextAlignment" Value="Right" />
                    </Style>
                </Border.Resources>
                <TextBlock Text="Hi" />
            </Border>
            """;

        var border = TerminalXaml.Load<Border>(xaml);
        var textBlock = (TextBlock)border.Child!;

        await Assert.That(textBlock.TextTrimming).IsEqualTo(TextTrimming.Ellipsis);
        await Assert.That(textBlock.HorizontalTextAlignment).IsEqualTo(TextAlignment.End);
    }

    [Test]
    public async Task Style_SetterWithInvalidValue_MessageNamesPropertyAndAcceptedValues()
    {
        var xaml = """
            <Border xmlns="http://schemas.terminalninja.dev/xaml">
                <Border.Resources>
                    <Style TargetType="TextBlock">
                        <Setter Property="TextTrimming" Value="Nonsense" />
                    </Style>
                </Border.Resources>
                <TextBlock Text="Hi" />
            </Border>
            """;

        var exception = await Assert.That(() => TerminalXaml.Load<Border>(xaml))
            .ThrowsExactly<ArgumentException>();

        await Assert.That(exception!.Message).Contains("TextTrimming");
        await Assert.That(exception.Message).Contains("Nonsense");
        await Assert.That(exception.Message).Contains("Ellipsis");
    }

    // ─── The parser directly ────────────────────────────────────────

    [Test]
    public async Task TryParse_UnknownValue_ReturnsFalse()
    {
        var parsed = XamlEnumValues.TryParse(typeof(TextTrimming), "Nope", out var result);

        await Assert.That(parsed).IsFalse();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task DescribeAcceptedValues_ListsMembersThenAliases()
    {
        var description = XamlEnumValues.DescribeAcceptedValues(typeof(TextAlignment));

        await Assert.That(description).StartsWith("Start, Center, End");
        await Assert.That(description).Contains("Left = Start");
        await Assert.That(description).Contains("Right = End");
    }
}
