using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace McpRoslyn.Server.RoslynPath
{
    /// <summary>
    /// Parses RoslynPath expressions into an AST
    /// </summary>
    public class RoslynPathParser
    {
        public PathExpression Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be empty");

            var lexer = new PathLexer(path);
            var tokens = lexer.Tokenize();
            var parser = new PathExpressionParser(tokens);
            return parser.ParsePath();
        }
    }

    public enum TokenType
    {
        Slash,              // /
        DoubleSlash,        // //
        LeftBracket,        // [
        RightBracket,       // ]
        At,                 // @
        Equals,             // =
        And,                // and
        Or,                 // or
        Not,                // not
        Identifier,         // method, class, etc.
        String,             // 'value' or "value"
        Number,             // 123
        Wildcard,           // * or ?
        Function,           // last(), count(), etc.
        LeftParen,          // (
        RightParen,         // )
        DotDot,             // ..
        Axis,               // ancestor::, following-sibling::, etc.
        Eof
    }

    public class Token
    {
        public TokenType Type { get; set; }
        public string Value { get; set; } = string.Empty;
        public int Position { get; set; }
    }

    public class PathLexer
    {
        private readonly string _input;
        private int _position;

        public PathLexer(string input)
        {
            _input = input;
            _position = 0;
        }

        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();

            while (_position < _input.Length)
            {
                SkipWhitespace();
                if (_position >= _input.Length) break;

                var token = NextToken();
                if (token != null)
                    tokens.Add(token);
            }

            tokens.Add(new Token { Type = TokenType.Eof, Position = _position });
            return tokens;
        }

        private Token NextToken()
        {
            var start = _position;
            var ch = Peek();

            switch (ch)
            {
                case '/':
                    Advance();
                    if (Peek() == '/')
                    {
                        Advance();
                        return new Token { Type = TokenType.DoubleSlash, Value = "//", Position = start };
                    }
                    return new Token { Type = TokenType.Slash, Value = "/", Position = start };

                case '[':
                    Advance();
                    return new Token { Type = TokenType.LeftBracket, Value = "[", Position = start };

                case ']':
                    Advance();
                    return new Token { Type = TokenType.RightBracket, Value = "]", Position = start };

                case '@':
                    Advance();
                    return new Token { Type = TokenType.At, Value = "@", Position = start };

                case '=':
                    Advance();
                    return new Token { Type = TokenType.Equals, Value = "=", Position = start };

                case '(':
                    Advance();
                    return new Token { Type = TokenType.LeftParen, Value = "(", Position = start };

                case ')':
                    Advance();
                    return new Token { Type = TokenType.RightParen, Value = ")", Position = start };

                case '.':
                    if (Peek(1) == '.')
                    {
                        Advance(); Advance();
                        return new Token { Type = TokenType.DotDot, Value = "..", Position = start };
                    }
                    break;

                case '\'':
                case '"':
                    return ReadString(ch);

                case '*':
                case '?':
                    Advance();
                    return new Token { Type = TokenType.Wildcard, Value = ch.ToString(), Position = start };
            }

            if (char.IsDigit(ch))
            {
                return ReadNumber();
            }

            if (char.IsLetter(ch) || ch == '_')
            {
                return ReadIdentifier();
            }

            throw new Exception($"Unexpected character '{ch}' at position {_position}");
        }

        private Token ReadString(char quote)
        {
            var start = _position;
            Advance(); // Skip opening quote
            var value = new System.Text.StringBuilder();

            while (_position < _input.Length && Peek() != quote)
            {
                if (Peek() == '\\')
                {
                    Advance();
                    if (_position < _input.Length)
                        value.Append(Peek());
                }
                else
                {
                    value.Append(Peek());
                }
                Advance();
            }

            if (_position >= _input.Length)
                throw new Exception($"Unterminated string at position {start}");

            Advance(); // Skip closing quote
            return new Token { Type = TokenType.String, Value = value.ToString(), Position = start };
        }

        private Token ReadNumber()
        {
            var start = _position;
            var value = new System.Text.StringBuilder();

            while (_position < _input.Length && char.IsDigit(Peek()))
            {
                value.Append(Peek());
                Advance();
            }

            return new Token { Type = TokenType.Number, Value = value.ToString(), Position = start };
        }

        private Token ReadIdentifier()
        {
            var start = _position;
            var value = new System.Text.StringBuilder();

            while (_position < _input.Length && (char.IsLetterOrDigit(Peek()) || Peek() == '_' || Peek() == '-' || Peek() == ':'))
            {
                value.Append(Peek());
                Advance();
            }

            var identifier = value.ToString();

            // Check for keywords
            switch (identifier)
            {
                case "and":
                    return new Token { Type = TokenType.And, Value = identifier, Position = start };
                case "or":
                    return new Token { Type = TokenType.Or, Value = identifier, Position = start };
                case "not":
                    return new Token { Type = TokenType.Not, Value = identifier, Position = start };
            }

            // Check for axes
            if (identifier.EndsWith("::"))
            {
                return new Token { Type = TokenType.Axis, Value = identifier, Position = start };
            }

            // Check for functions
            if (_position < _input.Length && Peek() == '(')
            {
                return new Token { Type = TokenType.Function, Value = identifier, Position = start };
            }

            return new Token { Type = TokenType.Identifier, Value = identifier, Position = start };
        }

        private void SkipWhitespace()
        {
            while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
                _position++;
        }

        private char Peek(int offset = 0)
        {
            var pos = _position + offset;
            return pos < _input.Length ? _input[pos] : '\0';
        }

        private void Advance()
        {
            _position++;
        }
    }

    // AST Classes
    public abstract class PathExpression { }

    public class PathStep : PathExpression
    {
        public StepType Type { get; set; }
        public string Axis { get; set; } = string.Empty;
        public string NodeTest { get; set; } = string.Empty;
        public List<Predicate> Predicates { get; set; } = new List<Predicate>();
    }

    public enum StepType
    {
        Child,          // /
        Descendant,     // //
        Parent,         // ..
        Axis            // ancestor::, etc.
    }

    public class PathSequence : PathExpression
    {
        public List<PathStep> Steps { get; set; } = new List<PathStep>();
    }

    public abstract class Predicate { }

    public class NamePredicate : Predicate
    {
        public string Name { get; set; } = string.Empty;
        public bool HasWildcard { get; set; }
    }

    public class PositionPredicate : Predicate
    {
        public string Expression { get; set; } = string.Empty; // "1", "last()", "last()-1"
    }

    public class AttributePredicate : Predicate
    {
        public string Name { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty; // =, contains, matches
        public string Value { get; set; } = string.Empty;
    }

    public class BooleanPredicate : Predicate
    {
        public string Name { get; set; } = string.Empty; // @async, @public
    }

    public class CompoundPredicate : Predicate
    {
        public Predicate? Left { get; set; }
        public string Operator { get; set; } = string.Empty; // and, or
        public Predicate? Right { get; set; }
    }

    public class NotPredicate : Predicate
    {
        public Predicate? Inner { get; set; }
    }

    public class PathExpressionParser
    {
        private readonly List<Token> _tokens;
        private int _position;

        public PathExpressionParser(List<Token> tokens)
        {
            _tokens = tokens;
            _position = 0;
        }

        public PathExpression ParsePath()
        {
            var sequence = new PathSequence();

            // Handle absolute vs relative paths
            if (Current.Type == TokenType.Slash)
            {
                Advance(); // Skip leading /
                // Absolute path - will need to start from root
            }

            while (Current.Type != TokenType.Eof)
            {
                var step = ParseStep();
                if (step != null)
                    sequence.Steps.Add(step);
            }

            return sequence;
        }

        private PathStep? ParseStep()
        {
            // Check if we're at the end or have nothing to parse
            if (Current.Type == TokenType.Eof)
                return null;
                
            var step = new PathStep();
            bool hasContent = false;

            // Check for axis or special steps
            if (Current.Type == TokenType.DotDot)
            {
                step.Type = StepType.Parent;
                step.NodeTest = "..";
                Advance();
                return step;
            }

            if (Current.Type == TokenType.DoubleSlash)
            {
                step.Type = StepType.Descendant;
                Advance();
                hasContent = true;
            }
            else if (Current.Type == TokenType.Slash)
            {
                step.Type = StepType.Child;
                Advance();
                hasContent = true;
            }
            else if (Current.Type == TokenType.Axis)
            {
                step.Type = StepType.Axis;
                step.Axis = Current.Value;
                Advance();
                hasContent = true;
            }

            // Parse node test
            if (Current.Type == TokenType.Identifier || Current.Type == TokenType.Wildcard)
            {
                step.NodeTest = Current.Value;
                Advance();
                hasContent = true;
            }

            // Parse predicates
            while (Current.Type == TokenType.LeftBracket)
            {
                Advance(); // Skip [
                var predicate = ParsePredicate();
                if (predicate != null)
                    step.Predicates.Add(predicate);
                Expect(TokenType.RightBracket);
                hasContent = true;
            }

            // If we didn't parse anything, return null to signal no step
            return hasContent ? step : null;
        }

        private Predicate ParsePredicate()
        {
            return ParseOrPredicate();
        }

        private Predicate ParseOrPredicate()
        {
            var left = ParseAndPredicate();

            while (Current.Type == TokenType.Or)
            {
                Advance();
                var right = ParseAndPredicate();
                left = new CompoundPredicate { Left = left, Operator = "or", Right = right };
            }

            return left;
        }

        private Predicate ParseAndPredicate()
        {
            var left = ParseNotPredicate();

            while (Current.Type == TokenType.And)
            {
                Advance();
                var right = ParseNotPredicate();
                left = new CompoundPredicate { Left = left, Operator = "and", Right = right };
            }

            return left;
        }

        private Predicate ParseNotPredicate()
        {
            if (Current.Type == TokenType.Not)
            {
                Advance();
                return new NotPredicate { Inner = ParsePrimaryPredicate() };
            }

            return ParsePrimaryPredicate();
        }

        private Predicate ParsePrimaryPredicate()
        {
            // Position predicate (number or function)
            if (Current.Type == TokenType.Number)
            {
                var value = Current.Value;
                Advance();
                return new PositionPredicate { Expression = value };
            }

            if (Current.Type == TokenType.Function)
            {
                var func = Current.Value;
                Advance();
                Expect(TokenType.LeftParen);
                // Parse function arguments if needed
                Expect(TokenType.RightParen);
                return new PositionPredicate { Expression = func + "()" };
            }

            // Attribute predicate
            if (Current.Type == TokenType.At)
            {
                Advance();
                var attrName = Current.Value;
                Advance();

                // Boolean attribute like @async
                if (Current.Type != TokenType.Equals)
                {
                    return new BooleanPredicate { Name = attrName };
                }

                // Attribute with value
                Advance(); // Skip =
                var value = Current.Value;
                Advance();
                return new AttributePredicate { Name = attrName, Operator = "=", Value = value };
            }

            // Name predicate
            if (Current.Type == TokenType.Identifier || Current.Type == TokenType.String || Current.Type == TokenType.Wildcard)
            {
                var name = Current.Value;
                var hasWildcard = Current.Type == TokenType.Wildcard || name.Contains("*") || name.Contains("?");
                Advance();
                return new NamePredicate { Name = name, HasWildcard = hasWildcard };
            }

            throw new Exception($"Unexpected token in predicate: {Current.Type}");
        }

        private Token Current => _position < _tokens.Count ? _tokens[_position] : _tokens.Last();

        private void Advance()
        {
            if (_position < _tokens.Count - 1)
                _position++;
        }

        private void Expect(TokenType type)
        {
            if (Current.Type != type)
                throw new Exception($"Expected {type} but found {Current.Type}");
            Advance();
        }
    }
}