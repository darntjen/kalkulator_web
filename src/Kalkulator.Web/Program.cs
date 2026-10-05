using System.Globalization;
using Kalkulator.Infrastructure;
using Kalkulator.Infrastructure.Ablage;
using Kalkulator.Infrastructure.Anwendung;
using Kalkulator.Infrastructure.Erstbefuellung;
using Kalkulator.Infrastructure.Paperless;
using Kalkulator.Infrastructure.Persistenz;
using Kalkulator.Infrastructure.Vorlagen;
using Kalkulator.Web.Anmeldung;
using Kalkulator.Web.Components;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

// „--erstbefuellung“: Datenbank migrieren, leeren Katalog befüllen und beenden (Issue #7, Betrieb siehe #8).
const string ErstbefuellungSchalter = "--erstbefuellung";
var erstbefuellung = args.Contains(ErstbefuellungSchalter);

// „--paperless-test name@firma.de“: zwei Muster-Entwürfe an Paperless übergeben und beenden (Frage 12.5).
var paperlessTest = Array.IndexOf(args, PaperlessTestlauf.Schalter);
var paperlessEmpfaenger = paperlessTest >= 0 && paperlessTest + 1 < args.Length ? args[paperlessTest + 1] : "";

var builder = WebApplication.CreateBuilder(args
    .Where((a, i) => a != ErstbefuellungSchalter && (paperlessTest < 0 || (i != paperlessTest && i != paperlessTest + 1)))
    .ToArray());

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHealthChecks();

// Vor der Infrastruktur, damit der angemeldete Benutzer den Systembenutzer ersetzt.
builder.Services.AddKalkulatorAnmeldung(builder.Environment);

// Die Verbindungszeichenfolge setzt die interne IT je Umgebung (siehe #2, #8); geöffnet wird erst beim ersten Zugriff.
builder.Services.AddKalkulatorInfrastruktur(builder.Configuration.GetConnectionString("Kalkulator"));

// Angebotsvorlage und Textbausteine liegen beim Programm (templates/angebot wird mitkopiert); der Ordner ist umstellbar (C-02).
builder.Services.Configure<AngebotsEinstellungen>(builder.Configuration.GetSection("Angebot"));
builder.Services.Configure<VorlagenEinstellungen>(builder.Configuration.GetSection(VorlagenEinstellungen.Abschnitt));
builder.Services.Configure<PdfEinstellungen>(builder.Configuration.GetSection(PdfEinstellungen.Abschnitt));
builder.Services.Configure<PaperlessEinstellungen>(builder.Configuration.GetSection(PaperlessEinstellungen.Abschnitt));
builder.Services.Configure<KundenablageEinstellungen>(builder.Configuration.GetSection(KundenablageEinstellungen.Abschnitt));
builder.Services.PostConfigure<AngebotsEinstellungen>(e =>
{
    if (string.IsNullOrWhiteSpace(e.Vorlagenordner))
    {
        e.Vorlagenordner = Path.Combine(AppContext.BaseDirectory, "Vorlagen", "angebot");
    }
});
builder.Services.Configure<VertragswerkEinstellungen>(builder.Configuration.GetSection("Vertragswerk"));
builder.Services.PostConfigure<VertragswerkEinstellungen>(e =>
{
    if (string.IsNullOrWhiteSpace(e.Deckblatt))
    {
        e.Deckblatt = Path.Combine(AppContext.BaseDirectory, "Vorlagen", "vertrag", "Deckblatt.docx");
    }
});

var app = builder.Build();

if (erstbefuellung)
{
    await using var bereich = app.Services.CreateAsyncScope();
    var kontext = bereich.ServiceProvider.GetRequiredService<KalkulatorDbContext>();
    await kontext.Database.MigrateAsync();
    var ergebnis = await KatalogErstbefuellung.AusfuehrenAsync(kontext);
    app.Logger.LogInformation("{Meldung}", ergebnis.Meldung);
    return;
}

