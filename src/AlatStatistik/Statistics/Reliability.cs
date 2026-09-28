using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Reliability
    {

        public readonly struct HasilAlfa
        {
            public double Alpha { get; init; }
            public double AlphaTerstandar { get; init; }
            public double RerataR { get; init; }

            public int Pasang { get; init; }
        }

        public static HasilAlfa Alfa(IReadOnlyList<double[]> data)
        {
            int k = data.Count;
            int n = k > 0 ? data[0].Length : 0;

            var variances = data.Select(col => Descriptives.Summarize(col.ToList()).Variance).ToArray();

            var total = new double[n];
            for (int j = 0; j < k; j++)
                for (int i = 0; i < n; i++) total[i] += data[j][i];

            double varTotal = Descriptives.Summarize(total.ToList()).Variance;
            double alpha = k / (double)(k - 1) * (1 - variances.Sum() / varTotal);

            double sumR = 0;
            int pairs = 0;
            for (int a = 0; a < k; a++)
                for (int b = a + 1; b < k; b++)
                {
                    double r = Correlation.Pearson(data[a].ToList(), data[b].ToList());
                    if (!double.IsNaN(r)) { sumR += r; pairs++; }
                }
            double meanR = pairs > 0 ? sumR / pairs : double.NaN;
            double alphaStd = !double.IsNaN(meanR) ? k * meanR / (1 + (k - 1) * meanR) : double.NaN;

            return new HasilAlfa { Alpha = alpha, AlphaTerstandar = alphaStd, RerataR = meanR, Pasang = pairs };
        }

        public static List<ResultBlock> CronbachAlpha(Dataset ds, List<string> items, bool reverseNote = true)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading("Analisis reliabilitas — alfa Cronbach", 1) };

            if (items.Count < 2)
            {
                blocks.Add(Blocks.Note("Butuh sedikitnya 2 aitem.", NoteKind.Error));
                return blocks;
            }

            // CompleteRows hanya memeriksa "tidak kosong", bukan "bisa jadi angka".
            // Sel berisi teks lolos lalu !.Value melempar, jadi disaring di sini.
            var idItem = items.Select(it => ds.IndexOf(it)).ToList();
            var keep = ds.CompleteRows(items)
                .Where(r => idItem.All(c => Dataset.ToDouble(ds.Rows[r][c]).HasValue))
                .ToList();
            int n = keep.Count;
            int k = items.Count;

            if (n < 3)
            {
                blocks.Add(Blocks.Note($"Hanya {n} baris lengkap; butuh sedikitnya 3.", NoteKind.Error));
                return blocks;
            }

            
            var data = new double[k][];
            for (int j = 0; j < k; j++)
            {
                int col = idItem[j];
                data[j] = keep.Select(r => Dataset.ToDouble(ds.Rows[r][col])!.Value).ToArray();
            }

            var hasil = Alfa(data);
            var variances = data.Select(col => Descriptives.Summarize(col.ToList()).Variance).ToArray();

            var total = new double[n];
            for (int j = 0; j < k; j++)
                for (int i = 0; i < n; i++) total[i] += data[j][i];

            double varTotal = Descriptives.Summarize(total.ToList()).Variance;
            double alpha = hasil.Alpha;
            double meanR = hasil.RerataR;
            double alphaStd = hasil.AlphaTerstandar;

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.AlfaCronbach, DaftarRumus.RagamSampel));
            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Jumlah aitem & amatan", $"k = {Fmt.Int(k)} aitem   N = {Fmt.Int(n)} baris lengkap"),
                ("Jumlah ragam aitem", $"Σσᵢ² = {Fmt.Num(variances.Sum(), 3)}"),
                ("Ragam skor total", $"σₜ² = {Fmt.Num(varTotal, 3)}"),
                ("Alfa Cronbach",
                 $"α = (k / (k−1)) · (1 − Σσᵢ² / σₜ²) = ({Fmt.Int(k)} / {Fmt.Int(k - 1)}) · "
                 + $"(1 − {Fmt.Num(variances.Sum(), 3)} / {Fmt.Num(varTotal, 3)}) = {Fmt.Num(alpha, 3)}"),
                ("Alfa terstandar",
                 $"r̄ = {Fmt.Num(meanR, 3)}   →   α_terstandar = k·r̄ / (1 + (k−1)·r̄) = {Fmt.Num(alphaStd, 3)}")));

            blocks.Add(Blocks.Table("Reliabilitas",
                new[] { "Ukuran", "Nilai", "Jumlah aitem", "N" },
                new[]
                {
                    new[] { "Alfa Cronbach", Fmt.Num(alpha, 3), Fmt.Int(k), Fmt.Int(n) },
                    new[] { "Alfa terstandar", Fmt.Num(alphaStd, 3), Fmt.Int(k), Fmt.Int(n) },
                    new[] { "Korelasi antar-aitem rerata", Fmt.Num(meanR, 3), Fmt.Int(hasil.Pasang) + " pasang", Fmt.Int(n) }
                },
                $"Tafsir: {TafsirAlpha(alpha)}. Alfa terstandar dipakai bila aitem memiliki skala yang berbeda."));

            
            var itemRows = new List<List<string>>();
            for (int j = 0; j < k; j++)
            {
                var others = Enumerable.Range(0, k).Where(t => t != j).ToArray();
                var totalWithout = new double[n];
                for (int i = 0; i < n; i++)
                    foreach (int t in others) totalWithout[i] += data[t][i];

                double corrCorrected = Correlation.Pearson(data[j].ToList(), totalWithout.ToList());

                int kMinus = k - 1;
                double varWithout = Descriptives.Summarize(totalWithout.ToList()).Variance;
                double sumVarWithout = others.Sum(t => variances[t]);
                double alphaWithout = kMinus > 1
                    ? kMinus / (double)(kMinus - 1) * (1 - sumVarWithout / varWithout)
                    : double.NaN;

                var s = Descriptives.Summarize(data[j].ToList());

                itemRows.Add(new List<string>
                {
                    items[j], Fmt.Num(s.Mean), Fmt.Num(s.Sd), Fmt.Int(n),
                    Fmt.Num(corrCorrected, 3), Fmt.Num(alphaWithout, 3)
                });
            }

            blocks.Add(Blocks.Table("Statistik aitem",
                new[] { "Aitem", "Rerata", "Simpangan baku", "N", "Korelasi aitem–total terkoreksi", "Alfa jika aitem dibuang" },
                itemRows,
                "Korelasi aitem–total terkoreksi dihitung terhadap jumlah aitem lainnya (tanpa aitem bersangkutan). "
                + "Nilai di bawah 0,30 patut dipertimbangkan untuk dibuang."));

            
            var weakest = itemRows
                .Select((row, idx) => (Item: items[idx], Alpha: ParseOr(row[5])))
                .OrderBy(t => double.IsNaN(t.Alpha) ? double.MaxValue : t.Alpha)
                .FirstOrDefault();

            if (!double.IsNaN(weakest.Alpha) && alpha < 0.8)
                blocks.Add(Blocks.Note(
                    $"Alfa {Fmt.Num(alpha, 3)} masih bisa ditingkatkan dengan meninjau aitem "
                    + $"'{weakest.Item}' (alfa akan menjadi {Fmt.Num(weakest.Alpha, 3)} bila dibuang).",
                    NoteKind.Warning));

            if (reverseNote)
                blocks.Add(Blocks.Note(
                    "Bila ada aitem yang dibalik arahnya (unfavorable), balik dulu nilainya "
                    + "sebelum menghitung alfa; jika tidak, alfa bisa mengecil drastis.", NoteKind.Info));

            return blocks;
        }

        private static double ParseOr(string text)
        {
            string s = text.Replace(".", "").Replace(",", ".");
            return double.TryParse(s, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out double v)
                ? v : double.NaN;
        }

        private static string TafsirAlpha(double a)
        {
            if (double.IsNaN(a)) return "tidak dapat dihitung";
            if (a >= 0.9) return "sangat baik";
            if (a >= 0.8) return "baik";
            if (a >= 0.7) return "dapat diterima";
            if (a >= 0.6) return "meragukan";
            return "kurang";
        }
    }
}
