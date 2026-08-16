using Masaniello.Core.Systems;

namespace Masaniello.Tests;

public class SystemTests
{
    [Fact]
    public void DozzineSestina_QuotaEUnita()
    {
        var sys = Catalog.DozzineSestina(25);
        Assert.Equal(5, sys.UnitaTotali);
        Assert.Equal(6, sys.RitornoUnita);
        Assert.Equal(1.2, sys.Q, 10);
        Assert.Equal(30, sys.NumeriCoperti);
    }

    [Fact]
    public void QuasiTutto_QuotaEUnita()
    {
        var sys = Catalog.QuasiTutto(31);
        Assert.Equal(11, sys.UnitaTotali);
        Assert.Equal(12, sys.RitornoUnita);
        Assert.Equal(12.0 / 11.0, sys.Q, 10);
        Assert.Equal(33, sys.NumeriCoperti);
        Assert.Equal(new[] { 0, 34, 35, 36 }, sys.NumeriScoperti);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(31)]
    public void DozzineSestina_ProfittoRealePerOgniNumero(int sestina)
    {
        var sys = Catalog.DozzineSestina(sestina);
        decimal puntata = 5m; // con chip 0,10€: 50 chip = 10 × granularità (0,50€)

        for (int n = 0; n <= 36; n++)
        {
            decimal atteso;
            if (n >= 1 && n <= 24) atteso = puntata / 5m;                       // dozzina: 2 unità × 3 = 6/5
            else if (n >= sestina && n <= sestina + 5) atteso = puntata / 5m;   // sestina: 1 unità × 6 = 6/5
            else atteso = -puntata;                                             // 0 e sestina opposta

            Assert.Equal(atteso, sys.Profitto(n, puntata));
            Assert.Equal(atteso > 0, sys.Copre(n));
        }
    }

    [Theory]
    [InlineData(31)]
    [InlineData(34)]
    public void QuasiTutto_ProfittoRealePerOgniNumero(int terzina)
    {
        var sys = Catalog.QuasiTutto(terzina);
        decimal puntata = 11m; // 1 unità = 1€

        for (int n = 0; n <= 36; n++)
        {
            bool coperto = (n >= 1 && n <= 30) || (n >= terzina && n <= terzina + 2);
            decimal atteso = coperto ? 1m : -11m; // incasso sempre 12 → +1; scoperto → -11

            Assert.Equal(atteso, sys.Profitto(n, puntata));
            Assert.Equal(coperto, sys.Copre(n));
        }
    }

    [Fact]
    public void Breakdown_SommaEsattamenteLaPuntata()
    {
        var sys = Catalog.QuasiTutto(31);
        decimal puntata = 33m; // 3 × granularità con chip 1€
        var br = sys.Breakdown(puntata);
        Assert.Equal(4, br.Count);
        Assert.Equal(puntata, br.Sum(b => b.Importo));
        Assert.Equal(12m, br[0].Importo); // dozzina 1: 4/11
        Assert.Equal(12m, br[1].Importo); // dozzina 2: 4/11
        Assert.Equal(6m, br[2].Importo);  // sestina: 2/11
        Assert.Equal(3m, br[3].Importo);  // terzina: 1/11
    }

    [Fact]
    public void SistemaConRitornoNonCostante_VieneRifiutato()
    {
        Assert.Throws<ArgumentException>(() => new BettingSystem("X", "X", new[]
        {
            new BetSegment("Dozzina", Enumerable.Range(1, 12).ToList(), 3, 2),
            new BetSegment("Sestina", Enumerable.Range(25, 6).ToList(), 6, 2), // 12 ≠ 6
        }));
    }

    [Fact]
    public void PuntataMinima_DipendeDalGettonePiuPiccolo()
    {
        // la granularità del sistema: 5 gettoni per S1, 11 per S2
        Assert.Equal(0.5m, Catalog.DozzineSestina().PuntataMinima(0.1m));
        Assert.Equal(1.1m, Catalog.QuasiTutto().PuntataMinima(0.1m));
        Assert.Equal(5m, Catalog.DozzineSestina().PuntataMinima(1m));
        Assert.Equal(11m, Catalog.QuasiTutto().PuntataMinima(1m));
    }
}
