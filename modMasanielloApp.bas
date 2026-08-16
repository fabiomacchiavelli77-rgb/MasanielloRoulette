Attribute VB_Name = "modMasanielloApp"
Option Explicit

' Masaniello per roulette – 2 dozzine + 1 sestina (40/40/20)

Private Const SH_CONFIG  As String = "Config"
Private Const SH_DASH    As String = "Dashboard"
Private Const SH_SESSION As String = "Session"
Private Const SH_STORICO As String = "Storico"

Private Const C_M       As String = "B4"
Private Const C_W       As String = "B5"
Private Const C_BANK0   As String = "B6"
Private Const C_CHIP    As String = "B7"
Private Const C_MAXPCT  As String = "B8"
Private Const C_SEXT    As String = "B9"
Private Const C_Q       As String = "B10"
Private Const C_MODE    As String = "B11"
Private Const C_ROLL    As String = "B12"
Private Const C_RNDMODE As String = "B13"

Private Const D_STATO     As String = "B3"
Private Const D_BANK      As String = "B4"
Private Const D_PLAYED    As String = "B5"
Private Const D_WINS      As String = "B6"
Private Const D_LASTSTAKE As String = "B7"
Private Const D_LASTNUM   As String = "B8"
Private Const D_LASTESITO As String = "B9"
Private Const D_SEXT      As String = "B10"
Private Const D_BETBREAK  As String = "D7"

Private Const S_FIRST_ROW As Long = 2

' Una puntata 40/40/20 coerente richiede multipli di 5 chip (2+2+1):
' con meno la copertura degenera (sestina scoperta o dozzine scoperte)
Private Const UNITS_PER_BET As Long = 5

' Stato del motore Masaniello: la tabella vive in memoria VBA e va
' ricostruita se il file viene riaperto a sessione in corso
Private mEngM As Long
Private mEngW As Long
Private mEngQ As Double
Private mEngReady As Boolean

'=====================================================================
' SESSIONE
'=====================================================================

Public Sub NewSession()
    Dim wsC As Worksheet: Set wsC = ThisWorkbook.Worksheets(SH_CONFIG)
    Dim wsD As Worksheet: Set wsD = ThisWorkbook.Worksheets(SH_DASH)
    Dim wsS As Worksheet: Set wsS = ThisWorkbook.Worksheets(SH_SESSION)

    Dim M As Long: M = CLng(ToDouble(wsC.Range(C_M).Value))
    Dim W As Long: W = CLng(ToDouble(wsC.Range(C_W).Value))
    Dim chip As Currency: chip = ToCurrency(wsC.Range(C_CHIP).Value)
    Dim sextStart As Long: sextStart = CLng(ToDouble(wsC.Range(C_SEXT).Value))
    Dim q As Double: q = ToDouble(wsC.Range(C_Q).Value)

    If M <= 0 Or W <= 0 Or W > M Then
        MsgBox "Config non valida: controlla M e W (W <= M).", vbExclamation
        Exit Sub
    End If
    If chip <= 0 Then
        MsgBox "Chip minimo non valido.", vbExclamation
        Exit Sub
    End If
    If sextStart <> 25 And sextStart <> 31 Then
        MsgBox "Sestina deve essere 25 oppure 31.", vbExclamation
        Exit Sub
    End If
    If q <= 1# Then q = 1.2

    Dim maxPct As Double
    maxPct = ModeToMaxPct(UCase$(Trim$(CStr(wsC.Range(C_MODE).Value))))
    If maxPct < 0 Then
        MsgBox "Modalità rischio non valida in B11. Usa: Prudente, Intermedia, Aggressiva, Ultra.", vbExclamation
        Exit Sub
    End If
    wsC.Range(C_MAXPCT).Value = maxPct

    ' Se c'è una sessione in corso con colpi giocati, archiviala in Storico
    Dim playedOld As Long: playedOld = CLng(ToDouble(wsD.Range(D_PLAYED).Value))
    If UCase$(Trim$(CStr(wsD.Range(D_STATO).Value))) = "IN CORSO" And playedOld > 0 Then
        Dim winsOld As Long: winsOld = CLng(ToDouble(wsD.Range(D_WINS).Value))
        Dim bankOld As Currency: bankOld = ToCurrency(wsD.Range(D_BANK).Value)
        Dim bank0Old As Currency: bank0Old = ToCurrency(wsS.Cells(S_FIRST_ROW, 9).Value)
        If bank0Old <= 0 Then bank0Old = bankOld
        AppendStorico "INTERROTTA", playedOld, winsOld, bank0Old, bankOld
    End If

    ' MODIFICA 1: rollover automatico della cassa.
    ' Se esiste una sessione precedente in Storico si riparte dal suo
    ' bank finale, altrimenti dal bankroll configurato in Config.
    Dim bank0 As Currency: bank0 = ToCurrency(wsC.Range(C_BANK0).Value)
    If RolloverOn(wsC) Then
        Dim lastBank As Currency: lastBank = LastStoricoBank()
        If lastBank > 0 Then bank0 = lastBank
    End If

    If bank0 < UNITS_PER_BET * chip Then
        MsgBox "Bankroll insufficiente: servono almeno " & _
               FormatCurrency(UNITS_PER_BET * chip) & " (5 chip) per coprire 2 dozzine + sestina.", vbExclamation
        Exit Sub
    End If

    EnsureEngine M, W, q
    InitRandom

    wsS.Cells.Clear
    WriteSessionHeader wsS

    wsD.Range(D_STATO).Value = "IN CORSO"
    wsD.Range(D_BANK).Value = bank0
    wsD.Range(D_PLAYED).Value = 0
    wsD.Range(D_WINS).Value = 0
    wsD.Range(D_LASTSTAKE).Value = ""
    wsD.Range(D_LASTNUM).Value = ""
    wsD.Range(D_LASTESITO).Value = ""
    wsD.Range(D_SEXT).Value = sextStart
    wsD.Range(D_BETBREAK).Value = ""

    Dim nextStake As Currency
    nextStake = CalcNextStake(bank0, 0, 0, chip, maxPct, M, W)
    wsD.Range(D_LASTSTAKE).Value = nextStake
    wsD.Range(D_BETBREAK).Value = BetBreakdownText(nextStake, chip, sextStart)
