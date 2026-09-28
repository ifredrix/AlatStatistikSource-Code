using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Crosstab
    {

        public readonly struct UjiTabelSilang
        {
            public double Chi2 { get; init; }
            public double G2 { get; init; }
            public double P { get; init; }
            public double PG { get; init; }
            public double CramersV { get; init; }
            public double Contingency { get; init; }
            public int Df { get; init; }
        }

        public static UjiTabelSilang ChiSquareTable(int[,] table)
        {
            int nr = table.GetLength(0), nc = table.GetLength(1);

            var rowTotals = new int[nr];
            var colTotals = new int[nc];
            int total = 0;
            for (int r = 0; r < nr; r++)
                for (int c = 0; c < nc; c++)
                {
                    rowTotals[r] += table[r, c];
                    colTotals[c] += table[r, c];
                    total += table[r, c];
                }

            double chi2 = 0, g2 = 0;
            for (int r = 0; r < nr; r++)
                for (int c = 0; c < nc; c++)
                {
                    double expected = (double)rowTotals[r] * colTotals[c] / total;
                    if (expected <= 0) continue;
                    double diff = table[r, c] - expected;
                    chi2 += diff * diff / expected;
                    if (table[r, c] > 0) g2 += 2.0 * table[r, c] * Math.Log(table[r, c] / expected);
                }

            int df = (nr - 1) * (nc - 1);
            int minDim = Math.Min(nr, nc) - 1;

            return new UjiTabelSilang
            {
                Chi2 = chi2,
                G2 = g2,
                Df = df,
                P = df > 0 ? Distributions.ChiSquareUpper(chi2, df) : double.NaN,
                PG = df > 0 ? Distributions.ChiSquareUpper(g2, df) : double.NaN,
                CramersV = df > 0 && chi2 > 0 && minDim > 0 ? Math.Sqrt(chi2 / (total * minDim)) : double.NaN,
                Contingency = chi2 > 0 ? Math.Sqrt(chi2 / (chi2 + total)) : double.NaN
            };
        }

        public static List<ResultBlock> Analyze(Dataset ds, string rowVar, string colVar, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading($"Tabel silang — {rowVar} × {colVar}", 1) };

            var rows = ds.Levels(rowVar);
            var cols = ds.Levels(colVar);
            if (rows.Count == 0 || cols.Count == 0)
            {
                blocks.Add(Blocks.Note("Salah satu variabel tidak punya kategori yang sah.", NoteKind.Error));
                return blocks;
            }

            var rowText = ds.Text(rowVar);
            var colText = ds.Text(colVar);

            var table = new int[rows.Count, cols.Count];
            for (int i = 0; i < rowText.Length && i < colText.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(rowText[i]) || string.IsNullOrWhiteSpace(colText[i])) continue;
                int r = rows.IndexOf(rowText[i]!);
                int c = cols.IndexOf(colText[i]!);
                if (r >= 0 && c >= 0) table[r, c]++;
            }

            int total = 0;
            foreach (int v in table) total += v;
            if (total == 0)
            {
                blocks.Add(Blocks.Note("Tidak ada pasangan data yang lengkap.", NoteKind.Error));
                return blocks;
            }

            var rowTotals = new int[rows.Count];
            var colTotals = new int[cols.Count];
            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < cols.Count; c++) { rowTotals[r] += table[r, c]; colTotals[c] += table[r, c]; }

            
            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.ChiSquare, DaftarRumus.CramerV));

            
            var displayCols = new List<string> { $"{rowVar} \\ {colVar}" };
            displayCols.AddRange(cols);
            displayCols.Add("Total");

            var displayRows = new List<List<string>>();
            for (int r = 0; r < rows.Count; r++)
            {
                var line = new List<string> { rows[r] };
                for (int c = 0; c < cols.Count; c++) line.Add(Fmt.Int(table[r, c]));
                line.Add(Fmt.Int(rowTotals[r]));
                displayRows.Add(line);
            }

            var totalLine = new List<string> { "Total" };
            for (int c = 0; c < cols.Count; c++) totalLine.Add(Fmt.Int(colTotals[c]));
            totalLine.Add(Fmt.Int(total));
            displayRows.Add(totalLine);

            blocks.Add(Blocks.Table("Tabel amatan", displayCols, displayRows));

            
            var stat = ChiSquareTable(table);
            double chi2 = stat.Chi2, g2 = stat.G2;
            int minExpectedZero = 0;
            var expectedRows = new List<List<string>>();

            for (int r = 0; r < rows.Count; r++)
            {
                var line = new List<string> { rows[r] };
                for (int c = 0; c < cols.Count; c++)
                {
                    double expected = (double)rowTotals[r] * colTotals[c] / total;
                    line.Add(Fmt.Num(expected, 2));
                    if (expected < 5) minExpectedZero++;
                }
                expectedRows.Add(line);
            }

            int df = stat.Df;
            double p = stat.P;
            double pG = stat.PG;

            int minDim = Math.Min(rows.Count, cols.Count) - 1;
            double cramersV = stat.CramersV;
            double contingency = stat.Contingency;

            
            var langkah = new List<LangkahHitung>();
            int jumlahSel = rows.Count * cols.Count;
            if (jumlahSel <= 9)
            {
                for (int r = 0; r < rows.Count; r++)
                {
                    for (int c = 0; c < cols.Count; c++)
                    {
                        double e = (double)rowTotals[r] * colTotals[c] / total;
                        double selisih = table[r, c] - e;
                        langkah.Add(new LangkahHitung
                        {
                            Uraian = $"Sel {rows[r]} × {cols[c]}",
                            Hitungan = $"E = ({Fmt.Int(rowTotals[r])} × {Fmt.Int(colTotals[c])}) / {Fmt.Int(total)} = {Fmt.Num(e, 2)}"
                                       + $"   (O − E)²/E = ({Fmt.Int(table[r, c])} − {Fmt.Num(e, 2)})² / {Fmt.Num(e, 2)} = {Fmt.Num(selisih * selisih / e, 3)}"
                        });
                    }
                }
            }
            else
            {
                langkah.Add(new LangkahHitung
                {
                    Uraian = "Frekuensi harapan",
                    Hitungan = $"Eᵢⱼ = (barisᵢ × kolomⱼ) / N,   N = {Fmt.Int(total)}, {jumlahSel} sel"
                });
            }

            langkah.Add(new LangkahHitung
            {
                Uraian = "Statistik χ²",
                Hitungan = $"χ² = Σ (O − E)² / E = {Fmt.Num(chi2, 3)}   df = (baris−1)(kolom−1) = {Fmt.Int(df)}"
                           + $"   →   p = {Fmt.P(p)}"
            });
            langkah.Add(new LangkahHitung
            {
                Uraian = "V Cramér",
                Hitungan = $"V = √(χ² / (N · (k − 1))) = √({Fmt.Num(chi2, 3)} / ({Fmt.Int(total)} × {Fmt.Int(minDim)})) = {Fmt.Num(cramersV, 3)}"
            });
            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data", langkah));

            blocks.Add(Blocks.Table("Frekuensi harapan", displayCols.Take(cols.Count + 1).ToList(), expectedRows,
                "Chi-kuadrat dapat dipercaya bila sebagian besar sel punya frekuensi harapan ≥ 5."));

            blocks.Add(Blocks.Table("Uji chi-kuadrat",
                new[] { "Uji", "Nilai", "df", "p", "Keputusan (α = 0,05)" },
                new[]
                {
                    new[] { "Chi-kuadrat Pearson", Fmt.Num(chi2, 3), Fmt.Int(df), Fmt.P(p), Compare.Keputusan(p, alpha) },
                    new[] { "Rasio kemungkinan (G²)", Fmt.Num(g2, 3), Fmt.Int(df), Fmt.P(pG), Compare.Keputusan(pG, alpha) }
                },
                "H0: kedua variabel saling bebas."));

            blocks.Add(Blocks.Table("Ukuran asosiasi",
                new[] { "Ukuran", "Nilai", "Tafsir" },
                new[]
                {
                    new[] { "Cramér's V", Fmt.Num(cramersV, 3), TafsirV(cramersV) },
                    new[] { "Koefisien kontingensi", Fmt.Num(contingency, 3), Fmt.NA }
                },
                "Cramér's V: 0,1 lemah · 0,3 sedang · 0,5 kuat."));

            if (minExpectedZero > 0)
                blocks.Add(Blocks.Note(
                    $"{minExpectedZero} sel punya frekuensi harapan di bawah 5. "
                    + "Chi-kuadrat jadi kurang dapat dipercaya; untuk tabel 2x2 pakai uji pasti Fisher di bawah.",
                    NoteKind.Warning));

            if (rows.Count == 2 && cols.Count == 2)
            {
                double fisherP = FisherExact2x2(table[0, 0], table[0, 1], table[1, 0], table[1, 1]);
                blocks.Add(Blocks.Rumus("Rumus tambahan untuk tabel 2×2", DaftarRumus.UjiFisher));
                blocks.Add(Blocks.Substitusi("Pemasukan nilai (uji pasti Fisher)",
                    ("Tabel 2×2", $"a = {Fmt.Int(table[0, 0])}   b = {Fmt.Int(table[0, 1])}   "
                                  + $"c = {Fmt.Int(table[1, 0])}   d = {Fmt.Int(table[1, 1])}   N = {Fmt.Int(total)}"),
                    ("Peluang", $"p = Σ P(T) untuk semua T dengan P(T) ≤ P(T₀) = {Fmt.P(fisherP)}")));
                blocks.Add(Blocks.Table("Uji pasti Fisher (2×2)",
                    new[] { "p (2 sisi)", "Keputusan (α = 0,05)" },
                    new[] { new[] { Fmt.P(fisherP), Compare.Keputusan(fisherP, alpha) } },
                    "Tepat, tidak memakai pendekatan; dipakai bila ada sel yang kecil."));
            }

            return blocks;
        }

        public static double FisherExact2x2(int a, int b, int c, int d)
        {
            int n = a + b + c + d;
            int r1 = a + b, r2 = c + d, c1 = a + c;
            if (n == 0) return double.NaN;

            double pObserved = Hypergeom(a, r1, r2, c1);
            double sum = 0;
            int lo = Math.Max(0, c1 - r2);
            int hi = Math.Min(r1, c1);

            for (int x = lo; x <= hi; x++)
            {
                double px = Hypergeom(x, r1, r2, c1);
                if (px <= pObserved * (1 + 1e-7)) sum += px;
            }
            return Math.Min(1.0, sum);
        }

        private static double Hypergeom(int x, int r1, int r2, int c1)
        {
            int n = r1 + r2;
            return Math.Exp(LogChoose(r1, x) + LogChoose(r2, c1 - x) - LogChoose(n, c1));
        }

        private static double LogChoose(int n, int k)
        {
            if (k < 0 || k > n) return double.NegativeInfinity;
            double result = 0;
            for (int i = 1; i <= k; i++) result += Math.Log(n - k + i) - Math.Log(i);
            return result;
        }

        private static string TafsirV(double v)
        {
            if (double.IsNaN(v)) return "tidak dapat dihitung";
            if (v < 0.1) return "sangat lemah";
            if (v < 0.3) return "lemah";
            if (v < 0.5) return "sedang";
            return "kuat";
        }
    }
}
