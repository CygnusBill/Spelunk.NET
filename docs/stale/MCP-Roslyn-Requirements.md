# MCP Roslyn Server Requirements Specification

## Document Information
- **Version**: 1.0.0
- **Date**: 2025-01-29
- **Status**: Draft
- **Requirement Prefix**: MCP-ROSLYN

## Overview

The MCP Roslyn Server provides tools for inspecting, analyzing, and eventually refactoring .NET code using Microsoft's Roslyn compiler platform. This server enables AI assistants to perform deep code analysis, understand .NET project structures, and provide intelligent code assistance.

## Requirements Tracking Legend

- **ARCH**: Architecture requirements
- **TOOL**: Tool/feature requirements  
- **PERF**: Performance requirements
- **SEC**: Security requirements
- **CONFIG**: Configuration requirements
- **INT**: Integration requirements

## Requirements Status Tracking

### Status Definitions
- **📋 Specified**: Requirement defined
- **🚧 In Progress**: Currently being implemented
- **✅ Implemented**: Code complete
- **🧪 Tested**: Tests passing
- **📦 Delivered**: Released/deployed

## Architecture Requirements

| ID | Priority | Status | Description |
|---|---|---|---|
| MCP-ROSLYN-ARCH-001 | P0 | 📋 Specified | **Roslyn Integration** - Utilize Microsoft.CodeAnalysis APIs, support C#/VB.NET, provide syntax tree and semantic model access |
| MCP-ROSLYN-ARCH-002 | P0 | 📋 Specified | **MCP Persistent Server** - Long-running server process, JSON-RPC 2.0 over stdio, maintain loaded workspaces between requests |
| MCP-ROSLYN-ARCH-003 | P0 | 📋 Specified | **Project/Solution Management** - MSBuild integration, solution-wide analysis, reference resolution |
| MCP-ROSLYN-ARCH-004 | P0 | 📋 Specified | **Workspace Lifecycle** - Persistent workspace caching, lazy loading, warm-up on startup, workspace eviction policies |
| MCP-ROSLYN-ARCH-005 | P0 | 📋 Specified | **Solution Loading Strategy** - Background solution loading, incremental project loading, progress reporting |

## Tool Requirements

### Code Inspection Tools

| ID | Priority | Status | Tool Name | Description |
|---|---|---|---|---|
| MCP-ROSLYN-TOOL-001 | P0 | 📋 Specified | `dotnet-analyze-syntax` | Analyze syntax tree of C#/VB.NET files |
| MCP-ROSLYN-TOOL-002 | P0 | 📋 Specified | `dotnet-get-symbols` | Retrieve symbol information from code |
| MCP-ROSLYN-TOOL-003 | P0 | 📋 Specified | `dotnet-find-references` | Find all references to a symbol |
| MCP-ROSLYN-TOOL-004 | P0 | 📋 Specified | `dotnet-get-diagnostics` | Retrieve compiler diagnostics and warnings |

### Code Understanding Tools

| ID | Priority | Status | Tool Name | Description |
|---|---|---|---|---|
| MCP-ROSLYN-TOOL-005 | P1 | 📋 Specified | `dotnet-get-type-hierarchy` | Retrieve inheritance hierarchy |
| MCP-ROSLYN-TOOL-006 | P1 | 📋 Specified | `dotnet-analyze-dependencies` | Analyze project dependencies |
| MCP-ROSLYN-TOOL-007 | P2 | 📋 Specified | `dotnet-get-metrics` | Calculate code metrics |

### Navigation Tools

| ID | Priority | Status | Tool Name | Description |
|---|---|---|---|---|
| MCP-ROSLYN-TOOL-008 | P1 | 📋 Specified | `dotnet-go-to-definition` | Navigate to symbol definition |
| MCP-ROSLYN-TOOL-009 | P1 | 📋 Specified | `dotnet-find-implementations` | Find interface implementations |

### Workspace Management Tools

