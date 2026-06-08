using DirectorComercialIA.Models;

namespace DirectorComercialIA.Services;

public class CountryLanguageInferenceService
{
    private static readonly List<CountryRule> Rules =
    [
        new("España", "Español", ["España","Spain","Madrid","Barcelona","Valencia","Sevilla","Malaga","Málaga","Cadiz","Cádiz","Zaragoza","Navarra","Canarias","Valladolid","Tarragona","Alicante","Elche","Badalona","Badajoz","Melilla","Leon","León","Burgos","Castellon","Castellón","Calahorra","Fuerteventura","Jerez","Aranjuez"]),
        new("Francia", "Francés", ["France","Francia","Paris","París","Lyon","Marseille","Toulouse","Bordeaux","Lille","Nice","Nantes","Montpellier"]),
        new("Canadá", "Inglés", ["Canada","Canadá","Toronto","Montreal","Montréal","Quebec","Vancouver","Ontario","Providence Health"]),
        new("Chile", "Español", ["Chile","Santiago"]),
        new("México", "Español", ["Mexico","México","CDMX","Monterrey","Guadalajara"]),
        new("Portugal", "Portugués", ["Portugal","Lisboa","Lisbon","Porto","Oporto"]),
        new("Israel", "Inglés", ["Israel","Tel Aviv","Jerusalem","Jerusalén"]),
        new("Polonia", "Inglés", ["Poland","Polonia","Warsaw","Varsovia"]),
        new("Rumanía", "Inglés", ["Romania","Rumanía","Bucarest","Bucharest"]),
        new("Reino Unido", "Inglés", ["UK","United Kingdom","Reino Unido","London","Londres"]),
        new("Estados Unidos", "Inglés", ["USA","United States","Estados Unidos","Miami","New York","California","Texas"]),
        new("Alemania", "Alemán", ["Germany","Alemania","Berlin","Berlín","Munich","München"]),
        new("Italia", "Italiano", ["Italy","Italia","Rome","Roma","Milan","Milano"])
    ];

    public (string? Country, string? Language) Infer(CommercialTask task)
    {
        var source = $"{task.Name} {task.Description} {task.SectionName} {task.CustomFieldsJson} {task.TagsJson}";
        var text = Normalize(source);

        foreach (var rule in Rules)
        {
            if (rule.Keywords.Any(k => text.Contains(Normalize(k))))
            {
                var language = rule.DefaultLanguage;
                if (rule.Country == "Canadá" && (text.Contains("QUEBEC") || text.Contains("MONTREAL") || text.Contains("MONTRÉAL")))
                {
                    language = "Francés";
                }

                return (rule.Country, language);
            }
        }

        return (null, null);
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

    private sealed record CountryRule(string Country, string DefaultLanguage, IReadOnlyList<string> Keywords);
}
