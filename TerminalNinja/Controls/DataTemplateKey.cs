namespace TerminalNinja.Controls;

/// <summary>
/// The resource-dictionary key under which an implicit <see cref="DataTemplate"/> is stored.
/// </summary>
/// <remarks>
/// A <see cref="DataTemplate"/> declared without an <c>x:Key</c> but with a
/// <see cref="DataTemplate.DataType"/> is filed under <c>new DataTemplateKey(dataType)</c>, and
/// looked up the same way when a control needs a template for a data item.
///
/// The key wraps the type rather than being the type itself because implicit
/// <see cref="Styling.Style"/>s are already keyed by their bare <c>TargetType</c>. Filing both
/// under the same key would make a <c>&lt;Style TargetType="Button"/&gt;</c> and a
/// <c>&lt;DataTemplate DataType="Button"/&gt;</c> in one dictionary overwrite each other, and
/// would let a template answer an implicit-style lookup.
/// </remarks>
public sealed class DataTemplateKey : IEquatable<DataTemplateKey>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataTemplateKey"/> class.
    /// </summary>
    /// <param name="dataType">The data type the template applies to.</param>
    public DataTemplateKey(Type dataType)
    {
        ArgumentNullException.ThrowIfNull(dataType);
        DataType = dataType;
    }

    /// <summary>
    /// Gets the data type the template applies to.
    /// </summary>
    public Type DataType { get; }

    /// <inheritdoc />
    public bool Equals(DataTemplateKey? other) => other is not null && DataType == other.DataType;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DataTemplateKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => DataType.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => $"DataTemplateKey({DataType.Name})";
}
