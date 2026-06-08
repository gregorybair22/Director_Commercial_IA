using System.Text.RegularExpressions;
using Google.Apis.Gmail.v1.Data;

namespace DirectorComercialIA.Mail.Helpers;

public static class GmailMessageHelper
{
    private static readonly Regex EmailInAngleBrackets = new("<([^>]+@[^>]+)>", RegexOptions.Compiled);

    public static string? GetHeader(Message message, string name)
    {
        return message.Payload?.Headers?.FirstOrDefault(h =>
            h.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    public static string? ParseEmailAddress(string? fromHeader)
    {
        if (string.IsNullOrWhiteSpace(fromHeader)) return null;
        var m = EmailInAngleBrackets.Match(fromHeader);
        if (m.Success) return m.Groups[1].Value.Trim().ToLowerInvariant();
        if (fromHeader.Contains('@')) return fromHeader.Trim().ToLowerInvariant();
        return null;
    }
}
