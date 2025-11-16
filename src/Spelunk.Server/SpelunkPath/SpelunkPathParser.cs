using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spelunk.Server.SpelunkPath
{
    /// <summary>
    /// Complete rewrite of SpelunkPath parser with proper grammar support
    /// </summary>
    public class SpelunkPathParser
    {
        public PathExpression Parse(string input)
        {
            var lexer = new Lexer(input);
            var parser = new Parser(lexer);
            return parser.ParsePath();
        }
    }

    #region Lexer

    public enum TokenType
    {
        // Operators
        Slash,              // /
        DoubleSlash,        // //
        DotDot,             // ..
        Dot,                // .
        
        // Brackets
        LeftBracket,        // [
        RightBracket,       // ]
        LeftParen,          // (
        RightParen,         // )
        
        // Predicates
        At,                 // @
        Equals,             // =
        NotEquals,          // !=
        Contains,           // ~=
        LessThan,           // <
        GreaterThan,        // >
        Comma,              // , (for function arguments)
        LessOrEqual,        // <=
        GreaterOrEqual,     // >=
        
        // Logical
        And,                // and
        Or,                 // or
        Not,                // not
        
        // Axis
        AxisSeparator,      // ::
        
        // Values
        Identifier,         // method, class, etc.
        Pattern,            // Get*User, *Test*, etc.
        String,             // "value" or 'value'
        Number,             // 123
        
        // Special
        Minus,              // - (for last()-1)
        Eof
    }

    public class Token
    {
        public TokenType Type { get; }
        public string Value { get; }
        public int Position { get; }

        public Token(TokenType type, string value, int position)
        {
            Type = type;
            Value = value;
            Position = position;
        }
    }

    public class Lexer
    {
        private readonly string _input;
        private int _pos;
        private readonly List<Token> _tokens = new();

        public Lexer(string input)
        {
            _input = input;
            _pos = 0;
            Tokenize();
        }

        private void Tokenize()
        {
            while (_pos < _input.Length)
            {
                SkipWhitespace();
                if (_pos >= _input.Length) break;

                var token = NextToken();
                if (token != null)
                    _tokens.Add(token);
            }
            _tokens.Add(new Token(TokenType.Eof, "", _pos));
        }

        private Token? NextToken()
        {
            var start = _pos;
            var ch = Peek();

            return ch switch
            {
                '/' when Peek(1) == '/' => Consume(TokenType.DoubleSlash, 2),
                '/' => Consume(TokenType.Slash, 1),
                '[' => Consume(TokenType.LeftBracket, 1),
                ']' => Consume(TokenType.RightBracket, 1),
                '(' => Consume(TokenType.LeftParen, 1),
                ')' => Consume(TokenType.RightParen, 1),
                ',' => Consume(TokenType.Comma, 1),
                '@' => Consume(TokenType.At, 1),
                '.' when Peek(1) == '.' => Consume(TokenType.DotDot, 2),
                '.' when Peek(1) == '/' => Consume(TokenType.Dot, 1),
                '.' => Consume(TokenType.Dot, 1),
                '=' => Consume(TokenType.Equals, 1),
                '!' when Peek(1) == '=' => Consume(TokenType.NotEquals, 2),
                '~' when Peek(1) == '=' => Consume(TokenType.Contains, 2),
                '<' when Peek(1) == '=' => Consume(TokenType.LessOrEqual, 2),
                '<' => Consume(TokenType.LessThan, 1),
                '>' when Peek(1) == '=' => Consume(TokenType.GreaterOrEqual, 2),
                '>' => Consume(TokenType.GreaterThan, 1),
                '-' when IsInPredicateContext() => Consume(TokenType.Minus, 1),
                '"' or '\'' => ReadString(ch),
                _ when char.IsDigit(ch) => ReadNumber(),
                _ when char.IsLetter(ch) || ch == '_' || ch == '*' || ch == '?' => ReadIdentifierOrPattern(),
                _ => throw new Exception($"Unexpected character '{ch}' at position {_pos}")
            };
        }

        private Token ReadIdentifierOrPattern()
        {
            var start = _pos;
            var text = new StringBuilder();
            bool hasWildcard = false;
            
            // Read identifier/pattern characters
            while (_pos < _input.Length)
            {
                var ch = Peek();
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '*' || ch == '?')
                {
                    if (ch == '*' || ch == '?')
                        hasWildcard = true;
                    text.Append(ch);
                    _pos++;
                }
                else if (ch == '-' && _pos + 1 < _input.Length && 
                         (char.IsLetterOrDigit(Peek(1)) || Peek(1) == '_'))
                {
                    // Handle hyphenated names
                    text.Append(ch);
                    _pos++;
                }
                else if (ch == ':' && Peek(1) == ':')
                {
                    // Axis separator
                    text.Append("::");
                    _pos += 2;
                    break;
                }
                else
                {
                    break;
                }
            }

            var value = text.ToString();
            
            // If we're in a predicate context and it contains wildcards, it's a pattern
            if (hasWildcard && IsInPredicateContext())
                return new Token(TokenType.Pattern, value, start);
            
            // Check for keywords (only if not a pattern)
            if (!hasWildcard)
            {
                var type = value switch
                {
                    "and" => TokenType.And,
                    "or" => TokenType.Or,
                    "not" => TokenType.Not,
                    _ when value.EndsWith("::") => TokenType.Identifier, // Axis
                    _ => TokenType.Identifier
                };
                return new Token(type, value, start);
            }
            
            // Wildcard outside predicate context
            return new Token(TokenType.Pattern, value, start);
        }


        private Token ReadString(char quote)
        {
            var start = _pos;
            _pos++; // Skip opening quote
            var text = new StringBuilder();

            while (_pos < _input.Length && Peek() != quote)
            {
                if (Peek() == '\\' && _pos + 1 < _input.Length)
                {
                    _pos++; // Skip escape
                    text.Append(Peek());
                }
                else
                {
                    text.Append(Peek());
                }
                _pos++;
            }

            if (_pos < _input.Length)
                _pos++; // Skip closing quote

            return new Token(TokenType.String, text.ToString(), start);
        }

        private Token ReadNumber()
        {
            var start = _pos;
            var text = new StringBuilder();

            while (_pos < _input.Length && char.IsDigit(Peek()))
            {
                text.Append(Peek());
                _pos++;
            }

            return new Token(TokenType.Number, text.ToString(), start);
        }

        private Token Consume(TokenType type, int count)
        {
            var start = _pos;
            var value = _input.Substring(_pos, count);
            _pos += count;
            return new Token(type, value, start);
        }

        private char Peek(int offset = 0)
        {
            var index = _pos + offset;
            return index < _input.Length ? _input[index] : '\0';
        }

        private void SkipWhitespace()
        {
            while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
                _pos++;
        }

        private bool IsInPredicateContext()
        {
            // Simple heuristic: we're in a predicate if we've seen a [ without matching ]
            int brackets = 0;
            for (int i = 0; i < _pos; i++)
            {
                if (_input[i] == '[') brackets++;
                else if (_input[i] == ']') brackets--;
            }
            return brackets > 0;
        }

        private int _tokenIndex = 0;
        
        public Token Current => _tokenIndex < _tokens.Count ? _tokens[_tokenIndex] : new Token(TokenType.Eof, "", _pos);
        
        public Token Next()
        {
            if (_tokenIndex < _tokens.Count)
            {
                return _tokens[_tokenIndex++];
            }
            return new Token(TokenType.Eof, "", _pos);
        }

        public Token PeekToken(int ahead)
        {
            var index = _tokenIndex + ahead;
            if (index < _tokens.Count)
                return _tokens[index];
            return new Token(TokenType.Eof, "", _input.Length);
        }
    }

    #endregion

    #region Parser

    public class Parser
    {
        private readonly Lexer _lexer;
        private Token _current;

        public Parser(Lexer lexer)
        {
            _lexer = lexer;
            _current = _lexer.Next();
        }

        public PathExpression ParsePath()
        {
            var steps = new List<PathStep>();

            // Handle absolute vs relative
            bool isAbsolute = false;
            if (_current.Type == TokenType.Slash)
            {
                isAbsolute = true;
                Consume(TokenType.Slash);
            }
            // Handle paths starting with . (self)
            else if (_current.Type == TokenType.Dot)
            {
                Consume(TokenType.Dot);
                // Add a self step
                var selfStep = new PathStep
                {
                    Axis = StepAxis.Self,
                    NodeTest = ""
                };
                steps.Add(selfStep);
                
                // Check if followed by / or //
                if (_current.Type == TokenType.Slash)
                {
                    Consume(TokenType.Slash);
                }
            }

            // Parse steps
            while (_current.Type != TokenType.Eof)
            {
                var step = ParseStep();
                if (step != null)
                    steps.Add(step);
                
                // Check for step separator
                if (_current.Type == TokenType.Slash)
                {
                    Consume(TokenType.Slash);
                }
                else if (_current.Type == TokenType.DoubleSlash)
                {
                    // DoubleSlash is also a valid separator (it starts the next step with descendant-or-self axis)
                    // Don't consume it here - let ParseStep handle it
                    continue;
                }
                else if (_current.Type != TokenType.Eof)
                {
                    break; // End of path
                }
            }

            return new PathExpression { IsAbsolute = isAbsolute, Steps = steps };
        }

        private PathStep? ParseStep()
        {
            if (_current.Type == TokenType.Eof)
                return null;

            var step = new PathStep();

            // Handle special cases
            if (_current.Type == TokenType.DotDot)
            {
                step.Axis = StepAxis.Parent;
                step.NodeTest = "..";
                Consume(TokenType.DotDot);
                return step;
            }

            if (_current.Type == TokenType.DoubleSlash)
            {
                step.Axis = StepAxis.DescendantOrSelf;
                Consume(TokenType.DoubleSlash);
            }
            else if (_current.Type == TokenType.Identifier && _current.Value.EndsWith("::"))
            {
                // Explicit axis
                step.Axis = ParseAxis(_current.Value);
                Consume(TokenType.Identifier);
            }
            else
            {
                step.Axis = StepAxis.Child; // Default
            }

            // Parse node test
            if (_current.Type == TokenType.Identifier || _current.Type == TokenType.Pattern)
            {
                step.NodeTest = _current.Value;
                Advance();
            }
            else if (_current.Value == "*")
            {
                step.NodeTest = "*";
                Advance();
            }

            // Parse predicates
            while (_current.Type == TokenType.LeftBracket)
            {
                Consume(TokenType.LeftBracket);
                var predicate = ParsePredicateExpr();
                if (predicate != null)
                    step.Predicates.Add(predicate);
                Consume(TokenType.RightBracket);
            }

            return step;
        }

        private PredicateExpr? ParsePredicateExpr()
        {
            return ParseOrExpr();
        }

        private PredicateExpr? ParseOrExpr()
        {
            var left = ParseAndExpr();
            
            while (_current.Type == TokenType.Or)
            {
                Consume(TokenType.Or);
                var right = ParseAndExpr();
                left = new OrExpr { Left = left!, Right = right! };
            }

            return left;
        }

        private PredicateExpr? ParseAndExpr()
        {
            var left = ParseNotExpr();
            
            while (_current.Type == TokenType.And)
            {
                Consume(TokenType.And);
                var right = ParseNotExpr();
                left = new AndExpr { Left = left!, Right = right! };
            }

            return left;
        }

        private PredicateExpr? ParseNotExpr()
        {
            if (_current.Type == TokenType.Not)
            {
                Consume(TokenType.Not);
                
                // Handle not(...) and not @attr
                if (_current.Type == TokenType.LeftParen)
                {
                    Consume(TokenType.LeftParen);
                    var inner = ParsePredicateExpr();
                    Consume(TokenType.RightParen);
                    return new NotExpr { Inner = inner! };
                }
                else
                {
                    var inner = ParsePrimaryExpr();
                    return new NotExpr { Inner = inner! };
                }
            }

            return ParsePrimaryExpr();
        }

        private PredicateExpr? ParsePrimaryExpr()
        {
            // Parenthesized expression
            if (_current.Type == TokenType.LeftParen)
            {
                Consume(TokenType.LeftParen);
                var expr = ParsePredicateExpr();
                Consume(TokenType.RightParen);
                return expr;
            }

            // Attribute predicate
            if (_current.Type == TokenType.At)
            {
                return ParseAttributePredicate();
            }

            // Position predicate (number or last())
            if (_current.Type == TokenType.Number)
            {
                var pos = _current.Value;
                Consume(TokenType.Number);
                return new PositionExpr { Position = pos };
            }

            // Function (last(), etc.)
            // TODO: BUG - Function argument parsing doesn't work correctly
            // The lookahead logic (_lexer.PeekToken(0)) may not be correctly detecting functions with arguments
            // like contains('Test'). This causes such expressions to be parsed as simple names instead of
            // function calls. Debug the PeekToken offset or lexer state to fix this issue.
            // See failing tests in RoslynPathFunctionTests.cs (currently skipped)
            // PeekToken(0) gets the next token after _current (since _tokenIndex already advanced)
            if (_current.Type == TokenType.Identifier && _lexer.PeekToken(0).Type == TokenType.LeftParen)
            {
                return ParseFunctionPredicate();
            }

            // Path predicate (.//something or just .something)
            if (_current.Type == TokenType.Dot || _current.Type == TokenType.DoubleSlash)
            {
                return ParsePathPredicate();
            }

            // Name predicate (identifier or pattern)
            if (_current.Type == TokenType.Identifier || _current.Type == TokenType.Pattern)
            {
                var name = _current.Value;
                Advance();
                return new NameExpr { Pattern = name };
            }

            return null;
        }

        private PredicateExpr ParseAttributePredicate()
        {
            Consume(TokenType.At);
            var name = _current.Value;
            Consume(TokenType.Identifier);

            // Boolean attribute (just @name)
            if (_current.Type != TokenType.Equals && _current.Type != TokenType.NotEquals &&
                _current.Type != TokenType.Contains && _current.Type != TokenType.LessThan &&
                _current.Type != TokenType.GreaterThan && _current.Type != TokenType.LessOrEqual &&
                _current.Type != TokenType.GreaterOrEqual)
            {
                return new AttributeExpr { Name = name };
            }

            // Attribute with operator and value
            var op = _current.Type switch
            {
                TokenType.Equals => "=",
                TokenType.NotEquals => "!=",
                TokenType.Contains => "~=",
                TokenType.LessThan => "<",
                TokenType.GreaterThan => ">",
                TokenType.LessOrEqual => "<=",
                TokenType.GreaterOrEqual => ">=",
                _ => "="
            };
            Advance();

            var value = _current.Value;
            if (_current.Type == TokenType.String || _current.Type == TokenType.Number || 
                _current.Type == TokenType.Identifier || _current.Type == TokenType.Pattern)
            {
                Advance();
            }

            return new AttributeExpr { Name = name, Operator = op, Value = value };
        }

        private PredicateExpr ParseFunctionPredicate()
        {
            var func = _current.Value;
            Consume(TokenType.Identifier);
            Consume(TokenType.LeftParen);
            
            // Parse function arguments if present
            var arguments = new List<string>();
            while (_current.Type != TokenType.RightParen && _current.Type != TokenType.Eof)
            {
                // Parse argument - can be a string literal, number, or identifier
                if (_current.Type == TokenType.String)
                {
                    arguments.Add(_current.Value);
                    Consume(TokenType.String);
                }
                else if (_current.Type == TokenType.Number)
                {
                    arguments.Add(_current.Value);
                    Consume(TokenType.Number);
                }
                else if (_current.Type == TokenType.Identifier)
                {
                    arguments.Add(_current.Value);
                    Consume(TokenType.Identifier);
                }
                else if (_current.Type == TokenType.Dot)
                {
                    // Support '.' as current node reference
                    arguments.Add(".");
                    Consume(TokenType.Dot);
                }
                
                // Check for comma separator
                if (_current.Type == TokenType.Comma)
                {
                    Consume(TokenType.Comma);
                }
                else if (_current.Type != TokenType.RightParen)
                {
                    // If not comma and not closing paren, break to avoid infinite loop
                    break;
                }
            }
            
            Consume(TokenType.RightParen);

            // Build function expression string
            var expr = func + "(" + string.Join(", ", arguments) + ")";
            
            // Handle last()-N special case
            if (func == "last" && arguments.Count == 0 && _current.Type == TokenType.Minus)
            {
                Consume(TokenType.Minus);
                if (_current.Type == TokenType.Number)
                {
                    expr += "-" + _current.Value;
                    Consume(TokenType.Number);
                }
            }

            // For position-based functions, return PositionExpr
            if (func == "last" || func == "first" || func == "position")
            {
                return new PositionExpr { Position = expr };
            }
            
            // For other functions (like contains, starts-with), treat as attribute expressions
            // This allows for future extension to support text-matching functions
            return new AttributeExpr 
            { 
                Name = "function",
                Operator = "=",
                Value = expr
            };
        }

        private PredicateExpr ParsePathPredicate()
        {
            // This is a nested path starting from current node
            var savedPos = _current.Position;
            var pathTokens = new List<Token>();
            int bracketDepth = 0;

            // Collect tokens that form the path, handling nested brackets
            while (_current.Type != TokenType.Eof)
            {
                // Track bracket depth
                if (_current.Type == TokenType.LeftBracket)
                {
                    bracketDepth++;
                }
                else if (_current.Type == TokenType.RightBracket)
                {
                    if (bracketDepth == 0)
                        break; // End of this path predicate
                    bracketDepth--;
                }
                else if (bracketDepth == 0 && (_current.Type == TokenType.And || _current.Type == TokenType.Or))
                {
                    break; // End of this path predicate (boolean operator at same level)
                }
                
                pathTokens.Add(_current);
                Advance();
            }

            // Build the path string, handling special tokens
            var pathStr = "";
            foreach (var token in pathTokens)
            {
                // Add space before certain tokens if needed
                if (pathStr.Length > 0 && (token.Type == TokenType.And || token.Type == TokenType.Or || token.Type == TokenType.Not))
                {
                    pathStr += " ";
                }
                
                // Add the token value (strings need quotes)
                if (token.Type == TokenType.String)
                {
                    pathStr += "'" + token.Value + "'";
                }
                else
                {
                    pathStr += token.Value;
                }
                
                // Add space after certain tokens if needed
                if (token.Type == TokenType.And || token.Type == TokenType.Or || token.Type == TokenType.Not)
                {
                    pathStr += " ";
                }
            }
            return new PathPredicateExpr { PathString = pathStr };
        }

        private StepAxis ParseAxis(string axisStr)
        {
            var axis = axisStr.TrimEnd(':');
            return axis switch
            {
                "ancestor" => StepAxis.Ancestor,
                "ancestor-or-self" => StepAxis.AncestorOrSelf,
                "child" => StepAxis.Child,
                "descendant" => StepAxis.Descendant,
                "descendant-or-self" => StepAxis.DescendantOrSelf,
                "following" => StepAxis.Following,
                "following-sibling" => StepAxis.FollowingSibling,
                "parent" => StepAxis.Parent,
                "preceding" => StepAxis.Preceding,
                "preceding-sibling" => StepAxis.PrecedingSibling,
                "self" => StepAxis.Self,
                _ => StepAxis.Child
            };
        }

        private void Advance()
        {
            _current = _lexer.Next();
        }
        
        private Token PeekNext()
        {
            // Save current lexer position
            var savedToken = _lexer.PeekToken(1);
            return savedToken;
        }

        private void Consume(TokenType expected)
        {
            if (_current.Type != expected)
                throw new Exception($"Expected {expected} but got {_current.Type} ('{_current.Value}') at position {_current.Position}");
            Advance();
        }
    }

    #endregion

    #region AST

    public class PathExpression
    {
        public bool IsAbsolute { get; set; }
        public List<PathStep> Steps { get; set; } = new();
    }

    public class PathStep
    {
        public StepAxis Axis { get; set; }
        public string NodeTest { get; set; } = "";
        public List<PredicateExpr> Predicates { get; set; } = new();
    }

    public enum StepAxis
    {
        Ancestor,
        AncestorOrSelf,
        Child,
        Descendant,
        DescendantOrSelf,
        Following,
        FollowingSibling,
        Parent,
        Preceding,
        PrecedingSibling,
        Self
    }

    public abstract class PredicateExpr
    {
    }

    public class AndExpr : PredicateExpr
    {
        public PredicateExpr Left { get; set; } = null!;
        public PredicateExpr Right { get; set; } = null!;
    }

    public class OrExpr : PredicateExpr
    {
        public PredicateExpr Left { get; set; } = null!;
        public PredicateExpr Right { get; set; } = null!;
    }

    public class NotExpr : PredicateExpr
    {
        public PredicateExpr Inner { get; set; } = null!;
    }

    public class AttributeExpr : PredicateExpr
    {
        public string Name { get; set; } = "";
        public string? Operator { get; set; }
        public string? Value { get; set; }
    }

    public class NameExpr : PredicateExpr
    {
        public string Pattern { get; set; } = "";
    }

    public class PositionExpr : PredicateExpr
    {
        public string Position { get; set; } = "";
    }

    public class PathPredicateExpr : PredicateExpr
    {
        public string PathString { get; set; } = "";
        // TODO: Should be PathExpression Path
    }

    #endregion
}