| ID | Priority | Status | Tool Name | Description |
|---|---|---|---|---|
| MCP-ROSLYN-TOOL-010 | P0 | 📋 Specified | `dotnet-load-workspace` | Load solution/project into workspace |
| MCP-ROSLYN-TOOL-011 | P0 | 📋 Specified | `dotnet-workspace-status` | Get loading progress and workspace info |
| MCP-ROSLYN-TOOL-012 | P1 | 📋 Specified | `dotnet-unload-workspace` | Remove workspace from memory |

### Future Tools (Phase 2)

| ID | Priority | Status | Tool Name | Description |
|---|---|---|---|---|
| MCP-ROSLYN-TOOL-013 | P2 | 📋 Specified | `dotnet-refactor` | Code refactoring operations |
| MCP-ROSLYN-TOOL-014 | P2 | 📋 Specified | `dotnet-quick-fix` | Apply compiler-suggested fixes |
| MCP-ROSLYN-TOOL-015 | P3 | 📋 Specified | `dotnet-format` | Format code per EditorConfig |

## Performance Requirements

| ID | Priority | Status | Description | Target |
|---|---|---|---|---|
| MCP-ROSLYN-PERF-001 | P1 | 📋 Specified | **Response Time** | <100ms for cached operations, <5s single file |
| MCP-ROSLYN-PERF-002 | P1 | 📋 Specified | **Memory Usage** | <2GB typical solutions, <4GB large solutions |
| MCP-ROSLYN-PERF-003 | P1 | 📋 Specified | **Caching** | Compilation cache, symbol index, LRU eviction |
| MCP-ROSLYN-PERF-004 | P2 | 📋 Specified | **Concurrent Operations** | Support 10 concurrent invocations |
| MCP-ROSLYN-PERF-005 | P0 | 📋 Specified | **Startup Time** | Server ready <2s, background loading for solutions |
| MCP-ROSLYN-PERF-006 | P0 | 📋 Specified | **Solution Loading** | Progressive loading, usable before fully loaded |

## Security Requirements

| ID | Priority | Status | Description |
|---|---|---|---|
| MCP-ROSLYN-SEC-001 | P0 | 📋 Specified | **File System Access** - Configurable root restrictions, no system access |
| MCP-ROSLYN-SEC-002 | P0 | 📋 Specified | **Code Execution** - No dynamic execution, sandboxed compilation |
| MCP-ROSLYN-SEC-003 | P1 | 📋 Specified | **Error Information** - Sanitized paths, no env var exposure |

## Configuration Requirements

| ID | Priority | Status | Description |
|---|---|---|---|
| MCP-ROSLYN-CONFIG-001 | P1 | 📋 Specified | **Server Configuration** - JSON config, env overrides, runtime updates |
| MCP-ROSLYN-CONFIG-002 | P1 | 📋 Specified | **Workspace Discovery** - Auto .sln discovery, project patterns |

## Integration Requirements

| ID | Priority | Status | Description |
|---|---|---|---|
| MCP-ROSLYN-INT-001 | P0 | 📋 Specified | **MCP Protocol Compliance** - Full MCP 1.0 support |
| MCP-ROSLYN-INT-002 | P0 | 📋 Specified | **Error Handling** - Standard JSON-RPC errors, graceful degradation |
| MCP-ROSLYN-INT-003 | P1 | 📋 Specified | **Logging** - Structured logging, configurable levels |

## Testing Requirements

| ID | Priority | Status | Description | Target |
|---|---|---|---|---|
| MCP-ROSLYN-TEST-001 | P0 | 📋 Specified | **Unit Testing** | >80% coverage |
| MCP-ROSLYN-TEST-002 | P1 | 📋 Specified | **Integration Testing** | E2E protocol tests |
| MCP-ROSLYN-TEST-003 | P1 | 📋 Specified | **Test Projects** | Sample solutions |

## Detailed Tool Specifications

### MCP-ROSLYN-TOOL-001: `dotnet-analyze-syntax`

**Parameters:**
```typescript
{
  filePath: string;           // Required: Path to source file
  includeTrivia?: boolean;    // Optional: Include whitespace/comments
}
```

**Returns:**
```typescript
{
  syntaxTree: {
    root: SyntaxNode;
    errors: SyntaxError[];
  };
  nodeTypes: NodeTypeInfo[];
}
```

