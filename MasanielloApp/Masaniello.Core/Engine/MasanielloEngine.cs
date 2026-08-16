namespace Masaniello.Core.Engine;

/// <summary>
/// Tabella del Masaniello classico a quota costante q.
/// V[p, h] = moltiplicatore necessario (target / cassa corrente) nello stato
/// "p colpi giocati, h vittorie ottenute".
/// </summary>
public sealed class MasanielloTable
{
    private readonly double[,] _v;

    public int M { get; }
    public int W { get; }
    public double Q { get; }

    /// <summary>Moltiplicatore obiettivo della sessione (target / cassa iniziale).</summary>
    public double TargetMultiplier => _v[0, 0];

    internal MasanielloTable(int m, int w, double q)
    {
        if (m <= 0 || w <= 0 || w > m) throw new ArgumentException("Servono M > 0 e 0 < W <= M.");
        if (q <= 1.0) throw new ArgumentException("La quota q deve essere > 1.");

        M = m;
        W = w;
        Q = q;
        _v = new double[m + 1, w + 2];

        for (int h = 0; h <= w + 1; h++)
            _v[m, h] = h >= w ? 1.0 : 0.0;

        // suffix[p] = q^(M-p): moltiplicatore quando vanno vinti tutti i colpi rimanenti
        var suffix = new double[m + 1];
        suffix[m] = 1.0;
        for (int p = m - 1; p >= 0; p--)
            suffix[p] = suffix[p + 1] * q;

        for (int p = m - 1; p >= 0; p--)
        {
            for (int h = w; h >= 0; h--)
            {
                int winsNeed = w - h;
                int remColpi = m - p;

                if (winsNeed <= 0)
                {
                    _v[p, h] = 1.0;
                }
                else if (winsNeed > remColpi)
                {
                    _v[p, h] = 0.0; // obiettivo irraggiungibile
                }
                else if (winsNeed == remColpi)
                {
                    _v[p, h] = suffix[p];
                }
                else
                {
                    double a = _v[p + 1, h];     // se il prossimo colpo è perso
                    double b = _v[p + 1, h + 1]; // se il prossimo colpo è vinto
                    double den = a + (q - 1.0) * b;
                    _v[p, h] = den == 0.0 ? 0.0 : q * a * b / den;
                }
            }
            _v[p, w + 1] = 1.0;
        }
    }

    /// <summary>
    /// Frazione di cassa da puntare nello stato (played, wins).
    /// 0 = sessione finita o obiettivo irraggiungibile;
    /// 1 = all-in (vanno vinti tutti i colpi rimanenti).
    /// </summary>
    public double StakeFraction(int played, int wins)
    {
        if (played >= M || wins >= W) return 0.0;
        if (W - wins > M - played) return 0.0;

        double a = _v[played + 1, wins + 1]; // valore se il prossimo colpo è vinto
        double b = _v[played + 1, wins];     // valore se il prossimo colpo è perso

        if (a <= 0.0) return 0.0;
        if (b <= 0.0) return 1.0; // perdere renderebbe l'obiettivo impossibile: all-in

        double den = b + (Q - 1.0) * a;
        double frac = den <= 0.0 ? 1.0 : (b - a) / den;
        return Math.Clamp(frac, 0.0, 1.0);
    }
}

public static class MasanielloEngine
{
    private static readonly Dictionary<(int M, int W, double Q), MasanielloTable> Cache = new();
    private static readonly object Lock = new();

    public static MasanielloTable GetTable(int m, int w, double q)
    {
        lock (Lock)
        {
            if (!Cache.TryGetValue((m, w, q), out var tab))
            {
                tab = new MasanielloTable(m, w, q);
                Cache[(m, w, q)] = tab;
            }
            return tab;
        }
    }
}
