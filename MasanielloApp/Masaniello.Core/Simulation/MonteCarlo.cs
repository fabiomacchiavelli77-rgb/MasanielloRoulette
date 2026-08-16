using Masaniello.Core.Engine;
using Masaniello.Core.Sessions;
using Masaniello.Core.Systems;

namespace Masaniello.Core.Simulation;

public enum McModalita
{
    /// <summary>Ogni sessione riparte dal bankroll iniziale: statistiche indipendenti.</summary>
    Indipendente,
    /// <summary>La cassa della sessione N+1 è il bank finale della sessione N.</summary>
    Rollover,
}

public sealed record McConfig(string Etichetta, BettingSystem Sistema, int M, int W, ModalitaRischio Modalita,
                              GestionePuntata Gestione = GestionePuntata.Masaniello, int K = 10)
{
    /// <summary>Colpi massimi di una sessione (col RecuperoPicco un recupero può andare K colpi oltre M).</summary>
    public int ColpiMassimi => Gestione == GestionePuntata.RecuperoPicco ? M + K : M;
}

public sealed record McSessione(int Indice, bool Vinta, int Colpi, int Vittorie,
                                decimal BankIniziale, decimal Profitto, decimal MaxDrawdown);

public sealed record McStat(string Etichetta, int Sessioni, double PctVinte,
                            decimal ProfittoMedio, decimal ProfittoMax, decimal PerditaMax,
                            decimal DdMedio, decimal DdMax, double RoiMedioPct,
                            decimal? BankFinaleRollover, bool Bancarotta);

public sealed record McRisultato(McConfig Config, McStat Stat, IReadOnlyList<McSessione> Sessioni);

public static class MonteCarloRunner
{
    /// <summary>
    /// Esegue N sessioni per ogni configurazione con numeri casuali. Tutte le
    /// configurazioni rigiocano le STESSE sequenze (common random numbers, un seed
    /// per sessione): le differenze dipendono solo dai parametri, non dalla fortuna.
    /// </summary>
    public static List<McRisultato> Esegui(IReadOnlyList<McConfig> configs, int nSessioni,
                                           McModalita modalita, decimal bank0, decimal chip,
                                           int seed, IProgress<double>? progresso = null)
    {
        if (nSessioni <= 0) throw new ArgumentException("Il numero di sessioni deve essere positivo.");

        var seedSessioni = new int[nSessioni];
        var master = new Random(seed);
        for (int i = 0; i < nSessioni; i++) seedSessioni[i] = master.Next();

        return EseguiInterno(configs, nSessioni, modalita, bank0, chip, progresso,
            i =>
            {
                var rng = new Random(seedSessioni[i]);
                return () => rng.Next(37);
            });
    }

    /// <summary>
    /// Backtest su una permanenza reale, divisa in blocchi fissi della lunghezza
    /// massima richiesta dalle configurazioni: la sessione i-esima di OGNI
    /// configurazione legge gli stessi numeri dal blocco i (confronto equo,
    /// l'analogo dei common random numbers su dati veri).
    /// </summary>
    public static List<McRisultato> EseguiSuPermanenza(IReadOnlyList<McConfig> configs,
                                                       IReadOnlyList<int> numeri, McModalita modalita,
                                                       decimal bank0, decimal chip,
                                                       IProgress<double>? progresso = null)
    {
        int blocco = configs.Max(c => c.ColpiMassimi);
        int nSessioni = numeri.Count / blocco;
        if (nSessioni == 0)
            throw new ArgumentException(
                $"Permanenza troppo corta: servono almeno {blocco} colpi per una sessione.");

        return EseguiInterno(configs, nSessioni, modalita, bank0, chip, progresso,
            i =>
            {
                int pos = i * blocco;
                return () => numeri[pos++];
            });
    }

    /// <summary>Numero di sessioni-blocco che una permanenza fornisce a queste configurazioni.</summary>
    public static int SessioniDaPermanenza(IReadOnlyList<McConfig> configs, int nColpi) =>
        nColpi / configs.Max(c => c.ColpiMassimi);

    private static List<McRisultato> EseguiInterno(IReadOnlyList<McConfig> configs, int nSessioni,
                                                   McModalita modalita, decimal bank0, decimal chip,
                                                   IProgress<double>? progresso,
                                                   Func<int, Func<int>> sorgentePerSessione)
    {
        var risultati = new List<McRisultato>();
        long totale = (long)configs.Count * nSessioni, fatte = 0;

        foreach (var cfg in configs)
        {
            var sessioni = new List<McSessione>(nSessioni);
            decimal bankStart = bank0;
            bool bancarotta = false;

            for (int i = 0; i < nSessioni; i++)
            {
                if (modalita == McModalita.Rollover && bankStart < cfg.Sistema.PuntataMinima(chip))
                {
                    bancarotta = true;
                    break;
                }

                var ses = SimulaSessione(cfg, bankStart, chip, sorgentePerSessione(i), i + 1);
                sessioni.Add(ses);

                if (modalita == McModalita.Rollover) bankStart += ses.Profitto;

                fatte++;
                if (fatte % 500 == 0) progresso?.Report((double)fatte / totale);
            }

            risultati.Add(new McRisultato(cfg, CalcolaStat(cfg.Etichetta, sessioni,
                modalita == McModalita.Rollover ? bankStart : null, bancarotta), sessioni));
        }

        progresso?.Report(1.0);
        return risultati;
    }

    /// <summary>Una sessione completa in memoria, con lo stesso motore delle sessioni reali.</summary>
    public static McSessione SimulaSessione(McConfig cfg, decimal bank0, decimal chip,
                                            Func<int> prossimoNumero, int indice)
    {
        var motore = new MotoreSessione(cfg.Sistema, cfg.Gestione, cfg.M, cfg.W, cfg.K,
                                        bank0, chip, cfg.Modalita.MaxPct());
        decimal peak = bank0, maxDd = 0m;

        while (!motore.Terminata)
        {
            motore.Applica(prossimoNumero());
            if (motore.Bank > peak) peak = motore.Bank;
            if (peak - motore.Bank > maxDd) maxDd = peak - motore.Bank;
        }

        return new McSessione(indice, motore.Vinta, motore.Colpi, motore.Vittorie,
                              bank0, motore.Bank - bank0, maxDd);
    }

    private static McStat CalcolaStat(string etichetta, IReadOnlyList<McSessione> sessioni,
                                      decimal? bankFinaleRollover, bool bancarotta)
    {
        if (sessioni.Count == 0)
            return new McStat(etichetta, 0, 0, 0, 0, 0, 0, 0, 0, bankFinaleRollover, bancarotta);

        int n = sessioni.Count;
        return new McStat(
            Etichetta: etichetta,
            Sessioni: n,
            PctVinte: sessioni.Count(s => s.Vinta) * 100.0 / n,
            ProfittoMedio: Math.Round(sessioni.Sum(s => s.Profitto) / n, 2),
            ProfittoMax: sessioni.Max(s => s.Profitto),
            PerditaMax: sessioni.Min(s => s.Profitto),
            DdMedio: Math.Round(sessioni.Sum(s => s.MaxDrawdown) / n, 2),
            DdMax: sessioni.Max(s => s.MaxDrawdown),
            RoiMedioPct: sessioni.Average(s => (double)(s.Profitto / s.BankIniziale)) * 100.0,
            BankFinaleRollover: bankFinaleRollover,
            Bancarotta: bancarotta);
    }
}
