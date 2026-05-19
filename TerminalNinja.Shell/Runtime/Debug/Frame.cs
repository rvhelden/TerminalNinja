using TerminalNinja.Shell.Ast;

namespace TerminalNinja.Shell.Runtime.Debug;

/// <summary>
/// One entry in the synthetic call stack the debugger maintains. The C# stack
/// is the real call stack, but it isn't reachable from DAP requests, so a
/// debug sink pushes a <see cref="Frame"/> on every user-level call and pops
/// it on return. <see cref="Span"/> is the *call site* in the caller, not the
/// callee's source location.
/// </summary>
internal readonly record struct Frame(string Name, Span Span, Env Env);
