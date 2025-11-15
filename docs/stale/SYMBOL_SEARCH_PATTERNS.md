# Unified Symbol Search Pattern Syntax

## Overview
A single `spelunk-find-symbol` tool that uses pattern matching to find any symbol type in the codebase.

## Pattern Syntax

### Basic Patterns
- `foo` - Find any symbol named "foo" (class, method, property, field, variable, etc.)
- `*foo*` - Find any symbol containing "foo" (wildcard search)
- `foo*` - Find symbols starting with "foo"
- `*foo` - Find symbols ending with "foo"

### Type Filters
- `foo /class` - Find only classes named "foo"
- `foo /interface` - Find only interfaces
- `foo /method` - Find only methods
- `foo /property` - Find only properties
- `foo /field` - Find only fields
- `foo /local` - Find only local variables
- `foo /parameter` - Find only parameters
- `foo /enum` - Find only enums
- `foo /struct` - Find only structs
- `foo /delegate` - Find only delegates
- `foo /event` - Find only events
- `foo /namespace` - Find only namespaces

### Container/Scope Patterns
- `Controller.foo` - Find "foo" inside "Controller" class
- `*.foo` - Find "foo" in any container
- `UserController.Get*` - Find methods starting with "Get" in UserController
- `namespace.class.method` - Full path specification

### Signature Patterns (for methods)
- `foo()` - Method with no parameters
- `foo(int)` - Method with one int parameter
- `foo(int, string)` - Method with int and string parameters
- `foo(*)` - Method with any single parameter
- `foo(*,*)` - Method with any two parameters
- `foo(...)` - Method with any number of parameters

### Modifiers
- `foo /public` - Only public symbols
- `foo /private` - Only private symbols
- `foo /static` - Only static symbols
- `foo /async` - Only async methods
- `foo /virtual` - Only virtual methods
- `foo /override` - Only override methods
- `foo /abstract` - Only abstract methods

### Combined Patterns
- `Get* /method /public` - Public methods starting with "Get"
- `_* /field /private` - Private fields starting with underscore
- `*Async /method /async` - Async methods ending with "Async"
- `UserController.* /method /public` - All public methods in UserController
- `I* /interface` - All interfaces (convention: start with I)
- `*Repository /class` - All repository classes
- `Process*(string) /method` - Methods starting with "Process" that take a string

### Special Patterns
- `@implements:IDisposable` - Classes implementing IDisposable
- `@derives:BaseController` - Classes deriving from BaseController
- `@returns:Task` - Methods returning Task
- `@returns:Task<*>` - Methods returning Task<T>
- `@calls:Log` - Methods that call a Log method
- `@calledby:Process` - Methods called by Process method

## Examples

1. Find all variables named "count":
   - `count /local`
   - `count /field`
   - `count /parameter`

2. Find all async methods in UserController:
   - `UserController.* /method /async`

3. Find all private fields starting with underscore:
   - `_* /field /private`

4. Find all methods that take an int and return Task:
   - `*(int) @returns:Task`

5. Find all classes implementing ILogger:
   - `@implements:ILogger`

6. Find all public properties in any Controller:
   - `*Controller.* /property /public`

7. Find local variable "i" (common in loops):
   - `i /local`

8. Find all static factory methods:
   - `Create* /method /static`

## Implementation Benefits
- Single tool for all symbol searches
- Consistent pattern syntax
- Easy for agents to construct queries
- Supports complex filtering and matching
- Extensible for future needs