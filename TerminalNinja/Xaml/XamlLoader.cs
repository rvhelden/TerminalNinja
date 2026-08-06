using System.Windows.Data;
using System.Xml.Linq;
using TerminalNinja.Aot;
using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.Documents;
using TerminalNinja.Primitives;
using TerminalNinja.Resources;
using TerminalNinja.Styling;
using TerminalNinja.Xaml.Binding;
using TerminalNinja.Xaml.Data;

namespace TerminalNinja.Xaml;

/// <summary>
/// AOT-compatible XAML loader that parses XAML using <see cref="XDocument"/> and instantiates
/// controls via <see cref="ControlFactoryRegistry"/> and <see cref="PropertyAccessorRegistry"/>.
/// Replaces Portable.Xaml with zero runtime reflection.
/// </summary>
internal sealed class XamlLoader
{
    // Well-known XML namespace URIs
    private const string TerminalNinjaXamlNs = "http://schemas.terminalninja.dev/xaml";
    private const string XamlXNs = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string DesignTimeNs = "http://schemas.microsoft.com/expression/blend/2008";
    private const string MarkupCompatNs = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    /// <summary>
    /// Maps the TerminalNinja XAML namespace to its CLR namespace prefixes.
    /// Order matters: the first match wins when resolving a type name.
    /// </summary>
    private static readonly string[] DefaultClrNamespaces =
    [
        "TerminalNinja.Controls",
        "TerminalNinja.Controls.Primitives",
        "TerminalNinja.Controls.Charts",
        "TerminalNinja.Documents",
        "TerminalNinja.Primitives",
        "TerminalNinja.Styling",
        "TerminalNinja.Commands",
        "TerminalNinja.Resources",
        "TerminalNinja.App",
        "TerminalNinja.Xaml.Binding",
        "TerminalNinja.Xaml.Mvvm",
        "TerminalNinja.Xaml.Data"
    ];

    // Content property names are now discovered at compile time by the source generator
    // and registered in ContentPropertyRegistry via [ModuleInitializer].
    // See ControlFactoryGenerator and ContentPropertyRegistry.

    /// <summary>
    /// Binding information collected during parsing.
    /// </summary>
    private readonly List<PendingBinding> _pendingBindings = [];

    /// <summary>
    /// Named elements collected during parsing (x:Name).
    /// </summary>
    private readonly Dictionary<string, object> _namedElements = new(StringComparer.Ordinal);

    /// <summary>
    /// Extra CLR namespace → assembly mappings from xmlns:prefix="clr-namespace:...;assembly=..." declarations.
    /// </summary>
    private readonly Dictionary<string, List<string>> _xmlNsToClrNamespaces = new();

    /// <summary>
    /// Set of namespace prefixes declared as mc:Ignorable.
    /// </summary>
    private readonly HashSet<string> _ignorablePrefixes = new(StringComparer.Ordinal);

    /// <summary>
    /// Maps namespace prefix → namespace URI for the document root, so we can resolve
    /// ignorable prefixes to URIs.
    /// </summary>
    private readonly Dictionary<string, string> _prefixToUri = new(StringComparer.Ordinal);

    /// <summary>
    /// Loads and instantiates a control tree from a XAML string.
    /// </summary>
    public T Load<T>(string xaml, object? dataContext) where T : FrameworkElement
    {
        var doc = XDocument.Parse(xaml);
        return LoadFromDocument<T>(doc, dataContext);
    }

    /// <summary>
    /// Loads and instantiates a control tree from a stream.
    /// </summary>
    public T LoadFromStream<T>(Stream stream, object? dataContext) where T : FrameworkElement
    {
        var doc = XDocument.Load(stream);
        return LoadFromDocument<T>(doc, dataContext);
    }

    /// <summary>
    /// Loads XAML from a stream onto an existing root object instance.
    /// Used by <c>x:Class</c> code-behind: instead of creating a new root element,
    /// properties and children from the XAML are loaded directly onto the instance.
    /// </summary>
    public void LoadFromStreamOntoInstance<T>(Stream stream, T rootObjectInstance) where T : FrameworkElement
    {
        var doc = XDocument.Load(stream);
        var root = doc.Root ?? throw new InvalidOperationException("XAML document has no root element");

        CollectNamespaces(root);

        // Instead of creating a new element for the root, apply attributes and children
        // onto the existing instance. This is the key fix for the FindAncestor bug:
        // bindings on children resolve against this actual instance, not a transient template root.
        var type = rootObjectInstance.GetType();
        ProcessAttributes(root, rootObjectInstance, type, rootObjectInstance);
        ProcessChildElements(root, rootObjectInstance, type, rootObjectInstance);

        // Resolve {StaticResource} lookups
        ResolveStaticResources(rootObjectInstance);

        // Activate bindings via the new expression-based system
        ActivateBindings();

        // Set DataContext if provided
        // (For x:Class, DataContext is typically set later by the consumer.)
    }

    private T LoadFromDocument<T>(XDocument doc, object? dataContext) where T : FrameworkElement
    {
        var root = doc.Root ?? throw new InvalidOperationException("XAML document has no root element");

        // Collect xmlns prefix → URI mappings and mc:Ignorable prefixes
        CollectNamespaces(root);

        // Parse element tree
        var result = CreateElement(root, parent: null);

        if (result is not T typed)
        {
            var actualType = result?.GetType().Name ?? "null";
            throw new InvalidCastException(
                $"XAML root control is of type {actualType}, expected {typeof(T).Name}");
        }

        // Resolve {StaticResource} lookups now that the full tree is built
        ResolveStaticResources(typed as FrameworkElement);

        // Activate bindings via the new expression-based system.
        // All bindings are created immediately via BindingOperations.SetBinding,
        // which attaches a BindingExpression to the target DP.
        // The expression resolves its source (DataContext, RelativeSource, explicit Source)
        // on attach and re-evaluates when the environment changes.
        ActivateBindings();

        // Set DataContext (this will trigger Invalidate on DataContext-dependent bindings)
        if (dataContext != null)
        {
            typed.DataContext = dataContext;
        }

        return typed;
    }

    // ────────────────────────────────────────────────────────────────
    //  ResourceDictionary loading (standalone theme XAML files)
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a standalone <c>&lt;ResourceDictionary&gt;</c> XAML document.
    /// Used for theme files where the root element is <c>ResourceDictionary</c>
    /// rather than a <c>FrameworkElement</c>-derived control.
    /// </summary>
    /// <remarks>
    /// <c>{StaticResource}</c> references within the dictionary resolve against
    /// resources already added earlier in the same dictionary (document order).
    /// </remarks>
    public ResourceDictionary LoadResourceDictionary(string xaml)
    {
        var doc = XDocument.Parse(xaml);
        return LoadResourceDictionaryFromDocument(doc);
    }

    /// <summary>
    /// Loads a standalone <c>&lt;ResourceDictionary&gt;</c> XAML document from a stream.
    /// </summary>
    public ResourceDictionary LoadResourceDictionaryFromStream(Stream stream)
    {
        var doc = XDocument.Load(stream);
        return LoadResourceDictionaryFromDocument(doc);
    }

