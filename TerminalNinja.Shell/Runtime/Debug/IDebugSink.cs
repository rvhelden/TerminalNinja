using TerminalNinja.Shell.Ast;

namespace TerminalNinja.Shell.Runtime.Debug;

/// <summary>
/// Callback surface the evaluator uses to notify a debugger of execution
/// progress. Implementations decide whether to pause, step, or continue.
/// <see cref="NinjaEvaluator"/> calls these methods only when a sink has been
/// installed for the current thread via <see cref="DebugScope"/>; uninstalled
/// runs incur a single null-check per AST node.
/// </summary>
internal interface IDebugSink
{
    /// <summary>
    /// Fires immediately before <see cref="NinjaEvaluator.Eval"/> dispatches on
    /// <paramref name="expr"/>. The sink may block this call (to pause the
    /// evaluator) until the user resumes via the DAP protocol.
    /// </summary>
    void OnEnter(Expr expr, Env env);

    /// <summary>Fires when a user-defined function or lambda is entered.</summary>
    void OnEnterCall(string name, Span callSite);

    /// <summary>Fires when the current user-defined call returns.</summary>
    void OnLeaveCall();
}
