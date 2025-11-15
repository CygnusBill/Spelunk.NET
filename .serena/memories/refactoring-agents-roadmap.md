# Refactoring Agents Roadmap

## Completed Agents
1. **SQL_PARAMETERIZATION_AGENT** - Converts SQL queries to parameterized queries
   - Status: Documented, ready for testing
   - Location: docs/agents/SQL_PARAMETERIZATION_AGENT.md
   
2. **ASYNC_CONVERSION_AGENT** - Converts synchronous operations to async/await
   - Status: Fully documented with Classic ASP.NET and VB.NET patterns
   - Location: docs/agents/ASYNC_CONVERSION_AGENT.md
   - Special focus: Complete lifecycle consolidation, regression prevention

## Priority Queue (To Be Created)

### Tier 1 - Critical Safety & Reliability
1. **NULL_SAFETY_AGENT** - Prevent NullReferenceException
2. **EXCEPTION_HANDLING_AGENT** - Consistent error management
3. **RESOURCE_DISPOSAL_AGENT** - Proper IDisposable implementation
4. **VALIDATION_AGENT** - Input validation

### Tier 2 - Modern .NET Practices
5. **DEPENDENCY_INJECTION_AGENT** - Constructor injection patterns
6. **CONFIGURATION_AGENT** - Externalize hardcoded values
7. **TEST_GENERATION_AGENT** - Unit test creation
8. **NAMING_CONVENTION_AGENT** - .NET naming standards

### Tier 3 - Performance & Optimization
9. **LINQ_OPTIMIZATION_AGENT** - LINQ performance improvements
10. **CACHING_AGENT** - Caching strategies
11. **PERFORMANCE_OPTIMIZATION_AGENT** - General optimizations

### Tier 4 - Architecture Patterns
12. **REPOSITORY_PATTERN_AGENT** - Data access abstraction
13. **MAPPER_AGENT** - DTO mapping
14. **MIDDLEWARE_PIPELINE_AGENT** - ASP.NET Core middleware
15. **IMMUTABILITY_AGENT** - Functional patterns

### Tier 5 - Advanced Patterns
16. **API_MODERNIZATION_AGENT** - Update to modern APIs
17. **SECURITY_HARDENING_AGENT** - Security fixes
18. **LOCALIZATION_AGENT** - i18n support
19. **EVENT_SOURCING_AGENT** - Event-driven patterns
20. **GRPC_MIGRATION_AGENT** - REST to gRPC

## Next Steps
- Test SQL_PARAMETERIZATION_AGENT on real production code
- Test ASYNC_CONVERSION_AGENT on Classic ASP.NET projects
- Start with NULL_SAFETY_AGENT as highest priority new agent