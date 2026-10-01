using Kalkulator.Domain.Katalog;
using Kalkulator.Domain.Preise;

namespace Kalkulator.Domain.Berechnung;

/// <summary>Codes, an denen der Rechenkern Sonderfälle erkennt (docs/06_ist-analyse.md, Abschnitte 3–7).</summary>
public static class KatalogCodes
{
    public const string StrategischeBegleitung = "S41";
    public const string Schwachstellenmanagement = "S25";
    public const string ServerBackup = "S14";
    public const string ServerPremium = "B04";
    public const string Supportkontingent = "S60";
    public const string CloudServer = "S61";

    public const string BackupGrund = "S14-GRUND";
    public const string BackupServer = "S14-SERVER";
    public const string BackupPaket = "S14-PAKET";
    public const string BackupObjektspeicher = "S14-OBJEKT";
    public const string BackupLizenz = "S14-LIZENZ";

    /// <summary>Services, die nur über ihren Sonderrechner gebucht werden, nicht als freie Position.</summary>
    public static readonly IReadOnlySet<string> Sonderrechner = new HashSet<string> { ServerBackup, Supportkontingent, CloudServer };
}

/// <summary>
/// Berechnet eine Managed-Services-Kalkulation aus Katalog und Preisliste (docs/03_fachmodell.md, „Rechenkern“).
/// Ohne Oberfläche und Datenbank; dieselbe Logik dient Kalkulator, Angebot, Vertrag und Statistik.
/// Rundung: jede Zeile kaufmännisch auf Cent.
/// </summary>
public sealed class Rechenkern
{
    private readonly Preisliste _preisliste;
    private readonly Dictionary<string, Service> _services;
    private readonly Dictionary<string, Preiskomponente> _komponenten;
    private readonly Dictionary<Preiskomponente, Service> _serviceZuKomponente;

    public Rechenkern(IEnumerable<Service> katalog, Preisliste preisliste)
    {
        _preisliste = preisliste;
        _services = katalog.ToDictionary(s => s.Code);
        _serviceZuKomponente = _services.Values
            .SelectMany(s => s.Preiskomponenten.Select(k => (Komponente: k, Service: s)))
            .ToDictionary(x => x.Komponente, x => x.Service);
        _komponenten = _serviceZuKomponente.Keys.ToDictionary(k => k.Code);
    }

    /// <summary>Preisliste, mit der dieser Rechenkern rechnet.</summary>
    public Preisliste Preisliste => _preisliste;

    public KalkulationsErgebnis Berechne(KalkulationsEingabe eingabe)
    {
        var lauf = new Lauf(this, eingabe);
        return lauf.Ausfuehren();
    }

    private static decimal Runden(decimal betrag) => Math.Round(betrag, 2, MidpointRounding.AwayFromZero);

    private static string Name(Service service) => $"{service.Code} {service.Bezeichnung}";

    /// <summary>Monatliche Komponenten eines Service in Katalogreihenfolge.</summary>
    private static List<Preiskomponente> Monatlich(Service service) =>
        [.. service.Preiskomponenten.Where(k => k.Abrechnungsart == Abrechnungsart.Monatlich).OrderBy(k => k.Sortierung)];

    /// <summary>Ein Rechenlauf mit seinem Zwischenstand.</summary>
    private sealed class Lauf(Rechenkern kern, KalkulationsEingabe eingabe)
    {
        private readonly List<(ErgebnisPosition Position, (int, int, int, int) Sortierung)> _positionen = [];
        private readonly List<Meldung> _meldungen = [];
        private readonly HashSet<Service> _direkt = [];
        private readonly Dictionary<Preiskomponente, (decimal Menge, string Bundle)> _abgedeckt = [];
        private SupportkontingentErgebnis? _supportkontingent;
        private ServerBackupErgebnis? _serverBackup;

