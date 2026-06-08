using DirectorComercialIA.Mail.Abstractions;

namespace DirectorComercialIA.Mail.Endpoints;

/// <summary>
/// Gmail OAuth uses Google's redirect back to <c>/mail/gmail-callback</c>. Connect/disconnect run as
/// plain HTTP endpoints so session state is saved before the browser leaves for Google (Blazor onclick does not).
/// </summary>
public static class MailGmailOAuthEndpoints
{
    public static WebApplication MapMailGmailOAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/mail/gmail-connect", (HttpContext http, IMailGmailOAuthService gmail) =>
        {
            var state = Guid.NewGuid().ToString("N");
            http.Session.SetString("gmail_oauth_state", state);
            var redirect = $"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}/mail/gmail-callback";
            var url = gmail.GetAuthorizationUrl(redirect, state);
            return Results.Redirect(url);
        }).DisableAntiforgery();

        app.MapPost("/mail/gmail-disconnect", async Task<IResult> (
            HttpContext http,
            IMailGmailOAuthService gmail,
            CancellationToken ct) =>
        {
            await gmail.DisconnectAsync(ct);
            return Results.Redirect("/mail/gmail");
        }).DisableAntiforgery();

        app.MapGet("/mail/gmail-callback", async Task<IResult> (
            HttpContext http,
            IMailGmailOAuthService gmail,
            CancellationToken ct) =>
        {
            var error = http.Request.Query["error"].FirstOrDefault();
            if (!string.IsNullOrEmpty(error))
                return Results.Redirect($"/mail/gmail?error={Uri.EscapeDataString(error)}");

            var expected = http.Session.GetString("gmail_oauth_state");
            var state = http.Request.Query["state"].FirstOrDefault();
            if (string.IsNullOrEmpty(expected) || state != expected)
            {
                return Results.Redirect("/mail/gmail?error=" +
                    Uri.EscapeDataString("Invalid OAuth state. Try Connect Gmail again."));
            }

            var code = http.Request.Query["code"].FirstOrDefault();
            if (string.IsNullOrEmpty(code))
            {
                return Results.Redirect("/mail/gmail?error=" +
                    Uri.EscapeDataString("Missing authorization code."));
            }

            var redirect =
                $"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}/mail/gmail-callback";
            try
            {
                await gmail.CompleteAuthorizationAsync(code, redirect, ct);
            }
            catch (Exception ex)
            {
                return Results.Redirect("/mail/gmail?error=" + Uri.EscapeDataString(ex.Message));
            }

            http.Session.Remove("gmail_oauth_state");
            return Results.Redirect("/mail/gmail?ok=1");
        });

        return app;
    }
}
