# AGENTS.md - Developer Guide for TerminalNinja

This document provides essential information for AI coding agents working in the TerminalNinja codebase.

## Project Overview

- **Language**: C# 13 (latest)
- **Framework**: .NET 10.0
- **Test Framework**: TUnit v1.12.93
- **IDE**: JetBrains Rider (optional)
- **Solution Structure**: 
  - `TerminalNinja.Core` - Core class library
  - `TerminalNinja.Core.Tests` - Test project
  - `Sample` - Sample console application

## Build & Test Commands

### Building the Project

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build TerminalNinja.Core/TerminalNinja.Core.csproj

# Build in Release mode
dotnet build -c Release

# Clean and rebuild
dotnet clean && dotnet build
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test -v detailed

# Run a single test by filter
dotnet test --filter "FullyQualifiedName=TerminalNinja.Core.Tests.Tests.MyTest"

# Run tests matching a pattern
dotnet test --filter "FullyQualifiedName~MyTest"

# Run tests with code coverage
dotnet test --collect:"Code Coverage"

# List all discovered tests without running them
dotnet test --list-tests
```

### Running the Sample Application

```bash
# Run the sample console app
dotnet run --project Sample/Sample.csproj
```

## Important reference sources

Portable.Xaml
e:\thirdparty\Portable.Xaml\

Spectre.Console
e:\thirdparty\spectre\

## Code Style Guidelines

### General Principles

- Use **nullable reference types** - all projects have `<Nullable>enable</Nullable>`
- Use **implicit usings** - avoid redundant using statements for common namespaces
- Use **file-scoped namespaces** for cleaner code
- Follow standard .NET naming conventions
- Write async code with `async/await` by default

### Imports and Usings

**Implicit Global Usings (Auto-imported for all projects):**
- `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`
- `System.Net.Http`, `System.Threading`, `System.Threading.Tasks`

**Test Project Global Usings (in GlobalUsings.cs):**
```csharp
global using TUnit.Core;
global using TUnit.Assertions;
global using TUnit.Assertions.Extensions;
global using TerminalNinja.Core.Primitives;
global using TerminalNinja.Core.Buffers;
global using TerminalNinja.Core.Elements;
global using TerminalNinja.Core.Styling;
global using TerminalNinja.Core.Tests.Helpers;
```

**Guidelines:**
- Do NOT add explicit usings for types covered by implicit usings
- Place project-specific global usings in `GlobalUsings.cs`
- Order explicit usings: System namespaces first, then third-party, then project namespaces
- Remove unused usings (IDE will warn)

### Namespace and File Structure

```csharp
namespace TerminalNinja.Core.ComponentName;

public class ClassName
{
    // Implementation
}
```

- Use **file-scoped namespaces** (single line, no braces)
- One public type per file
- File name must match the primary type name
- Namespace should match folder structure: `TerminalNinja.Core.{FolderPath}`

### Naming Conventions

| Element            | Convention        | Example                               |
|--------------------|-------------------|---------------------------------------|
| Namespaces         | PascalCase        | `TerminalNinja.Core.Services`         |
| Classes/Interfaces | PascalCase        | `CommandExecutor`, `ICommandHandler`  |
| Methods            | PascalCase        | `ExecuteCommand`, `GetResultAsync`    |
| Properties         | PascalCase        | `CommandName`, `IsEnabled`            |
| Fields (private)   | camelCase with _  | `_commandQueue`, `_isInitialized`     |
| Parameters         | camelCase         | `commandText`, `userName`             |
| Local variables    | camelCase         | `result`, `commandLine`               |
| Constants          | PascalCase        | `MaxRetryCount`, `DefaultTimeout`     |
| Async methods      | Suffix with Async | `ExecuteAsync`, `ProcessCommandAsync` |

### Type and Null Safety

```csharp
// Always annotate nullability explicitly
public string? GetOptionalValue() => null;  // nullable return
public string GetRequiredValue() => "value";  // non-nullable return

