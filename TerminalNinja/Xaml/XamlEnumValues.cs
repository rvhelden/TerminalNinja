namespace TerminalNinja.Xaml;

/// <summary>
/// Parses enum values written in XAML.
/// <para>
/// TerminalNinja's enums deliberately do not use WPF's spellings — <see cref="Primitives.TextTrimming"/>
/// is <c>None</c>/<c>Ellipsis</c> rather than <c>CharacterEllipsis</c>, and both
/// <see cref="Primitives.TextAlignment"/> and <see cref="Primitives.Alignment"/> are
/// <c>Start</c>/<c>Center</c>/<c>End</c> rather than <c>Left</c>/<c>Right</c>. Anyone arriving
/// from WPF writes the WPF spelling, and because XAML attributes are converted at load time that
/// mistake is not a build error: it is an exception thrown the first time the screen opens.
/// </para>
/// <para>
/// So the WPF spellings are accepted here as aliases of the existing members. The public enums
/// stay unchanged — nothing new to switch over, no duplicate members — and a value that is still
/// wrong produces a message naming the property, the offending value and every accepted spelling
/// instead of .NET's bare "Requested value 'X' was not found".
/// </para>
/// </summary>
public static class XamlEnumValues
{
    /// <summary>
    /// WPF (and other close-enough) spellings mapped to the TerminalNinja member they mean.
    /// Keyed by enum type; the inner lookup is case-insensitive, matching the case-insensitive
    /// parsing XAML has always done.
    /// </summary>
    /// <remarks>
    /// <c>XamlLayoutGenerator</c> carries a copy of this table because a source generator cannot
    /// reference the runtime assembly. Keep the two in step.
    /// </remarks>
    private static readonly Dictionary<Type, Dictionary<string, string>> AliasesByType = new()
    {
        [typeof(Primitives.TextTrimming)] = new(StringComparer.OrdinalIgnoreCase)
        {
            // WPF distinguishes trimming at a character or a word boundary; the terminal
            // renderer only trims at cells, so both mean "Ellipsis" here.
            ["CharacterEllipsis"] = "Ellipsis",
            ["WordEllipsis"] = "Ellipsis",
            ["Clip"] = "None",
        },
        [typeof(Primitives.TextAlignment)] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Left"] = "Start",
            ["Right"] = "End",
        },
        [typeof(Primitives.Alignment)] = new(StringComparer.OrdinalIgnoreCase)
        {
            // Alignment is used for both axes, so the vertical spellings alias too.
            ["Left"] = "Start",
            ["Top"] = "Start",
            ["Right"] = "End",
            ["Bottom"] = "End",
        },
        [typeof(Primitives.TextWrapping)] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["WrapWithOverflow"] = "Wrap",
        },
    };

    /// <summary>
    /// Parses <paramref name="value"/> as a member of <paramref name="enumType"/>, accepting the
    /// canonical spelling (case-insensitively) or any WPF alias.
    /// </summary>
    /// <returns><c>true</c> when the value was recognised.</returns>
    public static bool TryParse(Type enumType, string value, out object? result)
    {
        ArgumentNullException.ThrowIfNull(enumType);

        result = null;
        if (!enumType.IsEnum || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        // Canonical spelling wins, so an alias can never shadow a real member.
        // Enum.TryParse accepts numeric text as well ("1"); XAML has always allowed that.
        if (Enum.TryParse(enumType, trimmed, ignoreCase: true, out var parsed))
        {
            result = parsed;
            return true;
        }

        if (AliasesByType.TryGetValue(enumType, out var aliases)
            && aliases.TryGetValue(trimmed, out var canonical)
            && Enum.TryParse(enumType, canonical, ignoreCase: true, out var aliased))
        {
            result = aliased;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses <paramref name="value"/> as a member of <paramref name="enumType"/>, throwing an
    /// <see cref="ArgumentException"/> that names the property, the offending value and the
    /// accepted spellings when it is not recognised.
    /// </summary>
    /// <param name="enumType">The enum type being assigned.</param>
    /// <param name="value">The raw text from the XAML attribute.</param>
    /// <param name="propertyName">The property being set, if known — this is what the author has to fix.</param>
    /// <param name="ownerTypeName">The type declaring the property, if known.</param>
    public static object Parse(Type enumType, string value, string? propertyName = null, string? ownerTypeName = null)
    {
        if (TryParse(enumType, value, out var result))
        {
            return result!;
        }

        throw new ArgumentException(BuildErrorMessage(enumType, value, propertyName, ownerTypeName));
    }

    /// <summary>
    /// Builds the "here is what you may write instead" message used by <see cref="Parse"/>.
    /// </summary>
    public static string BuildErrorMessage(Type enumType, string value, string? propertyName = null, string? ownerTypeName = null)
    {
        ArgumentNullException.ThrowIfNull(enumType);

        var target = propertyName is null
            ? $"type '{enumType.Name}'"
            : ownerTypeName is null
                ? $"property '{propertyName}' (type '{enumType.Name}')"
                : $"property '{ownerTypeName}.{propertyName}' (type '{enumType.Name}')";

        var message = $"'{value}' is not a valid value for {target}. Valid values are: {DescribeAcceptedValues(enumType)}.";

        var suggestion = SuggestClosest(enumType, value);
        if (suggestion != null)
        {
            message += $" Did you mean '{suggestion}'?";
        }

        return message;
    }

    /// <summary>
    /// Lists the canonical members of <paramref name="enumType"/> followed by the accepted
    /// WPF aliases, e.g. <c>None, Ellipsis (also accepted: CharacterEllipsis, WordEllipsis)</c>.
    /// </summary>
    public static string DescribeAcceptedValues(Type enumType)
    {
        ArgumentNullException.ThrowIfNull(enumType);

        var names = string.Join(", ", Enum.GetNames(enumType));

        if (AliasesByType.TryGetValue(enumType, out var aliases) && aliases.Count > 0)
        {
            var aliasText = string.Join(", ", aliases.Select(a => $"{a.Key} = {a.Value}"));
            names += $" (WPF spellings also accepted: {aliasText})";
        }

        return names;
    }

    /// <summary>
    /// Picks the accepted spelling closest to <paramref name="value"/> — a case-insensitive
    /// prefix or substring match — so a typo points at its own fix. Returns null when nothing is close.
    /// </summary>
    private static string? SuggestClosest(Type enumType, string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        foreach (var name in Enum.GetNames(enumType))
        {
            if (name.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(name, StringComparison.OrdinalIgnoreCase)
                || name.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }
}
