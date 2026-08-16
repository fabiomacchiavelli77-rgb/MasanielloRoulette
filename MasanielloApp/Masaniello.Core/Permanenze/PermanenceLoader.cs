using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;

namespace Masaniello.Core.Permanenze;

/// <summary>
/// Carica una permanenza (sequenza di numeri usciti 0-36) da file .xls/.xlsx/.csv/.txt.
/// Supporta il formato esportato dal permanenzimetro di laroulette.it (colonne
/// Boule / Numero / Rosso / Nero / …) e anche i falsi Excel che in realtà sono
/// tabelle HTML salvate con estensione .xls.
/// La colonna dei numeri viene riconosciuta da sola: vince quella con più valori
/// interi in 0-36 sufficientemente diversi tra loro, scartando le colonne indice
/// (1, 2, 3, …, anche con buchi), le colonne costanti e i flag binari 0/1.
/// </summary>
public static class PermanenceLoader
{
    /// <summary>Sotto questa soglia il file non è considerato una permanenza valida.</summary>
    public const int MinimoNumeri = 10;

    private static readonly Regex TagCella = new(@"</t[dh]\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TagRiga = new(@"</tr\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TagGenerico = new(@"<[^>]+>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static PermanenceLoader() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // serve a ExcelDataReader per i .xls

    public static List<int> Carica(string percorso)
    {
        string ext = Path.GetExtension(percorso).ToLowerInvariant();
        List<int>? migliore = null;

        if (ext is ".xls" or ".xlsx")
        {
            try
            {
                // foglio migliore = quello con più numeri riconosciuti (alcuni file
                // hanno prima un foglio riepilogo con frequenze 0-36)
                foreach (var griglia in LeggiExcel(percorso))
                    if (EstraiNumeri(griglia) is { } numeri &&
                        (migliore == null || numeri.Count > migliore.Count))
                        migliore = numeri;
            }
            catch
            {
                // non è un vero file Excel (per esempio una tabella HTML con
                // estensione .xls): si riprova leggendolo come testo
            }

            if (migliore == null)
                migliore = EstraiNumeri(LeggiTesto(percorso));
        }
        else
        {
            migliore = EstraiNumeri(LeggiTesto(percorso));
        }

        return migliore ?? throw new InvalidDataException(
            $"Nessuna colonna di numeri 0-36 trovata in \"{Path.GetFileName(percorso)}\" " +
            $"(servono almeno {MinimoNumeri} numeri, non tutti uguali).");
    }

    private static List<List<string[]>> LeggiExcel(string percorso)
    {
        using var stream = File.OpenRead(percorso);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var fogli = new List<List<string[]>>();
        do
        {
            var righe = new List<string[]>();
            while (reader.Read())
            {
                var riga = new string[reader.FieldCount];
                for (int c = 0; c < reader.FieldCount; c++)
                    riga[c] = Convert.ToString(reader.GetValue(c), CultureInfo.InvariantCulture) ?? "";
                righe.Add(riga);
            }
            fogli.Add(righe);
        } while (reader.NextResult());
        return fogli;
    }

    private static List<string[]> LeggiTesto(string percorso)
    {
        string testo = File.ReadAllText(percorso);

        // tabella HTML: le celle diventano colonne, le righe righe
        if (testo.Contains('<'))
        {
            testo = TagCella.Replace(testo, "\t");
            testo = TagRiga.Replace(testo, "\n");
            testo = TagGenerico.Replace(testo, " ");
        }

        return testo
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Split([';', ',', '\t', ' ', '|'], StringSplitOptions.RemoveEmptyEntries))
            .Where(c => c.Length > 0)
            .ToList();
    }

    private static List<int>? EstraiNumeri(List<string[]> righe)
    {
        if (righe.Count == 0) return null;
        int nColonne = righe.Max(r => r.Length);
        (List<int> Numeri, bool HeaderNumero)? migliore = null;

        for (int c = 0; c < nColonne; c++)
        {
            var numeri = new List<int>();
            var interi = new List<double>();
            foreach (var riga in righe)
            {
                if (c >= riga.Length) continue;
                string cella = riga[c].Trim();
                if (cella.Length == 0) continue;
                if (TryParseIntero(cella, out double v))
                {
                    interi.Add(v);
                    if (v is >= 0 and <= 36) numeri.Add((int)v);
                }
            }

            // colonna indice/progressiva del colpo (es. "Boule" 1, 2, 3, …, anche
            // con buchi): una permanenza vera non è mai strettamente crescente
            if (interi.Count >= 3 &&
                Enumerable.Range(1, interi.Count - 1).All(i => interi[i] > interi[i - 1]))
                continue;

            // colonne costanti o flag 0/1 (tavolo, Rosso/Nero, Pari/Disp, …):
            // valori troppo pochi e diversi non sono numeri usciti
            if (numeri.Count < MinimoNumeri || numeri.Distinct().Count() < 5)
                continue;

            // indizio dall'intestazione: "Numero" / "Number" (formato laroulette.it)
            bool headerNumero = righe[0].Length > c &&
                righe[0][c].Trim().ToLowerInvariant().Contains("num");

            if (migliore == null || numeri.Count > migliore.Value.Numeri.Count ||
                (numeri.Count == migliore.Value.Numeri.Count && headerNumero && !migliore.Value.HeaderNumero))
            {
                migliore = (numeri, headerNumero);
            }
        }

        return migliore?.Numeri;
    }

    /// <summary>Parse tollerante: accetta anche il decimale virgola ("22,00" → 22).</summary>
    private static bool TryParseIntero(string cella, out double v)
    {
        if (double.TryParse(cella, NumberStyles.Any, CultureInfo.InvariantCulture, out v) ||
            double.TryParse(cella.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out v))
            return v == Math.Floor(v);

        v = 0;
        return false;
    }
}
