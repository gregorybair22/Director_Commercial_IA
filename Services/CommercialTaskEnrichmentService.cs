using System.Text;
using System.Text;
using System.Text.Json;
using DirectorComercialIA.Models;

namespace DirectorComercialIA.Services;

public class CommercialTaskEnrichmentService
{
    private const decimal MinReasonableEstimatedValue = 150m;
    private const decimal MaxReasonableEstimatedValue = 100_000_000m;

    private readonly LocalStatusInferenceService _statusInference;
    private readonly CountryLanguageInferenceService _countryLanguageInference;
    private readonly ClientTypeInferenceService _clientTypeInference;
    private readonly PriorityInferenceService _priorityInference;
    private readonly ContactInferenceService _contactInference;

    public CommercialTaskEnrichmentService(
        LocalStatusInferenceService statusInference,
        CountryLanguageInferenceService countryLanguageInference,
        ClientTypeInferenceService clientTypeInference,
        PriorityInferenceService priorityInference,
        ContactInferenceService contactInference)
    {
        _statusInference = statusInference;
        _countryLanguageInference = countryLanguageInference;
        _clientTypeInference = clientTypeInference;
        _priorityInference = priorityInference;
        _contactInference = contactInference;
    }

    public void EnrichFromAsana(CommercialTask task)
    {
        var notes = new List<string>();

        ApplyDirectCustomFieldValues(task, notes);
        ApplyLocalStatus(task, notes);
        ApplyNextAction(task, notes);
        ApplyCountryLanguage(task, notes);
        ApplyClientType(task, notes);
        ApplyClientContacts(task, notes);
        ApplyCommercialOwner(task, notes);
        ApplyPriority(task, notes);

        if (!task.IsEstimatedValueManual && (task.EstimatedValue ?? 0) == 0 && task.CustomFieldsJson is { Length: > 0 })
        {
            var inferred = TryInferEstimatedValue(task.CustomFieldsJson);
            if (inferred.HasValue)
            {
                task.EstimatedValue = inferred.Value;
                notes.Add($"EstimatedValue inferido de custom_fields: {inferred.Value:N2}");
            }
        }

        if (!task.IsEstimatedValueManual && task.EstimatedValue.HasValue && !IsReasonableEstimatedValue(task.EstimatedValue.Value))
        {
            task.EstimatedValue = 0m;
            notes.Add("EstimatedValue descartado por no razonable. Se deja a 0.");
        }

        task.LastAutoEnrichedAt = DateTime.UtcNow;
        if (notes.Count > 0)
        {
            AppendAutoNotes(task, notes);
        }
    }

    private void ApplyLocalStatus(CommercialTask task, List<string> notes)
    {
        var inferred = _statusInference.Infer(task);

        if (!task.IsLocalStatusManual && !string.IsNullOrWhiteSpace(inferred.Status) && task.LocalStatus != inferred.Status)
        {
            task.LocalStatus = inferred.Status;
            notes.Add($"LocalStatus => {task.LocalStatus}");
        }

        if (!task.IsProbabilityManual && inferred.Probability.HasValue)
        {
            task.Probability = inferred.Probability;
            notes.Add($"Probability => {task.Probability}");
        }

        if (inferred.ShouldCloseLocally)
        {
            task.IsCompleted = true;
            task.CompletedAt ??= DateTime.UtcNow;
            notes.Add("Marcada como cerrada localmente por sección");
        }
    }

    private static void ApplyNextAction(CommercialTask task, List<string> notes)
    {
        if (task.IsNextActionManual)
        {
            return;
        }

        var section = Normalize(task.SectionName);
        string? action = null;

        if (section.Contains("LLAMAR")) action = "Llamar";
        else if (section.Contains("EMAIL")) action = "Enviar email";
        else if (section.Contains("VISITA")) action = "Agendar o realizar visita";
        else if (section.Contains("PTE PUBLICACION") || section.Contains("PTE PUBLICACIÓN")) action = "Revisar publicación del concurso";
        else if (section.Contains("PTE ACEPTACION") || section.Contains("PTE. ACEPTACION")) action = "Confirmar aceptación / cierre";
        else if (section.Contains("CLIENTE COMPLEMENTAR")) action = "Completar información del cliente";

        if (!string.IsNullOrWhiteSpace(action))
        {
            task.NextAction = action;
            notes.Add($"NextAction => {action}");
        }

        if (task.DueOn.HasValue && !task.NextActionDate.HasValue)
        {
            task.NextActionDate = task.DueOn;
            notes.Add($"NextActionDate => {task.NextActionDate:dd/MM/yyyy}");
        }
    }

