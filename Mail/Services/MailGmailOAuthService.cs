using System.Net;
using System.Text.RegularExpressions;
using DirectorComercialIA.Data;
using DirectorComercialIA.Mail.Abstractions;
using DirectorComercialIA.Mail.Models;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DirectorComercialIA.Mail.Services;

public class MailGmailOAuthService : IMailGmailOAuthService
{
    private static readonly string[] Scopes =
    [
        GmailService.Scope.GmailSend,
        GmailService.Scope.GmailReadonly,
        GmailService.Scope.GmailModify
    ];

    private readonly AppDbContext _db;
    private readonly GmailOptions _options;
    private readonly IDataProtectionProvider _dataProtection;

    public MailGmailOAuthService(AppDbContext db, IOptions<GmailOptions> options, IDataProtectionProvider dataProtection)
    {
        _db = db;
        _options = options.Value;
        _dataProtection = dataProtection;
    }

    private IDataProtector Protector => _dataProtection.CreateProtector("DirectorComercialIA.Gmail.v1");

    private GoogleAuthorizationCodeFlow CreateFlow() =>
        new(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _options.ClientId,
                ClientSecret = _options.ClientSecret
            },
            Scopes = Scopes
        });

    public string GetAuthorizationUrl(string redirectUri, string state)
    {
        return new GoogleAuthorizationCodeRequestUrl(new Uri(Google.Apis.Auth.OAuth2.GoogleAuthConsts.AuthorizationUrl))
        {
            ClientId = _options.ClientId,
            RedirectUri = redirectUri,
            ResponseType = "code",
            Scope = string.Join(" ", Scopes),
            State = state,
            AccessType = "offline",
            Prompt = "consent"
        }.Build().ToString();
    }

    public async Task CompleteAuthorizationAsync(string code, string redirectUri, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Authorization code required.", nameof(code));

        var flow = CreateFlow();
        var token = await flow.ExchangeCodeForTokenAsync("app", code, redirectUri, ct);

        if (string.IsNullOrEmpty(token.RefreshToken))
            throw new InvalidOperationException(
                "Google did not return a refresh token. Disconnect the app in your Google account security settings and connect again.");

        var credential = new UserCredential(flow, "app", token);
        var gmail = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "DirectorComercialIA"
        });

        Google.Apis.Gmail.v1.Data.Profile profile;
        try
        {
            profile = await gmail.Users.GetProfile("me").ExecuteAsync(ct);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(FormatGmailForbiddenMessage(ex), ex);
        }

        var email = profile.EmailAddress ?? throw new InvalidOperationException("Could not read Gmail profile.");

        var row = await _db.MailGmailConnections.FirstOrDefaultAsync(x => x.Id == 1, ct);
        if (row == null)
        {
            row = new MailGmailConnection();
            _db.MailGmailConnections.Add(row);
        }

        row.ConnectedEmail = email;
        row.RefreshTokenProtected = Protector.Protect(token.RefreshToken);
        row.ConnectedAt = DateTime.UtcNow;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MailGmailStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var row = await _db.MailGmailConnections.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct);
        if (row == null || string.IsNullOrEmpty(row.RefreshTokenProtected))
            return new MailGmailStatusDto();

        try
        {
            _ = Protector.Unprotect(row.RefreshTokenProtected);
        }
        catch
        {
            return new MailGmailStatusDto();
        }

        return new MailGmailStatusDto
        {
            IsConnected = true,
            Email = row.ConnectedEmail,
            DisplayName = row.DisplayName
        };
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        var row = await _db.MailGmailConnections.FirstOrDefaultAsync(x => x.Id == 1, ct);
        if (row == null) return;
        row.RefreshTokenProtected = string.Empty;
        row.ConnectedEmail = string.Empty;
        row.DisplayName = null;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    internal async Task<UserCredential?> GetUserCredentialAsync(CancellationToken ct)
    {
        var row = await _db.MailGmailConnections.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct);
        if (row == null || string.IsNullOrEmpty(row.RefreshTokenProtected)) return null;
        string refresh;
        try
        {
            refresh = Protector.Unprotect(row.RefreshTokenProtected);
        }
        catch
        {
            return null;
        }

        var flow = CreateFlow();
        return new UserCredential(flow, "app", new TokenResponse { RefreshToken = refresh });
    }

    internal async Task<GmailService?> GetGmailServiceAsync(CancellationToken ct)
    {
        var credential = await GetUserCredentialAsync(ct);
        if (credential == null) return null;
        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "DirectorComercialIA"
        });
    }

    private static string FormatGmailForbiddenMessage(GoogleApiException ex)
    {
        var detail = ex.Message;
        var link = TryExtractGoogleApisConsoleUrl(detail);
        if (detail.Contains("Gmail API has not been used", StringComparison.OrdinalIgnoreCase)
            || (detail.Contains("gmail.googleapis.com", StringComparison.OrdinalIgnoreCase)
                && detail.Contains("disabled", StringComparison.OrdinalIgnoreCase)))
        {
            var msg =
                "The Gmail API is not enabled for the Google Cloud project that owns this OAuth client. "
                + "Open Google Cloud Console → APIs & Services → Library, search for Gmail API, click Enable, "
                + "wait a few minutes, then try Connect Gmail again.";
            return string.IsNullOrEmpty(link) ? msg : $"{msg} Enable link: {link}";
        }

        var suffix = string.IsNullOrEmpty(link) ? "" : $" See: {link}";
        return $"Gmail refused this request (403). Check API enablement and OAuth consent for restricted scopes.{suffix} Details: {detail}";
    }

    private static string? TryExtractGoogleApisConsoleUrl(string text)
    {
        var m = Regex.Match(text, @"https://console\.developers\.google\.com/\S+");
        if (!m.Success) return null;
        return m.Value.TrimEnd('.', ')');
    }
}