End Sub

' MODIFICA 2: AddNumber e AddRandomNumber condividono lo stesso flusso
' (ProcessNumber); qui resta solo l'input manuale.
Public Sub AddNumber()
    Dim wsD As Worksheet: Set wsD = ThisWorkbook.Worksheets(SH_DASH)
    If UCase$(Trim$(CStr(wsD.Range(D_STATO).Value))) <> "IN CORSO" Then
        MsgBox "Sessione non in corso. Premi 'Nuova sessione'.", vbExclamation
        Exit Sub
    End If

    Dim s As String
    s = InputBox("Inserisci numero uscito (0–36):", "Roulette")
    s = Trim$(s)
    If Len(s) = 0 Then Exit Sub
    If Not IsNumeric(s) Then
        MsgBox "Inserisci un numero 0–36.", vbExclamation
        Exit Sub
    End If

    Dim n As Long: n = CLng(ToDouble(s))
    If n < 0 Or n > 36 Then
        MsgBox "Numero fuori range (0–36).", vbExclamation
        Exit Sub
    End If

    ProcessNumber n, "MANUALE"
End Sub

' MODIFICA 2/3: numero generato dal RNG, stesso identico flusso di AddNumber
Public Sub AddRandomNumber()
    Dim wsD As Worksheet: Set wsD = ThisWorkbook.Worksheets(SH_DASH)
    If UCase$(Trim$(CStr(wsD.Range(D_STATO).Value))) <> "IN CORSO" Then
        MsgBox "Sessione non in corso. Premi 'Nuova sessione'.", vbExclamation
        Exit Sub
    End If

    ProcessNumber NextRouletteNumber(), "RANDOM"
End Sub

