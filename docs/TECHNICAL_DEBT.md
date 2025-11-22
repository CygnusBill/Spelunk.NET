# Technical Debt - Spelunk.NET

This document tracks known technical debt, code quality issues, and areas for improvement in the Spelunk.NET codebase.

**Last Updated:** November 2025

## Status Overview

**Recent Improvements (Latest Session):**
- ✅ **Functional error handling with LanguageExt** - Replaced exception-based error handling with `Either<SpelunkError, T>` pattern
- ✅ **SpelunkPath function argument parsing** - Fixed parser to handle function arguments like `contains(@name, 'Test')`
- ✅ Clean build: **0 errors, 0 warnings**
- ✅ All **55 tests passing** (previously 46 passing + 9 skipped)

**Key Methods Refactored to Either Pattern:**
- `GetStatementContextAsync` → `Either<SpelunkError, StatementContextResult>`
- `GetDataFlowAnalysisAsync` → `Either<SpelunkError, DataFlowResult>`
- `FindReferencesAsync` → `Either<SpelunkError, List<ReferenceInfo>>`

**Previous Session Improvements:**
- ✅ Fixed all null reference warnings (CS8601, CS8602, CS8604, CS8600)
- ✅ Fixed thread-safety issues in MarkerManager
- ✅ Fixed fire-and-forget async tasks in ProcessManager
- ✅ Removed 13 redundant NuGet package references
- ✅ Added configurable server options (WorkspaceTimeout, MaxMarkers, etc.)
- ✅ Migrated obsolete Roslyn APIs (WorkspaceFailed event, Renamer API)
- ✅ **Removed obsolete transformer code** (StatementTransformer, FixPatternAsync, 600+ lines)

---

## Outstanding Technical Debt

### 1. ~~Obsolete Code~~ ✅ COMPLETED

#### 1.1 Statement Transformer - ✅ REMOVED (Current Session)

**What was removed:**
- ✅ `StatementTransformer.cs` (491 lines) - Entire file deleted
- ✅ `FixPatternAsync` method in DotnetWorkspaceManager.cs (~200 lines)
- ✅ Helper methods: `FindAsyncPatterns`, `FindNullCheckPatterns`, `FindStringFormatPatterns` (~285 lines)
- ✅ Result classes: `PatternFixResult`, `PatternFix` (~15 lines)
- ✅ MCP tool registration for `spelunk-fix-pattern` in McpJsonRpcServer.cs (~120 lines)
- ✅ Tool description constant in ToolDescriptions.cs
- ✅ DotnetFixPattern method in DotnetTools.cs (~25 lines)

**Total removed:** ~600+ lines of obsolete code

**Result:**
- Build warnings reduced from 40 to 18 (eliminated 22 obsolete warnings)
- All 46 tests passing
- Cleaner architecture following agent orchestration pattern
- **Time taken:** ~1.5 hours

---

### 2. Error Handling Standardization (Medium Priority)

#### 2.1 Inconsistent Exception Types
**Locations:** Throughout `DotnetWorkspaceManager.cs` and `McpJsonRpcServer.cs`

**Issue:** Mix of exception types used for different error conditions:
- `InvalidOperationException` for workspace issues
- `ArgumentException` for validation
- `NotSupportedException` for unsupported operations
- Generic `Exception` for some edge cases

**Examples:**
```csharp
// Line 1168: Uses NotSupportedException
throw new NotSupportedException($"Finding references for '{symbolType}' symbols is not currently supported...");

// Line 1175: Uses ArgumentException
throw new ArgumentException($"Invalid symbolType '{symbolType}'...");

// Many places: Returns error strings instead of throwing
result.Error = "Symbol not found";
result.Success = false;
```

**Recommendation:**
- Create custom exception hierarchy:
  - `SpelunkException` (base)
  - `WorkspaceNotFoundException`
  - `SymbolNotFoundException`
  - `InvalidPatternException`
  - `UnsupportedOperationException`
- Standardize on consistent error reporting pattern
- Document error handling guidelines

**Estimated Effort:** 3-4 hours

#### 2.2 Missing Exception Logging
**Locations:** Several catch blocks in `DotnetWorkspaceManager.cs`

**Issue:** Some catch blocks log exceptions, others don't, and some just return null/false

**Examples:**
```csharp
// Good: Logs exception
catch (Exception ex)
{
    _logger.LogError(ex, "Error during rename operation");
    result.Error = $"Rename failed: {ex.Message}";
}

// Needs improvement: No logging
catch
{
    return null;
}
```