        public KalkulationsErgebnis Ausfuehren()
        {
            var gebucht = PruefeUndSammlePositionen();
            foreach (var (komponente, menge) in gebucht.Where(g => kern._serviceZuKomponente[g.Key].Typ == ServiceTyp.Bundle))
            {
                var bundle = kern._serviceZuKomponente[komponente];
                Abdecken(bundle, komponente, menge, bundle.Code);
            }

            foreach (var (komponente, menge) in gebucht)
            {
                Katalogzeile(komponente, menge);
            }

            Onboarding();
            Supportkontingent();
            ServerBackup();
            CloudServer();
            Sonderpositionen();
            PruefeRegeln();

            var vergleich = eingabe.BisherigerMonatspreis is { } bisher
                ? new VorherNachher(bisher, _positionen.Where(p => p.Position.Abrechnungsart == Abrechnungsart.Monatlich).Sum(p => p.Position.Betrag))
                : null;

            return new KalkulationsErgebnis(
                [.. _positionen.OrderBy(p => p.Sortierung).Select(p => p.Position)],
                [.. _meldungen.Distinct()],
                _supportkontingent,
                _serverBackup,
                vergleich);
        }

        private void Fehler(string text) => _meldungen.Add(new Meldung(Schwere.Fehler, text));

        private void Hinweis(string text) => _meldungen.Add(new Meldung(Schwere.Hinweis, text));

        private Dictionary<Preiskomponente, int> PruefeUndSammlePositionen()
        {
            var gebucht = new Dictionary<Preiskomponente, int>();
            foreach (var position in eingabe.Positionen)
            {
                if (!kern._komponenten.TryGetValue(position.KomponentenCode, out var komponente))
                {
                    Fehler($"„{position.KomponentenCode}“ ist im Katalog nicht vorhanden.");
                    continue;
                }

                var service = kern._serviceZuKomponente[komponente];
                if (position.Menge < 0)
                {
                    Fehler($"{Name(service)}: Die Menge darf nicht negativ sein.");
                    continue;
                }

                if (position.Menge == 0)
                {
                    continue;
                }

                if (KatalogCodes.Sonderrechner.Contains(service.Code))
                {
                    Fehler($"{Name(service)} wird über den zugehörigen Rechner erfasst, nicht als Position.");
                    continue;
                }

                if (komponente.Abrechnungsart == Abrechnungsart.Einmalig && komponente.StaffelBezug == StaffelBezug.User)
                {
                    Fehler("Die Onboarding-Pauschale wird automatisch aus Connect-Stufe und Anzahl User berechnet.");
                    continue;
                }

                if (!service.DarfAngebotenWerden)
                {
                    Fehler($"{Name(service)} darf nicht angeboten werden (Vertriebsstatus „{service.Vertriebsstatus}“).");
                    continue;
                }

                _direkt.Add(service);
                gebucht[komponente] = gebucht.GetValueOrDefault(komponente) + position.Menge;
            }

            return gebucht;
        }

        /// <summary>
        /// Regel R3: Was in einem gebuchten Bundle enthalten ist, wird bis zur Bundle-Menge nicht berechnet.
        /// Abgezogen wird nur, wenn die Einheit übereinstimmt (B01 je User deckt S03 je User ab, B07 je Switch deckt S51 ab).
        /// Leistungen auf anderer Einheit bleiben berechnet, z. B. eine zusätzliche Firewall S21 neben B05 (je Kunde)
        /// oder S53 für weitere Netzwerkgeräte neben B07 (RK-03). Verschachtelte Bundles werden durchlaufen.
        /// </summary>
        private void Abdecken(Service bundle, Preiskomponente bundleKomponente, decimal menge, string ursprung)
        {
            foreach (var bestandteil in bundle.Bestandteile.Select(b => b.Bestandteil!))
            {
                var passend = Monatlich(bestandteil).FirstOrDefault(k => k.Einheit == bundleKomponente.Einheit);
                if (passend is null)
                {
                    continue;
                }

                _abgedeckt[passend] = _abgedeckt.TryGetValue(passend, out var bisher)
                    ? (bisher.Menge + menge, bisher.Bundle)
                    : (menge, ursprung);

                if (bestandteil.Typ == ServiceTyp.Bundle)
                {
                    Abdecken(bestandteil, passend, menge, ursprung);
                }
            }
        }

