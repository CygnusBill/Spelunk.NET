using Microsoft.CodeAnalysis;

namespace McpDotnet.Server;

/// <summary>
/// Utility class for detecting programming language from file extensions and documents
/// </summary>
public static class LanguageDetector
{
    /// <summary>
    /// Determines the language from a file path
    /// </summary>
    public static string? GetLanguageFromPath(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        return extension switch
        {
            ".cs" => LanguageNames.CSharp,
            ".vb" => LanguageNames.VisualBasic,
            ".fs" or ".fsx" or ".fsi" => "F#",
            _ => null
        };
    }
    
    /// <summary>
    /// Determines the language from a Roslyn document
    /// </summary>
    public static string GetLanguageFromDocument(Document document)
    {
        // First check the document's project language
        var projectLanguage = document.Project.Language;
        if (!string.IsNullOrEmpty(projectLanguage))
            return projectLanguage;
        
        // Fallback to file extension
        return GetLanguageFromPath(document.FilePath ?? "") ?? LanguageNames.CSharp;
    }
    
    /// <summary>
    /// Checks if a language is supported by Roslyn
    /// </summary>
    public static bool IsRoslynSupported(string language)
    {
        return language == LanguageNames.CSharp || language == LanguageNames.VisualBasic;
    }
    
    /// <summary>
    /// Checks if a file extension represents a project file
    /// </summary>
    public static bool IsProjectFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".csproj" or ".vbproj" or ".fsproj" => true,
            _ => false
        };
    }
    
    /// <summary>
    /// Gets the language from a project file extension
    /// </summary>
    public static string? GetLanguageFromProjectFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".csproj" => LanguageNames.CSharp,
            ".vbproj" => LanguageNames.VisualBasic,
            ".fsproj" => "F#",
            _ => null
        };
    }
}