# Masaniello Roulette

Sistema Masaniello applicato alla roulette europea, in due implementazioni:
**Excel VBA** (storica, funzionante) e **app desktop .NET** (attuale, in sviluppo).

Applicazione WinForms scritta in C# (.NET 10) che implementa:

- **Due sistemi di puntata a ritorno costante:**
  - **S1 "Dozzine + Sestina"** — 30/37 coperti (81,1%%), vincita +20%%, 5 chip
  - **S2 "Quasi tutto"** — 33/37 coperti (89,2%%), vincita +9,09%%, 11 chip

- **Due gestioni di puntata:**
  - Masaniello classico (M colpi / W vittorie, tabella V)
  - Recupero del picco (minimo + mini-piano di recupero)

- **MonteCarlo comparativo** con common random numbers
- **Backtest su permanenze reali** (laroulette.it permanenzimetro)
- **Rollover cassa** tra sessioni
- **SQLite** per persistenza

## Matematica

EV = -1/37 (-2,70%%) per entrambi i sistemi. Il Masaniello gestisce la disciplina della cassa ma non lo elimina.

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
dotnet test                    # 44 test verdindotnet publish Masaniello.App -c Release -o publish
```

Richiede .NET 10 preview SDK.

## Avvio

```
MasanielloApp/publish/Masaniello.App.exe
```

---
Utente: Fabio — progetto hobbistico, UI in italiano.
