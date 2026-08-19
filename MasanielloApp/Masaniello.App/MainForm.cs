using System.Globalization;
using Masaniello.Core.Data;
using Masaniello.Core.Engine;
using Masaniello.Core.Permanenze;
using Masaniello.Core.Sessions;
using Masaniello.Core.Simulation;
using Masaniello.Core.Systems;
using ScottPlot.WinForms;

namespace Masaniello.App;

public sealed class MainForm : Form
{
    private const string NomeS1 = "Dozzine + Sestina (30 numeri, +20%)";
    private const string NomeS2 = "Quasi tutto (33 numeri, +9,09%)";
    private const string NomeS3 = "Quasi tutto + 1 pieno (34 numeri, +5,88%)";
    private const string NomeS4 = "Quasi tutto + 2 pieni (35 numeri, +2,86%)";

    private readonly Database _db;
    private readonly SessionService _svc;

    // tab Sessione
    private ComboBox _cmbSistema = null!, _cmbGestione = null!;
    private NumericUpDown _numM = null!, _numW = null!, _numK = null!;
    private Label _lblW = null!, _lblK = null!;
    private ComboBox _cmbModalita = null!;
    private Label _lblEsatte = null!;
    private Label _lblStato = null!, _lblBank = null!, _lblColpi = null!, _lblVittorie = null!,
                  _lblObiettivo = null!, _lblPuntata = null!, _lblBreakdown = null!;
    private TextBox _txtNumero = null!;
    private Button _btnAggiungi = null!, _btnRandom = null!, _btnPermColpo = null!;
    private ComboBox _cmbPermSessione = null!;
    private DataGridView _gridColpi = null!;

    // tab Storico
    private DataGridView _gridSessioni = null!;
    private FormsPlot _plotStorico = null!;
    private Label _lblRiepilogoStorico = null!;

    // tab MonteCarlo
    private NumericUpDown _numMcSessioni = null!;
    private ComboBox _cmbMcModalita = null!, _cmbMcSorgente = null!, _cmbMcPermanenza = null!;
    private Label _lblMcInfo = null!;
    private DataGridView _gridMcConfig = null!, _gridMcStats = null!;
    private FormsPlot _plotMc = null!;
    private Button _btnMcRun = null!;
    private ProgressBar _mcProgress = null!;

    // tab Config
    private TextBox _txtBank = null!, _txtChip = null!;
    private Label _lblInfoChip = null!;
    private ComboBox _cmbRollover = null!, _cmbSestina = null!, _cmbTerzina = null!, _cmbPieni = null!;
    private DataGridView _gridPermanenze = null!;

    /// <summary>Voce dei combo permanenza: mostra nome e posizione del cursore.</summary>
    private sealed record PermItem(PermanenzaRecord Rec)
    {
        public override string ToString() => $"{Rec.Nome} ({Rec.Cursore}/{Rec.NColpi})";
    }

    public MainForm()
    {
        Text = "Masaniello Roulette";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1200, 760);
        MinimumSize = new Size(1000, 640);

