namespace DirectorComercialIA.Mail.Abstractions;

public class MailSendEmailForPhaseResultDto
{
    public int Sent { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class MailContactMergeData
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string CustomerType { get; set; } = string.Empty;
}

public interface IMailEmailDispatchService
{
    Task<MailSendEmailForPhaseResultDto> SendForPhaseAsync(
        int phaseNumber,
        IReadOnlyList<int> contactIds,
        string subject,
        string htmlBody,
        string sentByUserId,
        CancellationToken ct = default);

    string MergeTemplate(string template, MailContactMergeData data);
}
