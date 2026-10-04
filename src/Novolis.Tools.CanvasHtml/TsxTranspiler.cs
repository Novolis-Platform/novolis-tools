using System.Text;
using System.Text.Json;

namespace Novolis.Tools.CanvasHtml;

/// <summary>Strips TypeScript from a canvas and rewrites JSX to <c>__h</c> calls.</summary>
internal sealed class TsxTranspiler
{
    private enum Kind
    {
        None,
        Ident,
        Keyword,
        Number,
        String,
        Close,
    }

    private enum TypeEnd
    {
        Binding,
        Return,
        AsCast,
    }

    private string _source = "";
    private int _index;
    private StringBuilder _output = new();
    private int _paren;
    private int _bracket;
    private int _brace;
    private Kind _kind;
    private string _last = "";
    private bool _nextFunctionIsDefault;
    private string? _entryPoint;

    internal readonly record struct Result(string JavaScript, string EntryPoint);

    internal Result Transpile(string source)
    {
        _source = source.TrimStart('\uFEFF');
        _index = 0;
        _output = new StringBuilder(source.Length);
        _paren = _bracket = _brace = 0;
        _kind = Kind.None;
        _last = "";
        _nextFunctionIsDefault = false;
        _entryPoint = null;

        while (_index < _source.Length)
        {
            var before = _index;
            ConsumeWhitespace(emit: true);
            if (_index >= _source.Length)
                break;
            Step();
            if (_index == before)
                throw Fail("Parser did not advance");
        }

        if (string.IsNullOrEmpty(_entryPoint))
            throw Fail("No export default function");

        return new Result(_output.ToString(), _entryPoint);
    }

    private void Step()
    {
        var current = Peek;
        if (current == '/' && PeekAt(1) == '/')
        {
            SkipLineComment();
            return;
        }

        if (current == '/' && PeekAt(1) == '*')
        {
            SkipBlockComment();
            return;
        }

        if (current is '"' or '\'')
        {
            EmitRaw(ReadQuoted());
            Mark(Kind.String, "str");
            return;
        }

        if (current == '`')
        {
            EmitTemplate();
            Mark(Kind.String, "str");
            return;
        }

        if (current == '<' && IsJsxStart())
        {
            Emit(ParseJsx());
            Mark(Kind.Close, ")");
            return;
        }

        if (TryEmitNumber())
            return;

        if (IsIdentStart(current))
        {
            var word = ReadIdent();
            HandleWord(word);
            return;
        }

        if (current == '=' && PeekAt(1) == '>')
        {
            Emit("=>");
            _index += 2;
            Mark(Kind.Keyword, "=>");
            return;
        }

        if (current == '=' && PeekAt(1) == '=')
        {
            var token = PeekAt(2) == '=' ? "===" : "==";
            Emit(token);
            _index += token.Length;
            Mark(Kind.Keyword, token);
            return;
        }

        if (current == '!' && PeekAt(1) == '=')
        {
            var token = PeekAt(2) == '=' ? "!==" : "!=";
            Emit(token);
            _index += token.Length;
            Mark(Kind.Keyword, token);
            return;
        }

        if (current == '!' && IsExpressionEnd() && PeekAt(1) != '=')
        {
            _index++;
            return;
        }

        if (current == '&' && PeekAt(1) == '&')
        {
            Emit("&&");
            _index += 2;
            Mark(Kind.Keyword, "&&");
            return;
        }

        if (current == '|' && PeekAt(1) == '|')
        {
            Emit("||");
            _index += 2;
            Mark(Kind.Keyword, "||");
            return;
        }

        if (current == '?' && PeekAt(1) == '?')
        {
            Emit("??");
            _index += 2;
            Mark(Kind.Keyword, "??");
            return;
        }

        if (current == '?' && PeekAt(1) == '.')
        {
            Emit("?.");
            _index += 2;
            Mark(Kind.Keyword, "?.");
            return;
        }

        if (current == '.' && PeekAt(1) == '.' && PeekAt(2) == '.')
        {
            Emit("...");
            _index += 3;
            Mark(Kind.Keyword, "...");
            return;
        }

        if (current == '<' && PeekAt(1) == '=')
        {
            Emit("<=");
            _index += 2;
            Mark(Kind.Keyword, "<=");
            return;
        }

        if (current == '>' && PeekAt(1) == '=')
        {
            Emit(">=");
            _index += 2;
            Mark(Kind.Keyword, ">=");
            return;
        }

        if (current == '(' && FollowedByArrow(_index))
        {
            Emit("(");
            _index++;
            _paren++;
            EmitParams();
            ConsumeWhitespace(emit: false);
            if (Peek == ':')
            {
                _index++;
                SkipType(TypeEnd.Return);
            }

            Mark(Kind.Close, ")");
            return;
        }

        Emit(current.ToString());
        _index++;
        switch (current)
        {
            case '(':
                _paren++;
                Mark(Kind.Keyword, "(");
                break;
            case ')':
                _paren--;
                Mark(Kind.Close, ")");
                break;
            case '[':
                _bracket++;
                Mark(Kind.Keyword, "[");
                break;
            case ']':
                _bracket--;
                Mark(Kind.Close, "]");
                break;
            case '{':
                _brace++;
                Mark(Kind.Keyword, "{");
                break;
            case '}':
                _brace--;
                Mark(Kind.Close, "}");
                break;
            case ';':
                Mark(Kind.Keyword, ";");
                break;
            case '?':
            case ':':
            case '=':
            case ',':
            case '+':
            case '-':
            case '*':
            case '/':
            case '%':
            case '!':
            case '~':
                Mark(Kind.Keyword, current.ToString());
                break;
            default:
                Mark(Kind.Keyword, current.ToString());
                break;
        }
    }

