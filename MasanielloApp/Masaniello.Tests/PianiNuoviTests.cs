using Masaniello.Core.Engine;
using Masaniello.Core.Systems;

namespace Masaniello.Tests;

public class PianiNuoviTests
{
    [Fact]
    public void PiuPieno_QuotaEUnita()
    {
        var sys = Catalog.PiuPieno(31, 34);
        Assert.Equal(34, sys.UnitaTotali);
        Assert.Equal(36, sys.RitornoUnita);
        Assert.Equal(36.0 / 34.0, sys.Q, 10);
        Assert.Equal(34, sys.NumeriCoperti);
        Assert.Equal(new[] { 0, 35, 36 }, sys.NumeriScoperti);
    }

    [Fact]
    public void PiuPieni_QuotaEUnita_ScopertiSoloZeroE36()
    {
        var sys = Catalog.PiuPieni(31, 34, 35);
        Assert.Equal(35, sys.UnitaTotali);
        Assert.Equal(36, sys.RitornoUnita);
        Assert.Equal(36.0 / 35.0, sys.Q, 10);
        Assert.Equal(35, sys.NumeriCoperti);
        Assert.Equal(new[] { 0, 36 }, sys.NumeriScoperti);
    }

    [Theory]
    [InlineData(31, 34, 35)]
    [InlineData(31, 34, 36)]
    [InlineData(31, 35, 36)]
    [InlineData(34, 31, 32)]
    public void PiuPieni_ProfittoRealePerOgniNumero(int terzina, int p1, int p2)
    {
        var sys = Catalog.PiuPieni(terzina, p1, p2);
        decimal puntata = 35m; // 1 unità = 1€

        for (int n = 0; n <= 36; n++)
        {
            bool coperto = (n >= 1 && n <= 30) || (n >= terzina && n <= terzina + 2) || n == p1 || n == p2;
            decimal atteso = coperto ? puntata / 35m : -puntata; // incasso sempre 36 → +1; scoperto → -35

            Assert.Equal(atteso, sys.Profitto(n, puntata));
            Assert.Equal(atteso > 0, sys.Copre(n));
        }
    }

    [Fact]
    public void PiuPieno_ProfittoRealePerOgniNumero()
    {
        var sys = Catalog.PiuPieno(31, 34);
        decimal puntata = 34m;

        for (int n = 0; n <= 36; n++)
        {
            bool coperto = (n >= 1 && n <= 30) || (n >= 31 && n <= 33) || n == 34;
            decimal atteso = coperto ? puntata * 2m / 34m : -puntata; // incasso 36 → +2; scoperto → -34

            Assert.Equal(atteso, sys.Profitto(n, puntata));
        }
    }

    [Fact]
    public void PuntataMinima_NuoviSistemi()
    {
        Assert.Equal(3.4m, Catalog.PiuPieno().PuntataMinima(0.10m));
        Assert.Equal(3.5m, Catalog.PiuPieni().PuntataMinima(0.10m));
    }

    [Fact]
    public void Validazioni_PieniERotazioni()
    {
        Assert.Throws<ArgumentException>(() => Catalog.PiuPieno(31, 25));   // non è un residuo
        Assert.Throws<ArgumentException>(() => Catalog.PiuPieno(31, 32));   // coperto dalla terzina
        Assert.Throws<ArgumentException>(() => Catalog.PiuPieni(31, 34, 34)); // duplicato
        // rotazione terzina 34-36: residui 31,32,33
        Assert.Equal(new[] { 0, 33 }, Catalog.PiuPieni(34, 31, 32).NumeriScoperti);
    }

    [Fact]
    public void Crea_CodificaParametroRoundtrip()
    {
        var s3 = Catalog.Crea(Catalog.CodicePiuPieno, Catalog.CodificaParametro(31, 34));
        Assert.Equal(Catalog.PiuPieno(31, 34).Nome, s3.Nome);

        var s4 = Catalog.Crea(Catalog.CodicePiuPieni, Catalog.CodificaParametro(34, 31, 33));
        Assert.Equal(Catalog.PiuPieni(34, 31, 33).Nome, s4.Nome);
        Assert.Equal(new[] { 0, 32 }, s4.NumeriScoperti);
    }

    [Fact]
    public void PianiOltre35Numeri_Rifiutati_NessunaVincita()
    {
        // 36/37: ritorno 36 unità su 36 puntate → netto 0 (terzo pieno aggiunto a mano)
        var seg36 = new[]
        {
            new BetSegment("D1", Enumerable.Range(1, 12).ToList(), 3, 12),
            new BetSegment("D2", Enumerable.Range(13, 12).ToList(), 3, 12),
            new BetSegment("SEST", Enumerable.Range(25, 6).ToList(), 6, 6),
            new BetSegment("TERZ", new[] { 31, 32, 33 }, 12, 3),
            new BetSegment("P34", new[] { 34 }, 36, 1),
            new BetSegment("P35", new[] { 35 }, 36, 1),
            new BetSegment("P36", new[] { 36 }, 36, 1),
        };
        var ex = Assert.Throws<ArgumentException>(() => new BettingSystem("X36", "36 numeri", seg36));
        Assert.Contains("vincita", ex.Message);

        var seg37 = seg36.Append(new BetSegment("P0", new[] { 0 }, 36, 1)).ToArray();
        Assert.Throws<ArgumentException>(() => new BettingSystem("X37", "37 numeri", seg37));
    }

    [Fact]
    public void ScommesseEsatte_ValoriVerificati()
    {
        // 30/37 (S1)
        var s1 = ScommesseEsatte.Suggerite(30);
        Assert.Contains(s1, s => s.Tag == "corta" && s.W == 9 && s.M == 11);
        Assert.Contains(s1, s => s.Tag == "media" && s.W == 17 && s.M == 21);
        Assert.Contains(s1, s => s.Tag == "esatta" && s.W == 30 && s.M == 37);

        // 33/37 (S2)
        var s2 = ScommesseEsatte.Suggerite(33);
        Assert.Contains(s2, s => s.Tag == "corta" && s.W == 8 && s.M == 9);
        Assert.Contains(s2, s => s.Tag == "media" && s.W == 25 && s.M == 28);
        Assert.Contains(s2, s => s.Tag == "esatta" && s.W == 33 && s.M == 37);

        // 34/37 (S3)
        var s3 = ScommesseEsatte.Suggerite(34);
        Assert.Contains(s3, s => s.Tag == "corta" && s.W == 11 && s.M == 12);
        Assert.Contains(s3, s => s.Tag == "media" && s.W == 23 && s.M == 25);
        Assert.Contains(s3, s => s.Tag == "esatta" && s.W == 34 && s.M == 37);

        // 35/37 (S4)
        var s4 = ScommesseEsatte.Suggerite(35);
        Assert.Contains(s4, s => s.Tag == "media" && s.W == 18 && s.M == 19);
        Assert.Contains(s4, s => s.Tag == "esatta" && s.W == 35 && s.M == 37);
    }

    [Fact]
    public void Masaniello_NuoviSistemi_TabellaEObiettivo()
    {
        // S4 con W/M dalla scommessa media (18/19): tabella calcolabile e obiettivo positivo
        var sys = Catalog.PiuPieni(31, 34, 35);
        var tab = MasanielloEngine.GetTable(19, 18, sys.Q);
        Assert.True(tab.TargetMultiplier > 1.0);
        double frac = tab.StakeFraction(0, 0);
        Assert.InRange(frac, 0.0, 1.0);
    }
}