    private void ApplyCountryLanguage(CommercialTask task, List<string> notes)
    {
        var (country, language) = _countryLanguageInference.Infer(task);

        if (!task.IsCountryManual && !string.IsNullOrWhiteSpace(country) && task.Country != country)
        {
            task.Country = country;
            notes.Add($"Country => {country}");
        }

        if (!task.IsLanguageManual && !string.IsNullOrWhiteSpace(language) && task.Language != language)
        {
            task.Language = language;
            notes.Add($"Language => {language}");
        }
    }

    private void ApplyClientType(CommercialTask task, List<string> notes)
    {
        if (task.IsClientTypeManual)
        {
            return;
        }

        var inferred = _clientTypeInference.Infer(task);
        if (task.ClientType != inferred)
        {
            task.ClientType = inferred;
            notes.Add($"ClientType => {inferred}");
        }
    }

    private static void ApplyCommercialOwner(CommercialTask task, List<string> notes)
    {
        if (task.IsCommercialOwnerManual)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(task.AssigneeName) && task.CommercialOwner != task.AssigneeName)
        {
            task.CommercialOwner = task.AssigneeName;
            notes.Add($"CommercialOwner => {task.CommercialOwner}");
        }
    }

    private void ApplyPriority(CommercialTask task, List<string> notes)
    {
        if (task.IsPriorityManual)
        {
            return;
        }

        var inferred = _priorityInference.Infer(task);
        if (task.Priority != inferred)
        {
            task.Priority = inferred;
            notes.Add($"Priority => {inferred}");
        }
    }

    private static decimal? TryInferEstimatedValue(string customFieldsJson)
    {
        // Seguridad: solo inferir valor si hay señales claras de campo monetario.
        var map = ParseCustomFields(customFieldsJson);
        if (map.Count == 0)
            return null;

        var moneyFieldHints = new[]
        {
            "valor", "importe", "amount", "budget", "precio", "price", "cost", "revenue", "euros", "eur", "usd"
        };

        foreach (var kv in map)
        {
            var normalizedKey = Normalize(kv.Key);
            var normalizedValue = Normalize(kv.Value);

            var keyLooksMonetary = moneyFieldHints.Any(h => normalizedKey.Contains(Normalize(h)));
            var valueLooksMonetary = normalizedValue.Contains("€") || normalizedValue.Contains("$") ||
                                     normalizedValue.Contains(" EUR") || normalizedValue.Contains(" USD") ||
                                     normalizedValue.Contains(" EURO");

            if (!keyLooksMonetary && !valueLooksMonetary)
                continue;

            var cleaned = new string(kv.Value.Where(c => char.IsDigit(c) || c == ',' || c == '.').ToArray());
            if (string.IsNullOrWhiteSpace(cleaned))
                continue;

            // Normalización simple de decimal ES/EN
            cleaned = cleaned.Replace(" ", string.Empty);
            if (cleaned.Count(c => c == ',') > 0 && cleaned.Count(c => c == '.') > 0)
            {
                cleaned = cleaned.Replace(".", string.Empty).Replace(',', '.');
            }
            else if (cleaned.Count(c => c == ',') == 1 && cleaned.Count(c => c == '.') == 0)
            {
                cleaned = cleaned.Replace(',', '.');
            }

            if (!decimal.TryParse(cleaned, out var parsed))
                continue;

            // Evitar capturar probabilidad/contadores pequeños como valor estimado.
            if (parsed < 150)
                continue;

            return parsed;
        }

        return null;
    }

    private static bool IsReasonableEstimatedValue(decimal value)
    {
        return value >= MinReasonableEstimatedValue && value <= MaxReasonableEstimatedValue;
    }

    private static void ApplyDirectCustomFieldValues(CommercialTask task, List<string> notes)
    {
        if (string.IsNullOrWhiteSpace(task.CustomFieldsJson))
            return;

        var map = ParseCustomFields(task.CustomFieldsJson);
        if (map.Count == 0)
            return;

        if (!task.IsCountryManual && string.IsNullOrWhiteSpace(task.Country) && TryGetValue(map, new[] { "country", "pais", "país" }, out var country))
        {
            task.Country = country;
            notes.Add($"Country(custom_fields) => {country}");
        }

        if (!task.IsLanguageManual && string.IsNullOrWhiteSpace(task.Language) && TryGetValue(map, new[] { "language", "idioma" }, out var language))
        {
            task.Language = language;
            notes.Add($"Language(custom_fields) => {language}");
        }

        if (!task.IsClientTypeManual && (string.IsNullOrWhiteSpace(task.ClientType) || task.ClientType == "Otro") && TryGetValue(map, new[] { "client type", "tipo cliente" }, out var clientType))
        {
            task.ClientType = clientType;
            notes.Add($"ClientType(custom_fields) => {clientType}");
        }

        if (!task.IsCommercialOwnerManual && string.IsNullOrWhiteSpace(task.CommercialOwner) && TryGetValue(map, new[] { "commercial owner", "responsable comercial" }, out var owner))
        {
            task.CommercialOwner = owner;
            notes.Add($"CommercialOwner(custom_fields) => {owner}");
        }

        if (!task.IsNextActionManual && string.IsNullOrWhiteSpace(task.NextAction) && TryGetValue(map, new[] { "next action", "proxima accion", "próxima acción" }, out var nextAction))
        {
            task.NextAction = nextAction;
            notes.Add($"NextAction(custom_fields) => {nextAction}");
        }

        if (!task.IsProbabilityManual && !task.Probability.HasValue && TryGetValue(map, new[] { "probability", "probabilidad" }, out var probText) && int.TryParse(new string(probText.Where(char.IsDigit).ToArray()), out var prob))
        {
            task.Probability = Math.Clamp(prob, 0, 100);
            notes.Add($"Probability(custom_fields) => {task.Probability}");
        }

        if (!task.IsEstimatedValueManual && (!task.EstimatedValue.HasValue || task.EstimatedValue <= 0) && TryGetValue(map, new[] { "estimated value", "valor estimado", "importe" }, out var valueText))
        {
            var cleaned = new string(valueText.Where(c => char.IsDigit(c) || c == ',' || c == '.').ToArray()).Replace(',', '.');
            if (decimal.TryParse(cleaned, out var amount) && amount > 0)
            {
                if (IsReasonableEstimatedValue(amount))
                {
                    task.EstimatedValue = amount;
                    notes.Add($"EstimatedValue(custom_fields) => {amount:N2}");
                }
                else
                {
                    task.EstimatedValue = 0m;
                    notes.Add($"EstimatedValue(custom_fields) descartado por no razonable: {amount:N2}. Se deja a 0.");
                }
            }
        }
    }

    private static Dictionary<string, string> ParseCustomFields(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("name", out var nameEl))
                    continue;

                var name = nameEl.GetString();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                string? value = null;
                if (item.TryGetProperty("display_value", out var display) && display.ValueKind != JsonValueKind.Null)
                    value = display.GetString();
                else if (item.TryGetProperty("text_value", out var text) && text.ValueKind != JsonValueKind.Null)
                    value = text.GetString();
                else if (item.TryGetProperty("number_value", out var number) && number.ValueKind == JsonValueKind.Number)
                    value = number.GetDecimal().ToString();
                else if (item.TryGetProperty("enum_value", out var enumValue) && enumValue.ValueKind != JsonValueKind.Null && enumValue.TryGetProperty("name", out var enumName))
                    value = enumName.GetString();

                if (!string.IsNullOrWhiteSpace(value))
                    map[name] = value;
            }

            return map;
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool TryGetValue(Dictionary<string, string> map, IEnumerable<string> aliases, out string value)
    {
        foreach (var alias in aliases)
        {
            var normalizedAlias = Normalize(alias);
            var found = map.FirstOrDefault(kv => Normalize(kv.Key).Contains(normalizedAlias));
            if (!string.IsNullOrWhiteSpace(found.Key))
            {
                value = found.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private void ApplyClientContacts(CommercialTask task, List<string> notes)
    {
        var customMap = ParseCustomFields(task.CustomFieldsJson ?? string.Empty);
        var inferred = _contactInference.Infer(task, customMap);

        if (!task.IsClientNameManual && string.IsNullOrWhiteSpace(task.ClientName) && !string.IsNullOrWhiteSpace(inferred.ClientName))
        {
            task.ClientName = inferred.ClientName;
            notes.Add($"ClientName => {inferred.ClientName}");
        }

        if (!task.IsContactNameManual && string.IsNullOrWhiteSpace(task.ContactName) && !string.IsNullOrWhiteSpace(inferred.ContactName))
        {
            task.ContactName = inferred.ContactName;
            notes.Add($"ContactName => {inferred.ContactName}");
        }

        if (!task.IsContactPhoneManual && string.IsNullOrWhiteSpace(task.ContactPhone) && !string.IsNullOrWhiteSpace(inferred.ContactPhone))
        {
            task.ContactPhone = inferred.ContactPhone;
            notes.Add($"ContactPhone => {inferred.ContactPhone}");
        }

        if (!task.IsContactMobileManual && string.IsNullOrWhiteSpace(task.ContactMobile) && !string.IsNullOrWhiteSpace(inferred.ContactMobile))
        {
            task.ContactMobile = inferred.ContactMobile;
            notes.Add($"ContactMobile => {inferred.ContactMobile}");
        }

        if (!task.IsContactRoleManual && string.IsNullOrWhiteSpace(task.ContactRole) && !string.IsNullOrWhiteSpace(inferred.ContactRole))
        {
            task.ContactRole = inferred.ContactRole;
            notes.Add($"ContactRole => {inferred.ContactRole}");
        }

        if (!task.IsContactEmailManual && string.IsNullOrWhiteSpace(task.ContactEmail) && !string.IsNullOrWhiteSpace(inferred.ContactEmail))
        {
            task.ContactEmail = inferred.ContactEmail;
            notes.Add($"ContactEmail => {inferred.ContactEmail}");
        }

        var contactsAreManual = task.IsContactNameManual || task.IsContactPhoneManual || task.IsContactMobileManual || task.IsContactRoleManual || task.IsContactEmailManual;
        if (!contactsAreManual && !string.IsNullOrWhiteSpace(inferred.ContactsJson))
        {
            var merged = MergeContacts(task.ContactsJson, inferred.ContactsJson);
            if (!string.Equals(task.ContactsJson, merged, StringComparison.Ordinal))
            {
                task.ContactsJson = merged;
                notes.Add("ContactsJson => actualizado con todos los contactos detectados");
            }
        }
    }

    private static string MergeContacts(string? existingJson, string inferredJson)
    {
        var existing = ParseContactRecords(existingJson);
        var inferred = ParseContactRecords(inferredJson);

        foreach (var inf in inferred)
        {
            var match = existing.FirstOrDefault(e => IsSameContact(e, inf));
            if (match is null)
            {
                existing.Add(inf);
            }
            else
            {
                match.Name ??= inf.Name;
                match.Role ??= inf.Role;
                match.Email ??= inf.Email;
                match.Phone ??= inf.Phone;
                match.Mobile ??= inf.Mobile;
            }
        }

        return JsonSerializer.Serialize(existing, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private static List<ContactRecord> ParseContactRecords(string? json)
    {
        var result = new List<ContactRecord>();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var rec = new ContactRecord
                {
                    Name = ReadProperty(item, "name", "Name"),
                    Role = ReadProperty(item, "role", "Role", "cargo"),
                    Email = ReadProperty(item, "email", "Email", "correo"),
                    Phone = ReadProperty(item, "phone", "Phone", "telefono", "teléfono"),
                    Mobile = ReadProperty(item, "mobile", "Mobile", "movil", "móvil")
                };

                if (!string.IsNullOrWhiteSpace(rec.Name) || !string.IsNullOrWhiteSpace(rec.Email) || !string.IsNullOrWhiteSpace(rec.Phone) || !string.IsNullOrWhiteSpace(rec.Mobile) || !string.IsNullOrWhiteSpace(rec.Role))
                    result.Add(rec);
            }
        }
        catch
        {
            // ignore malformed json
        }

        return result;
    }

    private static string? ReadProperty(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (item.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null)
                return value.GetString();
        }

        return null;
    }

    private static bool IsSameContact(ContactRecord a, ContactRecord b)
    {
        var aEmail = (a.Email ?? string.Empty).Trim().ToLowerInvariant();
        var bEmail = (b.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(aEmail) && aEmail == bEmail)
            return true;

        var aPhone = NormalizeContactPhone(a.Phone ?? a.Mobile);
        var bPhone = NormalizeContactPhone(b.Phone ?? b.Mobile);
        if (!string.IsNullOrWhiteSpace(aPhone) && aPhone == bPhone)
            return true;

        var aName = (a.Name ?? string.Empty).Trim().ToUpperInvariant();
        var bName = (b.Name ?? string.Empty).Trim().ToUpperInvariant();
        return !string.IsNullOrWhiteSpace(aName) && aName == bName;
    }

    private static string NormalizeContactPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private sealed class ContactRecord
    {
        public string? Name { get; set; }
        public string? Role { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Mobile { get; set; }
    }

    private static void AppendAutoNotes(CommercialTask task, List<string> notes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] Auto-enrichment:");
        foreach (var note in notes)
        {
            sb.AppendLine($"- {note}");
        }

        task.AutoEnrichmentNotes = string.IsNullOrWhiteSpace(task.AutoEnrichmentNotes)
            ? sb.ToString().TrimEnd()
            : task.AutoEnrichmentNotes + Environment.NewLine + sb.ToString().TrimEnd();

        // No borrar notas del usuario; solo añadir traza automática
        if (!string.IsNullOrWhiteSpace(task.InternalNotes))
        {
            task.InternalNotes += Environment.NewLine + "[AUTO] Campos comerciales recalculados automáticamente.";
        }
        else
        {
            task.InternalNotes = "[AUTO] Campos comerciales recalculados automáticamente.";
        }
    }

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
}
