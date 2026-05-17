using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using TerminalNinja.Shell.Ast;
using TerminalNinja.Shell.Parser;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Runtime;

/// <summary>
/// Tree-walking interpreter for NinjaShell. Stateless — every evaluation
/// receives the source <see cref="Expr"/> and an immutable <see cref="Env"/>
/// and returns an <see cref="NValue"/>.
/// </summary>
public static class NinjaEvaluator
{
    /// <summary>
    /// Parse + evaluate a NinjaShell source string against <paramref name="env"/>.
    /// Top-level <c>let NAME = VALUE</c> (without an <c>in</c> clause) extends
    /// the returned environment; otherwise the environment passes through.
    /// Restricted to a single top-level form; use <see cref="EvalScript"/> for
    /// multi-form scripts.
    /// </summary>
    public static EvalResult EvalSource(string source, Env env)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(env);

        var expr = NinjaParser.ParseExpression(source);
        return EvalTop(expr, env);
    }

    /// <summary>
    /// Parse + evaluate <paramref name="source"/> as a sequence of zero or more
    /// top-level forms. Each form is evaluated via <see cref="EvalTop"/>, so
    /// let-statements extend the environment that subsequent forms see. The
    /// returned <see cref="EvalResult.Value"/> is the last form's value (or
    /// <see cref="NUnit.Instance"/> for an empty script).
    /// </summary>
    public static EvalResult EvalScript(string source, Env env)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(env);

        var forms = NinjaParser.ParseScript(source);
        if (forms.Length == 0) return new EvalResult(NUnit.Instance, env);

        NValue last = NUnit.Instance;
        foreach (var form in forms)
        {
            var r = EvalTop(form, env);
            env = r.Env;
            last = r.Value;
        }
        return new EvalResult(last, env);
    }

    /// <summary>Maximum nesting depth for <c>source(...)</c> includes.</summary>
    public const int MaxSourceDepth = 32;

    [ThreadStatic] private static int _sourceDepth;

    /// <summary>
    /// Evaluate a parsed top-level expression. <see cref="LetStatement"/> and
    /// <see cref="SourceStatement"/> nodes extend the environment for subsequent
    /// calls; everything else returns the same <see cref="Env"/>.
    /// </summary>
    public static EvalResult EvalTop(Expr expr, Env env)
    {
        ArgumentNullException.ThrowIfNull(expr);
        ArgumentNullException.ThrowIfNull(env);

        if (expr is LetStatement ls)
        {
            var newEnv = env.Reserve(ls.Name, out var slot);
            slot.Value = Eval(ls.Value, newEnv);
            return new EvalResult(slot.Value, newEnv);
        }
        if (expr is SourceStatement src)
        {
            return EvalSourceStatement(src, env);
        }
        return new EvalResult(Eval(expr, env), env);
    }

    private static EvalResult EvalSourceStatement(SourceStatement src, Env env)
    {
        var pathVal = Eval(src.Path, env);
        if (pathVal is not NString s)
            throw new EvaluatorException("source: path must evaluate to a string");
        var full = Path.GetFullPath(s.Value);
        if (!File.Exists(full))
            throw new EvaluatorException($"source: file not found: {full}");

        _sourceDepth++;
        try
        {
            if (_sourceDepth > MaxSourceDepth)
                throw new EvaluatorException($"source: maximum nesting depth ({MaxSourceDepth}) exceeded — recursive include from '{full}'?");

            string content;
            try
            {
                content = File.ReadAllText(full);
            }
            catch (Exception ex)
            {
                throw new EvaluatorException($"source: could not read '{full}': {ex.Message}", ex);
            }

            ImmutableArray<Expr> forms;
            try
            {
                forms = NinjaParser.ParseScript(content);
            }
            catch (Exception ex)
            {
                throw new EvaluatorException($"source: parse error in '{full}': {ex.Message}", ex);
            }

            NValue last = NUnit.Instance;
            foreach (var form in forms)
            {
                var r = EvalTop(form, env);
                env = r.Env;
                last = r.Value;
            }
            return new EvalResult(last, env);
        }
        finally
        {
            _sourceDepth--;
        }
    }

    /// <summary>Evaluate <paramref name="expr"/> against <paramref name="env"/>, producing an <see cref="NValue"/>.</summary>
    public static NValue Eval(Expr expr, Env env)
    {
        switch (expr)
        {
            case Lit lit: return lit.Value;
            case Var v: return env.Lookup(v.Name);
            case Let let:
                {
                    var inner = env.Reserve(let.Name, out var slot);
                    slot.Value = Eval(let.Value, inner);
                    return Eval(let.Body, inner);
                }
            case LetStatement ls:
                throw new EvaluatorException("'let' statement is only valid at the top level (no 'in' clause)");
            case Lambda lam: return MakeLambda(lam, env);
            case Call call: return EvalCall(call, env);
            case Switch sw: return EvalSwitch(sw, env);
            case RecordLit rec: return EvalRecord(rec, env);
            case ListLit list: return EvalList(list, env);
            case RangeLit range: return EvalRange(range, env);
            case InterpExpr interp: return EvalInterp(interp, env);
            case PwshExpr pwsh: return EvalPwsh(pwsh, env);
            case MemberAccess m: return EvalMember(m, env);
            case IndexAccess ix: return EvalIndex(ix, env);
            case BinOp bin: return EvalBinOp(bin, env);
            case UnaryOp un: return EvalUnaryOp(un, env);
            default:
                throw new EvaluatorException($"unhandled AST node {expr.GetType().Name}");
        }
    }

    private static NValue MakeLambda(Lambda lam, Env captured)
    {
        var paramNames = lam.Parameters;
        var body = lam.Body;
        int arity = paramNames.Length;
        return new NFunc(args =>
        {
            if (args.Length != arity)
                throw new EvaluatorException($"lambda expected {arity} arg(s), got {args.Length}");
            var callEnv = captured;
            for (int i = 0; i < args.Length; i++)
                callEnv = callEnv.Extend(paramNames[i], args[i]);
            return Eval(body, callEnv);
        }, arity);
    }

    private static NValue EvalCall(Call call, Env env)
    {
        var fn = Eval(call.Function, env);
        if (fn is not NFunc f)
            throw new EvaluatorException($"value of type {NValueDescriber.Describe(fn)} is not callable");
        var args = new NValue[call.Args.Length];
        for (int i = 0; i < call.Args.Length; i++)
            args[i] = Eval(call.Args[i], env);
        return f.Apply(args);
    }

    private static NValue EvalSwitch(Switch sw, Env env)
    {
        var v = Eval(sw.Scrutinee, env);
        foreach (var arm in sw.Arms)
        {
            if (TryMatchPattern(arm.Pattern, v, env, out var armEnv))
                return Eval(arm.Body, armEnv);
        }
        throw new EvaluatorException($"no switch arm matched value of type {NValueDescriber.Describe(v)}");
    }

    private static bool TryMatchPattern(Pattern p, NValue v, Env env, out Env armEnv)
    {
        switch (p)
        {
            case LitPattern lp:
                armEnv = env;
                return NValueOps.Equals(lp.Value, v);
            case WildcardPattern:
                armEnv = env;
                return true;
            case BindingPattern bp:
                armEnv = env.Extend(bp.Name, v);
                return true;
            default:
                armEnv = env;
                return false;
        }
    }

    private static NValue EvalRecord(RecordLit rec, Env env)
    {
        var b = ImmutableSortedDictionary.CreateBuilder<string, NValue>(StringComparer.Ordinal);
        foreach (var field in rec.Fields)
            b.Add(field.Key, Eval(field.Value, env));
        return new NRecord(b.ToImmutable());
    }

    private static NValue EvalList(ListLit list, Env env)
    {
        var b = ImmutableArray.CreateBuilder<NValue>(list.Items.Length);
        foreach (var item in list.Items)
            b.Add(Eval(item, env));
        return new NList(b.MoveToImmutable());
    }

    private static NValue EvalRange(RangeLit range, Env env)
    {
        var lo = Eval(range.Lo, env);
        var hi = Eval(range.Hi, env);
        if (lo is NInt li && hi is NInt hh)
        {
            return new NSeq(EnumerateRange(li.Value, hh.Value));
        }
        throw new EvaluatorException("range bounds must be integers");
    }

    private static IEnumerable<NValue> EnumerateRange(long lo, long hi)
    {
        for (long i = lo; i <= hi; i++)
            yield return new NInt(i);
    }

    private static NValue EvalInterp(InterpExpr interp, Env env)
    {
        var sb = new StringBuilder();
        foreach (var seg in interp.Segments)
        {
            switch (seg)
            {
                case InterpTextSegment t: sb.Append(t.Text); break;
                case InterpHoleSegment h:
                    sb.Append(NValueOps.FormatForInterpolation(Eval(h.Expression, env)));
                    break;
            }
        }
        return new NString(sb.ToString());
    }

    private static NValue EvalPwsh(PwshExpr pwsh, Env env)
    {
        var bridge = env.Lookup("__pwsh_bridge__");
        if (bridge is not NFunc bridgeFn)
            throw new EvaluatorException("PowerShell bridge is not installed in this environment");
        return bridgeFn.Apply(new NValue[] { new NString(pwsh.Body) });
    }

    private static NValue EvalMember(MemberAccess m, Env env)
    {
        var target = Eval(m.Target, env);
        if (target is NRecord rec && rec.Fields.TryGetValue(m.Member, out var val)) return val;
        throw new EvaluatorException($"cannot read member '{m.Member}' on value of type {NValueDescriber.Describe(target)}");
    }

    private static NValue EvalIndex(IndexAccess ix, Env env)
    {
        var target = Eval(ix.Target, env);
        var index = Eval(ix.Index, env);
        if (target is NRecord rec && index is NString s)
        {
            if (rec.Fields.TryGetValue(s.Value, out var val)) return val;
            throw new EvaluatorException($"record has no field '{s.Value}'");
        }
        if (target is NList list && index is NInt i)
        {
            if (i.Value < 0 || i.Value >= list.Items.Length)
                throw new EvaluatorException($"index {i.Value} out of range [0, {list.Items.Length})");
            return list.Items[(int)i.Value];
        }
        throw new EvaluatorException($"cannot index value of type {NValueDescriber.Describe(target)} with {NValueDescriber.Describe(index)}");
    }

    private static NValue EvalBinOp(BinOp bin, Env env)
    {
        if (bin.Op == BinOpKind.And)
        {
            var l = Eval(bin.Left, env);
            if (l is NBool lb)
            {
                if (!lb.Value) return new NBool(false);
                var r = Eval(bin.Right, env);
                if (r is NBool rb) return new NBool(rb.Value);
            }
            throw new EvaluatorException("'&&' requires boolean operands");
        }
        if (bin.Op == BinOpKind.Or)
        {
            var l = Eval(bin.Left, env);
            if (l is NBool lb)
            {
                if (lb.Value) return new NBool(true);
                var r = Eval(bin.Right, env);
                if (r is NBool rb) return new NBool(rb.Value);
            }
            throw new EvaluatorException("'||' requires boolean operands");
        }

        var left = Eval(bin.Left, env);
        var right = Eval(bin.Right, env);

        switch (bin.Op)
        {
            case BinOpKind.Add: return NValueOps.Add(left, right);
            case BinOpKind.Sub: return NValueOps.Sub(left, right);
            case BinOpKind.Mul: return NValueOps.Mul(left, right);
            case BinOpKind.Div: return NValueOps.Div(left, right);
            case BinOpKind.Eq: return new NBool(NValueOps.Equals(left, right));
            case BinOpKind.NotEq: return new NBool(!NValueOps.Equals(left, right));
            case BinOpKind.Less: return new NBool(NValueOps.Compare(left, right) < 0);
            case BinOpKind.LessEq: return new NBool(NValueOps.Compare(left, right) <= 0);
            case BinOpKind.Greater: return new NBool(NValueOps.Compare(left, right) > 0);
            case BinOpKind.GreaterEq: return new NBool(NValueOps.Compare(left, right) >= 0);
            default: throw new EvaluatorException($"unhandled binary op {bin.Op}");
        }
    }

    private static NValue EvalUnaryOp(UnaryOp un, Env env)
    {
        var v = Eval(un.Operand, env);
        switch (un.Op)
        {
            case UnaryOpKind.Neg:
                if (v is NInt i) return new NInt(-i.Value);
                if (v is NFloat f) return new NFloat(-f.Value);
                throw new EvaluatorException($"unary '-' requires numeric operand, got {NValueDescriber.Describe(v)}");
            case UnaryOpKind.Not:
                if (v is NBool b) return new NBool(!b.Value);
                throw new EvaluatorException($"unary '!' requires boolean operand, got {NValueDescriber.Describe(v)}");
            default:
                throw new EvaluatorException($"unhandled unary op {un.Op}");
        }
    }
}

/// <summary>The result of evaluating a top-level expression: the produced value plus the (possibly extended) environment.</summary>
public readonly record struct EvalResult(NValue Value, Env Env);
