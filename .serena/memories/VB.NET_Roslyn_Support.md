# VB.NET Support in Roslyn

VB.NET is fully supported by Roslyn (Microsoft.CodeAnalysis) alongside C#. Key information:

## Roslyn VB.NET Support
- VB.NET has full Roslyn compiler support
- Uses the same workspace/project/document model as C#
- Supports .vbproj files
- Has its own syntax tree, semantic model, and compilation APIs
- Located in Microsoft.CodeAnalysis.VisualBasic namespace

## Key Namespaces
- `Microsoft.CodeAnalysis.VisualBasic` - Main VB.NET compiler APIs
- `Microsoft.CodeAnalysis.VisualBasic.Syntax` - VB syntax tree APIs
- `Microsoft.CodeAnalysis.VisualBasic.Symbols` - VB symbol APIs

## Integration Points
- LanguageNames.VisualBasic constant for language identification
- VisualBasicSyntaxTree for parsing VB code
- VisualBasicCompilation for VB compilation
- Same workspace APIs work with both C# and VB.NET projects