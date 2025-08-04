#!/usr/bin/env python3
"""
Simple test to check F# detection response
"""

import sys
import os
import json

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))
from utils.simple_client import SimpleClient

client = SimpleClient(allowed_paths=["test-workspace"])

# Load workspace
client.call_tool("dotnet-load-workspace", {
    "path": os.path.abspath("test-workspace/TestProject.csproj")
})

# Test F# file
result = client.call_tool("dotnet-query-syntax", {
    "roslynPath": "//method",
    "file": os.path.abspath("test-workspace/FSharpDetectionTest.fs")
})

print(f"Success: {result['success']}")
print(f"Result: {json.dumps(result, indent=2)}")

client.close()