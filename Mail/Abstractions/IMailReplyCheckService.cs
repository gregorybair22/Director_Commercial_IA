namespace DirectorComercialIA.Mail.Abstractions;

public class MailReplyCheckSummaryDto
{
    public int Checked { get; set; }
    public int RepliesFound { get; set; }
    public int NoReply { get; set; }
    public int Errors { get; set; }
}

public interface IMailReplyCheckService
{
    Task<MailReplyCheckSummaryDto> CheckRepliesForPhaseAsync(int phase, CancellationToken ct = default);
}
