using TerminalNinja.Resources;

namespace TerminalNinja.Tests.Unit.Resources;

/// <summary>
/// Tests for ResourceDictionary covering:
/// - Basic CRUD operations (Add, Get, Remove, Clear)
/// - Key lookup with merged dictionaries
/// - Hierarchical lookup priority (local > merged in reverse order)
/// - Edge cases (null keys, missing keys)
/// </summary>
public class ResourceDictionaryTests
{
    #region Basic Operations

    [Test]
    public async Task Add_ValidKeyValue_StoresResource()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        
        // Act
        dictionary.Add("key1", "value1");
        
        // Assert
        await Assert.That(dictionary["key1"]).IsEqualTo("value1");
    }

    [Test]
    public async Task Indexer_Set_StoresResource()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        
        // Act
        dictionary["key1"] = "value1";
        
        // Assert
        await Assert.That(dictionary["key1"]).IsEqualTo("value1");
    }

    [Test]
    public async Task Indexer_Get_MissingKey_ThrowsKeyNotFoundException()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        
        // Act & Assert
        await Assert.That(() => _ = dictionary["missing"])
            .ThrowsExactly<KeyNotFoundException>();
    }

    [Test]
    public async Task Add_NullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        
        // Act & Assert
        await Assert.That(() => dictionary.Add(null!, "value"))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task Add_DuplicateKey_ThrowsArgumentException()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        dictionary.Add("key1", "value1");
        
        // Act & Assert
        await Assert.That(() => dictionary.Add("key1", "value2"))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Remove_ExistingKey_ReturnsTrue()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        dictionary.Add("key1", "value1");
        
        // Act
        var result = dictionary.Remove("key1");
        
        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(dictionary.ContainsKey("key1")).IsFalse();
    }

    [Test]
    public async Task Remove_MissingKey_ReturnsFalse()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        
        // Act
        var result = dictionary.Remove("missing");
        
        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Clear_RemovesAllResources()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        dictionary.Add("key1", "value1");
        dictionary.Add("key2", "value2");
        
        // Act
        dictionary.Clear();
        
        // Assert
        await Assert.That(dictionary.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Count_ReturnsNumberOfResources()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        dictionary.Add("key1", "value1");
        dictionary.Add("key2", "value2");
        
        // Assert
        await Assert.That(dictionary.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Keys_ReturnsAllKeys()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        dictionary.Add("key1", "value1");
        dictionary.Add("key2", "value2");
        
        // Assert
        await Assert.That(dictionary.Keys).Contains("key1");
        await Assert.That(dictionary.Keys).Contains("key2");
    }

    [Test]
    public async Task Values_ReturnsAllValues()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        dictionary.Add("key1", "value1");
        dictionary.Add("key2", "value2");
        
        // Assert
        await Assert.That(dictionary.Values).Contains("value1");
        await Assert.That(dictionary.Values).Contains("value2");
    }

    #endregion

    #region ContainsKey and TryGetValue

    [Test]
    public async Task ContainsKey_ExistingKey_ReturnsTrue()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        dictionary.Add("key1", "value1");
        
        // Assert
        await Assert.That(dictionary.ContainsKey("key1")).IsTrue();
    }

    [Test]
    public async Task ContainsKey_MissingKey_ReturnsFalse()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        
        // Assert
        await Assert.That(dictionary.ContainsKey("missing")).IsFalse();
    }

    [Test]
    public async Task TryGetValue_ExistingKey_ReturnsTrueAndValue()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        dictionary.Add("key1", "value1");
        
        // Act
        var result = dictionary.TryGetValue("key1", out var value);
        
        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(value).IsEqualTo("value1");
    }

    [Test]
    public async Task TryGetValue_MissingKey_ReturnsFalse()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        
        // Act
        var result = dictionary.TryGetValue("missing", out var value);
        
        // Assert
        await Assert.That(result).IsFalse();
        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task TryGetValue_NullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        
        // Act & Assert
        await Assert.That(() => dictionary.TryGetValue(null!, out _))
            .ThrowsExactly<ArgumentNullException>();
    }

    #endregion

    #region Merged Dictionaries

    [Test]
    public async Task MergedDictionaries_LookupInMerged_FindsResource()
    {
        // Arrange
        var merged = new ResourceDictionary();
        merged.Add("mergedKey", "mergedValue");
        
        var dictionary = new ResourceDictionary();
        dictionary.MergedDictionaries.Add(merged);
        
        // Act
        var result = dictionary.TryGetValue("mergedKey", out var value);
        
        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(value).IsEqualTo("mergedValue");
    }

    [Test]
    public async Task MergedDictionaries_LocalOverridesMerged()
    {
        // Arrange
        var merged = new ResourceDictionary();
        merged.Add("key", "mergedValue");
        
        var dictionary = new ResourceDictionary();
        dictionary.MergedDictionaries.Add(merged);
        dictionary.Add("key", "localValue");
        
        // Act
        var result = dictionary.TryGetValue("key", out var value);
        
        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(value).IsEqualTo("localValue");
    }

    [Test]
    public async Task MergedDictionaries_LastAddedHasHigherPriority()
    {
        // Arrange
        var merged1 = new ResourceDictionary();
        merged1.Add("key", "value1");
        
        var merged2 = new ResourceDictionary();
        merged2.Add("key", "value2");
        
        var dictionary = new ResourceDictionary();
        dictionary.MergedDictionaries.Add(merged1);
        dictionary.MergedDictionaries.Add(merged2);
        
        // Act
        var result = dictionary.TryGetValue("key", out var value);
        
        // Assert - merged2 was added last, so it has higher priority
        await Assert.That(result).IsTrue();
        await Assert.That(value).IsEqualTo("value2");
    }

    [Test]
    public async Task MergedDictionaries_NestedMergedDictionaries()
    {
        // Arrange - nested: dict -> merged1 -> nested
        var nested = new ResourceDictionary();
        nested.Add("nestedKey", "nestedValue");
        
        var merged1 = new ResourceDictionary();
        merged1.MergedDictionaries.Add(nested);
        
        var dictionary = new ResourceDictionary();
        dictionary.MergedDictionaries.Add(merged1);
        
        // Act
        var result = dictionary.TryGetValue("nestedKey", out var value);
        
        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(value).IsEqualTo("nestedValue");
    }

    [Test]
    public async Task ContainsKey_FindsKeyInMergedDictionary()
    {
        // Arrange
        var merged = new ResourceDictionary();
        merged.Add("mergedKey", "mergedValue");
        
        var dictionary = new ResourceDictionary();
        dictionary.MergedDictionaries.Add(merged);
        
        // Assert
        await Assert.That(dictionary.ContainsKey("mergedKey")).IsTrue();
    }

    [Test]
    public async Task Count_DoesNotIncludeMergedDictionaries()
    {
        // Arrange
        var merged = new ResourceDictionary();
        merged.Add("key1", "value1");
        merged.Add("key2", "value2");
        
        var dictionary = new ResourceDictionary();
        dictionary.MergedDictionaries.Add(merged);
        dictionary.Add("localKey", "localValue");
        
        // Assert - Count only includes local resources
        await Assert.That(dictionary.Count).IsEqualTo(1);
    }

    #endregion

    #region Null Value Handling

    [Test]
    public async Task Add_NullValue_IsAllowed()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        
        // Act
        dictionary.Add("key", null);
        
        // Assert
        await Assert.That(dictionary.ContainsKey("key")).IsTrue();
        await Assert.That(dictionary["key"]).IsNull();
    }

    [Test]
    public async Task TryGetValue_NullValue_ReturnsTrueWithNull()
    {
        // Arrange
        var dictionary = new ResourceDictionary();
        dictionary.Add("key", null);
        
        // Act
        var result = dictionary.TryGetValue("key", out var value);
        
        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(value).IsNull();
    }

    #endregion

    #region Enumeration

    [Test]
    public async Task GetEnumerator_EnumeratesLocalResourcesOnly()
    {
        // Arrange
        var merged = new ResourceDictionary();
        merged.Add("mergedKey", "mergedValue");
        
        var dictionary = new ResourceDictionary();
        dictionary.MergedDictionaries.Add(merged);
        dictionary.Add("localKey", "localValue");
        
        // Act
        var keys = dictionary.Select(kvp => kvp.Key).ToList();
        
        // Assert - Only local resources are enumerated
        await Assert.That(keys.Count).IsEqualTo(1);
        await Assert.That(keys).Contains("localKey");
    }

    #endregion
}
