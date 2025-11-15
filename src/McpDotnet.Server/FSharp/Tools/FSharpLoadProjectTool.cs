using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace McpDotnet.Server.FSharp.Tools;

/// <summary>
/// MCP tool for loading F# projects.
/// </summary>
public class FSharpLoadProjectTool
{
    private readonly FSharpWorkspaceManager _workspaceManager;
    private readonly ILogger<FSharpLoadProjectTool> _logger;

    public FSharpLoadProjectTool(FSharpWorkspaceManager workspaceManager, ILogger<FSharpLoadProjectTool> logger)
    {
        _workspaceManager = workspaceManager;
        _logger = logger;
    }


    public async Task<object> ExecuteAsync(string projectPath)
    {
        try
        {
            _logger.LogInformation("Loading F# project: {ProjectPath}", projectPath);

            var (success, message, projectInfo) = await _workspaceManager.LoadProjectAsync(projectPath);

            if (!success || projectInfo == null)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Failed to load F# project: {message}"
                        }
                    },
                    isError = true
                };
            }

            var projectSummary = $@"# F# Project Loaded: {projectInfo.ProjectName}

**Project Path:** {projectInfo.ProjectPath}
**Target Framework:** {projectInfo.TargetFramework}
**Source Files:** {projectInfo.SourceFiles.Count}
**References:** {projectInfo.References.Count}

## Source Files:
{string.Join("\n", projectInfo.SourceFiles.Select(f => $"- {f}"))}

## Key References:
{string.Join("\n", projectInfo.References.Take(10).Select(r => $"- {System.IO.Path.GetFileName(r)}"))}
{(projectInfo.References.Count > 10 ? $"\n... and {projectInfo.References.Count - 10} more references" : "")}

F# project loaded successfully. You can now use F# analysis tools on files in this project.";

            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = projectSummary
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading F# project");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error loading F# project: {ex.Message}"
                    }
                },
                isError = true
            };
        }
    }
}