namespace Masaniello.Core.Systems;

/// <summary>I sistemi di puntata predefiniti.</summary>
public static class Catalog
{
    public const string CodiceDozzineSestina = "DOZZINE_SESTINA";
    public const string CodiceQuasiTutto = "QUASI_TUTTO";
    public const string CodicePiuPieno = "PIU_PIENO";
    public const string CodicePiuPieni = "PIU_PIENI";

    /// <summary>
    /// Piani con copertura oltre i 35 numeri: il ritorno non supera più la puntata
    /// (36/37 netto 0, 37/37 perdita certa) — non sono sistemi, non esistono qui.
    /// Il BettingSystem li rifiuta già ("Sistema senza vincita").
    /// </summary>
    public const string NotaPianiImpossibili =
        "Oltre 35 numeri coperti non c'è vincita: 36/37 restituisce esattamente la puntata " +
        "(scudo a netto 0), 37/37 perde 1 unità a colpo con certezza. La copertura massima " +
        "con profitto è 35/37.";

    private static IReadOnlyList<int> Intervallo(int da, int quanti) =>
        Enumerable.Range(da, quanti).ToList();

    private static int TerzinaValida(int terzinaStart)
    {
        if (terzinaStart != 31 && terzinaStart != 34)
            throw new ArgumentException("La terzina deve iniziare a 31 o 34 (una fila della sestina 31-36).");
        return terzinaStart;
    }

    private static int PienoValido(int pieno, int terzinaStart)
    {
        if (pieno < 31 || pieno > 36)
            throw new ArgumentException($"Il pieno {pieno} non è un residuo della terza dozzina (31-36).");
        if (pieno >= terzinaStart && pieno <= terzinaStart + 2)
            throw new ArgumentException($"Il pieno {pieno} è già coperto dalla terzina {terzinaStart}-{terzinaStart + 2}.");
        return pieno;
    }

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

    /// <summary>
    /// Sistema 3 – "Quasi tutto + 1 pieno": 2 dozzine + sestina 25-30 + terzina
    /// + un pieno sui residui della terza dozzina. 34/37 coperti (91,9%),
    /// unità 12/12/6/3/1 (totale 34), ogni vincita +5,88% (q = 36/34).
    /// Scoperti: lo 0 e i 2 residui non puntati.
    /// </summary>
    public static BettingSystem PiuPieno(int terzinaStart = 31, int pieno = 34)
    {
        int t = TerzinaValida(terzinaStart);
        int p = PienoValido(pieno, terzinaStart);

        return new BettingSystem(CodicePiuPieno, $"Quasi tutto + pieno {p} (34 numeri)", new[]
        {
            new BetSegment("Dozzina 1 (1-12)", Intervallo(1, 12), 3, 12),
            new BetSegment("Dozzina 2 (13-24)", Intervallo(13, 12), 3, 12),
            new BetSegment("Sestina 25-30", Intervallo(25, 6), 6, 6),
            new BetSegment($"Terzina {t}-{t + 2}", Intervallo(t, 3), 12, 3),
            new BetSegment($"Pieno {p}", new[] { p }, 36, 1),
        });
    }

    /// <summary>
    /// Sistema 4 – "Quasi tutto + 2 pieni": come S3 con due pieni sui residui.
    /// 35/37 coperti (94,6%), unità 12/12/6/3/1/1 (totale 35), ogni vincita
    /// +2,86% (q = 36/35). Con i pieni 34+35 restano scoperti solo 0 e 36.
    /// È la copertura massima con profitto: oltre, il ritorno non batte la puntata.
    /// </summary>
    public static BettingSystem PiuPieni(int terzinaStart = 31, int pieno1 = 34, int pieno2 = 35)
    {
        int t = TerzinaValida(terzinaStart);
        int p1 = PienoValido(pieno1, terzinaStart);
        int p2 = PienoValido(pieno2, terzinaStart);
        if (p1 == p2) throw new ArgumentException("I due pieni devono essere numeri diversi.");

        return new BettingSystem(CodicePiuPieni, $"Quasi tutto + pieni {p1}+{p2} (35 numeri)", new[]
        {
            new BetSegment("Dozzina 1 (1-12)", Intervallo(1, 12), 3, 12),
            new BetSegment("Dozzina 2 (13-24)", Intervallo(13, 12), 3, 12),
            new BetSegment("Sestina 25-30", Intervallo(25, 6), 6, 6),
            new BetSegment($"Terzina {t}-{t + 2}", Intervallo(t, 3), 12, 3),
            new BetSegment($"Pieno {p1}", new[] { p1 }, 36, 1),
            new BetSegment($"Pieno {p2}", new[] { p2 }, 36, 1),
        });
    }

    /// <summary>Ricostruisce un sistema dal codice salvato nel DB e dal parametro di posizione.</summary>
    public static BettingSystem Crea(string codice, int parametro) => codice switch
    {
        CodiceDozzineSestina => DozzineSestina(parametro),
        CodiceQuasiTutto => QuasiTutto(parametro),
        CodicePiuPieno => PiuPieno((parametro / 10000) % 100, parametro / 100 % 100),
        CodicePiuPieni => PiuPieni((parametro / 10000) % 100, parametro / 100 % 100, parametro % 100),
        _ => throw new ArgumentException($"Sistema sconosciuto: {codice}"),
    };

    /// <summary>Codifica terzina + pieni nel parametro unico del DB (t*10000 + p1*100 + p2).</summary>
    public static int CodificaParametro(int terzinaStart, int pieno1, int pieno2 = 0) =>
        terzinaStart * 10000 + pieno1 * 100 + pieno2;
}
