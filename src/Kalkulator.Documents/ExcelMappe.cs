using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Kalkulator.Documents;

/// <summary>Zahlenformat einer Zelle.</summary>
public enum Zellformat
{
    Standard = 0,
    Euro = 2,
    Prozent = 3,
    Datum = 4,
    Zahl = 5,
}

/// <summary>Wert einer Zelle; Text bleibt Text, Zahlen werden als Zahl mit Format geschrieben.</summary>
public readonly record struct Zelle(object? Wert, Zellformat Format = Zellformat.Standard)
{
    public static implicit operator Zelle(string? text) => new(text);

    public static implicit operator Zelle(int zahl) => new(zahl, Zellformat.Zahl);

    public static Zelle Euro(decimal? betrag) => new(betrag, Zellformat.Euro);

    public static Zelle Prozent(decimal? anteil) => new(anteil, Zellformat.Prozent);

    public static Zelle Zahl(decimal? wert) => new(wert, Zellformat.Zahl);

    public static Zelle Datum(DateOnly? datum) => new(datum, Zellformat.Datum);
}

/// <summary>Ein Tabellenblatt mit fetter, fixierter Kopfzeile.</summary>
public sealed record Tabellenblatt(string Name, IReadOnlyList<string> Spalten, IEnumerable<IReadOnlyList<Zelle>> Zeilen);

/// <summary>
/// Schreibt einfache Excel-Arbeitsmappen (.xlsx) mit dem Open XML SDK, z. B. für den Katalog-Export (A-09).
/// Bewusst schlicht: Kopfzeile, Werte, Zahlenformate, Spaltenbreiten und Autofilter.
/// </summary>
public static class ExcelMappe
{
    public static byte[] Erzeuge(IReadOnlyList<Tabellenblatt> blaetter)
    {
        using var strom = new MemoryStream();
        using (var mappe = SpreadsheetDocument.Create(strom, SpreadsheetDocumentType.Workbook))
        {
            var teil = mappe.AddWorkbookPart();
            teil.Workbook = new Workbook();
            var stile = teil.AddNewPart<WorkbookStylesPart>();
            stile.Stylesheet = Stilvorlage();

            var liste = teil.Workbook.AppendChild(new Sheets());
            uint nummer = 1;
            foreach (var blatt in blaetter)
            {
                var blattTeil = teil.AddNewPart<WorksheetPart>();
                blattTeil.Worksheet = Blatt(blatt);
                liste.Append(new Sheet { Id = teil.GetIdOfPart(blattTeil), SheetId = nummer++, Name = Blattname(blatt.Name) });
            }

            teil.Workbook.Save();
        }

        return strom.ToArray();
    }

    private static Worksheet Blatt(Tabellenblatt blatt)
    {
        var zeilen = blatt.Zeilen.ToList();
        var daten = new SheetData();
        daten.Append(new Row(blatt.Spalten.Select(s => TextZelle(s, 1))) { RowIndex = 1 });
        uint index = 2;
        foreach (var zeile in zeilen)
        {
            daten.Append(new Row(zeile.Select(Zellwert)) { RowIndex = index++ });
        }

        var breiten = blatt.Spalten.Select((s, i) => Math.Clamp(
            Math.Max(s.Length, zeilen.Select(z => i < z.Count ? Anzeigelaenge(z[i]) : 0).DefaultIfEmpty(0).Max()) + 2, 8, 60)).ToList();
        var spalten = new Columns(breiten.Select((b, i) => new Column { Min = (uint)i + 1, Max = (uint)i + 1, Width = b, CustomWidth = true }));

        var ansicht = new SheetViews(new SheetView(new Pane
        {
            VerticalSplit = 1,
            TopLeftCell = "A2",
            ActivePane = PaneValues.BottomLeft,
            State = PaneStateValues.Frozen,
        })
        { WorkbookViewId = 0 });

        var arbeitsblatt = new Worksheet(ansicht, spalten, daten);
        if (blatt.Spalten.Count > 0)
        {
            arbeitsblatt.Append(new AutoFilter { Reference = $"A1:{Spaltenname(blatt.Spalten.Count)}{Math.Max(1, zeilen.Count + 1)}" });
        }

        return arbeitsblatt;
    }

    private static Cell Zellwert(Zelle zelle) => zelle.Wert switch
    {
        null => new Cell(),
        string text => TextZelle(text, 0),
        bool wahr => TextZelle(wahr ? "ja" : "nein", 0),
        DateOnly datum => new Cell
        {
            DataType = CellValues.Number,
            CellValue = new CellValue(datum.ToDateTime(TimeOnly.MinValue).ToOADate()),
            StyleIndex = (uint)Zellformat.Datum,
        },
        int zahl => new Cell { DataType = CellValues.Number, CellValue = new CellValue(zahl), StyleIndex = (uint)Format(zelle) },
        decimal zahl => new Cell { DataType = CellValues.Number, CellValue = new CellValue(zahl), StyleIndex = (uint)Format(zelle) },
        var anderes => TextZelle(anderes.ToString(), 0),
    };

    private static Zellformat Format(Zelle zelle) => zelle.Format == Zellformat.Standard ? Zellformat.Zahl : zelle.Format;

    private static Cell TextZelle(string? text, uint stil) => new(new InlineString(new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve }))
    {
        DataType = CellValues.InlineString,
        StyleIndex = stil,
    };

    private static int Anzeigelaenge(Zelle zelle) => zelle.Wert switch
    {
        null => 0,
        string text => text.Length,
        DateOnly => 10,
        decimal or int => 12,
        var anderes => anderes.ToString()?.Length ?? 0,
    };

    /// <summary>Stile: 0 Standard, 1 Kopfzeile fett, 2 Euro, 3 Prozent, 4 Datum, 5 Zahl.</summary>
    private static Stylesheet Stilvorlage() => new(
        new NumberingFormats(
            new NumberingFormat { NumberFormatId = 164, FormatCode = "#,##0.00 \"€\"" },
            new NumberingFormat { NumberFormatId = 165, FormatCode = "0.0%" },
            new NumberingFormat { NumberFormatId = 166, FormatCode = "dd.mm.yyyy" },
            new NumberingFormat { NumberFormatId = 167, FormatCode = "#,##0.####" })
        { Count = 4 },
        new Fonts(new Font(), new Font(new Bold())) { Count = 2 },
        new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 })) { Count = 2 },
        new Borders(new Border()) { Count = 1 },
        new CellFormats(
            new CellFormat(),
            new CellFormat { FontId = 1, ApplyFont = true },
            new CellFormat { NumberFormatId = 164, ApplyNumberFormat = true },
            new CellFormat { NumberFormatId = 165, ApplyNumberFormat = true },
            new CellFormat { NumberFormatId = 166, ApplyNumberFormat = true },
            new CellFormat { NumberFormatId = 167, ApplyNumberFormat = true })
        { Count = 6 });

    /// <summary>Excel erlaubt höchstens 31 Zeichen und keine Zeichen aus []:*?/\ im Blattnamen.</summary>
    private static string Blattname(string name)
    {
        var sauber = new string([.. name.Select(z => "[]:*?/\\".Contains(z) ? '-' : z)]);
        return sauber.Length > 31 ? sauber[..31] : sauber;
    }

    private static string Spaltenname(int nummer)
    {
        var name = "";
        while (nummer > 0)
        {
            var rest = (nummer - 1) % 26;
            name = (char)('A' + rest) + name;
            nummer = (nummer - 1) / 26;
        }

        return name;
    }
}