    private ResourceDictionary LoadResourceDictionaryFromDocument(XDocument doc)
    {
        var root = doc.Root ?? throw new InvalidOperationException("XAML document has no root element");

        // Collect xmlns prefix → URI mappings and mc:Ignorable prefixes
        CollectNamespaces(root);

        // Verify root element is ResourceDictionary
        var rootType = ResolveType(root.Name);
        if (rootType != typeof(ResourceDictionary))
        {
            throw new InvalidOperationException(
                $"Expected root element 'ResourceDictionary', got '{root.Name.LocalName}'");
        }

        var dict = new ResourceDictionary();

        // Process MergedDictionaries if present (as a property element)
        foreach (var propElement in root.Elements())
        {
            if (IsPropertyElement(propElement.Name, "MergedDictionaries"))
            {
                foreach (var mergedChild in propElement.Elements())
                {
                    var mergedType = ResolveType(mergedChild.Name);
                    if (mergedType != typeof(ResourceDictionary))
                    {
                        throw new InvalidOperationException(
                            $"Expected 'ResourceDictionary' inside MergedDictionaries, got '{mergedChild.Name.LocalName}'");
                    }

                    // Recursively load nested ResourceDictionary
                    var nestedLoader = new XamlLoader();
                    var nestedDoc = new XDocument(new XElement(mergedChild));
                    // Copy namespace declarations from root to nested element
                    foreach (var attr in root.Attributes().Where(a => a.IsNamespaceDeclaration))
                    {
                        if (nestedDoc.Root!.Attribute(attr.Name) == null)
                        {
                            nestedDoc.Root.Add(attr);
                        }
                    }
                    dict.MergedDictionaries.Add(nestedLoader.LoadResourceDictionaryFromDocument(nestedDoc));
                }
                continue;
            }

            // Skip other property elements (e.g., ResourceDictionary.SomeProperty)
            if (IsPropertyElement(propElement.Name))
            {
                continue;
            }

            // Process resource entries
            ProcessResourceDictionaryEntry(propElement, dict);
        }

        // Resolve pending {StaticResource} lookups against the dictionary itself
        ResolveStaticResourcesAgainstDictionary(dict);

        return dict;
    }

    /// <summary>
    /// Processes a single child element within a standalone <c>&lt;ResourceDictionary&gt;</c>.
    /// Similar to <see cref="ProcessResourcesElement"/> but stores into a dictionary parameter
    /// instead of a FrameworkElement's Resources.
    /// </summary>
    private void ProcessResourceDictionaryEntry(XElement child, ResourceDictionary dict)
    {
        var keyAttr = child.Attribute(XName.Get("Key", XamlXNs));
        var childTypeName = child.Name.LocalName;

        var childType = ResolveType(child.Name);
        if (childType == null)
        {
            throw new InvalidOperationException(
                $"Cannot resolve resource type '{childTypeName}' in namespace '{child.Name.NamespaceName}'");
        }

        // Implicit Style keying (Style without x:Key, keyed by TargetType)
        if (keyAttr == null)
        {
            if (childType == typeof(Style) || childType.IsSubclassOf(typeof(Style)))
            {
                if (ControlFactoryRegistry.TryCreate(childType, out var styleInstance))
                {
                    // Create a temporary FrameworkElement so that {StaticResource} references
                    // inside style setters can resolve against the dictionary being built.
                    // We use a DictionaryBackedFrameworkElement wrapper for this purpose.
                    var tempFe = new DictionaryBackedFrameworkElement(dict);
                    ProcessAttributes(child, styleInstance, childType, tempFe);
                    if (child.HasElements)
                    {
                        ProcessChildElements(child, styleInstance, childType, tempFe);
                    }

                    var style = (Style)styleInstance;
                    if (style.TargetType != null)
                    {
                        dict[style.TargetType] = style;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "Style without x:Key must have a TargetType for implicit style keying.");
                    }
                }
                else
                {
                    throw new InvalidOperationException(
                        $"No factory registered for Style type '{childType.FullName}'");
                }
                return;
            }

            // Not a Style and no x:Key — skip
            return;
        }

        var key = keyAttr.Value;

        // Simple value types (like Color) — parse from text content
        if (!child.HasElements)
        {
            var textValue = child.Value.Trim();
            if (!string.IsNullOrEmpty(textValue))
            {
                var converter = TypeConverterRegistry.GetConverterOrEnum(childType);
                if (converter != null && converter.CanConvertFrom(typeof(string)))
                {
                    var value = converter.ConvertFromInvariantString(textValue);
                    dict[key] = value;
                    return;
                }
            }
        }

