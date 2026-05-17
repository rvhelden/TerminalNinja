using System.Collections.Immutable;
using TerminalNinja.Shell.Ast;
using TerminalNinja.Shell.Lexer;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Parser;

/// <summary>
/// Recursive-descent parser for NinjaShell. The token stream comes from
/// <see cref="NinjaLexer"/>; the parser produces an <see cref="Expr"/> AST.
/// Pipes are desugared to <see cref="Call"/> nodes at parse time, ranges to
/// <see cref="RangeLit"/>, and interpolation hole bodies are recursively
/// re-parsed as sub-expressions.
/// </summary>
public static class NinjaParser
{
    /// <summary>Parse a single top-level expression from <paramref name="source"/>.</summary>
    public static Expr ParseExpression(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tokens = NinjaLexer.Tokenize(source);
        var p = new State(tokens);
        var expr = p.ParseTopLevel();
        p.ExpectEof();
        return expr;
    }

    private sealed class State
    {
        private readonly IReadOnlyList<Token> _tokens;
        private int _pos;

        public State(IReadOnlyList<Token> tokens)
        {
            _tokens = tokens;
            _pos = 0;
        }

        private Token Peek(int offset = 0) => _tokens[Math.Min(_pos + offset, _tokens.Count - 1)];

        private Token Current => _tokens[_pos];

        private bool IsAtEnd => Current.Kind == TokenKind.Eof;

        private Token Advance()
        {
            var t = Current;
            if (!IsAtEnd) _pos++;
            return t;
        }

        private bool Check(TokenKind k) => Current.Kind == k;

        private bool Match(TokenKind k)
        {
            if (Check(k)) { Advance(); return true; }
            return false;
        }

        private Token Expect(TokenKind k, string what)
        {
            if (Check(k)) return Advance();
            bool incomplete = IsAtEnd;
            throw new ParserException(
                $"expected {what} but got {Current.Kind}('{Current.Text}')",
                Current.Line, Current.Column, incomplete);
        }

        private void SkipNewlines()
        {
            while (Match(TokenKind.Newline)) { }
        }

        public void ExpectEof()
        {
            SkipNewlines();
            if (!IsAtEnd)
                throw new ParserException(
                    $"unexpected trailing token {Current.Kind}('{Current.Text}')",
                    Current.Line, Current.Column, isIncomplete: false);
        }

        public Expr ParseTopLevel()
        {
            SkipNewlines();
            if (Check(TokenKind.KwLet) && IsTopLevelLetStatement())
            {
                return ParseLetStatementOrLetIn();
            }
            return ParseExpr();
        }

