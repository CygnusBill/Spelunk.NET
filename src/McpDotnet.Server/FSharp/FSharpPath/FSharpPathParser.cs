using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace McpDotnet.Server.FSharp.FSharpPath;

/// <summary>
/// Parser for FSharpPath queries - an XPath-inspired query language for F# AST.
/// </summary>
public class FSharpPathParser
{
    /// <summary>
    /// Parses an FSharpPath query string into a sequence of steps.
    /// </summary>
    public FSharpPathQuery Parse(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty", nameof(query));
        }

        var steps = new List<FSharpPathStep>();
        var parts = SplitQuery(query);

        foreach (var part in parts)
        {
            var step = ParseStep(part);
            steps.Add(step);
        }

        return new FSharpPathQuery(steps);
    }

    private List<string> SplitQuery(string query)
    {
        var parts = new List<string>();
        var current = "";
        var inBrackets = false;
        var inQuotes = false;
        var quoteChar = '\0';

        for (int i = 0; i < query.Length; i++)
        {
            var ch = query[i];

            if (ch == '"' || ch == '\'')
            {
                if (!inQuotes)
                {
                    inQuotes = true;
                    quoteChar = ch;
                }
                else if (ch == quoteChar)
                {
                    inQuotes = false;
                }
                current += ch;
            }
            else if (ch == '[' && !inQuotes)
            {
                inBrackets = true;
                current += ch;
            }
            else if (ch == ']' && !inQuotes)
            {
                inBrackets = false;
                current += ch;
            }
            else if (ch == '/' && !inBrackets && !inQuotes && i + 1 < query.Length && query[i + 1] != '/')
            {
                if (!string.IsNullOrEmpty(current))
                {
                    parts.Add(current);
                    current = "";
                }
            }
            else if (ch == '/' && !inBrackets && !inQuotes && i + 1 < query.Length && query[i + 1] == '/')
            {
                if (!string.IsNullOrEmpty(current))
                {
                    parts.Add(current);
                    current = "";
                }
                parts.Add("//");
                i++; // Skip the second /
            }
            else
            {
                current += ch;
            }
        }

        if (!string.IsNullOrEmpty(current))
        {
            parts.Add(current);
        }

        return parts;
    }

    private FSharpPathStep ParseStep(string step)
    {
        if (step == "//")
        {
            return new FSharpPathStep
            {
                Axis = FSharpPathAxis.DescendantOrSelf,
                NodeTest = new FSharpNodeTest { Type = FSharpNodeType.Any }
            };
        }

        // Check for axis specifier
        var axisMatch = Regex.Match(step, @"^([\w-]+)::");
        var axis = FSharpPathAxis.Child;
        var remaining = step;

        if (axisMatch.Success)
        {
            axis = ParseAxis(axisMatch.Groups[1].Value);
            remaining = step.Substring(axisMatch.Length);
        }

        // Parse node test and predicates
        var predicateStart = remaining.IndexOf('[');
        var nodeTestPart = predicateStart >= 0 ? remaining.Substring(0, predicateStart) : remaining;
        var predicates = new List<FSharpPredicate>();

        if (predicateStart >= 0)
        {
            predicates = ParsePredicates(remaining.Substring(predicateStart));
        }

        var nodeTest = ParseNodeTest(nodeTestPart);

        return new FSharpPathStep
        {
            Axis = axis,
            NodeTest = nodeTest,
            Predicates = predicates
        };
    }

    private FSharpPathAxis ParseAxis(string axisName)
    {
        return axisName.ToLower() switch
        {
            "child" => FSharpPathAxis.Child,
            "descendant" => FSharpPathAxis.Descendant,
            "descendant-or-self" => FSharpPathAxis.DescendantOrSelf,
            "parent" => FSharpPathAxis.Parent,
            "ancestor" => FSharpPathAxis.Ancestor,
            "ancestor-or-self" => FSharpPathAxis.AncestorOrSelf,
            "following-sibling" => FSharpPathAxis.FollowingSibling,
            "preceding-sibling" => FSharpPathAxis.PrecedingSibling,
            "following" => FSharpPathAxis.Following,
            "preceding" => FSharpPathAxis.Preceding,
            _ => throw new ArgumentException($"Unknown axis: {axisName}")
        };
    }

    private FSharpNodeTest ParseNodeTest(string nodeTest)
    {
        if (string.IsNullOrEmpty(nodeTest) || nodeTest == "*")
        {
            return new FSharpNodeTest { Type = FSharpNodeType.Any };
        }

        // Check for F# specific node types
        var nodeType = nodeTest.ToLower() switch
        {
            "let" => FSharpNodeType.Let,
            "function" => FSharpNodeType.Function,
            "value" => FSharpNodeType.Value,
            "type" => FSharpNodeType.Type,
            "module" => FSharpNodeType.Module,
            "union" => FSharpNodeType.Union,
            "record" => FSharpNodeType.Record,
            "match" => FSharpNodeType.Match,
            "pattern" => FSharpNodeType.Pattern,
            "computation" => FSharpNodeType.Computation,
            "class" => FSharpNodeType.Class,
            "interface" => FSharpNodeType.Interface,
            "member" => FSharpNodeType.Member,
            "property" => FSharpNodeType.Property,
            _ => FSharpNodeType.Named
        };

        return new FSharpNodeTest
        {
            Type = nodeType,
            Name = nodeType == FSharpNodeType.Named ? nodeTest : null
        };
    }

    private List<FSharpPredicate> ParsePredicates(string predicateString)
    {
        var predicates = new List<FSharpPredicate>();
        var matches = Regex.Matches(predicateString, @"\[([^\[\]]+)\]");

        foreach (Match match in matches)
        {
            var predicateText = match.Groups[1].Value.Trim();
            var predicate = ParsePredicate(predicateText);
            predicates.Add(predicate);
        }

        return predicates;
    }

    private FSharpPredicate ParsePredicate(string predicateText)
    {
        // Check for position predicate (e.g., [1], [last()])
        if (int.TryParse(predicateText, out var position))
        {
            return new FSharpPredicate
            {
                Type = FSharpPredicateType.Position,
                Value = position.ToString()
            };
        }

        // Check for function calls
        if (predicateText.Contains("(") && predicateText.Contains(")"))
        {
            return ParseFunctionPredicate(predicateText);
        }

        // Check for attribute tests (e.g., [@name='test'])
        var attrMatch = Regex.Match(predicateText, @"@(\w+)\s*(=|!=|<|>|<=|>=)\s*['""]([^'""]+)['""]");
        if (attrMatch.Success)
        {
            return new FSharpPredicate
            {
                Type = FSharpPredicateType.Attribute,
                AttributeName = attrMatch.Groups[1].Value,
                Operator = attrMatch.Groups[2].Value,
                Value = attrMatch.Groups[3].Value
            };
        }

        // Check for boolean attributes (e.g., [@recursive], [@async])
        var boolAttrMatch = Regex.Match(predicateText, @"@(\w+)$");
        if (boolAttrMatch.Success)
        {
            return new FSharpPredicate
            {
                Type = FSharpPredicateType.BooleanAttribute,
                AttributeName = boolAttrMatch.Groups[1].Value
            };
        }

        // Check for type tests (e.g., [union], [record])
        if (IsTypeTest(predicateText))
        {
            return new FSharpPredicate
            {
                Type = FSharpPredicateType.TypeTest,
                Value = predicateText
            };
        }

        // Default to expression predicate
        return new FSharpPredicate
        {
            Type = FSharpPredicateType.Expression,
            Value = predicateText
        };
    }

    private FSharpPredicate ParseFunctionPredicate(string predicateText)
    {
        var funcMatch = Regex.Match(predicateText, @"(\w+)\s*\((.*)\)");
        if (funcMatch.Success)
        {
            var functionName = funcMatch.Groups[1].Value;
            var args = funcMatch.Groups[2].Value;

            return new FSharpPredicate
            {
                Type = FSharpPredicateType.Function,
                FunctionName = functionName,
                Value = args
            };
        }

        return new FSharpPredicate
        {
            Type = FSharpPredicateType.Expression,
            Value = predicateText
        };
    }

    private bool IsTypeTest(string text)
    {
        return text.ToLower() switch
        {
            "union" => true,
            "record" => true,
            "enum" => true,
            "struct" => true,
            "class" => true,
            "interface" => true,
            "abstract" => true,
            _ => false
        };
    }
}

