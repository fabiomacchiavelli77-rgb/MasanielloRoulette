using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Masaniello.Core.Data;

public sealed record SessioneRecord(
    long Id, string Sistema, int Parametro, int M, int W, double Q,
    string ModalitaRischio, decimal Chip, decimal BankIniziale,
    decimal? BankFinale, string Stato, string IniziataIl, string? ChiusaIl,
    string Gestione, int KRecupero);

public sealed record PermanenzaRecord(long Id, string Nome, string CaricataIl, int NColpi, int Cursore);

public sealed record ColpoRecord(
    long Id, long SessioneId, int NColpo, int Numero, string Esito,
    decimal Puntata, string Split, decimal BankPrima, decimal BankDopo,
    decimal Profitto, string Fonte, string RegistratoIl);

public sealed record McStatRecord(
    long RunId, string Etichetta, int Sessioni, double PctVinte,
    decimal ProfittoMedio, decimal ProfittoMax, decimal PerditaMax,
    decimal DdMedio, decimal DdMax, double RoiMedioPct);

/// <summary>Persistenza SQLite. Gli importi sono salvati come TEXT invariant per evitare errori di virgola mobile.</summary>
public sealed class Database : IDisposable
{
    public const string StatoInCorso = "IN_CORSO";
    public const string StatoVinta = "VINTA";
    public const string StatoPersa = "PERSA";
    public const string StatoInterrotta = "INTERROTTA";

    private readonly SqliteConnection _conn;

    public Database(string percorso)
    {
        _conn = new SqliteConnection($"Data Source={percorso}");
        _conn.Open();
        CreaSchema();
    }

    public void Dispose() => _conn.Dispose();

    private void CreaSchema()
    {
        Esegui("""
            CREATE TABLE IF NOT EXISTS config (
                chiave TEXT PRIMARY KEY,
                valore TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS sessioni (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                sistema TEXT NOT NULL,
                parametro INTEGER NOT NULL,
                m INTEGER NOT NULL,
                w INTEGER NOT NULL,
                q REAL NOT NULL,
                modalita_rischio TEXT NOT NULL,
                chip TEXT NOT NULL,
                bank_iniziale TEXT NOT NULL,
                bank_finale TEXT,
                stato TEXT NOT NULL,
                iniziata_il TEXT NOT NULL,
                chiusa_il TEXT
            );
            CREATE TABLE IF NOT EXISTS colpi (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                sessione_id INTEGER NOT NULL REFERENCES sessioni(id),
                n_colpo INTEGER NOT NULL,
                numero INTEGER NOT NULL,
                esito TEXT NOT NULL,
                puntata TEXT NOT NULL,
                split TEXT NOT NULL,
                bank_prima TEXT NOT NULL,
                bank_dopo TEXT NOT NULL,
                profitto TEXT NOT NULL,
                fonte TEXT NOT NULL,
                registrato_il TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_colpi_sessione ON colpi(sessione_id);
            CREATE TABLE IF NOT EXISTS mc_runs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                eseguito_il TEXT NOT NULL,
                n_sessioni INTEGER NOT NULL,
                modalita TEXT NOT NULL,
                seed INTEGER NOT NULL,
                bank_iniziale TEXT NOT NULL,
                chip TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS mc_stats (
                run_id INTEGER NOT NULL REFERENCES mc_runs(id),
                etichetta TEXT NOT NULL,
                sessioni INTEGER NOT NULL,
                pct_vinte REAL NOT NULL,
                profitto_medio TEXT NOT NULL,
                profitto_max TEXT NOT NULL,
                perdita_max TEXT NOT NULL,
                dd_medio TEXT NOT NULL,
                dd_max TEXT NOT NULL,
                roi_medio_pct REAL NOT NULL
            );
            CREATE TABLE IF NOT EXISTS permanenze (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                nome TEXT NOT NULL,
                caricata_il TEXT NOT NULL,
                n_colpi INTEGER NOT NULL,
                numeri TEXT NOT NULL,
                cursore INTEGER NOT NULL DEFAULT 0
            );
            """);

        // migrazione dei DB creati prima della gestione "Recupero del picco"
        AggiungiColonnaSeManca("sessioni", "gestione", "TEXT NOT NULL DEFAULT 'MASANIELLO'");
        AggiungiColonnaSeManca("sessioni", "k_recupero", "INTEGER NOT NULL DEFAULT 0");
    }

    private void AggiungiColonnaSeManca(string tabella, string colonna, string definizione)
    {
        using var cmd = Comando(
            $"SELECT COUNT(*) FROM pragma_table_info('{tabella}') WHERE name = '{colonna}'");
        if ((long)cmd.ExecuteScalar()! == 0)
            Esegui($"ALTER TABLE {tabella} ADD COLUMN {colonna} {definizione}");
    }

