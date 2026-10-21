using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;

namespace GroceryHelper.Api.Authentication;

internal static class AuthenticationEndpoints
{
    internal const string SessionCookieName = "GroceryHelper.Session";

    public static IServiceCollection AddDemoAuthentication(
        this IServiceCollection services)
    {
        services.AddSingleton<DemoAccountAuthenticator>();
        services.AddSingleton<SessionStore>();
        services.AddSingleton(TimeProvider.System);
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services
            .AddAuthentication(DemoSessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DemoSessionAuthenticationHandler>(
                DemoSessionAuthenticationHandler.SchemeName,
                _ => { });
        services.AddAuthorization();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "GroceryHelper.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });
        return services;
    }

    public static WebApplication MapAuthenticationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new CsrfResponse(tokens.RequestToken!));
        });

        app.MapPost(
            "/api/auth/login",
            async (
                HttpContext context,
                LoginRequest request,
                IAntiforgery antiforgery,
                DemoAccountAuthenticator authenticator,
                SessionStore sessions,
                TimeProvider timeProvider) =>
            {
                try
                {
                    await antiforgery.ValidateRequestAsync(context);
                }
                catch (AntiforgeryValidationException)
                {
                    return Results.BadRequest();
                }

                if (!authenticator.Authenticate(request.Username, request.Password))
                {
                    return Results.Problem(
                        detail: "Invalid username or password",
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                var session = sessions.Create(timeProvider.GetUtcNow());
                context.Response.Cookies.Append(
                    SessionCookieName,
                    session.Token,
                    new CookieOptions
                    {
                        Expires = session.ExpiresAt,
                        HttpOnly = true,
                        IsEssential = true,
                        Path = "/",
                        SameSite = SameSiteMode.Lax,
                        Secure = context.Request.IsHttps,
                    });
                return Results.NoContent();
            });

        app.MapGet(
            "/api/auth/session",
            (HttpContext context) => Results.Ok(
                new SessionResponse(context.User.Identity!.Name!)))
            .RequireAuthorization();

        return app;
    }

    private sealed record CsrfResponse(string Token);

    private sealed record LoginRequest(string? Username, string? Password);

    private sealed record SessionResponse(string Username);
}
