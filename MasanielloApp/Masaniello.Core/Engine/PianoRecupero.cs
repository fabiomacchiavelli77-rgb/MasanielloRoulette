namespace Masaniello.Core.Engine;

public static class PianoRecupero
{
    /// <summary>
    /// W minimo per cui un piano Masaniello di k colpi a quota q raggiunge il
    /// moltiplicatore target; null se nemmeno vincendo tutti i k colpi (q^k &lt; target).
    /// </summary>
    public static int? TrovaW(double q, int k, double target)
    {
        for (int w = 1; w <= k; w++)
            if (MasanielloEngine.GetTable(k, w, q).TargetMultiplier >= target - 1e-12)
                return w;
        return null;
    }
}
