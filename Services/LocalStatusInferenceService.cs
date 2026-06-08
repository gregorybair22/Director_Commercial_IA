using DirectorComercialIA.Models;

namespace DirectorComercialIA.Services;

public class LocalStatusInferenceService
{
    public LocalStatusInferenceResult Infer(CommercialTask task)
    {
        var section = Normalize(task.SectionName);
        var text = Normalize($"{task.Name} {task.Description} {task.CustomFieldsJson} {task.TagsJson}");

        var result = new LocalStatusInferenceResult();

        if (section.Contains("GANADO"))
        {
            result.Status = "Ganado";
            result.Probability = 100;
            result.ShouldCloseLocally = true;
            return result;
        }

        if (section.Contains("PERDIDO"))
        {
            result.Status = "Perdido";
            result.Probability = 0;
            result.ShouldCloseLocally = true;
            return result;
        }

        if (section.Contains("PTE ACEPTACION") || section.Contains("PTE. ACEPTACION"))
        {
            result.Status = "Pendiente aceptación";
            result.Probability = 80;
            return result;
        }

        if (section.Contains("NEGOCIACION"))
        {
            result.Status = "Negociación";
            result.Probability = 60;
            return result;
        }

        if (section.Contains("OFERTA") || text.Contains("OFERTA ENVIADA") || text.Contains("PRESUPUESTO ENVIADO") || text.Contains("PROPOSAL SENT") || text.Contains("QUOTATION SENT"))
        {
            result.Status = "Oferta enviada";
            result.Probability = 50;
            return result;
        }

        if (section.Contains("SEGUIMIENTO") || section.Contains("FOLLOW"))
        {
            result.Status = "Seguimiento";
            result.Probability = 35;
            return result;
        }

        if (section.Contains("VISITA") || text.Contains("DEMO") || text.Contains("VISITA") || text.Contains("VISIT") || text.Contains("MEETING"))
        {
            result.Status = "Contactado";
            result.Probability = 25;
            return result;
        }

        if (section.Contains("PRESENTAR") || section.Contains("EMAIL PRESENTAR") || section.Contains("LLAMAR PRESENTAR"))
        {
            result.Status = "Nuevo";
            result.Probability = 10;
            return result;
        }

        if (section.Contains("SIN DINERO, QUIERE"))
        {
            result.Status = "Dormido";
            result.RecommendedPriority = "Media";
            return result;
        }

        if (section.Contains("SIN DINERO, NO QUIERE"))
        {
            result.Status = "Perdido";
            result.Probability = 0;
            result.ShouldCloseLocally = true;
            return result;
        }

        if (string.IsNullOrWhiteSpace(task.LocalStatus))
        {
            result.Status = "Nuevo";
        }

        return result;
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

public sealed class LocalStatusInferenceResult
{
    public string? Status { get; set; }
    public int? Probability { get; set; }
    public string? RecommendedPriority { get; set; }
    public bool ShouldCloseLocally { get; set; }
}
