using System;
using System.Collections.Generic;
using System.Text;

namespace Acai.src;

public class Lexer
{
    private readonly string _source;
    private int _position;
    private readonly List<Token> _tokens = new();

    public Lexer(string source)
    {
        _source = source.Replace("\r", string.Empty);
        _position = 0;
    }

    public IReadOnlyList<Token> Tokenize()
    {
        while (!IsAtEnd())
        {
            SkipWhitespace();
            if (IsAtEnd())
            {
                break;
            }

            var current = Peek();

            if ((current == 'r' || current == 'R') && PeekNext() == '"')
            {
                ReadRawString();
                continue;
            }

            if (char.IsLetter(current))
            {
                ReadWord();
                continue;
            }

            if (char.IsDigit(current))
            {
                ReadNumber();
                continue;
            }

            if (current == '"')
            {
                ReadString();
                continue;
            }

            if (IsOperatorStart(current))
            {
                ReadOperator();
                continue;
            }

            Advance();
        }

        return _tokens;
    }

    private bool IsAtEnd() => _position >= _source.Length;

    private char Peek() => IsAtEnd() ? '\0' : _source[_position];

    private char PeekNext() => _position + 1 >= _source.Length ? '\0' : _source[_position + 1];

    private char Advance() => IsAtEnd() ? '\0' : _source[_position++];

    private void SkipWhitespace()
    {
        while (!IsAtEnd())
        {
            var c = Peek();
            if (c == ' ' || c == '\t' || c == '\n')
            {
                Advance();
                continue;
            }
            break;
        }
    }

    private void ReadWord()
    {
        var start = _position;
        while (char.IsLetterOrDigit(Peek()) || Peek() == '_')
        {
            Advance();
        }

        var value = _source[start.._position];
        var lower = value.ToLowerInvariant();

        if (lower is "show" or "set" or "to" or "if" or "then" or "else" or "end" or "true" or "false" or "repeat" or "until" or "while" or "continue" or "stop" or "break" or "for" or "from" or "step" or "make" or "function" or "call" or "return" or "use" or "class")
        {
            _tokens.Add(new Token(TokenType.Keyword, lower));
            return;
        }

        _tokens.Add(new Token(TokenType.Identifier, value));
    }

    private void ReadNumber()
    {
        var start = _position;
        while (char.IsDigit(Peek()))
        {
            Advance();
        }

        if (Peek() == '.' && char.IsDigit(PeekNext()))
        {
            Advance();
            while (char.IsDigit(Peek()))
            {
                Advance();
            }
        }

        var value = _source[start.._position];
        _tokens.Add(new Token(TokenType.Number, value));
    }

    private void ReadString()
    {
        Advance();
        var builder = new StringBuilder();

        while (!IsAtEnd() && Peek() != '"')
        {
            if (Peek() == '\\')
            {
                Advance();
                var escape = Peek();
                switch (escape)
                {
                    case 'n': builder.Append('\n'); break;
                    case 't': builder.Append('\t'); break;
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    default: builder.Append(escape); break;
                }
                Advance();
                continue;
            }

            builder.Append(Advance());
        }

        if (Peek() == '"')
        {
            Advance();
        }

        _tokens.Add(new Token(TokenType.String, builder.ToString(), isRaw: false));
    }

    private void ReadRawString()
    {
        Advance();
        Advance();
        var builder = new StringBuilder();

        while (!IsAtEnd() && Peek() != '"')
        {
            builder.Append(Advance());
        }

        if (Peek() == '"')
        {
            Advance();
        }

        _tokens.Add(new Token(TokenType.String, builder.ToString(), isRaw: true));
    }

    private bool IsOperatorStart(char c) => "+-*/=<>!(),.".IndexOf(c) >= 0;

    private void ReadOperator()
    {
        var current = Advance();
        var next = Peek();
        var op = current.ToString();

        if ((current == '=' || current == '!' || current == '<' || current == '>') && next == '=')
        {
            op += Advance();
        }

        _tokens.Add(new Token(TokenType.Operator, op));
    }
}