' Flusso comune: registra un numero uscito, aggiorna bankroll, contatori,
' storico colpi e calcola la prossima puntata.
Private Sub ProcessNumber(ByVal n As Long, ByVal fonte As String)
    Dim wsC As Worksheet: Set wsC = ThisWorkbook.Worksheets(SH_CONFIG)
    Dim wsD As Worksheet: Set wsD = ThisWorkbook.Worksheets(SH_DASH)
    Dim wsS As Worksheet: Set wsS = ThisWorkbook.Worksheets(SH_SESSION)

    If UCase$(Trim$(CStr(wsD.Range(D_STATO).Value))) <> "IN CORSO" Then
        MsgBox "Sessione non in corso. Premi 'Nuova sessione'.", vbExclamation
        Exit Sub
    End If

    Dim M As Long: M = CLng(ToDouble(wsC.Range(C_M).Value))
    Dim W As Long: W = CLng(ToDouble(wsC.Range(C_W).Value))
    Dim chip As Currency: chip = ToCurrency(wsC.Range(C_CHIP).Value)
    Dim q As Double: q = ToDouble(wsC.Range(C_Q).Value)
    If q <= 1# Then q = 1.2

    Dim maxPct As Double
    maxPct = ModeToMaxPct(UCase$(Trim$(CStr(wsC.Range(C_MODE).Value))))
    If maxPct < 0 Then maxPct = 0.07

    ' ricostruisce la tabella Masaniello se il file è stato riaperto
    EnsureEngine M, W, q

    Dim played As Long: played = CLng(ToDouble(wsD.Range(D_PLAYED).Value))
    Dim wins As Long: wins = CLng(ToDouble(wsD.Range(D_WINS).Value))
    Dim bankBefore As Currency: bankBefore = ToCurrency(wsD.Range(D_BANK).Value)
    Dim sextActive As Long: sextActive = CLng(ToDouble(wsD.Range(D_SEXT).Value))

    Dim stake As Currency: stake = ToCurrency(wsD.Range(D_LASTSTAKE).Value)
    If stake <= 0 Then
        MsgBox "Puntata consigliata = 0. Sessione terminata o banca insufficiente.", vbInformation
        Exit Sub
    End If

    Dim isWin As Boolean: isWin = EvaluateOutcome(n, sextActive)
    Dim esito As String: esito = IIf(isWin, "W", "L")

    ' Profitto REALE: dipende dalla ripartizione effettiva dei chip
    ' (d1/d2/sestina) e dal segmento dove è uscito il numero
    Dim profit As Currency: profit = ComputeProfit(n, sextActive, stake, chip)
    Dim bankAfter As Currency: bankAfter = bankBefore + profit

    Dim rowOut As Long: rowOut = S_FIRST_ROW + played
    WriteSessionRow wsS, rowOut, played + 1, n, sextActive, esito, stake, chip, bankBefore, bankAfter, profit, fonte

    played = played + 1
    If isWin Then wins = wins + 1

    wsD.Range(D_BANK).Value = bankAfter
    wsD.Range(D_PLAYED).Value = played
    wsD.Range(D_WINS).Value = wins
    wsD.Range(D_LASTNUM).Value = n
    wsD.Range(D_LASTESITO).Value = esito

    Dim nextStake As Currency
    nextStake = CalcNextStake(bankAfter, played, wins, chip, maxPct, M, W)

    If nextStake <= 0 Then
        Dim statoFinale As String
        statoFinale = IIf(wins >= W, "VINTA", "PERSA")
        wsD.Range(D_LASTSTAKE).Value = 0
        wsD.Range(D_BETBREAK).Value = ""
        wsD.Range(D_STATO).Value = statoFinale

        Dim bank0Sess As Currency: bank0Sess = ToCurrency(wsS.Cells(S_FIRST_ROW, 9).Value)
        AppendStorico statoFinale, played, wins, bank0Sess, bankAfter

        MsgBox "Sessione " & statoFinale & "." & vbCrLf & _
               "Colpi: " & played & "  –  Vittorie: " & wins & "/" & W & vbCrLf & _
               "Bankroll finale: " & FormatCurrency(bankAfter) & vbCrLf & _
               "Profitto sessione: " & FormatCurrency(bankAfter - bank0Sess), vbInformation
        Exit Sub
    End If

    wsD.Range(D_LASTSTAKE).Value = nextStake
    wsD.Range(D_BETBREAK).Value = BetBreakdownText(nextStake, chip, sextActive)
End Sub

'=====================================================================
' MODIFICA 4: simulazione di una sessione completa in memoria.
' Usa ESATTAMENTE le stesse funzioni delle sessioni reali:
' CalcNextStake (con Mas_Stake), EvaluateOutcome, ComputeProfit.
'=====================================================================

