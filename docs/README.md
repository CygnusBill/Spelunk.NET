# Spelunk.NET Documentation

## Overview

This directory contains the current documentation for the Spelunk.NET.

## Documentation Structure

### Core Documentation

- **[TOOL_SYNOPSIS.md](./TOOL_SYNOPSIS.md)** - Comprehensive reference for all 24 implemented MCP tools with formats, examples, and usage patterns

### Design Documents (`design/`)

- **[STATEMENT_LEVEL_EDITING.md](./design/STATEMENT_LEVEL_EDITING.md)** - Core philosophy behind statement-level code operations
- **[EPHEMERAL_MARKER_DESIGN.md](./design/EPHEMERAL_MARKER_DESIGN.md)** - Design for the marker system that tracks statements through edits

### SpelunkPath Documentation (`roslyn-path/`)

SpelunkPath is our XPath-inspired syntax for stable code navigation:

- **[SPELUNK_PATH_SYNTAX_DESIGN.md](./roslyn-path/SPELUNK_PATH_SYNTAX_DESIGN.md)** - Complete syntax design and rationale
- **[SPELUNK_PATH_INSTRUCTIONS.md](./roslyn-path/SPELUNK_PATH_INSTRUCTIONS.md)** - Quick reference for using SpelunkPath
- **[SPELUNK_PATH_AGENT_GUIDE.md](./roslyn-path/SPELUNK_PATH_AGENT_GUIDE.md)** - 5-minute guide for AI agents
- **[SPELUNK_PATH_ANALYSIS_EXAMPLES.md](./roslyn-path/SPELUNK_PATH_ANALYSIS_EXAMPLES.md)** - Powerful analysis patterns (security, performance, quality)
- **[SPELUNK_PATH_TEST_PACKAGE.md](./roslyn-path/SPELUNK_PATH_TEST_PACKAGE.md)** - Test package for evaluating with different agents

### Archived Documentation (`stale/`)

Contains early design documents and requirements that have been superseded by current implementations. These are kept for historical reference but should not be used for current development.

## Quick Links

- **Using the Tools**: Start with [TOOL_SYNOPSIS.md](./TOOL_SYNOPSIS.md)
- **Understanding SpelunkPath**: Read [SPELUNK_PATH_INSTRUCTIONS.md](./roslyn-path/SPELUNK_PATH_INSTRUCTIONS.md)
- **AI Agent Integration**: See [SPELUNK_PATH_AGENT_GUIDE.md](./roslyn-path/SPELUNK_PATH_AGENT_GUIDE.md)
- **Design Philosophy**: Read [STATEMENT_LEVEL_EDITING.md](./design/STATEMENT_LEVEL_EDITING.md)

## Key Concepts

1. **Statement-Level Operations** - All code modifications work at the statement level for optimal granularity
2. **SpelunkPath** - Stable code references that survive edits (like `/class[UserService]/method[GetUser]`)
3. **Ephemeral Markers** - Temporary statement tracking within a session
4. **Tool Composition** - Complex refactorings built from simple, composable tools