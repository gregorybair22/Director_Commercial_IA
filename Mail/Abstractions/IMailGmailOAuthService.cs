namespace DirectorComercialIA.Mail.Abstractions;

public class MailGmailStatusDto
{
    public bool IsConnected { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public interface IMailGmailOAuthService
{
    string GetAuthorizationUrl(string redirectUri, string state);
    Task CompleteAuthorizationAsync(string code, string redirectUri, CancellationToken ct = default);
    Task<MailGmailStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
}
