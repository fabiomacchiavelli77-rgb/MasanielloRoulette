# Progetto Masaniello Roulette

Sistema Masaniello applicato alla roulette europea, in due implementazioni:
**Excel VBA** (storica, funzionante) e **app desktop .NET** (attuale, in sviluppo).
Utente: Fabio, hobbista italiano — tutta la UI e la documentazione in italiano.

## File e struttura

```
C:\Progetti AI\MasanielloRoulette\
├── Masaniello.xlsm            Excel ORIGINALE (versione ChatGPT, con bug — non toccare)
├── Masaniello_v2.xlsm         Excel corretto e completo (giugno 2026)
├── mod*.bas                   sorgenti VBA allineati a Masaniello_v2.xlsm
├── Manuale_Excel.pdf/.html    manuale utente dell'Excel
├── Manuale_App.pdf/.html      manuale utente dell'app
├── 888-roulette-*.xls         permanenza reale di esempio (2.021 colpi, usata nei test)
└── MasanielloApp\             soluzione .NET (C#, net10.0-windows)
    ├── Avvia Masaniello.bat   launcher (→ publish\Masaniello.App.exe)
    ├── Masaniello.Core\       libreria con TUTTA la logica (zero UI, riusabile)
    │   ├── Engine\            MasanielloTable (tabella V, StakeFraction), StakeCalculator, ScommesseEsatte,
    │   │                      PianoRecupero (W' minimo per tornare al picco in K colpi)
    │   ├── Systems\           BettingSystem (ritorno costante) + Catalog (i 4 sistemi: S1 30/37, S2 33/37, S3 34/37 +pieno, S4 35/37 +2 pieni)
    │   ├── Sessions\          MotoreSessione (macchina a stati condivisa: live, ripresa
    │   │                      da DB e MonteCarlo), SessionService (rollover, replay)
    │   ├── Permanenze\        PermanenceLoader (xls/xlsx/csv/txt, ExcelDataReader,
    │   │                      auto-rilevamento colonna numeri)
    │   ├── Simulation\        MonteCarloRunner (random CRN + backtest su permanenza)
    │   └── Data\              Database SQLite (sessioni, colpi, config, mc_runs, permanenze)
    ├── Masaniello.App\        WinForms: tab Sessione | Storico | MonteCarlo | Config
    ├── Masaniello.Tests\      34 test xUnit (tutti verdi) — eseguire con `dotnet test`
    └── publish\               build Release + masaniello.db (i dati dell'utente!)
```

## I due sistemi di puntata

| | Sistema 1 "Dozzine+Sestina" | Sistema 2 "Quasi tutto" |
|---|---|---|
| Copertura | 1-24 + sestina (25-30 o 31-36) | 1-24 + sestina 25-30 + terzina (31-33 o 34-36) |
| Numeri coperti | 30/37 (81,1%) | 33/37 (89,2%) |
| Unità di puntata | 2/2/1 (totale 5 chip) | 4/4/2/1 (totale 11 chip) |
| Vincita netta | sempre +20% (q = 6/5) | sempre +9,09% (q = 12/11) |
| Scoperti | 0 + sestina opposta | 0 + 3 numeri dell'ultima terzina |

Le unità pareggiano l'incasso (dozzina paga 2:1 → ritorno 3×; sestina 5:1 → 6×;
terzina 11:1 → 12×): qualunque numero coperto esca, l'incasso è identico.
Le puntate sono SEMPRE multiple delle unità totali × chip → vincita esatta.

## Fatti matematici verificati (non ridiscutere, sono testati)

- **Margine casa identico**: per entrambi i sistemi la somma dei profitti sui 37
  numeri = −puntata → EV = −1/37 (−2,70%) del puntato, sempre. Nessun sistema lo elimina.
- **Target Masaniello** (moltiplicatore V(0,0); coincide con la replica risk-neutral 1/P*):

  | W su 20 colpi | S1 (q=1,2) | S2 (q=12/11) |
  |---|---|---|
  | 13 | +1,14% | ~0% |
  | 14 | +3,86% | ~0% |
  | 15 | +11,34% | +0,47% |
  | 16 | +30,08% | +2,20% |
  | 17 | +76,51% | +8,65% |
  | 18 | +204,27% | +29,86% |

