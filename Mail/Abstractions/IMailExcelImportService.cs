using DirectorComercialIA.Mail.Models;

namespace DirectorComercialIA.Mail.Abstractions;

public class MailColumnMappingDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Company { get; set; }
    public string? Email { get; set; }
    public string? Country { get; set; }
    public string? Language { get; set; }
    public string? CustomerType { get; set; }
    public string? JobTitle { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? City { get; set; }
    public string? Notes { get; set; }
}

public class MailImportPreviewRowDto
{
    public int RowNumber { get; set; }
    public Dictionary<string, string?> Cells { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class MailImportPreviewResultDto
{
    public List<string> Headers { get; set; } = new();
    public List<MailImportPreviewRowDto> Rows { get; set; } = new();
}

public class MailImportExecuteResultDto
{
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public int NewRows { get; set; }
    public int UpdatedRows { get; set; }
    public int IgnoredDuplicates { get; set; }
    public int ImportBatchId { get; set; }
}

public interface IMailExcelImportService
{
    Task<MailImportPreviewResultDto> PreviewAsync(Stream xlsxStream, CancellationToken ct = default);
    Task<MailImportExecuteResultDto> ImportAsync(
        Stream xlsxStream,
        MailColumnMappingDto mapping,
        DuplicateImportMode duplicateMode,
        string? campaignName,
        string importedByUserId,
        bool importOnlyValidRows,
        CancellationToken ct = default);
}
