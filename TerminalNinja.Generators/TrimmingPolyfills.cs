// Polyfill stubs for trimming/AOT attributes used by Scriban 7.0.6 source-embedded code.
// These types exist in .NET 5+ but are missing in netstandard2.0.
// They are no-op stubs — they satisfy the compiler without affecting runtime behavior.

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

#if !NET5_0_OR_GREATER

// ReSharper disable once CheckNamespace
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
    internal sealed class RequiresUnreferencedCodeAttribute(string message) : Attribute
    {
        public string Message { get; } = message;
        public string? Url { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
    internal sealed class RequiresDynamicCodeAttribute(string message) : Attribute
    {
        public string Message { get; } = message;
        public string? Url { get; set; }
    }

    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    internal sealed class UnconditionalSuppressMessageAttribute(string category, string checkId) : Attribute
    {
        public string Category { get; } = category;
        public string CheckId { get; } = checkId;
        public string? Scope { get; set; }
        public string? Target { get; set; }
        public string? MessageId { get; set; }
        public string? Justification { get; set; }
    }

    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter |
        AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Method |
        AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct,
        Inherited = false, AllowMultiple = true)]
    internal sealed class DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes) : Attribute
    {
        public DynamicallyAccessedMemberTypes MemberTypes { get; } = memberTypes;
    }

    [Flags]
    internal enum DynamicallyAccessedMemberTypes
    {
        None = 0,
        PublicParameterlessConstructor = 0x0001,
        PublicConstructors = 0x0003,
        NonPublicConstructors = 0x0004,
        PublicMethods = 0x0008,
        NonPublicMethods = 0x0010,
        PublicFields = 0x0020,
        NonPublicFields = 0x0040,
        PublicNestedTypes = 0x0080,
        NonPublicNestedTypes = 0x0100,
        PublicProperties = 0x0200,
        NonPublicProperties = 0x0400,
        PublicEvents = 0x0800,
        NonPublicEvents = 0x1000,
        Interfaces = 0x2000,
        All = ~None,
    }
}

#endif