        /// <summary>
        /// Returns true if the current `let` should be parsed as a statement (no `in` clause).
        /// Walks forward to find the matching `in` or EOF at the same nesting depth.
        /// </summary>
        private bool IsTopLevelLetStatement()
        {
            int i = _pos + 1;
            int parenDepth = 0, bracketDepth = 0, braceDepth = 0;
            while (i < _tokens.Count)
            {
                var tk = _tokens[i].Kind;
                if (parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                {
                    if (tk == TokenKind.KwIn) return false;
                    if (tk == TokenKind.Eof) return true;
                }
                switch (tk)
                {
                    case TokenKind.LParen: parenDepth++; break;
                    case TokenKind.RParen: parenDepth--; break;
                    case TokenKind.LBracket: bracketDepth++; break;
                    case TokenKind.RBracket: bracketDepth--; break;
                    case TokenKind.LBrace: braceDepth++; break;
                    case TokenKind.RBrace: braceDepth--; break;
                }
                i++;
            }
            return true;
        }

        private Expr ParseLetStatementOrLetIn()
        {
            Expect(TokenKind.KwLet, "'let'");
            var name = Expect(TokenKind.Identifier, "identifier after 'let'").Text;
            Expect(TokenKind.Assign, "'=' after let name");
            SkipNewlines();
            var value = ParseExpr();
            SkipNewlines();
            if (Match(TokenKind.KwIn))
            {
                SkipNewlines();
                var body = ParseExpr();
                return new Let(name, value, body);
            }
            return new LetStatement(name, value);
        }

        private Expr ParseExpr()
        {
            if (Check(TokenKind.KwLet) && !IsTopLevelLetStatement())
            {
                Advance();
                var name = Expect(TokenKind.Identifier, "identifier after 'let'").Text;
                Expect(TokenKind.Assign, "'=' after let name");
                SkipNewlines();
                var value = ParseExpr();
                SkipNewlines();
                Expect(TokenKind.KwIn, "'in' to close let-binding");
                SkipNewlines();
                var body = ParseExpr();
                return new Let(name, value, body);
            }

            return ParsePipe();
        }

        private Expr ParsePipe()
        {
            var left = ParseOr();
            while (true)
            {
                SkipNewlines();
                if (!Match(TokenKind.Pipe)) break;
                SkipNewlines();
                var right = ParseOr();
                left = DesugarPipe(left, right);
            }
            return left;
        }

        private static Expr DesugarPipe(Expr lhs, Expr rhs)
        {
            return rhs switch
            {
                Call call => new Call(call.Function, ImmutableArray.Create(lhs).AddRange(call.Args)),
                _ => new Call(rhs, ImmutableArray.Create(lhs)),
            };
        }

        private Expr ParseOr()
        {
            var left = ParseAnd();
            while (Match(TokenKind.OrOr))
            {
                var right = ParseAnd();
                left = new BinOp(BinOpKind.Or, left, right);
            }
            return left;
        }

        private Expr ParseAnd()
        {
            var left = ParseEquality();
            while (Match(TokenKind.AndAnd))
            {
                var right = ParseEquality();
                left = new BinOp(BinOpKind.And, left, right);
            }
            return left;
        }

        private Expr ParseEquality()
        {
            var left = ParseComparison();
            while (true)
            {
                if (Match(TokenKind.EqEq)) left = new BinOp(BinOpKind.Eq, left, ParseComparison());
                else if (Match(TokenKind.NotEq)) left = new BinOp(BinOpKind.NotEq, left, ParseComparison());
                else break;
            }
            return left;
        }

        private Expr ParseComparison()
        {
            var left = ParseRange();
            while (true)
            {
                if (Match(TokenKind.Less)) left = new BinOp(BinOpKind.Less, left, ParseRange());
                else if (Match(TokenKind.LessEq)) left = new BinOp(BinOpKind.LessEq, left, ParseRange());
                else if (Match(TokenKind.Greater)) left = new BinOp(BinOpKind.Greater, left, ParseRange());
                else if (Match(TokenKind.GreaterEq)) left = new BinOp(BinOpKind.GreaterEq, left, ParseRange());
                else break;
            }
            return left;
        }

        private Expr ParseRange()
        {
            var left = ParseAddSub();
            if (Match(TokenKind.DotDot))
            {
                var right = ParseAddSub();
                return new RangeLit(left, right);
            }
            return left;
        }

        private Expr ParseAddSub()
        {
            var left = ParseMulDiv();
            while (true)
            {
                if (Match(TokenKind.Plus)) left = new BinOp(BinOpKind.Add, left, ParseMulDiv());
                else if (Match(TokenKind.Minus)) left = new BinOp(BinOpKind.Sub, left, ParseMulDiv());
                else break;
            }
            return left;
        }

        private Expr ParseMulDiv()
        {
            var left = ParseUnary();
            while (true)
            {
                if (Match(TokenKind.Star)) left = new BinOp(BinOpKind.Mul, left, ParseUnary());
                else if (Match(TokenKind.Slash)) left = new BinOp(BinOpKind.Div, left, ParseUnary());
                else break;
            }
            return left;
        }

        private Expr ParseUnary()
        {
            if (Match(TokenKind.Minus)) return new UnaryOp(UnaryOpKind.Neg, ParseUnary());
            return ParsePostfix();
        }

        private Expr ParsePostfix()
        {
            var expr = ParsePrimary();
            while (true)
            {
                if (Match(TokenKind.Dot))
                {
                    var member = Expect(TokenKind.Identifier, "identifier after '.'").Text;
                    expr = new MemberAccess(expr, member);
                }
                else if (Match(TokenKind.LBracket))
                {
                    SkipNewlines();
                    var index = ParseExpr();
                    SkipNewlines();
                    Expect(TokenKind.RBracket, "']' to close indexer");
                    expr = new IndexAccess(expr, index);
                }
                else if (Match(TokenKind.LParen))
                {
                    var args = ParseCallArgs();
                    expr = new Call(expr, args);
                }
                else if (Check(TokenKind.KwSwitch))
                {
                    Advance();
                    expr = ParseSwitchBody(expr);
                }
                else
                {
                    break;
                }
            }
            return expr;
        }

        private ImmutableArray<Expr> ParseCallArgs()
        {
            SkipNewlines();
            var args = ImmutableArray.CreateBuilder<Expr>();
            if (!Check(TokenKind.RParen))
            {
                args.Add(ParseExpr());
                while (true)
                {
                    SkipNewlines();
                    if (!Match(TokenKind.Comma)) break;
                    SkipNewlines();
                    if (Check(TokenKind.RParen)) break;
                    args.Add(ParseExpr());
                }
            }
            SkipNewlines();
            Expect(TokenKind.RParen, "')' to close argument list");
            return args.ToImmutable();
        }

        private Expr ParseSwitchBody(Expr scrutinee)
        {
            Expect(TokenKind.LBrace, "'{' to open switch body");
            SkipNewlines();
            var arms = ImmutableArray.CreateBuilder<SwitchArm>();
            while (!Check(TokenKind.RBrace) && !IsAtEnd)
            {
                arms.Add(ParseSwitchArm());
                SkipSeparators();
            }
            Expect(TokenKind.RBrace, "'}' to close switch body");
            return new Switch(scrutinee, arms.ToImmutable());
        }

        private SwitchArm ParseSwitchArm()
        {
            var pattern = ParsePattern();
            Expect(TokenKind.FatArrow, "'=>' between switch pattern and body");
            SkipNewlines();
            var body = ParseExpr();
            return new SwitchArm(pattern, body);
        }

        private Pattern ParsePattern()
        {
            var t = Current;
            switch (t.Kind)
            {
                case TokenKind.IntLiteral:
                    Advance();
                    return new LitPattern(new NInt(long.Parse(t.Text, System.Globalization.CultureInfo.InvariantCulture)));
                case TokenKind.FloatLiteral:
                    Advance();
                    return new LitPattern(new NFloat(double.Parse(t.Text, System.Globalization.CultureInfo.InvariantCulture)));
                case TokenKind.StringLiteral:
                    Advance();
                    return new LitPattern(new NString(t.Text));
                case TokenKind.KwTrue:
                    Advance();
                    return new LitPattern(new NBool(true));
                case TokenKind.KwFalse:
                    Advance();
                    return new LitPattern(new NBool(false));
                case TokenKind.Minus:
                    Advance();
                    if (Check(TokenKind.IntLiteral))
                    {
                        var it = Advance();
                        return new LitPattern(new NInt(-long.Parse(it.Text, System.Globalization.CultureInfo.InvariantCulture)));
                    }
                    if (Check(TokenKind.FloatLiteral))
                    {
                        var ft = Advance();
                        return new LitPattern(new NFloat(-double.Parse(ft.Text, System.Globalization.CultureInfo.InvariantCulture)));
                    }
                    throw new ParserException("expected numeric literal after '-' in pattern",
                        Current.Line, Current.Column, IsAtEnd);
                case TokenKind.Identifier:
                    Advance();
                    if (t.Text == "_") return new WildcardPattern();
                    return new BindingPattern(t.Text);
                default:
                    throw new ParserException(
                        $"expected switch pattern but got {t.Kind}('{t.Text}')",
                        t.Line, t.Column, IsAtEnd);
            }
        }

        private void SkipSeparators()
        {
            while (Match(TokenKind.Comma) || Match(TokenKind.Newline)) { }
        }

        private Expr ParsePrimary()
        {
            SkipNewlines();
            var t = Current;
            switch (t.Kind)
            {
                case TokenKind.IntLiteral:
                    Advance();
                    return new Lit(new NInt(long.Parse(t.Text, System.Globalization.CultureInfo.InvariantCulture)));
                case TokenKind.FloatLiteral:
                    Advance();
                    return new Lit(new NFloat(double.Parse(t.Text, System.Globalization.CultureInfo.InvariantCulture)));
                case TokenKind.StringLiteral:
                    Advance();
                    return new Lit(new NString(t.Text));
                case TokenKind.KwTrue:
                    Advance();
                    return new Lit(new NBool(true));
                case TokenKind.KwFalse:
                    Advance();
                    return new Lit(new NBool(false));
                case TokenKind.Identifier:
                    if (Peek(1).Kind == TokenKind.FatArrow)
                    {
                        return ParseSingleParamLambda();
                    }
                    Advance();
                    return new Var(t.Text);
                case TokenKind.LParen:
                    return ParseLambdaOrParen();
                case TokenKind.LBracket:
                    return ParseListLiteral();
                case TokenKind.LBrace:
                    return ParseRecordLiteral();
                case TokenKind.InterpStart:
                    return ParseInterpolation();
                case TokenKind.KwPwsh:
                    return ParsePwshExpr();
                default:
                    throw new ParserException(
                        $"unexpected token {t.Kind}('{t.Text}')",
                        t.Line, t.Column, IsAtEnd);
            }
        }

        private Expr ParseSingleParamLambda()
        {
            var name = Expect(TokenKind.Identifier, "lambda parameter").Text;
            Expect(TokenKind.FatArrow, "'=>' in lambda");
            SkipNewlines();
            if (IsAtEnd)
                throw new ParserException("expected lambda body after '=>'", Current.Line, Current.Column, isIncomplete: true);
            var body = ParseExpr();
            return new Lambda(ImmutableArray.Create(name), body);
        }

        private Expr ParseLambdaOrParen()
        {
            int checkpoint = _pos;
            try
            {
                return TryParseParenLambda();
            }
            catch (ParserException ex) when (!ex.IsIncomplete)
            {
                _pos = checkpoint;
                return ParseParenExpression();
            }
        }

        private Expr TryParseParenLambda()
        {
            Expect(TokenKind.LParen, "'('");
            var parameters = ImmutableArray.CreateBuilder<string>();
            if (!Check(TokenKind.RParen))
            {
                if (!Check(TokenKind.Identifier))
                    throw new ParserException(
                        $"expected lambda parameter but got {Current.Kind}",
                        Current.Line, Current.Column, isIncomplete: false);
                parameters.Add(Advance().Text);
                while (Match(TokenKind.Comma))
                {
                    if (!Check(TokenKind.Identifier))
                        throw new ParserException(
                            $"expected lambda parameter after ',' but got {Current.Kind}",
                            Current.Line, Current.Column, isIncomplete: false);
                    parameters.Add(Advance().Text);
                }
            }
            Expect(TokenKind.RParen, "')' to close lambda parameter list");
            Expect(TokenKind.FatArrow, "'=>' in lambda");
            SkipNewlines();
            if (IsAtEnd)
                throw new ParserException("expected lambda body after '=>'", Current.Line, Current.Column, isIncomplete: true);
            var body = ParseExpr();
            return new Lambda(parameters.ToImmutable(), body);
        }

        private Expr ParseParenExpression()
        {
            Expect(TokenKind.LParen, "'('");
            SkipNewlines();
            var expr = ParseExpr();
            SkipNewlines();
            Expect(TokenKind.RParen, "')'");
            return expr;
        }

        private Expr ParseListLiteral()
        {
            Expect(TokenKind.LBracket, "'['");
            var items = ImmutableArray.CreateBuilder<Expr>();
            SkipSeparators();
            while (!Check(TokenKind.RBracket) && !IsAtEnd)
            {
                items.Add(ParseExpr());
                SkipSeparators();
            }
            Expect(TokenKind.RBracket, "']' to close list literal");
            return new ListLit(items.ToImmutable());
        }

        private Expr ParseRecordLiteral()
        {
            int braceLine = Current.Line, braceCol = Current.Column;
            Expect(TokenKind.LBrace, "'{'");
            var fields = ImmutableArray.CreateBuilder<RecordField>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            SkipSeparators();
            while (!Check(TokenKind.RBrace) && !IsAtEnd)
            {
                string key;
                int keyLine = Current.Line, keyCol = Current.Column;
                if (Check(TokenKind.Identifier))
                {
                    key = Advance().Text;
                }
                else if (Check(TokenKind.StringLiteral))
                {
                    key = Advance().Text;
                }
                else
                {
                    throw new ParserException(
                        $"expected record field name but got {Current.Kind}",
                        Current.Line, Current.Column, IsAtEnd);
                }
                if (!keys.Add(key))
                    throw new ParserException(
                        $"duplicate record field '{key}'",
                        keyLine, keyCol, isIncomplete: false);

                Expect(TokenKind.Colon, "':' between record field name and value");
                SkipNewlines();
                var value = ParseExpr();
                fields.Add(new RecordField(key, value));
                SkipSeparators();
            }
            if (IsAtEnd)
                throw new ParserException("'}' to close record literal", braceLine, braceCol, isIncomplete: true);
            Expect(TokenKind.RBrace, "'}' to close record literal");
            return new RecordLit(fields.ToImmutable());
        }

        private Expr ParseInterpolation()
        {
            Expect(TokenKind.InterpStart, "'$\"'");
            var segments = ImmutableArray.CreateBuilder<InterpSegment>();
            while (!Check(TokenKind.InterpEnd) && !IsAtEnd)
            {
                var t = Current;
                if (t.Kind == TokenKind.InterpText)
                {
                    Advance();
                    segments.Add(new InterpTextSegment(t.Text));
                }
                else if (t.Kind == TokenKind.InterpHole)
                {
                    Advance();
                    var holeExpr = ParseExpression(t.Text);
                    segments.Add(new InterpHoleSegment(holeExpr));
                }
                else
                {
                    throw new ParserException(
                        $"unexpected token {t.Kind} inside interpolated string",
                        t.Line, t.Column, IsAtEnd);
                }
            }
            Expect(TokenKind.InterpEnd, "closing '\"' of interpolated string");
            return new InterpExpr(segments.ToImmutable());
        }

        private Expr ParsePwshExpr()
        {
            Expect(TokenKind.KwPwsh, "'pwsh'");
            var block = Expect(TokenKind.PwshBlock, "'{' block after 'pwsh'");
            return new PwshExpr(block.Text);
        }
    }
}
