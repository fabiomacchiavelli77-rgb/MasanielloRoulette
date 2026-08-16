using Masaniello.Core.Systems;

namespace Masaniello.Core.Engine;

public enum ModalitaRischio
{
    Prudente,
    Intermedia,
    Aggressiva,
    Ultra,
}

public static class ModalitaRischioExt
{
    /// <summary>Tetto della puntata come frazione della cassa corrente.</summary>
    public static double MaxPct(this ModalitaRischio m) => m switch
    {
        ModalitaRischio.Prudente => 0.03,
        ModalitaRischio.Intermedia => 0.07,
        ModalitaRischio.Aggressiva => 0.15,
        ModalitaRischio.Ultra => 1.0,
        _ => 0.07,
    };

    public static ModalitaRischio Parse(string s) =>
        Enum.TryParse<ModalitaRischio>(s, true, out var m) ? m : ModalitaRischio.Intermedia;
}

public static class StakeCalculator
{
    /// <summary>
    /// Prossima puntata consigliata, sempre multipla della granularità del sistema
    /// (UnitaTotali × chip) così lo split sui segmenti è esatto.
    /// Restituisce 0 quando la sessione è finita: obiettivo raggiunto, colpi esauriti,
    /// obiettivo matematicamente irraggiungibile o banca sotto la puntata minima.
    /// </summary>
    public static decimal Calcola(MasanielloTable tab, BettingSystem sys,
                                  decimal bank, int played, int wins,
                                  decimal chip, double maxPct)
    {
        decimal gran = sys.PuntataMinima(chip);

        if (played >= tab.M || wins >= tab.W) return 0m;
        if (tab.W - wins > tab.M - played) return 0m;
        if (bank < gran) return 0m;

        double frac = tab.StakeFraction(played, wins);
        decimal cap = maxPct >= 1.0 ? bank : Math.Max(gran, bank * (decimal)maxPct);

        decimal stake = Math.Round(bank * (decimal)frac / gran, MidpointRounding.AwayFromZero) * gran;
        if (stake > cap) stake = Math.Floor(cap / gran) * gran;
        if (stake < gran) stake = gran;
        if (stake > bank) stake = Math.Floor(bank / gran) * gran;

        return stake;
    }
}