        private void Katalogzeile(Preiskomponente komponente, int menge)
        {
            var service = kern._serviceZuKomponente[komponente];
            decimal? preis;
            string? stufenHinweis = null;
            switch (komponente.StaffelBezug)
            {
                case StaffelBezug.Mitarbeitende when eingabe.AnzahlMitarbeitende is not > 0:
                    Fehler($"{Name(service)}: Bitte die Anzahl Mitarbeitende angeben.");
                    preis = null;
                    break;
                case StaffelBezug.Mitarbeitende:
                    var stufe = kern._preisliste.StaffelstufeFuer(komponente, eingabe.AnzahlMitarbeitende!.Value);
                    preis = stufe?.VkNetto;
                    stufenHinweis = stufe?.Bezeichnung;
                    break;
                default:
                    preis = kern._preisliste.PreisFuer(komponente);
                    if (preis is null)
                    {
                        Fehler($"{Name(service)}: Für „{komponente.Bezeichnung}“ ist kein Preis hinterlegt (auf Anfrage). Bitte als Sonderposition erfassen.");
                    }

                    break;
            }

            var berechnet = (decimal)menge;
            var hinweis = stufenHinweis;
            if (_abgedeckt.TryGetValue(komponente, out var abdeckung))
            {
                var frei = Math.Min(menge, abdeckung.Menge);
                berechnet = menge - frei;
                hinweis = $"In {abdeckung.Bundle} enthalten: {frei:0.##} von {menge} nicht berechnet";
                Hinweis($"{Name(service)} ist in {abdeckung.Bundle} enthalten und wird für {frei:0.##} von {menge} Einheiten nicht zusätzlich berechnet.");
            }

            var betrag = preis is null ? 0m : Runden(preis.Value * berechnet);
            var ek = kern._preisliste.KostenFuer(komponente);
            Hinzufuegen(
                new ErgebnisPosition(komponente.Code, komponente.Bezeichnung, service.Code, menge, berechnet, preis, betrag,
                    komponente.Abrechnungsart, PositionsHerkunft.Katalog, hinweis)
                {
                    Kosten = berechnet == 0 ? 0m : ek is null ? null : Runden(ek.Value * berechnet),
                },
                service, komponente.Sortierung);
        }

        private void Onboarding()
        {
            var connect = _direkt.Where(s => s.Typ == ServiceTyp.Connect).ToList();
            if (connect.Count != 1)
            {
                return;
            }

            var stufeConnect = connect[0];
            var komponente = stufeConnect.Preiskomponenten.FirstOrDefault(k =>
                k.Abrechnungsart == Abrechnungsart.Einmalig && k.StaffelBezug == StaffelBezug.User);
            if (komponente is null)
            {
                return;
            }

            if (eingabe.AnzahlUser <= 0)
            {
                Hinweis("Für die Onboarding-Pauschale bitte die Anzahl User angeben.");
                return;
            }

            var stufe = kern._preisliste.StaffelstufeFuer(komponente, eingabe.AnzahlUser);
            if (stufe?.VkNetto is not { } preis)
            {
                Hinweis($"Onboarding bei {eingabe.AnzahlUser} Usern: {stufe?.Bezeichnung ?? "individuell kalkulieren"}. Der Betrag ist nicht in der Summe enthalten.");
                return;
            }

            Hinzufuegen(
                new ErgebnisPosition(komponente.Code, $"{komponente.Bezeichnung} {stufe.Bezeichnung}", stufeConnect.Code, 1, 1, preis, preis,
                    Abrechnungsart.Einmalig, PositionsHerkunft.Onboarding),
                stufeConnect, komponente.Sortierung);
        }

