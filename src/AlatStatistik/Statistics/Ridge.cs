using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public class HasilRidge
    {
        public double[] Beta = Array.Empty<double>();   
        public double[] Se = Array.Empty<double>();
        public double R2, AdjR2, SeEstimasi, F, FP;
        public double Sst, Sse;
        public int N, DfReg, DfRes;
        public string? Catatan;
    }

    public static class Ridge
    {
        public static HasilRidge? Fit(double[] y, List<double[]> prediktor, double alpha)
        {
            int n = y.Length;
            int p = prediktor.Count;
            if (n < p + 2 || p < 0) return null;
            if (prediktor.Any(v => v.Length != n)) return null;
            if (alpha < 0) return null;

            int k = p + 1;
            var x = new double[n][];
            for (int i = 0; i < n; i++)
            {
                x[i] = new double[k];
                x[i][0] = 1.0;
                for (int j = 0; j < p; j++) x[i][j + 1] = prediktor[j][i];
            }

            var xtx = new double[k, k];
            var xty = new double[k];
            for (int i = 0; i < n; i++)
            {
                for (int a = 0; a < k; a++)
                {
                    xty[a] += x[i][a] * y[i];
                    for (int b = 0; b < k; b++) xtx[a, b] += x[i][a] * x[i][b];
                }
            }
            
            for (int i = 1; i < k; i++) xtx[i, i] += alpha;

            var inv = Regression.Inverse(xtx);
            if (inv is null) return null;

            var beta = new double[k];
            for (int a = 0; a < k; a++)
            {
                double sum = 0;
                for (int b = 0; b < k; b++) sum += inv[a, b] * xty[b];
                beta[a] = sum;
            }

            var resid = new double[n];
            double meanY = y.Average();
            double ssRes = 0, ssTot = 0;
            for (int i = 0; i < n; i++)
            {
                double pred = 0;
                for (int a = 0; a < k; a++) pred += beta[a] * x[i][a];
                resid[i] = y[i] - pred;
                ssRes += resid[i] * resid[i];
                ssTot += (y[i] - meanY) * (y[i] - meanY);
            }

            int dfRes = n - k;
            if (dfRes <= 0) return null;
            double s2 = ssRes / dfRes;
            var se = new double[k];
            for (int a = 0; a < k; a++) se[a] = Math.Sqrt(Math.Max(0, s2 * inv[a, a]));

            double ssReg = ssTot - ssRes;
            double r2 = ssTot > 0 ? 1 - ssRes / ssTot : double.NaN;
            double adjR2 = 1 - (1 - r2) * (n - 1) / (n - k);
            double msReg = ssReg / (k - 1);
            double f = msReg > 0 ? msReg / s2 : double.NaN;

            return new HasilRidge
            {
                Beta = beta,
                Se = se,
                R2 = r2,
                AdjR2 = adjR2,
                SeEstimasi = Math.Sqrt(s2),
                F = f,
                FP = Distributions.FUpper(f, k - 1, dfRes),
                Sst = ssTot,
                Sse = ssRes,
                N = n,
                DfReg = k - 1,
                DfRes = dfRes
            };
        }

        public static List<ResultBlock> RidgeBlocks(Dataset ds, string dependen, List<string> prediktor,
                                                    double alpha, double alfaT = 0.05)
        {
            var blok = new List<ResultBlock> { Blocks.Heading($"Regresi Ridge (α = {Fmt.Num(alpha, 3)}) — {dependen}", 1) };

            if (prediktor.Count == 0)
            {
                blok.Add(Blocks.Note("Pilih sedikitnya satu variabel bebas.", NoteKind.Error));
                return blok;
            }

            var vars = new List<string> { dependen };
            vars.AddRange(prediktor);
            var keep = ds.CompleteRows(vars);
            int p = prediktor.Count;
            if (keep.Count < p + 2)
            {
                blok.Add(Blocks.Note(
                    $"Butuh sedikitnya {p + 2} baris lengkap untuk {p} variabel bebas, tersedia {keep.Count}.",
                    NoteKind.Error));
                return blok;
            }

            var barisSah = new List<int>();
            for (int i = 0; i < keep.Count; i++)
            {
                int r = keep[i];
                bool ok = Dataset.ToDouble(ds.Rows[r][ds.IndexOf(dependen)]).HasValue;
                for (int j = 0; j < p && ok; j++)
                    ok = Dataset.ToDouble(ds.Rows[r][ds.IndexOf(prediktor[j])]).HasValue;
                if (ok) barisSah.Add(r);
            }
            if (barisSah.Count < p + 2)
            {
                blok.Add(Blocks.Note(
                    $"Butuh sedikitnya {p + 2} baris dengan angka sah untuk {p} variabel bebas, "
                    + $"tersedia {barisSah.Count}.", NoteKind.Error));
                return blok;
            }

            var y = new double[barisSah.Count];
            var kolom = new List<double[]>();
            for (int j = 0; j < p; j++) kolom.Add(new double[barisSah.Count]);
            for (int i = 0; i < barisSah.Count; i++)
            {
                int r = barisSah[i];
                y[i] = Dataset.ToDouble(ds.Rows[r][ds.IndexOf(dependen)])!.Value;
                for (int j = 0; j < p; j++)
                    kolom[j][i] = Dataset.ToDouble(ds.Rows[r][ds.IndexOf(prediktor[j])])!.Value;
            }

            var model = Fit(y, kolom, alpha);
            if (model is null)
            {
                blok.Add(Blocks.Note("Matriks variabel bebas singular; hapus satu prediktor lalu ulangi.",
                    NoteKind.Error));
                return blok;
            }

            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.RegresiRidge, DaftarRumus.RegresiLinear));

            var langkah = new List<LangkahHitung>
            {
                new() { Uraian = "Banyak amatan", Hitungan = $"n = {Fmt.Int(model.N)}   prediktor = {Fmt.Int(p)}" },
                new() { Uraian = "Parameter penalti", Hitungan = $"α = {Fmt.Num(alpha, 4)} (diberikan pada slope, bukan konstanta)" },
                new() { Uraian = "Jumlah kuadrat", Hitungan = $"SS_total = {Fmt.Num(model.Sst, 3)}   SS_residual = {Fmt.Num(model.Sse, 3)}" }
            };
            var suku = new List<string> { Fmt.Num(model.Beta[0], 3) };
            for (int j = 0; j < p; j++) suku.Add($"{Fmt.Num(model.Beta[j + 1], 3)}·{prediktor[j]}");
            langkah.Add(new LangkahHitung
            {
                Uraian = "Persamaan",
                Hitungan = $"ŷ = {string.Join("  +  ", suku)}"
            });
            langkah.Add(new LangkahHitung
            {
                Uraian = "Koefisien determinasi",
                Hitungan = $"R² = 1 − SS_res/SS_tot = {Fmt.Num(model.R2, 3)}   (tersesuaikan {Fmt.Num(model.AdjR2, 3)})"
            });
            blok.Add(Blocks.Substitusi("Pemasukan nilai dari data", langkah));

            var baris = new List<List<string>>();
            for (int j = 0; j < p + 1; j++)
            {
                double b = model.Beta[j], se = model.Se[j];
                double batas = 1.959963984540054 * se;
                baris.Add(new List<string>
                {
                    j == 0 ? "(Konstanta)" : prediktor[j - 1],
                    Fmt.Num(b, 4),
                    Fmt.Num(se, 4),
                    $"[{Fmt.Num(b - batas, 4)}; {Fmt.Num(b + batas, 4)}]"
                });
            }
            blok.Add(Blocks.Table("Koefisien (terkena penalti)",
                new[] { "Variabel", "B", "Galat baku", "Selang 95% untuk B" },
                baris,
                "Untuk regresi Ridge, uji t/z atas koefisien tidak standar dilakukan; selang di sini "
                + "hanya berasal dari ragam (XᵀX+P)⁻¹·s². Konstanta tidak diberi penalti."));

            blok.Add(Blocks.Table("Ringkasan model",
                new[] { "R²", "R² tersesuaikan", "Galat baku taksiran", "F", "p", "N" },
                new[]
                {
                    new[]
                    {
                        Fmt.Num(model.R2, 3), Fmt.Num(model.AdjR2, 3), Fmt.Num(model.SeEstimasi, 3),
                        Fmt.Num(model.F, 3), Fmt.P(model.FP), Fmt.Int(model.N)
                    }
                },
                "F menguji apakah seluruh prediktor (setelah penalti) bersama-sama berhubungan dengan y."));

            return blok;
        }
    }
}
