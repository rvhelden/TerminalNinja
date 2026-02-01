using TerminalNinja.Primitives;

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
        var style = new Style(typeof(Label));
        
        // Assert
        await Assert.That(style.TargetType).IsEqualTo(typeof(Label));
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
        var baseStyle = new Style(typeof(Label));
        var derivedStyle = new Style(typeof(Label));
        
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
        setter.Property = "BackgroundColor";
        setter.Value = Color.Red;
        
        // Assert
        await Assert.That(setter.Property).IsEqualTo("BackgroundColor");
        await Assert.That(setter.Value).IsEqualTo(Color.Red);
    }

    #endregion

    #region Style Application

    [Test]
    public async Task ApplyStyle_SetterWithStringProperty_SetsValue()
    {
        // Arrange
        var label = new Label();
        var style = new Style(typeof(Label));
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
        var rect = new Rectangle();
        var style = new Style(typeof(Rectangle));
        style.Setters.Add(new Setter("BackgroundColor", Color.Blue));
        
        // Act
        rect.Style = style;
        
        // Assert
        await Assert.That(rect.BackgroundColor).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task ApplyStyle_MultipleSetters_SetsAllValues()
    {
        // Arrange
        var label = new Label();
        var style = new Style(typeof(Label));
        style.Setters.Add(new Setter("Text", "Hello"));
        style.Setters.Add(new Setter("ForegroundColor", Color.Cyan));
        
        // Act
        label.Style = style;
        
        // Assert
        await Assert.That(label.Text).IsEqualTo("Hello");
        await Assert.That(label.ForegroundColor).IsEqualTo(Color.Cyan);
    }

    [Test]
    public async Task ApplyStyle_SetterWithNullProperty_IsSkipped()
    {
        // Arrange
        var label = new Label { Text = "Original" };
        var style = new Style(typeof(Label));
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
        var label = new Label();
        var style = new Style(typeof(Rectangle)); // Wrong type
        
        // Act & Assert
        await Assert.That(() => label.Style = style)
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task ApplyStyle_NullTargetType_DoesNotThrow()
    {
        // Arrange
        var label = new Label();
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
        // Label extends FrameworkElement, which extends ElementBase
        var label = new Label();
        var style = new Style(typeof(ElementBase)); // Base type
        style.Setters.Add(new Setter("Name", "StyledLabel"));
        
        // Act
        label.Style = style;
        
        // Assert
        await Assert.That(label.Name).IsEqualTo("StyledLabel");
    }

    #endregion

    #region Property Validation

    [Test]
    public async Task ApplyStyle_NonExistentProperty_ThrowsInvalidOperationException()
    {
        // Arrange
        var label = new Label();
        var style = new Style(typeof(Label));
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
        var label = new Label();
        var style = new Style(typeof(Label));
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
        var label = new Label();
        var style1 = new Style(typeof(Label));
        style1.Setters.Add(new Setter("Text", "Style1"));
        
        var style2 = new Style(typeof(Label));
        style2.Setters.Add(new Setter("Text", "Style2"));
        
        // Act
        label.Style = style1;
        label.Style = style2;
        
        // Assert - Second style is applied
        await Assert.That(label.Text).IsEqualTo("Style2");
    }

    #endregion
}
