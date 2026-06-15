using System;

namespace Acai.src;

public enum TokenType
{
    Keyword,
    Identifier,
    String,
    Number,
    Operator,
    Unknown
}

public struct Token
{
    public TokenType Type;
    public string Value;
    public bool IsRaw;

    public Token(TokenType type, string value, bool isRaw = false)
    {
        Type = type;
        Value= value;
        IsRaw = isRaw;
    }
}