Public Sub SimulateSession(ByVal bank0 As Currency, ByVal M As Long, ByVal W As Long, _
                           ByVal q As Double, ByVal chip As Currency, ByVal maxPct As Double, _
                           ByVal sextStart As Long, _
                           ByRef outProfit As Currency, ByRef outWon As Boolean, _
                           ByRef outMaxDD As Currency, ByRef outColpi As Long, ByRef outWins As Long)
    EnsureEngine M, W, q

    Dim bank As Currency: bank = bank0
    Dim peak As Currency: peak = bank0
    Dim maxDD As Currency
    Dim played As Long, wins As Long
    Dim stake As Currency, n As Long

    Do
        stake = CalcNextStake(bank, played, wins, chip, maxPct, M, W)
        If stake <= 0 Then Exit Do

        n = NextRouletteNumber()
        bank = bank + ComputeProfit(n, sextStart, stake, chip)
        If EvaluateOutcome(n, sextStart) Then wins = wins + 1
        played = played + 1

        If bank > peak Then peak = bank
        If peak - bank > maxDD Then maxDD = peak - bank
    Loop

    outProfit = bank - bank0
    outWon = (wins >= W)
    outMaxDD = maxDD
    outColpi = played
    outWins = wins
End Sub

'=====================================================================
' GENERATORE CASUALE (MODIFICA 3)
'=====================================================================

' Estrazione uniforme 0–36 (roulette europea, nessun sistema predittivo)
Public Function NextRouletteNumber() As Long
    NextRouletteNumber = Int(Rnd() * 37)
End Function

' UNIFORME    = seed fisso: sequenza riproducibile (utile per test/confronti)
' REALISTICO  = seed dall'orologio: sequenza sempre diversa (default)
Public Sub InitRandom()
    Dim mode As String
    mode = UCase$(Trim$(CStr(ThisWorkbook.Worksheets(SH_CONFIG).Range(C_RNDMODE).Value)))
    If mode = "UNIFORME" Then
        Rnd -1
        Randomize 12345
    Else
        Randomize
    End If
End Sub

'=====================================================================
' MOTORE / PUNTATE
'=====================================================================

Private Sub EnsureEngine(ByVal M As Long, ByVal W As Long, ByVal q As Double)
    If mEngReady And mEngM = M And mEngW = W And mEngQ = q Then Exit Sub
    modMasanielloClassic.Mas_Prepara M, W, q
    mEngM = M: mEngW = W: mEngQ = q
    mEngReady = True
End Sub

Public Function ModeToMaxPct(ByVal modeU As String) As Double
    Select Case modeU
        Case "PRUDENTE": ModeToMaxPct = 0.03
        Case "INTERMEDIA": ModeToMaxPct = 0.07
        Case "AGGRESSIVA": ModeToMaxPct = 0.15
        Case "ULTRA": ModeToMaxPct = 1#
        Case Else: ModeToMaxPct = -1
    End Select
End Function

Private Function CalcNextStake(ByVal bank As Currency, ByVal played As Long, ByVal wins As Long, _
                               ByVal chip As Currency, ByVal maxPct As Double, _
                               ByVal M As Long, ByVal W As Long) As Currency
    Dim unitBet As Currency: unitBet = UNITS_PER_BET * chip

    If played >= M Or wins >= W Then
        CalcNextStake = 0
        Exit Function
    End If
    ' obiettivo non più raggiungibile: sessione matematicamente persa
    If (W - wins) > (M - played) Then
        CalcNextStake = 0
        Exit Function
    End If
    If bank < unitBet Then
        CalcNextStake = 0
        Exit Function
    End If

    Dim cap As Currency
    If maxPct >= 1 Then
        cap = bank
    Else
        cap = CCur(CDbl(bank) * maxPct)
        If cap < unitBet Then cap = unitBet
    End If

    ' puntata Masaniello arrotondata a multipli di 5 chip, così lo split
    ' 40/40/20 è esatto e ogni vincita paga esattamente +20%
    Dim stake As Currency
    stake = modMasanielloClassic.Mas_Stake(bank, played, wins, unitBet, 0)

    If stake > cap Then stake = CCur(Int(CDbl(cap) / CDbl(unitBet)) * CDbl(unitBet))
    If stake < unitBet Then stake = unitBet
    If stake > bank Then stake = CCur(Int(CDbl(bank) / CDbl(unitBet)) * CDbl(unitBet))

    CalcNextStake = stake
