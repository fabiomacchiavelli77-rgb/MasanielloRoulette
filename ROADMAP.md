# ROADMAP.md — MasanielloRoulette (piani multi-copertura)

Checklist dell'applicazione del piano `Implementazione.md` (derivato da
IL_GIOCATORE, 2026-08-19). Aggiornare lo stato a fine fase.

## Convenzioni repo

- Tutto in italiano (UI, docs, nomi pubblici dove naturale).
- Core zero-UI; test xUnit in `MasanielloApp`; `dotnet test MasanielloApp.slnx`.
- DB SQLite: schema stabile, nessuna migrazione se evitabile.
- Commit: messaggi italiani brevi (stile storico repo).

## FASI

### FASE 1 — Core: sistemi S3/S4 + scommesse esatte
- [x] 1.1 `Catalog`: `PiuPieno` (34/37, unità 12/12/6/3/1), `PiuPieni`
      (35/37, +1 unità), validazioni pieni, `CodificaParametro`/`Crea`
      (encoding t*10000+p1*100+p2, schema DB invariato).
- [x] 1.2 `NotaPianiImpossibili` + rifiuto 36/37-37/37 (già in BettingSystem,
      test esplicito).
- [x] 1.3 `Engine/ScommesseEsatte.cs`: corta/media/esatta per coperti.
- [x] 1.4 Test `PianiNuoviTests.cs` (13): quote, scoperti, profitti per numero,
      minimo, validazioni, roundtrip, degenere, esatte, tabella S4.

**Accettazione**: `dotnet test` verde (57), valori tabella §1.2/§1.4.

### FASE 2 — UI
- [x] 2.1 Combo S1..S4 in tab Sessione + mapping codice/parametro.
- [x] 2.2 Etichetta scommesse esatte + bottone USA ESATTA (media su M/W).
- [x] 2.3 Config: combo pieni residui (34+35 / 34+36 / 35+36), chiave `pieni`.
- [x] 2.4 Info gettone con S3/S4; MC combo + sigle; Storico nomi.

**Accettazione**: build 0 errori; S4 selezionabile, esatte 18/19 visibili.

### FASE 3 — Docs
- [x] 3.1 README: 4 sistemi, tabella esatte, copertura massima 35/37, fix
      riga build (44→57 test).
- [x] 3.2 `Implementazione.md` + `ROADMAP.md` (questo file).
- [x] 3.3 AGENTS.md/CLAUDE.md: struttura aggiornata (Engine + ScommesseEsatte,
      Catalog 4 sistemi, test 57).

### FASE 4 — Finale
- [x] 4.1 `dotnet test` verde, build 0 errori.
- [x] 4.2 Smoke run app.
- [x] 4.3 Commit (+ push se richiesto).

## Stato

| Fase | Stato | Data | Note |
|---|---|---|---|
| 1 | completata | 2026-08-19 | Motore Masaniello locale già equalizzato: nessun fix necessario (vedi Implementazione §1.5) |
| 2 | completata | 2026-08-19 | Schema DB invariato (parametro unico codificato) |
| 3 | completata | 2026-08-19 | |
| 4 | completata | 2026-08-19 | 57/57 test, smoke OK |
