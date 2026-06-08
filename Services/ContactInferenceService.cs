using System.Text.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using DirectorComercialIA.Models;

namespace DirectorComercialIA.Services;

public class ContactInferenceService
{
    private static readonly Regex EmailRegex = new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"\+?\d[\d\s().-]{7,}\d", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"\d+", RegexOptions.Compiled);

    public ContactInferenceResult Infer(CommercialTask task, Dictionary<string, string> customFields)
    {
        var contactsByIndex = new Dictionary<int, ContactCandidate>();
        string? inferredClientName = null;

        foreach (var kv in customFields)
        {
            var key = Normalize(kv.Key);
            var value = kv.Value?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var idxMatch = NumberRegex.Match(key);
            var index = idxMatch.Success && int.TryParse(idxMatch.Value, out var parsedIdx) ? parsedIdx : 0;

            if (!contactsByIndex.TryGetValue(index, out var contact))
            {
                contact = new ContactCandidate();
                contactsByIndex[index] = contact;
            }

            if (ContainsAny(key, "CORREO", "EMAIL", "MAIL"))
                contact.Email ??= value;
            else if (ContainsAny(key, "MOVIL", "MÓVIL", "MOBILE", "CELULAR", "CELL"))
                contact.Mobile ??= value;
            else if (ContainsAny(key, "TELEFONO", "TELÉFONO", "PHONE", "TEL"))
                contact.Phone ??= value;
            else if (ContainsAny(key, "CARGO", "ROLE", "PUESTO", "POSITION"))
                contact.Role ??= value;
            else if (ContainsAny(key, "CONTACT", "CONTACTO", "PERSONA"))
                contact.Name ??= value;

            if (ContainsAny(key, "CLIENTE", "CUSTOMER", "COMPANY", "EMPRESA", "HOSPITAL"))
                inferredClientName ??= value;
        }

        var rawText = $"{task.Name}\n{task.Description}\n{task.CustomFieldsJson}\n{task.TagsJson}";
        var emails = EmailRegex.Matches(rawText).Select(m => m.Value).Distinct().ToList();
        var phones = PhoneRegex.Matches(rawText).Select(m => m.Value.Trim()).Distinct().ToList();

        if (contactsByIndex.Count == 0)
            contactsByIndex[0] = new ContactCandidate();

        AddStandaloneContactsFromRawText(contactsByIndex, emails, phones);

        var first = contactsByIndex.OrderBy(k => k.Key).First().Value;

        var contacts = contactsByIndex
            .OrderBy(k => k.Key)
            .Select(k => k.Value)
            .Where(c => !string.IsNullOrWhiteSpace(c.Name) || !string.IsNullOrWhiteSpace(c.Email) || !string.IsNullOrWhiteSpace(c.Phone) || !string.IsNullOrWhiteSpace(c.Mobile) || !string.IsNullOrWhiteSpace(c.Role))
            .ToList();

        return new ContactInferenceResult
        {
            ClientName = inferredClientName,
            ContactName = first.Name,
            ContactEmail = first.Email,
            ContactPhone = first.Phone,
            ContactMobile = first.Mobile,
            ContactRole = first.Role,
            ContactsJson = contacts.Count > 0
                ? JsonSerializer.Serialize(contacts, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                : null
        };
    }

    private static void AddStandaloneContactsFromRawText(Dictionary<int, ContactCandidate> contactsByIndex, List<string> emails, List<string> phones)
    {
        var nextIndex = contactsByIndex.Count == 0 ? 0 : contactsByIndex.Keys.Max() + 1;

        foreach (var email in emails)
        {
            var alreadyExists = contactsByIndex.Values.Any(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase));
            if (alreadyExists)
                continue;

            contactsByIndex[nextIndex++] = new ContactCandidate { Email = email };
        }

        foreach (var phone in phones)
        {
            var digits = NormalizeDigits(phone);
            var alreadyExists = contactsByIndex.Values.Any(c =>
                NormalizeDigits(c.Phone) == digits || NormalizeDigits(c.Mobile) == digits);
            if (alreadyExists)
                continue;

            var candidate = new ContactCandidate();
            if (IsLikelyMobile(phone))
                candidate.Mobile = phone;
            else
                candidate.Phone = phone;

            contactsByIndex[nextIndex++] = candidate;
        }
    }

    private static string NormalizeDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static bool IsLikelyMobile(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("34") && digits.Length >= 11)
        {
            var localStart = digits.Substring(2, 1);
            return localStart is "6" or "7";
        }

        if (digits.Length >= 9)
        {
            var start = digits[0];
            return start is '6' or '7';
        }

        return false;
    }

    private static bool ContainsAny(string text, params string[] terms) => terms.Any(t => Normalize(text).Contains(Normalize(t)));

    private static string Normalize(string? value)
    {
        value ??= string.Empty;
        return value
            .ToUpperInvariant()
            .Replace("Á", "A")
            .Replace("É", "E")
            .Replace("Í", "I")
            .Replace("Ó", "O")
            .Replace("Ú", "U");
    }

    private sealed class ContactCandidate
    {
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Mobile { get; set; }
        public string? Role { get; set; }
        public string? Email { get; set; }
    }
}

public sealed class ContactInferenceResult
{
    public string? ClientName { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactMobile { get; set; }
    public string? ContactRole { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactsJson { get; set; }
}
