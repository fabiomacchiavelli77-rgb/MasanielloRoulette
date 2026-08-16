Attribute VB_Name = "modMasanielloClassic"
Option Explicit
' Masaniello "originale" a quota decimale (q costante)

Private mOdds() As Double
Private mV() As Double
Private mM As Long, mW As Long
Private mReady As Boolean

Public Sub Mas_Prepara(ByVal M As Long, ByVal W As Long, ByVal quotaDec As Double)
    Dim i As Long
    ReDim mOdds(1 To M)
    For i = 1 To M
        mOdds(i) = quotaDec
    Next i
    BuildTable M, W
End Sub

Public Function Mas_Stake(ByVal bank As Currency, ByVal played As Long, ByVal wins As Long, _
                          ByVal minChip As Currency, ByVal maxStake As Currency) As Currency
    If Not mReady Then Mas_Stake = 0: Exit Function
    If played >= mM Then Mas_Stake = 0: Exit Function
    If wins >= mW Then Mas_Stake = 0: Exit Function

    Dim q As Double
    q = mOdds(played + 1)
    If q <= 1# Then Mas_Stake = 0: Exit Function

    Dim W As Long
    W = wins
    If W > mW Then W = mW

    Dim a As Double, b As Double, denom As Double, frac As Double
    a = mV(played + 1, W + 1)   ' valore se il prossimo colpo viene vinto
    b = mV(played + 1, W)       ' valore se il prossimo colpo viene perso

    If a <= 0# Then
        ' obiettivo irraggiungibile anche vincendo: non puntare
        frac = 0#
    ElseIf b <= 0# Then
        ' perdere il prossimo colpo rende l'obiettivo impossibile:
        ' il Masaniello richiede all-in (vanno vinti tutti i colpi rimanenti)
        frac = 1#
    Else
        denom = b + (q - 1#) * a
        If denom <= 0# Then
            frac = 1#
        Else
            frac = (b - a) / denom
        End If
    End If

    If frac < 0# Then frac = 0#
    If frac > 1# Then frac = 1#

    Dim stakeRaw As Currency
    stakeRaw = CCur(CDbl(bank) * frac)

    Dim stake As Currency
    stake = RoundToChip(stakeRaw, minChip)

    If stake > bank Then stake = bank
    If maxStake > 0 And stake > maxStake Then stake = maxStake
    If stake < 0 Then stake = 0

    Mas_Stake = stake
End Function

Private Sub BuildTable(ByVal M As Long, ByVal W As Long)
    mM = M
    mW = W
    mReady = False

    ReDim mV(0 To M, 0 To W + 1)

    Dim h As Long
    For h = 0 To W + 1
        If h >= W Then
            mV(M, h) = 1#
        Else
            mV(M, h) = 0#
        End If
    Next h

    Dim suffix() As Double
    ReDim suffix(0 To M)

    Dim p As Long
    Dim prod As Double
    prod = 1#
    suffix(M) = 1#
    For p = M - 1 To 0 Step -1
        prod = prod * mOdds(p + 1)
        suffix(p) = prod
    Next p

    Dim winsNeed As Long
    Dim remColpi As Long
    Dim q As Double
    Dim a As Double, b As Double, den As Double

    For p = M - 1 To 0 Step -1
        q = mOdds(p + 1)
        For h = W To 0 Step -1
            winsNeed = W - h
            remColpi = M - p

            If winsNeed <= 0 Then
                mV(p, h) = 1#
            ElseIf winsNeed > remColpi Then
                mV(p, h) = 0#
            ElseIf winsNeed = remColpi Then
                mV(p, h) = suffix(p)
            Else
                a = mV(p + 1, h)
                b = mV(p + 1, h + 1)
                den = a + (q - 1#) * b
                If den = 0# Then
                    mV(p, h) = 0#
                Else
                    mV(p, h) = q * a * b / den
                End If
            End If
        Next h
        mV(p, W + 1) = 1#
    Next p

    mReady = True
End Sub

Public Function RoundToChip(ByVal x As Currency, ByVal chip As Currency) As Currency
    If chip <= 0 Then
        RoundToChip = x
    Else
        RoundToChip = CCur(Int(CDbl(x) / CDbl(chip) + 0.5) * CDbl(chip))
    End If
End Function