        _db = new Database(Path.Combine(AppContext.BaseDirectory, "masaniello.db"));
        _svc = new SessionService(_db);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CreaTabSessione());
        tabs.TabPages.Add(CreaTabStorico());
        tabs.TabPages.Add(CreaTabMonteCarlo());
        tabs.TabPages.Add(CreaTabConfig());
        Controls.Add(tabs);

        CaricaConfigNeiCampi();
        CaricaPermanenze();
        AggiornaPannelloSessione();
        AggiornaStorico();
        FormClosed += (_, _) => _db.Dispose();
    }

    // ----------------------------------------------------------------- config

    private string Cfg(string chiave, string predefinito) => _db.GetConfig(chiave) ?? predefinito;

    private decimal CfgDec(string chiave, decimal predefinito) =>
        decimal.TryParse(Cfg(chiave, ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
            ? v : predefinito;

    private static bool ProvaParseImporto(string testo, out decimal valore) =>
        decimal.TryParse(testo.Trim().Replace("€", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out valore);

    private BettingSystem SistemaSelezionato()
    {
        int terzina = (int)CfgDec("terzina", 31);
        var (p1, p2) = PieniConfigurati();
        return _cmbSistema.SelectedIndex switch
        {
            1 => Catalog.QuasiTutto(terzina),
            2 => Catalog.PiuPieno(terzina, p1),
            3 => Catalog.PiuPieni(terzina, p1, p2),
            _ => Catalog.DozzineSestina((int)CfgDec("sestina", 25)),
        };
    }

    /// <summary>Pieni sui residui della terza dozzina, da config ("34,35" di default).</summary>
    private (int P1, int P2) PieniConfigurati()
    {
        var parti = Cfg("pieni", "34,35").Split(',');
        int p1 = int.TryParse(parti[0].Trim(), out var a) ? a : 34;
        int p2 = parti.Length > 1 && int.TryParse(parti[1].Trim(), out var b) ? b : 35;
        return (p1, p2);
    }

    private (string Codice, int Parametro) SistemaCodiceEParametro()
    {
        int terzina = (int)CfgDec("terzina", 31);
        var (p1, p2) = PieniConfigurati();
        return _cmbSistema.SelectedIndex switch
        {
            1 => (Catalog.CodiceQuasiTutto, terzina),
            2 => (Catalog.CodicePiuPieno, Catalog.CodificaParametro(terzina, p1)),
            3 => (Catalog.CodicePiuPieni, Catalog.CodificaParametro(terzina, p1, p2)),
            _ => (Catalog.CodiceDozzineSestina, (int)CfgDec("sestina", 25)),
        };
    }

    // ------------------------------------------------------------ tab Sessione

    private TabPage CreaTabSessione()
    {
        var page = new TabPage("Sessione");

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 370));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var sinistra = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(6),
        };

        // --- nuova sessione ---
        var gbNuova = NuovoGroupBox("Nuova sessione", 340, 320);
        _cmbSistema = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 310, Left = 12, Top = 24 };
        _cmbSistema.Items.AddRange(new object[] { NomeS1, NomeS2, NomeS3, NomeS4 });
        _cmbSistema.SelectedIndex = 0;
        _cmbSistema.SelectedIndexChanged += (_, _) => AggiornaEtichettaEsatte();

        var lblGes = NuovaEtichetta("Gestione:", 12, 58);
        _cmbGestione = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 152, Left = 170, Top = 56 };
        _cmbGestione.Items.AddRange(new object[] { "Masaniello classico", "Recupero del picco" });
        _cmbGestione.SelectedIndex = 0;
        _cmbGestione.SelectedIndexChanged += (_, _) => AggiornaCampiGestione();

        var lblM = NuovaEtichetta("Colpi totali (M):", 12, 88);
        _numM = new NumericUpDown { Minimum = 1, Maximum = 200, Value = 20, Width = 70, Left = 170, Top = 86 };
        _lblW = NuovaEtichetta("Vittorie obiettivo (W):", 12, 118);
        _numW = new NumericUpDown { Minimum = 1, Maximum = 200, Value = 13, Width = 70, Left = 170, Top = 116 };
        _lblK = NuovaEtichetta("Colpi recupero (K):", 12, 118);
        _numK = new NumericUpDown { Minimum = 1, Maximum = 50, Value = 10, Width = 70, Left = 170, Top = 116 };

        var lblMod = NuovaEtichetta("Modalità rischio:", 12, 148);
        _cmbModalita = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 152, Left = 170, Top = 146 };
        _cmbModalita.Items.AddRange(new object[] { "Prudente", "Intermedia", "Aggressiva", "Ultra" });
        _cmbModalita.SelectedIndex = 3; // Ultra = Masaniello puro, il default

        var btnNuova = new Button { Text = "NUOVA SESSIONE", Width = 310, Height = 40, Left = 12, Top = 182 };
        btnNuova.Click += (_, _) => NuovaSessione();
        var btnEsatta = new Button { Text = "USA ESATTA", Width = 150, Height = 26, Left = 12, Top = 226 };
        btnEsatta.Click += (_, _) => ApplicaScommessaEsatta();
        _lblEsatte = NuovaEtichetta("", 170, 230);
        _lblEsatte.MaximumSize = new Size(180, 0);
        gbNuova.Controls.AddRange(new Control[] { _cmbSistema, lblGes, _cmbGestione, lblM, _numM,
                                                  _lblW, _numW, _lblK, _numK, lblMod, _cmbModalita, btnNuova,
                                                  btnEsatta, _lblEsatte });
        AggiornaCampiGestione();
        AggiornaEtichettaEsatte();

        // --- stato ---
        var gbStato = NuovoGroupBox("Stato sessione", 340, 150);
        _lblStato = NuovaEtichetta("Nessuna sessione", 12, 24, grassetto: true);
        _lblBank = NuovaEtichetta("Bankroll: –", 12, 48);
        _lblColpi = NuovaEtichetta("Colpi: –", 12, 72);
        _lblVittorie = NuovaEtichetta("Vittorie: –", 170, 72);
        _lblObiettivo = NuovaEtichetta("Obiettivo: –", 12, 96);
        gbStato.Controls.AddRange(new Control[] { _lblStato, _lblBank, _lblColpi, _lblVittorie, _lblObiettivo });

        // --- prossima puntata ---
        var gbPuntata = NuovoGroupBox("Prossima puntata", 340, 165);
        _lblPuntata = NuovaEtichetta("–", 12, 26, grassetto: true);
        _lblPuntata.Font = new Font(Font.FontFamily, 14, FontStyle.Bold);
        _lblBreakdown = NuovaEtichetta("", 12, 58);
        _lblBreakdown.AutoSize = true;
        gbPuntata.Controls.AddRange(new Control[] { _lblPuntata, _lblBreakdown });

        // --- gioca ---
        var gbGioca = NuovoGroupBox("Gioca un numero", 340, 150);
        var lblNum = NuovaEtichetta("Numero uscito (0-36):", 12, 28);
        _txtNumero = new TextBox { Width = 60, Left = 170, Top = 25 };
        _txtNumero.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AggiungiNumero(); } };
        _btnAggiungi = new Button { Text = "AGGIUNGI", Width = 86, Height = 26, Left = 236, Top = 24 };
        _btnAggiungi.Click += (_, _) => AggiungiNumero();
        _btnRandom = new Button { Text = "SIMULA RANDOM", Width = 310, Height = 34, Left = 12, Top = 60 };
        _btnRandom.Click += (_, _) => SimulaRandom();
        _cmbPermSessione = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Left = 12, Top = 102 };
        _btnPermColpo = new Button { Text = "PERMANENZA ▶", Width = 156, Height = 26, Left = 166, Top = 100 };
        _btnPermColpo.Click += (_, _) => GiocaColpoPermanenza();
        gbGioca.Controls.AddRange(new Control[] { lblNum, _txtNumero, _btnAggiungi, _btnRandom,
                                                  _cmbPermSessione, _btnPermColpo });

        sinistra.Controls.AddRange(new Control[] { gbNuova, gbStato, gbPuntata, gbGioca });

        _gridColpi = NuovaGriglia();
        _gridColpi.Columns.AddRange(
            ColTesto("#", 40), ColTesto("Numero", 60), ColTesto("Esito", 50),
            ColTesto("Puntata", 80), ColTesto("Profitto", 80), ColTesto("Bank dopo", 90),
            ColTesto("Fonte", 80), ColTesto("Ripartizione", 320));

        layout.Controls.Add(sinistra, 0, 0);
        layout.Controls.Add(_gridColpi, 1, 0);
        page.Controls.Add(layout);
        return page;
    }

    private void AggiornaCampiGestione()
    {
        bool recupero = _cmbGestione.SelectedIndex == 1;
        _lblW.Visible = _numW.Visible = !recupero;
        _lblK.Visible = _numK.Visible = recupero;
    }

    private void NuovaSessione()
    {
        try
        {
            var (codice, parametro) = SistemaCodiceEParametro();
            var cfg = new NuovaSessioneConfig(
                codice, parametro,
                (int)_numM.Value, (int)_numW.Value,
                ModalitaRischioExt.Parse(_cmbModalita.Text),
                CfgDec("chip", 1m), CfgDec("bank", 100m),
                Cfg("rollover", "SI") != "NO",
                _cmbGestione.SelectedIndex == 1 ? GestionePuntata.RecuperoPicco : GestionePuntata.Masaniello,
                (int)_numK.Value);

            _svc.NuovaSessione(cfg);
            AggiornaPannelloSessione();
            AggiornaStorico();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Nuova sessione", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>Scommesse W/M più vicine a coperti/37 per il sistema selezionato.</summary>
    private void AggiornaEtichettaEsatte()
    {
        try
        {
            var sys = SistemaSelezionato();
            var parti = ScommesseEsatte.Suggerite(sys.NumeriCoperti)
                .Select(s => $"{s.Tag} {s.W}/{s.M}");
            _lblEsatte.Text = "Esatte: " + string.Join(" · ", parti);
        }
        catch
        {
            _lblEsatte.Text = "";
        }
    }

    /// <summary>Applica la scommessa "media" (M ≤ 30) al Masaniello classico.</summary>
    private void ApplicaScommessaEsatta()
    {
        if (_cmbGestione.SelectedIndex == 1)
        {
            MessageBox.Show("La scommessa esatta vale per il Masaniello classico, non per il recupero del picco.",
                            "Scommessa esatta", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var sys = SistemaSelezionato();
            var media = ScommesseEsatte.Suggerite(sys.NumeriCoperti).FirstOrDefault(s => s.Tag == "media");
            if (media == null) return;
            _numM.Value = Math.Min(media.M, _numM.Maximum);
            _numW.Value = Math.Min(media.W, _numW.Maximum);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Scommessa esatta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AggiungiNumero()
    {
        if (!int.TryParse(_txtNumero.Text.Trim(), out int n) || n < 0 || n > 36)
        {
            MessageBox.Show("Inserisci un numero da 0 a 36.", "Numero non valido",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _txtNumero.Clear();
        GiocaNumero(() => _svc.ProcessaNumero(n, SessionService.FonteManuale));
    }

    private void SimulaRandom() => GiocaNumero(_svc.SimulaRandom);

    private void GiocaColpoPermanenza()
    {
        if (_cmbPermSessione.SelectedItem is not PermItem item)
        {
            MessageBox.Show("Carica una permanenza nella tab Config e selezionala qui accanto.",
                            "Permanenza", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        GiocaNumero(() => _svc.GiocaColpoPermanenza(item.Rec.Id));
        CaricaPermanenze(); // aggiorna i contatori del cursore nei combo e nella lista
    }

    private void GiocaNumero(Func<EsitoColpo> azione)
    {
        if (_svc.Corrente == null)
        {
            MessageBox.Show("Nessuna sessione in corso: premi NUOVA SESSIONE.", "Sessione",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var esito = azione();
            AggiornaPannelloSessione();

            if (esito.StatoSessione != Database.StatoInCorso)
            {
                AggiornaStorico();
                MessageBox.Show(
                    $"Sessione {esito.StatoSessione}.\nBankroll finale: {esito.BankDopo:C}",
                    "Sessione terminata", MessageBoxButtons.OK,
                    esito.StatoSessione == Database.StatoVinta ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Errore", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AggiornaPannelloSessione()
    {
        var s = _svc.Corrente;
        _gridColpi.Rows.Clear();

        if (s == null)
        {
            _lblStato.Text = "Nessuna sessione in corso";
            _lblBank.Text = $"Prossima cassa: {(Cfg("rollover", "SI") != "NO" && _db.UltimoBankFinale() is decimal u && u > 0 ? u : CfgDec("bank", 100m)):C}";
            _lblColpi.Text = "Colpi: –";
            _lblVittorie.Text = "Vittorie: –";
            _lblObiettivo.Text = "Obiettivo: –";
            _lblPuntata.Text = "–";
            _lblBreakdown.Text = "";
            _btnAggiungi.Enabled = _btnRandom.Enabled = _btnPermColpo.Enabled = false;
            return;
        }

        var m = s.Motore;
        bool recupero = m.Gestione == GestionePuntata.RecuperoPicco;
        _btnAggiungi.Enabled = _btnRandom.Enabled = _btnPermColpo.Enabled = true;
        _lblStato.Text = $"IN CORSO – {s.Sistema.Nome}" + (recupero ? " – Recupero del picco" : "");
        _lblBank.Text = $"Bankroll: {s.Bank:C}";
        _lblColpi.Text = $"Colpi: {s.Colpi} / {s.Record.M}";

        string breakdown = string.Join(Environment.NewLine,
            s.Sistema.Breakdown(s.ProssimaPuntata).Select(b => $"{b.Nome}: {b.Importo:C}"));

        if (recupero)
        {
            _lblVittorie.Text = $"Vittorie: {s.Vittorie}";
            _lblObiettivo.Text = m.InRecupero
                ? $"RECUPERO → {m.Picco:C} (colpo {m.RecuperoColpi + 1}/{m.K})"
                : $"Picco cassa: {m.Picco:C} – puntata minima";
            _lblPuntata.Text = s.ProssimaPuntata.ToString("C");
            _lblBreakdown.Text = breakdown;
        }
        else
        {
            _lblVittorie.Text = $"Vittorie: {s.Vittorie} / {s.Record.W}";
            _lblObiettivo.Text = $"Obiettivo: {s.Record.BankIniziale * (decimal)s.Tabella!.TargetMultiplier:C} " +
                                 $"(+{(s.Tabella.TargetMultiplier - 1) * 100:0.0}%)";
            // se il piano chiede meno della metà della puntata minima, il minimo domina
            // e la sessione di fatto non segue più il Masaniello
            decimal puntataPiano = s.Bank * (decimal)s.Tabella.StakeFraction(s.Colpi, s.Vittorie);
            bool minimoForzato = puntataPiano < s.Sistema.PuntataMinima(s.Record.Chip) / 2;
            _lblPuntata.Text = s.ProssimaPuntata.ToString("C") + (minimoForzato ? "  ⚠" : "");
            _lblBreakdown.Text = breakdown +
                (minimoForzato
                    ? Environment.NewLine + Environment.NewLine +
                      "⚠ Il piano Masaniello chiederebbe " + puntataPiano.ToString("C") + ":" +
                      Environment.NewLine + "la puntata minima sta forzando il piano." +
                      Environment.NewLine + "Riduci il chip o alza l'obiettivo W."
                    : "");
        }

        foreach (var c in _db.ColpiDiSessione(s.Record.Id))
            _gridColpi.Rows.Add(c.NColpo, c.Numero, c.Esito, c.Puntata.ToString("C"),
                                c.Profitto.ToString("C"), c.BankDopo.ToString("C"), c.Fonte, c.Split);
    }

    // ------------------------------------------------------------- tab Storico

    private TabPage CreaTabStorico()
    {
        var page = new TabPage("Storico");
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 300 };

        var pannello = new Panel { Dock = DockStyle.Fill };
        _lblRiepilogoStorico = new Label { Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
        _gridSessioni = NuovaGriglia();
        _gridSessioni.Columns.AddRange(
            ColTesto("Id", 40), ColTesto("Sistema", 150), ColTesto("Gestione", 80),
            ColTesto("M", 40), ColTesto("W", 40),
            ColTesto("Modalità", 90), ColTesto("Bank iniziale", 100), ColTesto("Bank finale", 100),
            ColTesto("Profitto", 90), ColTesto("Stato", 90), ColTesto("Iniziata", 130), ColTesto("Chiusa", 130));
        _gridSessioni.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _gridSessioni.Rows[e.RowIndex].Cells[0].Value is int id)
                MostraColpi(id);
        };
        pannello.Controls.Add(_gridSessioni);
        pannello.Controls.Add(_lblRiepilogoStorico);

        _plotStorico = new FormsPlot { Dock = DockStyle.Fill };

        split.Panel1.Controls.Add(pannello);
        split.Panel2.Controls.Add(_plotStorico);
        page.Controls.Add(split);
        return page;
    }

    private void AggiornaStorico()
    {
        var sessioni = _db.TutteLeSessioni();
        _gridSessioni.Rows.Clear();

        var chiuse = sessioni.Where(s => s.BankFinale.HasValue).ToList();
        decimal profittoTotale = chiuse.Sum(s => s.BankFinale!.Value - s.BankIniziale);
        int vinte = chiuse.Count(s => s.Stato == Database.StatoVinta);
        _lblRiepilogoStorico.Text = chiuse.Count == 0
            ? "Nessuna sessione chiusa."
            : $"Sessioni chiuse: {chiuse.Count}   Vinte: {vinte} ({vinte * 100.0 / chiuse.Count:0.0}%)   Profitto totale: {profittoTotale:C}";

        foreach (var s in sessioni)
        {
            string nomeSistema = s.Sistema switch
            {
                Catalog.CodiceQuasiTutto => "Quasi tutto",
                Catalog.CodicePiuPieno => "S3 +pieno",
                Catalog.CodicePiuPieni => "S4 +2 pieni",
                _ => "Dozzine + Sestina",
            };
            bool recupero = s.Gestione == GestionePuntataExt.CodiceRecuperoPicco;
            _gridSessioni.Rows.Add((int)s.Id, nomeSistema, recupero ? $"Recupero K{s.KRecupero}" : "Classico",
                s.M, recupero ? "–" : s.W.ToString(), s.ModalitaRischio,
                s.BankIniziale.ToString("C"), s.BankFinale?.ToString("C") ?? "",
                s.BankFinale.HasValue ? (s.BankFinale.Value - s.BankIniziale).ToString("C") : "",
                s.Stato, s.IniziataIl, s.ChiusaIl ?? "");
        }

        // equity: andamento della cassa attraverso le sessioni chiuse
        var plt = _plotStorico.Plot;
        plt.Clear();
        if (chiuse.Count > 0)
        {
            double[] xs = Enumerable.Range(1, chiuse.Count).Select(i => (double)i).ToArray();
            double[] ys = chiuse.Select(s => (double)s.BankFinale!.Value).ToArray();
            var sc = plt.Add.Scatter(xs, ys);
            sc.LineWidth = 2;
            plt.Title("Andamento della cassa (bank finale per sessione)");
            plt.XLabel("Sessione");
            plt.YLabel("Bank (€)");
            plt.Axes.AutoScale();
        }
        _plotStorico.Refresh();
    }

    private void MostraColpi(int sessioneId)
    {
        var colpi = _db.ColpiDiSessione(sessioneId);
        using var f = new Form
        {
            Text = $"Colpi sessione {sessioneId}",
            Size = new Size(900, 500),
            StartPosition = FormStartPosition.CenterParent,
        };
        var g = NuovaGriglia();
        g.Columns.AddRange(
            ColTesto("#", 40), ColTesto("Numero", 60), ColTesto("Esito", 50),
            ColTesto("Puntata", 80), ColTesto("Profitto", 80), ColTesto("Bank dopo", 90),
            ColTesto("Fonte", 80), ColTesto("Ripartizione", 330));
        foreach (var c in colpi)
            g.Rows.Add(c.NColpo, c.Numero, c.Esito, c.Puntata.ToString("C"),
                       c.Profitto.ToString("C"), c.BankDopo.ToString("C"), c.Fonte, c.Split);
        f.Controls.Add(g);
        f.ShowDialog(this);
    }

    // ---------------------------------------------------------- tab MonteCarlo

    private TabPage CreaTabMonteCarlo()
    {
        var page = new TabPage("MonteCarlo");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var barra = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        barra.Controls.Add(NuovaEtichetta("Sessioni:", 0, 0, inline: true));
        _numMcSessioni = new NumericUpDown { Minimum = 100, Maximum = 100000, Value = 10000, Increment = 1000, Width = 80 };
        barra.Controls.Add(_numMcSessioni);
        barra.Controls.Add(NuovaEtichetta("Modalità:", 0, 0, inline: true));
        _cmbMcModalita = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
        _cmbMcModalita.Items.AddRange(new object[] { "INDIPENDENTE", "ROLLOVER" });
        _cmbMcModalita.SelectedIndex = 0;
        barra.Controls.Add(_cmbMcModalita);
        barra.Controls.Add(NuovaEtichetta("Sorgente:", 0, 0, inline: true));
        _cmbMcSorgente = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        _cmbMcSorgente.Items.AddRange(new object[] { "RANDOM", "PERMANENZA" });
        _cmbMcSorgente.SelectedIndex = 0;
        _cmbMcSorgente.SelectedIndexChanged += (_, _) => AggiornaInfoMc();
        barra.Controls.Add(_cmbMcSorgente);
        _cmbMcPermanenza = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180, Enabled = false };
        _cmbMcPermanenza.SelectedIndexChanged += (_, _) => AggiornaInfoMc();
        barra.Controls.Add(_cmbMcPermanenza);
        var btnAggiungiCfg = new Button { Text = "+ Configurazione", Width = 120 };
        btnAggiungiCfg.Click += (_, _) => AggiungiRigaMcConfig();
        barra.Controls.Add(btnAggiungiCfg);
        var btnRimuoviCfg = new Button { Text = "− Rimuovi", Width = 90 };
        btnRimuoviCfg.Click += (_, _) =>
        {
            if (_gridMcConfig.CurrentRow != null && _gridMcConfig.Rows.Count > 1)
                _gridMcConfig.Rows.Remove(_gridMcConfig.CurrentRow);
        };
        barra.Controls.Add(btnRimuoviCfg);
        _btnMcRun = new Button { Text = "AVVIA MONTECARLO", Width = 160, Height = 30 };
        _btnMcRun.Click += async (_, _) => await EseguiMonteCarlo();
        barra.Controls.Add(_btnMcRun);
        _mcProgress = new ProgressBar { Width = 200, Height = 24, Maximum = 1000 };
        barra.Controls.Add(_mcProgress);
        _lblMcInfo = NuovaEtichetta("", 0, 0, inline: true);
        barra.Controls.Add(_lblMcInfo);

        _gridMcConfig = NuovaGriglia(soloLettura: false);
        var colSis = new DataGridViewComboBoxColumn { HeaderText = "Sistema", Width = 240 };
        colSis.Items.AddRange(NomeS1, NomeS2, NomeS3, NomeS4);
        var colGes = new DataGridViewComboBoxColumn { HeaderText = "Gestione", Width = 130 };
        colGes.Items.AddRange("Masaniello", "Recupero picco");
        var colMod = new DataGridViewComboBoxColumn { HeaderText = "Modalità", Width = 110 };
        colMod.Items.AddRange("Prudente", "Intermedia", "Aggressiva", "Ultra");
        _gridMcConfig.Columns.AddRange(colSis, colGes,
            ColTesto("M", 50, editabile: true), ColTesto("W", 50, editabile: true),
            ColTesto("K", 50, editabile: true), colMod);

        _gridMcStats = NuovaGriglia();
        _gridMcStats.Columns.AddRange(
            ColTesto("Configurazione", 240), ColTesto("Sessioni", 70), ColTesto("% vinte", 70),
            ColTesto("Profitto medio", 100), ColTesto("Profitto max", 95), ColTesto("Perdita max", 95),
            ColTesto("DD medio", 85), ColTesto("DD max", 85), ColTesto("ROI medio %", 90), ColTesto("Note", 150));

        _plotMc = new FormsPlot { Dock = DockStyle.Fill };

        layout.Controls.Add(barra, 0, 0);
        layout.Controls.Add(_gridMcConfig, 0, 1);
        layout.Controls.Add(_gridMcStats, 0, 2);
        layout.Controls.Add(_plotMc, 0, 3);
        page.Controls.Add(layout);

        AggiungiRigaMcConfig(NomeS1, "Masaniello", 20, 15, modalita: "Ultra");
        AggiungiRigaMcConfig(NomeS2, "Masaniello", 20, 17, modalita: "Ultra");
        return page;
    }

    private void AggiungiRigaMcConfig(string? sistema = null, string gestione = "Masaniello",
                                      int m = 20, int w = 15, int k = 10, string modalita = "Ultra") =>
        _gridMcConfig.Rows.Add(sistema ?? NomeS1, gestione, m, w, k, modalita);

    private List<McConfig> LeggiMcConfigs()
    {
        var configs = new List<McConfig>();
        foreach (DataGridViewRow riga in _gridMcConfig.Rows)
        {
            if (riga.IsNewRow) continue;
            string nomeSis = riga.Cells[0].Value?.ToString() ?? NomeS1;
            int idxSis = new[] { NomeS1, NomeS2, NomeS3, NomeS4 }.ToList().IndexOf(nomeSis);
            if (idxSis < 0) idxSis = 0;
            bool recupero = riga.Cells[1].Value?.ToString() == "Recupero picco";
            int.TryParse(riga.Cells[3].Value?.ToString(), out int w);
            int.TryParse(riga.Cells[4].Value?.ToString(), out int k);
            if (!int.TryParse(riga.Cells[2].Value?.ToString(), out int m) || m <= 0 ||
                (!recupero && (w <= 0 || w > m)) || (recupero && k <= 0))
                throw new ArgumentException(recupero
                    ? $"Riga {riga.Index + 1}: M e K non validi (servono M > 0 e K > 0)."
                    : $"Riga {riga.Index + 1}: M e W non validi (serve 0 < W <= M).");

            int terzina = (int)CfgDec("terzina", 31);
            var (p1, p2) = PieniConfigurati();
            var sistema = idxSis switch
            {
                1 => Catalog.QuasiTutto(terzina),
                2 => Catalog.PiuPieno(terzina, p1),
                3 => Catalog.PiuPieni(terzina, p1, p2),
                _ => Catalog.DozzineSestina((int)CfgDec("sestina", 25)),
            };
            var modalita = ModalitaRischioExt.Parse(riga.Cells[5].Value?.ToString() ?? "Ultra");
            string sigla = $"S{idxSis + 1}";
            configs.Add(recupero
                ? new McConfig($"{sigla} Rec M{m} K{k} {modalita}", sistema, m, 0, modalita,
                               GestionePuntata.RecuperoPicco, k)
                : new McConfig($"{sigla} {m}/{w} {modalita}", sistema, m, w, modalita));
        }
        if (configs.Count == 0) throw new ArgumentException("Aggiungi almeno una configurazione.");
        return configs;
    }

    private void AggiornaInfoMc()
    {
        bool suPermanenza = _cmbMcSorgente.SelectedIndex == 1;
        _cmbMcPermanenza.Enabled = suPermanenza;
        _numMcSessioni.Enabled = !suPermanenza;
        _lblMcInfo.Text = "";
        if (suPermanenza && _cmbMcPermanenza.SelectedItem is PermItem item)
        {
            try
            {
                var configs = LeggiMcConfigs();
                int n = MonteCarloRunner.SessioniDaPermanenza(configs, item.Rec.NColpi);
                _lblMcInfo.Text = $"{item.Rec.NColpi} colpi → {n} sessioni";
            }
            catch { /* configurazioni non ancora valide */ }
        }
    }

    private async Task EseguiMonteCarlo()
    {
        List<McConfig> configs;
        try { configs = LeggiMcConfigs(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "MonteCarlo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int nSessioni = (int)_numMcSessioni.Value;
        var modalita = _cmbMcModalita.SelectedIndex == 1 ? McModalita.Rollover : McModalita.Indipendente;
        decimal bank0 = CfgDec("bank", 100m), chip = CfgDec("chip", 1m);
        int seed = Environment.TickCount;

        bool suPermanenza = _cmbMcSorgente.SelectedIndex == 1;
        int[] numeriPermanenza = [];
        string descrizione = modalita.ToString().ToUpperInvariant();
        if (suPermanenza)
        {
            if (_cmbMcPermanenza.SelectedItem is not PermItem item)
            {
                MessageBox.Show("Seleziona la permanenza da usare (caricala nella tab Config).",
                                "MonteCarlo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            numeriPermanenza = _db.NumeriPermanenza(item.Rec.Id);
            descrizione += $" PERMANENZA:{item.Rec.Nome}";
            seed = 0; // nessun random: i numeri sono quelli del file
        }

        _btnMcRun.Enabled = false;
        _mcProgress.Value = 0;
        var progresso = new Progress<double>(p => _mcProgress.Value = Math.Min(1000, (int)(p * 1000)));

        try
        {
            var risultati = await Task.Run(() => suPermanenza
                ? MonteCarloRunner.EseguiSuPermanenza(configs, numeriPermanenza, modalita, bank0, chip, progresso)
                : MonteCarloRunner.Esegui(configs, nSessioni, modalita, bank0, chip, seed, progresso));

            long runId = _db.InserisciMcRun(risultati[0].Stat.Sessioni, descrizione, seed, bank0, chip);
            _gridMcStats.Rows.Clear();
            foreach (var r in risultati)
            {
                var s = r.Stat;
                _db.InserisciMcStat(new McStatRecord(runId, s.Etichetta, s.Sessioni, s.PctVinte,
                    s.ProfittoMedio, s.ProfittoMax, s.PerditaMax, s.DdMedio, s.DdMax, s.RoiMedioPct));
                string note = s.Bancarotta ? "BANCAROTTA" :
                    s.BankFinaleRollover is decimal bf ? $"Bank finale: {bf:C}" : "";
                _gridMcStats.Rows.Add(s.Etichetta, s.Sessioni, s.PctVinte.ToString("0.00"),
                    s.ProfittoMedio.ToString("C"), s.ProfittoMax.ToString("C"), s.PerditaMax.ToString("C"),
                    s.DdMedio.ToString("C"), s.DdMax.ToString("C"), s.RoiMedioPct.ToString("0.00"), note);
            }

            DisegnaGraficoMc(risultati, modalita, bank0);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "MonteCarlo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnMcRun.Enabled = true;
            _mcProgress.Value = _mcProgress.Maximum;
        }
    }

    private void DisegnaGraficoMc(List<McRisultato> risultati, McModalita modalita, decimal bank0)
    {
        var plt = _plotMc.Plot;
        plt.Clear();

        if (modalita == McModalita.Rollover)
        {
            // curva della cassa attraverso le sessioni
            foreach (var r in risultati)
            {
                double bank = (double)bank0;
                var ys = new List<double> { bank };
                foreach (var s in r.Sessioni) { bank += (double)s.Profitto; ys.Add(bank); }
                double[] xs = Enumerable.Range(0, ys.Count).Select(i => (double)i).ToArray();
                var sc = plt.Add.Scatter(xs, ys.ToArray());
                sc.LegendText = r.Config.Etichetta;
                sc.MarkerSize = 0;
                sc.LineWidth = 2;
            }
            plt.Title("Evoluzione della cassa (rollover)");
            plt.XLabel("Sessione");
            plt.YLabel("Bank (€)");
        }
        else
        {
            // distribuzione dei profitti per sessione
            foreach (var r in risultati)
            {
                var profitti = r.Sessioni.Select(s => (double)s.Profitto).ToArray();
                if (profitti.Length == 0) continue;
                double min = profitti.Min(), max = profitti.Max();
                if (min == max) { min -= 1; max += 1; }
                const int nBin = 40;
                double passo = (max - min) / nBin;
                var conteggi = new double[nBin];
                foreach (double p in profitti)
                {
                    int b = (int)((p - min) / passo);
                    if (b >= nBin) b = nBin - 1;
                    conteggi[b]++;
                }
                double[] centri = Enumerable.Range(0, nBin).Select(i => min + passo * (i + 0.5)).ToArray();
                var sc = plt.Add.Scatter(centri, conteggi);
                sc.LegendText = r.Config.Etichetta;
                sc.LineWidth = 2;
                sc.MarkerSize = 0;
            }
            plt.Title("Distribuzione dei profitti per sessione");
            plt.XLabel("Profitto (€)");
            plt.YLabel("Numero di sessioni");
        }

        plt.ShowLegend();
        plt.Axes.AutoScale();
        _plotMc.Refresh();
    }

    // -------------------------------------------------------------- tab Config

    private TabPage CreaTabConfig()
    {
        var page = new TabPage("Config");
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12),
        };

        var gb = NuovoGroupBox("Impostazioni", 420, 280);
        gb.Controls.Add(NuovaEtichetta("Bankroll iniziale (€):", 12, 30));
        _txtBank = new TextBox { Width = 100, Left = 220, Top = 27 };
        gb.Controls.Add(_txtBank);
        gb.Controls.Add(NuovaEtichetta("Gettone minimo (€):", 12, 62));
        _txtChip = new TextBox { Width = 100, Left = 220, Top = 59 };
        gb.Controls.Add(_txtChip);
        _lblInfoChip = new Label
        {
            AutoSize = true,
            Left = 220,
            Top = 76,
            ForeColor = Color.DimGray,
            Font = new Font(Font.FontFamily, 7.5f),
            Text = "",
        };
        gb.Controls.Add(_lblInfoChip);
        _txtChip.TextChanged += (_, _) => AggiornaInfoChip();
        gb.Controls.Add(NuovaEtichetta("Rollover cassa:", 12, 94));
        _cmbRollover = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100, Left = 220, Top = 91 };
        _cmbRollover.Items.AddRange(new object[] { "SI", "NO" });
        gb.Controls.Add(_cmbRollover);
        gb.Controls.Add(NuovaEtichetta("Sestina (sistema 1):", 12, 126));
        _cmbSestina = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100, Left = 220, Top = 123 };
        _cmbSestina.Items.AddRange(new object[] { "25-30", "31-36" });
        gb.Controls.Add(_cmbSestina);
        gb.Controls.Add(NuovaEtichetta("Terzina (sistema 2):", 12, 158));
        _cmbTerzina = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100, Left = 220, Top = 155 };
        _cmbTerzina.Items.AddRange(new object[] { "31-33", "34-36" });
        gb.Controls.Add(_cmbTerzina);
        gb.Controls.Add(NuovaEtichetta("Pieni residui (S3/S4):", 12, 190));
        _cmbPieni = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 188, Left = 220, Top = 187 };
        _cmbPieni.Items.AddRange(new object[]
        {
            "34+35 (scoperti 0 e 36)", "34+36 (scoperti 0 e 35)", "35+36 (scoperti 0 e 34)",
        });
        gb.Controls.Add(_cmbPieni);

        var btnSalva = new Button { Text = "SALVA", Width = 388, Height = 36, Left = 12, Top = 224 };
        btnSalva.Click += (_, _) => SalvaConfig();
        gb.Controls.Add(btnSalva);

        // --- permanenze reali ---
        var gbPerm = NuovoGroupBox("Permanenze reali (sequenze di numeri da casinò veri)", 560, 270);
        _gridPermanenze = NuovaGriglia();
        _gridPermanenze.Dock = DockStyle.None;
        _gridPermanenze.SetBounds(12, 24, 536, 190);
        _gridPermanenze.Columns.AddRange(
            ColTesto("Id", 40), ColTesto("Nome", 230), ColTesto("Colpi", 60),
            ColTesto("Cursore", 70), ColTesto("Caricata il", 130));
        var btnCarica = new Button { Text = "CARICA PERMANENZA…", Width = 180, Height = 30, Left = 12, Top = 224 };
        btnCarica.Click += (_, _) => CaricaPermanenzaDaFile();
        var btnRiavvolgi = new Button { Text = "RIAVVOLGI", Width = 100, Height = 30, Left = 200, Top = 224 };
        btnRiavvolgi.Click += (_, _) => RiavvolgiPermanenza();
        var btnElimina = new Button { Text = "ELIMINA", Width = 100, Height = 30, Left = 308, Top = 224 };
        btnElimina.Click += (_, _) => EliminaPermanenza();
        gbPerm.Controls.AddRange(new Control[] { _gridPermanenze, btnCarica, btnRiavvolgi, btnElimina });

        var nota = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Margin = new Padding(4, 16, 4, 4),
            Text = "Nota: su ogni puntata il banco trattiene in media il 2,70% (lo 0 e i numeri " +
                   "scoperti). Il Masaniello gestisce il rischio e la disciplina della cassa, " +
                   "ma nessun sistema elimina il margine della roulette: usa il MonteCarlo " +
                   "per vedere i numeri reali e gioca solo ciò che puoi permetterti di perdere.",
        };

        flow.Controls.Add(gb);
        flow.Controls.Add(gbPerm);
        flow.Controls.Add(nota);
        page.Controls.Add(flow);
        return page;
    }

    /// <summary>Ricarica l'elenco delle permanenze nei combo e nella griglia, conservando la selezione.</summary>
    private void CaricaPermanenze()
    {
        var permanenze = _db.TutteLePermanenze();

        foreach (var cmb in new[] { _cmbPermSessione, _cmbMcPermanenza })
        {
            long? selezionata = (cmb.SelectedItem as PermItem)?.Rec.Id;
            cmb.Items.Clear();
            foreach (var p in permanenze)
            {
                int i = cmb.Items.Add(new PermItem(p));
                if (p.Id == selezionata) cmb.SelectedIndex = i;
            }
            if (cmb.SelectedIndex < 0 && cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        }

        _gridPermanenze.Rows.Clear();
        foreach (var p in permanenze)
            _gridPermanenze.Rows.Add((int)p.Id, p.Nome, p.NColpi, p.Cursore, p.CaricataIl);

        AggiornaInfoMc();
    }

    private void CaricaPermanenzaDaFile()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Carica permanenza",
            Filter = "Permanenze (*.xls;*.xlsx;*.csv;*.txt)|*.xls;*.xlsx;*.csv;*.txt|Tutti i file|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var numeri = PermanenceLoader.Carica(dlg.FileName);
            _db.InserisciPermanenza(Path.GetFileNameWithoutExtension(dlg.FileName), numeri);
            CaricaPermanenze();
            MessageBox.Show($"Caricati {numeri.Count} colpi.", "Permanenza",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message + Environment.NewLine + Environment.NewLine +
                "Se il file viene da laroulette.it e non viene riconosciuto, " +
                "prova anche il download \"file di testo\".",
                "Permanenza", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private long? PermanenzaSelezionataInGriglia() =>
        _gridPermanenze.CurrentRow?.Cells[0].Value is int id ? id : null;

    private void RiavvolgiPermanenza()
    {
        if (PermanenzaSelezionataInGriglia() is not long id) return;
        _db.AggiornaCursorePermanenza(id, 0);
        CaricaPermanenze();
    }

    private void EliminaPermanenza()
    {
        if (PermanenzaSelezionataInGriglia() is not long id) return;
        if (MessageBox.Show("Eliminare la permanenza selezionata?", "Permanenze",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _db.EliminaPermanenza(id);
        CaricaPermanenze();
    }

    private void CaricaConfigNeiCampi()
    {
        _txtBank.Text = CfgDec("bank", 100m).ToString(CultureInfo.CurrentCulture);
        _txtChip.Text = CfgDec("chip", 1m).ToString(CultureInfo.CurrentCulture);
        _cmbRollover.SelectedIndex = Cfg("rollover", "SI") == "NO" ? 1 : 0;
        _cmbSestina.SelectedIndex = (int)CfgDec("sestina", 25) == 31 ? 1 : 0;
        _cmbTerzina.SelectedIndex = (int)CfgDec("terzina", 31) == 34 ? 1 : 0;
        _cmbPieni.SelectedIndex = PieniConfigurati() switch
        {
            (34, 36) => 1,
            (35, 36) => 2,
            _ => 0,
        };
        AggiornaInfoChip();
    }

    /// <summary>Mostra la puntata minima che il gettone configurato rende possibile.</summary>
    private void AggiornaInfoChip()
    {
        if (!ProvaParseImporto(_txtChip.Text, out decimal chip) || chip <= 0)
        {
            _lblInfoChip.Text = "";
            return;
        }
        _lblInfoChip.Text = $"Puntata minima: S1 {5 * chip:C} · S2 {11 * chip:C} · S3 {34 * chip:C} · S4 {35 * chip:C}";
    }

    private void SalvaConfig()
    {
        if (!ProvaParseImporto(_txtBank.Text, out decimal bank) || bank <= 0)
        {
            MessageBox.Show("Bankroll non valido.", "Config", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!ProvaParseImporto(_txtChip.Text, out decimal chip) || chip <= 0 || chip != Math.Round(chip, 2))
        {
            MessageBox.Show(
                "Valore gettone non valido.\n\nIndica il valore del gettone più piccolo " +
                "utilizzabile sul tavolo (es. 0,10 €), con al massimo 2 decimali.",
                "Config", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _db.SetConfig("bank", bank.ToString(CultureInfo.InvariantCulture));
        _db.SetConfig("chip", chip.ToString(CultureInfo.InvariantCulture));
        _db.SetConfig("rollover", _cmbRollover.SelectedIndex == 1 ? "NO" : "SI");
        _db.SetConfig("sestina", _cmbSestina.SelectedIndex == 1 ? "31" : "25");
        _db.SetConfig("terzina", _cmbTerzina.SelectedIndex == 1 ? "34" : "31");
        _db.SetConfig("pieni", _cmbPieni.SelectedIndex switch
        {
            1 => "34,36",
            2 => "35,36",
            _ => "34,35",
        });

        AggiornaPannelloSessione();
        AggiornaEtichettaEsatte();
        MessageBox.Show("Impostazioni salvate.", "Config", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ---------------------------------------------------------------- helpers

    private static GroupBox NuovoGroupBox(string titolo, int larghezza, int altezza) =>
        new() { Text = titolo, Width = larghezza, Height = altezza, Margin = new Padding(4, 4, 4, 8) };

    private static Label NuovaEtichetta(string testo, int x, int y, bool grassetto = false, bool inline = false)
    {
        var l = new Label { Text = testo, Left = x, Top = y, AutoSize = true };
        if (grassetto) l.Font = new Font(l.Font, FontStyle.Bold);
        if (inline) { l.Anchor = AnchorStyles.Left; l.Margin = new Padding(8, 8, 2, 0); }
        return l;
    }

    private static DataGridView NuovaGriglia(bool soloLettura = true) => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = soloLettura,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
    };

    private static DataGridViewTextBoxColumn ColTesto(string titolo, int larghezza, bool editabile = false) =>
        new() { HeaderText = titolo, Width = larghezza, ReadOnly = !editabile };
}