        // Complex types — instantiate
        if (ControlFactoryRegistry.TryCreate(childType, out var resourceInstance))
        {
            var tempFe = new DictionaryBackedFrameworkElement(dict);
            ProcessAttributes(child, resourceInstance, childType, tempFe);
            if (child.HasElements)
            {
                ProcessChildElements(child, resourceInstance, childType, tempFe);
            }

            dict[key] = resourceInstance;
        }
        else
        {
            throw new InvalidOperationException(
                $"No factory registered for resource type '{childType.FullName}'");
        }
    }

    /// <summary>
    /// Checks if an element name is a property element (e.g., "ResourceDictionary.MergedDictionaries").
    /// </summary>
    private static bool IsPropertyElement(XName name)
    {
        return name.LocalName.Contains('.');
    }

    /// <summary>
    /// Checks if an element name is a specific property element.
    /// </summary>
    private static bool IsPropertyElement(XName name, string propertyName)
    {
        return name.LocalName.Contains('.') && name.LocalName.EndsWith("." + propertyName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves pending <c>{StaticResource}</c> lookups against a standalone dictionary.
    /// Resources added earlier in document order are available for later references.
    /// </summary>
    private void ResolveStaticResourcesAgainstDictionary(ResourceDictionary dict)
    {
        foreach (var pending in _pendingStaticResources)
        {
            object? resource;

            // First try to resolve from the DictionaryBackedFrameworkElement (which delegates to dict)
            if (pending.Context is DictionaryBackedFrameworkElement dfe)
            {
                resource = dfe.TryFindResource(pending.ResourceKey);
            }
            else
            {
                // Fallback: look up directly in the dictionary
                dict.TryGetValue(pending.ResourceKey, out resource);
            }

            if (resource == null)
            {
                throw new InvalidOperationException(
                    $"StaticResource '{pending.ResourceKey}' not found in ResourceDictionary " +
                    $"for property '{pending.PropertyName}' on type '{pending.TargetType.Name}'");
            }

            if (!PropertyAccessorRegistry.TryGetAccessor(pending.TargetType, pending.PropertyName, out var accessor)
                || !accessor.Value.CanWrite)
            {
                throw new InvalidOperationException(
                    $"Property '{pending.PropertyName}' not found or is read-only on type '{pending.TargetType.Name}'");
            }

            var value = resource;
            if (!accessor.Value.PropertyType.IsInstanceOfType(value))
            {
                var converter = TypeConverterRegistry.GetConverterOrEnum(accessor.Value.PropertyType);
                if (converter != null && converter.CanConvertFrom(value.GetType()))
                {
                    value = converter.ConvertFrom(value);
                }
            }

            accessor.Value.Setter!(pending.Target, value);
        }
    }

    /// <summary>
    /// A minimal FrameworkElement that delegates TryFindResource to a ResourceDictionary.
    /// Used during standalone ResourceDictionary loading so that {StaticResource} markup
    /// extensions inside the dictionary can resolve against resources already added to it.
    /// </summary>
    private sealed class DictionaryBackedFrameworkElement : FrameworkElement
    {
        private readonly ResourceDictionary _dict;

        public DictionaryBackedFrameworkElement(ResourceDictionary dict)
        {
            _dict = dict;
        }

        public override object? TryFindResource(object key)
        {
            if (_dict.TryGetValue(key, out var value))
            {
                return value;
            }

            // Fall back to Application.Resources if available
            return base.TryFindResource(key);
        }

        protected override void OnRender(CellBuffer buffer, Rect parentBounds) { }
        public override Rect CalculateBounds(Rect parentBounds) => default;
        public override Size2D GetPreferredSize(Rect parentBounds) => default;
    }

    // ────────────────────────────────────────────────────────────────
    //  Namespace resolution
    // ────────────────────────────────────────────────────────────────

    private void CollectNamespaces(XElement root)
    {
        foreach (var attr in root.Attributes())
        {
            if (!attr.IsNamespaceDeclaration)
            {
                continue;
            }

            var prefix = attr.Name.LocalName;
            if (attr.Name.Namespace == XNamespace.Xmlns)
            {
                // xmlns:prefix="uri"
                _prefixToUri[prefix] = attr.Value;
            }
            else if (attr.Name.LocalName == "xmlns")
            {
                // default xmlns
                _prefixToUri[""] = attr.Value;
            }
        }

        // Check for mc:Ignorable
        var mcIgnorable = root.Attribute(XName.Get("Ignorable", MarkupCompatNs));
        if (mcIgnorable != null)
        {
            foreach (var prefix in mcIgnorable.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                _ignorablePrefixes.Add(prefix);
            }
        }
    }

    /// <summary>
    /// Resolve a CLR type from an XML element name + namespace URI.
    /// </summary>
    private Type? ResolveType(XName name)
    {
        var ns = name.NamespaceName;
        var localName = name.LocalName;

        var clrNamespaces = GetClrNamespaces(ns);
        if (clrNamespaces == null)
        {
            return null;
        }

        foreach (var clrNs in clrNamespaces)
        {
            var fullName = $"{clrNs}.{localName}";
            var type = ResolveTypeByName(fullName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the CLR namespace(s) for a given XML namespace URI.
    /// </summary>
    private List<string>? GetClrNamespaces(string xmlNs)
    {
        if (xmlNs == TerminalNinjaXamlNs)
        {
            return [.. DefaultClrNamespaces];
        }

        if (_xmlNsToClrNamespaces.TryGetValue(xmlNs, out var cached))
        {
            return cached;
        }

        // Try clr-namespace:...;assembly=... syntax
        if (xmlNs.StartsWith("clr-namespace:", StringComparison.Ordinal))
        {
            var clrNs = xmlNs["clr-namespace:".Length..];
            var semiIdx = clrNs.IndexOf(';');
            if (semiIdx >= 0)
            {
                clrNs = clrNs[..semiIdx];
            }

            var list = new List<string> { clrNs };
            _xmlNsToClrNamespaces[xmlNs] = list;
            return list;
        }

        return null;
    }

    /// <summary>
    /// Resolve a Type by its fully-qualified name using the TypeNameRegistry.
    /// AOT-safe: no assembly scanning or reflection.
    /// </summary>
    private static Type? ResolveTypeByName(string fullName)
    {
        return TypeNameRegistry.ResolveType(fullName);
    }

    // ────────────────────────────────────────────────────────────────
    //  Element instantiation
    // ────────────────────────────────────────────────────────────────

    private object CreateElement(XElement element, FrameworkElement? parent)
    {
        var type = ResolveType(element.Name)
            ?? throw new InvalidOperationException(
                $"Cannot resolve XAML type '{element.Name.LocalName}' in namespace '{element.Name.NamespaceName}'");

        // Create instance via factory registry (AOT-safe)
        if (!ControlFactoryRegistry.TryCreate(type, out var instance))
        {
            throw new InvalidOperationException(
                $"No factory registered for type '{type.FullName}'. " +
                "Ensure the type is discovered by the ControlFactoryGenerator.");
        }

        // Set Parent for resource lookup chain
        if (instance is FrameworkElement fe2 && parent != null)
        {
            fe2.Parent = parent;
        }

        var currentFrameworkElement = instance as FrameworkElement;

        // Process attributes (simple properties, attached properties, markup extensions, x:Name)
        ProcessAttributes(element, instance, type, currentFrameworkElement);

        // Process child elements (property elements, content children)
        ProcessChildElements(element, instance, type, currentFrameworkElement);

        return instance;
    }

    // ────────────────────────────────────────────────────────────────
    //  Attribute processing
    // ────────────────────────────────────────────────────────────────

    private void ProcessAttributes(XElement element, object instance, Type type, FrameworkElement? currentFe)
    {
        foreach (var attr in element.Attributes())
        {
            // Skip namespace declarations
            if (attr.IsNamespaceDeclaration)
            {
                continue;
            }

            var attrNs = attr.Name.NamespaceName;
            var attrName = attr.Name.LocalName;

            // Skip ignorable namespace attributes
            if (IsIgnorableNamespace(attrNs))
            {
                continue;
            }

            // x:Name
            if (attrNs == XamlXNs && attrName == "Name")
            {
                _namedElements[attr.Value] = instance;
                // Also set the Name property if it exists
                if (PropertyAccessorRegistry.TryGetAccessor(type, "Name", out var nameAccessor) && nameAccessor.Value.CanWrite)
                {
                    nameAccessor.Value.Setter!(instance, attr.Value);
                }
                continue;
            }

            // x:Key — handled by parent (Window.Resources, etc.)
            if (attrNs == XamlXNs && attrName == "Key")
            {
                continue;
            }

            // Skip mc:Ignorable itself
            if (attrNs == MarkupCompatNs)
            {
                continue;
            }

            // Attached property: Type.PropertyName (e.g., StackPanel.SizeMode)
            if (string.IsNullOrEmpty(attrNs) && attrName.Contains('.'))
            {
                SetAttachedProperty(instance, attrName, attr.Value, element);
                continue;
            }

            // Skip attributes from non-empty namespaces we don't handle
            if (!string.IsNullOrEmpty(attrNs) && attrNs != TerminalNinjaXamlNs)
            {
                continue;
            }

            // Regular property
            SetPropertyFromString(instance, type, attrName, attr.Value, currentFe);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Child element processing
    // ────────────────────────────────────────────────────────────────

    private void ProcessChildElements(XElement element, object instance, Type type, FrameworkElement? currentFe)
    {
        // Determine the content property for this type
        var contentPropertyName = GetContentPropertyName(type);

        // Check if the content property holds an InlineCollection (mixed content support).
        // When it does, we iterate element.Nodes() to pick up both XText and XElement nodes,
        // converting bare text nodes into Run objects automatically.
        var isInlineContent = false;
        if (contentPropertyName != null &&
            PropertyAccessorRegistry.TryGetAccessor(type, contentPropertyName, out var contentAccessor))
        {
            var propValue = contentAccessor.Value.Getter(instance);
            isInlineContent = propValue is InlineCollection;
        }

        foreach (var node in element.Nodes())
        {
            // Handle bare text nodes — convert to Run and add to InlineCollection
            if (node is XText textNode)
            {
                var text = textNode.Value;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                if (isInlineContent && contentPropertyName != null)
                {
                    var run = new Run { Text = text };
                    if (currentFe != null)
                    {
                        run.Parent = currentFe;
                    }

                    AddToContentProperty(instance, type, contentPropertyName, run);
                }
                else if (contentPropertyName != null)
                {
                    // Set bare text as the content property value (e.g., <ListBoxItem>Hello</ListBoxItem>
                    // sets Content = "Hello"). Only applies to writable non-collection content properties.
                    if (PropertyAccessorRegistry.TryGetAccessor(type, contentPropertyName, out var textAccessor)
                        && textAccessor.Value.CanWrite)
                    {
                        var propValue = textAccessor.Value.Getter(instance);
                        // Don't overwrite collection properties — only set scalar content
                        if (propValue == null || !TryAddToCollection(propValue, text))
                        {
                            textAccessor.Value.Setter!(instance, text);
                        }
                    }
                }
                continue;
            }

            if (node is not XElement child)
            {
                continue;
            }

            var childNs = child.Name.NamespaceName;
            var childLocalName = child.Name.LocalName;

            // Skip ignorable namespace elements
            if (IsIgnorableNamespace(childNs))
            {
                continue;
            }

            // Property element syntax: <TypeName.PropertyName> ... </TypeName.PropertyName>
            if (childLocalName.Contains('.'))
            {
                var parts = childLocalName.Split('.', 2);
                var ownerTypeName = parts[0];
                var propertyName = parts[1];

                // Check if this is an attached property element or a regular property element.
                // Match against the instance type AND all its base types, because in x:Class
                // scenarios the XAML declares the base type name (e.g., <UserControl.Resources>)
                // but the actual instance type is the derived class (e.g., ActivityLogControl).
                if (IsPropertyElementOwner(type, ownerTypeName))
                {
                    // Regular property element: <Window.Resources>, <Rectangle.Child>, etc.
                    ProcessPropertyElement(instance, type, propertyName, child, currentFe);
                }
                else
                {
                    // Attached property element (rare in our XAML, but support it)
                    var attachedOwnerType = ResolveType(XName.Get(ownerTypeName, childNs));
                    if (attachedOwnerType != null)
                    {
                        // Get the single child value
                        var childValue = child.Elements().FirstOrDefault();
                        if (childValue != null)
                        {
                            var value = CreateElement(childValue, currentFe);
                            SetAttachedPropertyValue(instance, attachedOwnerType, propertyName, value);
                        }
                    }
                }
                continue;
            }

            // Content child element — add to the content property
            if (contentPropertyName != null)
            {
                var childInstance = CreateElement(child, currentFe);
                AddToContentProperty(instance, type, contentPropertyName, childInstance);
            }
        }
    }

    /// <summary>
    /// Checks whether <paramref name="ownerTypeName"/> matches the instance type or any
    /// of its base types. This is needed for property element syntax: in x:Class scenarios,
    /// the XAML declares the base type name (e.g., <c>&lt;UserControl.Resources&gt;</c>) but
    /// the actual instance type is the derived class (e.g., <c>ActivityLogControl</c>).
    /// </summary>
    private static bool IsPropertyElementOwner(Type instanceType, string ownerTypeName)
    {
        var current = instanceType;
        while (current != null && current != typeof(object))
        {
            if (current.Name == ownerTypeName)
            {
                return true;
            }
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Processes a property element like &lt;Window.Resources&gt; or &lt;Rectangle.Child&gt;.
    /// </summary>
    private void ProcessPropertyElement(object instance, Type type, string propertyName, XElement element, FrameworkElement? currentFe)
    {
        // Special case: Resources property on FrameworkElement
        if (propertyName == "Resources" && instance is FrameworkElement fe)
        {
            ProcessResourcesElement(fe, element);
            return;
        }

        // For other property elements, check if the property is a collection or a single value
        if (!PropertyAccessorRegistry.TryGetAccessor(type, propertyName, out var accessor))
        {
            throw new InvalidOperationException(
                $"Property '{propertyName}' not found on type '{type.Name}'");
        }

        var propType = accessor.Value.PropertyType;

        // Single child — set the property directly, or add to a read-only collection
        // property (e.g. <NodeGraph.GraphEdges> with one edge).
        var childElements = element.Elements().ToList();
        if (childElements.Count == 1)
        {
            var childInstance = CreateElement(childElements[0], currentFe);
            if (accessor.Value.CanWrite)
            {
                accessor.Value.Setter!(instance, childInstance);
            }
            else
            {
                AddToCollection(accessor.Value.Getter(instance), childInstance);
            }
        }
        else if (childElements.Count > 1)
        {
            // Multiple children — property should be a collection
            foreach (var child in childElements)
            {
                var childInstance = CreateElement(child, currentFe);
                AddToCollection(accessor.Value.Getter(instance), childInstance);
            }
        }
    }

    /// <summary>
    /// Processes &lt;Window.Resources&gt; or &lt;FrameworkElement.Resources&gt; element.
    /// </summary>
    private void ProcessResourcesElement(FrameworkElement fe, XElement resourcesElement)
    {
        foreach (var child in resourcesElement.Elements())
        {
            // Get x:Key
            var keyAttr = child.Attribute(XName.Get("Key", XamlXNs));
            var childNs = child.Name.NamespaceName;
            var childTypeName = child.Name.LocalName;

            // Resolve the type
            var childType = ResolveType(child.Name);
            if (childType == null)
            {
                throw new InvalidOperationException(
                    $"Cannot resolve resource type '{childTypeName}' in namespace '{childNs}'");
            }

            // If no x:Key, allow implicit Style keying by TargetType
            if (keyAttr == null)
            {
                if (childType == typeof(Style) || childType.IsSubclassOf(typeof(Style)))
                {
                    // Create the Style, process attributes/children, then derive key from TargetType
                    if (ControlFactoryRegistry.TryCreate(childType, out var styleInstance))
                    {
                        ProcessAttributes(child, styleInstance, childType, fe);
                        if (child.HasElements)
                        {
                            ProcessChildElements(child, styleInstance, childType, fe);
                        }

                        var style = (Style)styleInstance;
                        if (style.TargetType != null)
                        {
                            fe.Resources[style.TargetType] = style;
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                "Style without x:Key must have a TargetType for implicit style keying.");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"No factory registered for Style type '{childType.FullName}'");
                    }
                }
                // Not a Style and no x:Key — skip
                continue;
            }

            var key = keyAttr.Value;

            // Simple value types (like Color) — parse from text content
            if (child.HasElements == false)
            {
                var textValue = child.Value.Trim();
                if (!string.IsNullOrEmpty(textValue))
                {
                    var converter = TypeConverterRegistry.GetConverterOrEnum(childType);
                    if (converter != null && converter.CanConvertFrom(typeof(string)))
                    {
                        var value = converter.ConvertFromInvariantString(textValue);
                        fe.Resources[key] = value;
                        continue;
                    }
                }
            }

            // Complex types — instantiate
            if (ControlFactoryRegistry.TryCreate(childType, out var resourceInstance))
            {
                // Set properties from attributes (skip x:Key)
                ProcessAttributes(child, resourceInstance, childType, fe);

                // Process child elements (e.g., DataTemplate with TemplateContent via [ContentProperty])
                if (child.HasElements)
                {
                    ProcessChildElements(child, resourceInstance, childType, fe);
                }

                fe.Resources[key] = resourceInstance;
            }
            else
            {
                throw new InvalidOperationException(
                    $"No factory registered for resource type '{childType.FullName}'");
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Property setting
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets a property on an object from a XAML attribute string value.
    /// Handles {Binding} and {StaticResource} markup extensions.
    /// </summary>
    private void SetPropertyFromString(object instance, Type type, string propertyName, string value, FrameworkElement? currentFe)
    {
        var trimmedValue = value.Trim();

        // {Binding ...}
        if (trimmedValue.StartsWith("{Binding", StringComparison.Ordinal))
        {
            ParseBindingExtension(instance, propertyName, trimmedValue, currentFe);
            return;
        }

        // {StaticResource ...}
        if (trimmedValue.StartsWith("{StaticResource", StringComparison.Ordinal))
        {
            ParseStaticResourceExtension(instance, type, propertyName, trimmedValue, currentFe);
            return;
        }

        // Regular property assignment via registry
        if (!PropertyAccessorRegistry.TryGetAccessor(type, propertyName, out var accessor))
        {
            throw new InvalidOperationException(
                $"Property '{propertyName}' not found on type '{type.Name}'");
        }

        if (!accessor.Value.CanWrite)
        {
            throw new InvalidOperationException(
                $"Property '{propertyName}' on type '{type.Name}' is read-only");
        }

        var propType = accessor.Value.PropertyType;

        // System.Type properties (e.g., DataTemplate.DataType, Style.TargetType)
        // require AOT-safe resolution via TypeNameRegistry — not ConvertValue.
        if (propType == typeof(Type))
        {
            var resolved = ResolveTypeFromXamlString(trimmedValue)
                ?? throw new InvalidOperationException(
                    $"Cannot resolve type '{trimmedValue}' for property '{propertyName}' on '{type.Name}'. " +
                    $"Ensure the type is registered in TypeNameRegistry.");
            accessor.Value.Setter!(instance, resolved);
            return;
        }

        var converted = ConvertValue(trimmedValue, propType);
        accessor.Value.Setter!(instance, converted);
    }

    /// <summary>
    /// Converts a string value to the target property type using TypeDescriptor converters.
    /// </summary>
    private static object? ConvertValue(string value, Type targetType)
    {
        if (targetType == typeof(string))
        {
            return value;
        }

        // When the target type is object (e.g., Setter.Value), store as string.
        // The actual conversion happens later when the style is applied
        // and the concrete target property type is known.
        if (targetType == typeof(object))
        {
            return value;
        }

        // Handle enums directly (faster than TypeDescriptor for enums)
        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, value, ignoreCase: true);
        }

        // Handle common primitive types
        if (targetType == typeof(int))
        {
            return int.Parse(value);
        }

        if (targetType == typeof(double))
        {
            return double.Parse(value);
        }

        if (targetType == typeof(bool))
        {
            return bool.Parse(value);
        }

        if (targetType == typeof(char))
        {
            if (value.Length != 1)
                throw new InvalidOperationException(
                    $"Cannot convert '{value}' to Char: string must be exactly one character");
            return value[0];
        }

        // Use TypeConverterRegistry (AOT-safe replacement for TypeDescriptor.GetConverter)
        var converter = TypeConverterRegistry.GetConverterOrEnum(targetType);
        if (converter != null && converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromInvariantString(value);
        }

        throw new InvalidOperationException(
            $"Cannot convert '{value}' to type '{targetType.Name}': no suitable TypeConverter found");
    }

    // ────────────────────────────────────────────────────────────────
    //  Attached properties
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses and sets an attached property like StackPanel.SizeMode="Fixed".
    /// Uses the AttachedPropertySetterRegistry for AOT-safe method dispatch.
    /// </summary>
    private void SetAttachedProperty(object instance, string dotNotation, string value, XElement element)
    {
        var parts = dotNotation.Split('.', 2);
        var ownerTypeName = parts[0];
        var propertyName = parts[1];

        // Resolve the owner type from the element's namespace context
        var ownerType = ResolveAttachedPropertyOwnerType(ownerTypeName, element);
        if (ownerType == null)
        {
            throw new InvalidOperationException(
                $"Cannot resolve attached property owner type '{ownerTypeName}' for '{dotNotation}'");
        }

        var trimmedValue = value.Trim();

        // Markup extensions on attached properties — same {Binding} / {StaticResource} syntax
        // as regular properties, just routed against an attached DP (registered on the owner
        // type, not the target). Without this branch the raw text "{Binding Foo}" hits
        // ConvertValue and throws a FormatException trying to parse it as the target type.
        if (trimmedValue.StartsWith("{Binding", StringComparison.Ordinal))
        {
            ParseBindingExtension(instance, propertyName, trimmedValue, FindAncestorFe(instance), ownerType);
            return;
        }

        if (trimmedValue.StartsWith("{StaticResource", StringComparison.Ordinal))
        {
            // Resolve the resource immediately and feed it to the setter — static resources
            // are a one-shot lookup, not an ongoing binding, so the existing setter path is
            // fine once we have the resolved value.
            ParseStaticResourceForAttached(instance, ownerType, propertyName, trimmedValue);
            return;
        }

        // Look up the setter in the AOT-safe registry
        if (!AttachedPropertySetterRegistry.TryGetSetter(ownerType, propertyName, out var setter))
        {
            throw new InvalidOperationException(
                $"Attached property setter '{ownerType.Name}.Set{propertyName}' not found in AttachedPropertySetterRegistry");
        }

        // Convert the string value to the parameter type
        var converted = ConvertValue(value, setter.ParameterType);
        setter.Setter(instance, converted);
    }

    /// <summary>
    /// Walks up from <paramref name="instance"/> via <see cref="FrameworkElement.Parent"/> to
    /// find the nearest <see cref="FrameworkElement"/>, used as the binding context (for
    /// resolving converters / static resources at activation time).
    /// </summary>
    private static FrameworkElement? FindAncestorFe(object instance)
    {
        if (instance is FrameworkElement fe) return fe;
        return null;
    }

    /// <summary>
    /// Handles <c>{StaticResource Key}</c> when used as an attached-property value.
    /// </summary>
    private void ParseStaticResourceForAttached(object instance, Type ownerType, string propertyName, string markup)
    {
        var inner = markup[1..^1].Trim();
        if (inner.StartsWith("StaticResource", StringComparison.Ordinal))
        {
            inner = inner["StaticResource".Length..].Trim();
        }

        if (!AttachedPropertySetterRegistry.TryGetSetter(ownerType, propertyName, out var setter))
        {
            throw new InvalidOperationException(
                $"Attached property setter '{ownerType.Name}.Set{propertyName}' not found in AttachedPropertySetterRegistry");
        }

        _pendingStaticResources.Add(new PendingStaticResource(
            instance, ownerType, propertyName, inner, instance as FrameworkElement));
        _ = setter; // resolved at activation time
    }

    /// <summary>
    /// Sets an attached property value using a resolved owner type and value object.
    /// Uses the AttachedPropertySetterRegistry for AOT-safe method dispatch.
    /// </summary>
    private static void SetAttachedPropertyValue(object instance, Type ownerType, string propertyName, object value)
    {
        if (AttachedPropertySetterRegistry.TryGetSetter(ownerType, propertyName, out var setter))
        {
            setter.Setter(instance, value);
        }
    }

    /// <summary>
    /// Resolves the owner type for an attached property by looking up type name against 
    /// all known CLR namespaces.
    /// </summary>
    private Type? ResolveAttachedPropertyOwnerType(string typeName, XElement context)
    {
        // Try the default XAML namespace first
        foreach (var clrNs in DefaultClrNamespaces)
        {
            var type = ResolveTypeByName($"{clrNs}.{typeName}");
            if (type != null)
            {
                return type;
            }
        }

        // Try CLR namespace mappings from xmlns declarations
        foreach (var nsMapping in _xmlNsToClrNamespaces.Values)
        {
            foreach (var clrNs in nsMapping)
            {
                var type = ResolveTypeByName($"{clrNs}.{typeName}");
                if (type != null)
                {
                    return type;
                }
            }
        }

        return null;
    }

    // ────────────────────────────────────────────────────────────────
    //  Content properties
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the content property name for a type by walking the type hierarchy.
    /// Delegates to <see cref="ContentPropertyRegistry"/> which is populated by generated code.
    /// </summary>
    private static string? GetContentPropertyName(Type type)
    {
        return ContentPropertyRegistry.GetContentPropertyName(type);
    }

    /// <summary>
    /// Adds a child to the appropriate content property.
    /// </summary>
    private static void AddToContentProperty(object parent, Type parentType, string contentPropertyName, object child)
    {
        if (!PropertyAccessorRegistry.TryGetAccessor(parentType, contentPropertyName, out var accessor))
        {
            throw new InvalidOperationException(
                $"Content property '{contentPropertyName}' not found on type '{parentType.Name}'");
        }

        var propValue = accessor.Value.Getter(parent);

        // If the property value is a collection, add to it
        if (propValue != null && TryAddToCollection(propValue, child))
        {
            return;
        }

        // Otherwise set the property directly (single-child content, e.g., Rectangle.Child, Window.Content)
        if (accessor.Value.CanWrite)
        {
            accessor.Value.Setter!(parent, child);
            return;
        }

        throw new InvalidOperationException(
            $"Cannot add child of type '{child.GetType().Name}' to '{parentType.Name}.{contentPropertyName}'");
    }

    /// <summary>
    /// Attempts to add an item to a collection-like object.
    /// </summary>
    private static bool TryAddToCollection(object collection, object item)
    {
        return AddToCollection(collection, item);
    }

    /// <summary>
    /// Adds an item to a known collection type.
    /// Checks specific generic interfaces first for type safety, then falls back
    /// to <see cref="System.Collections.IList"/> for extensibility with custom collections.
    /// </summary>
    private static bool AddToCollection(object? collection, object item)
    {
        if (collection == null)
        {
            return false;
        }

        // ObservableControlCollection (Panel.Children)
        if (collection is ObservableControlCollection occ && item is UIElement uiElement)
        {
            occ.Add(uiElement);
            return true;
        }

        // IList<object> (ItemsControl.Items)
        if (collection is IList<object> objectList)
        {
            objectList.Add(item);
            return true;
        }

        // IList<Setter> (Style.Setters)
        if (collection is IList<Setter> setterList && item is Setter setter)
        {
            setterList.Add(setter);
            return true;
        }

        // IList<RowDefinition> (Grid.RowDefinitions)
        if (collection is IList<RowDefinition> rowList && item is RowDefinition row)
        {
            rowList.Add(row);
            return true;
        }

        // IList<ColumnDefinition> (Grid.ColumnDefinitions)
        if (collection is IList<ColumnDefinition> colList && item is ColumnDefinition col)
        {
            colList.Add(col);
            return true;
        }

        // Fallback: non-generic IList (supports custom collection types from consuming projects)
        if (collection is System.Collections.IList nonGenericList)
        {
            nonGenericList.Add(item);
            return true;
        }

        return false;
    }

    // ────────────────────────────────────────────────────────────────
    //  Markup extensions
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a {Binding ...} markup extension and stores it for later activation.
    /// Syntax: {Binding Path=PropertyName} or {Binding PropertyName}
    /// Supports: Path, Mode, Converter={StaticResource Key}, ConverterParameter=Value,
    ///           RelativeSource={RelativeSource Self|FindAncestor, AncestorType=TypeName, AncestorLevel=N}
    /// </summary>
    private void ParseBindingExtension(object target, string targetPropertyName, string markup, FrameworkElement? context, Type? attachedOwnerType = null)
    {
        // Strip braces: "{Binding Path=Text}" → "Binding Path=Text"
        var inner = markup[1..^1].Trim();

        // Remove "Binding" prefix
        if (inner.StartsWith("Binding", StringComparison.Ordinal))
        {
            inner = inner["Binding".Length..].Trim();
        }

        string? path = null;
        var mode = BindingMode.Default;
        IValueConverter? converter = null;
        object? converterParameter = null;
        RelativeSource? relativeSource = null;

        if (string.IsNullOrEmpty(inner))
        {
            throw new InvalidOperationException("Binding markup extension requires at least a Path");
        }

        // Parse key=value pairs, or a single positional value (the path)
        var parts = SplitMarkupExtensionArgs(inner);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx < 0)
            {
                // Positional argument: the path
                path = trimmed;
                continue;
            }

            var key = trimmed[..eqIdx].Trim();
            var val = trimmed[(eqIdx + 1)..].Trim();

            switch (key)
            {
                case "Path":
                    path = val;
                    break;
                case "Mode":
                    mode = Enum.Parse<BindingMode>(val, ignoreCase: true);
                    break;
                case "Converter":
                    // {StaticResource ConverterKey}
                    converter = ResolveInlineStaticResource<IValueConverter>(val, context);
                    break;
                case "ConverterParameter":
                    converterParameter = val;
                    break;
                case "RelativeSource":
                    relativeSource = ParseRelativeSourceValue(val);
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Binding markup extension requires a Path");
        }

        _pendingBindings.Add(new PendingBinding(target, targetPropertyName, path, mode, converter, converterParameter)
        {
            Context = context,
            RelativeSource = relativeSource,
            AttachedOwnerType = attachedOwnerType,
        });
    }

    /// <summary>
    /// Parses a <c>{RelativeSource ...}</c> nested markup extension value.
    /// Supports:
    ///   <c>{RelativeSource Self}</c>
    ///   <c>{RelativeSource TemplatedParent}</c>
    ///   <c>{RelativeSource FindAncestor, AncestorType=TypeName}</c>
    ///   <c>{RelativeSource FindAncestor, AncestorType=TypeName, AncestorLevel=2}</c>
    /// </summary>
    private RelativeSource ParseRelativeSourceValue(string value)
    {
        // Value could be "{RelativeSource Self}" or just the inner part after stripping
        var inner = value;
        if (inner.StartsWith("{", StringComparison.Ordinal) && inner.EndsWith("}", StringComparison.Ordinal))
        {
            inner = inner[1..^1].Trim();
        }

        // Remove "RelativeSource" prefix
        if (inner.StartsWith("RelativeSource", StringComparison.Ordinal))
        {
            inner = inner["RelativeSource".Length..].Trim();
        }

        if (string.IsNullOrEmpty(inner))
        {
            throw new InvalidOperationException("RelativeSource markup extension requires at least a Mode");
        }

        // Parse inner arguments (comma-separated, but first positional = mode)
        var parts = SplitMarkupExtensionArgs(inner);
        RelativeSourceMode mode = default;
        Type? ancestorType = null;
        var ancestorLevel = 1;
        var modeSet = false;

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx < 0)
            {
                // Positional: the mode
                mode = Enum.Parse<RelativeSourceMode>(trimmed, ignoreCase: true);
                modeSet = true;
                continue;
            }

            var key = trimmed[..eqIdx].Trim();
            var val = trimmed[(eqIdx + 1)..].Trim();

            switch (key)
            {
                case "Mode":
                    mode = Enum.Parse<RelativeSourceMode>(val, ignoreCase: true);
                    modeSet = true;
                    break;
                case "AncestorType":
                    ancestorType = ResolveAncestorType(val);
                    if (ancestorType == null)
                    {
                        throw new InvalidOperationException(
                            $"Cannot resolve AncestorType '{val}'. Ensure the type is registered in TypeNameRegistry.");
                    }

                    break;
                case "AncestorLevel":
                    ancestorLevel = int.Parse(val);
                    break;
            }
        }

        // If AncestorType was specified without an explicit Mode, default to FindAncestor (WPF behavior)
        if (!modeSet)
        {
            if (ancestorType != null)
            {
                mode = RelativeSourceMode.FindAncestor;
            }
            else
            {
                throw new InvalidOperationException("RelativeSource markup extension requires a Mode");
            }
        }

        var relativeSource = new RelativeSource(mode)
        {
            AncestorType = ancestorType,
            AncestorLevel = ancestorLevel
        };

        return relativeSource;
    }

    /// <summary>
    /// Resolves an AncestorType value from a RelativeSource markup extension.
    /// Handles three formats:
    /// <list type="bullet">
    ///   <item><c>Window</c> — simple name, searched across default CLR namespaces</item>
    ///   <item><c>sample:ActivityLogControl</c> — prefixed name, resolved via xmlns mapping</item>
    ///   <item><c>{system:Type sample:ActivityLogControl}</c> — {x:Type} / {system:Type} markup extension wrapper</item>
    /// </list>
    /// </summary>
    private Type? ResolveAncestorType(string value)
    {
        var typeName = value;

        // Unwrap {x:Type ...} or {system:Type ...} markup extension if present
        if (typeName.StartsWith("{", StringComparison.Ordinal) && typeName.EndsWith("}", StringComparison.Ordinal))
        {
            var inner = typeName[1..^1].Trim();

            // Strip the "x:Type" or "system:Type" prefix
            var spaceIdx = inner.IndexOf(' ');
            if (spaceIdx > 0)
            {
                typeName = inner[(spaceIdx + 1)..].Trim();
            }
            else
            {
                typeName = inner;
            }
        }

        // Try instance method first (handles prefixed names like "sample:ActivityLogControl"
        // and fully-qualified names like "Sample.ActivityLogControl")
        return ResolveTypeFromXamlString(typeName) ?? ResolveTypeBySimpleName(typeName);
    }

    /// <summary>
    /// Resolves a type by its simple (unqualified) name by searching all default CLR namespaces.
    /// Used for <c>AncestorType=Window</c> syntax in markup extensions.
    /// </summary>
    private static Type? ResolveTypeBySimpleName(string simpleName)
    {
        foreach (var ns in DefaultClrNamespaces)
        {
            var fullName = $"{ns}.{simpleName}";
            var type = ResolveTypeByName(fullName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a <see cref="Type"/> from a XAML attribute string value.
    /// AOT-safe: uses only <see cref="TypeNameRegistry"/> lookups.
    ///
    /// Supported formats:
    /// <list type="bullet">
    ///   <item><c>Button</c> — simple name, searched across default CLR namespaces and custom xmlns mappings</item>
    ///   <item><c>sample:LogEntry</c> — prefixed name, resolved via xmlns prefix → CLR namespace mapping</item>
    ///   <item><c>Sample.LogEntry</c> — fully-qualified CLR name, looked up directly in TypeNameRegistry</item>
    /// </list>
    /// </summary>
    private Type? ResolveTypeFromXamlString(string value)
    {
        // 1. Prefixed name (e.g., "sample:LogEntry")
        var colonIndex = value.IndexOf(':');
        if (colonIndex > 0)
        {
            var prefix = value[..colonIndex];
            var typeName = value[(colonIndex + 1)..];

            // Look up the XML namespace URI for this prefix
            if (_prefixToUri.TryGetValue(prefix, out var xmlNsUri))
            {
                // Resolve the CLR namespace(s) for that URI
                var clrNamespaces = GetClrNamespaces(xmlNsUri);
                if (clrNamespaces != null)
                {
                    foreach (var clrNs in clrNamespaces)
                    {
                        var resolved = ResolveTypeByName($"{clrNs}.{typeName}");
                        if (resolved != null)
                        {
                            return resolved;
                        }
                    }
                }
            }

            return null;
        }

        // 2. Fully-qualified name (e.g., "Sample.LogEntry") — try direct lookup first
        if (value.Contains('.'))
        {
            var direct = ResolveTypeByName(value);
            if (direct != null)
            {
                return direct;
            }
        }

        // 3. Simple name (e.g., "Button") — search default CLR namespaces
        var simple = ResolveTypeBySimpleName(value);
        if (simple != null)
        {
            return simple;
        }

        // 4. Also search custom xmlns CLR namespace mappings
        foreach (var nsMapping in _xmlNsToClrNamespaces.Values)
        {
            foreach (var clrNs in nsMapping)
            {
                var resolved = ResolveTypeByName($"{clrNs}.{value}");
                if (resolved != null)
                {
                    return resolved;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Parses a {StaticResource Key} markup extension and defers the resource lookup.
    /// </summary>
    private void ParseStaticResourceExtension(object instance, Type type, string propertyName, string markup, FrameworkElement? context)
    {
        // Strip braces: "{StaticResource HeaderBackground}" → "StaticResource HeaderBackground"
        var inner = markup[1..^1].Trim();

        // Remove "StaticResource" prefix
        if (inner.StartsWith("StaticResource", StringComparison.Ordinal))
        {
            inner = inner["StaticResource".Length..].Trim();
        }

        if (string.IsNullOrEmpty(inner))
        {
            throw new InvalidOperationException("StaticResource markup extension requires a resource key");
        }

        var resourceKey = inner;

        // Store for deferred resolution (after all elements + resources are constructed)
        _pendingStaticResources.Add(new PendingStaticResource(instance, type, propertyName, resourceKey, context));
    }

    /// <summary>
    /// Pending static resource lookups, resolved after the full tree is built.
    /// </summary>
    private readonly List<PendingStaticResource> _pendingStaticResources = [];

    /// <summary>
    /// Resolves an inline {StaticResource Key} within a binding attribute (e.g., Converter={StaticResource ...}).
    /// These must be resolved immediately since the binding needs the converter reference.
    /// Returns null if the resource is not yet available (will be resolved later).
    /// </summary>
    private T? ResolveInlineStaticResource<T>(string markup, FrameworkElement? context) where T : class
    {
        // The value might be "{StaticResource Key}" or just "Key" if it's already parsed
        var key = markup;
        if (key.StartsWith("{StaticResource", StringComparison.Ordinal))
        {
            key = key[1..^1].Trim();
            key = key["StaticResource".Length..].Trim();
        }

        // Defer resolution — store a placeholder and resolve later
        // For Converter in Binding, we need to store the key and resolve after tree is built
        // We'll store the key in the pending binding and resolve it during ResolveStaticResources
        _deferredConverterKeys[_pendingBindings.Count] = key;
        return null;
    }

    /// <summary>
    /// Maps pending binding index → converter resource key for deferred resolution.
    /// </summary>
    private readonly Dictionary<int, string> _deferredConverterKeys = new();

    /// <summary>
    /// Resolves all pending {StaticResource} lookups now that the full element tree is constructed.
    /// </summary>
    private void ResolveStaticResources(FrameworkElement? root)
    {
        // First resolve deferred converter keys on bindings
        foreach (var kvp in _deferredConverterKeys)
        {
            var bindingIdx = kvp.Key;
            var resourceKey = kvp.Value;

            if (bindingIdx < _pendingBindings.Count)
            {
                var binding = _pendingBindings[bindingIdx];
                var resolveFrom = binding.Context ?? root;
                if (resolveFrom != null)
                {
                    var resource = resolveFrom.TryFindResource(resourceKey);
                    if (resource is IValueConverter conv)
                    {
                        _pendingBindings[bindingIdx] = binding with { Converter = conv };
                    }
                }
            }
        }

        // Then resolve all property static resources
        foreach (var pending in _pendingStaticResources)
        {
            var resolveFrom = pending.Target as FrameworkElement ?? pending.Context ?? root;
            if (resolveFrom == null)
            {
                throw new InvalidOperationException(
                    $"StaticResource '{pending.ResourceKey}' cannot be resolved: no FrameworkElement context");
            }

            var resource = resolveFrom.TryFindResource(pending.ResourceKey);
            if (resource == null)
            {
                throw new InvalidOperationException(
                    $"StaticResource '{pending.ResourceKey}' not found for property '{pending.PropertyName}'");
            }

            // Set the property value
            if (!PropertyAccessorRegistry.TryGetAccessor(pending.TargetType, pending.PropertyName, out var accessor)
                || !accessor.Value.CanWrite)
            {
                throw new InvalidOperationException(
                    $"Property '{pending.PropertyName}' not found or is read-only on type '{pending.TargetType.Name}'");
            }

            var value = resource;
            if (!accessor.Value.PropertyType.IsInstanceOfType(value))
            {
                var converter = TypeConverterRegistry.GetConverterOrEnum(accessor.Value.PropertyType);
                if (converter != null && converter.CanConvertFrom(value.GetType()))
                {
                    value = converter.ConvertFrom(value);
                }
            }

            accessor.Value.Setter!(pending.Target, value);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Markup extension argument parsing
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Splits markup extension arguments, respecting nested braces.
    /// E.g., "Path=Text, Converter={StaticResource Conv}, ConverterParameter=HH:mm:ss"
    /// → ["Path=Text", "Converter={StaticResource Conv}", "ConverterParameter=HH:mm:ss"]
    /// </summary>
    private static List<string> SplitMarkupExtensionArgs(string input)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            switch (c)
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    break;
                case ',' when depth == 0:
                    result.Add(input[start..i]);
                    start = i + 1;
                    break;
            }
        }

        if (start < input.Length)
        {
            result.Add(input[start..]);
        }

        return result;
    }

    // ────────────────────────────────────────────────────────────────
    //  Ignorable namespace check
    // ────────────────────────────────────────────────────────────────

    private bool IsIgnorableNamespace(string namespaceUri)
    {
        if (string.IsNullOrEmpty(namespaceUri))
        {
            return false;
        }

        // Design-time and markup-compat are always ignorable
        if (namespaceUri is DesignTimeNs or MarkupCompatNs)
        {
            return true;
        }

        // Check mc:Ignorable prefixes
        foreach (var prefix in _ignorablePrefixes)
        {
            if (_prefixToUri.TryGetValue(prefix, out var uri) && uri == namespaceUri)
            {
                return true;
            }
        }

        return false;
    }

    // ────────────────────────────────────────────────────────────────
    //  FindByName support
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the named elements dictionary after loading. Used by FindByName.
    /// </summary>
    internal IReadOnlyDictionary<string, object> NamedElements => _namedElements;

    // ────────────────────────────────────────────────────────────────
    //  Binding activation
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts all <see cref="PendingBinding"/> records collected during parsing into
    /// <see cref="BindingExpression"/>s attached to the target's <see cref="DependencyProperty"/>
    /// via <see cref="BindingOperations.SetBinding"/>.
    /// </summary>
    private void ActivateBindings()
    {
        foreach (var pb in _pendingBindings)
        {
            if (pb.Target is not DependencyObject depObj)
            {
                // Non-DependencyObject targets cannot participate in the DP expression system.
                System.Diagnostics.Debug.WriteLine(
                    $"Binding skipped: target '{pb.Target.GetType().Name}' is not a DependencyObject " +
                    $"(property '{pb.TargetPropertyName}').");
                continue;
            }

            // Look up the DependencyProperty by name. For attached-property bindings the
            // DP is registered against the owner type (e.g. StackPanel), not against the
            // target's own type — so the binding for "StackPanel.FixedSize" needs
            // Find(StackPanel, FixedSize), not Find(Border, FixedSize).
            var lookupType = pb.AttachedOwnerType ?? depObj.GetType();
            var dp = DependencyProperty.Find(lookupType, pb.TargetPropertyName);

            if (dp == null)
            {
                // No DP found — the property might be a plain CLR property.
                // For now, skip it with a diagnostic. In the future, we could support
                // CLR-only bindings via a wrapper expression, but WPF itself requires DPs.
                System.Diagnostics.Debug.WriteLine(
                    $"Binding skipped: no DependencyProperty '{pb.TargetPropertyName}' found on " +
                    $"'{lookupType.Name}'. Only DP-backed properties support data binding.");
                continue;
            }

            // Build a Binding description from the parsed metadata.
            // Use fully-qualified name to avoid collision with the Binding namespace.
            var binding = new System.Windows.Markup.Binding(pb.Path)
            {
                Mode = pb.Mode,
                Converter = pb.Converter,
                ConverterParameter = pb.ConverterParameter,
                RelativeSource = pb.RelativeSource
            };

            // Attach the binding expression to the target DP.
            // This calls Expression.Attach() → OnAttach() → ResolveAndActivate(),
            // which resolves the source immediately if available (e.g., RelativeSource),
            // or defers if the source isn't available yet (e.g., DataContext not yet set).
            BindingOperations.SetBinding(depObj, dp, binding);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Internal record types
    // ────────────────────────────────────────────────────────────────

    private sealed record PendingBinding(
        object Target,
        string TargetPropertyName,
        string Path,
        BindingMode Mode,
        IValueConverter? Converter,
        object? ConverterParameter)
    {
        /// <summary>
        /// The FrameworkElement context for resolving resources.
        /// </summary>
        public FrameworkElement? Context { get; init; }

        /// <summary>
        /// Optional RelativeSource for resolving the binding source relative to the target element.
        /// When set, the binding source is determined by the RelativeSource instead of DataContext.
        /// </summary>
        public RelativeSource? RelativeSource { get; init; }

        /// <summary>
        /// Owner type for an attached-property binding (e.g. <c>StackPanel</c> for
        /// <c>StackPanel.FixedSize="{Binding …}"</c>). When non-null, <see cref="ActivateBindings"/>
        /// resolves the <see cref="DependencyProperty"/> via <see cref="DependencyProperty.Find"/>
        /// on this owner instead of on the target's own type.
        /// </summary>
        public Type? AttachedOwnerType { get; init; }
    }

    private sealed record PendingStaticResource(
        object Target,
        Type TargetType,
        string PropertyName,
        string ResourceKey,
        FrameworkElement? Context);
}
