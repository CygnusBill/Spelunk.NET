#!/usr/bin/env python3
"""Comprehensive test of all MCP Dotnet tools to ensure valuable outcomes."""

import json
import subprocess
import os
import sys
from typing import Dict, Any, Optional, List

class MCPToolTester:
    def __init__(self, workspace_path: str):
        self.workspace_path = workspace_path
        env = os.environ.copy()
        env['SPELUNK_ALLOWED_PATHS'] = workspace_path
        
        self.process = subprocess.Popen(
            ['dotnet', 'run', '--project', 'src/Spelunk.Server', '--no-build'],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            env=env
        )
        self.request_id = 0
        self.results = []
        
    def send_request(self, tool: str, params: Dict[str, Any]) -> Optional[Dict]:
        """Send a tool request and get response."""
        self.request_id += 1
        request = {
            "jsonrpc": "2.0",
            "id": self.request_id,
            "method": "tools/call",
            "params": {
                "name": tool,
                "arguments": params
            }
        }
        
        try:
            json.dump(request, self.process.stdin)
            self.process.stdin.write('\n')
            self.process.stdin.flush()
            
            response_line = self.process.stdout.readline()
            if response_line:
                return json.loads(response_line)
        except Exception as e:
            return {"error": str(e)}
        return None
    
    def test_tool(self, tool_name: str, params: Dict[str, Any], 
                  description: str, expected_behavior: str) -> Dict:
        """Test a single tool and analyze results."""
        print(f"\n{'='*60}")
        print(f"Testing: {tool_name}")
        print(f"Description: {description}")
        print(f"Expected: {expected_behavior}")
        print("-" * 60)
        
        response = self.send_request(tool_name, params)
        
        result = {
            "tool": tool_name,
            "description": description,
            "params": params,
            "expected": expected_behavior,
            "status": "unknown",
            "outcome": None,
            "remedy": None
        }
        
        if not response:
            result["status"] = "no_response"
            result["remedy"] = "Server may be down or request timed out"
            print("❌ No response received")
            
        elif "error" in response:
            error = response["error"]
            result["status"] = "error"
            result["outcome"] = error
            
            # Analyze error for remedy
            if error and isinstance(error, dict) and "message" in error:
                msg = error["message"]
                if "No workspace loaded" in msg:
                    result["remedy"] = "Load a workspace first using dotnet-load-workspace"
                elif "not found" in msg:
                    result["remedy"] = "Check file paths and ensure target exists"
                elif "Invalid" in msg:
                    result["remedy"] = f"Check parameter format: {msg}"
                else:
                    result["remedy"] = "Review error message and adjust parameters"
                print(f"❌ Error: {msg}")
            else:
                result["remedy"] = "Unknown error - check server logs"
                print(f"❌ Error: {error if error else 'Unknown error'}")
            
            if "remedy" in result:
                print(f"💡 Remedy: {result['remedy']}")
            
        elif "result" in response:
            result_data = response["result"]
            
            # Check for empty/null results
            if not result_data or (isinstance(result_data, dict) and "content" in result_data and not result_data["content"]):
                result["status"] = "empty"
                result["remedy"] = "No results found - verify search criteria or target exists"
                print("⚠️ Empty result - no matching items found")
                
            # Check for warnings in result
            elif isinstance(result_data, dict) and "content" in result_data:
                content = result_data["content"]
                if content and len(content) > 0:
                    try:
                        parsed = json.loads(content[0]["text"]) if isinstance(content[0], dict) else None
                        if parsed and "Warnings" in parsed:
                            result["status"] = "warning"
                            result["outcome"] = parsed["Warnings"]
                            result["remedy"] = self.analyze_warnings(parsed["Warnings"])
                            print(f"⚠️ Warnings: {parsed['Warnings']}")
                            print(f"💡 Remedy: {result['remedy']}")
                        else:
                            result["status"] = "success"
                            result["outcome"] = "Data returned successfully"
                            print("✅ Success - valuable data returned")
                    except:
                        result["status"] = "success"
                        result["outcome"] = "Data returned (non-JSON)"
                        print("✅ Success - data returned")
            else:
                result["status"] = "success" 
                result["outcome"] = "Operation completed"
                print("✅ Success")
                
        self.results.append(result)
        return result
    
    def analyze_warnings(self, warnings: List[Dict]) -> str:
        """Analyze warnings to provide remedies."""
        remedies = []
        for warning in warnings:
            warn_type = warning.get("Type", "")
            if "ControlFlowError" in warn_type:
                remedies.append("Select complete statements within a single block")
            elif "DataFlowError" in warn_type:
                remedies.append("Ensure region has valid syntax")
            elif "AnalysisError" in warn_type:
                remedies.append("Check that region boundaries are valid")
            else:
                remedies.append(warning.get("Message", "Review warning details"))
        return "; ".join(remedies) if remedies else "Review warnings and adjust request"
    
    def close(self):
        """Close the MCP server."""
        self.process.stdin.close()
        self.process.terminate()
        self.process.wait()
    
    def print_summary(self):
        """Print test summary."""
        print("\n" + "="*80)
        print("TEST SUMMARY")
        print("="*80)
        
        by_status = {}
        for result in self.results:
            status = result["status"]
            by_status[status] = by_status.get(status, 0) + 1
        
        print(f"Total tools tested: {len(self.results)}")
        for status, count in by_status.items():
            emoji = {"success": "✅", "error": "❌", "warning": "⚠️", 
                    "empty": "📭", "no_response": "🔇"}.get(status, "❓")
            print(f"{emoji} {status}: {count}")
        
        # Print tools needing attention
        print("\n" + "="*60)
        print("TOOLS NEEDING ATTENTION:")
        print("="*60)
        
        for result in self.results:
            if result["status"] not in ["success"]:
                print(f"\n{result['tool']}:")
                print(f"  Status: {result['status']}")
                print(f"  Remedy: {result['remedy']}")