    private void HandleWord(string word)
    {
        if (IsBoundary() && word == "import")
        {
            SkipImport();
            return;
        }

        if (IsBoundary() && word == "export")
        {
            HandleExport();
            return;
        }

        if (IsBoundary() && word == "type" && LooksLikeTypeAlias())
        {
            SkipTypeAlias();
            return;
        }

        if (IsBoundary() && word == "interface")
        {
            SkipInterface();
            return;
        }

        if (word is "const" or "let" or "var" && _last != "." && !NextNonWhiteIs(':'))
        {
            BeginDeclaration(word);
            return;
        }

        if (word == "function" && _last != ".")
        {
            EmitFunction();
            return;
        }

        if (word == "as" && IsExpressionEnd())
        {
            SkipType(TypeEnd.AsCast);
            return;
        }

        if (word == "satisfies" && IsExpressionEnd())
        {
            SkipType(TypeEnd.AsCast);
            return;
        }

        Emit(word);
        Mark(IsNonValueKeyword(word) ? Kind.Keyword : Kind.Ident, word);
    }

    private void BeginDeclaration(string keyword)
    {
        Emit(keyword);
        Emit(" ");
        ConsumeWhitespace(emit: false);
        EmitBindingPattern();
        ConsumeWhitespace(emit: false);
        if (Peek == ':')
        {
            _index++;
            SkipType(TypeEnd.Binding);
            ConsumeWhitespace(emit: false);
        }

        if (_index < _source.Length && !char.IsWhiteSpace(Peek) && Peek is not (';' or ',' or ')'))
            Emit(" ");
    }

    private void EmitFunction()
    {
        Emit("function");
        ConsumeWhitespace(emit: false);
        if (IsIdentStart(Peek))
        {
            var name = ReadIdent();
            Emit(" ");
            Emit(name);
            if (_nextFunctionIsDefault)
            {
                _entryPoint = name;
                _nextFunctionIsDefault = false;
            }

            ConsumeWhitespace(emit: false);
            if (Peek == '<')
                SkipAngles();
        }
        else if (_nextFunctionIsDefault)
        {
            Emit(" __canvasDefault");
            _entryPoint = "__canvasDefault";
            _nextFunctionIsDefault = false;
        }

        ConsumeWhitespace(emit: false);
        if (Peek != '(')
            throw Fail("Expected function parameters");

        Emit("(");
        _index++;
        _paren++;
        EmitParams();
        ConsumeWhitespace(emit: false);
        if (Peek == ':')
        {
            _index++;
            SkipType(TypeEnd.Return);
        }

        Mark(Kind.Close, ")");
    }

