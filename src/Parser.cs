using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Acai.src;

public class ReturnException : Exception
{
    public object? Value { get; }
    public ReturnException(object? value) { Value = value; }
}
public class ContinueException : Exception { }
public class BreakException : Exception { }

public sealed class UserFunction
{
    public string Name { get; }
    public List<string> Parameters { get; }
    public List<Token> Body { get; }
    public UserFunction(string name, IEnumerable<string> parameters, IEnumerable<Token> body)
    {
        Name = name; Parameters = parameters.ToList(); Body = body.ToList();
    }
}

public sealed class UserClass
{
    public string Name { get; }
    public List<(string Name, List<Token>? DefaultTokens)> Parameters { get; }
    public List<Token> Body { get; }
    public UserClass(string name, IEnumerable<(string, List<Token>?)> parameters, IEnumerable<Token> body)
    {
        Name = name; Parameters = parameters.ToList(); Body = body.ToList();
    }
}

public class Parser
{
    private readonly List<Token> _tokens;
    private int _position;

    internal readonly Dictionary<string, object> _variables;
    private readonly Dictionary<string, object>? _outerVariables;
    internal readonly Dictionary<string, UserFunction> _functions;
    internal readonly Dictionary<string, UserClass> _classes;
    private readonly string? _basePath;

    public Parser(IReadOnlyList<Token> tokens, string? basePath = null)
        : this(tokens, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, UserFunction>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, UserClass>(StringComparer.OrdinalIgnoreCase), null, basePath)
    { }

    private Parser(IReadOnlyList<Token> tokens, Dictionary<string, object> variables, Dictionary<string, UserFunction> functions, Dictionary<string, UserClass> classes, Dictionary<string, object>? outerVariables, string? basePath)
    {
        _tokens = tokens.Where(t => t.Type != TokenType.Unknown).ToList();
        _position = 0;
        _variables = variables;
        _outerVariables = outerVariables;
        _functions = functions;
        _classes = classes;
        _basePath = basePath;
    }

    public void ParseAndExecute()
    {
        while (!IsAtEnd()) ParseStatement();
    }

    private void ParseStatement()
    {
        if (MatchKeyword("use")) { ParseUseStatement(); return; }
        if (MatchKeyword("show")) { var value = EvaluateExpression(); Console.WriteLine(value?.ToString() ?? string.Empty); return; }
        if (MatchKeyword("set")) { ParseAssignment(); return; }
        if (MatchKeyword("if")) { ParseIfStatement(); return; }
        if (MatchKeyword("repeat")) { ParseRepeatStatement(); return; }
        if (MatchKeyword("for")) { ParseForStatement(); return; }
        if (MatchKeyword("make")) { ParseMakeDefinition(); return; }
        if (MatchKeyword("call")) { EvaluateCallExpression(); return; }
        if (MatchKeyword("continue")) throw new ContinueException();
        if (MatchKeyword("stop") || MatchKeyword("break")) throw new BreakException();
        if (MatchKeyword("return")) { var value = EvaluateExpression(); throw new ReturnException(value); }

        throw new InvalidOperationException($"Unexpected statement starting with '{CurrentToken().Value}'.");
    }