        private void Supportkontingent()
        {
            if (eingabe.Supportkontingent is not { } s60)
            {
                return;
            }

            var service = kern._services[KatalogCodes.Supportkontingent];
            var komponente = Monatlich(service).First();
            if (s60.AnfragenProMonat < 0 || s60.AeJeAnfrage < 0)
            {
                Fehler($"{Name(service)}: Anfragen und AE je Anfrage dürfen nicht negativ sein.");
                return;
            }

            var block = (int)kern._preisliste.ParameterWert(ParameterSchluessel.SupportkontingentBlockgroesseAe);
            var bedarf = Math.Round(s60.AnfragenProMonat * s60.AeJeAnfrage, 1, MidpointRounding.AwayFromZero);
            var kontingent = (int)Math.Ceiling(bedarf / block) * block;
            if (kontingent == 0)
            {
                Hinweis($"{Name(service)}: Bei 0 Anfragen entsteht kein Kontingent.");
                return;
            }

            var satz = kern._preisliste.PreisFuer(komponente)!.Value;
            var monatspreis = Runden(kontingent * satz);
            var adHoc = Runden(kontingent * kern._preisliste.ParameterWert(ParameterSchluessel.AeSatzEbene2));
            _supportkontingent = new SupportkontingentErgebnis(bedarf, kontingent, kontingent / 4m, monatspreis, adHoc);

            _direkt.Add(service);
            Hinzufuegen(
                new ErgebnisPosition(komponente.Code, $"Supportkontingent ({kontingent} AE = {kontingent / 4m:0.##} Std.)", service.Code,
                    kontingent, kontingent, satz, monatspreis, Abrechnungsart.Monatlich, PositionsHerkunft.Supportkontingent),
                service, komponente.Sortierung);
        }

        /// <summary>S14 nach Baukasten V5.7 mit Preisuntergrenze (Parameter, aktuell 187 €).</summary>
        private void ServerBackup()
        {
            if (eingabe.ServerBackup is not { } s14)
            {
                return;
            }

            var service = kern._services[KatalogCodes.ServerBackup];
            if (s14.Server < 1 || s14.NativGeschuetzteGb < 0 || s14.BelegteTb < 0 || s14.Lizenzinstanzen < 0)
            {
                Fehler($"{Name(service)}: Mindestens ein Server; Datenmengen und Lizenzinstanzen dürfen nicht negativ sein.");
                return;
            }

            _direkt.Add(service);
            var zeilen = new List<(string Code, decimal Menge)> { (KatalogCodes.BackupGrund, 1), (KatalogCodes.BackupServer, s14.Server) };
            if (s14.Variante == BackupVariante.Cloud)
            {
                var paketGroesse = kern._preisliste.ParameterWert(ParameterSchluessel.BackupPaketgroesseGb);
                zeilen.Add((KatalogCodes.BackupPaket, Math.Max(1, Math.Ceiling(s14.NativGeschuetzteGb / paketGroesse))));
            }
            else
            {
                zeilen.Add((KatalogCodes.BackupObjektspeicher, s14.BelegteTb));
                if (s14.Lizenzinstanzen > 0)
                {
                    zeilen.Add((KatalogCodes.BackupLizenz, s14.Lizenzinstanzen));
                }
            }

            var summe = 0m;
            foreach (var (code, menge) in zeilen)
            {
                var komponente = kern._komponenten[code];
                var preis = kern._preisliste.PreisFuer(komponente)!.Value;
                var betrag = Runden(preis * menge);
                summe += betrag;
                Hinzufuegen(
                    new ErgebnisPosition(code, komponente.Bezeichnung, service.Code, menge, menge, preis, betrag,
                        Abrechnungsart.Monatlich, PositionsHerkunft.ServerBackup),
                    service, komponente.Sortierung);
            }

            var untergrenze = kern._preisliste.ParameterWert(ParameterSchluessel.BackupPreisuntergrenze);
            var monatspreis = Math.Max(summe, untergrenze);
            _serverBackup = new ServerBackupErgebnis(summe, untergrenze, monatspreis);
            if (monatspreis > summe)
            {
                Hinzufuegen(
                    new ErgebnisPosition("S14-UNTERGRENZE", $"Ausgleich auf die Preisuntergrenze von {untergrenze:0.00} €", service.Code,
                        1, 1, monatspreis - summe, monatspreis - summe, Abrechnungsart.Monatlich, PositionsHerkunft.ServerBackup),
                    service, 99);
            }
        }

