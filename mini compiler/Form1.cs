using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace mini_compiler 
{
    // ══════════════════════════════════════════════════════════════════
    //  TOKEN TYPES  —  full C-like language
    // ══════════════════════════════════════════════════════════════════
    public enum TokenType
    {
        // Literals
        INT_LITERAL, FLOAT_LITERAL, STRING_LITERAL, CHAR_LITERAL, BOOL_LITERAL,
        // Keywords
        KW_INT, KW_FLOAT, KW_DOUBLE, KW_CHAR, KW_BOOL, KW_STRING,
        KW_IF, KW_ELSE, KW_WHILE, KW_FOR, KW_DO, KW_RETURN,
        KW_VOID, KW_BREAK, KW_CONTINUE, KW_TRUE, KW_FALSE,
        // Identifiers
        IDENTIFIER,
        // Arithmetic
        PLUS, MINUS, STAR, SLASH, PERCENT,
        // Relational
        EQ_EQ, NEQ, LT, GT, LTE, GTE,
        // Logical
        AND, OR, NOT,
        // Bitwise
        BIT_AND, BIT_OR, BIT_XOR, BIT_NOT, LSHIFT, RSHIFT,
        // Assignment
        ASSIGN, PLUS_ASSIGN, MINUS_ASSIGN, STAR_ASSIGN, SLASH_ASSIGN,
        // Inc/Dec
        INC, DEC,
        // Punctuation
        LPAREN, RPAREN, LBRACE, RBRACE, LBRACKET, RBRACKET,
        SEMICOLON, COMMA, DOT, COLON,
        // Special
        EOF, ERROR, COMMENT, NEWLINE
    }

    // ══════════════════════════════════════════════════════════════════
    //  TOKEN
    // ══════════════════════════════════════════════════════════════════
    public class Token
    {
        public TokenType Type;
        public string Lexeme;
        public int Line;
        public int Column;
        public Token(TokenType t, string l, int line, int col)
        { Type = t; Lexeme = l; Line = line; Column = col; }
        public override string ToString() => $"[{Line}:{Column}] {Type,-20} \"{Lexeme}\"";
    }

    // ══════════════════════════════════════════════════════════════════
    //  LEXER  —  hand-written DFA
    // ══════════════════════════════════════════════════════════════════
    public class Lexer
    {
        private readonly string _src;
        private int _pos, _line = 1, _col = 1;
        public List<string> Errors = new List<string>();

        static readonly Dictionary<string, TokenType> Keywords = new Dictionary<string, TokenType>
{
    {"int", TokenType.KW_INT},{"float", TokenType.KW_FLOAT},{"double", TokenType.KW_DOUBLE},
    {"char", TokenType.KW_CHAR},{"bool", TokenType.KW_BOOL},{"string", TokenType.KW_STRING},
    {"if", TokenType.KW_IF},{"else", TokenType.KW_ELSE},{"while", TokenType.KW_WHILE},
    {"for", TokenType.KW_FOR},{"do", TokenType.KW_DO},{"return", TokenType.KW_RETURN},
    {"void", TokenType.KW_VOID},{"break", TokenType.KW_BREAK},{"continue", TokenType.KW_CONTINUE},
    {"true", TokenType.KW_TRUE},{"false", TokenType.KW_FALSE}
};

        public Lexer(string src) { _src = src; }

        char Cur => _pos < _src.Length ? _src[_pos] : '\0';
        char Peek(int off = 1) => (_pos + off) < _src.Length ? _src[_pos + off] : '\0';

        void Advance() { if (_pos < _src.Length) { if (_src[_pos] == '\n') { _line++; _col = 1; } else _col++; _pos++; } }

        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();
            while (_pos < _src.Length)
            {
                SkipWhitespace();
                if (_pos >= _src.Length) break;

                int sl = _line, sc = _col;
                char c = Cur;

                // Comments
                if (c == '/' && Peek() == '/')
                { SkipLineComment(); continue; }
                if (c == '/' && Peek() == '*')
                { SkipBlockComment(); continue; }

                // Numbers
                if (char.IsDigit(c) || (c == '.' && char.IsDigit(Peek())))
                { tokens.Add(ReadNumber(sl, sc)); continue; }

                // Strings
                if (c == '"') { tokens.Add(ReadString(sl, sc)); continue; }
                if (c == '\'') { tokens.Add(ReadChar(sl, sc)); continue; }

                // Identifiers / keywords
                if (char.IsLetter(c) || c == '_') { tokens.Add(ReadIdent(sl, sc)); continue; }

                // Operators / punctuation
                var opTok = ReadOperator(sl, sc);
                if (opTok != null) { tokens.Add(opTok); continue; }

                // Error
                Errors.Add($"[{sl}:{sc}] Unexpected character: '{c}'");
                tokens.Add(new Token(TokenType.ERROR, c.ToString(), sl, sc));
                Advance();
            }
            tokens.Add(new Token(TokenType.EOF, "", _line, _col));
            return tokens;
        }

        void SkipWhitespace() { while (_pos < _src.Length && char.IsWhiteSpace(Cur)) Advance(); }
        void SkipLineComment() { while (_pos < _src.Length && Cur != '\n') Advance(); }
        void SkipBlockComment()
        {
            Advance(); Advance(); // skip /*
            while (_pos < _src.Length)
            {
                if (Cur == '*' && Peek() == '/') { Advance(); Advance(); return; }
                Advance();
            }
            Errors.Add($"Unterminated block comment");
        }

        Token ReadNumber(int sl, int sc)
        {
            var sb = new StringBuilder();
            bool isFloat = false;
            while (_pos < _src.Length && (char.IsDigit(Cur) || Cur == '.'))
            {
                if (Cur == '.') { if (isFloat) break; isFloat = true; }
                sb.Append(Cur); Advance();
            }
            // suffix f/F for float
            if (_pos < _src.Length && (Cur == 'f' || Cur == 'F')) { sb.Append(Cur); Advance(); isFloat = true; }
            return new Token(isFloat ? TokenType.FLOAT_LITERAL : TokenType.INT_LITERAL, sb.ToString(), sl, sc);
        }

        Token ReadString(int sl, int sc)
        {
            var sb = new StringBuilder("\""); Advance();
            while (_pos < _src.Length && Cur != '"' && Cur != '\n')
            {
                if (Cur == '\\') { sb.Append(Cur); Advance(); if (_pos < _src.Length) { sb.Append(Cur); Advance(); } }
                else { sb.Append(Cur); Advance(); }
            }
            if (Cur == '"') { sb.Append('"'); Advance(); }
            else Errors.Add($"[{sl}:{sc}] Unterminated string");
            return new Token(TokenType.STRING_LITERAL, sb.ToString(), sl, sc);
        }

        Token ReadChar(int sl, int sc)
        {
            var sb = new StringBuilder("'"); Advance();
            if (_pos < _src.Length && Cur == '\\') { sb.Append(Cur); Advance(); if (_pos < _src.Length) { sb.Append(Cur); Advance(); } }
            else if (_pos < _src.Length) { sb.Append(Cur); Advance(); }
            if (_pos < _src.Length && Cur == '\'') { sb.Append('\''); Advance(); }
            else Errors.Add($"[{sl}:{sc}] Unterminated char literal");
            return new Token(TokenType.CHAR_LITERAL, sb.ToString(), sl, sc);
        }

        Token ReadIdent(int sl, int sc)
        {
            var sb = new StringBuilder();
            while (_pos < _src.Length && (char.IsLetterOrDigit(Cur) || Cur == '_')) { sb.Append(Cur); Advance(); }
            string s = sb.ToString();
            TokenType t = Keywords.ContainsKey(s) ? Keywords[s] : TokenType.IDENTIFIER;
            return new Token(t, s, sl, sc);
        }

        Token ReadOperator(int sl, int sc)
        {
            char c = Cur, n = Peek();
            // Two-char operators
            if (c == '=' && n == '=') { Advance(); Advance(); return new Token(TokenType.EQ_EQ, "==", sl, sc); }
            if (c == '!' && n == '=') { Advance(); Advance(); return new Token(TokenType.NEQ, "!=", sl, sc); }
            if (c == '<' && n == '=') { Advance(); Advance(); return new Token(TokenType.LTE, "<=", sl, sc); }
            if (c == '>' && n == '=') { Advance(); Advance(); return new Token(TokenType.GTE, ">=", sl, sc); }
            if (c == '&' && n == '&') { Advance(); Advance(); return new Token(TokenType.AND, "&&", sl, sc); }
            if (c == '|' && n == '|') { Advance(); Advance(); return new Token(TokenType.OR, "||", sl, sc); }
            if (c == '<' && n == '<') { Advance(); Advance(); return new Token(TokenType.LSHIFT, "<<", sl, sc); }
            if (c == '>' && n == '>') { Advance(); Advance(); return new Token(TokenType.RSHIFT, ">>", sl, sc); }
            if (c == '+' && n == '+') { Advance(); Advance(); return new Token(TokenType.INC, "++", sl, sc); }
            if (c == '-' && n == '-') { Advance(); Advance(); return new Token(TokenType.DEC, "--", sl, sc); }
            if (c == '+' && n == '=') { Advance(); Advance(); return new Token(TokenType.PLUS_ASSIGN, "+=", sl, sc); }
            if (c == '-' && n == '=') { Advance(); Advance(); return new Token(TokenType.MINUS_ASSIGN, "-=", sl, sc); }
            if (c == '*' && n == '=') { Advance(); Advance(); return new Token(TokenType.STAR_ASSIGN, "*=", sl, sc); }
            if (c == '/' && n == '=') { Advance(); Advance(); return new Token(TokenType.SLASH_ASSIGN, "/=", sl, sc); }
            // Single-char
            switch (c)
            {
                case '+': Advance(); return new Token(TokenType.PLUS, "+", sl, sc);
                case '-': Advance(); return new Token(TokenType.MINUS, "-", sl, sc);
                case '*': Advance(); return new Token(TokenType.STAR, "*", sl, sc);
                case '/': Advance(); return new Token(TokenType.SLASH, "/", sl, sc);
                case '%': Advance(); return new Token(TokenType.PERCENT, "%", sl, sc);
                case '=': Advance(); return new Token(TokenType.ASSIGN, "=", sl, sc);
                case '<': Advance(); return new Token(TokenType.LT, "<", sl, sc);
                case '>': Advance(); return new Token(TokenType.GT, ">", sl, sc);
                case '!': Advance(); return new Token(TokenType.NOT, "!", sl, sc);
                case '&': Advance(); return new Token(TokenType.BIT_AND, "&", sl, sc);
                case '|': Advance(); return new Token(TokenType.BIT_OR, "|", sl, sc);
                case '^': Advance(); return new Token(TokenType.BIT_XOR, "^", sl, sc);
                case '~': Advance(); return new Token(TokenType.BIT_NOT, "~", sl, sc);
                case '(': Advance(); return new Token(TokenType.LPAREN, "(", sl, sc);
                case ')': Advance(); return new Token(TokenType.RPAREN, ")", sl, sc);
                case '{': Advance(); return new Token(TokenType.LBRACE, "{", sl, sc);
                case '}': Advance(); return new Token(TokenType.RBRACE, "}", sl, sc);
                case '[': Advance(); return new Token(TokenType.LBRACKET, "[", sl, sc);
                case ']': Advance(); return new Token(TokenType.RBRACKET, "]", sl, sc);
                case ';': Advance(); return new Token(TokenType.SEMICOLON, ";", sl, sc);
                case ',': Advance(); return new Token(TokenType.COMMA, ",", sl, sc);
                case '.': Advance(); return new Token(TokenType.DOT, ".", sl, sc);
                case ':': Advance(); return new Token(TokenType.COLON, ":", sl, sc);
            }
            return null;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  AST NODES
    // ══════════════════════════════════════════════════════════════════
    public abstract class AstNode { public int Line; }
    public class ProgramNode : AstNode { public List<AstNode> Statements = new List<AstNode>(); }
    public class DeclStmt : AstNode { public string DataType; public string Name; public AstNode Initializer; }
    public class AssignStmt : AstNode { public string Name; public AstNode Value; public string Op; }
    public class IfStmt : AstNode { public AstNode Cond; public AstNode Then; public AstNode Else; }
    public class WhileStmt : AstNode { public AstNode Cond; public AstNode Body; }
    public class ForStmt : AstNode { public AstNode Init; public AstNode Cond; public AstNode Update; public AstNode Body; }
    public class ReturnStmt : AstNode { public AstNode Value; }
    public class BlockStmt : AstNode { public List<AstNode> Stmts = new List<AstNode>(); }
    public class ExprStmt : AstNode { public AstNode Expr; }
    public class BinaryExpr : AstNode { public string Op; public AstNode Left, Right; }
    public class UnaryExpr : AstNode { public string Op; public AstNode Operand; }
    public class LiteralExpr : AstNode { public string Value; public string Kind; }
    public class IdentExpr : AstNode { public string Name; }
    public class FuncCallExpr : AstNode { public string Name; public List<AstNode> Args = new List<AstNode>(); }
    public class FuncDecl : AstNode { public string RetType; public string Name; public List<(string, string)> Params = new List<(string, string)>(); public BlockStmt Body; }
    public class BreakStmt : AstNode { }
    public class ContinueStmt : AstNode { }

    // ══════════════════════════════════════════════════════════════════
    //  PARSER  —  recursive descent, full precedence
    // ══════════════════════════════════════════════════════════════════
    public class Parser
    {
        private List<Token> _tokens;
        private int _pos;
        public List<string> Errors = new List<string>();

        static readonly HashSet<TokenType> TypeKeywords = new HashSet<TokenType>
        { TokenType.KW_INT, TokenType.KW_FLOAT, TokenType.KW_DOUBLE,
          TokenType.KW_CHAR, TokenType.KW_BOOL, TokenType.KW_STRING, TokenType.KW_VOID };

        public Parser(List<Token> tokens) { _tokens = tokens; }

        Token Cur => _pos < _tokens.Count ? _tokens[_pos] : _tokens[_tokens.Count - 1];
        Token Peek(int off = 1) => (_pos + off) < _tokens.Count ? _tokens[_pos + off] : _tokens[_tokens.Count - 1];

        Token Consume() { var t = Cur; _pos++; return t; }

        Token Expect(TokenType type, string msg = null)
        {
            if (Cur.Type == type) return Consume();
            string err = msg ?? $"Expected {type} but got '{Cur.Lexeme}' ({Cur.Type}) at [{Cur.Line}:{Cur.Column}]";
            Errors.Add(err);
            return Cur; // error recovery
        }

        bool Match(TokenType t) { if (Cur.Type == t) { Consume(); return true; } return false; }
        bool Check(TokenType t) => Cur.Type == t;

        public ProgramNode Parse()
        {
            var prog = new ProgramNode { Line = 1 };
            while (!Check(TokenType.EOF))
            {
                try { var stmt = ParseTopLevel(); if (stmt != null) prog.Statements.Add(stmt); }
                catch (Exception ex) { Errors.Add("Parse panic: " + ex.Message); Synchronize(); }
            }
            return prog;
        }

        void Synchronize()
        {
            while (!Check(TokenType.EOF) && !Check(TokenType.SEMICOLON) && !Check(TokenType.RBRACE))
                Consume();
            if (Check(TokenType.SEMICOLON)) Consume();
        }

        AstNode ParseTopLevel()
        {
            // Function declaration: type name ( ...
            if (TypeKeywords.Contains(Cur.Type) && Peek().Type == TokenType.IDENTIFIER && Peek(2).Type == TokenType.LPAREN)
                return ParseFuncDecl();
            return ParseStatement();
        }

        FuncDecl ParseFuncDecl()
        {
            var fd = new FuncDecl { Line = Cur.Line };
            fd.RetType = Consume().Lexeme;
            fd.Name = Expect(TokenType.IDENTIFIER).Lexeme;
            Expect(TokenType.LPAREN);
            while (!Check(TokenType.RPAREN) && !Check(TokenType.EOF))
            {
                string pt = Consume().Lexeme;
                string pn = Expect(TokenType.IDENTIFIER).Lexeme;
                fd.Params.Add((pt, pn));
                if (!Match(TokenType.COMMA)) break;
            }
            Expect(TokenType.RPAREN);
            fd.Body = ParseBlock();
            return fd;
        }

        BlockStmt ParseBlock()
        {
            var blk = new BlockStmt { Line = Cur.Line };
            Expect(TokenType.LBRACE);
            while (!Check(TokenType.RBRACE) && !Check(TokenType.EOF))
                blk.Stmts.Add(ParseStatement());
            Expect(TokenType.RBRACE);
            return blk;
        }

        AstNode ParseStatement()
        {
            if (Check(TokenType.LBRACE)) return ParseBlock();
            if (Check(TokenType.KW_IF)) return ParseIf();
            if (Check(TokenType.KW_WHILE)) return ParseWhile();
            if (Check(TokenType.KW_FOR)) return ParseFor();
            if (Check(TokenType.KW_RETURN)) return ParseReturn();
            if (Check(TokenType.KW_BREAK)) { var ln = Cur.Line; Consume(); Expect(TokenType.SEMICOLON); return new BreakStmt { Line = ln }; }
            if (Check(TokenType.KW_CONTINUE)) { var ln = Cur.Line; Consume(); Expect(TokenType.SEMICOLON); return new ContinueStmt { Line = ln }; }
            if (TypeKeywords.Contains(Cur.Type) && Peek().Type == TokenType.IDENTIFIER)
                return ParseDecl();
            var expr = ParseExpr();
            Expect(TokenType.SEMICOLON, $"Expected ';' after expression at line {Cur.Line}");
            return new ExprStmt { Expr = expr, Line = expr?.Line ?? 0 };
        }

        DeclStmt ParseDecl()
        {
            var ds = new DeclStmt { Line = Cur.Line };
            ds.DataType = Consume().Lexeme;
            ds.Name = Expect(TokenType.IDENTIFIER).Lexeme;
            if (Match(TokenType.ASSIGN)) ds.Initializer = ParseExpr();
            Expect(TokenType.SEMICOLON, $"Expected ';' after declaration of '{ds.Name}' at line {Cur.Line}");
            return ds;
        }

        IfStmt ParseIf()
        {
            var ifs = new IfStmt { Line = Cur.Line }; Consume();
            Expect(TokenType.LPAREN); ifs.Cond = ParseExpr(); Expect(TokenType.RPAREN);
            ifs.Then = ParseStatement();
            if (Match(TokenType.KW_ELSE)) ifs.Else = ParseStatement();
            return ifs;
        }

        WhileStmt ParseWhile()
        {
            var ws = new WhileStmt { Line = Cur.Line }; Consume();
            Expect(TokenType.LPAREN); ws.Cond = ParseExpr(); Expect(TokenType.RPAREN);
            ws.Body = ParseStatement();
            return ws;
        }

        ForStmt ParseFor()
        {
            var fs = new ForStmt { Line = Cur.Line }; Consume();
            Expect(TokenType.LPAREN);
            fs.Init = TypeKeywords.Contains(Cur.Type) ? ParseDecl() : (AstNode)(new ExprStmt { Expr = ParseExpr() });
            if (fs.Init is ExprStmt) Expect(TokenType.SEMICOLON);
            fs.Cond = ParseExpr(); Expect(TokenType.SEMICOLON);
            fs.Update = ParseExpr();
            Expect(TokenType.RPAREN);
            fs.Body = ParseStatement();
            return fs;
        }

        ReturnStmt ParseReturn()
        {
            var rs = new ReturnStmt { Line = Cur.Line }; Consume();
            if (!Check(TokenType.SEMICOLON)) rs.Value = ParseExpr();
            Expect(TokenType.SEMICOLON);
            return rs;
        }

        // Expression parsing — full precedence hierarchy
        AstNode ParseExpr() => ParseAssign();

        AstNode ParseAssign()
        {
            var left = ParseOr();
            if (Cur.Type == TokenType.ASSIGN || Cur.Type == TokenType.PLUS_ASSIGN ||
                Cur.Type == TokenType.MINUS_ASSIGN || Cur.Type == TokenType.STAR_ASSIGN ||
                Cur.Type == TokenType.SLASH_ASSIGN)
            {
                string op = Consume().Lexeme;
                var right = ParseAssign();
                if (left is IdentExpr ie)
                    return new AssignStmt { Name = ie.Name, Value = right, Op = op, Line = left.Line };
                return new BinaryExpr { Op = op, Left = left, Right = right, Line = left.Line };
            }
            return left;
        }

        AstNode ParseOr()
        {
            var left = ParseAnd();
            while (Check(TokenType.OR)) { var op = Consume().Lexeme; left = new BinaryExpr { Op = op, Left = left, Right = ParseAnd(), Line = left.Line }; }
            return left;
        }

        AstNode ParseAnd()
        {
            var left = ParseEq();
            while (Check(TokenType.AND)) { var op = Consume().Lexeme; left = new BinaryExpr { Op = op, Left = left, Right = ParseEq(), Line = left.Line }; }
            return left;
        }

        AstNode ParseEq()
        {
            var left = ParseRel();
            while (Check(TokenType.EQ_EQ) || Check(TokenType.NEQ)) { var op = Consume().Lexeme; left = new BinaryExpr { Op = op, Left = left, Right = ParseRel(), Line = left.Line }; }
            return left;
        }

        AstNode ParseRel()
        {
            var left = ParseAdd();
            while (Check(TokenType.LT) || Check(TokenType.GT) || Check(TokenType.LTE) || Check(TokenType.GTE))
            { var op = Consume().Lexeme; left = new BinaryExpr { Op = op, Left = left, Right = ParseAdd(), Line = left.Line }; }
            return left;
        }

        AstNode ParseAdd()
        {
            var left = ParseMul();
            while (Check(TokenType.PLUS) || Check(TokenType.MINUS))
            { var op = Consume().Lexeme; left = new BinaryExpr { Op = op, Left = left, Right = ParseMul(), Line = left.Line }; }
            return left;
        }

        AstNode ParseMul()
        {
            var left = ParseUnary();
            while (Check(TokenType.STAR) || Check(TokenType.SLASH) || Check(TokenType.PERCENT))
            { var op = Consume().Lexeme; left = new BinaryExpr { Op = op, Left = left, Right = ParseUnary(), Line = left.Line }; }
            return left;
        }

        AstNode ParseUnary()
        {
            if (Check(TokenType.NOT) || Check(TokenType.MINUS) || Check(TokenType.BIT_NOT) || Check(TokenType.INC) || Check(TokenType.DEC))
            { var op = Consume().Lexeme; return new UnaryExpr { Op = op, Operand = ParseUnary(), Line = Cur.Line }; }
            return ParsePostfix();
        }

        AstNode ParsePostfix()
        {
            var expr = ParsePrimary();
            while (Check(TokenType.INC) || Check(TokenType.DEC))
            { var op = Consume().Lexeme; expr = new UnaryExpr { Op = "post" + op, Operand = expr, Line = expr.Line }; }
            return expr;
        }

        AstNode ParsePrimary()
        {
            var ln = Cur.Line;
            if (Check(TokenType.INT_LITERAL)) return new LiteralExpr { Value = Consume().Lexeme, Kind = "int", Line = ln };
            if (Check(TokenType.FLOAT_LITERAL)) return new LiteralExpr { Value = Consume().Lexeme, Kind = "float", Line = ln };
            if (Check(TokenType.STRING_LITERAL)) return new LiteralExpr { Value = Consume().Lexeme, Kind = "string", Line = ln };
            if (Check(TokenType.CHAR_LITERAL)) return new LiteralExpr { Value = Consume().Lexeme, Kind = "char", Line = ln };
            if (Check(TokenType.KW_TRUE)) { Consume(); return new LiteralExpr { Value = "true", Kind = "bool", Line = ln }; }
            if (Check(TokenType.KW_FALSE)) { Consume(); return new LiteralExpr { Value = "false", Kind = "bool", Line = ln }; }
            if (Check(TokenType.IDENTIFIER))
            {
                string name = Consume().Lexeme;
                if (Check(TokenType.LPAREN))
                {
                    Consume();
                    var call = new FuncCallExpr { Name = name, Line = ln };
                    while (!Check(TokenType.RPAREN) && !Check(TokenType.EOF))
                    { call.Args.Add(ParseExpr()); if (!Match(TokenType.COMMA)) break; }
                    Expect(TokenType.RPAREN);
                    return call;
                }
                return new IdentExpr { Name = name, Line = ln };
            }
            if (Check(TokenType.LPAREN))
            { Consume(); var e = ParseExpr(); Expect(TokenType.RPAREN); return e; }

            Errors.Add($"Unexpected token '{Cur.Lexeme}' at [{Cur.Line}:{Cur.Column}]");
            return new LiteralExpr { Value = "0", Kind = "int", Line = ln };
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  SYMBOL TABLE ENTRY
    // ══════════════════════════════════════════════════════════════════
    public class SymbolEntry
    {
        public string Name, Type, Scope;
        public bool IsFunction;
        public int Line;
        public string Value;
        public SymbolEntry(string n, string t, string s, int ln, bool fn = false)
        { Name = n; Type = t; Scope = s; Line = ln; IsFunction = fn; }
    }

    // ══════════════════════════════════════════════════════════════════
    //  SEMANTIC ANALYSER
    // ══════════════════════════════════════════════════════════════════
    public class SemanticAnalyser
    {
        public List<SymbolEntry> Symbols = new List<SymbolEntry>();
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();

        private Stack<Dictionary<string, SymbolEntry>> _scopes = new Stack<Dictionary<string, SymbolEntry>>();
        private string _currentScope = "global";

        public void Analyse(ProgramNode prog)
        {
            PushScope("global");
            foreach (var stmt in prog.Statements) AnalyseNode(stmt);
            PopScope();
        }

        void PushScope(string name) { _currentScope = name; _scopes.Push(new Dictionary<string, SymbolEntry>()); }
        void PopScope() { _scopes.Pop(); _currentScope = _scopes.Count > 0 ? "inner" : "global"; }

        void Declare(string name, string type, int line, bool isFunc = false)
        {
            if (_scopes.Count > 0 && _scopes.Peek().ContainsKey(name))
            { Errors.Add($"[Line {line}] Redeclaration of '{name}' in scope '{_currentScope}'"); return; }
            var sym = new SymbolEntry(name, type, _currentScope, line, isFunc);
            _scopes.Peek()[name] = sym;
            Symbols.Add(sym);
        }

        SymbolEntry Lookup(string name)
        {
            foreach (var scope in _scopes) if (scope.ContainsKey(name)) return scope[name];
            return null;
        }

        string AnalyseNode(AstNode node)
        {
            if (node == null) return "void";
            switch (node)
            {
                case FuncDecl fd:
                    Declare(fd.Name, fd.RetType, fd.Line, true);
                    PushScope(fd.Name);
                    foreach (var p in fd.Params) Declare(p.Item2, p.Item1, fd.Line);
                    if (fd.Body != null) AnalyseNode(fd.Body);
                    PopScope();
                    return fd.RetType;

                case DeclStmt ds:
                    if (ds.Initializer != null)
                    {
                        string initType = AnalyseNode(ds.Initializer);
                        if (!TypesCompatible(ds.DataType, initType))
                            Warnings.Add($"[Line {ds.Line}] Type mismatch: assigning '{initType}' to '{ds.DataType}' variable '{ds.Name}'");
                    }
                    Declare(ds.Name, ds.DataType, ds.Line);
                    return ds.DataType;

                case AssignStmt ast:
                    var sym = Lookup(ast.Name);
                    if (sym == null) Errors.Add($"[Line {ast.Line}] Undeclared variable '{ast.Name}'");
                    var rType = AnalyseNode(ast.Value);
                    if (sym != null && !TypesCompatible(sym.Type, rType))
                        Warnings.Add($"[Line {ast.Line}] Possible type mismatch assigning '{rType}' to '{ast.Name}' ({sym.Type})");
                    return sym?.Type ?? "unknown";

                case BlockStmt blk:
                    PushScope("block");
                    foreach (var s in blk.Stmts) AnalyseNode(s);
                    PopScope();
                    return "void";

                case IfStmt ifs:
                    AnalyseNode(ifs.Cond); AnalyseNode(ifs.Then);
                    if (ifs.Else != null) AnalyseNode(ifs.Else);
                    return "void";

                case WhileStmt ws:
                    AnalyseNode(ws.Cond); AnalyseNode(ws.Body); return "void";

                case ForStmt frs:
                    PushScope("for");
                    AnalyseNode(frs.Init); AnalyseNode(frs.Cond);
                    AnalyseNode(frs.Update); AnalyseNode(frs.Body);
                    PopScope(); return "void";

                case ReturnStmt rs:
                    return AnalyseNode(rs.Value);

                case ExprStmt es:
                    return AnalyseNode(es.Expr);

                case BinaryExpr be:
                    string lt = AnalyseNode(be.Left), rt = AnalyseNode(be.Right);
                    if (be.Op == "==" || be.Op == "!=" || be.Op == "<" || be.Op == ">" || be.Op == "<=" || be.Op == ">=" || be.Op == "&&" || be.Op == "||")
                        return "bool";
                    return DominantType(lt, rt);

                case UnaryExpr ue:
                    return AnalyseNode(ue.Operand);

                case LiteralExpr le:
                    return le.Kind;

                case IdentExpr ide:
                    var s2 = Lookup(ide.Name);
                    if (s2 == null) Errors.Add($"[Line {ide.Line}] Undeclared identifier '{ide.Name}'");
                    return s2?.Type ?? "unknown";

                case FuncCallExpr fc:
                    var fs = Lookup(fc.Name);
                    if (fs == null) Warnings.Add($"[Line {fc.Line}] Calling undeclared function '{fc.Name}'");
                    foreach (var a in fc.Args) AnalyseNode(a);
                    return fs?.Type ?? "unknown";

                default: return "void";
            }
        }

        bool TypesCompatible(string a, string b)
        {
            if (a == b) return true;
            var numerics = new[] { "int", "float", "double" };
            if (numerics.Contains(a) && numerics.Contains(b)) return true;
            if (b == "unknown" || a == "unknown") return true;
            return false;
        }

        string DominantType(string a, string b)
        {
            if (a == "double" || b == "double") return "double";
            if (a == "float" || b == "float") return "float";
            return "int";
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  TAC / IR GENERATION
    // ══════════════════════════════════════════════════════════════════
    public class TACGenerator
    {
        public List<string> Instructions = new List<string>();
        private int _tempCount = 0;
        private int _labelCount = 0;
        private string NewTemp() => $"t{++_tempCount}";
        private string NewLabel() => $"L{++_labelCount}";

        public void Generate(ProgramNode prog)
        {
            foreach (var s in prog.Statements) GenStmt(s);
        }

        void GenStmt(AstNode node)
        {
            if (node == null) return;
            switch (node)
            {
                case FuncDecl fd:
                    Emit($"FUNC_BEGIN {fd.Name}");
                    foreach (var p in fd.Params) Emit($"PARAM {p.Item1} {p.Item2}");
                    GenStmt(fd.Body);
                    Emit($"FUNC_END {fd.Name}");
                    break;

                case DeclStmt ds:
                    Emit($"; decl {ds.DataType} {ds.Name}");
                    if (ds.Initializer != null)
                    {
                        string v = GenExpr(ds.Initializer);
                        Emit($"{ds.Name} = {v}");
                    }
                    break;

                case AssignStmt ast:
                    string val = GenExpr(ast.Value);
                    if (ast.Op == "=") Emit($"{ast.Name} = {val}");
                    else
                    {
                        string op = ast.Op.Replace("=", "");
                        string tmp = NewTemp();
                        Emit($"{tmp} = {ast.Name} {op} {val}");
                        Emit($"{ast.Name} = {tmp}");
                    }
                    break;

                case ExprStmt es:
                    GenExpr(es.Expr);
                    break;

                case BlockStmt blk:
                    foreach (var s in blk.Stmts) GenStmt(s);
                    break;

                case IfStmt ifs:
                    string cond = GenExpr(ifs.Cond);
                    string lElse = NewLabel(), lEnd = NewLabel();
                    Emit($"IF_FALSE {cond} GOTO {lElse}");
                    GenStmt(ifs.Then);
                    Emit($"GOTO {lEnd}");
                    Emit($"{lElse}:");
                    if (ifs.Else != null) GenStmt(ifs.Else);
                    Emit($"{lEnd}:");
                    break;

                case WhileStmt ws:
                    string lTop = NewLabel(), lExit = NewLabel();
                    Emit($"{lTop}:");
                    string wc = GenExpr(ws.Cond);
                    Emit($"IF_FALSE {wc} GOTO {lExit}");
                    GenStmt(ws.Body);
                    Emit($"GOTO {lTop}");
                    Emit($"{lExit}:");
                    break;

                case ForStmt fs:
                    string fTop = NewLabel(), fExit = NewLabel();
                    GenStmt(fs.Init);
                    Emit($"{fTop}:");
                    string fc = GenExpr(fs.Cond);
                    Emit($"IF_FALSE {fc} GOTO {fExit}");
                    GenStmt(fs.Body);
                    GenExpr(fs.Update);
                    Emit($"GOTO {fTop}");
                    Emit($"{fExit}:");
                    break;

                case ReturnStmt rs:
                    if (rs.Value != null) { string rv = GenExpr(rs.Value); Emit($"RETURN {rv}"); }
                    else Emit("RETURN");
                    break;

                case BreakStmt _: Emit("BREAK"); break;
                case ContinueStmt _: Emit("CONTINUE"); break;
            }
        }

        string GenExpr(AstNode node)
        {
            if (node == null) return "0";
            switch (node)
            {
                case LiteralExpr le: return le.Value;
                case IdentExpr ie: return ie.Name;

                case BinaryExpr be:
                    string l = GenExpr(be.Left), r = GenExpr(be.Right);
                    string t = NewTemp();
                    Emit($"{t} = {l} {be.Op} {r}");
                    return t;

                case UnaryExpr ue:
                    string operand = GenExpr(ue.Operand);
                    if (ue.Op == "post++" || ue.Op == "post--")
                    {
                        string tmp2 = NewTemp();
                        Emit($"{tmp2} = {operand}");
                        string realOp = ue.Op == "post++" ? "+" : "-";
                        string tmp3 = NewTemp();
                        Emit($"{tmp3} = {operand} {realOp} 1");
                        if (ue.Operand is IdentExpr ie2) Emit($"{ie2.Name} = {tmp3}");
                        return tmp2;
                    }
                    string ut = NewTemp();
                    Emit($"{ut} = {ue.Op} {operand}");
                    return ut;

                case FuncCallExpr fc:
                    foreach (var a in fc.Args) { string av = GenExpr(a); Emit($"ARG {av}"); }
                    string ft = NewTemp();
                    Emit($"{ft} = CALL {fc.Name} {fc.Args.Count}");
                    return ft;

                case AssignStmt ast:
                    string av2 = GenExpr(ast.Value);
                    Emit($"{ast.Name} = {av2}");
                    return ast.Name;

                default: return "0";
            }
        }

        void Emit(string instr) { Instructions.Add(instr); }
    }

    // ══════════════════════════════════════════════════════════════════
    //  OPTIMISER  —  constant folding, copy propagation, dead code elim
    // ══════════════════════════════════════════════════════════════════
    public class Optimiser
    {
        public List<string> Optimized = new List<string>();
        public List<string> Report = new List<string>();

        public void Optimise(List<string> input)
        {
            var pass1 = ConstantFolding(input);
            var pass2 = CopyPropagation(pass1);
            var pass3 = DeadCodeElim(pass2);
            Optimized = pass3;
        }

        List<string> ConstantFolding(List<string> lines)
        {
            var result = new List<string>();
            foreach (var line in lines)
            {
                var m = Regex.Match(line, @"^(\w+)\s*=\s*([\d.]+)\s*([\+\-\*\/\%])\s*([\d.]+)$");
                if (m.Success)
                {
                    double l = double.Parse(m.Groups[2].Value);
                    double r = double.Parse(m.Groups[4].Value);
                    string op = m.Groups[3].Value;
                    double res = op == "+" ? l + r : op == "-" ? l - r : op == "*" ? l * r : r != 0 ? l / r : 0;
                    string vs = res == Math.Floor(res) ? ((long)res).ToString() : res.ToString("F4").TrimEnd('0');
                    string folded = $"{m.Groups[1].Value} = {vs}";
                    result.Add(folded);
                    Report.Add($"Constant Fold: '{line}' → '{folded}'");
                }
                else result.Add(line);
            }
            return result;
        }

        List<string> CopyPropagation(List<string> lines)
        {
            var copies = new Dictionary<string, string>();
            var result = new List<string>();
            foreach (var line in lines)
            {
                var m = Regex.Match(line, @"^(\w+)\s*=\s*(\w+)$");
                if (m.Success)
                {
                    string dst = m.Groups[1].Value, src = m.Groups[2].Value;
                    string resolved = copies.ContainsKey(src) ? copies[src] : src;
                    copies[dst] = resolved;
                    if (dst == resolved) { Report.Add($"Dead Copy Elim: '{line}'"); continue; }
                    result.Add($"{dst} = {resolved}");
                    if (resolved != src) Report.Add($"Copy Prop: '{line}' → '{dst} = {resolved}'");
                    else result[result.Count - 1] = line;
                    continue;
                }
                // Replace variables in RHS
                string newLine = line;
                foreach (var kv in copies)
                {
                    newLine = Regex.Replace(newLine, $@"\b{Regex.Escape(kv.Key)}\b", m2 =>
                    {
                        int eqPos = line.IndexOf('=');
                        if (eqPos >= 0 && m2.Index <= eqPos) return m2.Value;
                        return kv.Value;
                    });
                }
                result.Add(newLine);
            }
            return result;
        }

        List<string> DeadCodeElim(List<string> lines)
        {
            // Find used variables
            var used = new HashSet<string>();
            foreach (var line in lines)
            {
                int eq = line.IndexOf('=');
                if (eq >= 0)
                {
                    string rhs = line.Substring(eq + 1);
                    foreach (Match m in Regex.Matches(rhs, @"\b([a-zA-Z_]\w*)\b"))
                        used.Add(m.Groups[1].Value);
                }
                // Always keep labels, func calls, if, goto, return
                if (line.Contains("CALL") || line.Contains("GOTO") || line.Contains("IF_") ||
                    line.Contains("RETURN") || line.Contains("FUNC_") || line.Contains("ARG") ||
                    line.Contains("PARAM") || line.EndsWith(":"))
                {
                    foreach (Match m in Regex.Matches(line, @"\b([a-zA-Z_]\w*)\b"))
                        used.Add(m.Groups[1].Value);
                }
            }

            var result = new List<string>();
            foreach (var line in lines)
            {
                var m = Regex.Match(line, @"^(t\d+)\s*=");
                if (m.Success && !used.Contains(m.Groups[1].Value))
                { Report.Add($"Dead Code Elim: '{line}'"); continue; }
                result.Add(line);
            }
            return result;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  TARGET CODE GENERATOR  —  pseudo-ARM-like assembly
    // ══════════════════════════════════════════════════════════════════
    public class TargetGenerator
    {
        public List<string> Assembly = new List<string>();
        private int _regCount = 0;
        private Dictionary<string, string> _regMap = new Dictionary<string, string>();
        private int _stackOffset = 0;
        private Dictionary<string, int> _stackMap = new Dictionary<string, int>();

        string AllocReg(string var)
        {
            if (_regMap.ContainsKey(var)) return _regMap[var];
            string reg = $"R{_regCount % 8}"; _regCount++;
            _regMap[var] = reg;
            return reg;
        }

        public void Generate(List<string> tac)
        {
            Emit(".data");
            Emit(".text");
            Emit(".global _start");
            Emit("_start:");

            foreach (var line in tac)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                Emit($"  ; {line}");

                if (line.StartsWith("FUNC_BEGIN"))
                { string fn = line.Split(' ')[1]; Emit($"{fn}:"); Emit("  PUSH {LR, FP}"); Emit("  MOV FP, SP"); _regMap.Clear(); _stackOffset = 0; continue; }

                if (line.StartsWith("FUNC_END"))
                { Emit("  MOV SP, FP"); Emit("  POP {PC, FP}"); continue; }

                if (line.EndsWith(":")) { Emit(line); continue; }

                if (line.StartsWith("RETURN"))
                {
                    if (line.Length > 7) { string rv = line.Substring(7).Trim(); Emit($"  MOV R0, {Operand(rv)}"); }
                    Emit("  BX LR"); continue;
                }

                if (line.StartsWith("IF_FALSE"))
                {
                    var parts = line.Split(' ');
                    string cond = parts[1], lbl = parts[3];
                    Emit($"  CMP {Operand(cond)}, #0");
                    Emit($"  BEQ {lbl}"); continue;
                }

                if (line.StartsWith("GOTO")) { Emit($"  B {line.Split(' ')[1]}"); continue; }

                if (line.StartsWith("ARG")) { string av = line.Split(' ')[1]; Emit($"  PUSH {{{Operand(av)}}}"); continue; }

                if (line.StartsWith(";")) { continue; }

                var m = Regex.Match(line, @"^(\w+)\s*=\s*(\w[\w.]*)\s*([\+\-\*\/\%\<\>\!&\|=]+)\s*(\w[\w.]*)$");
                if (m.Success)
                {
                    string dst = m.Groups[1].Value, lhs = m.Groups[2].Value, op = m.Groups[3].Value, rhs = m.Groups[4].Value;
                    string dstReg = AllocReg(dst);
                    Emit($"  MOV {dstReg}, {Operand(lhs)}");
                    string ins = op == "+" ? "ADD" : op == "-" ? "SUB" : op == "*" ? "MUL" : op == "/" ? "SDIV" :
                                 op == "%" ? "UMOD" : op == "<" ? "MOVLT" : op == ">" ? "MOVGT" :
                                 op == "<=" ? "MOVLE" : op == ">=" ? "MOVGE" : op == "==" ? "MOVEQ" :
                                 op == "!=" ? "MOVNE" : op == "&&" ? "AND" : op == "||" ? "ORR" : "MOV";
                    if (op == "<" || op == ">" || op == "<=" || op == ">=" || op == "==" || op == "!=")
                    {
                        Emit($"  CMP {dstReg}, {Operand(rhs)}");
                        Emit($"  MOV {dstReg}, #0");
                        Emit($"  {ins} {dstReg}, #1");
                    }
                    else Emit($"  {ins} {dstReg}, {dstReg}, {Operand(rhs)}");
                    AllocReg(dst);
                    continue;
                }

                var m2 = Regex.Match(line, @"^(\w+)\s*=\s*(\w[\w.]*)$");
                if (m2.Success)
                {
                    string dst = m2.Groups[1].Value, src = m2.Groups[2].Value;
                    string dstReg = AllocReg(dst);
                    Emit($"  MOV {dstReg}, {Operand(src)}");
                    _stackOffset += 4;
                    _stackMap[dst] = _stackOffset;
                    Emit($"  STR {dstReg}, [FP, #-{_stackOffset}]");
                    continue;
                }

                var m3 = Regex.Match(line, @"^(\w+)\s*=\s*([+-]?[\d.]+)$");
                if (m3.Success)
                {
                    string dst = m3.Groups[1].Value, val = m3.Groups[2].Value;
                    string dstReg = AllocReg(dst);
                    Emit($"  MOV {dstReg}, #{val}");
                    _stackOffset += 4;
                    _stackMap[dst] = _stackOffset;
                    Emit($"  STR {dstReg}, [FP, #-{_stackOffset}]");
                    continue;
                }

                Emit($"  ; unhandled: {line}");
            }

            Emit("  MOV R0, #0");
            Emit("  MOV R7, #1");
            Emit("  SVC #0");
        }

        string Operand(string v)
        {
            if (Regex.IsMatch(v, @"^[+-]?[\d.]+$")) return "#" + v;
            if (_regMap.ContainsKey(v)) return _regMap[v];
            if (_stackMap.ContainsKey(v)) return $"[FP, #-{_stackMap[v]}]";
            return v;
        }

        void Emit(string s) { Assembly.Add(s); }
    }

    // ══════════════════════════════════════════════════════════════════
    //  MAIN FORM  —  Production-grade UI
    // ══════════════════════════════════════════════════════════════════
    public partial class Form1 : Form
    {
        // ── Color Palette ─────────────────────────────────────────────
        static Color C_BG0 = Color.FromArgb(8, 8, 16);
        static Color C_BG1 = Color.FromArgb(13, 13, 25);
        static Color C_BG2 = Color.FromArgb(18, 18, 35);
        static Color C_BG3 = Color.FromArgb(24, 24, 46);
        static Color C_BG4 = Color.FromArgb(32, 32, 58);
        static Color C_CYAN = Color.FromArgb(0, 215, 255);
        static Color C_BLUE = Color.FromArgb(70, 130, 255);
        static Color C_PURPLE = Color.FromArgb(155, 90, 255);
        static Color C_GREEN = Color.FromArgb(0, 230, 115);
        static Color C_AMBER = Color.FromArgb(255, 190, 0);
        static Color C_CORAL = Color.FromArgb(255, 75, 95);
        static Color C_TEAL = Color.FromArgb(0, 200, 175);
        static Color C_PINK = Color.FromArgb(255, 80, 180);
        static Color C_TEXT = Color.FromArgb(220, 225, 245);
        static Color C_MUTED = Color.FromArgb(110, 115, 150);
        static Color C_DIM = Color.FromArgb(60, 65, 95);

        static string[] STEP_NAMES = { "Lexical Analysis", "Syntax / Parse", "Semantic Analysis", "TAC / IR Gen", "Optimise", "Target Code" };
        static Color[] STEP_COLORS;

        // ── UI Controls ───────────────────────────────────────────────
        private Panel sidePanel, mainPanel, headerPanel;
        private RichTextBox codeEditor;
        private DataGridView gridTokens, gridSymbols;
        private ListBox listTAC, listOptTAC, listTarget, listLog;
        private Panel panelAST;
        private GlowButton[] stepButtons;
        private GlowButton btnRunAll, btnClear;
        private Label lblStatus;
        private Timer pulseTimer;
        private int pulseAlpha = 0;
        private bool pulseUp = true;
        private Panel statusBar;

        // ── Compiler State ────────────────────────────────────────────
        private List<Token> _tokens;
        private ProgramNode _ast;
        private SemanticAnalyser _semantic;
        private TACGenerator _tacGen;
        private Optimiser _optimiser;
        private TargetGenerator _target;
        private int _activeStep = -1;

        // ─────────────────────────────────────────────────────────────
        //  GlowButton (custom drawn)
        // ─────────────────────────────────────────────────────────────
        class GlowButton : Control
        {
            public Color GlowColor = Color.FromArgb(0, 215, 255);
            public string Badge = "";
            public bool IsActive = false;
            private bool _hover;

            public GlowButton()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                Cursor = Cursors.Hand;
                Height = 42;
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                var rc = new Rectangle(2, 2, Width - 4, Height - 4);

                int bgA = IsActive ? 55 : _hover ? 30 : 12;
                using (var br = new SolidBrush(Color.FromArgb(bgA, GlowColor))) FillRR(g, br, rc, 8);
                int borderA = IsActive ? 210 : _hover ? 150 : 65;
                using (var pen = new Pen(Color.FromArgb(borderA, GlowColor), IsActive ? 1.5f : 1f)) DrawRR(g, pen, rc, 8);
                using (var bar = new SolidBrush(Color.FromArgb(IsActive ? 255 : _hover ? 190 : 100, GlowColor)))
                    g.FillRectangle(bar, new Rectangle(2, 8, 3, Height - 16));

                var badge = new Rectangle(10, Height / 2 - 11, 22, 22);
                using (var br = new SolidBrush(Color.FromArgb(65, GlowColor))) FillRR(g, br, badge, 4);
                using (var f = new Font("Consolas", 8f, FontStyle.Bold))
                using (var tb = new SolidBrush(GlowColor))
                    g.DrawString(Badge, f, tb, badge, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });

                var textRc = new Rectangle(38, 0, Width - 44, Height);
                using (var f = new Font("Segoe UI", 9f, IsActive ? FontStyle.Bold : FontStyle.Regular))
                using (var tb = new SolidBrush(IsActive ? Color.White : Color.FromArgb(195, C_TEXT)))
                    g.DrawString(Text, f, tb, textRc, new StringFormat { LineAlignment = StringAlignment.Center });
            }

            static Color C_TEXT = Color.FromArgb(220, 225, 245);
            static void FillRR(Graphics g, Brush b, Rectangle rc, int r) { using (var gp = RRP(rc, r)) g.FillPath(b, gp); }
            static void DrawRR(Graphics g, Pen p, Rectangle rc, int r) { using (var gp = RRP(rc, r)) g.DrawPath(p, gp); }
            static GraphicsPath RRP(Rectangle rc, int r)
            {
                var gp = new GraphicsPath();
                gp.AddArc(rc.X, rc.Y, r * 2, r * 2, 180, 90);
                gp.AddArc(rc.Right - r * 2, rc.Y, r * 2, r * 2, 270, 90);
                gp.AddArc(rc.Right - r * 2, rc.Bottom - r * 2, r * 2, r * 2, 0, 90);
                gp.AddArc(rc.X, rc.Bottom - r * 2, r * 2, r * 2, 90, 90);
                gp.CloseFigure(); return gp;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  CardPanel (custom painted border)
        // ─────────────────────────────────────────────────────────────
        class CardPanel : Panel
        {
            private Color _glow;
            public CardPanel(Color g) { _glow = g; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (var pen = new Pen(Color.FromArgb(90, _glow), 1f))
                {
                    var rc = new Rectangle(0, 0, Width - 1, Height - 1);
                    using (var gp = new GraphicsPath())
                    {
                        int r = 6;
                        gp.AddArc(rc.X, rc.Y, r * 2, r * 2, 180, 90);
                        gp.AddArc(rc.Right - r * 2, rc.Y, r * 2, r * 2, 270, 90);
                        gp.AddArc(rc.Right - r * 2, rc.Bottom - r * 2, r * 2, r * 2, 0, 90);
                        gp.AddArc(rc.X, rc.Bottom - r * 2, r * 2, r * 2, 90, 90);
                        gp.CloseFigure();
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        e.Graphics.DrawPath(pen, gp);
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  CONSTRUCTOR
        // ─────────────────────────────────────────────────────────────
        public Form1()
        {
            STEP_COLORS = new[] { C_GREEN, C_BLUE, C_PURPLE, C_TEAL, C_AMBER, C_CORAL };
            SuspendLayout();
            InitializeUI();
            codeEditor.TextChanged += CodeEditor_TextChanged;
            panelAST.Paint += PanelAST_Paint;
            ResumeLayout(false);

            pulseTimer = new Timer { Interval = 28 };
            pulseTimer.Tick += (s, e) =>
            {
                pulseAlpha = pulseUp ? Math.Min(pulseAlpha + 3, 75) : Math.Max(pulseAlpha - 3, 0);
                if (pulseAlpha >= 75) pulseUp = false;
                if (pulseAlpha <= 0) pulseUp = true;
                headerPanel?.Invalidate();
            };
            pulseTimer.Start();
        }

        // ─────────────────────────────────────────────────────────────
        //  UI INIT
        // ─────────────────────────────────────────────────────────────
        void InitializeUI()
        {
            Text = "Modern Compiler Studio  —  Full Pipeline";
            WindowState = FormWindowState.Maximized;
            BackColor = C_BG1;
            Font = new Font("Segoe UI", 9f);
            ForeColor = C_TEXT;
            DoubleBuffered = true;

            // ── Sidebar ───────────────────────────────────────────────
            sidePanel = new Panel { Dock = DockStyle.Left, Width = 240, BackColor = C_BG2 };
            sidePanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.Clear(C_BG2);
                using (var pen = new Pen(Color.FromArgb(55, C_CYAN), 1f))
                    g.DrawLine(pen, sidePanel.Width - 1, 0, sidePanel.Width - 1, sidePanel.Height);
                using (var br = new SolidBrush(Color.FromArgb(12, C_CYAN)))
                    for (int y = 24; y < sidePanel.Height; y += 26)
                        for (int x = 12; x < sidePanel.Width - 12; x += 26)
                            g.FillEllipse(br, x, y, 2, 2);
            };

            mainPanel = new Panel { Dock = DockStyle.Fill, BackColor = C_BG1, Padding = new Padding(8) };

            Controls.Add(mainPanel);
            Controls.Add(sidePanel);

            BuildSidebar();
            BuildMainArea();
            BuildStatusBar();

            // Wire step buttons
            stepButtons[0].Click += (s, e) => RunStep(0, StepLexical);
            stepButtons[1].Click += (s, e) => RunStep(1, StepParse);
            stepButtons[2].Click += (s, e) => RunStep(2, StepSemantic);
            stepButtons[3].Click += (s, e) => RunStep(3, StepTAC);
            stepButtons[4].Click += (s, e) => RunStep(4, StepOptimise);
            stepButtons[5].Click += (s, e) => RunStep(5, StepTarget);
            btnRunAll.Click += (s, e) => RunAll();
            btnClear.Click += (s, e) => ClearAll();
        }

        // ─────────────────────────────────────────────────────────────
        //  SIDEBAR
        // ─────────────────────────────────────────────────────────────
        void BuildSidebar()
        {
            headerPanel = new Panel { Bounds = new Rectangle(0, 0, 240, 106), BackColor = Color.Transparent };
            headerPanel.Paint += HeaderPanel_Paint;
            sidePanel.Controls.Add(headerPanel);

            stepButtons = new GlowButton[6];
            int top = 112;
            for (int i = 0; i < 6; i++)
            {
                var btn = new GlowButton
                {
                    Text = STEP_NAMES[i],
                    Badge = (i + 1).ToString(),
                    GlowColor = STEP_COLORS[i],
                    Bounds = new Rectangle(10, top, 220, 42)
                };
                stepButtons[i] = btn;
                sidePanel.Controls.Add(btn);
                top += 47;
            }

            top += 8;
            sidePanel.Controls.Add(MkLabel("EXECUTION LOG", new Rectangle(14, top, 212, 16)));

            listLog = new ListBox
            {
                Bounds = new Rectangle(10, top + 18, 220, 145),
                BackColor = C_BG0,
                ForeColor = C_GREEN,
                Font = new Font("Consolas", 7.8f),
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 17
            };
            listLog.DrawItem += LogDrawItem;
            sidePanel.Controls.Add(listLog);

            top += 168;
            btnRunAll = new GlowButton { Text = "▶  Run All Steps", Badge = "▶", GlowColor = C_CYAN, Bounds = new Rectangle(10, top, 220, 42) };
            sidePanel.Controls.Add(btnRunAll);

            btnClear = new GlowButton { Text = "✕  Clear All", Badge = "✕", GlowColor = C_CORAL, Bounds = new Rectangle(10, top + 48, 220, 42) };
            sidePanel.Controls.Add(btnClear);
        }

        Label MkLabel(string text, Rectangle bounds)
        {
            return new Label
            {
                Text = text,
                Bounds = bounds,
                ForeColor = C_DIM,
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
        }

        void LogDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            string s = listLog.Items[e.Index].ToString();
            e.DrawBackground();
            Color fg = s.Contains("ERROR") ? C_CORAL
                     : s.Contains("WARN") ? C_AMBER
                     : s.StartsWith("---") || s.StartsWith("===") ? C_CYAN
                     : s.Contains("OK") || s.Contains("✓") ? C_GREEN
                     : C_MUTED;
            using (var f = new Font("Consolas", 7.8f, s.StartsWith("===") ? FontStyle.Bold : FontStyle.Regular))
            using (var br = new SolidBrush(fg))
                e.Graphics.DrawString(s, f, br, e.Bounds.X + 4, e.Bounds.Y + 1);
        }

        void HeaderPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int cx = 120, cy = 50;
            for (int r = 55; r > 0; r -= 10)
            {
                int a = (int)((55 - r) / 55.0 * pulseAlpha * 0.45);
                using (var br = new SolidBrush(Color.FromArgb(Math.Max(0, Math.Min(255, a)), C_CYAN)))
                    g.FillEllipse(br, cx - r, cy - r, r * 2, r * 2);
            }

            var iconRc = new Rectangle(14, 14, 38, 38);
            using (var br = new SolidBrush(Color.FromArgb(35, C_CYAN))) g.FillEllipse(br, iconRc);
            using (var pen = new Pen(C_CYAN, 1.5f)) g.DrawEllipse(pen, iconRc);
            using (var f = new Font("Consolas", 12f, FontStyle.Bold))
            using (var br = new SolidBrush(C_CYAN))
                g.DrawString("</>", f, br, iconRc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });

            using (var f = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var br = new SolidBrush(C_TEXT)) g.DrawString("Compiler Studio", f, br, 60, 14);
            using (var f = new Font("Segoe UI", 7.5f))
            using (var br = new SolidBrush(C_MUTED)) g.DrawString("Full Real-World Pipeline  v4.0", f, br, 61, 37);
            using (var f = new Font("Segoe UI", 7f))
            using (var br = new SolidBrush(C_DIM)) g.DrawString("by Talha", f, br, 61, 54);

            int w = headerPanel.Width;
            using (var lgb = new LinearGradientBrush(new Point(0, 0), new Point(w, 0), C_CYAN, C_PURPLE))
            using (var pen = new Pen(lgb, 1.5f))
                g.DrawLine(pen, 10, headerPanel.Height - 2, w - 10, headerPanel.Height - 2);
        }

        // ─────────────────────────────────────────────────────────────
        //  MAIN AREA
        // ─────────────────────────────────────────────────────────────
        void BuildMainArea()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            mainPanel.Controls.Add(layout);

            // Row 0 — code editor
            var r0 = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 6) };
            r0.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            r0.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            r0.Controls.Add(SectionLabel("  ⌨  SOURCE CODE  —  Supports: declarations, if/else, while, for, functions, operators", C_CYAN), 0, 0);
            codeEditor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 11.5f),
                BackColor = C_BG2,
                ForeColor = Color.FromArgb(185, 215, 255),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                AcceptsTab = true,
                Text =
                    "// Modern Compiler Studio — Test Program\r\n" +
                    "int x = 10;\r\n" +
                    "int y = 20 + 5;\r\n" +
                    "float pi = 3.14;\r\n" +
                    "int sum = x + y;\r\n" +
                    "int product = x * 4;\r\n" +
                    "bool flag = true;\r\n" +
                    "\r\n" +
                    "int factorial(int n) {\r\n" +
                    "    if (n <= 1) {\r\n" +
                    "        return 1;\r\n" +
                    "    }\r\n" +
                    "    return n * factorial(n - 1);\r\n" +
                    "}\r\n" +
                    "\r\n" +
                    "int main() {\r\n" +
                    "    int i = 0;\r\n" +
                    "    while (i < 5) {\r\n" +
                    "        i++;\r\n" +
                    "    }\r\n" +
                    "    return 0;\r\n" +
                    "}"
            };
            r0.Controls.Add(WrapCard(codeEditor, C_BLUE), 0, 1);
            layout.Controls.Add(r0, 0, 0);

            // Row 1 — tokens + symbols
            var r1 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
            r1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            r1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            r1.Controls.Add(MakeGrid("  ⬡  TOKENS", C_GREEN, new[] { "Line", "Col", "Lexeme", "Token Type" }, out gridTokens), 0, 0);
            r1.Controls.Add(MakeGrid("  ⬡  SYMBOL TABLE", C_PURPLE, new[] { "Name", "Type", "Scope", "Line", "Kind" }, out gridSymbols), 1, 0);
            layout.Controls.Add(r1, 0, 1);

            // Row 2 — AST + TAC + Opt + Assembly
            var r2 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.Transparent };
            for (int i = 0; i < 4; i++) r2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            r2.Controls.Add(MakeAST(out panelAST), 0, 0);
            r2.Controls.Add(MakeList("  ⬡  ORIGINAL TAC / IR", C_TEAL, out listTAC), 1, 0);
            r2.Controls.Add(MakeList("  ⬡  OPTIMISED IR", C_AMBER, out listOptTAC), 2, 0);
            r2.Controls.Add(MakeList("  ⬡  TARGET ASSEMBLY", C_CORAL, out listTarget), 3, 0);
            layout.Controls.Add(r2, 0, 2);
        }

        void BuildStatusBar()
        {
            statusBar = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = C_BG0 };
            statusBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(40, C_CYAN)))
                    e.Graphics.DrawLine(pen, 0, 0, statusBar.Width, 0);
            };
            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = C_MUTED,
                Font = new Font("Consolas", 8f),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Text = "  Ready — Load source code and run the pipeline"
            };
            statusBar.Controls.Add(lblStatus);
            Controls.Add(statusBar);
        }
        void CodeEditor_TextChanged(object sender, EventArgs e)
        {
            int cursor = codeEditor.SelectionStart;

            codeEditor.SelectAll();
            codeEditor.SelectionColor = Color.FromArgb(185, 215, 255);

            string[] keywords = { "int", "float", "if", "else", "while", "for", "return", "bool", "true", "false" };

            foreach (string word in keywords)
            {
                foreach (Match m in Regex.Matches(codeEditor.Text, $@"\b{word}\b"))
                {
                    codeEditor.Select(m.Index, m.Length);
                    codeEditor.SelectionColor = Color.Cyan;
                }
            }

            foreach (Match m in Regex.Matches(codeEditor.Text, @"\b\d+(\.\d+)?\b"))
            {
                codeEditor.Select(m.Index, m.Length);
                codeEditor.SelectionColor = Color.LightGreen;
            }

            codeEditor.SelectionStart = cursor;
            codeEditor.SelectionLength = 0;
        }

        // ─────────────────────────────────────────────────────────────
        //  UI FACTORIES
        // ─────────────────────────────────────────────────────────────
        Label SectionLabel(string text, Color color)
        {
            return new Label
            {
                Text = text,
                ForeColor = color,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
        }

        Panel WrapCard(Control child, Color glow)
        {
            var card = new CardPanel(glow) { Dock = DockStyle.Fill, Padding = new Padding(1) };
            child.Dock = DockStyle.Fill;
            card.Controls.Add(child);
            return card;
        }

        Panel MakeGrid(string title, Color color, string[] cols, out DataGridView grid)
        {
            var outer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Transparent, Padding = new Padding(2, 0, 2, 0) };
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            outer.Controls.Add(SectionLabel(title, color), 0, 0);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = C_BG3,
                GridColor = Color.FromArgb(35, 40, 65),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = 26,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = new Font("Consolas", 8.5f),
                RowTemplate = { Height = 20 }
            };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = C_BG4,
                ForeColor = color,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                SelectionBackColor = C_BG4,
                SelectionForeColor = color,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            };
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = C_BG3,
                ForeColor = C_TEXT,
                SelectionBackColor = Color.FromArgb(45, color.R, color.G, color.B),
                SelectionForeColor = Color.White,
                Padding = new Padding(4, 0, 0, 0)
            };
            grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = C_BG2,
                ForeColor = C_TEXT,
                SelectionBackColor = Color.FromArgb(45, color.R, color.G, color.B),
                SelectionForeColor = Color.White,
                Padding = new Padding(4, 0, 0, 0)
            };
            foreach (var c in cols) grid.Columns.Add(c, c);
            outer.Controls.Add(WrapCard(grid, color), 0, 1);
            var wrap = new Panel { Dock = DockStyle.Fill }; wrap.Controls.Add(outer); return wrap;
        }

        Panel MakeList(string title, Color color, out ListBox list)
        {
            var outer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Transparent, Padding = new Padding(2, 0, 2, 0) };
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            outer.Controls.Add(SectionLabel(title, color), 0, 0);

            list = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG3,
                ForeColor = color,
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 19
            };
            Color capturedColor = color;
            list.DrawItem += (s, de) =>
            {
                if (de.Index < 0) return;
                string item = ((ListBox)s).Items[de.Index].ToString();
                de.DrawBackground();
                Color fg = item.StartsWith("  ;") ? C_DIM
                         : item.StartsWith("FUNC_") ? C_PINK
                         : item.StartsWith(".") ? C_MUTED
                         : item.StartsWith("_start") || item.EndsWith(":") ? C_CYAN
                         : item.Contains("MOV") || item.Contains("STR") || item.Contains("LDR") ? C_BLUE
                         : item.Contains("ADD") || item.Contains("SUB") || item.Contains("MUL") || item.Contains("DIV") ? C_AMBER
                         : item.Contains("IF_") || item.Contains("GOTO") || item.Contains("BEQ") || item.Contains("BNE") || item.Contains("  B ") ? C_PURPLE
                         : item.Contains("RETURN") || item.Contains("BX LR") ? C_CORAL
                         : item.Contains("=") ? capturedColor
                         : C_MUTED;
                using (var br = new SolidBrush((de.State & DrawItemState.Selected) != 0 ? Color.White : fg))
                using (var f = new Font("Consolas", 9f))
                    de.Graphics.DrawString(item, f, br, de.Bounds.X + 5, de.Bounds.Y + 2);
            };
            outer.Controls.Add(WrapCard(list, color), 0, 1);
            var wrap = new Panel { Dock = DockStyle.Fill }; wrap.Controls.Add(outer); return wrap;
        }

        Panel MakeAST(out Panel inner)
        {
            var outer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Transparent, Padding = new Padding(2, 0, 2, 0) };
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            outer.Controls.Add(SectionLabel("  ⬡  AST — ABSTRACT SYNTAX TREE", C_TEAL), 0, 0);
            inner = new Panel { Dock = DockStyle.Fill, BackColor = C_BG3 };
            outer.Controls.Add(WrapCard(inner, C_TEAL), 0, 1);
            var wrap = new Panel { Dock = DockStyle.Fill }; wrap.Controls.Add(outer); return wrap;
        }

        // ─────────────────────────────────────────────────────────────
        //  STEP RUNNER
        // ─────────────────────────────────────────────────────────────
        void RunStep(int idx, Action action)
        {
            _activeStep = idx;
            foreach (var b in stepButtons) b.IsActive = false;
            stepButtons[idx].IsActive = true;
            foreach (var b in stepButtons) b.Invalidate();
            try { action(); }
            catch (Exception ex) { Log($"ERROR: {ex.Message}"); Status($"Step failed: {ex.Message}", C_CORAL); }
        }

        // ─────────────────────────────────────────────────────────────
        //  STEP 1 — LEXICAL
        // ─────────────────────────────────────────────────────────────
        void StepLexical()
        {
            gridTokens.Rows.Clear();
            string src = codeEditor.Text;
            var lexer = new Lexer(src);
            _tokens = lexer.Tokenize();

            foreach (var tok in _tokens.Where(t => t.Type != TokenType.EOF))
                gridTokens.Rows.Add(tok.Line.ToString(), tok.Column.ToString(), tok.Lexeme, tok.Type.ToString());

            if (lexer.Errors.Count > 0)
            {
                foreach (var err in lexer.Errors) Log("LEX ERROR: " + err);
                Status($"Lexical: {_tokens.Count} tokens, {lexer.Errors.Count} error(s)", C_CORAL);
            }
            else
            {
                Log($"✓ Lexical: {_tokens.Where(t => t.Type != TokenType.EOF).Count()} tokens");
                Status("Lexical Analysis complete — " + _tokens.Count + " tokens", C_GREEN);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  STEP 2 — PARSE
        // ─────────────────────────────────────────────────────────────
        void StepParse()
        {
            if (_tokens == null || _tokens.Count == 0) { Log("Run Lexical first"); return; }
            var parser = new Parser(_tokens);
            _ast = parser.Parse();

            if (parser.Errors.Count > 0)
            {
                foreach (var err in parser.Errors) Log("PARSE ERROR: " + err);
                Status("Syntax errors found — check log", C_CORAL);
                MessageBox.Show(string.Join("\n", parser.Errors), "Syntax Errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                Log($"✓ Syntax: AST built — {CountNodes(_ast)} nodes");
                Status("Parse complete — AST constructed", C_GREEN);
            }
            panelAST.Invalidate();
        }

        int CountNodes(AstNode n)
        {
            if (n == null) return 0;
            int c = 1;
            switch (n)
            {
                case ProgramNode p: foreach (var s in p.Statements) c += CountNodes(s); break;
                case BlockStmt b: foreach (var s in b.Stmts) c += CountNodes(s); break;
                case BinaryExpr be: c += CountNodes(be.Left) + CountNodes(be.Right); break;
                case UnaryExpr ue: c += CountNodes(ue.Operand); break;
                case DeclStmt ds: c += CountNodes(ds.Initializer); break;
                case IfStmt ifs: c += CountNodes(ifs.Cond) + CountNodes(ifs.Then) + CountNodes(ifs.Else); break;
                case WhileStmt ws: c += CountNodes(ws.Cond) + CountNodes(ws.Body); break;
                case ForStmt fs: c += CountNodes(fs.Init) + CountNodes(fs.Cond) + CountNodes(fs.Update) + CountNodes(fs.Body); break;
                case ReturnStmt rs: c += CountNodes(rs.Value); break;
                case ExprStmt es: c += CountNodes(es.Expr); break;
                case FuncDecl fd: c += CountNodes(fd.Body); break;
                case FuncCallExpr fc: foreach (var a in fc.Args) c += CountNodes(a); break;
                case AssignStmt ast: c += CountNodes(ast.Value); break;
            }
            return c;
        }

        // ─────────────────────────────────────────────────────────────
        //  STEP 3 — SEMANTIC
        // ─────────────────────────────────────────────────────────────
        void StepSemantic()
        {
            if (_ast == null) { Log("Run Parse first"); return; }
            gridSymbols.Rows.Clear();
            _semantic = new SemanticAnalyser();
            _semantic.Analyse(_ast);

            foreach (var sym in _semantic.Symbols)
                gridSymbols.Rows.Add(sym.Name, sym.Type, sym.Scope, sym.Line.ToString(), sym.IsFunction ? "Function" : "Variable");

            foreach (var err in _semantic.Errors) Log("SEM ERROR: " + err);
            foreach (var w in _semantic.Warnings) Log("WARN: " + w);

            if (_semantic.Errors.Count > 0) Status($"Semantic: {_semantic.Errors.Count} error(s)", C_CORAL);
            else { Log($"✓ Semantic: {_semantic.Symbols.Count} symbols, {_semantic.Warnings.Count} warning(s)"); Status("Semantic analysis complete", C_GREEN); }
        }

        // ─────────────────────────────────────────────────────────────
        //  STEP 4 — TAC / IR
        // ─────────────────────────────────────────────────────────────
        void StepTAC()
        {
            if (_ast == null) { Log("Run Parse first"); return; }
            listTAC.Items.Clear();
            _tacGen = new TACGenerator();
            _tacGen.Generate(_ast);
            foreach (var line in _tacGen.Instructions) listTAC.Items.Add(line);
            Log($"✓ TAC Gen: {_tacGen.Instructions.Count} instructions");
            Status("TAC / IR generation complete", C_GREEN);
        }

        // ─────────────────────────────────────────────────────────────
        //  STEP 5 — OPTIMISE
        // ─────────────────────────────────────────────────────────────
        void StepOptimise()
        {
            if (_tacGen == null || _tacGen.Instructions.Count == 0) { Log("Run TAC first"); return; }
            listOptTAC.Items.Clear();
            _optimiser = new Optimiser();
            _optimiser.Optimise(_tacGen.Instructions);
            foreach (var line in _optimiser.Optimized) listOptTAC.Items.Add(line);
            foreach (var r in _optimiser.Report) Log("OPT: " + r);
            Log($"✓ Optimised: {_tacGen.Instructions.Count} → {_optimiser.Optimized.Count} instructions ({_optimiser.Report.Count} optimisations)");
            Status("Optimisation complete", C_GREEN);
        }

        // ─────────────────────────────────────────────────────────────
        //  STEP 6 — TARGET CODE
        // ─────────────────────────────────────────────────────────────
        void StepTarget()
        {
            var src = _optimiser?.Optimized?.Count > 0 ? _optimiser.Optimized
                    : _tacGen?.Instructions?.Count > 0 ? _tacGen.Instructions
                    : null;
            if (src == null) { Log("Run Optimise or TAC first"); return; }

            listTarget.Items.Clear();
            _target = new TargetGenerator();
            _target.Generate(src);
            foreach (var line in _target.Assembly) listTarget.Items.Add(line);
            int real = _target.Assembly.Count(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith(";") && !l.TrimStart().StartsWith(".") && !l.EndsWith(":"));
            Log($"✓ Target: {real} assembly instructions");
            Status("Target code generation complete — full pipeline done!", C_GREEN);
        }

        // ─────────────────────────────────────────────────────────────
        //  RUN ALL
        // ─────────────────────────────────────────────────────────────
        void RunAll()
        {
            ClearAll(silent: true);
            Log("=== Pipeline Start ===");
            Action[] steps = { StepLexical, StepParse, StepSemantic, StepTAC, StepOptimise, StepTarget };
            for (int i = 0; i < steps.Length; i++)
            {
                foreach (var b in stepButtons) b.IsActive = false;
                stepButtons[i].IsActive = true;
                stepButtons[i].Invalidate();
                try { steps[i](); }
                catch (Exception ex) { Log($"STEP {i + 1} FAILED: {ex.Message}"); }
            }
            Log("=== Pipeline Complete ===");
        }

        // ─────────────────────────────────────────────────────────────
        //  CLEAR
        // ─────────────────────────────────────────────────────────────
        void ClearAll(bool silent = false)
        {
            _tokens = null; _ast = null; _semantic = null;
            _tacGen = null; _optimiser = null; _target = null;
            gridTokens.Rows.Clear(); gridSymbols.Rows.Clear();
            listTAC.Items.Clear(); listOptTAC.Items.Clear();
            listTarget.Items.Clear(); listLog.Items.Clear();
            foreach (var b in stepButtons) { b.IsActive = false; b.Invalidate(); }
            _activeStep = -1;
            panelAST.Invalidate();
            if (!silent) { Log("-- Cleared --"); Status("Cleared", C_MUTED); }
        }

        void Log(string msg)
        {
            listLog.Items.Add(msg);
            listLog.SelectedIndex = listLog.Items.Count - 1;
        }

        void Status(string msg, Color color)
        {
            lblStatus.ForeColor = color;
            lblStatus.Text = "  " + msg;
        }

        // ─────────────────────────────────────────────────────────────
        //  AST PAINTER
        // ─────────────────────────────────────────────────────────────
        void PanelAST_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(C_BG3);

            using (var pen = new Pen(Color.FromArgb(14, C_TEAL), 1f))
            {
                for (int y = 0; y < panelAST.Height; y += 28) g.DrawLine(pen, 0, y, panelAST.Width, y);
                for (int x = 0; x < panelAST.Width; x += 28) g.DrawLine(pen, x, 0, x, panelAST.Height);
            }

            if (_ast == null || _ast.Statements.Count == 0)
            {
                using (var f = new Font("Segoe UI", 9f, FontStyle.Italic))
                using (var br = new SolidBrush(C_DIM))
                    g.DrawString("Run Parse to visualise AST", f, br, new RectangleF(10, panelAST.Height / 2f - 12, panelAST.Width - 20, 24),
                        new StringFormat { Alignment = StringAlignment.Center });
                return;
            }

            // Draw last function or last statement
            AstNode root = null;
            foreach (var s in _ast.Statements)
                if (s is FuncDecl || s is DeclStmt || s is IfStmt || s is WhileStmt) root = s;
            if (root == null && _ast.Statements.Count > 0) root = _ast.Statements[_ast.Statements.Count - 1];
            if (root == null) return;

            DrawASTNode(g, root, panelAST.Width / 2, 36, Math.Max(panelAST.Width / 6, 50), 58, 0);
        }

        void DrawASTNode(Graphics g, AstNode node, int x, int y, int xOff, int yStep, int depth)
        {
            if (node == null || depth > 5) return;
            string label = NodeLabel(node);
            Color col = NodeColor(node);
            int r = 20;

            // Draw children
            var children = GetChildren(node);
            int cx = x - (children.Count - 1) * xOff / 2;
            for (int i = 0; i < children.Count && i < 4; i++)
            {
                int nx = cx + i * xOff, ny = y + yStep;
                if (children[i] == null) { cx += 0; continue; }
                using (var pen = new Pen(Color.FromArgb(40, col), 2f)) g.DrawLine(pen, x, y + r, nx, ny - r);
                using (var pen = new Pen(Color.FromArgb(100, col), 1f)) g.DrawLine(pen, x, y + r, nx, ny - r);
                DrawASTNode(g, children[i], nx, ny, Math.Max(xOff * 6 / 10, 22), yStep, depth + 1);
            }

            using (var br = new SolidBrush(Color.FromArgb(16, col))) g.FillEllipse(br, x - r - 5, y - r - 5, (r + 5) * 2, (r + 5) * 2);
            using (var br = new SolidBrush(Color.FromArgb(45, col))) g.FillEllipse(br, x - r, y - r, r * 2, r * 2);
            using (var pen = new Pen(col, 1.5f)) g.DrawEllipse(pen, x - r, y - r, r * 2, r * 2);
            using (var f = new Font("Consolas", 7.5f, FontStyle.Bold))
            using (var br = new SolidBrush(col))
                g.DrawString(label, f, br, new RectangleF(x - r, y - r, r * 2, r * 2),
                    new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        string NodeLabel(AstNode n)
        {
            switch (n)
            {
                case FuncDecl fd: return fd.Name + "()";
                case DeclStmt ds: return ds.DataType + "\n" + ds.Name;
                case AssignStmt ast: return "=" + ast.Op + "\n" + ast.Name;
                case BinaryExpr be: return be.Op;
                case UnaryExpr ue: return ue.Op;
                case LiteralExpr le: return le.Value.Length > 7 ? le.Value.Substring(0, 7) : le.Value;
                case IdentExpr ie: return ie.Name;
                case IfStmt _: return "if";
                case WhileStmt _: return "while";
                case ForStmt _: return "for";
                case ReturnStmt _: return "return";
                case BlockStmt _: return "{ }";
                case FuncCallExpr fc: return fc.Name + "()";
                default: return n.GetType().Name.Replace("Stmt", "").Replace("Expr", "");
            }
        }

        Color NodeColor(AstNode n)
        {
            if (n is FuncDecl) return C_PINK;
            if (n is DeclStmt) return C_CYAN;
            if (n is AssignStmt) return C_BLUE;
            if (n is BinaryExpr) return C_GREEN;
            if (n is UnaryExpr) return C_GREEN;
            if (n is LiteralExpr) return C_AMBER;
            if (n is IdentExpr) return C_TEAL;
            if (n is IfStmt || n is WhileStmt || n is ForStmt) return C_PURPLE;
            if (n is ReturnStmt) return C_CORAL;
            if (n is BlockStmt) return C_MUTED;
            if (n is FuncCallExpr) return C_PINK;
            return C_TEXT;
        }

        List<AstNode> GetChildren(AstNode n)
        {
            var ch = new List<AstNode>();
            switch (n)
            {
                case FuncDecl fd: ch.Add(fd.Body); break;
                case DeclStmt ds: if (ds.Initializer != null) ch.Add(ds.Initializer); break;
                case AssignStmt ast: ch.Add(ast.Value); break;
                case BinaryExpr be: ch.Add(be.Left); ch.Add(be.Right); break;
                case UnaryExpr ue: ch.Add(ue.Operand); break;
                case IfStmt ifs: ch.Add(ifs.Cond); ch.Add(ifs.Then); if (ifs.Else != null) ch.Add(ifs.Else); break;
                case WhileStmt ws: ch.Add(ws.Cond); ch.Add(ws.Body); break;
                case ForStmt fs: ch.Add(fs.Init); ch.Add(fs.Cond); ch.Add(fs.Body); break;
                case ReturnStmt rs: if (rs.Value != null) ch.Add(rs.Value); break;
                case BlockStmt blk: foreach (var s in blk.Stmts.Take(3)) ch.Add(s); break;
                case FuncCallExpr fc: foreach (var a in fc.Args.Take(3)) ch.Add(a); break;
                case ExprStmt es: ch.Add(es.Expr); break;
            }
            return ch;
        }
    }
}