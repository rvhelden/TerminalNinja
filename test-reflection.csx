#!/usr/bin/env dotnet-script
#r "nuget: Portable.Xaml, 0.26.0"

using Portable.Xaml;
using System.Reflection;

var method = typeof(XamlSchemaContext).GetMethod(
    "GetCustomAttributeProvider", 
    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
    null,
    new[] { typeof(Type) },
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
}
else
{
    Console.WriteLine("Method not found!");
}
