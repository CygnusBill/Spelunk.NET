using System;
using System.Collections.Generic;
using System.Linq;

namespace McpRoslyn.Server.FSharp
{
    /// <summary>
    /// Information about F# projects that were skipped during workspace loading
    /// </summary>
    public class FSharpProjectInfo
    {
        public string ProjectPath { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string WorkspaceId { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
        public bool IsLoaded { get; set; }
        public string? LoadError { get; set; }
        public List<string> SourceFiles { get; set; } = new();
        public List<string> References { get; set; } = new();
    }
    
    /// <summary>
    /// Tracks F# projects across all workspaces
    /// </summary>
    public class FSharpProjectTracker
    {
        private readonly Dictionary<string, FSharpProjectInfo> _fsharpProjects = new();
        private readonly object _lock = new();
        
        public void AddSkippedProject(string projectPath, string projectName, string workspaceId)
        {
            lock (_lock)
            {
                _fsharpProjects[projectPath] = new FSharpProjectInfo
                {
                    ProjectPath = projectPath,
                    ProjectName = projectName,
                    WorkspaceId = workspaceId,
                    DetectedAt = DateTime.UtcNow,
                    IsLoaded = false
                };
            }
        }
        
        public void MarkProjectLoaded(string projectPath)
        {
            lock (_lock)
            {
                if (_fsharpProjects.TryGetValue(projectPath, out var info))
                {
                    info.IsLoaded = true;
                }
            }
        }
        
        public void SetProjectError(string projectPath, string error)
        {
            lock (_lock)
            {
                if (_fsharpProjects.TryGetValue(projectPath, out var info))
                {
                    info.LoadError = error;
                }
            }
        }
        
        public IReadOnlyList<FSharpProjectInfo> GetSkippedProjects(string? workspaceId = null)
        {
            lock (_lock)
            {
                var projects = _fsharpProjects.Values.Where(p => !p.IsLoaded);
                
                if (!string.IsNullOrEmpty(workspaceId))
                {
                    projects = projects.Where(p => p.WorkspaceId == workspaceId);
                }
                
                return projects.ToList();
            }
        }
        
        public IReadOnlyList<FSharpProjectInfo> GetAllProjects(string? workspaceId = null)
        {
            lock (_lock)
            {
                var projects = _fsharpProjects.Values.AsEnumerable();
                
                if (!string.IsNullOrEmpty(workspaceId))
                {
                    projects = projects.Where(p => p.WorkspaceId == workspaceId);
                }
                
                return projects.ToList();
            }
        }
        
        public FSharpProjectInfo? GetProject(string projectPath)
        {
            lock (_lock)
            {
                return _fsharpProjects.TryGetValue(projectPath, out var info) ? info : null;
            }
        }
        
        public void Clear(string? workspaceId = null)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(workspaceId))
                {
                    _fsharpProjects.Clear();
                }
                else
                {
                    var toRemove = _fsharpProjects
                        .Where(kvp => kvp.Value.WorkspaceId == workspaceId)
                        .Select(kvp => kvp.Key)
                        .ToList();
                    
                    foreach (var key in toRemove)
                    {
                        _fsharpProjects.Remove(key);
                    }
                }
            }
        }
    }
}