if (paperlessTest >= 0)
{
    var einstellungen = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaperlessEinstellungen>>().Value;
    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
    try
    {
        await PaperlessTestlauf.AusfuehrenAsync(
            einstellungen, paperlessEmpfaenger, e => new PaperlessUebergabe(http, Microsoft.Extensions.Options.Options.Create(e)), Console.Out);
    }
    catch (Exception e) when (e is PaperlessFehler or ArgumentException)
    {
        Console.Error.WriteLine($"Paperless-Testlauf fehlgeschlagen: {e.Message}");
        Environment.ExitCode = 1;
    }

    return;
}

// Die Oberfläche ist ausschließlich deutsch: Zahlen, Währungen und Datumsangaben im Format de-DE.
var deutsch = new CultureInfo("de-DE");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(deutsch),
    SupportedCultures = [deutsch],
    SupportedUICultures = [deutsch],
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapEntwicklungsRollenwechsel();

// Archiviertes Angebot herunterladen (C-07); Rechte prüft der Dienst wie in der Oberfläche.
app.MapGet("/angebote/{id:int}/datei", async (int id, AngebotsDienst dienst, CancellationToken abbruch) =>
{
    try
    {
        var (name, inhalt) = await dienst.DateiAsync(id, abbruch);
        return Results.File(inhalt, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", name);
    }
    catch (KeinZugriffException)
    {
        return Results.Forbid();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
});

// Katalog und Preisliste als Excel (A-09); nur Produktmanagement und Führung, weil die EK-Kalkulation enthalten ist.
app.MapGet("/katalog/export/{id:int}", async (int id, PreislistenDienst dienst, CancellationToken abbruch) =>
{
    try
    {
        var (name, inhalt) = await dienst.ExportAsync(id, abbruch);
        return Results.File(inhalt, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }
    catch (KeinZugriffException)
    {
        return Results.Forbid();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
});
// Vertragswerk als Gesamt-PDF oder ZIP (#26, Teil C); wer das Kundenprojekt sehen darf.
app.MapGet("/vertragswerke/{id:int}/{art:regex(^(pdf|zip)$)}", async (int id, string art, VertragswerkDienst dienst, CancellationToken abbruch) =>
{
    try
    {
        var zip = art == "zip";
        var (name, inhalt) = await dienst.DateiAsync(id, zip, abbruch);
        return Results.File(inhalt, zip ? "application/zip" : "application/pdf", name);
    }
    catch (KeinZugriffException)
    {
        return Results.Forbid();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
});

// Word-Datei einer Vorlagenfassung (#26, Teil B); Produktmanagement und Führung.
app.MapGet("/katalog/vorlagen/fassung/{id:int}/datei", async (int id, VorlagenDienst dienst, CancellationToken abbruch) =>
{
    try
    {
        var (name, inhalt) = await dienst.DateiAsync(id, abbruch);
        return Results.File(inhalt, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", name);
    }
    catch (KeinZugriffException)
    {
        return Results.Forbid();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
});
// Hochgeladene Unterlage bzw. Datei aus dem Kanalordner eines Kundenprojekts (Phase 4); wer das Projekt sehen darf.
app.MapGet("/unterlagen/{id:int}", async (int id, UnterlagenDienst dienst, CancellationToken abbruch) =>
{
    try
    {
        var (name, typ, inhalt) = await dienst.DateiAsync(id, abbruch);
        return Results.File(inhalt, typ, name);
    }
    catch (KeinZugriffException)
    {
        return Results.Forbid();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
});
app.MapGet("/kundenprojekte/{id:int}/ablage", async (int id, string datei, UnterlagenDienst dienst, CancellationToken abbruch) =>
{
    try
    {
        var (name, inhalt) = await dienst.AblageDateiAsync(id, datei, abbruch);
        return Results.File(inhalt, "application/octet-stream", name);
    }
    catch (KeinZugriffException)
    {
        return Results.Forbid();
    }
    catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException)
    {
        return Results.NotFound();
    }
});
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

/// <summary>Einstiegspunkt; öffentlich, damit Integrationstests die Anwendung starten können.</summary>
public partial class Program;
