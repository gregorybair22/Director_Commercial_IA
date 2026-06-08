using System.Net.Mail;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DirectorComercialIA.Data;
using DirectorComercialIA.Mail.Abstractions;
using DirectorComercialIA.Mail.Models;
using Microsoft.EntityFrameworkCore;

namespace DirectorComercialIA.Mail.Services;

public class MailExcelImportService : IMailExcelImportService
{
    private readonly AppDbContext _db;

    public MailExcelImportService(AppDbContext db) => _db = db;

    public async Task<MailImportPreviewResultDto> PreviewAsync(Stream xlsxStream, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook(xlsxStream);
        var ws = workbook.Worksheets.First();
        var range = ws.RangeUsed();
        if (range == null)
            return new MailImportPreviewResultDto();

        var firstRow = range.FirstRow();
        var headers = new List<string>();
        foreach (var cell in firstRow.CellsUsed())
            headers.Add(cell.GetString().Trim());

        var result = new MailImportPreviewResultDto { Headers = headers };

        var lastRow = range.LastRow().RowNumber();
        for (var r = firstRow.RowNumber() + 1; r <= lastRow; r++)
        {
            var rowErrors = new List<string>();
            var cells = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count; c++)
            {
                var v = ws.Cell(r, c + 1).GetFormattedString().Trim();
                if (!string.IsNullOrEmpty(v))
                    cells[headers[c]] = v;
            }

            if (IsRowEmpty(cells))
                continue;

            var emailCol = FindEmailInRow(cells, headers);
            if (emailCol == null)
                rowErrors.Add("No valid email found in row.");
            else if (!IsValidEmail(emailCol))
                rowErrors.Add($"Invalid email: {emailCol}");

            result.Rows.Add(new MailImportPreviewRowDto
            {
                RowNumber = r,
                Cells = cells,
                Errors = rowErrors
            });
        }

