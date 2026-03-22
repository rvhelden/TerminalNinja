using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace TerminalNinja.Resources;

/// <summary>
/// A dictionary that holds resources (styles, colors, etc.) that can be looked up by key.
/// Supports hierarchical lookup through merged dictionaries.
/// </summary>
public class ResourceDictionary : IDictionary<object, object?>
{
    private readonly Dictionary<object, object?> _resources = new();
    private readonly List<ResourceDictionary> _mergedDictionaries = new();
    
    /// <summary>
    /// Gets the collection of merged ResourceDictionaries.
    /// Resources are looked up in merged dictionaries in reverse order (last added = highest priority).
    /// </summary>
    public IList<ResourceDictionary> MergedDictionaries => _mergedDictionaries;
    
    /// <summary>
    /// Gets or sets a resource by key.
    /// </summary>
    public object? this[object key]
    {
        get
        {
            if (TryGetValue(key, out var value))
            {
                return value;
            }

            throw new KeyNotFoundException($"Resource with key '{key}' not found");
        }
        set => _resources[key] = value;
    }
    
    /// <summary>
    /// Gets the collection of keys in this dictionary (not including merged dictionaries).
    /// </summary>
    public ICollection<object> Keys => _resources.Keys;
    
    /// <summary>
    /// Gets the collection of values in this dictionary (not including merged dictionaries).
    /// </summary>
    public ICollection<object?> Values => _resources.Values;
    
    /// <summary>
    /// Gets the count of resources in this dictionary (not including merged dictionaries).
    /// </summary>
    public int Count => _resources.Count;
    
    /// <summary>
    /// Gets whether this dictionary is read-only.
    /// </summary>
    public bool IsReadOnly => false;
    
    /// <summary>
    /// Adds a resource with the specified key.
    /// </summary>
    public void Add(object key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _resources.Add(key, value);
    }
    
    /// <summary>
    /// Adds a key-value pair to the dictionary.
    /// </summary>
    public void Add(KeyValuePair<object, object?> item)
    {
        Add(item.Key, item.Value);
    }
    
    /// <summary>
    /// Clears all resources from this dictionary (not merged dictionaries).
    /// </summary>
    public void Clear()
    {
        _resources.Clear();
    }
    
    /// <summary>
    /// Checks if this dictionary contains the specified key-value pair.
    /// </summary>
    public bool Contains(KeyValuePair<object, object?> item)
    {
        return _resources.Contains(item);
    }
    
    /// <summary>
    /// Checks if a resource with the specified key exists in this dictionary or merged dictionaries.
    /// </summary>
    public bool ContainsKey(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        
        // Check this dictionary first
        if (_resources.ContainsKey(key))
        {
            return true;
        }

        // Check merged dictionaries in reverse order
        for (var i = _mergedDictionaries.Count - 1; i >= 0; i--)
        {
            if (_mergedDictionaries[i].ContainsKey(key))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Copies the resources to an array.
    /// </summary>
    public void CopyTo(KeyValuePair<object, object?>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<object, object?>>)_resources).CopyTo(array, arrayIndex);
    }
    
    /// <summary>
    /// Gets an enumerator for the resources in this dictionary.
    /// </summary>
    public IEnumerator<KeyValuePair<object, object?>> GetEnumerator()
    {
        return _resources.GetEnumerator();
    }
    
    /// <summary>
    /// Removes a resource with the specified key from this dictionary.
    /// </summary>
    public bool Remove(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _resources.Remove(key);
    }
    
    /// <summary>
    /// Removes a key-value pair from this dictionary.
    /// </summary>
    public bool Remove(KeyValuePair<object, object?> item)
    {
        return ((ICollection<KeyValuePair<object, object?>>)_resources).Remove(item);
    }
    
    /// <summary>
    /// Tries to get a resource value by key from this dictionary or merged dictionaries.
    /// </summary>
    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        
        // Check this dictionary first
        if (_resources.TryGetValue(key, out value))
        {
            return true;
        }

        // Check merged dictionaries in reverse order (last added = highest priority)
        for (var i = _mergedDictionaries.Count - 1; i >= 0; i--)
        {
            if (_mergedDictionaries[i].TryGetValue(key, out value))
            {
                return true;
            }
        }
        
        value = null;
        return false;
    }
    
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
