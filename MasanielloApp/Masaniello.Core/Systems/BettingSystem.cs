namespace Masaniello.Core.Systems;

/// <summary>
/// Un segmento di puntata: un gruppo di numeri coperto da una singola puntata
/// (es. una dozzina), con il moltiplicatore di ritorno totale del banco
/// (dozzina paga 2:1 → ritorno 3x) e le unità di chip assegnate.
/// </summary>
public sealed record BetSegment(string Nome, IReadOnlyList<int> Numeri, int MoltiplicatoreRitorno, int Unita)
{
    /// <summary>Unità incassate se esce un numero di questo segmento.</summary>
    public int RitornoUnita => MoltiplicatoreRitorno * Unita;
}

/// <summary>
/// Sistema di puntata a ritorno costante: qualunque numero coperto esca,
/// l'incasso è sempre RitornoUnita, quindi la vincita netta è identica e
/// il sistema equivale a un evento binario a quota Q = RitornoUnita / UnitaTotali.
/// </summary>
public sealed class BettingSystem
{
    public string Codice { get; }
    public string Nome { get; }
    public IReadOnlyList<BetSegment> Segmenti { get; }
    public int UnitaTotali { get; }
    public int RitornoUnita { get; }
    public double Q => (double)RitornoUnita / UnitaTotali;
    public int NumeriCoperti { get; }
    public double ProbabilitaColpo => NumeriCoperti / 37.0;

    private readonly BetSegment?[] _perNumero = new BetSegment?[37];

    public BettingSystem(string codice, string nome, IReadOnlyList<BetSegment> segmenti)
    {
        if (segmenti.Count == 0) throw new ArgumentException("Serve almeno un segmento.");

        Codice = codice;
        Nome = nome;
        Segmenti = segmenti;

        int ritorno = segmenti[0].RitornoUnita;
        foreach (var seg in segmenti)
        {
            if (seg.Unita <= 0) throw new ArgumentException($"Unità non valide nel segmento {seg.Nome}.");
            if (seg.RitornoUnita != ritorno)
                throw new ArgumentException(
                    $"Ritorno non costante: {seg.Nome} incassa {seg.RitornoUnita} unità invece di {ritorno}. " +
                    "Le unità dei segmenti devono pareggiare l'incasso.");
            foreach (int n in seg.Numeri)
            {
                if (n < 0 || n > 36) throw new ArgumentException($"Numero fuori range: {n}.");
                if (_perNumero[n] != null) throw new ArgumentException($"Numero {n} coperto da più segmenti.");
                _perNumero[n] = seg;
            }
        }

        RitornoUnita = ritorno;
        UnitaTotali = segmenti.Sum(s => s.Unita);
        NumeriCoperti = _perNumero.Count(s => s != null);

        if (RitornoUnita <= UnitaTotali)
            throw new ArgumentException("Sistema senza vincita: il ritorno non supera la puntata.");
    }

    public bool Copre(int numero) => _perNumero[numero] != null;

    public IReadOnlyList<int> NumeriScoperti =>
        Enumerable.Range(0, 37).Where(n => _perNumero[n] == null).ToList();

    /// <summary>Granularità e puntata minima: multipli di UnitaTotali × chip.</summary>
    public decimal PuntataMinima(decimal chip) => UnitaTotali * chip;

    /// <summary>
    /// Profitto reale del colpo: incasso effettivo del segmento colpito meno la
    /// puntata totale. Con puntate multiple della granularità la vincita è
    /// esattamente (RitornoUnita - UnitaTotali) / UnitaTotali della puntata.
    /// </summary>
    public decimal Profitto(int numero, decimal puntata)
    {
        var seg = _perNumero[numero];
        decimal perUnita = puntata / UnitaTotali;
        decimal incasso = seg == null ? 0m : seg.RitornoUnita * perUnita;
        return incasso - puntata;
    }

    /// <summary>Ripartizione della puntata sui segmenti (importi in valuta).</summary>
    public IReadOnlyList<(string Nome, decimal Importo)> Breakdown(decimal puntata)
    {
        decimal perUnita = puntata / UnitaTotali;
        return Segmenti.Select(s => (s.Nome, s.Unita * perUnita)).ToList();
    }
}
