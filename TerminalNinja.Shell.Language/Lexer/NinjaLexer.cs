using System.Text;

namespace TerminalNinja.Shell.Lexer;

/// <summary>
/// Hand-rolled, single-pass lexer for NinjaShell. Produces a flat token stream
/// from a source string. Strings, interpolations, and <c>pwsh { ... }</c> blocks
/// that run off the end of input throw <see cref="LexerException"/> with
/// <see cref="LexerException.IsIncomplete"/> set, so the REPL can distinguish
/// "user is still typing" from "real syntax error".
/// </summary>
public static class NinjaLexer
{
    private static readonly Dictionary<string, TokenKind> Keywords = new(StringComparer.Ordinal)
    {
        ["let"] = TokenKind.KwLet,
        ["in"] = TokenKind.KwIn,
        ["switch"] = TokenKind.KwSwitch,
        ["pwsh"] = TokenKind.KwPwsh,
        ["true"] = TokenKind.KwTrue,
        ["false"] = TokenKind.KwFalse,
    };

    /// <summary>Tokenize a complete source string into a flat token list ending in <see cref="TokenKind.Eof"/>.</summary>
    public static IReadOnlyList<Token> Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var state = new LexState(source);
        var tokens = new List<Token>();

        while (!state.IsAtEnd)
        {
            SkipInsignificantWhitespaceAndComments(state);
            if (state.IsAtEnd) break;

            int startLine = state.Line;
            int startColumn = state.Column;
            char c = state.Peek();

            if (c == '\r' || c == '\n')
            {
                state.ConsumeLineBreak();
                tokens.Add(new Token(TokenKind.Newline, "\n", startLine, startColumn));
            }
            else if (char.IsDigit(c))
            {
                tokens.Add(ReadNumber(state, startLine, startColumn));
            }
            else if (c == '"')
            {
                tokens.Add(ReadPlainString(state, startLine, startColumn));
            }
            else if (c == '$' && state.PeekAt(1) == '"')
            {
                ReadInterpolatedString(state, tokens, startLine, startColumn);
            }
            else if (c == '_' || char.IsLetter(c))
            {
                var token = ReadIdentifierOrKeyword(state, startLine, startColumn);
                tokens.Add(token);

                if (token.Kind == TokenKind.KwPwsh)
                {
                    SkipInlineWhitespace(state);
                    if (!state.IsAtEnd && state.Peek() == '{')
                    {
                        tokens.Add(ReadPwshBlock(state));
                    }
                }
            }
            else
            {
                tokens.Add(ReadPunctuationOrOperator(state, startLine, startColumn));
            }
        }