# Create tester
print("Starting comprehensive tool testing...")
tester = MCPToolTester("/Users/bill/Repos/SampleAppForMcp")

# Test 1: Workspace Loading Tools
print("\n" + "="*80)
print("CATEGORY 1: WORKSPACE AND LOADING TOOLS")
print("="*80)

tester.test_tool(
    "spelunk-load-workspace",
    {"path": "/Users/bill/Repos/SampleAppForMcp/SampleAppForMcp.sln"},
    "Load a solution file",
    "Should load workspace and return project info"
)

tester.test_tool(
    "spelunk-workspace-status",
    {},
    "Get workspace status after loading",
    "Should return loading progress and workspace info"
)

# Test 2: Symbol Discovery Tools
print("\n" + "="*80)
print("CATEGORY 2: SYMBOL DISCOVERY TOOLS")
print("="*80)

tester.test_tool(
    "spelunk-find-class",
    {"pattern": "Program"},
    "Find class by name pattern",
    "Should find Program class"
)

tester.test_tool(
    "spelunk-find-class",
    {"pattern": "NonExistent*"},
    "Find non-existent class pattern",
    "Should return empty with clear message"
)

tester.test_tool(
    "spelunk-find-method",
    {"methodPattern": "Main"},
    "Find Main method",
    "Should find Main method"
)

tester.test_tool(
    "spelunk-find-property",
    {"propertyPattern": "*"},
    "Find all properties",
    "Should list properties or indicate none exist"
)

# Test 3: Reference and Inheritance Tools
print("\n" + "="*80)
print("CATEGORY 3: REFERENCE AND INHERITANCE TOOLS")
print("="*80)

tester.test_tool(
    "spelunk-find-references",
    {"symbolName": "Main"},
    "Find references to Main method",
    "Should find references or indicate none"
)

tester.test_tool(
    "spelunk-find-method-callers",
    {"methodName": "WriteLine"},
    "Find callers of WriteLine",
    "Should find methods calling WriteLine"
)

tester.test_tool(
    "spelunk-find-method-calls",
    {"methodName": "Main"},
    "Find methods called by Main",
    "Should list methods called from Main"
)

tester.test_tool(
    "spelunk-find-derived-types",
    {"baseClassName": "Object"},
    "Find types derived from Object",
    "Should find derived types"
)

tester.test_tool(
    "spelunk-find-implementations",
    {"interfaceName": "IDisposable"},
    "Find IDisposable implementations",
    "Should find implementations or indicate none"
)

tester.test_tool(
    "spelunk-find-overrides",
    {"methodName": "ToString", "className": "Object"},
    "Find ToString overrides",
    "Should find overrides or indicate none"
)

# Test 4: Statement-Level Tools
print("\n" + "="*80)
print("CATEGORY 4: STATEMENT-LEVEL TOOLS")
print("="*80)

test_file = "/Users/bill/Repos/SampleAppForMcp/ConsoleApp/Program.cs"

tester.test_tool(
    "spelunk-find-statements",
    {"pattern": "Console.WriteLine", "filePath": test_file},
    "Find Console.WriteLine statements",
    "Should find print statements with IDs"
)

tester.test_tool(
    "spelunk-find-statements",
    {"pattern": "//if-statement", "patternType": "roslynpath"},
    "Find if statements using RoslynPath",
    "Should find if statements or indicate none"
)

# Test 5: Marker System Tools
print("\n" + "="*80)
print("CATEGORY 5: MARKER SYSTEM TOOLS")
print("="*80)

