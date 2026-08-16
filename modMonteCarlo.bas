Attribute VB_Name = "modMonteCarlo"
Option Explicit

' MODIFICA 4: MonteCarlo – N sessioni automatiche con lo stesso identico
' algoritmo delle sessioni reali (modMasanielloApp.SimulateSession).

Private Const SH_MC     As String = "MonteCarlo"
Private Const SH_CONFIG As String = "Config"

Private Const C_M    As String = "B4"
Private Const C_W    As String = "B5"
Private Const C_BANK As String = "B6"
Private Const C_CHIP As String = "B7"
Private Const C_SEXT As String = "B9"
Private Const C_Q    As String = "B10"
Private Const C_MODE As String = "B11"

Private Const MC_NSESS As String = "B3"   ' numero sessioni da simulare
Private Const MC_MODE  As String = "B4"   ' INDIPENDENTE oppure ROLLOVER
Private Const MC_FIRST_DATA_ROW As Long = 18
Private Const MC_MAX_SESS As Long = 100000

Public Sub RunMonteCarlo()
    Dim wsC As Worksheet: Set wsC = ThisWorkbook.Worksheets(SH_CONFIG)
    Dim wsM As Worksheet: Set wsM = EnsureMonteCarloSheet()

    Dim M As Long: M = CLng(modMasanielloApp.ToDouble(wsC.Range(C_M).Value))
    Dim W As Long: W = CLng(modMasanielloApp.ToDouble(wsC.Range(C_W).Value))
    Dim bankCfg As Currency: bankCfg = modMasanielloApp.ToCurrency(wsC.Range(C_BANK).Value)
    Dim chip As Currency: chip = modMasanielloApp.ToCurrency(wsC.Range(C_CHIP).Value)
    Dim sextStart As Long: sextStart = CLng(modMasanielloApp.ToDouble(wsC.Range(C_SEXT).Value))
    Dim q As Double: q = modMasanielloApp.ToDouble(wsC.Range(C_Q).Value)
    If q <= 1# Then q = 1.2

    If M <= 0 Or W <= 0 Or W > M Then
        MsgBox "Config non valida: controlla M e W (W <= M).", vbExclamation
        Exit Sub
    End If
    If bankCfg <= 0 Or chip <= 0 Then
        MsgBox "Bankroll o chip non validi in Config.", vbExclamation
        Exit Sub
    End If
    If sextStart <> 25 And sextStart <> 31 Then
        MsgBox "Sestina deve essere 25 oppure 31.", vbExclamation
        Exit Sub
    End If

    Dim maxPct As Double
    maxPct = modMasanielloApp.ModeToMaxPct(UCase$(Trim$(CStr(wsC.Range(C_MODE).Value))))
    If maxPct < 0 Then
        MsgBox "Modalità rischio non valida in Config B11.", vbExclamation
        Exit Sub
    End If

    Dim nSess As Long: nSess = CLng(modMasanielloApp.ToDouble(wsM.Range(MC_NSESS).Value))
    If nSess <= 0 Then nSess = 10000
    If nSess > MC_MAX_SESS Then nSess = MC_MAX_SESS

    Dim rolloverMode As Boolean
    rolloverMode = (UCase$(Trim$(CStr(wsM.Range(MC_MODE).Value))) Like "ROLL*")

    ' pulizia output precedente
    wsM.Range("B7:B15").ClearContents
    wsM.Rows(MC_FIRST_DATA_ROW & ":" & wsM.Rows.Count).ClearContents

    Dim oldCalc As Long: oldCalc = Application.Calculation
    Application.ScreenUpdating = False
    Application.Calculation = xlCalculationManual

    modMasanielloApp.InitRandom

    Dim out() As Variant
    ReDim out(1 To nSess, 1 To 8)

    Dim i As Long, done As Long
    Dim bankStart As Currency: bankStart = bankCfg
    Dim p As Currency, dd As Currency
    Dim won As Boolean
    Dim colpi As Long, wins As Long

    Dim cntWon As Long
    Dim sumProfit As Double, sumDD As Double, sumROI As Double
    Dim maxProfit As Currency, minProfit As Currency
    Dim maxDDAll As Currency

    For i = 1 To nSess
        ' in modalità ROLLOVER la cassa passa alla sessione successiva;
        ' se non basta più per la puntata minima (5 chip) la simulazione si ferma
        If rolloverMode And bankStart < 5 * chip Then Exit For

        modMasanielloApp.SimulateSession bankStart, M, W, q, chip, maxPct, sextStart, _
                                         p, won, dd, colpi, wins
        done = done + 1

        out(done, 1) = done
        out(done, 2) = IIf(won, "VINTA", "PERSA")
        out(done, 3) = colpi
        out(done, 4) = wins
        out(done, 5) = bankStart
        out(done, 6) = p
        out(done, 7) = Round(CDbl(p) / CDbl(bankStart) * 100, 2)
        out(done, 8) = dd

        If won Then cntWon = cntWon + 1
        sumProfit = sumProfit + CDbl(p)
        sumDD = sumDD + CDbl(dd)
        sumROI = sumROI + CDbl(p) / CDbl(bankStart) * 100
        If done = 1 Or p > maxProfit Then maxProfit = p
        If done = 1 Or p < minProfit Then minProfit = p
        If dd > maxDDAll Then maxDDAll = dd

        If rolloverMode Then bankStart = bankStart + p

        If done Mod 500 = 0 Then
            Application.StatusBar = "MonteCarlo: " & done & " / " & nSess & " sessioni..."
            DoEvents
        End If
    Next i

    If done > 0 Then
        wsM.Range("B7").Value = done
        wsM.Range("B8").Value = Round(cntWon / done * 100, 2)
        wsM.Range("B9").Value = Round(sumProfit / done, 2)
        wsM.Range("B10").Value = maxProfit
        wsM.Range("B11").Value = minProfit
        wsM.Range("B12").Value = Round(sumDD / done, 2)
        wsM.Range("B13").Value = maxDDAll
        wsM.Range("B14").Value = Round(sumROI / done, 2)
        If rolloverMode Then
            wsM.Range("B15").Value = bankStart
        Else
            wsM.Range("B15").Value = "-"
        End If

        wsM.Range(wsM.Cells(MC_FIRST_DATA_ROW, 1), wsM.Cells(MC_FIRST_DATA_ROW + done - 1, 8)).Value = out
    End If

    Application.StatusBar = False
    Application.Calculation = oldCalc
    Application.ScreenUpdating = True

    If done < nSess And rolloverMode Then
        MsgBox "MonteCarlo completato: " & done & " sessioni su " & nSess & "." & vbCrLf & _
               "La cassa è scesa sotto la puntata minima (5 chip): bancarotta.", vbInformation
    Else
        MsgBox "MonteCarlo completato: " & done & " sessioni simulate." & vbCrLf & _
               "Sessioni vincenti: " & Round(cntWon / done * 100, 2) & "%", vbInformation
    End If
