using Microsoft.CodeAnalysis;

namespace Spelunk.Server.LanguageHandlers;

/// <summary>
/// Factory for creating language-specific handlers
/// </summary>
public static class LanguageHandlerFactory
{
    private static readonly Dictionary<string, ILanguageHandler> _handlers = new()
    {
        [LanguageNames.CSharp] = new CSharpLanguageHandler(),
        [LanguageNames.VisualBasic] = new VisualBasicLanguageHandler()
    };
    
    /// <summary>
    /// Gets a language handler for the specified language
    /// </summary>
    public static ILanguageHandler? GetHandler(string language)
    {
        return _handlers.TryGetValue(language, out var handler) ? handler : null;
    }
    
    /// <summary>
    /// Gets a language handler for a document
    /// </summary>
    public static ILanguageHandler? GetHandler(Document document)
    {
        var language = LanguageDetector.GetLanguageFromDocument(document);
        return GetHandler(language);
    }
    
    /// <summary>
    /// Gets a language handler for a file path
    /// </summary>
    public static ILanguageHandler? GetHandlerForPath(string filePath)
    {
        var language = LanguageDetector.GetLanguageFromPath(filePath);
        return language != null ? GetHandler(language) : null;
    }
    
    /// <summary>
    /// Checks if a language is supported
    /// </summary>
    public static bool IsLanguageSupported(string language)
    {
        return _handlers.ContainsKey(language);
    }
}