/// <summary>
/// Represents a parsed FSharpPath query.
/// </summary>
public class FSharpPathQuery
{
    public List<FSharpPathStep> Steps { get; }

    public FSharpPathQuery(List<FSharpPathStep> steps)
    {
        Steps = steps ?? throw new ArgumentNullException(nameof(steps));
    }
}

/// <summary>
/// Represents a single step in an FSharpPath query.
/// </summary>
public class FSharpPathStep
{
    public FSharpPathAxis Axis { get; set; } = FSharpPathAxis.Child;
    public FSharpNodeTest NodeTest { get; set; } = new();
    public List<FSharpPredicate> Predicates { get; set; } = new();
}

/// <summary>
/// Represents a node test in an FSharpPath query.
/// </summary>
public class FSharpNodeTest
{
    public FSharpNodeType Type { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// Represents a predicate in an FSharpPath query.
/// </summary>
public class FSharpPredicate
{
    public FSharpPredicateType Type { get; set; }
    public string? AttributeName { get; set; }
    public string? Operator { get; set; }
    public string? Value { get; set; }
    public string? FunctionName { get; set; }
}

/// <summary>
/// F# specific node types.
/// </summary>
public enum FSharpNodeType
{
    Any,
    Named,
    Let,
    Function,
    Value,
    Type,
    Module,
    Union,
    Record,
    Match,
    Pattern,
    Computation,
    Class,
    Interface,
    Member,
    Property
}

/// <summary>
/// XPath-style axes for F# AST navigation.
/// </summary>
public enum FSharpPathAxis
{
    Child,
    Descendant,
    DescendantOrSelf,
    Parent,
    Ancestor,
    AncestorOrSelf,
    FollowingSibling,
    PrecedingSibling,
    Following,
    Preceding
}

/// <summary>
/// Types of predicates in FSharpPath queries.
/// </summary>
public enum FSharpPredicateType
{
    Position,
    Attribute,
    BooleanAttribute,
    Function,
    Expression,
    TypeTest
}