### MCP-ROSLYN-TOOL-002: `dotnet-get-symbols`

**Parameters:**
```typescript
{
  filePath: string;           // Required: Path to source file
  position?: {                // Optional: Specific position
    line: number;
    column: number;
  };
  symbolName?: string;        // Optional: Symbol to find
}
```

**Returns:**
```typescript
{
  symbols: SymbolInfo[];
  declarations: Location[];
  references: Reference[];
  documentation: string;
}
```

### MCP-ROSLYN-TOOL-003: `dotnet-find-references`

**Parameters:**
```typescript
{
  filePath: string;           // Required: Source file
  position: {                 // Required: Symbol position
    line: number;
    column: number;
  };
}
```

**Returns:**
```typescript
{
  references: Array<{
    location: Location;
    kind: "read" | "write" | "declaration";
    context: string;
  }>;
}
```

### MCP-ROSLYN-TOOL-010: `dotnet-load-workspace`

**Parameters:**
```typescript
{
  path: string;               // Required: Path to .sln or .csproj
  preloadProjects?: string[]; // Optional: Projects to load immediately
  backgroundLoad?: boolean;   // Optional: Load remaining projects in background
}
```

**Returns:**
```typescript
{
  workspaceId: string;
  totalProjects: number;
  loadedProjects: number;
  status: "loading" | "ready" | "partial";
}
```

### MCP-ROSLYN-TOOL-011: `dotnet-workspace-status`

**Parameters:**
```typescript
{
  workspaceId?: string;       // Optional: Specific workspace
}
```

**Returns:**
```typescript
{
  workspaces: Array<{
    id: string;
    path: string;
    loadedAt: string;
    projectCount: number;
    loadedProjects: number;
    memoryUsage: number;
    compilationStatus: Record<string, boolean>;
  }>;
}
```

## Workspace Caching Strategy

### MCP-ROSLYN-CACHE-001: Solution Cache
- **Priority**: P0
- **Description**: Pre-loaded solution metadata
- **Implementation**:
  - Cache solution structure on first load
  - Store project dependency graph
  - Persist symbol index between sessions
  - Warm-up frequently used solutions on startup

### MCP-ROSLYN-CACHE-002: Compilation Cache
- **Priority**: P0
- **Description**: Cached compilation results
- **Implementation**:
  - In-memory compilation cache with TTL
  - Incremental compilation support
  - File change monitoring for cache invalidation
  - Memory-mapped file support for large caches

## Implementation Phases

### Phase 1: MVP (Target: Q1 2025)
- [ ] All P0 requirements
- [ ] Basic tool implementation
- [ ] Core documentation
- [ ] Initial testing

### Phase 2: GA (Target: Q2 2025)
- [ ] All P1 requirements
- [ ] Performance optimization
- [ ] Security hardening
- [ ] Production readiness

### Phase 3: Enhanced (Target: Q3 2025)
- [ ] P2/P3 requirements
- [ ] Refactoring tools
- [ ] Advanced analysis
- [ ] IDE integrations

## Server Lifecycle

### MCP-ROSLYN-LIFE-001: Server Startup
- **Priority**: P0
- **Description**: Server initialization sequence
- **Steps**:
  1. Load configuration
  2. Initialize Roslyn workspace
  3. Start MCP protocol handler
  4. Begin background solution pre-loading
  5. Report ready status

### MCP-ROSLYN-LIFE-002: Workspace Persistence
- **Priority**: P0  
- **Description**: Maintain workspaces across requests
- **Implementation**:
  - Keep solutions loaded between tool calls
  - Workspace timeout after inactivity (configurable)
  - Maximum workspace count limits
  - LRU eviction when limits reached

## Change Log

| Date | Version | Changes |
|---|---|---|
| 2025-01-29 | 1.0.0 | Initial specification created |
| 2025-01-29 | 1.1.0 | Added persistent server architecture, workspace management tools, and caching requirements |

## Notes

- This document will be updated after each implementation milestone
- Status changes should be tracked in the change log
- New requirements should follow the existing ID pattern