        return await Task.FromResult(result);
    }

    public async Task<MailImportExecuteResultDto> ImportAsync(
        Stream xlsxStream,
        MailColumnMappingDto mapping,
        DuplicateImportMode duplicateMode,
        string? campaignName,
        string importedByUserId,
        bool importOnlyValidRows,
        CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook(xlsxStream);
        var ws = workbook.Worksheets.First();
        var range = ws.RangeUsed();
        if (range == null)
            return new MailImportExecuteResultDto();

        var headers = new List<string>();
        foreach (var cell in range.FirstRow().CellsUsed())
            headers.Add(cell.GetString().Trim());

        var indexByHeader = headers
            .Select((h, i) => (h, i))
            .ToDictionary(x => NormalizeHeader(x.h), x => x.i, StringComparer.OrdinalIgnoreCase);

        int? Idx(string? mappedName)
        {
            if (string.IsNullOrWhiteSpace(mappedName)) return null;
            var key = NormalizeHeader(mappedName.Trim());
            return indexByHeader.TryGetValue(key, out var ix) ? ix : null;
        }

        var map = new ColumnIndexes(
            Idx(mapping.FirstName),
            Idx(mapping.LastName),
            Idx(mapping.Company),
            Idx(mapping.Email),
            Idx(mapping.Country),
            Idx(mapping.Language),
            Idx(mapping.CustomerType),
            Idx(mapping.JobTitle),
            Idx(mapping.Phone),
            Idx(mapping.Website),
            Idx(mapping.City),
            Idx(mapping.Notes));

        if (map.Email == null)
            throw new InvalidOperationException("Email column mapping is required.");

        var batch = new MailImportBatch
        {
            FileName = "import.xlsx",
            CampaignName = campaignName,
            ImportedByUserId = importedByUserId,
            ImportedAt = DateTime.UtcNow
        };
        _db.MailImportBatches.Add(batch);
        await _db.SaveChangesAsync(ct);

        var total = 0;
        var invalid = 0;
        var newRows = 0;
        var updated = 0;
        var ignored = 0;

        var lastRow = range.LastRow().RowNumber();
        for (var r = range.FirstRow().RowNumber() + 1; r <= lastRow; r++)
        {
            string Cell(int? ix) =>
                ix == null ? string.Empty : ws.Cell(r, ix.Value + 1).GetFormattedString().Trim();

            var emailRaw = Cell(map.Email);
            var email = EmailImportHelper.ExtractPrimaryEmail(emailRaw);
            if (string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(emailRaw))
                email = EmailImportHelper.ExtractPrimaryEmail(emailRaw.Replace(" ", ""));

            if (IsRowEmptyFromMapping(r, ws, map))
                continue;

            total++;

            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(email))
                errors.Add("Email missing");
            else if (!IsValidEmail(email))
                errors.Add("Invalid email");

            if (errors.Count > 0)
            {
                invalid++;
                if (importOnlyValidRows)
                    continue;
            }

            if (errors.Count > 0)
                continue;

            email = email!.Trim().ToLowerInvariant();

            var contact = new MailContact
            {
                FirstName = Cell(map.FirstName),
                LastName = Cell(map.LastName),
                Company = Cell(map.Company),
                Email = email,
                Country = Cell(map.Country),
                Language = Cell(map.Language),
                CustomerType = Cell(map.CustomerType),
                JobTitle = Cell(map.JobTitle),
                Phone = Cell(map.Phone),
                Website = Cell(map.Website),
                City = Cell(map.City),
                Notes = Cell(map.Notes),
                CurrentPhase = 1,
                ImportBatchId = batch.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var existing = await _db.MailContacts.FirstOrDefaultAsync(c => c.Email.ToLower() == contact.Email, ct);
            if (existing != null)
            {
                switch (duplicateMode)
                {
                    case DuplicateImportMode.Ignore:
                        ignored++;
                        continue;
                    case DuplicateImportMode.Update:
                        existing.FirstName = contact.FirstName;
                        existing.LastName = contact.LastName;
                        existing.Company = contact.Company;
                        existing.Country = contact.Country;
                        existing.Language = contact.Language;
                        existing.CustomerType = contact.CustomerType;
                        existing.JobTitle = contact.JobTitle;
                        existing.Phone = contact.Phone;
                        existing.Website = contact.Website;
                        existing.City = contact.City;
                        existing.Notes = string.IsNullOrEmpty(existing.Notes) ? contact.Notes : existing.Notes + "\n" + contact.Notes;
                        existing.ImportBatchId = batch.Id;
                        existing.UpdatedAt = DateTime.UtcNow;
                        updated++;
                        break;
                    case DuplicateImportMode.NewOnly:
                        ignored++;
                        continue;
                }
            }
            else
            {
                _db.MailContacts.Add(contact);
                newRows++;
            }
        }

        batch.TotalRows = total;
        batch.ValidRows = newRows + updated;
        batch.InvalidRows = invalid;
        batch.NewRows = newRows;
        batch.UpdatedRows = updated;
        batch.IgnoredDuplicates = ignored;

        await _db.SaveChangesAsync(ct);

        return new MailImportExecuteResultDto
        {
            TotalRows = total,
            ValidRows = batch.ValidRows,
            InvalidRows = invalid,
            NewRows = newRows,
            UpdatedRows = updated,
            IgnoredDuplicates = ignored,
            ImportBatchId = batch.Id
        };
    }

    private static bool IsRowEmpty(Dictionary<string, string?> cells) =>
        cells.Count == 0 || cells.Values.All(string.IsNullOrWhiteSpace);

    private static bool IsRowEmptyFromMapping(int r, IXLWorksheet ws, ColumnIndexes m)
    {
        int?[] ixes =
        {
            m.FirstName, m.LastName, m.Company, m.Email, m.Country, m.Language, m.CustomerType, m.JobTitle, m.Phone,
            m.Website, m.City, m.Notes
        };
        foreach (var ix in ixes)
        {
            if (ix == null) continue;
            var v = ws.Cell(r, ix.Value + 1).GetFormattedString().Trim();
            if (!string.IsNullOrEmpty(v))
                return false;
        }

        return true;
    }

    private sealed record ColumnIndexes(
        int? FirstName,
        int? LastName,
        int? Company,
        int? Email,
        int? Country,
        int? Language,
        int? CustomerType,
        int? JobTitle,
        int? Phone,
        int? Website,
        int? City,
        int? Notes);

    private static string? FindEmailInRow(Dictionary<string, string?> cells, List<string> headers)
    {
        foreach (var h in headers)
        {
            if (!cells.TryGetValue(h, out var val) || string.IsNullOrWhiteSpace(val)) continue;
            if (NormalizeHeader(h).Contains("mail", StringComparison.OrdinalIgnoreCase) ||
                EmailImportHelper.LooksLikeEmail(val))
            {
                var e = EmailImportHelper.ExtractPrimaryEmail(val);
                if (!string.IsNullOrEmpty(e))
                    return e;
            }
        }

        foreach (var kv in cells)
        {
            var e = EmailImportHelper.ExtractPrimaryEmail(kv.Value);
            if (!string.IsNullOrEmpty(e))
                return e;
        }

        return null;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
        }
    }

    private static string NormalizeHeader(string h) =>
        h.Trim().ToLowerInvariant().Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");
}

internal static class EmailImportHelper
{
    private static readonly Regex EmailRegex = new(@"[a-z0-9._%+\-]+@[a-z0-9.\-]+\.[a-z]{2,}", RegexOptions.IgnoreCase);

    public static bool LooksLikeEmail(string s) => EmailRegex.IsMatch(s);

    public static string? ExtractPrimaryEmail(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var matches = EmailRegex.Matches(raw);
        return matches.Count == 0 ? null : matches[0].Value.Trim().TrimEnd(',');
    }
}
