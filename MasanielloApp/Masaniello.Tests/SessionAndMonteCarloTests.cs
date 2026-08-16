using Masaniello.Core.Data;
using Masaniello.Core.Engine;
using Masaniello.Core.Sessions;
using Masaniello.Core.Simulation;
using Masaniello.Core.Systems;

namespace Masaniello.Tests;

public class SessionAndMonteCarloTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"mas_test_{Guid.NewGuid():N}.db");
    private readonly Database _db;

    public SessionAndMonteCarloTests() => _db = new Database(_dbPath);

    public void Dispose()
    {
        _db.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    private static NuovaSessioneConfig Config(decimal bank = 100m, bool rollover = true) => new(
        Catalog.CodiceDozzineSestina, 25, 20, 13, ModalitaRischio.Ultra, 1m, bank, rollover);

    [Fact]
    public void SessioneCompleta_ChiusaESalvataNelDb()
    {
        var svc = new SessionService(_db, seedRng: 7);
        svc.NuovaSessione(Config());

        EsitoColpo? ultimo = null;
        while (svc.Corrente != null) ultimo = svc.SimulaRandom();

        Assert.NotNull(ultimo);
        Assert.True(ultimo.StatoSessione is Database.StatoVinta or Database.StatoPersa);

        var sessioni = _db.TutteLeSessioni();
        Assert.Single(sessioni);
        Assert.Equal(ultimo.StatoSessione, sessioni[0].Stato);
        Assert.Equal(ultimo.BankDopo, sessioni[0].BankFinale);

        var colpi = _db.ColpiDiSessione(sessioni[0].Id);
        Assert.Equal(colpi.Count, colpi[^1].NColpo);
        // la contabilità deve quadrare colpo per colpo
        decimal bank = 100m;
        foreach (var c in colpi)
        {
            Assert.Equal(bank, c.BankPrima);
            Assert.Equal(c.BankPrima + c.Profitto, c.BankDopo);
            bank = c.BankDopo;
        }
    }

    [Fact]
    public void Rollover_RipartedalBankFinalePrecedente()
    {
        var svc = new SessionService(_db, seedRng: 7);
        svc.NuovaSessione(Config());
        while (svc.Corrente != null) svc.SimulaRandom();
        decimal bankFinale = _db.UltimoBankFinale()!.Value;

        // se la sessione precedente è finita a cassa 0 (all-in perso) il rollover
        // ricade sul bank configurato, altrimenti continua dal bank finale
        var s2 = svc.NuovaSessione(Config(bank: 100m, rollover: true));
        decimal atteso = bankFinale > 0 ? bankFinale : 100m;
        Assert.Equal(atteso, s2.Record.BankIniziale);

        // con rollover disattivato si riparte dal bank configurato
        var s3 = svc.NuovaSessione(Config(bank: 250m, rollover: false));
        Assert.Equal(250m, s3.Record.BankIniziale);
    }

    [Fact]
    public void NuovaSessioneSuSessioneAperta_ArchiviaComeInterrotta()
    {
        var svc = new SessionService(_db, seedRng: 7);
        svc.NuovaSessione(Config());
        svc.SimulaRandom();
        decimal bankDopoUnColpo = svc.Corrente!.Bank;

        svc.NuovaSessione(Config());

        var sessioni = _db.TutteLeSessioni();
        Assert.Equal(2, sessioni.Count);
        Assert.Equal(Database.StatoInterrotta, sessioni[0].Stato);
        Assert.Equal(bankDopoUnColpo, sessioni[0].BankFinale);
        // e il rollover usa il bank della sessione interrotta
        Assert.Equal(bankDopoUnColpo, sessioni[1].BankIniziale);
    }

    [Fact]
    public void RipresaSessioneInCorso_DalDb()
    {
        var svc1 = new SessionService(_db, seedRng: 7);
        svc1.NuovaSessione(Config());
        svc1.SimulaRandom();
        svc1.SimulaRandom();
        var attesa = (svc1.Corrente!.Bank, svc1.Corrente.Colpi, svc1.Corrente.Vittorie, svc1.Corrente.ProssimaPuntata);

        // nuovo servizio sullo stesso DB: come riaprire l'app
        var svc2 = new SessionService(_db, seedRng: 7);
        Assert.NotNull(svc2.Corrente);
        Assert.Equal(attesa, (svc2.Corrente!.Bank, svc2.Corrente.Colpi, svc2.Corrente.Vittorie, svc2.Corrente.ProssimaPuntata));
    }

    [Fact]
    public void MonteCarlo_Sistema1_StatisticheNoteDallExcel()
    {
        // parità con il VBA verificato: 20/13 Ultra → ~98% vinte, profitto medio leggermente negativo
        var cfg = new McConfig("S1", Catalog.DozzineSestina(), 20, 13, ModalitaRischio.Ultra);
        var ris = MonteCarloRunner.Esegui(new[] { cfg }, 5000, McModalita.Indipendente, 100m, 1m, seed: 123);

        var stat = ris[0].Stat;
        Assert.InRange(stat.PctVinte, 95.0, 99.5);
        Assert.InRange((double)stat.ProfittoMedio, -6.0, 0.5);
        Assert.True(stat.PerditaMax < 0);
    }

    [Fact]
    public void MonteCarlo_CommonRandomNumbers_StessiNumeriPerOgniConfig()
    {
        // due configurazioni IDENTICHE devono produrre risultati identici
        var a = new McConfig("A", Catalog.DozzineSestina(), 20, 13, ModalitaRischio.Ultra);
        var b = new McConfig("B", Catalog.DozzineSestina(), 20, 13, ModalitaRischio.Ultra);
        var ris = MonteCarloRunner.Esegui(new[] { a, b }, 500, McModalita.Indipendente, 100m, 1m, seed: 9);

        for (int i = 0; i < 500; i++)
            Assert.Equal(ris[0].Sessioni[i] with { }, ris[1].Sessioni[i]);
    }

    [Fact]
    public void MargineCasa_IdenticoPerEntrambiISistemi()
    {
        // verità matematica esatta: sommando il profitto su tutti i 37 numeri
        // il totale è -puntata, cioè il banco trattiene 1/37 della puntata a colpo
        // (margine 2,70%) qualunque sia il sistema
        foreach (var sys in new[] { Catalog.DozzineSestina(), Catalog.QuasiTutto() })
        {
            decimal puntata = sys.UnitaTotali * 1m;
            decimal totale = Enumerable.Range(0, 37).Sum(n => sys.Profitto(n, puntata));
            Assert.Equal(-puntata, totale);
        }
    }

    [Fact]
    public void MonteCarlo_QuasiTutto_PiuPrudente()
    {
        // a parità di M/W e di numeri estratti, il sistema 2 copre più numeri
        // (89,2% vs 81,1% per colpo) quindi vince più sessioni, e con la quota
        // più bassa (12/11 vs 6/5) il target — e quindi il profitto massimo — è minore
        var s1 = new McConfig("S1", Catalog.DozzineSestina(), 20, 13, ModalitaRischio.Ultra);
        var s2 = new McConfig("S2", Catalog.QuasiTutto(), 20, 13, ModalitaRischio.Ultra);
        var ris = MonteCarloRunner.Esegui(new[] { s1, s2 }, 3000, McModalita.Indipendente, 100m, 0.1m, seed: 5);

        Assert.True(ris[1].Stat.PctVinte > ris[0].Stat.PctVinte);
        Assert.True(ris[1].Stat.ProfittoMax < ris[0].Stat.ProfittoMax);
    }
}
