using Portable.Xaml;
using System.Reflection;

var method = typeof(XamlSchemaContext).GetMethod(
    "GetCustomAttributeProvider", 
    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
    null,
    [typeof(Type)],
    null);

if (method != null)
{
    Console.WriteLine($"Method found: {method.Name}");
    Console.WriteLine($"IsVirtual: {method.IsVirtual}");
    Console.WriteLine($"IsPublic: {method.IsPublic}");
    Console.WriteLine($"IsFamily: {method.IsFamily}"); // protected
    Console.WriteLine($"IsAssembly: {method.IsAssembly}"); // internal
    Console.WriteLine($"IsFamilyOrAssembly: {method.IsFamilyOrAssembly}"); // protected internal
    Console.WriteLine($"IsFamilyAndAssembly: {method.IsFamilyAndAssembly}"); // protected AND internal (rare)
    Console.WriteLine($"Return type: {method.ReturnType}");
    Console.WriteLine($"Parameters: {string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))}");
    Console.WriteLine($"ToString: {method}");
}
else
{
    Console.WriteLine("Method not found!");
}