End Sub

Public Function EnsureMonteCarloSheet() As Worksheet
    Dim ws As Worksheet
    On Error Resume Next
    Set ws = ThisWorkbook.Worksheets(SH_MC)
    On Error GoTo 0
    If ws Is Nothing Then
        Set ws = ThisWorkbook.Worksheets.Add(After:=ThisWorkbook.Worksheets(ThisWorkbook.Worksheets.Count))
        ws.Name = SH_MC
    End If

    If Len(Trim$(CStr(ws.Range("A1").Value))) = 0 Then
        ws.Range("A1").Value = "SIMULAZIONE MONTECARLO MASANIELLO"
        ws.Range("A1").Font.Bold = True

        ws.Range("A3").Value = "Numero sessioni"
        ws.Range(MC_NSESS).Value = 10000
        ws.Range("A4").Value = "Modalità (INDIPENDENTE/ROLLOVER)"
        ws.Range(MC_MODE).Value = "INDIPENDENTE"

        ws.Range("A6").Value = "RISULTATI"
        ws.Range("A6").Font.Bold = True
        ws.Range("A7").Value = "Sessioni simulate"
        ws.Range("A8").Value = "% sessioni vincenti"
        ws.Range("A9").Value = "Profitto medio"
        ws.Range("A10").Value = "Profitto massimo"
        ws.Range("A11").Value = "Perdita massima"
        ws.Range("A12").Value = "Drawdown medio"
        ws.Range("A13").Value = "Drawdown massimo"
        ws.Range("A14").Value = "ROI medio %"
        ws.Range("A15").Value = "Bank finale (solo ROLLOVER)"

        ws.Range("A17:H17").Value = Array("Sessione", "Esito", "Colpi", "Vittorie", _
                                          "BankIniziale", "Profitto", "ROI %", "MaxDD")
        ws.Rows(17).Font.Bold = True
        ws.Columns("A:H").AutoFit
    End If

    Set EnsureMonteCarloSheet = ws
End Function
