using System.Globalization;
using Kalkulator.Infrastructure;
using Kalkulator.Infrastructure.Erstbefuellung;
using Kalkulator.Infrastructure.Persistenz;
using Kalkulator.Web.Components;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

// „--erstbefuellung“: Datenbank migrieren, leeren Katalog befüllen und beenden (Issue #7, Betrieb siehe #8).
const string ErstbefuellungSchalter = "--erstbefuellung";
var erstbefuellung = args.Contains(ErstbefuellungSchalter);

var builder = WebApplication.CreateBuilder(args.Where(a => a != ErstbefuellungSchalter).ToArray());

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHealthChecks();

// Die Verbindungszeichenfolge setzt die interne IT je Umgebung (siehe #2, #8); geöffnet wird erst beim ersten Zugriff.
builder.Services.AddKalkulatorInfrastruktur(builder.Configuration.GetConnectionString("Kalkulator"));

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

app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

/// <summary>Einstiegspunkt; öffentlich, damit Integrationstests die Anwendung starten können.</summary>
public partial class Program;