    private void ParseUseStatement()
    {
        string path;
        if (Match(TokenType.String)) path = PreviousToken().Value;
        else path = Consume(TokenType.Identifier, "Expected file name after 'use'.").Value;
        if (!path.EndsWith(".acai", StringComparison.OrdinalIgnoreCase)) path += ".acai";
        var baseDir = _basePath ?? Directory.GetCurrentDirectory();
        var full = Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path);
        if (!File.Exists(full)) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"Error: Imported file '{full}' not found."); Console.ResetColor(); return; }
        var source = File.ReadAllText(full);
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var imported = new Parser(tokens, Path.GetDirectoryName(full));
        // share same dictionaries so definitions persist
        imported._functions.Clear(); foreach (var kvp in _functions) imported._functions[kvp.Key] = kvp.Value;
        imported._classes.Clear(); foreach (var kvp in _classes) imported._classes[kvp.Key] = kvp.Value;
        imported._variables.Clear(); foreach (var kvp in _variables) imported._variables[kvp.Key] = kvp.Value;
        imported.ParseAndExecute();
        foreach (var kvp in imported._functions) _functions[kvp.Key] = kvp.Value;
        foreach (var kvp in imported._classes) _classes[kvp.Key] = kvp.Value;
        foreach (var kvp in imported._variables) _variables[kvp.Key] = kvp.Value;
    }

    private void ParseAssignment()
    {
        var nameToken = Consume(TokenType.Identifier, "Expected variable name after 'set'.");
        var variableName = nameToken.Value;

        // support property assignment: set self.name to value
        if (MatchOperator("."))
        {
            var prop = Consume(TokenType.Identifier, "Expected property name after '.'.").Value;
            if (MatchKeyword("to") || MatchOperator("="))
            {
                var value = EvaluateExpression();
                if (!TryGetVariable(variableName, out var obj) || obj is not Dictionary<string, object?> dict)
                {
                    dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    _variables[variableName] = dict;
                }
                dict[prop] = value;
                return;
            }
        }

        if (MatchKeyword("to") || MatchOperator("=")) { var value = EvaluateExpression(); AssignVariable(variableName, value); return; }
        throw new InvalidOperationException("Expected 'to' or '=' after variable name in assignment.");
    }

    private void ParseIfStatement()
    {
        var conditionStart = _position;
        _ = EvaluateExpression();
        var conditionTokens = _tokens.GetRange(conditionStart, _position - conditionStart);
        ConsumeKeyword("then", "Expected 'then' after if condition.");

        var trueBranch = new List<Token>();
        var falseBranch = new List<Token>();
        var targetBranch = trueBranch;
        var depth = 1;

        while (!IsAtEnd() && depth > 0)
        {
            // handle 'else if' by peeking ahead when at depth 1
            if (CheckKeyword("else") && depth == 1 && CheckNextKeyword("if"))
            {
                // consume 'else'
                Advance();
                // consume 'if' and add it to the false branch
                Advance();
                targetBranch = falseBranch;
                targetBranch.Add(PreviousToken());
                depth++;
                continue;
            }

            if (MatchKeyword("if")) { targetBranch.Add(PreviousToken()); depth++; continue; }
            if (MatchKeyword("then")) { targetBranch.Add(PreviousToken()); continue; }
            if (MatchKeyword("else") && depth == 1) { targetBranch = falseBranch; continue; }
            if (MatchKeyword("end")) { depth--; if (depth == 0) break; targetBranch.Add(PreviousToken()); continue; }
            targetBranch.Add(Advance());
        }

        var conditionValue = EvaluateExpressionFromTokens(conditionTokens);
        var branchTokens = IsTrue(conditionValue) ? trueBranch : falseBranch;
        var branchParser = CreateChildParser(branchTokens);
        branchParser.ParseAndExecute();
        CopyVariablesFromChild(branchParser);
    }

    private void ParseRepeatStatement()
    {
        var isWhile = MatchKeyword("while");
        var isUntil = false;
        if (!isWhile) isUntil = MatchKeyword("until");
        if (!isWhile && !isUntil) throw new InvalidOperationException("Expected 'while' or 'until' after 'repeat'.");

        var conditionStart = _position;
        _ = EvaluateExpression();
        var conditionTokens = _tokens.GetRange(conditionStart, _position - conditionStart);
        MatchKeyword("then");
        var bodyTokens = CollectBlockTokens("repeat");

        while (true)
        {
            object? conditionValue = EvaluateExpressionFromTokens(conditionTokens);
            if (isWhile && !IsTrue(conditionValue)) break;
            if (isUntil && IsTrue(conditionValue)) break;

            var bodyParser = CreateChildParser(bodyTokens);
            try { bodyParser.ParseAndExecute(); }
            catch (ContinueException) { CopyVariablesFromChild(bodyParser); continue; }
            catch (BreakException) { CopyVariablesFromChild(bodyParser); break; }
            catch (ReturnException) { CopyVariablesFromChild(bodyParser); throw; }
            CopyVariablesFromChild(bodyParser);
        }
    }

    private void ParseForStatement()
    {
        var nameToken = Consume(TokenType.Identifier, "Expected loop variable name after 'for'.");
        var variableName = nameToken.Value;
        ConsumeKeyword("from", "Expected 'from' after loop variable.");

        var startStart = _position; _ = EvaluateExpression(); var startTokens = _tokens.GetRange(startStart, _position - startStart);
        ConsumeKeyword("to", "Expected 'to' after loop start value."); var endStart = _position; _ = EvaluateExpression(); var endTokens = _tokens.GetRange(endStart, _position - endStart);

        List<Token>? stepTokens = null;
        if (MatchKeyword("step")) { var stepStart = _position; _ = EvaluateExpression(); stepTokens = _tokens.GetRange(stepStart, _position - stepStart); }

        MatchKeyword("then"); var bodyTokens = CollectBlockTokens("for");

        var startValue = ToNumber(EvaluateExpressionFromTokens(startTokens));
        var endValue = ToNumber(EvaluateExpressionFromTokens(endTokens));
        var stepValue = stepTokens is null ? 1 : ToNumber(EvaluateExpressionFromTokens(stepTokens)); if (stepValue == 0) stepValue = 1;

        var current = startValue;
        while (true)
        {
            if (stepValue > 0 && current > endValue) break;
            if (stepValue < 0 && current < endValue) break;
            AssignVariable(variableName, current);
            var bodyParser = CreateChildParser(bodyTokens);
            try { bodyParser.ParseAndExecute(); }
            catch (ContinueException) { CopyVariablesFromChild(bodyParser); current += stepValue; continue; }
            catch (BreakException) { CopyVariablesFromChild(bodyParser); break; }
            catch (ReturnException) { CopyVariablesFromChild(bodyParser); throw; }
            CopyVariablesFromChild(bodyParser);
            current += stepValue;
        }
    }

    private void ParseMakeDefinition()
    {
        if (CheckKeyword("function")) { ConsumeKeyword("function", "Expected 'function' after 'make'."); ParseFunctionDefInner(); return; }
        if (CheckKeyword("class")) { ConsumeKeyword("class", "Expected 'class' after 'make'."); ParseClassDefInner(); return; }
        throw new InvalidOperationException("Expected 'function' or 'class' after 'make'.");
    }

    private void ParseFunctionDefInner()
    {
        var nameToken = Consume(TokenType.Identifier, "Expected function name."); var functionName = nameToken.Value;
        var parameters = new List<string>();
        if (MatchOperator("(")) { while (!MatchOperator(")")) { if (MatchOperator(",")) continue; var parameterToken = Consume(TokenType.Identifier, "Expected parameter name inside '()'."); parameters.Add(parameterToken.Value); } }
        else { while (!IsAtEnd() && !CheckKeyword("then") && !CheckKeyword("end")) { var parameterToken = Consume(TokenType.Identifier, "Expected parameter name."); parameters.Add(parameterToken.Value); } }
        MatchKeyword("then"); var bodyTokens = CollectBlockTokens("make"); _functions[functionName] = new UserFunction(functionName, parameters, bodyTokens);
    }

    private void ParseClassDefInner()
    {
        var nameToken = Consume(TokenType.Identifier, "Expected class name."); var className = nameToken.Value;
        var parameters = new List<(string, List<Token>?)>();
        if (MatchOperator("("))
        {
            while (!MatchOperator(")"))
            {
                if (MatchOperator(",")) continue;
                var paramName = Consume(TokenType.Identifier, "Expected parameter name inside '()'.").Value;
                List<Token>? defaultTokens = null;
                if (MatchOperator("="))
                {
                    var exprStart = _position;
                    while (!IsAtEnd() && !CheckOperator(",") && !CheckOperator(")")) Advance();
                    defaultTokens = _tokens.GetRange(exprStart, _position - exprStart);
                }
                parameters.Add((paramName, defaultTokens));
            }
        }
        MatchKeyword("then"); var bodyTokens = CollectBlockTokens("make"); _classes[className] = new UserClass(className, parameters, bodyTokens);
    }

    private object? EvaluateCallExpression()
    {
        var nameToken = Consume(TokenType.Identifier, "Expected function name after 'call'.");
        var functionName = nameToken.Value;
        var args = new List<object?>();
        if (MatchOperator("(")) { while (!MatchOperator(")")) { if (MatchOperator(",")) { continue; } args.Add(EvaluateExpression()); } }
        else
        {
            if (_functions.TryGetValue(functionName, out var function)) { foreach (var _ in function.Parameters) args.Add(EvaluateExpression()); }
            else if (_classes.ContainsKey(functionName)) { var cls = _classes[functionName]; foreach (var _ in cls.Parameters) args.Add(EvaluateExpression()); }
            else { if (!IsAtEnd() && (Check(TokenType.String) || Check(TokenType.Number) || Check(TokenType.Identifier) || CheckOperator("("))) { args.Add(EvaluateExpression()); } }
        }

        if (string.Equals(functionName, "input", StringComparison.OrdinalIgnoreCase)) { var prompt = args.Count > 0 ? args[0]?.ToString() ?? string.Empty : string.Empty; if (!string.IsNullOrEmpty(prompt)) Console.Write(prompt); var line = Console.ReadLine(); return line ?? string.Empty; }

        if (_classes.TryGetValue(functionName, out var classDef))
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < classDef.Parameters.Count; i++)
            {
                var (pname, defaultTokens) = classDef.Parameters[i];
                if (i < args.Count) values[pname] = args[i];
                else if (defaultTokens != null) values[pname] = EvaluateExpressionFromTokens(defaultTokens);
                else throw new InvalidOperationException($"Missing required parameter '{pname}' for class '{functionName}'.");
            }
            var instance = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in values) instance[kvp.Key] = kvp.Value;
            var bodyParser = CreateChildParser(classDef.Body);
            bodyParser._variables["self"] = instance;
            try { bodyParser.ParseAndExecute(); }
            catch (ReturnException) { }
            CopyVariablesFromChild(bodyParser);
            return instance;
        }

        if (!_functions.TryGetValue(functionName, out var userFunction)) throw new InvalidOperationException($"Function '{functionName}' is not defined.");
        return ExecuteFunction(userFunction, args);
    }

    private object? ExecuteFunction(UserFunction function, List<object?> arguments)
    {
        if (arguments.Count != function.Parameters.Count) throw new InvalidOperationException($"Function '{function.Name}' expects {function.Parameters.Count} arguments.");
        var localVars = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < function.Parameters.Count; i++) localVars[function.Parameters[i]] = arguments[i]!;
        var childParser = new Parser(function.Body, _basePath);
        // share functions and classes
        childParser._functions.Clear(); foreach (var kvp in _functions) childParser._functions[kvp.Key] = kvp.Value;
        childParser._classes.Clear(); foreach (var kvp in _classes) childParser._classes[kvp.Key] = kvp.Value;
        childParser._variables.Clear(); foreach (var kvp in localVars) childParser._variables[kvp.Key] = kvp.Value;
        try { childParser.ParseAndExecute(); }
        catch (ReturnException re) { return re.Value; }
        CopyVariablesFromChild(childParser);
        return null;
    }

    private object? EvaluateExpressionFromTokens(IReadOnlyList<Token> tokens)
    {
        var parser = new Parser(tokens, _basePath);
        parser._functions.Clear(); foreach (var kvp in _functions) parser._functions[kvp.Key] = kvp.Value;
        parser._classes.Clear(); foreach (var kvp in _classes) parser._classes[kvp.Key] = kvp.Value;
        parser._variables.Clear(); foreach (var kvp in _variables) parser._variables[kvp.Key] = kvp.Value;
        return parser.EvaluateExpression();
    }

    private List<Token> CollectBlockTokens(string blockKeyword)
    {
        var tokens = new List<Token>();
        var depth = 1;
        var nestedBlockKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "if", "repeat", "for", "make" };
        while (!IsAtEnd() && depth > 0)
        {
            if (Check(TokenType.Keyword) && nestedBlockKeywords.Contains(CurrentToken().Value)) { tokens.Add(Advance()); depth++; continue; }
            if (MatchKeyword("end")) { depth--; if (depth == 0) break; tokens.Add(PreviousToken()); continue; }
            tokens.Add(Advance());
        }
        return tokens;
    }

    private object? EvaluateExpression() => ParseEquality();

    private object? ParseEquality()
    {
        var left = ParseComparison();
        while (MatchOperator("==") || MatchOperator("!=")) { var op = PreviousToken().Value; var right = ParseComparison(); left = EvaluateComparison(left, right, op); }
        return left;
    }

    private object? ParseComparison()
    {
        var left = ParseTerm();
        while (MatchOperator("<") || MatchOperator("<=") || MatchOperator(">") || MatchOperator(">=")) { var op = PreviousToken().Value; var right = ParseTerm(); left = EvaluateComparison(left, right, op); }
        return left;
    }

    private object? ParseTerm()
    {
        var left = ParseFactor();
        while (MatchOperator("+") || MatchOperator("-")) { var op = PreviousToken().Value; var right = ParseFactor(); left = EvaluateArithmetic(left, right, op); }
        return left;
    }

    private object? ParseFactor()
    {
        var left = ParseUnary();
        while (MatchOperator("*") || MatchOperator("/")) { var op = PreviousToken().Value; var right = ParseUnary(); left = EvaluateArithmetic(left, right, op); }
        return left;
    }

    private object? ParseUnary()
    {
        if (MatchOperator("-")) { var right = ParseUnary(); return NegateValue(right); }
        return ParsePrimary();
    }

    private object? ParsePrimary()
    {
        if (Match(TokenType.Number)) { var token = PreviousToken(); return double.Parse(token.Value, CultureInfo.InvariantCulture); }
        if (Match(TokenType.String)) { var token = PreviousToken(); return token.IsRaw ? token.Value : EvaluateInterpolatedString(token.Value); }
        if (MatchKeyword("true")) return true;
        if (MatchKeyword("false")) return false;
        if (MatchKeyword("call")) return EvaluateCallExpression();
        if (Match(TokenType.Identifier))
        {
            var name = PreviousToken().Value;
            if (MatchOperator("."))
            {
                var prop = Consume(TokenType.Identifier, "Expected property name after '.'.").Value;
                if (TryGetVariable(name, out var container) && container is Dictionary<string, object?> dict && dict.TryGetValue(prop, out var valueVar)) return valueVar;
                return null;
            }
            return TryGetVariable(name, out var value) ? value : null;
        }
        if (MatchOperator("(")) { var expression = EvaluateExpression(); ConsumeOperator(")", "Expected ')' after expression."); return expression; }
        throw new InvalidOperationException($"Unable to parse expression at '{CurrentToken().Value}'.");
    }

    private object? EvaluateInterpolatedString(string template)
    {
        var builder = new StringBuilder();
        var index = 0;
        while (index < template.Length)
        {
            if (template[index] == '{')
            {
                var endIndex = template.IndexOf('}', index + 1);
                if (endIndex == -1) { builder.Append(template[index]); index++; continue; }
                var expressionText = template[(index + 1)..endIndex];
                var expressionTokens = new Lexer(expressionText).Tokenize();
                var expressionValue = EvaluateExpressionFromTokens(expressionTokens);
                builder.Append(expressionValue?.ToString() ?? string.Empty);
                index = endIndex + 1; continue;
            }
            builder.Append(template[index]); index++;
        }
        return builder.ToString();
    }

    private object? EvaluateArithmetic(object? left, object? right, string op)
    {
        if (op == "+")
        {
            if (left is string or null || right is string or null) return (left?.ToString() ?? string.Empty) + (right?.ToString() ?? string.Empty);
            return ToNumber(left) + ToNumber(right);
        }
        var leftNumber = ToNumber(left); var rightNumber = ToNumber(right);
        return op switch { "-" => leftNumber - rightNumber, "*" => leftNumber * rightNumber, "/" => rightNumber == 0 ? 0 : leftNumber / rightNumber, _ => throw new InvalidOperationException($"Unsupported arithmetic operator '{op}'.") };
    }

    private object EvaluateComparison(object? left, object? right, string op)
    {
        if (left is string || right is string)
        {
            var leftString = left?.ToString() ?? string.Empty; var rightString = right?.ToString() ?? string.Empty;
            return op switch { "==" => leftString == rightString, "!=" => leftString != rightString, _ => throw new InvalidOperationException($"Operator '{op}' is not supported for string comparison.") };
        }
        var leftNumber = ToNumber(left); var rightNumber = ToNumber(right);
        return op switch { "==" => leftNumber == rightNumber, "!=" => leftNumber != rightNumber, "<" => leftNumber < rightNumber, "<=" => leftNumber <= rightNumber, ">" => leftNumber > rightNumber, ">=" => leftNumber >= rightNumber, _ => throw new InvalidOperationException($"Unsupported comparison operator '{op}'.") };
    }

    private static double ToNumber(object? value) => value switch { double d => d, int i => i, long l => l, float f => f, decimal m => (double)m, string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed, _ => 0 };
    private static object? NegateValue(object? value) => value switch { double d => -d, int i => -i, float f => -f, decimal m => -m, string s => s.StartsWith("-") ? s[1..] : $"-{s}", _ => 0 };
    private static bool IsTrue(object? value) => value switch { null => false, bool b => b, string s => !string.IsNullOrEmpty(s), double d => Math.Abs(d) > double.Epsilon, int i => i != 0, _ => true };

    private Token Consume(TokenType type, string message) { if (Check(type)) return Advance(); throw new InvalidOperationException(message); }
    private void ConsumeKeyword(string keyword, string message) { if (!MatchKeyword(keyword)) throw new InvalidOperationException(message); }
    private void ConsumeOperator(string op, string message) { if (!MatchOperator(op)) throw new InvalidOperationException(message); }

    private bool Match(TokenType type) { if (Check(type)) { Advance(); return true; } return false; }
    private bool MatchKeyword(string keyword) { if (Check(TokenType.Keyword) && CurrentToken().Value.Equals(keyword, StringComparison.OrdinalIgnoreCase)) { Advance(); return true; } return false; }
    private bool MatchOperator(string op) { if (Check(TokenType.Operator) && CurrentToken().Value == op) { Advance(); return true; } return false; }
    private bool Check(TokenType type) { if (IsAtEnd()) return false; return CurrentToken().Type == type; }
    private bool CheckKeyword(string keyword) { if (IsAtEnd()) return false; return CurrentToken().Type == TokenType.Keyword && CurrentToken().Value.Equals(keyword, StringComparison.OrdinalIgnoreCase); }
    private bool CheckOperator(string op) { if (IsAtEnd()) return false; return CurrentToken().Type == TokenType.Operator && CurrentToken().Value == op; }

    private Token Advance() { if (!IsAtEnd()) _position++; return PreviousToken(); }

    private bool CheckNextKeyword(string keyword)
    {
        var idx = _position + 1;
        if (idx >= _tokens.Count) return false;
        return _tokens[idx].Type == TokenType.Keyword && _tokens[idx].Value.Equals(keyword, StringComparison.OrdinalIgnoreCase);
    }
    private bool IsAtEnd() => _position >= _tokens.Count;
    private Token CurrentToken() => _tokens[_position];
    private Token PreviousToken() => _tokens[_position - 1];

    private bool TryGetVariable(string name, out object? value) { if (_variables.TryGetValue(name, out value)) return true; if (_outerVariables != null && _outerVariables.TryGetValue(name, out value)) return true; value = null; return false; }
    private void AssignVariable(string name, object? value) { if (_variables.ContainsKey(name) || _outerVariables == null || !_outerVariables.ContainsKey(name)) { _variables[name] = value!; return; } _outerVariables[name] = value!; }

    private Parser CreateChildParser(List<Token> tokens)
    {
        var childVariables = new Dictionary<string, object>(_variables, StringComparer.OrdinalIgnoreCase);
        var childFunctions = _functions;
        var childClasses = _classes;
        return new Parser(tokens, childVariables, childFunctions, childClasses, _outerVariables ?? _variables, _basePath);
    }

    private void CopyVariablesFromChild(Parser child) { foreach (var kvp in child._variables) _variables[kvp.Key] = kvp.Value; }
}
