using Masaniello.Core.Engine;
using Masaniello.Core.Systems;

namespace Masaniello.Tests;

public class EngineTests
{
    /// <summary>P(X >= k) con X ~ Binomiale(n, p), calcolata in modo indipendente dalla tabella V.</summary>
    private static double TailBinomiale(int n, int k, double p)
    {
        double pr = Math.Pow(1 - p, n); // P(X = 0)
        for (int i = 0; i < k; i++)
            pr = pr * (n - i) / (i + 1) * p / (1 - p);
        double tot = pr;
        for (int i = k + 1; i <= n; i++)
        {
            pr = pr * (n - i + 1.0) / i * p / (1 - p);
            tot += pr;
        }
        return tot;
    }

    [Fact]
    public void M1W1_RichiedeAllIn()
    {
        var tab = MasanielloEngine.GetTable(1, 1, 1.2);
        Assert.Equal(1.0, tab.StakeFraction(0, 0)); // unico colpo da vincere: tutto
        Assert.Equal(1.2, tab.TargetMultiplier, 10);
    }

    [Fact]
    public void M2W1_FrazioneEsatta()
    {
        // caso verificato a mano: V(0,0) = 1,44/1,4; frazione = 0,2/1,4 = 1/7
        var tab = MasanielloEngine.GetTable(2, 1, 1.2);
        Assert.Equal(1.0 / 7.0, tab.StakeFraction(0, 0), 10);
        Assert.Equal(1.44 / 1.4, tab.TargetMultiplier, 10);
        // dopo una sconfitta va vinto l'unico colpo rimasto: all-in
        Assert.Equal(1.0, tab.StakeFraction(1, 0));
        // dopo una vittoria l'obiettivo è raggiunto
        Assert.Equal(0.0, tab.StakeFraction(1, 1));
    }

    [Fact]
    public void CoerenzaMasaniello_OgniPercorsoVincenteArrivaAlloStessoTarget()
    {
        // proprietà fondamentale del Masaniello: senza cap e senza arrotondamenti,
        // qualunque sequenza di W/L che raggiunge W vittorie entro M colpi termina
        // ESATTAMENTE al target, indipendentemente dall'ordine di vittorie e sconfitte
        var tab = MasanielloEngine.GetTable(20, 13, 1.2);
        double target = 100.0 * tab.TargetMultiplier;

        var rng = new Random(42);
        int percorsiVincenti = 0;
        for (int tentativo = 0; tentativo < 200; tentativo++)
        {
            double bank = 100.0;
            int played = 0, wins = 0;
            while (played < tab.M && wins < tab.W && tab.W - wins <= tab.M - played)
            {
                double stake = bank * tab.StakeFraction(played, wins);
                if (rng.NextDouble() < 0.81) { bank += stake * (tab.Q - 1.0); wins++; }
                else { bank -= stake; }
                played++;
            }
            if (wins >= tab.W)
            {
                percorsiVincenti++;
                Assert.Equal(target, bank, 6);
            }
        }
        Assert.True(percorsiVincenti > 100); // il test deve aver coperto molti percorsi diversi
    }

    [Fact]
    public void StatoMorto_NessunaPuntata()
    {
        var tab = MasanielloEngine.GetTable(10, 5, 1.2);
        // 4 colpi rimasti, 5 vittorie necessarie: impossibile
        Assert.Equal(0.0, tab.StakeFraction(6, 0));
    }

    [Theory]
    [InlineData(13, 1.14, 0.01)]
    [InlineData(14, 3.86, 0.08)]
    [InlineData(15, 11.34, 0.47)]
    [InlineData(16, 30.08, 2.20)]
    [InlineData(17, 76.51, 8.65)]
    [InlineData(18, 204.27, 29.86)]
    public void Target20Colpi_VerificatoConFormulaIndipendenteERefertorio(int w, double pctS1, double pctS2)
    {
        // doppia verifica: la tabella V deve coincidere con la replica risk-neutral
        // 1/P*(X >= W), con p* = 1/q, e con i valori documentati del progetto
        var s1 = MasanielloEngine.GetTable(20, w, 6.0 / 5.0);
        var s2 = MasanielloEngine.GetTable(20, w, 12.0 / 11.0);

        Assert.Equal(1.0 / TailBinomiale(20, w, 1.0 / s1.Q), s1.TargetMultiplier, 8);
        Assert.Equal(1.0 / TailBinomiale(20, w, 1.0 / s2.Q), s2.TargetMultiplier, 8);
        Assert.Equal(pctS1, Math.Round((s1.TargetMultiplier - 1) * 100, 2));
        Assert.Equal(pctS2, Math.Round((s2.TargetMultiplier - 1) * 100, 2));
    }

    [Fact]
    public void Calcola_ArrotondaAllaGranularita()
    {
        var sys = Catalog.DozzineSestina();
        var tab = MasanielloEngine.GetTable(20, 13, sys.Q);
        decimal chip = 0.1m; // granularità 0,50€

        decimal stake = StakeCalculator.Calcola(tab, sys, 100m, 0, 0, chip, 1.0);
        Assert.True(stake > 0);
        Assert.Equal(0m, stake % 0.5m);
    }

    [Fact]
    public void Calcola_RispettaIlCap()
    {
        var sys = Catalog.DozzineSestina();
        var tab = MasanielloEngine.GetTable(20, 13, sys.Q);

        decimal libera = StakeCalculator.Calcola(tab, sys, 1000m, 0, 0, 1m, 1.0);
        decimal prudente = StakeCalculator.Calcola(tab, sys, 1000m, 0, 0, 1m, 0.03);
        Assert.True(prudente <= 30m);
        Assert.True(prudente <= libera);
    }

    [Fact]
    public void Calcola_ZeroQuandoSessioneFinita()
    {
        var sys = Catalog.DozzineSestina();
        var tab = MasanielloEngine.GetTable(20, 13, sys.Q);

        Assert.Equal(0m, StakeCalculator.Calcola(tab, sys, 100m, 20, 10, 1m, 1.0)); // colpi finiti
        Assert.Equal(0m, StakeCalculator.Calcola(tab, sys, 100m, 10, 13, 1m, 1.0)); // obiettivo raggiunto
        Assert.Equal(0m, StakeCalculator.Calcola(tab, sys, 100m, 10, 2, 1m, 1.0));  // 11 vittorie in 10 colpi: morto
        Assert.Equal(0m, StakeCalculator.Calcola(tab, sys, 3m, 0, 0, 1m, 1.0));     // banca < 5 chip
    }

    [Fact]
    public void Calcola_AllInQuandoServonoTutteLeVittorie()
    {
        var sys = Catalog.DozzineSestina();
        var tab = MasanielloEngine.GetTable(20, 13, sys.Q);
        // 13 colpi rimasti, 13 vittorie necessarie → all-in (modalità Ultra)
        decimal stake = StakeCalculator.Calcola(tab, sys, 100m, 7, 0, 1m, 1.0);
        Assert.Equal(100m, stake);
    }
}
