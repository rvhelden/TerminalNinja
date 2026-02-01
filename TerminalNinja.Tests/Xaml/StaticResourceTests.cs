using TerminalNinja.Xaml.Markup;

namespace TerminalNinja.Tests.Xaml;

/// <summary>
/// Tests for StaticResourceExtension covering:
/// - Resource key validation
/// - Pending lookup registration
/// - Resource resolution from element tree
/// - Integration with FrameworkElement.TryFindResource
/// </summary>
public class StaticResourceExtensionTests
{
    #region Constructor and Properties

    [Test]
    public async Task Constructor_Default_ResourceKeyIsNull()
    {
        // Arrange & Act
        var extension = new StaticResourceExtension();
        
        // Assert
        await Assert.That(extension.ResourceKey).IsNull();
    }

    [Test]
    public async Task Constructor_WithResourceKey_SetsResourceKey()
    {
        // Arrange & Act
        var extension = new StaticResourceExtension("MyKey");
        
        // Assert
        await Assert.That(extension.ResourceKey).IsEqualTo("MyKey");
    }

    [Test]
    public async Task ResourceKey_SetValue_UpdatesProperty()
    {
        // Arrange
        var extension = new StaticResourceExtension();
        
        // Act
        extension.ResourceKey = "AnotherKey";
        
        // Assert
        await Assert.That(extension.ResourceKey).IsEqualTo("AnotherKey");
    }

    #endregion

    #region GetAndClearPendingLookups

    [Test]
    public async Task GetAndClearPendingLookups_NoPendingLookups_ReturnsEmptyList()
    {
        // Arrange - Clear any existing lookups first
        StaticResourceExtension.GetAndClearPendingLookups();
        
        // Act
        var lookups = StaticResourceExtension.GetAndClearPendingLookups();
        
        // Assert
        await Assert.That(lookups.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetAndClearPendingLookups_ClearsLookups()
    {
        // Arrange - Clear any existing lookups first
        StaticResourceExtension.GetAndClearPendingLookups();
        
        // Act
        var firstCall = StaticResourceExtension.GetAndClearPendingLookups();
        var secondCall = StaticResourceExtension.GetAndClearPendingLookups();
        
        // Assert
        await Assert.That(secondCall.Count).IsEqualTo(0);
    }

    #endregion

    #region Resource Lookup Integration

    [Test]
    public async Task TryFindResource_LocalResource_FindsResource()
    {
        // Arrange
        var window = new Window();
        window.Resources.Add("TestColor", Color.Red);
        
        // Act
        var result = window.TryFindResource("TestColor");
        
        // Assert
        await Assert.That(result).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task TryFindResource_ParentResource_FindsResource()
    {
        // Arrange
        var window = new Window();
        window.Resources.Add("TestColor", Color.Blue);
        
        var label = new Label();
        window.Content = label; // This sets label.Parent = window
        
        // Act
        var result = label.TryFindResource("TestColor");
        
        // Assert
        await Assert.That(result).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task TryFindResource_NestedParent_WalksUpTree()
    {
        // Arrange
        var window = new Window();
        window.Resources.Add("RootColor", Color.Green);
        
        var stack = new Stack { Orientation = StackOrientation.Vertical };
        var label = new Label();
        stack.Children.Add(StackChild.Auto(label));
        window.Content = stack;
        
        // Manually set parent relationships
        stack.Parent = window;
        label.Parent = stack;
        
        // Act
        var result = label.TryFindResource("RootColor");
        
        // Assert
        await Assert.That(result).IsEqualTo(Color.Green);
    }

    [Test]
    public async Task TryFindResource_LocalOverridesParent()
    {
        // Arrange
        var window = new Window();
        window.Resources.Add("Color", Color.Red);
        
        var stack = new Stack { Orientation = StackOrientation.Vertical };
        stack.Resources.Add("Color", Color.Blue); // Override
        stack.Parent = window;
        
        // Act
        var windowResult = window.TryFindResource("Color");
        var stackResult = stack.TryFindResource("Color");
        
        // Assert
        await Assert.That(windowResult).IsEqualTo(Color.Red);
        await Assert.That(stackResult).IsEqualTo(Color.Blue);
    }

    [Test]
    public async Task TryFindResource_NotFound_ReturnsNull()
    {
        // Arrange
        var window = new Window();
        
        // Act
        var result = window.TryFindResource("NonExistent");
        
        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task FindResource_NotFound_ThrowsResourceNotFoundException()
    {
        // Arrange
        var window = new Window();
        
        // Act & Assert
        await Assert.That(() => window.FindResource("NonExistent"))
            .ThrowsExactly<ResourceNotFoundException>();
    }

    [Test]
    public async Task FindResource_Found_ReturnsResource()
    {
        // Arrange
        var window = new Window();
        window.Resources.Add("MyValue", "Test");
        
        // Act
        var result = window.FindResource("MyValue");
        
        // Assert
        await Assert.That(result).IsEqualTo("Test");
    }

    #endregion

    #region Resource Types

    [Test]
    public async Task TryFindResource_ColorResource_ReturnsColor()
    {
        // Arrange
        var window = new Window();
        window.Resources.Add("AccentColor", Color.Cyan);
        
        // Act
        var result = window.TryFindResource("AccentColor");
        
        // Assert
        await Assert.That(result).IsEqualTo(Color.Cyan);
    }

    [Test]
    public async Task TryFindResource_StyleResource_ReturnsStyle()
    {
        // Arrange
        var style = new Style(typeof(Label));
        style.Setters.Add(new Setter("Text", "Styled"));
        
        var window = new Window();
        window.Resources.Add("LabelStyle", style);
        
        // Act
        var result = window.TryFindResource("LabelStyle");
        
        // Assert
        await Assert.That(result).IsEqualTo(style);
    }

    [Test]
    public async Task TryFindResource_StringResource_ReturnsString()
    {
        // Arrange
        var window = new Window();
        window.Resources.Add("WelcomeText", "Hello, World!");
        
        // Act
        var result = window.TryFindResource("WelcomeText");
        
        // Assert
        await Assert.That(result).IsEqualTo("Hello, World!");
    }

    [Test]
    public async Task TryFindResource_IntResource_ReturnsInt()
    {
        // Arrange
        var window = new Window();
        window.Resources.Add("MaxItems", 100);
        
        // Act
        var result = window.TryFindResource("MaxItems");
        
        // Assert
        await Assert.That(result).IsEqualTo(100);
    }

    #endregion

    #region MergedDictionaries Integration

    [Test]
    public async Task TryFindResource_MergedDictionary_FindsResource()
    {
        // Arrange
        var merged = new ResourceDictionary();
        merged.Add("MergedColor", Color.Magenta);
        
        var window = new Window();
        window.Resources.MergedDictionaries.Add(merged);
        
        // Act
        var result = window.TryFindResource("MergedColor");
        
        // Assert
        await Assert.That(result).IsEqualTo(Color.Magenta);
    }

    [Test]
    public async Task TryFindResource_LocalOverridesMerged()
    {
        // Arrange
        var merged = new ResourceDictionary();
        merged.Add("Color", Color.Red);
        
        var window = new Window();
        window.Resources.MergedDictionaries.Add(merged);
        window.Resources.Add("Color", Color.Blue); // Local override
        
        // Act
        var result = window.TryFindResource("Color");
        
        // Assert
        await Assert.That(result).IsEqualTo(Color.Blue);
    }

    #endregion
}
