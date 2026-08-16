using Masaniello.Core.Engine;
using Masaniello.Core.Systems;

namespace Masaniello.Core.Sessions;

public enum GestionePuntata
{
    /// <summary>Masaniello classico: piano M/W, puntata dalla tabella V.</summary>
    Masaniello,
    /// <summary>Puntata minima finché si vince; dopo una perdita un mini-piano
    /// Masaniello (max K colpi) riporta la cassa al massimo precedente.</summary>
    RecuperoPicco,
}

public static class GestionePuntataExt
{
    public const string CodiceMasaniello = "MASANIELLO";
    public const string CodiceRecuperoPicco = "RECUPERO_PICCO";

    public static string Codice(this GestionePuntata g) =>
        g == GestionePuntata.RecuperoPicco ? CodiceRecuperoPicco : CodiceMasaniello;

    public static GestionePuntata Parse(string? s) =>
        s == CodiceRecuperoPicco ? GestionePuntata.RecuperoPicco : GestionePuntata.Masaniello;
}

public sealed record RisultatoColpo(decimal Puntata, bool Vinto, decimal Profitto);

/// <summary>
/// Macchina a stati di una sessione, senza persistenza: la usano sia la sessione
/// live (SessionService) sia le simulazioni (MonteCarloRunner), così la logica di
/// puntata è scritta una volta sola.
///
/// Gestione RecuperoPicco: si punta sempre la puntata minima del sistema; quando una
/// perdita porta la cassa sotto il picco, un mini-piano Masaniello (M'=K, W' minimo
/// che raggiunge picco/cassa) lavora per recuperarla. Recupero riuscito (cassa ≥ picco
/// o W' vittorie) → si torna al minimo; piano morto o cassa insufficiente → PERSA.
/// La sessione chiude al colpo M (un recupero in corso si completa, max K colpi oltre)
/// con esito VINTA se la cassa è almeno quella iniziale.
/// </summary>
public sealed class MotoreSessione
{
    public BettingSystem Sistema { get; }
    public GestionePuntata Gestione { get; }
    public int M { get; }
    /// <summary>Vittorie obiettivo (0 con la gestione RecuperoPicco).</summary>
    public int W { get; }
    /// <summary>Colpi massimi di un recupero (0 con la gestione Masaniello).</summary>
    public int K { get; }
    public decimal Chip { get; }
    public double MaxPct { get; }

    /// <summary>Tabella del piano classico (null con la gestione RecuperoPicco).</summary>
    public MasanielloTable? Tabella { get; }

    public decimal BankIniziale { get; }
    public decimal Bank { get; private set; }
    public int Colpi { get; private set; }
    public int Vittorie { get; private set; }
    public decimal Picco { get; private set; }
    public bool InRecupero => _pianoRecupero != null;
    public MasanielloTable? PianoRecuperoCorrente => _pianoRecupero;
    public int RecuperoColpi => _recColpi;
    public int RecuperoVittorie => _recVittorie;
    public decimal ProssimaPuntata { get; private set; }
    public bool Terminata { get; private set; }
    public bool Vinta { get; private set; }

    private MasanielloTable? _pianoRecupero;
    private int _recColpi, _recVittorie;

    public MotoreSessione(BettingSystem sistema, GestionePuntata gestione, int m, int w, int k,
                          decimal bank0, decimal chip, double maxPct)
    {
        if (m <= 0)
            throw new ArgumentException("Servono colpi totali M > 0.");
        if (gestione == GestionePuntata.Masaniello && (w <= 0 || w > m))
            throw new ArgumentException("Parametri non validi: servono M > 0 e 0 < W <= M.");
        if (gestione == GestionePuntata.RecuperoPicco && k <= 0)
            throw new ArgumentException("Servono colpi di recupero K > 0.");
        if (chip <= 0)
            throw new ArgumentException("Il valore del chip deve essere positivo.");

        Sistema = sistema;
        Gestione = gestione;
        M = m;
        W = gestione == GestionePuntata.Masaniello ? w : 0;
        K = gestione == GestionePuntata.RecuperoPicco ? k : 0;
        Chip = chip;
        MaxPct = maxPct;
        BankIniziale = Bank = Picco = bank0;
        if (gestione == GestionePuntata.Masaniello)
            Tabella = MasanielloEngine.GetTable(m, w, sistema.Q);

        ProssimaPuntata = CalcolaPuntata();
        if (ProssimaPuntata <= 0) Termina(); // cassa già sotto la puntata minima
    }

    public RisultatoColpo Applica(int numero)
    {
        if (Terminata || ProssimaPuntata <= 0)
            throw new InvalidOperationException("Sessione terminata: nessuna puntata da giocare.");
        if (numero < 0 || numero > 36)
            throw new ArgumentException("Il numero deve essere tra 0 e 36.");

        decimal puntata = ProssimaPuntata;
        bool vinto = Sistema.Copre(numero);
        decimal profitto = Sistema.Profitto(numero, puntata);
        Bank += profitto;
        Colpi++;
        if (vinto) Vittorie++;

        if (Gestione == GestionePuntata.Masaniello)
        {
            ProssimaPuntata = CalcolaPuntata();
            if (ProssimaPuntata <= 0)
            {
                Terminata = true;
                Vinta = Vittorie >= W;
            }
        }
        else
        {
            AvanzaRecuperoPicco(vinto);
        }

        return new RisultatoColpo(puntata, vinto, profitto);
    }

    private void AvanzaRecuperoPicco(bool vinto)
    {
        if (_pianoRecupero != null)
        {
            _recColpi++;
            if (vinto) _recVittorie++;

            // recupero riuscito: cassa tornata al picco o piano completato
            if (Bank >= Picco || _recVittorie >= _pianoRecupero.W)
            {
                _pianoRecupero = null;
                if (Bank > Picco) Picco = Bank;
            }
        }
        else
        {
            if (Bank > Picco) Picco = Bank;
            if (!vinto)
            {
                // perdita al minimo: si entra in recupero per tornare al picco
                if (Bank < Sistema.PuntataMinima(Chip) ||
                    PianoRecupero.TrovaW(Sistema.Q, K, (double)(Picco / Bank)) is not int w)
                {
                    Termina();
                    return;
                }
                _pianoRecupero = MasanielloEngine.GetTable(K, w, Sistema.Q);
                _recColpi = 0;
                _recVittorie = 0;
            }
        }

        ProssimaPuntata = CalcolaPuntata();
        if (ProssimaPuntata <= 0) Termina();
    }

    private decimal CalcolaPuntata()
    {
        if (Gestione == GestionePuntata.Masaniello)
            return StakeCalculator.Calcola(Tabella!, Sistema, Bank, Colpi, Vittorie, Chip, MaxPct);

        if (_pianoRecupero != null)
            return StakeCalculator.Calcola(_pianoRecupero, Sistema, Bank, _recColpi, _recVittorie, Chip, MaxPct);

        // stato normale: puntata minima finché restano colpi e cassa
        if (Colpi >= M || Bank < Sistema.PuntataMinima(Chip)) return 0m;
        return Sistema.PuntataMinima(Chip);
    }

    private void Termina()
    {
        Terminata = true;
        ProssimaPuntata = 0m;
        // un recupero non chiuso è una sconfitta: la disciplina del piano è fallita
        Vinta = !InRecupero && Colpi > 0 && Bank >= BankIniziale;
    }
}
