#!/bin/bash

echo "Running new test coverage for recent features..."
echo "=============================================="

# Navigate to project root
cd "$(dirname "$0")"

# Test 1: Query-syntax semantic (existing test)
echo -e "\n1. Testing query-syntax semantic enrichment..."
python3 tests/tools/test-query-syntax-semantic.py
if [ $? -eq 0 ]; then
    echo "✅ Query-syntax semantic test passed"
else
    echo "❌ Query-syntax semantic test failed"
fi

# Test 2: Navigate semantic (new test)
echo -e "\n2. Testing navigate semantic enrichment..."
python3 tests/tools/test-navigate-semantic.py
if [ $? -eq 0 ]; then
    echo "✅ Navigate semantic test passed"
else
    echo "❌ Navigate semantic test failed"
fi

# Test 3: Get-AST semantic (new test)
echo -e "\n3. Testing get-ast semantic enrichment..."
python3 tests/tools/test-get-ast-semantic.py
if [ $? -eq 0 ]; then
    echo "✅ Get-AST semantic test passed"
else
    echo "❌ Get-AST semantic test failed"
fi

# Test 4: F# detection (new test)
echo -e "\n4. Testing F# file detection..."
python3 tests/tools/test-fsharp-detection.py
if [ $? -eq 0 ]; then
    echo "✅ F# detection test passed"
else
    echo "❌ F# detection test failed"
fi

echo -e "\n=============================================="
echo "Test coverage run complete!"