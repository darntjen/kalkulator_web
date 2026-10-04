using Kalkulator.Infrastructure.Ablage;
using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Berechnung;
using Kalkulator.Infrastructure.Paperless;
using Kalkulator.Infrastructure.Persistenz;
using Kalkulator.Infrastructure.Vorlagen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Kalkulator.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registriert Datenbankzugriff und Hilfsdienste. Die Verbindung wird erst beim ersten Zugriff geöffnet.</summary>
    public static IServiceCollection AddKalkulatorInfrastruktur(this IServiceCollection services, string? verbindungszeichenfolge)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IBenutzerKontext, SystemBenutzer>();
        // Fabrik je Anfrage bzw. Blazor-Verbindung, weil der Kontext den angemeldeten Benutzer braucht;
        // die Dienste erzeugen daraus kurzlebige Kontexte. Der Kontext selbst bleibt zusätzlich als Scoped-Dienst abrufbar.
        services.AddDbContextFactory<KalkulatorDbContext>(options => options.UseSqlServer(verbindungszeichenfolge), ServiceLifetime.Scoped);
        services.AddScoped<RechenkernLader>();
        services.AddScoped<KundenprojektDienst>();
        services.AddScoped<KalkulationsDienst>();
        services.AddScoped<AngebotsDienst>();
        services.AddScoped<KatalogDienst>();
        services.AddScoped<PreislistenDienst>();
        services.AddOptions<AngebotsEinstellungen>();
        services.AddOptions<VorlagenEinstellungen>();
        services.AddSingleton(Vorlagenquelle);
        services.AddOptions<PdfEinstellungen>();
        services.AddSingleton(PdfWandler);
        services.AddScoped<VorlagenDienst>();
        services.AddScoped<VertragswerkDienst>();
        services.AddOptions<VertragswerkEinstellungen>();
        services.AddOptions<PaperlessEinstellungen>();
        services.TryAddSingleton<IPaperlessUebergabe>(d =>
            new PaperlessUebergabe(new HttpClient { Timeout = TimeSpan.FromMinutes(2) }, d.GetRequiredService<IOptions<PaperlessEinstellungen>>()));
        services.AddHostedService<NaechtlicherVorlagenabgleich>();
        services.AddOptions<KundenablageEinstellungen>();
        services.AddSingleton(Kundenablage);
        services.AddScoped<UnterlagenDienst>();
        services.AddScoped<KundensituationDienst>();
        services.AddHostedService<UnterlagenAufbewahrung>();
        return services;
    }

    /// <summary>PDF-Umwandlung laut Konfiguration „Pdf“; Graph nutzt die Anmeldung aus „Vorlagen:SharePoint“.</summary>
    private static IPdfWandler PdfWandler(IServiceProvider dienste)
    {
        var pdf = dienste.GetRequiredService<IOptions<PdfEinstellungen>>().Value;
        var sharePoint = dienste.GetRequiredService<IOptions<VorlagenEinstellungen>>().Value.SharePoint;
        try
        {
            return (pdf.Wandler ?? "").Trim().ToUpperInvariant() switch
            {
                "GRAPH" => new GraphPdfWandler(GraphZugang.Aus(sharePoint, new HttpClient { Timeout = TimeSpan.FromMinutes(2) }), sharePoint, pdf),
                "LIBREOFFICE" => new LibreOfficeWandler(string.IsNullOrWhiteSpace(pdf.LibreOffice) ? "soffice" : pdf.LibreOffice),
                "" => new KeinPdfWandler(),
                _ => throw new InvalidOperationException($"Unbekannte PDF-Umwandlung „{pdf.Wandler}“; erlaubt sind „Graph“ und „LibreOffice“."),
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.Security.Cryptography.CryptographicException or IOException)
        {
            return new KeinPdfWandler(ex.Message);
        }
    }

    /// <summary>Quelle der Vertragsvorlagen laut Konfiguration „Vorlagen“; eine unvollständige Konfiguration meldet der Abgleich.</summary>
    private static IVorlagenQuelle Vorlagenquelle(IServiceProvider dienste)
    {
        var e = dienste.GetRequiredService<IOptions<VorlagenEinstellungen>>().Value;
        try
        {
            return (e.Quelle ?? "").Trim().ToUpperInvariant() switch
            {
                "SHAREPOINT" => new SharePointQuelle(GraphZugang.Aus(e.SharePoint, new HttpClient { Timeout = TimeSpan.FromMinutes(2) }), e.SharePoint),
                "ORDNER" => new OrdnerQuelle(string.IsNullOrWhiteSpace(e.Ordner)
                    ? throw new InvalidOperationException("Für die Quelle „Ordner“ fehlt die Einstellung Vorlagen:Ordner.")
                    : e.Ordner),
                "" => new KeineVorlagenquelle(),
                _ => throw new InvalidOperationException($"Unbekannte Vorlagenquelle „{e.Quelle}“; erlaubt sind „SharePoint“ und „Ordner“."),
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.Security.Cryptography.CryptographicException or IOException)
        {
            return new KeineVorlagenquelle(ex.Message);
        }
    }

    /// <summary>Kanalordner der Kunden laut Konfiguration „Kundenablage“; Graph nutzt die Anmeldung aus „Vorlagen:SharePoint“.</summary>
    private static IKundenablage Kundenablage(IServiceProvider dienste)
    {
        var e = dienste.GetRequiredService<IOptions<KundenablageEinstellungen>>().Value;
        var sharePoint = dienste.GetRequiredService<IOptions<VorlagenEinstellungen>>().Value.SharePoint;
        try
        {
            return (e.Quelle ?? "").Trim().ToUpperInvariant() switch
            {
                "SHAREPOINT" => new SharePointKundenablage(GraphZugang.Aus(sharePoint, new HttpClient { Timeout = TimeSpan.FromMinutes(2) }),
                    string.IsNullOrWhiteSpace(e.Website) ? throw new InvalidOperationException("Für die Kundenablage fehlt die Einstellung Kundenablage:Website.") : e),
                "ORDNER" => new OrdnerKundenablage(string.IsNullOrWhiteSpace(e.Ordner)
                    ? throw new InvalidOperationException("Für die Quelle „Ordner“ fehlt die Einstellung Kundenablage:Ordner.")
                    : e.Ordner),
                "" => new KeineKundenablage(),
                _ => throw new InvalidOperationException($"Unbekannte Kundenablage „{e.Quelle}“; erlaubt sind „SharePoint“ und „Ordner“."),
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.Security.Cryptography.CryptographicException or IOException)
        {
            return new KeineKundenablage(ex.Message);
        }
    }
}