// Use nullable value types when appropriate
public int? TryParse(string input) { ... }

// Validate parameters
public void ProcessCommand(string command)
{
    ArgumentNullException.ThrowIfNull(command);
    // Implementation
}
```

### Async/Await Patterns

```csharp
// Prefer async methods that return Task or Task<T>
public async Task<Result> ExecuteAsync(CancellationToken cancellationToken = default)
{
    var result = await SomeAsyncOperation(cancellationToken);
    return result;
}

// Use ConfigureAwait(false) in library code when safe
var data = await ReadDataAsync().ConfigureAwait(false);

// Pass CancellationToken as the last parameter
public async Task ProcessAsync(string input, CancellationToken cancellationToken)
```

### Error Handling

```csharp
// Use specific exception types
throw new ArgumentException($"Invalid command: {command}", nameof(command));
throw new InvalidOperationException("Service not initialized");

// Catch specific exceptions
try
{
    await ExecuteCommandAsync();
}
catch (CommandException ex)
{
    // Handle specific error
}
catch (Exception ex)
{
    // Log and rethrow or wrap
    throw new ApplicationException("Command execution failed", ex);
}
```

## Testing Guidelines

### Test Structure (TUnit Framework)

```csharp
namespace TerminalNinja.Core.Tests;

public class CommandExecutorTests
{
    [Test]
    public async Task ExecuteCommand_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var executor = new CommandExecutor();
        var command = "test-command";
        
        // Act
        var result = await executor.ExecuteAsync(command);
        
        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Output).IsNotNull();
    }
    
    [Test]
    public async Task ExecuteCommand_NullInput_ThrowsException()
    {
        // Arrange
        var executor = new CommandExecutor();
        
        // Act & Assert
        await Assert.That(() => executor.ExecuteAsync(null!))
            .ThrowsExactly<ArgumentNullException>();
    }
}
```

### Test Naming

- Pattern: `MethodName_Scenario_ExpectedBehavior`
- Examples:
  - `Execute_ValidCommand_ReturnsSuccess`
  - `Parse_EmptyString_ThrowsArgumentException`
  - `ProcessAsync_WithCancellation_StopsGracefully`

### TUnit Assertions

```csharp
// Boolean assertions
await Assert.That(value).IsTrue();
await Assert.That(value).IsFalse();

// Equality assertions
await Assert.That(actual).IsEqualTo(expected);
await Assert.That(actual).IsNotEqualTo(other);

// Null assertions
await Assert.That(value).IsNull();
await Assert.That(value).IsNotNull();

// Exception assertions
await Assert.That(action).ThrowsExactly<ExceptionType>();

// String assertions
await Assert.That(text).Contains("substring");
```

## Git Workflow

- **Branch naming**: `feature/description`, `bugfix/issue-name`, `refactor/component`
- **Commit messages**: Use conventional commits format
  - `feat: add command executor`
  - `fix: handle null input in parser`
  - `refactor: simplify command processing`
  - `test: add tests for command validation`
  - `docs: update API documentation`

## Important Notes

- All tests are **async** - use `async Task` and `await Assert.That(...)`
- No linting configuration yet - follow standard .NET conventions
- Target framework is **.NET 10.0** - use latest C# features
- Keep code coverage high - add tests for new functionality
- Prefer composition over inheritance
- Keep methods small and focused (single responsibility)
- Document public APIs with XML comments

## Project Files to Never Modify

- `bin/`, `obj/` - Build output directories (gitignored)
- `*.user` files - User-specific IDE settings
- `/.vs/`, `/.idea/` - IDE-specific folders

## Adding New Files

When creating new source files in `TerminalNinja.Core`:
1. Place in appropriate folder matching the namespace
2. Use file-scoped namespaces
3. Ensure nullable reference types are handled correctly
4. Add corresponding test file in `TerminalNinja.Core.Tests` with matching structure
5. Follow naming convention: `ComponentName.cs` for implementation, `ComponentNameTests.cs` for tests