    private void EmitParams()
    {
        var depth = _paren;
        while (_index < _source.Length)
        {
            ConsumeWhitespace(emit: true);
            if (Peek == ')')
            {
                Emit(")");
                _index++;
                _paren--;
                return;
            }

            if (Peek == ',')
            {
                Emit(",");
                _index++;
                continue;
            }

            if (Peek == '.' && PeekAt(1) == '.' && PeekAt(2) == '.')
            {
                Emit("...");
                _index += 3;
            }

            EmitBindingPattern();
            ConsumeWhitespace(emit: true);
            if (Peek == '?')
            {
                var afterQuestion = SkipWhitespaceIndex(_index + 1);
                if (afterQuestion < _source.Length && _source[afterQuestion] == ':')
                {
                    _index++;
                    ConsumeWhitespace(emit: true);
                }
            }

            if (Peek == ':')
            {
                _index++;
                SkipType(TypeEnd.Binding);
                ConsumeWhitespace(emit: true);
            }

            if (Peek == '=')
            {
                Emit("=");
                _index++;
                WalkUntil(() => _paren == depth && Peek is ',' or ')');
            }
        }

        throw Fail("Unclosed parameter list");
    }

    private void EmitBindingPattern()
    {
        ConsumeWhitespace(emit: true);
        if (Peek == '{')
        {
            CopyBalanced('{', '}');
            Mark(Kind.Close, "}");
            return;
        }

        if (Peek == '[')
        {
            CopyBalanced('[', ']');
            Mark(Kind.Close, "]");
            return;
        }

        if (!IsIdentStart(Peek))
            throw Fail("Expected a binding name");

        var name = ReadIdent();
        Emit(name);
        Mark(Kind.Ident, name);
    }

    private void WalkUntil(Func<bool> stop)
    {
        while (_index < _source.Length)
        {
            var before = _index;
            ConsumeWhitespace(emit: true);
            if (_index >= _source.Length || stop())
                return;
            Step();
            if (_index == before)
                throw Fail("Parser did not advance");
        }
    }

    private void HandleExport()
    {
        ConsumeWhitespace(emit: false);
        if (PeekWord("default"))
        {
            ReadIdent();
            ConsumeWhitespace(emit: false);
            if (PeekWord("function"))
            {
                _nextFunctionIsDefault = true;
                return;
            }

            if (!IsIdentStart(Peek))
                throw Fail("Expected a default export");

            _entryPoint = ReadIdent();
            ConsumeWhitespace(emit: false);
            if (Peek == ';')
                _index++;
            return;
        }

        if (PeekWord("type"))
        {
            ReadIdent();
            SkipTypeAlias();
            return;
        }

        if (PeekWord("interface"))
        {
            ReadIdent();
            SkipInterface();
            return;
        }
    }

    private void SkipImport()
    {
        while (_index < _source.Length)
        {
            if (Peek is '"' or '\'' or '`')
            {
                if (Peek == '`')
                    SkipTemplateRaw();
                else
                    SkipQuoted();
                continue;
            }

            if (Peek == '/' && PeekAt(1) == '/')
            {
                SkipLineComment();
                continue;
            }

            if (Peek == '/' && PeekAt(1) == '*')
            {
                SkipBlockComment();
                continue;
            }

            if (Peek == ';')
            {
                _index++;
                return;
            }

            _index++;
        }
    }

    private void SkipTypeAlias()
    {
        var depth = 0;
        while (_index < _source.Length)
        {
            if (Peek is '"' or '\'' or '`')
            {
                if (Peek == '`')
                    SkipTemplateRaw();
                else
                    SkipQuoted();
                continue;
            }

            if (Peek == '/' && PeekAt(1) == '/')
            {
                SkipLineComment();
                continue;
            }

            if (Peek == '/' && PeekAt(1) == '*')
            {
                SkipBlockComment();
                continue;
            }

            var current = Peek;
            if (current is '(' or '[' or '{')
                depth++;
            else if (current is ')' or ']' or '}')
                depth--;
            else if (current == ';' && depth == 0)
            {
                _index++;
                return;
            }

            _index++;
        }
    }

    private void SkipInterface()
    {
        while (_index < _source.Length && Peek != '{')
            _index++;
        if (Peek == '{')
            SkipBalanced('{', '}');
        ConsumeWhitespace(emit: false);
        if (Peek == ';')
            _index++;
    }