        tokens.Add(new Token(TokenKind.Eof, string.Empty, state.Line, state.Column));
        return tokens;
    }

    private static void SkipInsignificantWhitespaceAndComments(LexState s)
    {
        while (!s.IsAtEnd)
        {
            char c = s.Peek();
            if (c == ' ' || c == '\t')
            {
                s.Advance();
            }
            else if (c == '/' && s.PeekAt(1) == '/')
            {
                while (!s.IsAtEnd && s.Peek() != '\n' && s.Peek() != '\r') s.Advance();
            }
            else if (c == '/' && s.PeekAt(1) == '*')
            {
                s.Advance();
                s.Advance();
                while (!s.IsAtEnd && !(s.Peek() == '*' && s.PeekAt(1) == '/'))
                {
                    if (s.Peek() == '\n' || s.Peek() == '\r') s.ConsumeLineBreak();
                    else s.Advance();
                }
                if (s.IsAtEnd) throw new LexerException("unterminated block comment", s.Line, s.Column, isIncomplete: true);
                s.Advance();
                s.Advance();
            }
            else
            {
                break;
            }
        }
    }

    private static void SkipInlineWhitespace(LexState s)
    {
        while (!s.IsAtEnd && (s.Peek() == ' ' || s.Peek() == '\t')) s.Advance();
    }

    private static Token ReadNumber(LexState s, int line, int col)
    {
        int start = s.Position;
        while (!s.IsAtEnd && char.IsDigit(s.Peek())) s.Advance();

        bool isFloat = false;
        if (!s.IsAtEnd && s.Peek() == '.' && s.PeekAt(1) != '.' && char.IsDigit(s.PeekAt(1)))
        {
            isFloat = true;
            s.Advance();
            while (!s.IsAtEnd && char.IsDigit(s.Peek())) s.Advance();
        }

        string text = s.Substring(start);
        return new Token(isFloat ? TokenKind.FloatLiteral : TokenKind.IntLiteral, text, line, col);
    }

    private static Token ReadPlainString(LexState s, int line, int col)
    {
        s.Advance();
        var sb = new StringBuilder();
        while (true)
        {
            if (s.IsAtEnd)
                throw new LexerException("unterminated string literal", line, col, isIncomplete: true);

            char c = s.Peek();
            if (c == '"')
            {
                s.Advance();
                return new Token(TokenKind.StringLiteral, sb.ToString(), line, col);
            }
            if (c == '\\')
            {
                s.Advance();
                if (s.IsAtEnd)
                    throw new LexerException("unterminated escape sequence", s.Line, s.Column, isIncomplete: true);
                AppendEscape(s, sb);
            }
            else if (c == '\n' || c == '\r')
            {
                throw new LexerException("newline in string literal", s.Line, s.Column, isIncomplete: false);
            }
            else
            {
                sb.Append(c);
                s.Advance();
            }
        }
    }

    private static void AppendEscape(LexState s, StringBuilder sb)
    {
        char esc = s.Peek();
        s.Advance();
        switch (esc)
        {
            case '"': sb.Append('"'); break;
            case '\\': sb.Append('\\'); break;
            case 'n': sb.Append('\n'); break;
            case 'r': sb.Append('\r'); break;
            case 't': sb.Append('\t'); break;
            case '0': sb.Append('\0'); break;
            case '{': sb.Append('{'); break;
            case '}': sb.Append('}'); break;
            case 'u':
                if (s.Position + 4 > s.Source.Length)
                    throw new LexerException("incomplete \\uXXXX escape", s.Line, s.Column, isIncomplete: true);
                string hex = s.Source.Substring(s.Position, 4);
                if (!ushort.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var code))
                    throw new LexerException($"invalid \\u escape: \\u{hex}", s.Line, s.Column, isIncomplete: false);
                sb.Append((char)code);
                for (int i = 0; i < 4; i++) s.Advance();
                break;
            default:
                throw new LexerException($"unknown escape: \\{esc}", s.Line, s.Column, isIncomplete: false);
        }
    }

    private static void ReadInterpolatedString(LexState s, List<Token> tokens, int line, int col)
    {
        s.Advance();
        s.Advance();
        tokens.Add(new Token(TokenKind.InterpStart, "$\"", line, col));

        var textStart = (line: s.Line, col: s.Column);
        var textBuf = new StringBuilder();

        while (true)
        {
            if (s.IsAtEnd)
                throw new LexerException("unterminated interpolated string", line, col, isIncomplete: true);

            char c = s.Peek();
            if (c == '"')
            {
                if (textBuf.Length > 0)
                    tokens.Add(new Token(TokenKind.InterpText, textBuf.ToString(), textStart.line, textStart.col));
                int endLine = s.Line, endCol = s.Column;
                s.Advance();
                tokens.Add(new Token(TokenKind.InterpEnd, "\"", endLine, endCol));
                return;
            }
            if (c == '\\')
            {
                s.Advance();
                if (s.IsAtEnd)
                    throw new LexerException("unterminated escape sequence", s.Line, s.Column, isIncomplete: true);
                AppendEscape(s, textBuf);
            }
            else if (c == '{' && s.PeekAt(1) == '{')
            {
                textBuf.Append('{');
                s.Advance();
                s.Advance();
            }
            else if (c == '}' && s.PeekAt(1) == '}')
            {
                textBuf.Append('}');
                s.Advance();
                s.Advance();
            }
            else if (c == '{')
            {
                if (textBuf.Length > 0)
                {
                    tokens.Add(new Token(TokenKind.InterpText, textBuf.ToString(), textStart.line, textStart.col));
                    textBuf.Clear();
                }
                int holeLine = s.Line, holeCol = s.Column;
                s.Advance();
                string holeBody = ReadInterpHoleBody(s);
                tokens.Add(new Token(TokenKind.InterpHole, holeBody, holeLine, holeCol));
                textStart = (s.Line, s.Column);
            }
            else if (c == '\n' || c == '\r')
            {
                throw new LexerException("newline in string literal", s.Line, s.Column, isIncomplete: false);
            }
            else
            {
                textBuf.Append(c);
                s.Advance();
            }
        }
    }

    private static string ReadInterpHoleBody(LexState s)
    {
        var sb = new StringBuilder();
        int depth = 1;
        while (depth > 0)
        {
            if (s.IsAtEnd)
                throw new LexerException("unterminated interpolation hole", s.Line, s.Column, isIncomplete: true);

            char c = s.Peek();
            if (c == '{')
            {
                depth++;
                sb.Append(c);
                s.Advance();
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    s.Advance();
                    return sb.ToString();
                }
                sb.Append(c);
                s.Advance();
            }
            else if (c == '"')
            {
                sb.Append(c);
                s.Advance();
                while (!s.IsAtEnd)
                {
                    char sc = s.Peek();
                    if (sc == '\\')
                    {
                        sb.Append(sc);
                        s.Advance();
                        if (s.IsAtEnd) throw new LexerException("unterminated escape in hole", s.Line, s.Column, isIncomplete: true);
                        sb.Append(s.Peek());
                        s.Advance();
                    }
                    else if (sc == '"')
                    {
                        sb.Append(sc);
                        s.Advance();
                        break;
                    }
                    else
                    {
                        sb.Append(sc);
                        s.Advance();
                    }
                }
            }
            else
            {
                sb.Append(c);
                s.Advance();
            }
        }
        return sb.ToString();
    }

    private static Token ReadPwshBlock(LexState s)
    {
        int line = s.Line, col = s.Column;
        s.Advance();
        var sb = new StringBuilder();
        int depth = 1;

        while (depth > 0)
        {
            if (s.IsAtEnd)
                throw new LexerException("unterminated pwsh block", line, col, isIncomplete: true);

            char c = s.Peek();

            // Here-strings — must check before normal quote handling because the `@` is the marker.
            if (c == '@' && (s.PeekAt(1) == '\'' || s.PeekAt(1) == '"'))
            {
                char quote = s.PeekAt(1);
                sb.Append(c);
                sb.Append(quote);
                s.Advance();
                s.Advance();
                ConsumePwshHereString(s, sb, quote);
                continue;
            }

            switch (c)
            {
                case '{':
                    depth++;
                    sb.Append(c);
                    s.Advance();
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        s.Advance();
                        return new Token(TokenKind.PwshBlock, sb.ToString(), line, col);
                    }
                    sb.Append(c);
                    s.Advance();
                    break;
                case '\'':
                    sb.Append(c);
                    s.Advance();
                    while (!s.IsAtEnd && s.Peek() != '\'')
                    {
                        char inner = s.Peek();
                        if (inner == '\r' || inner == '\n')
                        {
                            sb.Append(inner);
                            if (inner == '\r' && s.PeekAt(1) == '\n') sb.Append('\n');
                            s.ConsumeLineBreak();
                        }
                        else
                        {
                            sb.Append(inner);
                            s.Advance();
                        }
                    }
                    if (s.IsAtEnd) throw new LexerException("unterminated single-quoted string in pwsh block", line, col, isIncomplete: true);
                    sb.Append('\'');
                    s.Advance();
                    break;
                case '"':
                    sb.Append(c);
                    s.Advance();
                    ConsumePwshDoubleQuotedString(s, sb);
                    break;
                case '#':
                    if (s.PeekAt(-1) == '<')
                    {
                        // Already handled below — defensive.
                        sb.Append(c);
                        s.Advance();
                    }
                    else
                    {
                        sb.Append(c);
                        s.Advance();
                        while (!s.IsAtEnd && s.Peek() != '\n' && s.Peek() != '\r')
                        {
                            sb.Append(s.Peek());
                            s.Advance();
                        }
                    }
                    break;
                case '<':
                    if (s.PeekAt(1) == '#')
                    {
                        sb.Append(c);
                        sb.Append('#');
                        s.Advance();
                        s.Advance();
                        while (!s.IsAtEnd && !(s.Peek() == '#' && s.PeekAt(1) == '>'))
                        {
                            if (s.Peek() == '\n' || s.Peek() == '\r')
                            {
                                sb.Append(s.Peek());
                                s.ConsumeLineBreak();
                            }
                            else
                            {
                                sb.Append(s.Peek());
                                s.Advance();
                            }
                        }
                        if (s.IsAtEnd) throw new LexerException("unterminated block comment in pwsh block", line, col, isIncomplete: true);
                        sb.Append('#');
                        sb.Append('>');
                        s.Advance();
                        s.Advance();
                    }
                    else
                    {
                        sb.Append(c);
                        s.Advance();
                    }
                    break;
                case '\n':
                case '\r':
                    sb.Append(c);
                    s.ConsumeLineBreak();
                    break;
                default:
                    sb.Append(c);
                    s.Advance();
                    break;
            }
        }

        return new Token(TokenKind.PwshBlock, sb.ToString(), line, col);
    }

    private static void ConsumePwshDoubleQuotedString(LexState s, StringBuilder sb)
    {
        while (!s.IsAtEnd)
        {
            char c = s.Peek();
            if (c == '`')
            {
                sb.Append(c);
                s.Advance();
                if (s.IsAtEnd) throw new LexerException("unterminated backtick escape in pwsh block", s.Line, s.Column, isIncomplete: true);
                sb.Append(s.Peek());
                s.Advance();
            }
            else if (c == '"')
            {
                sb.Append(c);
                s.Advance();
                return;
            }
            else if (c == '\n' || c == '\r')
            {
                sb.Append(c);
                s.ConsumeLineBreak();
            }
            else
            {
                sb.Append(c);
                s.Advance();
            }
        }
        throw new LexerException("unterminated double-quoted string in pwsh block", s.Line, s.Column, isIncomplete: true);
    }

    private static void ConsumePwshHereString(LexState s, StringBuilder sb, char quote)
    {
        // Here-strings end with a newline followed by `'@` or `"@` at the start of a line.
        while (!s.IsAtEnd)
        {
            char c = s.Peek();
            if ((c == '\n' || c == '\r')
                && s.PeekAt(c == '\r' && s.PeekAt(1) == '\n' ? 2 : 1) == quote
                && s.PeekAt(c == '\r' && s.PeekAt(1) == '\n' ? 3 : 2) == '@')
            {
                sb.Append(c);
                s.ConsumeLineBreak();
                sb.Append(quote);
                sb.Append('@');
                s.Advance();
                s.Advance();
                return;
            }
            if (c == '\n' || c == '\r')
            {
                sb.Append(c);
                s.ConsumeLineBreak();
            }
            else
            {
                sb.Append(c);
                s.Advance();
            }
        }
        throw new LexerException($"unterminated here-string ({quote}) in pwsh block", s.Line, s.Column, isIncomplete: true);
    }

    private static Token ReadIdentifierOrKeyword(LexState s, int line, int col)
    {
        int start = s.Position;
        while (!s.IsAtEnd)
        {
            char c = s.Peek();
            if (c == '_' || char.IsLetterOrDigit(c)) s.Advance();
            else break;
        }
        string text = s.Substring(start);
        if (Keywords.TryGetValue(text, out var kw))
            return new Token(kw, text, line, col);
        return new Token(TokenKind.Identifier, text, line, col);
    }

    private static Token ReadPunctuationOrOperator(LexState s, int line, int col)
    {
        char c = s.Peek();
        char n = s.PeekAt(1);

        switch (c)
        {
            case '(': s.Advance(); return new Token(TokenKind.LParen, "(", line, col);
            case ')': s.Advance(); return new Token(TokenKind.RParen, ")", line, col);
            case '[': s.Advance(); return new Token(TokenKind.LBracket, "[", line, col);
            case ']': s.Advance(); return new Token(TokenKind.RBracket, "]", line, col);
            case '{': s.Advance(); return new Token(TokenKind.LBrace, "{", line, col);
            case '}': s.Advance(); return new Token(TokenKind.RBrace, "}", line, col);
            case ',': s.Advance(); return new Token(TokenKind.Comma, ",", line, col);
            case ':': s.Advance(); return new Token(TokenKind.Colon, ":", line, col);
            case '|':
                if (n == '|') { s.Advance(); s.Advance(); return new Token(TokenKind.OrOr, "||", line, col); }
                s.Advance(); return new Token(TokenKind.Pipe, "|", line, col);
            case '&':
                if (n == '&') { s.Advance(); s.Advance(); return new Token(TokenKind.AndAnd, "&&", line, col); }
                break;
            case '=':
                if (n == '>') { s.Advance(); s.Advance(); return new Token(TokenKind.FatArrow, "=>", line, col); }
                if (n == '=') { s.Advance(); s.Advance(); return new Token(TokenKind.EqEq, "==", line, col); }
                s.Advance(); return new Token(TokenKind.Assign, "=", line, col);
            case '!':
                if (n == '=') { s.Advance(); s.Advance(); return new Token(TokenKind.NotEq, "!=", line, col); }
                break;
            case '<':
                if (n == '=') { s.Advance(); s.Advance(); return new Token(TokenKind.LessEq, "<=", line, col); }
                s.Advance(); return new Token(TokenKind.Less, "<", line, col);
            case '>':
                if (n == '=') { s.Advance(); s.Advance(); return new Token(TokenKind.GreaterEq, ">=", line, col); }
                s.Advance(); return new Token(TokenKind.Greater, ">", line, col);
            case '+': s.Advance(); return new Token(TokenKind.Plus, "+", line, col);
            case '-': s.Advance(); return new Token(TokenKind.Minus, "-", line, col);
            case '*': s.Advance(); return new Token(TokenKind.Star, "*", line, col);
            case '/': s.Advance(); return new Token(TokenKind.Slash, "/", line, col);
            case '.':
                if (n == '.') { s.Advance(); s.Advance(); return new Token(TokenKind.DotDot, "..", line, col); }
                s.Advance(); return new Token(TokenKind.Dot, ".", line, col);
        }

        throw new LexerException($"unexpected character '{c}'", line, col, isIncomplete: false);
    }

    private sealed class LexState
    {
        public string Source { get; }
        public int Position;
        public int Line = 1;
        public int Column = 1;

        public LexState(string source) => Source = source;

        public bool IsAtEnd => Position >= Source.Length;

        public char Peek() => Source[Position];

        public char PeekAt(int offset)
        {
            int idx = Position + offset;
            return (idx >= 0 && idx < Source.Length) ? Source[idx] : '\0';
        }

        public void Advance()
        {
            char c = Source[Position];
            Position++;
            if (c == '\n') { Line++; Column = 1; }
            else if (c == '\r') { /* handled by ConsumeLineBreak */ Column++; }
            else Column++;
        }

        public void ConsumeLineBreak()
        {
            if (Position < Source.Length && Source[Position] == '\r')
            {
                Position++;
                if (Position < Source.Length && Source[Position] == '\n') Position++;
            }
            else if (Position < Source.Length && Source[Position] == '\n')
            {
                Position++;
            }
            Line++;
            Column = 1;
        }

        public string Substring(int start) => Source.Substring(start, Position - start);
    }
}