End Function

Private Function EvaluateOutcome(ByVal n As Long, ByVal sextStart As Long) As Boolean
    If n = 0 Then
        EvaluateOutcome = False
        Exit Function
    End If
    If n >= 1 And n <= 24 Then
        EvaluateOutcome = True
        Exit Function
    End If
    EvaluateOutcome = InSextina(n, sextStart)
End Function

Private Function InSextina(ByVal n As Long, ByVal sextStart As Long) As Boolean
    InSextina = (n >= sextStart And n <= sextStart + 5)
End Function

' Profitto reale del colpo: incasso effettivo del segmento colpito
' (dozzina paga 2:1, sestina 5:1) meno la puntata totale
Private Function ComputeProfit(ByVal n As Long, ByVal sextStart As Long, _
                               ByVal stake As Currency, ByVal chip As Currency) As Currency
    Dim d1 As Currency, d2 As Currency, sx As Currency
    ComputeSplit stake, chip, d1, d2, sx

    Dim ret As Currency
    If n >= 1 And n <= 12 Then
        ret = 3 * d1
    ElseIf n >= 13 And n <= 24 Then
        ret = 3 * d2
    ElseIf n > 0 And InSextina(n, sextStart) Then
        ret = 6 * sx
    Else
        ret = 0
    End If

    ComputeProfit = ret - stake
End Function

Private Function BetBreakdownText(ByVal stake As Currency, ByVal chip As Currency, ByVal sextStart As Long) As String
    If stake <= 0 Then
        BetBreakdownText = ""
        Exit Function
    End If

    Dim d1 As Currency, d2 As Currency, sx As Currency
    ComputeSplit stake, chip, d1, d2, sx

    Dim sxLabel As String
    sxLabel = IIf(sextStart = 25, "Sestina 25-30", "Sestina 31-36")

    BetBreakdownText = "Puntata totale: " & FormatCurrency(stake) & vbCrLf & _
                       "Dozzina 1 (1-12): " & FormatCurrency(d1) & vbCrLf & _
                       "Dozzina 2 (13-24): " & FormatCurrency(d2) & vbCrLf & _
                       sxLabel & ": " & FormatCurrency(sx)
End Function

Private Sub ComputeSplit(ByVal stake As Currency, ByVal chip As Currency, _
                         ByRef d1 As Currency, ByRef d2 As Currency, ByRef sx As Currency)
    Dim unit As Long
    unit = CLng(Int(CDbl(stake) / CDbl(chip) + 0.0000001))
    If unit <= 0 Then
        d1 = 0: d2 = 0: sx = 0
        Exit Sub
    End If

    Dim u1 As Long, u2 As Long, u3 As Long
    u1 = CLng(Application.WorksheetFunction.Round(unit * 0.4, 0))
    u2 = CLng(Application.WorksheetFunction.Round(unit * 0.4, 0))
    u3 = unit - u1 - u2
    If u3 < 0 Then u3 = 0

    d1 = CCur(u1 * CDbl(chip))
    d2 = CCur(u2 * CDbl(chip))
    sx = CCur(u3 * CDbl(chip))

    Dim sum As Currency: sum = d1 + d2 + sx
    If sum <> stake Then
        sx = sx + (stake - sum)
        If sx < 0 Then sx = 0
    End If
End Sub

'=====================================================================
' STORICO SESSIONI (per rollover e tracciabilità)
'=====================================================================

Public Function EnsureStoricoSheet() As Worksheet
    Dim ws As Worksheet
    On Error Resume Next
    Set ws = ThisWorkbook.Worksheets(SH_STORICO)
    On Error GoTo 0
    If ws Is Nothing Then
        Set ws = ThisWorkbook.Worksheets.Add(After:=ThisWorkbook.Worksheets(ThisWorkbook.Worksheets.Count))
        ws.Name = SH_STORICO
    End If
    If Len(Trim$(CStr(ws.Range("A1").Value))) = 0 Then
        ws.Range("A1:I1").Value = Array("Sessione", "DataOra", "Esito", "Colpi", "Vittorie", _
                                        "BankIniziale", "BankFinale", "Profitto", "ROI %")
        ws.Rows(1).Font.Bold = True
    End If
    Set EnsureStoricoSheet = ws