        /// <summary>S61: VK = EK aus dem TERRA-Kalkulator ÷ Margenteiler (0,55), kaufmännisch gerundet.</summary>
        private void CloudServer()
        {
            if (eingabe.CloudServer is not { } s61)
            {
                return;
            }

            var service = kern._services[KatalogCodes.CloudServer];
            var komponente = Monatlich(service).First();
            _direkt.Add(service);
            if (s61.EkTerraKalkulator <= 0)
            {
                Fehler($"{Name(service)}: Bitte den EK aus dem TERRA-Kalkulator erfassen.");
                return;
            }

            var teiler = kern._preisliste.ParameterWert(ParameterSchluessel.CloudServerMargenteiler);
            var vk = Runden(s61.EkTerraKalkulator / teiler);
            Hinzufuegen(
                new ErgebnisPosition(komponente.Code, komponente.Bezeichnung, service.Code, 1, 1, vk, vk,
                    Abrechnungsart.Monatlich, PositionsHerkunft.CloudServer)
                {
                    Kosten = Runden(s61.EkTerraKalkulator),
                },
                service, komponente.Sortierung);

            switch (s61.Backup)
            {
                case CloudBackup.Offen:
                    Fehler($"{Name(service)}: Bitte die Datensicherung festlegen: S14 buchen oder „Datensicherung durch den Kunden“ wählen.");
                    break;
                case CloudBackup.ServerBackup when eingabe.ServerBackup is null:
                    Fehler($"{Name(service)}: Die Datensicherung über S14 ist gewählt, aber S14 ist nicht kalkuliert.");
                    break;
                case CloudBackup.ServerBackup when eingabe.ServerBackup.Variante != BackupVariante.Cloud:
                    Fehler($"{Name(service)}: Server aus S61 werden zwingend per Cloud-Backup gesichert (Regel R5).");
                    break;
                default:
                    break;
            }
        }

        private void Sonderpositionen()
        {
            var nummer = 0;
            foreach (var sp in eingabe.Sonderpositionen)
            {
                nummer++;
                var titel = string.IsNullOrWhiteSpace(sp.Bezeichnung) ? $"Sonderposition {nummer}" : $"Sonderposition „{sp.Bezeichnung}“";
                if (string.IsNullOrWhiteSpace(sp.Bezeichnung) || string.IsNullOrWhiteSpace(sp.Einheit) || string.IsNullOrWhiteSpace(sp.Begruendung))
                {
                    Fehler($"{titel}: Bezeichnung, Einheit und Begründung sind Pflicht.");
                }

                if (sp.Menge <= 0 || sp.Preis <= 0)
                {
                    Fehler($"{titel}: Menge und Preis müssen größer als 0 sein.");
                    continue;
                }

                if (!sp.Freigegeben)
                {
                    Fehler($"{titel}: Freigabe durch die Vertriebsleitung ausstehend. Angebot und Vertragspaket sind gesperrt.");
                }

                _positionen.Add((
                    new ErgebnisPosition($"SONDER-{nummer}", sp.Bezeichnung, null, sp.Menge, sp.Menge, sp.Preis, Runden(sp.Preis * sp.Menge),
                        sp.Einmalig ? Abrechnungsart.Einmalig : Abrechnungsart.Monatlich, PositionsHerkunft.Sonderposition, sp.Einheit),
                    (int.MaxValue, nummer, 0, 0)));
            }
        }

