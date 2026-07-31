using System.Text.Json;

namespace TargetApiSimulator;

/// <summary>
/// Stateless check that a string is a well-formed JSON document.
/// The options are spelled out rather than defaulted so that the accept/reject contract is
/// visible in one place and cannot drift when the .NET version is bumped.
/// </summary>
public static class JsonValidator
{
    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        MaxDepth = 64
    };

    public static bool IsValidJson(string? jsonString)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonString, Options);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
