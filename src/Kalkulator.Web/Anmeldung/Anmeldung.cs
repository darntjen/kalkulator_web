using System.Security.Claims;
using System.Text.Encodings.Web;
using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

namespace Kalkulator.Web.Anmeldung;

/// <summary>
/// Übergangslösung bis zur Anmeldung über Entra ID (#4): In der Entwicklungsumgebung ist man automatisch angemeldet
/// und wählt die Rolle über einen Umschalter im Kopf. In allen anderen Umgebungen ist die Anwendung gesperrt.
/// </summary>
public static class Anmeldung
{
    public const string EntwicklungsSchema = "Entwicklung";
    public const string GesperrtSchema = "Gesperrt";
    public const string RollenCookie = "kalkulator-rolle";
    public const string RollenPfad = "/entwicklung/rolle";

    public static IServiceCollection AddKalkulatorAnmeldung(this IServiceCollection services, IHostEnvironment umgebung)
    {
        if (umgebung.IsDevelopment())
        {
            services.AddAuthentication(EntwicklungsSchema)
                .AddScheme<AuthenticationSchemeOptions, EntwicklungsAnmeldung>(EntwicklungsSchema, null);
        }
        else
        {
            services.AddAuthentication(GesperrtSchema)
                .AddScheme<AuthenticationSchemeOptions, GesperrteAnmeldung>(GesperrtSchema, null);
        }

        // Ohne Anmeldung ist nichts erreichbar; Ausnahmen (Health-Check) erlauben anonymen Zugriff ausdrücklich.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddCascadingAuthenticationState();
        services.AddScoped<IBenutzerKontext, AngemeldeterBenutzer>();
        return services;
    }

    /// <summary>Rollenwechsel in der Entwicklungsumgebung: setzt das Cookie und lädt die Seite neu.</summary>
    public static void MapEntwicklungsRollenwechsel(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        app.MapGet(RollenPfad, (HttpContext http, string rolle, string? zurueck) =>
        {
            if (!Rollen.Alle.Contains(rolle))
            {
                return Results.BadRequest("Unbekannte Rolle.");
            }

            http.Response.Cookies.Append(RollenCookie, rolle, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true });
            var ziel = zurueck is { Length: > 0 } && zurueck.StartsWith('/') && !zurueck.StartsWith("//", StringComparison.Ordinal) ? zurueck : "/";
            return Results.LocalRedirect(ziel);
        }).AllowAnonymous();
    }
}

/// <summary>Meldet in der Entwicklung einen Testbenutzer mit der per Cookie gewählten Rolle an (Standard: Vertrieb).</summary>
public sealed class EntwicklungsAnmeldung(IOptionsMonitor<AuthenticationSchemeOptions> optionen, ILoggerFactory protokoll, UrlEncoder kodierer)
    : AuthenticationHandler<AuthenticationSchemeOptions>(optionen, protokoll, kodierer)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var rolle = Request.Cookies[Anmeldung.RollenCookie];
        if (rolle is null || !Rollen.Alle.Contains(rolle))
        {
            rolle = Rollen.Vertrieb;
        }

        var identitaet = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, $"{rolle.ToLowerInvariant()}@noesse.de"), new Claim(ClaimTypes.Role, rolle)],
            Anmeldung.EntwicklungsSchema);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identitaet), Scheme.Name)));
    }
}

/// <summary>Solange Entra ID nicht eingerichtet ist (#4), wird außerhalb der Entwicklung niemand angemeldet.</summary>
public sealed class GesperrteAnmeldung(IOptionsMonitor<AuthenticationSchemeOptions> optionen, ILoggerFactory protokoll, UrlEncoder kodierer)
    : AuthenticationHandler<AuthenticationSchemeOptions>(optionen, protokoll, kodierer)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
}

/// <summary>Der angemeldete Benutzer der aktuellen Anfrage bzw. Blazor-Verbindung.</summary>
public sealed class AngemeldeterBenutzer(AuthenticationStateProvider anmeldestatus) : IBenutzerKontext
{
    private ClaimsPrincipal? _benutzer;

    public string Name => Benutzer.Identity?.Name ?? "unbekannt";

    public bool IstInRolle(string rolle) => Benutzer.IsInRole(rolle);

    // Der Status steht in Blazor beim Aufbau der Verbindung bzw. beim Vorrendern fest; die Aufgabe ist dann abgeschlossen.
    // Außerhalb (z. B. bei der Erstbefüllung) gibt es keinen Status und damit keinen Benutzer.
    private ClaimsPrincipal Benutzer => _benutzer ??= Lese();

    private ClaimsPrincipal Lese()
    {
        try
        {
            return anmeldestatus.GetAuthenticationStateAsync().GetAwaiter().GetResult().User;
        }
        catch (InvalidOperationException)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
}