tester.test_tool(
    "spelunk-mark-statement",
    {"filePath": test_file, "line": 10, "column": 1},
    "Mark a statement at line 10",
    "Should mark statement or explain why it can't"
)

tester.test_tool(
    "spelunk-find-marked-statements",
    {},
    "Find all marked statements",
    "Should list marked statements or indicate none"
)

tester.test_tool(
    "spelunk-clear-markers",
    {},
    "Clear all markers",
    "Should clear markers successfully"
)

# Test 6: Analysis Tools
print("\n" + "="*80)
print("CATEGORY 6: ANALYSIS TOOLS")
print("="*80)

tester.test_tool(
    "spelunk-analyze-syntax",
    {"filePath": test_file},
    "Analyze syntax tree of a file",
    "Should return AST analysis"
)

tester.test_tool(
    "spelunk-get-symbols",
    {"filePath": test_file},
    "Get symbols from a file",
    "Should return symbol information"
)

tester.test_tool(
    "spelunk-get-statement-context",
    {"file": test_file, "line": 10, "column": 1},
    "Get context for a statement",
    "Should return semantic context or explain requirements"
)

tester.test_tool(
    "spelunk-get-data-flow",
    {
        "file": test_file,
        "startLine": 10, "startColumn": 1,
        "endLine": 15, "endColumn": 1,
        "includeControlFlow": False
    },
    "Get data flow for a region",
    "Should return data flow or explain region requirements"
)

# Test 7: Modification Tools
print("\n" + "="*80)
print("CATEGORY 7: MODIFICATION TOOLS")
print("="*80)

tester.test_tool(
    "spelunk-rename-symbol",
    {"oldName": "NonExistentMethod", "newName": "NewName", "preview": True},
    "Rename non-existent symbol (preview)",
    "Should indicate symbol not found"
)

tester.test_tool(
    "spelunk-edit-code",
    {
        "file": test_file,
        "operation": "add-method",
        "className": "Program",
        "code": "public void TestMethod() { }",
        "preview": True
    },
    "Add method to Program class (preview)",
    "Should preview addition or explain why it can't"
)

tester.test_tool(
    "spelunk-fix-pattern",
    {
        "findPattern": "Console.WriteLine",
        "replacePattern": "logger.Log",
        "patternType": "text",
        "preview": True
    },
    "Fix pattern (preview mode)",
    "Should show preview of changes or indicate no matches"
)

# Test 8: Statement Modification Tools
print("\n" + "="*80)
print("CATEGORY 8: STATEMENT MODIFICATION TOOLS")
print("="*80)

tester.test_tool(
    "spelunk-replace-statement",
    {
        "filePath": test_file,
        "line": 9999,
        "column": 1,
        "newStatement": "Console.WriteLine(\"test\");"
    },
    "Replace statement at invalid line",
    "Should provide clear error about invalid location"
)

tester.test_tool(
    "spelunk-insert-statement",
    {
        "filePath": test_file,
        "line": 10,
        "column": 1,
        "position": "before",
        "statement": "// Test comment"
    },
    "Insert statement at line 10",
    "Should insert or explain requirements"
)

tester.test_tool(
    "spelunk-remove-statement",
    {
        "filePath": test_file,
        "line": 9999,
        "column": 1
    },
    "Remove statement at invalid line",
    "Should provide clear error about invalid location"
)

# Print final summary
tester.print_summary()

# Generate recommendations
print("\n" + "="*80)
print("RECOMMENDATIONS FOR IMPROVEMENT")
print("="*80)

recommendations = []

# Analyze results for patterns
error_patterns = {}
for result in tester.results:
    if result["status"] == "error" and result["outcome"]:
        error_msg = str(result["outcome"])
        if "No workspace" in error_msg:
            error_patterns["workspace"] = error_patterns.get("workspace", 0) + 1
        elif "not found" in error_msg:
            error_patterns["not_found"] = error_patterns.get("not_found", 0) + 1
        elif "Invalid" in error_msg:
            error_patterns["invalid"] = error_patterns.get("invalid", 0) + 1

if error_patterns.get("workspace", 0) > 2:
    recommendations.append("Multiple tools require workspace - consider auto-loading or better error messages")
if error_patterns.get("not_found", 0) > 3:
    recommendations.append("Many 'not found' errors - improve discovery helpers or suggestions")
if error_patterns.get("invalid", 0) > 2:
    recommendations.append("Parameter validation issues - provide better examples or validation")

# Check for empty results
empty_count = sum(1 for r in tester.results if r["status"] == "empty")
if empty_count > 5:
    recommendations.append("Many empty results - consider providing suggestions or 'did you mean' functionality")

if recommendations:
    for i, rec in enumerate(recommendations, 1):
        print(f"{i}. {rec}")
else:
    print("✅ All tools appear to be functioning well with clear error messages")

# Close tester
tester.close()