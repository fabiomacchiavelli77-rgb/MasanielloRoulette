# Masaniello Roulette

Sistema Masaniello applicato alla roulette europea, in due implementazioni:
**Excel VBA** (storica, funzionante) e **app desktop .NET** (attuale, in sviluppo).

Applicazione WinForms scritta in C# (.NET 10) che implementa:

- **Quattro sistemi di puntata a ritorno costante:**
  - **S1 "Dozzine + Sestina"** — 30/37 coperti (81,1%), vincita +20%, 5 chip
  - **S2 "Quasi tutto"** — 33/37 coperti (89,2%), vincita +9,09%, 11 chip
  - **S3 "Quasi tutto + 1 pieno"** — 34/37 coperti (91,9%), vincita +5,88%, 34 chip
  - **S4 "Quasi tutto + 2 pieni"** — 35/37 coperti (94,6%), vincita +2,86%, 35 chip;
    con i pieni 34+35 restano scoperti **solo 0 e 36** — copertura massima con profitto

  Oltre 35 numeri non esiste sistema: 36/37 restituisce esattamente la puntata
  (netto 0), 37/37 perde 1 unità a colpo con certezza (verificato nei test).
  La posizione di sestina, terzina e pieni è configurabile nella tab Config.

- **Scommessa esatta**: per ogni sistema la coppia W/M più vicina a coperti/37
  (es. S4 → 18/19), applicabile con un click — mantiene le puntate quasi costanti.

- **Due gestioni di puntata:**
  - Masaniello classico (M colpi / W vittorie, tabella V)
  - Recupero del picco (minimo + mini-piano di recupero)

- **MonteCarlo comparativo** con common random numbers
- **Backtest su permanenze reali** (laroulette.it permanenzimetro)
- **Rollover cassa** tra sessioni
- **SQLite** per persistenza

## Matematica

EV = -1/37 (-2,70%) per tutti i sistemi. Il Masaniello gestisce la disciplina della cassa ma non lo elimina.

Scommesse più esatte (W/M più vicino a coperti/37):

| Sistema | p | corta (M≤12) | media (M≤30) | esatta |
|---|---|---|---|---|
| S1 30/37 | 0,8108 | 9/11 | 17/21 | 30/37 |
| S2 33/37 | 0,8919 | 8/9 | 25/28 | 33/37 |
| S3 34/37 | 0,9189 | 11/12 | 23/25 | 34/37 |
| S4 35/37 | 0,9460 | 11/12 | 18/19 | 35/37 |

| W su 20 | S1 (q=1,2) | S2 (q=12/11) |
|---|---|---|
| 13 | +1,14%% | ~0%% |
| 14 | +3,86%% | ~0%% |
| 15 | +11,34%% | +0,47%% |
| 16 | +30,08%% | +2,20%% |
| 17 | +76,51%% | +8,65%% |
| 18 | +204,27%% | +29,86%% |

## Build

```
cd MasanielloApp
dotnet test                     # 57 test verdi
dotnet publish Masaniello.App -c Release -o publish
```

Richiede .NET 10 preview SDK.

## Avvio

```
MasanielloApp/publish/Masaniello.App.exe
```

---
Utente: Fabio — progetto hobbistico, UI in italiano.
