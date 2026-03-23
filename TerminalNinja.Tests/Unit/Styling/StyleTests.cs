namespace TerminalNinja.Tests.Unit.Styling;

/// <summary>
/// Tests for Style and Setter classes covering:
/// - Style creation and properties (TargetType, BasedOn, Setters)
/// - Setter creation and properties
/// - Style application to elements
/// - TargetType validation
/// - Property value conversion
/// </summary>
public class StyleTests
{
    #region Style Properties

    [Test]
    public async Task Style_DefaultValues_AreCorrect()
    {
        // Arrange
        var style = new Style();
        
        // Assert
        await Assert.That(style.TargetType).IsNull();
        await Assert.That(style.BasedOn).IsNull();
        await Assert.That(style.Setters).IsNotNull();
        await Assert.That(style.Setters.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Style_ConstructorWithTargetType_SetsTargetType()
    {
        // Arrange
        var style = new Style(typeof(TextBlock));
        
        // Assert
        await Assert.That(style.TargetType).IsEqualTo(typeof(TextBlock));
    }

    [Test]
    public async Task Style_AddSetter_AddsToCollection()
    {
        // Arrange
        var style = new Style();
        var setter = new Setter("Text", "Hello");
        
        // Act
        style.Setters.Add(setter);
        
        // Assert
        await Assert.That(style.Setters.Count).IsEqualTo(1);
        await Assert.That(style.Setters[0]).IsEqualTo(setter);
    }

    [Test]
    public async Task Style_BasedOn_CanBeSet()
    {
        // Arrange
        var baseStyle = new Style(typeof(TextBlock));
        var derivedStyle = new Style(typeof(TextBlock));
        
        // Act
        derivedStyle.BasedOn = baseStyle;
        
        // Assert
        await Assert.That(derivedStyle.BasedOn).IsEqualTo(baseStyle);
    }

    #endregion

    #region Setter Properties

    [Test]
    public async Task Setter_DefaultValues_AreNull()
    {
        // Arrange
        var setter = new Setter();
        
        // Assert
        await Assert.That(setter.Property).IsNull();
        await Assert.That(setter.Value).IsNull();
    }

    [Test]
    public async Task Setter_ConstructorWithPropertyAndValue_SetsProperties()
    {
        // Arrange & Act
        var setter = new Setter("Text", "Hello World");
        
        // Assert
        await Assert.That(setter.Property).IsEqualTo("Text");
        await Assert.That(setter.Value).IsEqualTo("Hello World");
    }

    [Test]
    public async Task Setter_SetProperties_UpdatesValues()
    {
        // Arrange
        var setter = new Setter();
        
        // Act
        setter.Property = "Background";
        setter.Value = Color.Red;
        
        // Assert
        await Assert.That(setter.Property).IsEqualTo("Background");
        await Assert.That(setter.Value).IsEqualTo(Color.Red);
    }

    #endregion

    #region Style Application

    [Test]
    public async Task ApplyStyle_SetterWithStringProperty_SetsValue()
    {
        // Arrange
        var label = new TextBlock();
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter("Text", "Styled Text"));
        
        // Act
        label.Style = style;
        
        // Assert
        await Assert.That(label.Text).IsEqualTo("Styled Text");
    }

    [Test]
    public async Task ApplyStyle_SetterWithColorProperty_SetsValue()
    {
        // Arrange
        var rect = new global::TerminalNinja.Controls.Border();
        var style = new Style(typeof(global::TerminalNinja.Controls.Border));
        style.Setters.Add(new Setter("Background", Color.Blue));
        
        // Act
        rect.Style = style;
        
        // Assert
        await Assert.That(rect.Background).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task ApplyStyle_MultipleSetters_SetsAllValues()
    {
        // Arrange
        var label = new TextBlock();
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter("Text", "Hello"));
        style.Setters.Add(new Setter("Foreground", Color.Cyan));
        
        // Act
        label.Style = style;
        
        // Assert
        await Assert.That(label.Text).IsEqualTo("Hello");
        await Assert.That(label.Foreground).IsEqualTo(Color.Cyan);
    }

    [Test]
    public async Task ApplyStyle_SetterWithNullProperty_IsSkipped()
    {
        // Arrange
        var label = new TextBlock { Text = "Original" };
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(null!, "Should Be Ignored"));
        style.Setters.Add(new Setter("", "Also Ignored"));
        
        // Act
        label.Style = style;
        
        // Assert - Original text unchanged
        await Assert.That(label.Text).IsEqualTo("Original");
    }

    #endregion

    #region TargetType Validation

    [Test]
    public async Task ApplyStyle_WrongTargetType_ThrowsInvalidOperationException()
    {
        // Arrange
        var label = new TextBlock();
        var style = new Style(typeof(global::TerminalNinja.Controls.Border)); // Wrong type
        
        // Act & Assert
        await Assert.That(() => label.Style = style)
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task ApplyStyle_NullTargetType_DoesNotThrow()
    {
        // Arrange
        var label = new TextBlock();
        var style = new Style(); // No target type
        style.Setters.Add(new Setter("Text", "No Target Type"));
        
        // Act
        label.Style = style;
        
        // Assert
        await Assert.That(label.Text).IsEqualTo("No Target Type");
    }

    [Test]
    public async Task ApplyStyle_DerivedTypeMatchesTargetType_Works()
    {
        // Arrange
        // TextBlock extends FrameworkElement, which extends UIElement
        var label = new TextBlock();
        var style = new Style(typeof(FrameworkElement)); // Base type
        style.Setters.Add(new Setter("Name", "StyledTextBlock"));
        
        // Act
        label.Style = style;
        
        // Assert
        await Assert.That(label.Name).IsEqualTo("StyledTextBlock");
    }

    #endregion

    #region Property Validation

    [Test]
    public async Task ApplyStyle_NonExistentProperty_ThrowsInvalidOperationException()
    {
        // Arrange
        var label = new TextBlock();
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter("NonExistentProperty", "value"));
        
        // Act & Assert
        await Assert.That(() => label.Style = style)
            .ThrowsExactly<InvalidOperationException>();
    }

    #endregion

    #region Style Property Changes

    [Test]
    public async Task Style_SetToNull_DoesNotThrow()
    {
        // Arrange
        var label = new TextBlock();
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter("Text", "Styled"));
        label.Style = style;
        
        // Act
        label.Style = null;
        
        // Assert - Previous value remains
        await Assert.That(label.Text).IsEqualTo("Styled");
        await Assert.That(label.Style).IsNull();
    }

    [Test]
    public async Task Style_ChangeStyle_AppliesNewStyle()
    {
        // Arrange
        var label = new TextBlock();
        var style1 = new Style(typeof(TextBlock));
        style1.Setters.Add(new Setter("Text", "Style1"));
        
        var style2 = new Style(typeof(TextBlock));
        style2.Setters.Add(new Setter("Text", "Style2"));
        
        // Act
        label.Style = style1;
        label.Style = style2;
        
        // Assert - Second style is applied
        await Assert.That(label.Text).IsEqualTo("Style2");
    }

    #endregion
}