End Function

Private Sub AppendStorico(ByVal esito As String, ByVal colpi As Long, ByVal wins As Long, _
                          ByVal bank0 As Currency, ByVal bankF As Currency)
    Dim ws As Worksheet: Set ws = EnsureStoricoSheet()
    Dim r As Long: r = ws.Cells(ws.Rows.Count, 7).End(xlUp).Row + 1
    If r < 2 Then r = 2

    ws.Cells(r, 1).Value = r - 1
    ws.Cells(r, 2).Value = Now
    ws.Cells(r, 3).Value = esito
    ws.Cells(r, 4).Value = colpi
    ws.Cells(r, 5).Value = wins
    ws.Cells(r, 6).Value = bank0
    ws.Cells(r, 7).Value = bankF
    ws.Cells(r, 8).Value = bankF - bank0
    If bank0 > 0 Then ws.Cells(r, 9).Value = Round((CDbl(bankF) - CDbl(bank0)) / CDbl(bank0) * 100, 2)
End Sub

Public Function LastStoricoBank() As Currency
    Dim ws As Worksheet: Set ws = EnsureStoricoSheet()
    Dim r As Long: r = ws.Cells(ws.Rows.Count, 7).End(xlUp).Row
    If r < 2 Then
        LastStoricoBank = 0
    Else
        LastStoricoBank = ToCurrency(ws.Cells(r, 7).Value)
    End If
End Function

Private Function RolloverOn(ByVal wsC As Worksheet) As Boolean
    RolloverOn = (UCase$(Trim$(CStr(wsC.Range(C_ROLL).Value))) <> "NO")
End Function

'=====================================================================
' FOGLIO SESSION
'=====================================================================

Private Sub WriteSessionHeader(ByVal ws As Worksheet)
    ws.Range("A1:L1").Value = Array("#", "Numero", "Sestina", "Esito (W/L)", _
                                    "StakeTot", "D1(1-12)", "D2(13-24)", "Sext", _
                                    "BankBefore", "BankAfter", "Profit", "Fonte")
    ws.Rows(1).Font.Bold = True
End Sub

Private Sub WriteSessionRow(ByVal ws As Worksheet, ByVal r As Long, _
                            ByVal idx As Long, ByVal n As Long, ByVal sextActive As Long, _
                            ByVal esito As String, ByVal stake As Currency, ByVal chip As Currency, _
                            ByVal bankBefore As Currency, ByVal bankAfter As Currency, _
                            ByVal profit As Currency, ByVal fonte As String)

    Dim d1 As Currency, d2 As Currency, sx As Currency
    ComputeSplit stake, chip, d1, d2, sx

    ws.Cells(r, 1).Value = idx
    ws.Cells(r, 2).Value = n
    ws.Cells(r, 3).Value = sextActive
    ws.Cells(r, 4).Value = esito
    ws.Cells(r, 5).Value = stake
    ws.Cells(r, 6).Value = d1
    ws.Cells(r, 7).Value = d2
    ws.Cells(r, 8).Value = sx
    ws.Cells(r, 9).Value = bankBefore
    ws.Cells(r, 10).Value = bankAfter
    ws.Cells(r, 11).Value = profit
    ws.Cells(r, 12).Value = fonte
End Sub

'=====================================================================
' UTILITÀ
'=====================================================================

Public Function ToDouble(ByVal v As Variant) As Double
    On Error GoTo fallback
    If IsNumeric(v) Then
        ToDouble = CDbl(v)
        Exit Function
    End If
    Dim s As String: s = Trim$(CStr(v))
    If Len(s) = 0 Then ToDouble = 0: Exit Function
    ToDouble = CDbl(Application.WorksheetFunction.Value(s))
    Exit Function
fallback:
    ToDouble = 0
End Function

Public Function ToCurrency(ByVal v As Variant) As Currency
    On Error GoTo fallback
    If IsNumeric(v) Then
        ToCurrency = CCur(v)
        Exit Function
    End If
    Dim s As String: s = Trim$(CStr(v))
    If Len(s) = 0 Then ToCurrency = 0: Exit Function
    ToCurrency = CCur(Application.WorksheetFunction.Value(s))
    Exit Function
fallback:
    ToCurrency = 0
End Function
