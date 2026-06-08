using DirectorComercialIA.Models;

namespace DirectorComercialIA.Services;

public class PriorityInferenceService
{
    public string Infer(CommercialTask task)
    {
        var text = Normalize($"{task.Name} {task.Description} {task.CustomFieldsJson} {task.TagsJson}");

        if (task.LocalStatus == "Pendiente aceptación" || task.IsOverdue || (task.EstimatedValue ?? 0m) > 100000m ||
            ContainsAny(text, "CONCURSO", "TENDER", "LICITACION", "LICITACIÓN", "DEADLINE", "URGENTE"))
            return "Crítica";

        if (task.LocalStatus is "Negociación" or "Oferta enviada" || task.DaysWithoutActivity > 60)
            return "Alta";

        if (task.LocalStatus is "Seguimiento" or "Contactado")
            return "Media";

        if (task.LocalStatus is "Nuevo" or "Dormido")
            return "Baja";

        return "Media";
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
