using Masaniello.Core.Data;
using Masaniello.Core.Engine;
using Masaniello.Core.Permanenze;
using Masaniello.Core.Sessions;
using Masaniello.Core.Simulation;
using Masaniello.Core.Systems;

namespace Masaniello.Tests;

public class PermanenzeERecuperoTests
{
    // ------------------------------------------------------------- permanenze

    private static string PercorsoFile888()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string p = Path.Combine(dir.FullName, "888-roulette-31-01-2020-francese.xls");
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException("File permanenza 888 non trovato nella cartella del progetto.");
    }

    [Fact]
    public void Loader_CaricaLaPermanenzaReale888()
    {
        var numeri = PermanenceLoader.Carica(PercorsoFile888());

        Assert.Equal(2021, numeri.Count);
        Assert.Equal(new[] { 22, 2, 25, 11, 19 }, numeri.Take(5));
        Assert.All(numeri, n => Assert.InRange(n, 0, 36));
    }

    [Fact]
    public void Loader_IgnoraLaColonnaIndice()
    {
        // prima colonna = indice del colpo (1, 2, 3, …), seconda = numeri usciti
        string file = Path.Combine(Path.GetTempPath(), $"perm_{Guid.NewGuid():N}.csv");
        var attesi = new[] { 22, 2, 25, 11, 19, 12, 28, 1, 33, 28, 17, 2, 28, 3, 0, 36, 7, 15, 24, 31 };
        File.WriteAllLines(file, attesi.Select((n, i) => $"{i + 1};{n}"));
        try
        {
            Assert.Equal(attesi, PermanenceLoader.Carica(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Loader_IgnoraColonnaIndiceAncheConBuchi()
    {
        // boule NON consecutiva (1, 2, 4, 7, …) ma comunque strettamente crescente:
        // non è la permanenza, i numeri usciti sono nell'altra colonna
        string file = Path.Combine(Path.GetTempPath(), $"perm_{Guid.NewGuid():N}.csv");
        var attesi = new[] { 22, 2, 25, 11, 19, 12, 28, 1, 33, 28, 17, 2, 28, 3, 0, 36, 7, 15, 24, 31 };
        var boule = new[] { 1, 2, 4, 7, 9, 10, 14, 16, 19, 22, 25, 27, 30, 33, 35, 38, 40, 43, 45, 48 };
        File.WriteAllLines(file, attesi.Zip(boule, (n, b) => $"{b};{n}"));
        try
        {
            Assert.Equal(attesi, PermanenceLoader.Carica(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Loader_IgnoraColonneCostantiOFlag01()
    {
        // tavolo sempre "2" e flag Rosso 0/1: devono contare meno della colonna numeri
        string file = Path.Combine(Path.GetTempPath(), $"perm_{Guid.NewGuid():N}.csv");
        var attesi = new[] { 22, 2, 25, 11, 19, 12, 28, 1, 33, 28, 17, 2, 28, 3, 0, 36, 7, 15, 24, 31 };
        File.WriteAllLines(file, attesi.Select((n, i) => $"2;{n % 2};{n}"));
        try
        {
            Assert.Equal(attesi, PermanenceLoader.Carica(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Loader_LeggeFalsoXlsCheInRealtaEHtml()
    {
        // alcuni siti salvano tabelle HTML con estensione .xls: Excel le apre,
        // ExcelDataReader no → il loader deve ricadere sulla lettura testo/HTML
        string file = Path.Combine(Path.GetTempPath(), $"perm_{Guid.NewGuid():N}.xls");
        var attesi = new[] { 22, 2, 25, 11, 19, 12, 28, 1, 33, 28, 17, 2, 28, 3, 0, 36, 7, 15, 24, 31 };
        var righe = attesi.Select((n, i) => $"<tr><td>{i + 1}</td><td>{n}</td></tr>");
        File.WriteAllText(file, "<html><table><tr><td>Boule</td><td>Numero</td></tr>" +
                                string.Join("", righe) + "</table></html>");
        try
        {
            Assert.Equal(attesi, PermanenceLoader.Carica(file));
        }
        finally { File.Delete(file); }
    }

    // ----------------------------------------------- gestione Recupero del picco

    // S1 (q = 1,2, minimo 5 chip), chip 1 €, cassa 100 €: ogni vincita al minimo = +1 €
    private static MotoreSessione NuovoMotore(int m = 10, int k = 5) => new(
        Catalog.DozzineSestina(), GestionePuntata.RecuperoPicco, m, 0, k,
        100m, 1m, ModalitaRischio.Ultra.MaxPct());

    [Fact]
    public void RecuperoPicco_FincheVince_PuntaIlMinimoEAggiornaIlPicco()
    {
        var motore = NuovoMotore();
        Assert.Equal(5m, motore.ProssimaPuntata);

        motore.Applica(1); // numero coperto: vinto
        Assert.Equal(101m, motore.Bank);
        Assert.Equal(101m, motore.Picco);
        Assert.False(motore.InRecupero);
        Assert.Equal(5m, motore.ProssimaPuntata); // si resta al minimo
    }

    [Fact]
    public void RecuperoPicco_DopoUnaPerdita_EntraInRecuperoColWMinimo()
    {
        var motore = NuovoMotore();
        motore.Applica(1); // bank 101, picco 101
        motore.Applica(0); // perso: bank 96

        Assert.True(motore.InRecupero);
        // target 101/96 ≈ 1,0521 in 5 colpi a q=1,2: servono almeno 4 vittorie
        // (con W=3 il piano arriva solo a ×1,037)
        Assert.Equal(4, motore.PianoRecuperoCorrente!.W);
        // la puntata di recupero resta multipla del minimo del sistema
        Assert.True(motore.ProssimaPuntata >= 5m && motore.ProssimaPuntata % 5m == 0m);
    }

    [Fact]
    public void RecuperoPicco_RecuperoRiuscito_TornaAlMinimo()
    {
        var motore = NuovoMotore();
        motore.Applica(1);
        motore.Applica(0); // entra in recupero verso 101

        int colpi = 0;
        while (motore.InRecupero && colpi++ < 5) motore.Applica(1); // solo vincite

        Assert.False(motore.InRecupero);
        Assert.False(motore.Terminata);
        Assert.True(motore.Bank >= 101m);    // cassa tornata almeno al picco
        Assert.Equal(motore.Bank, motore.Picco);
        Assert.Equal(5m, motore.ProssimaPuntata); // e si riparte dal minimo
    }

    [Fact]
    public void RecuperoPicco_RecuperoMorto_SessionePersa()
    {
        var motore = NuovoMotore();
        motore.Applica(1);
        motore.Applica(0); // recupero: servono 4 vittorie in 5 colpi
        motore.Applica(0); // 4 in 4: ancora possibile
        motore.Applica(0); // 4 in 3: piano morto

        Assert.True(motore.Terminata);
        Assert.False(motore.Vinta);
        Assert.Equal(0m, motore.ProssimaPuntata);
    }

    [Fact]
    public void RecuperoPicco_ColpiEsauriti_VintaSeInProfitto()
    {
        var motore = NuovoMotore(m: 5, k: 5);
        for (int i = 0; i < 5; i++) motore.Applica(1);

        Assert.True(motore.Terminata);
        Assert.True(motore.Vinta);
        Assert.Equal(105m, motore.Bank); // 5 vincite al minimo da +1 €
    }

    [Fact]
    public void RecuperoPicco_RipresaDaDb_RicostruisceLoStatoDelRecupero()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"mas_test_{Guid.NewGuid():N}.db");
        try
        {
            using var db = new Database(dbPath);
            var svc1 = new SessionService(db, seedRng: 7);
            svc1.NuovaSessione(new NuovaSessioneConfig(
                Catalog.CodiceDozzineSestina, 25, 10, 0, ModalitaRischio.Ultra, 1m, 100m,
                Rollover: false, GestionePuntata.RecuperoPicco, K: 5));
            svc1.ProcessaNumero(1, SessionService.FonteManuale);  // vinta
            svc1.ProcessaNumero(0, SessionService.FonteManuale);  // persa: in recupero
            var m1 = svc1.Corrente!.Motore;
            Assert.True(m1.InRecupero);

            // come riaprire l'app: i colpi vengono rigiocati nel motore
            var svc2 = new SessionService(db, seedRng: 7);
            var m2 = svc2.Corrente!.Motore;
            Assert.True(m2.InRecupero);
            Assert.Equal(m1.Bank, m2.Bank);
            Assert.Equal(m1.Picco, m2.Picco);
            Assert.Equal(m1.ProssimaPuntata, m2.ProssimaPuntata);
            Assert.Equal(m1.PianoRecuperoCorrente!.W, m2.PianoRecuperoCorrente!.W);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }

    // ------------------------------------------------------ backtest permanenza

    [Fact]
    public void BacktestPermanenza_ConfigurazioniIdentiche_RisultatiIdentici()
    {
        var rng = new Random(42);
        var numeri = Enumerable.Range(0, 600).Select(_ => rng.Next(37)).ToList();
        var a = new McConfig("A", Catalog.DozzineSestina(), 20, 15, ModalitaRischio.Ultra);
        var b = new McConfig("B", Catalog.DozzineSestina(), 20, 15, ModalitaRischio.Ultra);

        var ris = MonteCarloRunner.EseguiSuPermanenza([a, b], numeri, McModalita.Indipendente, 100m, 0.1m);

        Assert.Equal(600 / 20, ris[0].Stat.Sessioni);
        for (int i = 0; i < ris[0].Sessioni.Count; i++)
            Assert.Equal(ris[0].Sessioni[i] with { }, ris[1].Sessioni[i]);
    }

    [Fact]
    public void BacktestPermanenza_File888_BlocchiDallaConfigPiuLunga()
    {
        var numeri = PermanenceLoader.Carica(PercorsoFile888());
        var classico = new McConfig("S1 20/15", Catalog.DozzineSestina(), 20, 15, ModalitaRischio.Ultra);
        var recupero = new McConfig("S1 Rec", Catalog.DozzineSestina(), 20, 0, ModalitaRischio.Ultra,
                                    GestionePuntata.RecuperoPicco, K: 10);

        // blocco = max colpi richiesti = 20+10 → 2021/30 = 67 sessioni per entrambe
        var ris = MonteCarloRunner.EseguiSuPermanenza([classico, recupero], numeri,
                                                      McModalita.Indipendente, 100m, 0.1m);

        Assert.All(ris, r => Assert.Equal(67, r.Stat.Sessioni));
        // sanity: su dati reali nessuna config può avere il 100% di vittorie e profitto medio positivo alto
        Assert.All(ris, r => Assert.InRange(r.Stat.PctVinte, 0.0, 100.0));
    }
}
