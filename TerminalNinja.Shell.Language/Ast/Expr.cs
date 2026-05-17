using System.Collections.Immutable;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Ast;

/// <summary>Base type for every NinjaShell AST node.</summary>
public abstract record Expr;

/// <summary>A constant value literal (int, float, string, bool, or unit).</summary>
public sealed record Lit(NValue Value) : Expr;

/// <summary>An identifier reference resolved at evaluation time.</summary>
public sealed record Var(string Name) : Expr;

/// <summary>
/// <c>let NAME = VALUE in BODY</c> — binds <see cref="Name"/> to <see cref="Value"/>
/// in the environment used to evaluate <see cref="Body"/>. The binding is recursive:
/// <see cref="Value"/> is evaluated in an environment that already contains
/// <see cref="Name"/>, so lambdas can reference themselves.
/// </summary>
public sealed record Let(string Name, Expr Value, Expr Body) : Expr;

/// <summary>
/// Top-level form for the REPL: <c>let NAME = VALUE</c> with no <c>in</c> body.
/// Evaluates <see cref="Value"/>, binds it in the REPL environment, and returns it
/// so the printer can show the bound value.
/// </summary>
public sealed record LetStatement(string Name, Expr Value) : Expr;

/// <summary>
/// Top-level <c>source("path")</c> form. Reads the file, parses it as a script,
/// and evaluates each form in the current scope — bindings persist back to the
/// caller. Only valid at the top level of a script; in expression position the
/// parser raises a syntax error.
/// </summary>
public sealed record SourceStatement(Expr Path) : Expr;

/// <summary>A lambda — <c>(p1, p2) =&gt; body</c> or single-param <c>p =&gt; body</c>.</summary>
public sealed record Lambda(ImmutableArray<string> Parameters, Expr Body) : Expr;

/// <summary>Application of <see cref="Function"/> to <see cref="Args"/>.</summary>
public sealed record Call(Expr Function, ImmutableArray<Expr> Args) : Expr;

/// <summary>C#-style switch expression: <c>scrutinee switch { arms }</c>.</summary>
public sealed record Switch(Expr Scrutinee, ImmutableArray<SwitchArm> Arms) : Expr;

/// <summary>One arm of a <see cref="Switch"/>: <c>pattern =&gt; body</c>.</summary>
public sealed record SwitchArm(Pattern Pattern, Expr Body);

/// <summary>Base type for switch arm patterns.</summary>
public abstract record Pattern;

/// <summary>Matches when the scrutinee is equal to <see cref="Value"/>.</summary>
public sealed record LitPattern(NValue Value) : Pattern;

/// <summary>Wildcard pattern (<c>_</c>) — matches anything, no binding.</summary>
public sealed record WildcardPattern : Pattern;

/// <summary>Binding pattern — matches anything and binds the scrutinee to <see cref="Name"/> in the arm body.</summary>
public sealed record BindingPattern(string Name) : Pattern;

/// <summary>Anonymous record literal: <c>{ key: value, ... }</c>.</summary>
public sealed record RecordLit(ImmutableArray<RecordField> Fields) : Expr;

/// <summary>A single field in a <see cref="RecordLit"/>.</summary>
public sealed record RecordField(string Key, Expr Value);

/// <summary>List literal: <c>[item, item, ...]</c>.</summary>
public sealed record ListLit(ImmutableArray<Expr> Items) : Expr;

/// <summary>Range literal: <c>lo..hi</c>, inclusive at both ends.</summary>
public sealed record RangeLit(Expr Lo, Expr Hi) : Expr;

/// <summary>Interpolated string: a sequence of text/hole segments rendered into a single <c>NString</c> at runtime.</summary>
public sealed record InterpExpr(ImmutableArray<InterpSegment> Segments) : Expr;

/// <summary>Base type for an interpolation segment.</summary>
public abstract record InterpSegment;

/// <summary>A literal text segment in an interpolated string.</summary>
public sealed record InterpTextSegment(string Text) : InterpSegment;

/// <summary>A <c>{expr}</c> hole in an interpolated string.</summary>
public sealed record InterpHoleSegment(Expr Expression) : InterpSegment;

/// <summary>A <c>pwsh { ... }</c> escape — verbatim PowerShell source forwarded to the bridge.</summary>
public sealed record PwshExpr(string Body) : Expr;

/// <summary>Dot member access: <c>target.member</c>.</summary>
public sealed record MemberAccess(Expr Target, string Member) : Expr;

/// <summary>Indexer access: <c>target[index]</c>.</summary>
public sealed record IndexAccess(Expr Target, Expr Index) : Expr;

/// <summary>Binary operation. The operator kinds match the precedence table in the parser.</summary>
public sealed record BinOp(BinOpKind Op, Expr Left, Expr Right) : Expr;

/// <summary>Unary operation.</summary>
public sealed record UnaryOp(UnaryOpKind Op, Expr Operand) : Expr;

/// <summary>Supported binary operators.</summary>
public enum BinOpKind
{
    Add, Sub, Mul, Div,
    Eq, NotEq, Less, LessEq, Greater, GreaterEq,
    And, Or,
}

/// <summary>Supported unary operators.</summary>
public enum UnaryOpKind { Neg, Not }
