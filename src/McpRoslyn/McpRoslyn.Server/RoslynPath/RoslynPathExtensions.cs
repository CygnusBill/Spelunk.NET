using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace McpRoslyn.Server.RoslynPath
{
    public static class RoslynPathExtensions
    {
        /// <summary>
        /// Extension method to add RoslynPath support to RoslynWorkspaceManager
        /// </summary>
        public static async Task<FindStatementsResult> FindStatementsWithPathAsync(
            this RoslynWorkspaceManager workspaceManager,
            string roslynPath,
            string workspacePath = null)
        {
            var result = new FindStatementsResult { Success = true };
            var statements = new List<StatementInfo>();
            var statementCounter = new StatementIdCounter();

            try
            {
                // Get workspaces to search
                var workspaces = await workspaceManager.GetWorkspacesToSearch(workspacePath);
                
                foreach (var workspace in workspaces)
                {
                    var projects = workspace.CurrentSolution.Projects;
                    
                    foreach (var project in projects)
                    {
                        foreach (var document in project.Documents)
                        {
                            if (!document.SupportsSyntaxTree) continue;
                            
                            var syntaxTree = await document.GetSyntaxTreeAsync();
                            if (syntaxTree == null) continue;
                            
                            var semanticModel = await document.GetSemanticModelAsync();
                            
                            // Use RoslynPath to find nodes
                            var nodes = RoslynPath.Find(syntaxTree, roslynPath, semanticModel);
                            
                            foreach (var node in nodes)
                            {
                                // Only process statement nodes
                                if (node is StatementSyntax statement)
                                {
                                    var info = CreateStatementInfo(statement, document.FilePath, statementCounter);
                                    statements.Add(info);
                                }
                            }
                        }
                    }
                }
                
                result.Statements = statements;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"RoslynPath error: {ex.Message}";
                return result;
            }
        }

        private static StatementInfo CreateStatementInfo(
            StatementSyntax statement, 
            string filePath,
            StatementIdCounter counter)
        {
            var location = statement.GetLocation();
            var lineSpan = location.GetLineSpan();
            
            // Get containing method and class
            var containingMethod = statement.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            var containingClass = statement.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            
            // Generate stable path
            var stablePath = RoslynPath.GetNodePath(statement);
            
            return new StatementInfo
            {
                StatementId = $"stmt-{counter.GetNext()}",
                Type = statement.GetType().Name.Replace("Syntax", ""),
                Text = statement.ToString(),
                Location = new Location
                {
                    File = filePath,
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1
                },
                ContainingMethod = containingMethod?.Identifier.Text ?? "<none>",
                ContainingClass = containingClass?.Identifier.Text ?? "<none>",
                SemanticTags = new List<string>(),
                StablePath = stablePath
            };
        }
    }

    public class FindStatementsResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public List<StatementInfo> Statements { get; set; } = new List<StatementInfo>();
    }

    public class StatementInfo
    {
        public string StatementId { get; set; }
        public string Type { get; set; }
        public string Text { get; set; }
        public Location Location { get; set; }
        public string ContainingMethod { get; set; }
        public string ContainingClass { get; set; }
        public List<string> SemanticTags { get; set; }
        public string StablePath { get; set; }
    }

    public class Location
    {
        public string File { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }

    public class StatementIdCounter
    {
        private int _counter = 0;
        public int GetNext() => ++_counter;
    }
}