using System.Text;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Ast;

/// <summary>
/// Renders an <see cref="Expr"/> to a canonical, fully-parenthesised string form.
/// Intended for round-trip parser tests and AST diagnostics — not for human-friendly
/// pretty printing.
/// </summary>
public static class AstPrinter
{
    /// <summary>Format <paramref name="expr"/> as a single-line canonical string.</summary>
    public static string Print(Expr expr)
    {
        var sb = new StringBuilder();
        Write(sb, expr);
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, Expr expr)
    {
        switch (expr)
        {
            case Lit lit:
                WriteValue(sb, lit.Value);
                break;
            case Var v:
                sb.Append(v.Name);
                break;
            case Let let:
                sb.Append("(let ").Append(let.Name).Append(" = ");
                Write(sb, let.Value);
                sb.Append(" in ");
                Write(sb, let.Body);
                sb.Append(')');
                break;
            case SourceStatement src:
                sb.Append("(source ");
                Write(sb, src.Path);
                sb.Append(')');
                break;
            case LetStatement letStmt:
                sb.Append("(let ").Append(letStmt.Name).Append(" = ");
                Write(sb, letStmt.Value);
                sb.Append(')');
                break;
            case Lambda lam:
                sb.Append('(');
                sb.Append('(').Append(string.Join(", ", lam.Parameters)).Append(')');
                sb.Append(" => ");
                Write(sb, lam.Body);
                sb.Append(')');
                break;
            case Call call:
                Write(sb, call.Function);
                sb.Append('(');
                for (int i = 0; i < call.Args.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    Write(sb, call.Args[i]);
                }
                sb.Append(')');
                break;
            case Switch sw:
                Write(sb, sw.Scrutinee);
                sb.Append(" switch { ");
                for (int i = 0; i < sw.Arms.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    WritePattern(sb, sw.Arms[i].Pattern);
                    sb.Append(" => ");
                    Write(sb, sw.Arms[i].Body);
                }
                sb.Append(" }");
                break;
            case RecordLit rec:
                sb.Append("{ ");
                for (int i = 0; i < rec.Fields.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(QuoteIfNeeded(rec.Fields[i].Key)).Append(": ");
                    Write(sb, rec.Fields[i].Value);
                }
                sb.Append(" }");
                break;
            case ListLit list:
                sb.Append('[');
                for (int i = 0; i < list.Items.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    Write(sb, list.Items[i]);
                }
                sb.Append(']');
                break;
            case RangeLit range:
                sb.Append('(');
                Write(sb, range.Lo);
                sb.Append("..");
                Write(sb, range.Hi);
                sb.Append(')');
                break;
            case InterpExpr interp:
                sb.Append("$\"");
                foreach (var seg in interp.Segments)
                {
                    switch (seg)
                    {
                        case InterpTextSegment t:
                            sb.Append(t.Text.Replace("\"", "\\\""));
                            break;
                        case InterpHoleSegment h:
                            sb.Append('{');
                            Write(sb, h.Expression);
                            sb.Append('}');
                            break;
                    }
                }
                sb.Append('"');
                break;
            case PwshExpr pwsh:
                sb.Append("pwsh {").Append(pwsh.Body).Append('}');
                break;
            case MemberAccess m:
                Write(sb, m.Target);
                sb.Append('.').Append(m.Member);
                break;
            case IndexAccess ix:
                Write(sb, ix.Target);
                sb.Append('[');
                Write(sb, ix.Index);
                sb.Append(']');
                break;
            case BinOp bin:
                sb.Append('(');
                Write(sb, bin.Left);
                sb.Append(' ').Append(BinOpToken(bin.Op)).Append(' ');
                Write(sb, bin.Right);
                sb.Append(')');
                break;
            case UnaryOp un:
                sb.Append('(').Append(un.Op == UnaryOpKind.Neg ? "-" : "!");
                Write(sb, un.Operand);
                sb.Append(')');
                break;
            default:
                sb.Append('?').Append(expr.GetType().Name).Append('?');
                break;
        }
    }

    private static void WritePattern(StringBuilder sb, Pattern p)
    {
        switch (p)
        {
            case LitPattern lp: WriteValue(sb, lp.Value); break;
            case WildcardPattern: sb.Append('_'); break;
            case BindingPattern bp: sb.Append(bp.Name); break;
        }
    }

    private static void WriteValue(StringBuilder sb, NValue v)
    {
        switch (v)
        {
            case NUnit: sb.Append("()"); break;
            case NBool b: sb.Append(b.Value ? "true" : "false"); break;
            case NInt i: sb.Append(i.Value); break;
            case NFloat f: sb.Append(f.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
            case NString s: sb.Append('"').Append(s.Value.Replace("\"", "\\\"")).Append('"'); break;
            default: sb.Append('?').Append(v.GetType().Name).Append('?'); break;
        }
    }

    private static string BinOpToken(BinOpKind op) => op switch
    {
        BinOpKind.Add => "+",
        BinOpKind.Sub => "-",
        BinOpKind.Mul => "*",
        BinOpKind.Div => "/",
        BinOpKind.Eq => "==",
        BinOpKind.NotEq => "!=",
        BinOpKind.Less => "<",
        BinOpKind.LessEq => "<=",
        BinOpKind.Greater => ">",
        BinOpKind.GreaterEq => ">=",
        BinOpKind.And => "&&",
        BinOpKind.Or => "||",
        _ => "?",
    };

    private static string QuoteIfNeeded(string key)
    {
        if (string.IsNullOrEmpty(key)) return "\"\"";
        if (!(char.IsLetter(key[0]) || key[0] == '_')) return "\"" + key + "\"";
        foreach (var c in key)
            if (!(char.IsLetterOrDigit(c) || c == '_')) return "\"" + key + "\"";
        return key;
    }
}