**Recommendation:**
- Ensure all exception catch blocks log appropriately
- Add structured logging with context (workspace ID, file paths, operation type)
- Use different log levels based on severity

**Estimated Effort:** 2-3 hours

---

### 3. Performance Optimizations (Low Priority)

#### 3.1 Synchronous File Operations
**Locations:** Multiple places in `DotnetWorkspaceManager.cs`

**Issue:** Some file operations are synchronous and could block:
```csharp
var root = await document.GetSyntaxRootAsync(); // Good: Async
var text = sourceText.ToString(); // Could be slow for large files
```

**Recommendation:**
- Audit all file I/O operations
- Convert to async where beneficial
- Consider streaming for very large files

**Estimated Effort:** 2-3 hours

#### 3.2 No Result Caching
**Locations:** Symbol lookup operations in `FindReferencesAsync`, `FindMethodCallsAsync`, etc.

**Issue:** Repeated symbol lookups traverse the entire compilation each time. No caching of frequently accessed symbols or results.

**Example:**
```csharp
// This traverses all types every time
foreach (var type in compilation.Assembly.GetAllTypes())
{
    if (type.Name == symbolName)
        return type;
}
```

**Recommendation:**
- Implement LRU cache for symbol lookups
- Cache compilation results per workspace
- Add cache invalidation on document changes

**Estimated Effort:** 4-6 hours

#### 3.3 Sequential Processing
**Locations:** Workspace loading, reference finding

**Issue:** Many operations process projects/documents sequentially when they could be parallelized:
```csharp
foreach (var project in solution.Projects)
{
    // Could use Parallel.ForEach or Task.WhenAll
    var compilation = await project.GetCompilationAsync();
    // ... process
}
```

**Recommendation:**
- Use `Task.WhenAll` for independent async operations
- Use `Parallel.ForEach` for CPU-bound work
- Be careful with Roslyn workspace thread-safety

**Estimated Effort:** 3-4 hours

---

### 4. Testing Gaps (Medium Priority)

#### 4.1 Missing SpelunkPath Function Tests
**Location:** `tests/RoslynPath/RoslynPathFunctionTests.cs`

**Issue:** 9 tests skipped with TODO comments:
```csharp
[Fact(Skip = "TODO: SpelunkPath function argument parsing not yet implemented")]
public void TestFunctionWithStringArgument() { }
```

**Recommendation:**
- Implement function argument parsing in SpelunkPath
- Enable and update these tests
- Add more edge cases

**Estimated Effort:** 6-8 hours (includes implementation)

#### 4.2 Limited Integration Test Coverage
**Location:** `tests/tools/` directory

