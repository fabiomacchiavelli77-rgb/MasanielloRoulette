namespace Masaniello.Core.Engine;

public sealed record ScommessaEsatta(string Tag, int M, int W, double DevPp);

/// <summary>
/// La "scommessa più esatta" per il Masaniello: la coppia W/M più vicina alla
/// probabilità di colpo p = numeriCoperti/37. Quando W/M = p le puntate della
/// tabella restano quasi costanti colpo dopo colpo; quando W/M sta sotto p il
/// profilo è aggressivo (serve fortuna), sopra p è conservativo.
/// </summary>
public static class ScommesseEsatte
{
    /// <summary>Migliori W/M per il sistema indicato: corta (M ≤ 12), media (M ≤ 30), esatta (W/M = coperti/37, M = 37).</summary>
    public static IReadOnlyList<ScommessaEsatta> Suggerite(int numeriCoperti)
    {
        double p = numeriCoperti / 37.0;

        ScommessaEsatta? Migliore(int maxM, string tag)
        {
            double migliorDev = double.PositiveInfinity;
            int migliorW = 0, migliorM = 0;
            for (int m = 1; m <= maxM; m++)
                for (int w = 1; w <= m; w++)
                {
                    double dev = Math.Abs(w / (double)m - p);
                    if (dev < migliorDev - 1e-15)
                    {
                        migliorDev = dev;
                        migliorW = w;
                        migliorM = m;
                    }
                }
            return migliorM == 0 ? null : new ScommessaEsatta(tag, migliorM, migliorW, migliorDev * 100.0);
        }

        var lista = new List<ScommessaEsatta>();
        var corta = Migliore(12, "corta");
        var media = Migliore(30, "media");
        if (corta != null) lista.Add(corta);
        if (media != null) lista.Add(media);
        lista.Add(new ScommessaEsatta("esatta", 37, numeriCoperti, 0.0));
        return lista;
    }
}