    private bool LooksLikeTypeAlias()
    {
        var cursor = SkipWhitespaceIndex(_index);
        if (cursor >= _source.Length || !IsIdentStart(_source[cursor]))
            return false;
        cursor++;
        while (cursor < _source.Length && IsIdentPart(_source[cursor]))
            cursor++;
        cursor = SkipWhitespaceIndex(cursor);
        return cursor < _source.Length && _source[cursor] is '=' or '<';
    }

    private void SkipType(TypeEnd end)
    {
        while (_index < _source.Length)
        {
            ConsumeWhitespace(emit: false);
            if (_index >= _source.Length || ShouldStopType(end))
                return;

            var current = Peek;
            if (current is '|' or '&' or '?')
            {
                if (current == '?' && PeekAt(1) == '.')
                    return;
                _index++;
                continue;
            }

            if (IsIdentStart(current))
            {
                ReadIdent();
                ConsumeWhitespace(emit: false);
                while (Peek == '.')
                {
                    _index++;
                    ConsumeWhitespace(emit: false);
                    if (IsIdentStart(Peek))
                        ReadIdent();
                }

                if (Peek == '<')
                    SkipAngles();
                while (Peek == '[')
                {
                    if (PeekAt(1) == ']')
                        _index += 2;
                    else
                        SkipBalanced('[', ']');
                }

                continue;
            }

            if (current is '"' or '\'')
            {
                SkipQuoted();
                continue;
            }

            if (current == '`')
            {
                SkipTemplateRaw();
                continue;
            }

            if (current == '(')
            {
                SkipBalanced('(', ')');
                ConsumeWhitespace(emit: false);
                if (Peek == '=' && PeekAt(1) == '>')
                    _index += 2;
                continue;
            }

            if (current == '{')
            {
                if (end == TypeEnd.Return)
                {
                    if (!ReturnTypeContinues())
                        return;
                }
                else if (end != TypeEnd.Binding)
                {
                    return;
                }

                SkipBalanced('{', '}');
                continue;
            }

            if (current == '[')
            {
                SkipBalanced('[', ']');
                continue;
            }

            return;
        }
    }

    private bool ReturnTypeContinues()
    {
        var cursor = ScanMatching(_index, '{', '}');
        if (cursor < 0)
            return false;

        cursor = SkipWhitespaceIndex(cursor + 1);
        while (cursor + 1 < _source.Length && _source[cursor] == '[' && _source[cursor + 1] == ']')
        {
            cursor = SkipWhitespaceIndex(cursor + 2);
        }

        if (cursor + 1 < _source.Length && _source[cursor] == '=' && _source[cursor + 1] == '>')
            return true;

        return cursor < _source.Length && _source[cursor] == '{';
    }

    private bool ShouldStopType(TypeEnd end)
    {
        if (Peek == '=' && PeekAt(1) == '>')
            return true;

        return end switch
        {
            TypeEnd.Binding => Peek is '=' or ',' or ')' or ';',
            TypeEnd.Return => Peek is ';',
            _ => Peek is ',' or ';' or ')' or ']' or '}' or '.' or '=' or '?' or ':' or '{' or '+' or '-' or '*' or '/' or '!' or '<' or '>',
        };
    }

    private string ParseJsx()
    {
        if (Peek != '<')
            throw Fail("Expected JSX");
        _index++;
        if (Peek == '>')
        {
            _index++;
            var fragmentChildren = ParseChildren(null);
            return "__h(__Fragment,null" + ChildArgs(fragmentChildren) + ")";
        }

        var tag = ReadJsxName();
        var (props, selfClosing) = ParseProps();
        var tagExpr = tag.Length > 0 && char.IsLower(tag[0]) ? JsString(tag) : tag;
        if (selfClosing)
            return $"__h({tagExpr},{props})";

        var children = ParseChildren(tag);
        return $"__h({tagExpr},{props}{ChildArgs(children)})";
    }