        private void PruefeRegeln()
        {
            var enthalten = new HashSet<Service>();
            foreach (var bundle in _direkt.Where(s => s.Typ == ServiceTyp.Bundle))
            {
                SammleBestandteile(bundle, enthalten);
            }

            var vorhanden = _direkt.Concat(enthalten).Select(s => s.Code).ToHashSet();

            // Für S61 zählt nur, was zusätzlich berechnet wird: Der Cloud Server braucht immer eine eigene
            // Firewall-Instanz S21, auch wenn B05 gebucht ist (Entscheidung 01.10.2026).
            var berechnet = _positionen
                .Where(p => p.Position.Herkunft == PositionsHerkunft.Katalog && p.Position.BerechneteMenge > 0)
                .Select(p => p.Position.ServiceCode!)
                .ToHashSet();

            // R1: genau eine Connect-Stufe; einzige Ausnahme sind reine S41-Kalkulationen.
            var connect = _direkt.Count(s => s.Typ == ServiceTyp.Connect);
            if (connect == 0 && _direkt.Any(s => s.Code != KatalogCodes.StrategischeBegleitung))
            {
                Fehler("Nösse Connect ist Pflicht: Bitte genau eine Connect-Stufe wählen. Ohne Connect ist nur eine reine S41-Kalkulation möglich.");
            }

            // Regeln aus dem Katalog. Services, die ohnehin über ein gebuchtes Bundle geliefert werden (z. B. S25-Assets
            // zu B06), prüfen wir nicht erneut; die Voraussetzungen des Bundles gelten.
            foreach (var service in _direkt.Where(s => !enthalten.Contains(s)))
            {
                foreach (var regel in service.Regeln)
                {
                    var ziele = regel.Ziele.Select(z => z.ZielService?.Code ?? ZielCode(z.ZielServiceId)).ToList();
                    var erfuellt = service.Code == KatalogCodes.CloudServer ? berechnet : vorhanden;
                    var verletzt = regel.Typ switch
                    {
                        RegelTyp.ErfordertAlle => !ziele.All(erfuellt.Contains),
                        RegelTyp.ErfordertEinenVon => !ziele.Any(erfuellt.Contains),
                        RegelTyp.SchliesstAus => _direkt.Any(s => ziele.Contains(s.Code)),
                        _ => false,
                    };
                    if (verletzt)
                    {
                        Fehler(regel.Meldung);
                    }
                }
            }

            if (_direkt.Any(s => s.Code == KatalogCodes.ServerPremium) && eingabe.ServerBackup is null)
            {
                Fehler("B04 Server as a Service Premium enthält Server Backup: Bitte den S14-Baukasten ausfüllen.");
            }

            PruefeSchwachstellenAssets(enthalten);
        }

        /// <summary>Assetpreise von S25 gibt es nur zusammen mit dem Grundservice, einzeln oder über B06.</summary>
        private void PruefeSchwachstellenAssets(HashSet<Service> enthalten)
        {
            if (!kern._services.TryGetValue(KatalogCodes.Schwachstellenmanagement, out var s25) || !_direkt.Contains(s25))
            {
                return;
            }

            var grund = Monatlich(s25).First();
            var grundGebucht = _positionen.Any(p => p.Position.Code == grund.Code);
            if (!grundGebucht && !enthalten.Contains(s25))
            {
                Fehler($"{Name(s25)}: Assetpreise (je Client, je Server) gibt es nur zusammen mit dem Grundservice oder mit B06.");
            }
        }

        private string ZielCode(int id) => kern._services.Values.Single(s => s.Id == id).Code;

        private static void SammleBestandteile(Service bundle, HashSet<Service> ziel)
        {
            foreach (var bestandteil in bundle.Bestandteile.Select(b => b.Bestandteil!))
            {
                if (ziel.Add(bestandteil) && bestandteil.Typ == ServiceTyp.Bundle)
                {
                    SammleBestandteile(bestandteil, ziel);
                }
            }
        }

        /// <summary>Reihenfolge wie im Katalog: Kategorie, Service, Komponente; Sonderpositionen zuletzt.</summary>
        private void Hinzufuegen(ErgebnisPosition position, Service service, int komponentenSortierung) =>
            _positionen.Add((position, (service.Kategorie?.Sortierung ?? 0, service.Sortierung, komponentenSortierung, _positionen.Count)));
    }
}
