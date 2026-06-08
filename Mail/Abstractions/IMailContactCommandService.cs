namespace DirectorComercialIA.Mail.Abstractions;

public interface IMailContactCommandService
{
    Task AddCommentAsync(int contactId, string userId, string comment, CancellationToken ct = default);
    Task AssignAsync(int contactId, string? assignUserId, CancellationToken ct = default);
    Task MovePhaseAsync(int contactId, int toPhase, string userId, string reason, CancellationToken ct = default);
    Task SetRespondedAsync(int contactId, bool responded, CancellationToken ct = default);
    Task SetDoNotContactAsync(int contactId, bool value, CancellationToken ct = default);
    Task SetAdminOverrideAsync(int contactId, bool allow, CancellationToken ct = default);
}
