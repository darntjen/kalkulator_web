using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Kalkulator.Infrastructure.Vorlagen;

/// <summary>
/// Abschnitt „Pdf“ der Konfiguration: Umwandlung der Word-Dokumente des Vertragswerks in PDF (#26, Teil C).
/// Im Betrieb über Microsoft 365 (Graph, Anmeldung wie bei <see cref="VorlagenEinstellungen.SharePoint"/>), in der
/// Entwicklung wahlweise mit LibreOffice.
/// </summary>
public sealed class PdfEinstellungen
{
    public const string Abschnitt = "Pdf";

    /// <summary>„Graph“, „LibreOffice“ oder leer (nicht eingerichtet).</summary>
    public string? Wandler { get; set; }

    /// <summary>Bei Graph: Arbeitsordner in der Bibliothek der Vorlagen-Website; Dateien dort werden nach der Umwandlung gelöscht.</summary>
    public string? Ordnerpfad { get; set; } = "Kalkulator/PDF-Umwandlung";

    /// <summary>Bei LibreOffice: Programm, z. B. „soffice“ oder der volle Pfad zu soffice.exe.</summary>
    public string? LibreOffice { get; set; } = "soffice";
}

public interface IPdfWandler
{
    string Name { get; }

    /// <summary>Wandelt eine Word-Datei (.docx) in PDF um.</summary>
    Task<byte[]> InPdfAsync(byte[] docx, CancellationToken abbruch);
}

/// <summary>Steht für die Umwandlung, solange keine eingerichtet ist; meldet den Grund verständlich.</summary>
public sealed class KeinPdfWandler(string grund = "Die PDF-Umwandlung ist nicht eingerichtet. Bitte „Pdf:Wandler“ auf „Graph“ oder „LibreOffice“ setzen.") : IPdfWandler
{
    public string Name => "keine";

    public string Grund => grund;

    public Task<byte[]> InPdfAsync(byte[] docx, CancellationToken abbruch) => throw new InvalidOperationException(grund);
}

/// <summary>
/// Umwandlung über Microsoft 365: Datei in einen Arbeitsordner hochladen, als PDF abrufen, wieder löschen. Die App
/// braucht dafür Schreibrecht auf die Website (Sites.Selected, Rolle „write“).
/// </summary>
public sealed class GraphPdfWandler(GraphZugang graph, SharePointEinstellungen sharePoint, PdfEinstellungen pdf) : IPdfWandler
{
    private string? _laufwerk;

    public string Name => "Microsoft 365";

    public async Task<byte[]> InPdfAsync(byte[] docx, CancellationToken abbruch)
    {
        _laufwerk ??= await graph.LaufwerkAsync(sharePoint.Website, sharePoint.Bibliothek, abbruch);
        var pfad = $"{(pdf.Ordnerpfad ?? "").Trim('/')}/{Guid.NewGuid():N}.docx".TrimStart('/');

        using var hochladen = new HttpRequestMessage(HttpMethod.Put, $"drives/{_laufwerk}/root:/{GraphZugang.Pfad(pfad)}:/content")
        {
            Content = new ByteArrayContent(docx) { Headers = { ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document") } },
        };
        string id;
        using (var antwort = await graph.SendeAsync(hochladen, abbruch))
        {
            id = (await antwort.Content.ReadFromJsonAsync<Element>(abbruch))?.Id ?? throw new InvalidOperationException("Microsoft Graph hat keine Datei-ID geliefert.");
        }

        try
        {
            return await graph.LadeAsync($"drives/{_laufwerk}/items/{id}/content?format=pdf", abbruch);
        }
        finally
        {
            try
            {
                using var loeschen = new HttpRequestMessage(HttpMethod.Delete, $"drives/{_laufwerk}/items/{id}");
                (await graph.SendeAsync(loeschen, CancellationToken.None)).Dispose();
            }
            catch (GraphFehler)
            {
                // Aufräumen ist nicht kritisch; der Arbeitsordner lässt sich auch von Hand leeren.
            }
        }
    }

    private sealed record Element([property: JsonPropertyName("id")] string Id);
}

/// <summary>Umwandlung mit LibreOffice (headless); für Entwicklung, Tests und als Notlösung.</summary>
public sealed class LibreOfficeWandler(string programm) : IPdfWandler
{
    /// <summary>LibreOffice verträgt keine parallelen Läufe mit demselben Profil.</summary>
    private static readonly SemaphoreSlim Sperre = new(1, 1);

    public string Name => "LibreOffice";

    public async Task<byte[]> InPdfAsync(byte[] docx, CancellationToken abbruch)
    {
        var ordner = Directory.CreateTempSubdirectory("kalkulator-pdf-").FullName;
        await Sperre.WaitAsync(abbruch);
        try
        {
            var eingabe = Path.Combine(ordner, "dokument.docx");
            await File.WriteAllBytesAsync(eingabe, docx, abbruch);
            var start = new ProcessStartInfo(programm)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in new[] { $"-env:UserInstallation=file://{Path.Combine(ordner, "profil").Replace('\\', '/')}", "--headless", "--convert-to", "pdf", "--outdir", ordner, eingabe })
            {
                start.ArgumentList.Add(argument);
            }

            using var prozess = Process.Start(start) ?? throw new InvalidOperationException($"LibreOffice ({programm}) ließ sich nicht starten.");
            using var zeitlimit = CancellationTokenSource.CreateLinkedTokenSource(abbruch);
            zeitlimit.CancelAfter(TimeSpan.FromMinutes(2));
            await prozess.WaitForExitAsync(zeitlimit.Token);
            var ausgabe = Path.Combine(ordner, "dokument.pdf");
            return File.Exists(ausgabe)
                ? await File.ReadAllBytesAsync(ausgabe, abbruch)
                : throw new InvalidOperationException($"LibreOffice hat kein PDF erzeugt: {await prozess.StandardError.ReadToEndAsync(abbruch)}");
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            throw new InvalidOperationException($"LibreOffice ({programm}) ist nicht installiert: {e.Message}");
        }
        finally
        {
            Sperre.Release();
            try
            {
                Directory.Delete(ordner, recursive: true);
            }
            catch (IOException)
            {
                // Temporärer Ordner; bleibt notfalls liegen.
            }
        }
    }
}
