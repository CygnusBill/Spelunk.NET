# F# Compiler Service Integration

F# has its own compiler service separate from Roslyn, providing code analysis capabilities.

## Key Information
- **Package**: FSharp.Compiler.Service
- **Main API**: FSharpChecker for code analysis
- **Not Roslyn-based**: F# uses its own compiler infrastructure

## Key Namespaces (from F# docs)
- `FSharp.Compiler.CodeAnalysis` - FSharpChecker, FSharpCheckFileResults, FSharpCheckProjectResults
- `FSharp.Compiler.Symbols` - FSharpEntity and symbol APIs
- `FSharp.Compiler.Syntax` - SyntaxTree, XmlDoc, PrettyNaming
- `FSharp.Compiler.Text` - ISourceFile, Range, TaggedText
- `FSharp.Compiler.Tokenization` - FSharpLineTokenizer
- `FSharp.Compiler.Diagnostics` - FSharpDiagnostic
- `FSharp.Compiler.EditorServices` - Editor functionality

## Key Components
- **FSharpChecker**: Main entry point for F# code analysis
- **FSharpProjectOptions**: Project configuration
- **ParseFile/CheckFile**: Parsing and type checking APIs
- Supports .fsproj files
- Can analyze individual F# projects but not cross-project with C#/VB

## Limitations
- Separate from Roslyn workspace model
- Cannot participate in cross-language analysis with C#/VB
- Requires separate handling from Roslyn-based languages