**Issue:** Python integration tests exist but don't cover:
- Multi-language scenarios (C# + VB.NET mixed solutions)
- Error conditions and edge cases
- Performance under load
- Configuration option validation

**Recommendation:**
- Add mixed-language test projects
- Add negative test cases (invalid inputs, timeouts, etc.)
- Add load testing scenarios
- Add configuration validation tests

**Estimated Effort:** 8-10 hours

#### 4.3 No End-to-End SSE Tests
**Issue:** SSE mode is tested manually but has no automated tests

**Recommendation:**
- Create automated SSE client tests
- Test background process lifecycle
- Test log file rotation
- Test multiple concurrent SSE connections

**Estimated Effort:** 4-6 hours

---

### 5. Code Organization (Low Priority)

#### 5.1 Large God Class: DotnetWorkspaceManager
**Location:** `src/Spelunk.Server/DotnetWorkspaceManager.cs` (5,700+ lines)

**Issue:** Single class handles:
- Workspace lifecycle
- Symbol finding
- Code editing
- Statement manipulation
- Marker management
- F# integration (commented out)
- Data flow analysis
- Renaming
- Reference finding

**Recommendation:**
- Split into focused services:
  - `WorkspaceService` - Lifecycle management
  - `SymbolService` - Symbol finding and resolution
  - `CodeEditService` - Statement manipulation
  - `AnalysisService` - Data flow, control flow
  - `RefactoringService` - Rename, transform
- Use dependency injection to compose
- Keep MarkerManager as separate class (already done)

**Estimated Effort:** 12-16 hours (significant refactoring)

#### 5.2 Marker Management Separate from Workspace
**Issue:** MarkerManager is tightly coupled but separate. Markers are workspace-specific but managed globally.

**Recommendation:**
- Consider per-workspace marker instances
- Or make relationship explicit in API

**Estimated Effort:** 2-3 hours

---

### 6. API Design (Low Priority)

#### 6.1 Inconsistent Return Types
**Issue:** Some methods return tuples, others return result objects, some return null for errors:

```csharp
// Tuple return
public async Task<(bool success, string message)> LoadWorkspaceAsync(...)

// Result object
public async Task<RenameResult> RenameSymbolAsync(...)

// Nullable return
public Workspace? GetWorkspace(string workspaceId)
```

**Recommendation:**
- Standardize on result objects with Success/Error properties
- Or use discriminated union pattern (F# Result<T, E> equivalent)
- Document conventions in architecture guide

**Estimated Effort:** 6-8 hours (would touch many signatures)

#### 6.2 Optional Parameters vs. Overloads
**Issue:** Heavy use of optional parameters makes some signatures complex:

```csharp
public async Task<List<ReferenceInfo>> FindReferencesAsync(
    string symbolName,
    string? symbolType = null,
    string? containerName = null,
    string? workspacePath = null)
```

**Recommendation:**
- Consider builder pattern for complex operations
- Or create specific overloads for common cases
- Document which parameters are commonly used together

**Estimated Effort:** 4-6 hours

---

### 7. Documentation (Medium Priority)

#### 7.1 Missing XML Documentation
**Locations:** Many public methods lack XML doc comments

**Issue:** IDE IntelliSense doesn't show parameter descriptions, return values, or usage examples.

**Recommendation:**
- Add XML doc comments to all public APIs
- Include `<example>` tags for complex methods
- Enable XML documentation generation in build

**Estimated Effort:** 6-8 hours

#### 7.2 Missing Architecture Decision Records (ADRs)
**Issue:** Major design decisions not documented:
- Why statement-level granularity?
- Why marker-based tracking instead of file positions?
- Why separate C#/VB.NET vs F# handling?

**Recommendation:**
- Create ADR documents in `docs/adr/`
- Follow ADR template format
- Link from architecture docs

**Estimated Effort:** 4-6 hours

---

## Priority Summary

### High Priority (Do Next)
1. **Remove Obsolete Transform Code** (1-2 hours) - PRE-RELEASE CLEANUP
   - Simply delete obsolete classes and tool (no migration needed)
   - Eliminates 25+ warnings

### Medium Priority (Near Term)
2. **Standardize Error Handling** (5-7 hours)
   - Custom exception hierarchy + consistent logging
3. **Complete SpelunkPath Function Tests** (6-8 hours)
   - 9 skipped tests need implementation
4. **Add XML Documentation** (6-8 hours)
   - Improve developer experience

### Low Priority (Long Term)
5. **Performance Optimizations** (9-13 hours total)
   - Caching, parallelization, async I/O
6. **Refactor DotnetWorkspaceManager** (12-16 hours)
   - Split into focused services
7. **API Design Consistency** (10-14 hours)
   - Standardize return types and signatures

### Nice to Have
8. **Integration Test Coverage** (8-10 hours)
9. **ADR Documentation** (4-6 hours)
10. **End-to-End SSE Tests** (4-6 hours)

---

## Total Estimated Effort
- **High Priority:** 1-2 hours
- **Medium Priority:** 17-23 hours
- **Low Priority:** 31-43 hours
- **Nice to Have:** 16-22 hours

**Grand Total:** 65-90 hours (approximately 2 weeks of focused work)

---

## Notes

### What's NOT Technical Debt
These are intentional design decisions, not debt:
- ✅ Statement-level granularity (core design principle)
- ✅ Marker-based tracking (survives edits better than positions)
- ✅ Separate F# handling (different compiler, justified)
- ✅ MCP protocol over custom API (standard integration)

### Monitoring Debt Growth
- Run `dotnet build` and check warning count (currently: 0)
- Run `dotnet test` and check test pass rate (currently: 46/55 = 83.6%)
- Monitor file size of DotnetWorkspaceManager.cs (currently: 5,700+ lines)
- Check package count (currently: 6 essential packages)

### When to Address Debt
- Before major refactoring: Clean up related areas first
- Before adding similar features: Fix patterns that will be copied
- When it blocks productivity: Prioritize what slows development
- During slow periods: Tackle low-hanging fruit

---

## Change Log

**November 2025 (Current Session):**
- ✅ Fixed all null reference warnings
- ✅ Fixed thread-safety issues
- ✅ Removed redundant packages
- ✅ Added configurable server options
- ✅ Migrated obsolete Roslyn APIs
- Created this technical debt document

**Next Review:** After completing high-priority items or before next major release
