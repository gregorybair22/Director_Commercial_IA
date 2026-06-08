using DirectorComercialIA.Models;

namespace DirectorComercialIA.Services;

public class ClientTypeInferenceService
{
    public string Infer(CommercialTask task)
    {
        var text = Normalize($"{task.Name} {task.Description} {task.CustomFieldsJson} {task.TagsJson}");

        if (ContainsAny(text, "HOSPITAL", "HOSPITALARIO", "CLINICA", "CLÍNICA", "CLINIC", "UNIVERSITY HOSPITAL", "MEDICAL CENTER", "H.U."))
            return "Hospital";

        if (ContainsAny(text, "LAVANDERIA", "LAUNDRY", "LAVAN", "ELIS", "SOLTRA"))
            return "Lavandería";

        if (ContainsAny(text, "DISTRIBUIDOR", "DISTRIBUTOR", "PARTNER", "DEALER", "RESELLER"))
            return "Distribuidor";

        if (ContainsAny(text, "TEXTIL", "TEXTILE", "FABRICANTE", "MANUFACTURER"))
            return "Fabricante textil";

        if (ContainsAny(text, "INDUSTRIA", "FACTORY", "INDUSTRIAL"))
            return "Industria";

        if (ContainsAny(text, "FARMACIA", "PHARMACY"))
            return "Farmacia";

        return "Otro";
    }

    private static bool ContainsAny(string text, params string[] words) => words.Any(w => text.Contains(Normalize(w)));

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
