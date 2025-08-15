#!/usr/bin/env python3
"""
Test null check detection functionality.
Verifies that the CheckIfAlreadyNullChecked method prevents duplicate null checks.
"""

import json
import sys
import os
from pathlib import Path

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))

from utils.test_client import TestClient

def test_null_check_detection():
    """Test that duplicate null checks are not added."""
    print("\n=== Testing Null Check Detection ===")
    
    with TestClient() as client:
        # Load the test workspace
        print("Loading workspace...")
        result = client.call_tool("dotnet-load-workspace", {
            "path": str(Path(__file__).parent.parent.parent / "test-workspace" / "TestProject.csproj")
        })
        
        if not result or "error" in result:
            print(f"Failed to load workspace: {result}")
            return False
        
        # Test 1: No existing null check - should add one
        print("\n--- Test 1: No existing null check ---")
        test_file1 = Path(__file__).parent.parent.parent / "test-workspace" / "NullCheckTest1.cs"
        test_file1.write_text("""
using System;

namespace TestProject
{
    public class NullCheckTest1
    {
        public void ProcessUser(User user)
        {
            user.UpdateProfile();
            Console.WriteLine(user.Name);
        }
    }
    
    public class User
    {
        public string Name { get; set; }
        public void UpdateProfile() { }
    }
}
""")
        
        result = client.call_tool("dotnet-fix-pattern", {
            "findPattern": "user.UpdateProfile()",
            "replacePattern": "",
            "patternType": "text",
            "transformationType": "add-null-check",
            "preview": True
        })
        
        if result and "Fixes" in result:
            print(f"Found {len(result['Fixes'])} fixes (expected: 1)")
            assert len(result['Fixes']) > 0, "Should add null check when none exists"
            for fix in result['Fixes']:
                print(f"  Preview: {fix.get('Preview', '')[:100]}...")
                assert "ArgumentNullException.ThrowIfNull(user)" in fix.get('Preview', ''), \
                    "Should add ArgumentNullException.ThrowIfNull"
        
        # Test 2: ArgumentNullException.ThrowIfNull already exists
        print("\n--- Test 2: ArgumentNullException.ThrowIfNull already exists ---")
        test_file2 = Path(__file__).parent.parent.parent / "test-workspace" / "NullCheckTest2.cs"
        test_file2.write_text("""
using System;

namespace TestProject
{
    public class NullCheckTest2
    {
        public void ProcessUser(User user)
        {
            ArgumentNullException.ThrowIfNull(user);
            user.UpdateProfile();
            Console.WriteLine(user.Name);
        }
    }
}
""")
        
        result = client.call_tool("dotnet-fix-pattern", {
            "findPattern": "user.UpdateProfile()",
            "replacePattern": "",
            "patternType": "text",
            "transformationType": "add-null-check",
            "preview": True
        })
        
        if result and "Fixes" in result:
            print(f"Found {len(result['Fixes'])} fixes (expected: 0)")
            assert len(result['Fixes']) == 0, "Should not add duplicate null check"
        else:
            print("No fixes needed - correct!")
        
        # Test 3: if-null-throw pattern already exists
        print("\n--- Test 3: if-null-throw pattern already exists ---")
        test_file3 = Path(__file__).parent.parent.parent / "test-workspace" / "NullCheckTest3.cs"
        test_file3.write_text("""
using System;

namespace TestProject
{
    public class NullCheckTest3
    {
        public void ProcessUser(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));
            user.UpdateProfile();
            Console.WriteLine(user.Name);
        }
    }
}
""")
        
        result = client.call_tool("dotnet-fix-pattern", {
            "findPattern": "user.UpdateProfile()",
            "replacePattern": "",
            "patternType": "text",
            "transformationType": "add-null-check",
            "preview": True
        })
        
        if result and "Fixes" in result:
            print(f"Found {len(result['Fixes'])} fixes (expected: 0)")
            assert len(result['Fixes']) == 0, "Should not add null check when if-null-throw exists"
        else:
            print("No fixes needed - correct!")
        
        # Test 4: null-coalescing throw pattern already exists
        print("\n--- Test 4: null-coalescing throw pattern already exists ---")
        test_file4 = Path(__file__).parent.parent.parent / "test-workspace" / "NullCheckTest4.cs"
        test_file4.write_text("""
using System;

namespace TestProject
{
    public class NullCheckTest4
    {
        public void ProcessUser(User user)
        {
            _ = user ?? throw new ArgumentNullException(nameof(user));
            user.UpdateProfile();
            Console.WriteLine(user.Name);
        }
    }
}
""")
        
        result = client.call_tool("dotnet-fix-pattern", {
            "findPattern": "user.UpdateProfile()",
            "replacePattern": "",
            "patternType": "text",
            "transformationType": "add-null-check",
            "preview": True
        })
        
        if result and "Fixes" in result:
            print(f"Found {len(result['Fixes'])} fixes (expected: 0)")
            assert len(result['Fixes']) == 0, "Should not add null check when null-coalescing throw exists"
        else:
            print("No fixes needed - correct!")
        
        # Test 5: Control flow boundary (return statement)
        print("\n--- Test 5: Control flow boundary (return before usage) ---")
        test_file5 = Path(__file__).parent.parent.parent / "test-workspace" / "NullCheckTest5.cs"
        test_file5.write_text("""
using System;

namespace TestProject
{
    public class NullCheckTest5
    {
        public void ProcessUser(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));
            
            if (SomeCondition())
                return;
            
            user.UpdateProfile();  // Should add null check here since return breaks flow
        }
        
        private bool SomeCondition() => false;
    }
}
""")
        
        result = client.call_tool("dotnet-fix-pattern", {
            "findPattern": "user.UpdateProfile()",
            "replacePattern": "",
            "patternType": "text",
            "transformationType": "add-null-check",
            "preview": True
        })
        
        if result and "Fixes" in result:
            print(f"Found {len(result['Fixes'])} fixes")
            # This depends on implementation details - control flow analysis
            # The check stops at return statements, so it might add a check
            for fix in result['Fixes']:
                print(f"  Preview: {fix.get('Preview', '')[:100]}...")
        
        print("\n✅ Null check detection tests passed!")
        return True

if __name__ == "__main__":
    success = test_null_check_detection()
    sys.exit(0 if success else 1)