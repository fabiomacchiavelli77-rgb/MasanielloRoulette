# Implementazione.md — Piani multi-copertura e scommesse esatte (MasanielloRoulette)

Applicazione a questo repo del piano sviluppato e verificato in `IL_GIOCATORE`
(Implementazione.md/ROADMAP.md del 2026-08-19). Tutti i numeri verificati con
script di calcolo, non copiati da fonti. Companion operativo: `ROADMAP.md`.

---

## 1. Matematica (verificata)

### 1.1 Legge equalizzata

Piano = moltiplicatori interi di un'unità base (dozzina 12, sestina 6, terzina 3,
pieno 1). Se T = Σ coefficienti = numeri coperti:

```
ritorno lordo  = 36 unità (sempre, qualeunque segmento vince)
netto/vinto    = (36 − coperti) unità
EV per colpo   = −T/37 (house edge −2,70% invariante)
```

### 1.2 I quattro sistemi

| | Coperti | Unità | T | q netta | Scoperti (default) |
|---|---|---|---|---|---|
| S1 Dozzine+Sestina | 30/37 | 2/2/1 (ridotte) | 5 | +20% | 0 + 6 residui |
| S2 Quasi tutto | 33/37 | 4/4/2/1 (ridotte) | 11 | +9,09% | 0 + 3 residui |
| **S3 +1 pieno** | 34/37 | 12/12/6/3/1 | 34 | +5,88% | 0 + 2 residui |
| **S4 +2 pieni** | 35/37 | 12/12/6/3/1/1 | 35 | +2,86% | **solo 0 e 36** |

S3/S4 non riducibili (coefficienti già primi tra loro). Rotazioni: la terzina
può stare a 31-33 o 34-36; i pieni sui residui corrispondenti (config tab).

### 1.3 Copertura massima = 35/37

- 36/37 → ritorno 36 su puntata 36: **netto 0** (scudo, nessuna vincita).
- 37/37 → **perdita certa 1 unità a colpo** (varianza zero).
- Il `BettingSystem` li rifiuta già in costruzione ("Sistema senza vincita"),
  test `PianiOltre35Numeri_Rifiutati_NessunaVincita`. Coprire di più riduce la
  varianza, NON cambia l'EV: sempre −T/37.

### 1.4 Scommessa più esatta (W/M → p = coperti/37)

W/M ≈ p → puntate Masaniello quasi costanti. Sotto p: aggressivo. Sopra: conservativo.

| Sistema | corta (M≤12) | media (M≤30) | esatta |
|---|---|---|---|
| S1 30/37 | 9/11 (±0,74pp) | 17/21 (±0,13pp) | 30/37 |
| S2 33/37 | 8/9 (±0,30pp) | 25/28 (±0,10pp) | 33/37 |
| S3 34/37 | 11/12 (±0,23pp) | 23/25 (±0,11pp) | 34/37 |
| S4 35/37 | 11/12 (±2,93pp) | 18/19 (±0,14pp) | 35/37 |

Implementato in `Engine/ScommesseEsatte.cs` + bottone "USA ESATTA" in UI.

### 1.5 Motore Masaniello — già corretto

La `MasanielloTable` di questo repo implementa la ricorsione equalizzata
(`StakeFraction = (b−a)/(b+q_net·a)` con a/b valori vinto/perso): per q=1
coincide con la formula classica, per quote basse (S2/S3/S4) NON chiede mai
più della cassa. Nota: IL_GIOCATORE aveva la formula classica "da manuale"
che con quote basse richiedeva fino a 32× la cassa — corretta lì con la
stessa ricorsione già usata qui. Nessuna modifica necessaria a questo repo.

### 1.6 Arrotondamento al gettone

Già presente in `StakeCalculator`: puntata sempre multipla di `UnitaTotali ×
chip` (nearest multiple, floor sui cap modalità/banca). S3/S4 ereditano il
comportamento senza modifiche.

---

## 2. Cosa è stato applicato (2026-08-19)

### Core (`Masaniello.Core`)

| File | Modifica |
|---|---|
| `Systems/Catalog.cs` | + `CodicePiuPieno`/`CodicePiuPieni`, `PiuPieno()` (34/37), `PiuPieni()` (35/37), validazioni pieni (residui terza dozzina, non sulla terzina, non duplicati), `NotaPianiImpossibili` (36/37-37/37), `CodificaParametro(t,p1,p2)` + `Crea` estesa (encoding `t*10000+p1*100+p2`, schema DB invariato) |
| `Engine/ScommesseEsatte.cs` | NUOVO: `Suggerite(numeriCoperti)` → corta/media/esatta |
| `Masaniello.Tests/PianiNuoviTests.cs` | NUOVO: 13 test (quote/unità, scoperti, profitto reale per ogni numero, puntata minima, validazioni, roundtrip Crea, rifiuto 36/37-37/37, scommesse esatte, tabella Masaniello S4) |

### UI (`Masaniello.App/MainForm.cs`)

- Combo sistema: S1..S4 (+ S3 "34 numeri, +5,88%", S4 "35 numeri, +2,86%").
- Etichetta "Esatte: corta 9/11 · media 17/21 · esatta 30/37" aggiornata sul
  sistema selezionato + bottone **USA ESATTA** (applica la media a M/W).
- Tab Config: combo **"Pieni residui (S3/S4)"** → `34+35` (scoperti 0 e 36,
  default) / `34+36` / `35+36`, salvata in config `pieni` come "34,35".
- Info gettone: puntata minima anche S3 (34×chip) e S4 (35×chip).
- MonteCarlo: S3/S4 nelle combo config; sigle S3/S4.
- Storico: nome sistema per S3/S4.

### Cosa NON serve (già presente qui)

- Persistenza SQLite di sessioni/colpi/config → nessun nuovo store.
- Arrotondamento chip → StakeCalculator già corretto.
- Resume sessione → SessionService.RiprendiSessioneInCorso già c'è.

---

## 3. Verifica

```
cd MasanielloApp
dotnet test        # 57 test verdi (44 + 13 nuovi)
dotnet run --project Masaniello.App   # smoke: S4 selezionabile, esatte 18/19
```

Caso d'uso chiave: S4 con pieni 34+35 → scoperti solo 0 e 36, q=36/35,
scommessa esatta 18/19, gettone es. 0,10€ → puntata minima 3,50€.
