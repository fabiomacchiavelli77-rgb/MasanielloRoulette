Attribute VB_Name = "modSetupButtons"
Option Explicit

Public Sub SetupButtons()
    SetupAll
    MsgBox "Setup completato: pulsanti e fogli creati/aggiornati.", vbInformation
End Sub

Public Sub SetupAll()
    Dim wsD As Worksheet: Set wsD = ThisWorkbook.Worksheets("Dashboard")
    Dim wsC As Worksheet: Set wsC = ThisWorkbook.Worksheets("Config")

    ' fogli Storico e MonteCarlo
    modMasanielloApp.EnsureStoricoSheet
    Dim wsM As Worksheet: Set wsM = modMonteCarlo.EnsureMonteCarloSheet()

    ' nuove voci di Config (non sovrascrive valori già presenti)
    If Len(Trim$(CStr(wsC.Range("A12").Value))) = 0 Then wsC.Range("A12").Value = "Rollover cassa (SI/NO)"
    If Len(Trim$(CStr(wsC.Range("B12").Value))) = 0 Then wsC.Range("B12").Value = "SI"
    If Len(Trim$(CStr(wsC.Range("A13").Value))) = 0 Then wsC.Range("A13").Value = "Random (UNIFORME/REALISTICO)"
    If Len(Trim$(CStr(wsC.Range("B13").Value))) = 0 Then wsC.Range("B13").Value = "REALISTICO"

    ' pulsanti Dashboard
    Dim sh As Shape
    For Each sh In wsD.Shapes
        If sh.Name Like "btnMas_*" Then sh.Delete
    Next sh
    AddBtn wsD, "btnMas_Add", "AGGIUNGI NUMERO", "modMasanielloApp.AddNumber", 120, 205, 220, 40
    AddBtn wsD, "btnMas_Rnd", "SIMULA RANDOM", "modMasanielloApp.AddRandomNumber", 120, 255, 220, 40
    AddBtn wsD, "btnMas_New", "NUOVA SESSIONE", "modMasanielloApp.NewSession", 120, 305, 220, 40

    ' pulsante MonteCarlo
    For Each sh In wsM.Shapes
        If sh.Name Like "btnMas_*" Then sh.Delete
    Next sh
    AddBtn wsM, "btnMas_MC", "AVVIA MONTECARLO", "modMonteCarlo.RunMonteCarlo", 280, 30, 220, 45
End Sub

Private Sub AddBtn(ByVal ws As Worksheet, ByVal nm As String, ByVal caption As String, ByVal macroName As String, _
                   ByVal left As Double, ByVal top As Double, ByVal width As Double, ByVal height As Double)
    Dim btn As Shape
    Set btn = ws.Shapes.AddShape(msoShapeRoundedRectangle, left, top, width, height)
    btn.Name = nm
    btn.TextFrame2.TextRange.Text = caption
    btn.TextFrame2.TextRange.Font.Size = 14
    btn.OnAction = macroName
End Sub