    private (string Props, bool SelfClosing) ParseProps()
    {
        var parts = new List<string>();
        var batch = new List<string>();
        var spread = false;

        void Flush()
        {
            if (batch.Count == 0)
                return;
            parts.Add("{" + string.Join(",", batch) + "}");
            batch.Clear();
        }

        while (_index < _source.Length)
        {
            ConsumeWhitespace(emit: false);
            if (Peek == '/' && PeekAt(1) == '>')
            {
                _index += 2;
                Flush();
                var self = parts.Count == 0
                    ? "null"
                    : spread
                        ? "__props(" + string.Join(",", parts) + ")"
                        : parts[0];
                return (self, true);
            }

            if (Peek == '>')
            {
                _index++;
                Flush();
                var open = parts.Count == 0
                    ? "null"
                    : spread
                        ? "__props(" + string.Join(",", parts) + ")"
                        : parts[0];
                return (open, false);
            }

            if (Peek == '{')
            {
                _index++;
                ConsumeWhitespace(emit: false);
                if (!(Peek == '.' && PeekAt(1) == '.' && PeekAt(2) == '.'))
                    throw Fail("Expected a JSX spread");
                _index += 3;
                spread = true;
                Flush();
                var spreadDepth = _brace;
                var expr = Capture(() => WalkUntil(() => _brace == spreadDepth && Peek == '}'));
                if (Peek == '}')
                    _index++;
                parts.Add("{__spread:true,value:(" + expr + ")}");
                continue;
            }

            if (!IsIdentStart(Peek))
                throw Fail("Expected a JSX attribute");

            var name = ReadJsxName();
            ConsumeWhitespace(emit: false);
            string value;
            if (Peek == '=')
            {
                _index++;
                ConsumeWhitespace(emit: false);
                if (Peek is '"' or '\'')
                    value = JsString(Unquote(ReadQuoted()));
                else if (Peek == '{')
                {
                    _index++;
                    var valueDepth = _brace;
                    var savedLast = _last;
                    var savedKind = _kind;
                    _last = "{";
                    _kind = Kind.Keyword;
                    value = "(" + Capture(() => WalkUntil(() => _brace == valueDepth && Peek == '}')) + ")";
                    _last = savedLast;
                    _kind = savedKind;
                    if (Peek == '}')
                        _index++;
                }
                else
                    throw Fail("Expected a JSX attribute value");
            }
            else
                value = "true";

            batch.Add(JsKey(name) + ":" + value);
        }

        throw Fail("Unclosed JSX tag");
    }

    private string ParseChildren(string? closingTag)
    {
        var parts = new List<string>();
        while (_index < _source.Length)
        {
            if (Peek == '<' && PeekAt(1) == '/')
            {
                _index += 2;
                var name = Peek == '>' ? "" : ReadJsxName();
                ConsumeWhitespace(emit: false);
                if (Peek != '>')
                    throw Fail("Expected '>'");
                _index++;
                if (closingTag is null)
                {
                    if (name.Length != 0)
                        throw Fail("Mismatched fragment close");
                }
                else if (!string.Equals(name, closingTag, StringComparison.Ordinal))
                    throw Fail("Mismatched </" + name + ">");

                return string.Join(",", parts);
            }

            if (Peek == '<')
            {
                parts.Add(ParseJsx());
                continue;
            }

            if (Peek == '{')
            {
                _index++;
                var childDepth = _brace;
                var savedLast = _last;
                var savedKind = _kind;
                _last = "{";
                _kind = Kind.Keyword;
                var expr = Capture(() => WalkUntil(() => _brace == childDepth && Peek == '}'));
                _last = savedLast;
                _kind = savedKind;
                if (Peek == '}')
                    _index++;
                if (!string.IsNullOrWhiteSpace(expr))
                    parts.Add("(" + expr + ")");
                continue;
            }

            var text = ReadJsxText();
            var collapsed = Collapse(text);
            if (collapsed.Length > 0)
                parts.Add(JsString(collapsed));
        }

        throw Fail("Unclosed JSX element");
    }

    private static string ChildArgs(string children)
        => children.Length == 0 ? "" : "," + children;

    private string ReadJsxText()
    {
        var start = _index;
        while (_index < _source.Length && Peek is not ('<' or '{'))
            _index++;
        return _source[start.._index];
    }

    private static string Collapse(string text)
    {
        var leading = text.Length > 0 && char.IsWhiteSpace(text[0]);
        var trailing = text.Length > 0 && char.IsWhiteSpace(text[^1]);
        var builder = new StringBuilder(text.Length);
        var pending = false;
        var any = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                pending = any;
                continue;
            }