- **Trappola della puntata minima**: se il piano chiede meno del minimo (5 o 11 chip),
  il minimo domina e il sistema degenera in flat bet → si paga solo margine.
  Successe col default 20/13 + chip 1€ su cassa 100€. La UI mostra "⚠ minimo forzato".
  Regola pratica: chip piccolo (0,10€) e/o W ambizioso (target ≥ 10%).
- **Bug storici del VBA ChatGPT, corretti in entrambe le implementazioni**: all-in mancante
  nello stato "vincere tutti i rimanenti"; sessioni matematicamente perse che continuavano
  (e andavano all-in); profitto teorico stake/5 invece di quello reale dai chip; puntate
  degeneri sotto i 5 chip; tabella persa alla riapertura del file.

## Le due gestioni di puntata

- **Masaniello classico**: piano M colpi / W vittorie, puntate dalla tabella V.
- **Recupero del picco** (idea di Fabio, giugno 2026): puntata sempre al MINIMO
  (5/11 chip); a ogni vincita si aggiorna il picco di cassa; dopo una perdita un
  mini-piano Masaniello (M'=K colpi, default 10; W'=minimo con V(0,0) ≥ picco/bank)
  riporta la cassa al picco, poi si torna al minimo. Uscita anticipata appena
  bank ≥ picco; piano morto o cassa insufficiente → PERSA. La sessione dura M colpi
  ma un recupero in corso può estenderla fino a M+K. VINTA se bank ≥ bank iniziale
  e non a metà recupero. Tutto in `MotoreSessione` (enum `GestionePuntata`),
  condiviso da sessioni live, ripresa da DB (replay colpi) e MonteCarlo.
- Sul backtest 888 reale (67 sessioni indipendenti, cassa 100€/chip 0,10€): il
  recupero alza %vinte (97% vs 90% S1) e taglia la perdita max, ma EV resta −2,70%.

## Permanenze reali

- Caricamento da Config (xls/xlsx/csv/txt) → tabella `permanenze` (numeri CSV,
  cursore replay persistente). `PermanenceLoader` auto-rileva la colonna dei numeri
  (scarta colonne indice strettamente consecutive).
- Uso: replay colpo-per-colpo in Sessione (pulsante PERMANENZA ▶, fonte colpo
  `PERMANENZA`) e backtest comparativo nel tab MonteCarlo
  (`EseguiSuPermanenza`: blocchi fissi = max ColpiMassimi tra le config, la
  sessione i-esima di ogni config legge il blocco i — confronto equo).
- Verificato sul file 888: le frequenze reali combaciano col modello uniforme
  (scoperti S2 9,95% vs 10,81% teorico) → il MonteCarlo random è affidabile.

## Decisioni prese con l'utente

- Profitto sempre REALE dalla ripartizione effettiva dei chip e dal segmento uscito.
- Modalità rischio default = **Ultra** (Masaniello puro, nessun cap); le altre
  restano selezionabili. Con RecuperoPicco il campo W è nascosto, compare K.
- Rollover cassa: nuova sessione riparte dal bank finale dell'ultima chiusa
  (disattivabile); sessione aperta sovrascritta → archiviata INTERROTTA.
- MonteCarlo comparativo: più configurazioni sulle stesse sequenze di numeri
  (common random numbers, un seed per sessione) — sia INDIPENDENTE che ROLLOVER.
- DB ripartito da zero (nessun import dall'Excel).
- L'Excel resta utilizzabile in parallelo; modifiche nuove solo sull'app .NET.

## Build e verifica

```
cd MasanielloApp
dotnet test                                   # 34 test, devono restare verdi
dotnet publish Masaniello.App -c Release -o publish
```
SDK: .NET 10 preview installato. ScottPlot.WinForms dà avvisi NU1701 (innocui).
Per modificare l'Excel: i .bas vanno convertiti in ANSI prima dell'import via COM
(AccessVBOM già =1); AutomationSecurity=1 per eseguire macro via Application.Run.

## Prossimi passi possibili (dichiarati dall'utente)

- Bot di puntata automatica su siti: riusare Masaniello.Core (DLL separata apposta);
  l'utente ha un vecchio progetto VB da fornire. ⚠ I ToS dei casinò online in genere
  vietano l'automazione (rischio chiusura conto) — discuterne prima di implementare.
