namespace Masaniello.Core.Systems;

/// <summary>I sistemi di puntata predefiniti.</summary>
public static class Catalog
{
    public const string CodiceDozzineSestina = "DOZZINE_SESTINA";
    public const string CodiceQuasiTutto = "QUASI_TUTTO";

    private static IReadOnlyList<int> Intervallo(int da, int quanti) =>
        Enumerable.Range(da, quanti).ToList();

    /// <summary>
    /// Sistema 1 – "Dozzine + Sestina": 1-24 più una sestina (25-30 o 31-36).
    /// 30/37 numeri coperti (81,1%), unità 2/2/1, ogni vincita +20% (q = 6/5).
    /// </summary>
    public static BettingSystem DozzineSestina(int sestinaStart = 25)
    {
        if (sestinaStart != 25 && sestinaStart != 31)
            throw new ArgumentException("La sestina deve iniziare a 25 o 31.");

        return new BettingSystem(CodiceDozzineSestina, "Dozzine + Sestina", new[]
        {
            new BetSegment("Dozzina 1 (1-12)", Intervallo(1, 12), 3, 2),
            new BetSegment("Dozzina 2 (13-24)", Intervallo(13, 12), 3, 2),
            new BetSegment($"Sestina {sestinaStart}-{sestinaStart + 5}", Intervallo(sestinaStart, 6), 6, 1),
        });
    }

    /// <summary>
    /// Sistema 2 – "Quasi tutto": 2 dozzine (1-24) + sestina (25-30) + una terzina
    /// dell'ultima sestina. 33/37 numeri coperti (89,2%), unità 4/4/2/1,
    /// ogni vincita +9,09% (q = 12/11). Scoperti: lo 0 e i 3 numeri rimanenti.
    /// </summary>
    public static BettingSystem QuasiTutto(int terzinaStart = 31)
    {
        if (terzinaStart != 31 && terzinaStart != 34)
            throw new ArgumentException("La terzina deve iniziare a 31 o 34 (una fila della sestina 31-36).");

        return new BettingSystem(CodiceQuasiTutto, "Quasi tutto (33 numeri)", new[]
        {
            new BetSegment("Dozzina 1 (1-12)", Intervallo(1, 12), 3, 4),
            new BetSegment("Dozzina 2 (13-24)", Intervallo(13, 12), 3, 4),
            new BetSegment("Sestina 25-30", Intervallo(25, 6), 6, 2),
            new BetSegment($"Terzina {terzinaStart}-{terzinaStart + 2}", Intervallo(terzinaStart, 3), 12, 1),
        });
    }

    /// <summary>Ricostruisce un sistema dal codice salvato nel DB e dal parametro di posizione.</summary>
    public static BettingSystem Crea(string codice, int parametro) => codice switch
    {
        CodiceDozzineSestina => DozzineSestina(parametro),
        CodiceQuasiTutto => QuasiTutto(parametro),
        _ => throw new ArgumentException($"Sistema sconosciuto: {codice}"),
    };
}