    private static string Inv(decimal d) => d.ToString(CultureInfo.InvariantCulture);
    private static decimal Dec(object v) => decimal.Parse((string)v, CultureInfo.InvariantCulture);
    private static string Adesso() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    private void Esegui(string sql, params (string Nome, object Valore)[] parametri)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (nome, valore) in parametri) cmd.Parameters.AddWithValue(nome, valore);
        cmd.ExecuteNonQuery();
    }

    private SqliteCommand Comando(string sql, params (string Nome, object Valore)[] parametri)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (nome, valore) in parametri) cmd.Parameters.AddWithValue(nome, valore);
        return cmd;
    }

    // ----- config -----

    public string? GetConfig(string chiave)
    {
        using var cmd = Comando("SELECT valore FROM config WHERE chiave = $c", ("$c", chiave));
        return cmd.ExecuteScalar() as string;
    }

    public void SetConfig(string chiave, string valore) =>
        Esegui("INSERT INTO config(chiave, valore) VALUES($c, $v) ON CONFLICT(chiave) DO UPDATE SET valore = $v",
               ("$c", chiave), ("$v", valore));

    // ----- sessioni -----

    public long InserisciSessione(string sistema, int parametro, int m, int w, double q,
                                  string modalita, decimal chip, decimal bankIniziale,
                                  string gestione, int kRecupero)
    {
        using var cmd = Comando("""
            INSERT INTO sessioni(sistema, parametro, m, w, q, modalita_rischio, chip,
                                 bank_iniziale, stato, iniziata_il, gestione, k_recupero)
            VALUES($sis, $par, $m, $w, $q, $mod, $chip, $bank, $stato, $data, $ges, $k);
            SELECT last_insert_rowid();
            """,
            ("$sis", sistema), ("$par", parametro), ("$m", m), ("$w", w), ("$q", q),
            ("$mod", modalita), ("$chip", Inv(chip)), ("$bank", Inv(bankIniziale)),
            ("$stato", StatoInCorso), ("$data", Adesso()), ("$ges", gestione), ("$k", kRecupero));
        return (long)cmd.ExecuteScalar()!;
    }

    public void ChiudiSessione(long id, string stato, decimal bankFinale) =>
        Esegui("UPDATE sessioni SET stato = $s, bank_finale = $b, chiusa_il = $d WHERE id = $id",
               ("$s", stato), ("$b", Inv(bankFinale)), ("$d", Adesso()), ("$id", id));

    private static SessioneRecord LeggiSessione(SqliteDataReader r) => new(
        r.GetInt64(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4),
        r.GetDouble(5), r.GetString(6), Dec(r.GetString(7)), Dec(r.GetString(8)),
        r.IsDBNull(9) ? null : Dec(r.GetString(9)), r.GetString(10), r.GetString(11),
        r.IsDBNull(12) ? null : r.GetString(12), r.GetString(13), r.GetInt32(14));

    private const string ColonneSessione =
        "id, sistema, parametro, m, w, q, modalita_rischio, chip, bank_iniziale, bank_finale, stato, iniziata_il, chiusa_il, gestione, k_recupero";

    public SessioneRecord? SessioneInCorso()
    {
        using var cmd = Comando($"SELECT {ColonneSessione} FROM sessioni WHERE stato = $s ORDER BY id DESC LIMIT 1",
                                ("$s", StatoInCorso));
        using var r = cmd.ExecuteReader();
        return r.Read() ? LeggiSessione(r) : null;
    }

    /// <summary>Bank finale dell'ultima sessione chiusa: è la cassa per il rollover.</summary>
    public decimal? UltimoBankFinale()
    {
        using var cmd = Comando(
            "SELECT bank_finale FROM sessioni WHERE bank_finale IS NOT NULL ORDER BY id DESC LIMIT 1");
        var v = cmd.ExecuteScalar();
        return v is string s ? Dec(s) : null;
    }

    public List<SessioneRecord> TutteLeSessioni()
    {
        using var cmd = Comando($"SELECT {ColonneSessione} FROM sessioni ORDER BY id");
        using var r = cmd.ExecuteReader();
        var res = new List<SessioneRecord>();
        while (r.Read()) res.Add(LeggiSessione(r));
        return res;
    }

    // ----- colpi -----

    public void InserisciColpo(long sessioneId, int nColpo, int numero, string esito,
                               decimal puntata, string split, decimal bankPrima,
                               decimal bankDopo, decimal profitto, string fonte) =>
        Esegui("""
            INSERT INTO colpi(sessione_id, n_colpo, numero, esito, puntata, split,
                              bank_prima, bank_dopo, profitto, fonte, registrato_il)
            VALUES($sid, $n, $num, $e, $p, $sp, $bp, $bd, $pr, $f, $d)
            """,
            ("$sid", sessioneId), ("$n", nColpo), ("$num", numero), ("$e", esito),
            ("$p", Inv(puntata)), ("$sp", split), ("$bp", Inv(bankPrima)),
            ("$bd", Inv(bankDopo)), ("$pr", Inv(profitto)), ("$f", fonte), ("$d", Adesso()));

    public List<ColpoRecord> ColpiDiSessione(long sessioneId)
    {
        using var cmd = Comando("""
            SELECT id, sessione_id, n_colpo, numero, esito, puntata, split,
                   bank_prima, bank_dopo, profitto, fonte, registrato_il
            FROM colpi WHERE sessione_id = $sid ORDER BY n_colpo
            """, ("$sid", sessioneId));
        using var r = cmd.ExecuteReader();
        var res = new List<ColpoRecord>();
        while (r.Read())
            res.Add(new ColpoRecord(
                r.GetInt64(0), r.GetInt64(1), r.GetInt32(2), r.GetInt32(3), r.GetString(4),
                Dec(r.GetString(5)), r.GetString(6), Dec(r.GetString(7)), Dec(r.GetString(8)),
                Dec(r.GetString(9)), r.GetString(10), r.GetString(11)));
        return res;
    }

    // ----- montecarlo -----

    public long InserisciMcRun(int nSessioni, string modalita, int seed, decimal bankIniziale, decimal chip)
    {
        using var cmd = Comando("""
            INSERT INTO mc_runs(eseguito_il, n_sessioni, modalita, seed, bank_iniziale, chip)
            VALUES($d, $n, $m, $s, $b, $c);
            SELECT last_insert_rowid();
            """,
            ("$d", Adesso()), ("$n", nSessioni), ("$m", modalita), ("$s", seed),
            ("$b", Inv(bankIniziale)), ("$c", Inv(chip)));
        return (long)cmd.ExecuteScalar()!;
    }

    // ----- permanenze -----

    public long InserisciPermanenza(string nome, IReadOnlyList<int> numeri)
    {
        using var cmd = Comando("""
            INSERT INTO permanenze(nome, caricata_il, n_colpi, numeri, cursore)
            VALUES($n, $d, $c, $num, 0);
            SELECT last_insert_rowid();
            """,
            ("$n", nome), ("$d", Adesso()), ("$c", numeri.Count), ("$num", string.Join(",", numeri)));
        return (long)cmd.ExecuteScalar()!;
    }

    public List<PermanenzaRecord> TutteLePermanenze()
    {
        using var cmd = Comando("SELECT id, nome, caricata_il, n_colpi, cursore FROM permanenze ORDER BY id");
        using var r = cmd.ExecuteReader();
        var res = new List<PermanenzaRecord>();
        while (r.Read())
            res.Add(new PermanenzaRecord(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetInt32(4)));
        return res;
    }

    public PermanenzaRecord? Permanenza(long id)
    {
        using var cmd = Comando("SELECT id, nome, caricata_il, n_colpi, cursore FROM permanenze WHERE id = $id", ("$id", id));
        using var r = cmd.ExecuteReader();
        return r.Read()
            ? new PermanenzaRecord(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetInt32(4))
            : null;
    }

    public int[] NumeriPermanenza(long id)
    {
        using var cmd = Comando("SELECT numeri FROM permanenze WHERE id = $id", ("$id", id));
        return cmd.ExecuteScalar() is string s && s.Length > 0
            ? Array.ConvertAll(s.Split(','), int.Parse)
            : [];
    }

    public void AggiornaCursorePermanenza(long id, int cursore) =>
        Esegui("UPDATE permanenze SET cursore = $c WHERE id = $id", ("$c", cursore), ("$id", id));

    public void EliminaPermanenza(long id) =>
        Esegui("DELETE FROM permanenze WHERE id = $id", ("$id", id));

    public void InserisciMcStat(McStatRecord s) =>
        Esegui("""
            INSERT INTO mc_stats(run_id, etichetta, sessioni, pct_vinte, profitto_medio,
                                 profitto_max, perdita_max, dd_medio, dd_max, roi_medio_pct)
            VALUES($r, $e, $n, $pv, $pm, $px, $pe, $dm, $dx, $roi)
            """,
            ("$r", s.RunId), ("$e", s.Etichetta), ("$n", s.Sessioni), ("$pv", s.PctVinte),
            ("$pm", Inv(s.ProfittoMedio)), ("$px", Inv(s.ProfittoMax)), ("$pe", Inv(s.PerditaMax)),
            ("$dm", Inv(s.DdMedio)), ("$dx", Inv(s.DdMax)), ("$roi", s.RoiMedioPct));
}
