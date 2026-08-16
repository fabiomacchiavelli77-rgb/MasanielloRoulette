using Masaniello.Core.Data;
using Masaniello.Core.Engine;
using Masaniello.Core.Systems;

namespace Masaniello.Core.Sessions;

public sealed record NuovaSessioneConfig(
    string SistemaCodice, int Parametro, int M, int W,
    ModalitaRischio Modalita, decimal Chip, decimal BankConfigurato, bool Rollover,
    GestionePuntata Gestione = GestionePuntata.Masaniello, int K = 10);

public sealed record EsitoColpo(
    int Numero, string Esito, decimal Puntata, decimal Profitto,
    decimal BankDopo, string StatoSessione, decimal ProssimaPuntata);

/// <summary>Sessione attiva: il motore tiene lo stato, il record la riga del DB.</summary>
public sealed class SessioneAttiva
{
    public required SessioneRecord Record { get; set; }
    public required MotoreSessione Motore { get; init; }

    public BettingSystem Sistema => Motore.Sistema;
    public MasanielloTable? Tabella => Motore.Tabella;
    public decimal Bank => Motore.Bank;
    public int Colpi => Motore.Colpi;
    public int Vittorie => Motore.Vittorie;
    public decimal ProssimaPuntata => Motore.ProssimaPuntata;
}

public sealed class SessionService
{
    public const string FonteManuale = "MANUALE";
    public const string FonteRandom = "RANDOM";
    public const string FontePermanenza = "PERMANENZA";

    private readonly Database _db;
    private readonly Random _rng;

    public SessioneAttiva? Corrente { get; private set; }

    public SessionService(Database db, int? seedRng = null)
    {
        _db = db;
        _rng = seedRng.HasValue ? new Random(seedRng.Value) : new Random();
        RiprendiSessioneInCorso();
    }

    /// <summary>
    /// Riprende dall'avvio l'eventuale sessione IN_CORSO salvata nel DB: i colpi
    /// registrati vengono rigiocati nel motore, così anche lo stato interno della
    /// gestione RecuperoPicco (picco, mini-piano) si ricostruisce esattamente.
    /// </summary>
    private void RiprendiSessioneInCorso()
    {
        var rec = _db.SessioneInCorso();
        if (rec == null) return;

        var sistema = Catalog.Crea(rec.Sistema, rec.Parametro);
        var motore = new MotoreSessione(
            sistema, GestionePuntataExt.Parse(rec.Gestione), rec.M, rec.W, rec.KRecupero,
            rec.BankIniziale, rec.Chip, ModalitaRischioExt.Parse(rec.ModalitaRischio).MaxPct());

        foreach (var c in _db.ColpiDiSessione(rec.Id))
            motore.Applica(c.Numero);

        if (motore.Terminata)
        {
            // DB incoerente (sessione finita ma mai chiusa): la si chiude ora
            _db.ChiudiSessione(rec.Id, motore.Vinta ? Database.StatoVinta : Database.StatoPersa, motore.Bank);
            return;
        }
        Corrente = new SessioneAttiva { Record = rec, Motore = motore };
    }

    /// <summary>
    /// Avvia una nuova sessione. Con il rollover attivo la cassa riparte dal bank
    /// finale dell'ultima sessione chiusa; un'eventuale sessione ancora aperta viene
    /// archiviata come INTERROTTA.
    /// </summary>
    public SessioneAttiva NuovaSessione(NuovaSessioneConfig cfg)
    {
        if (cfg.M <= 0)
            throw new ArgumentException("Servono colpi totali M > 0.");
        if (cfg.Gestione == GestionePuntata.Masaniello && (cfg.W <= 0 || cfg.W > cfg.M))
            throw new ArgumentException("Parametri non validi: servono M > 0 e 0 < W <= M.");
        if (cfg.Gestione == GestionePuntata.RecuperoPicco && cfg.K <= 0)
            throw new ArgumentException("Servono colpi di recupero K > 0.");
        if (cfg.Chip <= 0)
            throw new ArgumentException("Il valore del chip deve essere positivo.");

        if (Corrente != null)
        {
            _db.ChiudiSessione(Corrente.Record.Id, Database.StatoInterrotta, Corrente.Bank);
            Corrente = null;
        }

        var sistema = Catalog.Crea(cfg.SistemaCodice, cfg.Parametro);

        decimal bank0 = cfg.BankConfigurato;
        if (cfg.Rollover && _db.UltimoBankFinale() is decimal ultimo && ultimo > 0)
            bank0 = ultimo;

        if (bank0 < sistema.PuntataMinima(cfg.Chip))
            throw new ArgumentException(
                $"Bankroll insufficiente: servono almeno {sistema.PuntataMinima(cfg.Chip):C} " +
                $"({sistema.UnitaTotali} chip) per coprire tutti i segmenti.");

        var motore = new MotoreSessione(sistema, cfg.Gestione, cfg.M, cfg.W, cfg.K,
                                        bank0, cfg.Chip, cfg.Modalita.MaxPct());

        _db.InserisciSessione(cfg.SistemaCodice, cfg.Parametro, cfg.M, motore.W, sistema.Q,
                              cfg.Modalita.ToString(), cfg.Chip, bank0,
                              cfg.Gestione.Codice(), motore.K);

        Corrente = new SessioneAttiva { Record = _db.SessioneInCorso()!, Motore = motore };
        return Corrente;
    }

    public EsitoColpo ProcessaNumero(int numero, string fonte)
    {
        var s = Corrente ?? throw new InvalidOperationException("Nessuna sessione in corso.");

        var r = s.Motore.Applica(numero);
        string esito = r.Vinto ? "W" : "L";
        string split = string.Join(" | ",
            s.Sistema.Breakdown(r.Puntata).Select(b => $"{b.Nome}: {b.Importo:0.00}"));

        _db.InserisciColpo(s.Record.Id, s.Colpi, numero, esito, r.Puntata, split,
                           s.Bank - r.Profitto, s.Bank, r.Profitto, fonte);

        string stato;
        if (s.Motore.Terminata)
        {
            stato = s.Motore.Vinta ? Database.StatoVinta : Database.StatoPersa;
            _db.ChiudiSessione(s.Record.Id, stato, s.Bank);
            Corrente = null;
        }
        else
        {
            stato = Database.StatoInCorso;
        }

        return new EsitoColpo(numero, esito, r.Puntata, r.Profitto, s.Bank, stato,
                              stato == Database.StatoInCorso ? s.ProssimaPuntata : 0m);
    }

    public EsitoColpo SimulaRandom() => ProcessaNumero(_rng.Next(37), FonteRandom);

    /// <summary>Gioca il prossimo numero della permanenza e ne avanza il cursore.</summary>
    public EsitoColpo GiocaColpoPermanenza(long permanenzaId)
    {
        var perm = _db.Permanenza(permanenzaId)
            ?? throw new ArgumentException("Permanenza non trovata.");
        if (perm.Cursore >= perm.NColpi)
            throw new InvalidOperationException(
                "Permanenza esaurita: riavvolgi il cursore per rigiocarla dall'inizio.");

        var numeri = _db.NumeriPermanenza(permanenzaId);
        var esito = ProcessaNumero(numeri[perm.Cursore], FontePermanenza);
        _db.AggiornaCursorePermanenza(permanenzaId, perm.Cursore + 1);
        return esito;
    }
}
