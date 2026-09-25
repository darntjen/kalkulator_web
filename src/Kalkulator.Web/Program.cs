using System.Globalization;
using Kalkulator.Infrastructure;
using Kalkulator.Web.Components;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHealthChecks();

// Die Verbindungszeichenfolge setzt die interne IT je Umgebung (siehe #2, #8); geöffnet wird erst beim ersten Zugriff.
builder.Services.AddKalkulatorInfrastruktur(builder.Configuration.GetConnectionString("Kalkulator"));

var app = builder.Build();

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