            if (pending)
                builder.Append(' ');
            builder.Append(ch);
            pending = false;
            any = true;
        }

        if (builder.Length == 0)
            return "";
        if (leading)
            builder.Insert(0, ' ');
        if (trailing)
            builder.Append(' ');
        return builder.ToString();
    }

    private bool IsJsxStart()
    {
        var next = PeekAt(1);
        if (next == '=' || next == '<')
            return false;
        if (!(char.IsLetter(next) || next is '_' or '>' || next == '/'))
            return false;
        if (next == '/')
            return false;
        return IsExpressionPosition();
    }

    private bool IsExpressionPosition()
        => _last is "" or "(" or "[" or "," or "{" or "=" or "?" or ":" or "=>" or "&&" or "||" or "??" or "..." or "return" or "throw" or "+" or "-" or "!" or "~" or "*" or "/" or "%";

    private bool FollowedByArrow(int openIndex)
    {
        if (openIndex >= _source.Length || _source[openIndex] != '(')
            return false;

        var cursor = ScanMatching(openIndex, '(', ')');
        if (cursor < 0)
            return false;
        cursor++;
        cursor = SkipWhitespaceIndex(cursor);
        if (cursor + 1 < _source.Length && _source[cursor] == '=' && _source[cursor + 1] == '>')
            return true;
        if (cursor >= _source.Length || _source[cursor] != ':')
            return false;

        var paren = 0;
        var brace = 0;
        var bracket = 0;
        for (var i = cursor + 1; i + 1 < _source.Length; i++)
        {
            var ch = _source[i];
            if (ch is '"' or '\'' or '`')
            {
                i = SkipStringIndex(i);
                continue;
            }

            if (ch == '/' && i + 1 < _source.Length && _source[i + 1] == '/')
            {
                while (i < _source.Length && _source[i] != '\n')
                    i++;
                continue;
            }

            if (paren == 0 && brace == 0 && bracket == 0 && ch == '=' && _source[i + 1] == '>')
                return true;
            if (paren == 0 && brace == 0 && bracket == 0 && ch is '{' or ';')
                return false;
            switch (ch)
            {
                case '(': paren++; break;
                case ')': paren--; break;
                case '{': brace++; break;
                case '}': brace--; break;
                case '[': bracket++; break;
                case ']': bracket--; break;
            }
        }

        return false;
    }

    private bool TryEmitNumber()
    {
        var dotted = Peek == '.' && char.IsDigit(PeekAt(1)) && _kind is not (Kind.Ident or Kind.Number or Kind.String or Kind.Close);
        if (!dotted && !char.IsDigit(Peek))
            return false;
        if (!dotted && _kind == Kind.Ident && _last == ".")
            return false;

        var builder = new StringBuilder();
        if (Peek == '.')
        {
            builder.Append('.');
            _index++;
        }

        while (char.IsDigit(Peek) || Peek == '_')
        {
            if (Peek != '_')
                builder.Append(Peek);
            _index++;
        }

        if (Peek == '.' && char.IsDigit(PeekAt(1)))
        {
            builder.Append('.');
            _index++;
            while (char.IsDigit(Peek) || Peek == '_')
            {
                if (Peek != '_')
                    builder.Append(Peek);
                _index++;
            }
        }

        if (Peek is 'e' or 'E')
        {
            builder.Append(Peek);
            _index++;
            if (Peek is '+' or '-')
            {
                builder.Append(Peek);
                _index++;
            }

            while (char.IsDigit(Peek) || Peek == '_')
            {
                if (Peek != '_')
                    builder.Append(Peek);
                _index++;
            }
        }

        Emit(builder.ToString());
        Mark(Kind.Number, "0");
        return true;
    }

    private void EmitTemplate()
    {
        Emit("`");
        _index++;
        while (_index < _source.Length)
        {
            if (Peek == '\\')
            {
                Emit(Peek.ToString());
                Emit(PeekAt(1).ToString());
                _index += 2;
                continue;
            }

            if (Peek == '`')
            {
                Emit("`");
                _index++;
                return;
            }

            if (Peek == '$' && PeekAt(1) == '{')
            {
                Emit("${");
                _index += 2;
                var depth = _brace;
                WalkUntil(() => _brace == depth && Peek == '}');
                Emit("}");
                if (Peek == '}')
                    _index++;
                continue;
            }

            Emit(Peek.ToString());
            _index++;
        }

        throw Fail("Unclosed template");
    }

    private void CopyBalanced(char open, char close)
    {
        if (Peek != open)
            throw Fail("Expected '" + open + "'");
        var depth = 0;
        while (_index < _source.Length)
        {
            if (Peek is '"' or '\'')
            {
                EmitRaw(ReadQuoted());
                continue;
            }

            if (Peek == '`')
            {
                EmitTemplate();
                continue;
            }

            if (Peek == '/' && PeekAt(1) == '/')
            {
                SkipLineComment();
                continue;
            }

            if (Peek == '/' && PeekAt(1) == '*')
            {
                SkipBlockComment();
                continue;
            }

            var current = Peek;
            Emit(current.ToString());
            _index++;
            if (current == open)
                depth++;
            else if (current == close && --depth == 0)
                return;
        }

        throw Fail("Unclosed '" + open + "'");
    }

    private void SkipBalanced(char open, char close)
    {
        if (Peek != open)
            throw Fail("Expected '" + open + "'");
        var depth = 0;
        while (_index < _source.Length)
        {
            if (Peek is '"' or '\'')
            {
                SkipQuoted();
                continue;
            }

            if (Peek == '`')
            {
                SkipTemplateRaw();
                continue;
            }

            if (Peek == '/' && PeekAt(1) == '/')
            {
                SkipLineComment();
                continue;
            }

            if (Peek == '/' && PeekAt(1) == '*')
            {
                SkipBlockComment();
                continue;
            }

            var current = Peek;
            _index++;
            if (current == open)
                depth++;
            else if (current == close && --depth == 0)
                return;
        }

        throw Fail("Unclosed '" + open + "'");
    }

    private void SkipAngles()
    {
        if (Peek != '<')
            return;
        var depth = 0;
        while (_index < _source.Length)
        {
            if (Peek is '"' or '\'')
            {
                SkipQuoted();
                continue;
            }

            if (Peek == '<')
            {
                depth++;
                _index++;
                continue;
            }

            if (Peek == '>')
            {
                depth--;
                _index++;
                if (depth == 0)
                    return;
                continue;
            }

            _index++;
        }
    }

    private int ScanMatching(int openIndex, char open, char close)
    {
        var depth = 0;
        for (var i = openIndex; i < _source.Length; i++)
        {
            var ch = _source[i];
            if (ch is '"' or '\'' or '`')
            {
                i = SkipStringIndex(i);
                continue;
            }

            if (ch == '/' && i + 1 < _source.Length && _source[i + 1] == '/')
            {
                while (i < _source.Length && _source[i] != '\n')
                    i++;
                continue;
            }

            if (ch == open)
                depth++;
            else if (ch == close && --depth == 0)
                return i;
        }

        return -1;
    }

    private string Capture(Action emit)
    {
        var previous = _output;
        _output = new StringBuilder();
        emit();
        var text = _output.ToString();
        _output = previous;
        return text;
    }

    private void ConsumeWhitespace(bool emit)
    {
        while (_index < _source.Length)
        {
            if (char.IsWhiteSpace(Peek))
            {
                if (emit)
                    Emit(Peek.ToString());
                _index++;
                continue;
            }

            if (Peek == '/' && PeekAt(1) == '/')
            {
                SkipLineComment();
                if (emit)
                    Emit("\n");
                continue;
            }

            if (Peek == '/' && PeekAt(1) == '*')
            {
                SkipBlockComment();
                if (emit)
                    Emit(" ");
                continue;
            }

            return;
        }
    }

    private void SkipLineComment()
    {
        while (_index < _source.Length && Peek != '\n')
            _index++;
    }

    private void SkipBlockComment()
    {
        _index += 2;
        while (_index + 1 < _source.Length && !(Peek == '*' && PeekAt(1) == '/'))
            _index++;
        if (_index + 1 < _source.Length)
            _index += 2;
    }

    private string ReadQuoted()
    {
        var quote = Peek;
        var start = _index;
        _index++;
        while (_index < _source.Length)
        {
            if (Peek == '\\')
            {
                _index += 2;
                continue;
            }

            if (Peek == quote)
            {
                _index++;
                return _source[start.._index];
            }

            _index++;
        }

        throw Fail("Unclosed string");
    }

    private void SkipQuoted() => ReadQuoted();

    private void SkipTemplateRaw()
    {
        _index++;
        while (_index < _source.Length)
        {
            if (Peek == '\\')
            {
                _index += 2;
                continue;
            }

            if (Peek == '`')
            {
                _index++;
                return;
            }

            if (Peek == '$' && PeekAt(1) == '{')
            {
                _index += 2;
                var depth = 1;
                while (_index < _source.Length && depth > 0)
                {
                    if (Peek is '"' or '\'')
                    {
                        SkipQuoted();
                        continue;
                    }

                    if (Peek == '{')
                        depth++;
                    else if (Peek == '}')
                        depth--;
                    _index++;
                }

                continue;
            }

            _index++;
        }
    }

    private int SkipStringIndex(int index)
    {
        var quote = _source[index];
        index++;
        if (quote != '`')
        {
            while (index < _source.Length)
            {
                if (_source[index] == '\\')
                {
                    index += 2;
                    continue;
                }

                if (_source[index] == quote)
                    return index;
                index++;
            }

            return index;
        }

        while (index < _source.Length && _source[index] != '`')
        {
            if (_source[index] == '\\')
                index += 2;
            else
                index++;
        }

        return index;
    }

    private string ReadIdent()
    {
        var start = _index;
        _index++;
        while (_index < _source.Length && IsIdentPart(Peek))
            _index++;
        return _source[start.._index];
    }

    private string ReadJsxName()
    {
        var start = _index;
        if (!IsIdentStart(Peek))
            throw Fail("Expected a JSX name");
        _index++;
        while (_index < _source.Length && (IsIdentPart(Peek) || Peek is '-' or ':' or '.'))
            _index++;
        return _source[start.._index];
    }

    private bool PeekWord(string word)
    {
        var cursor = SkipWhitespaceIndex(_index);
        if (cursor + word.Length > _source.Length)
            return false;
        if (!string.Equals(_source.Substring(cursor, word.Length), word, StringComparison.Ordinal))
            return false;
        var after = cursor + word.Length;
        return after >= _source.Length || !IsIdentPart(_source[after]);
    }

    private bool NextNonWhiteIs(char expected)
    {
        var cursor = SkipWhitespaceIndex(_index);
        return cursor < _source.Length && _source[cursor] == expected;
    }

    private int SkipWhitespaceIndex(int index)
    {
        while (index < _source.Length && char.IsWhiteSpace(_source[index]))
            index++;
        return index;
    }

    private bool IsBoundary()
        => _kind == Kind.None || _last is ";" or "{" or "}";

    private bool IsExpressionEnd()
        => _kind is Kind.Ident or Kind.Number or Kind.String or Kind.Close;

    private static bool IsNonValueKeyword(string word)
        => word is "return" or "throw" or "case" or "new" or "typeof" or "void" or "in" or "of"
            or "instanceof" or "else" or "const" or "let" or "var" or "function" or "if" or "for"
            or "while" or "switch" or "do" or "class" or "extends" or "yield" or "await" or "delete"
            or "import" or "export" or "type" or "interface" or "default" or "from";

    private static bool IsIdentStart(char ch)
        => char.IsLetter(ch) || ch is '_' or '$';

    private static bool IsIdentPart(char ch)
        => IsIdentStart(ch) || char.IsDigit(ch);

    private static string JsString(string text)
        => JsonSerializer.Serialize(text);

    private static string JsKey(string name)
        => name.All(ch => IsIdentPart(ch) && ch != '-') ? name : JsString(name);

    private static string Unquote(string quoted)
    {
        if (quoted.Length < 2)
            return quoted;

        var body = quoted[1..^1];
        var builder = new StringBuilder(body.Length);
        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] != '\\' || i + 1 >= body.Length)
            {
                builder.Append(body[i]);
                continue;
            }

            var next = body[++i];
            builder.Append(next switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => next,
            });
        }

        return builder.ToString();
    }

    private void Mark(Kind kind, string token)
    {
        _kind = kind;
        _last = token;
    }

    private void Emit(string text) => _output.Append(text);

    private void EmitRaw(string text) => _output.Append(text);

    private char Peek => _index < _source.Length ? _source[_index] : '\0';

    private char PeekAt(int offset)
        => _index + offset < _source.Length ? _source[_index + offset] : '\0';

    private CanvasRenderException Fail(string message)
    {
        var line = 1;
        var column = 1;
        var end = Math.Min(_index, _source.Length);
        for (var i = 0; i < end; i++)
        {
            if (_source[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
                column++;
        }

        return new CanvasRenderException(message + " at line " + line + " column " + column);
    }
}
