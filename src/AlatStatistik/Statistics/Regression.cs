using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public class FitResult
    {
        public double[] Beta = Array.Empty<double>();      
        public double[] Se = Array.Empty<double>();
        public double[] Residuals = Array.Empty<double>();
        public double[] Fitted = Array.Empty<double>();
        public double[,]? Inv;
        public double[] BetaTerstandar = Array.Empty<double>();
        public double SeEstimasi;
        public double Ssr;
        public double Sse;
        public double Sst;
        public double R2;
        public double AdjR2;
        public double MsRes;
        public double F;
        public double FP;
        public int N;
        public int DfReg;
        public int DfRes;
    }

    public static class Regression
    {

        public static FitResult? Fit(double[] y, List<double[]> predictors)
        {
            int n = y.Length;
            int p = predictors.Count;

            if (n < p + 2 || p < 0) return null;
            if (predictors.Any(v => v.Length != n)) return null;

            int k = p + 1;                       

            var x = new double[n][];
            for (int i = 0; i < n; i++)
            {
                x[i] = new double[k];
                x[i][0] = 1.0;
                for (int j = 0; j < p; j++) x[i][j + 1] = predictors[j][i];
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

            var inv = Inverse(xtx);
            if (inv == null) return null;

            var beta = new double[k];
            for (int a = 0; a < k; a++)
            {
                double sum = 0;
                for (int b = 0; b < k; b++) sum += inv[a, b] * xty[b];
                beta[a] = sum;
            }

            var fitted = new double[n];
            var resid = new double[n];
            double meanY = y.Average();
            double ssRes = 0, ssTot = 0;
            for (int i = 0; i < n; i++)
            {
                double pred = 0;
                for (int a = 0; a < k; a++) pred += beta[a] * x[i][a];
                fitted[i] = pred;
                resid[i] = y[i] - pred;
                ssRes += resid[i] * resid[i];
                ssTot += (y[i] - meanY) * (y[i] - meanY);
            }

            double ssReg = ssTot - ssRes;
            int dfReg = p, dfRes = n - k;
            double msRes = ssRes / dfRes;
            double msReg = ssReg / dfReg;
            double f = msRes > 0 ? msReg / msRes : double.NaN;
            double r2 = ssTot > 0 ? 1 - ssRes / ssTot : double.NaN;
            double adjR2 = 1 - (1 - r2) * (n - 1) / (n - k);

            var se = new double[k];
            for (int a = 0; a < k; a++) se[a] = Math.Sqrt(Math.Max(0, msRes * inv[a, a]));

            
            
            double sdY = Math.Sqrt(y.Sum(v => (v - meanY) * (v - meanY)) / (n - 1));
            var betaStd = new double[k];
            betaStd[0] = double.NaN;
            for (int j = 0; j < p; j++)
            {
                double mj = predictors[j].Average();
                double sdj = Math.Sqrt(predictors[j].Sum(v => (v - mj) * (v - mj)) / (n - 1));
                betaStd[j + 1] = sdY > 0 ? beta[j + 1] * sdj / sdY : double.NaN;
            }

            return new FitResult
            {
                Beta = beta,
                Se = se,
                BetaTerstandar = betaStd,
                SeEstimasi = Math.Sqrt(msRes),
                Residuals = resid,
                Fitted = fitted,
                Inv = inv,
                Ssr = ssReg,
                Sse = ssRes,
                Sst = ssTot,
                R2 = r2,
                AdjR2 = adjR2,
                MsRes = msRes,
                F = f,
                FP = Distributions.FUpper(f, dfReg, dfRes),
                N = n,
                DfReg = dfReg,
                DfRes = dfRes
            };
        }

        public static List<ResultBlock> Linear(Dataset ds, string dependent, List<string> predictors,
                                               double alpha = 0.05)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading($"Regresi linear — {dependent}", 1) };

            if (predictors.Count == 0)
            {
                blocks.Add(Blocks.Note("Pilih sedikitnya satu variabel bebas.", NoteKind.Error));
                return blocks;
            }

            var vars = new List<string> { dependent };
            vars.AddRange(predictors);
            var keep = ds.CompleteRows(vars);

            int p = predictors.Count;
            if (keep.Count < p + 2)
            {
                blocks.Add(Blocks.Note(
                    $"Butuh sedikitnya {p + 2} baris lengkap untuk {p} variabel bebas, tersedia {keep.Count}.",
                    NoteKind.Error));
                return blocks;
            }

            

            var barisTerpakai = new List<int>();
            for (int i = 0; i < keep.Count; i++)
            {
                int r = keep[i];
                bool lengkap = Dataset.ToDouble(ds.Rows[r][ds.IndexOf(dependent)]).HasValue;
                for (int j = 0; j < p && lengkap; j++)
                    lengkap = Dataset.ToDouble(ds.Rows[r][ds.IndexOf(predictors[j])]).HasValue;
                if (lengkap) barisTerpakai.Add(r);
            }

            int dibuang = keep.Count - barisTerpakai.Count;
            if (dibuang > 0)
                blocks.Add(Blocks.Note(
                    $"{dibuang} baris dilewati karena salah satu nilainya bukan angka "
                    + "(misalnya teks pada kolom numerik). Periksa tab Data bila jumlah ini "
                    + "lebih besar dari yang kau duga.", NoteKind.Warning));

            if (barisTerpakai.Count < p + 2)
            {
                blocks.Add(Blocks.Note(
                    $"Butuh sedikitnya {p + 2} baris dengan angka yang sah untuk {p} variabel bebas, "
                    + $"tersedia {barisTerpakai.Count}.", NoteKind.Error));
                return blocks;
            }

            var y = new double[barisTerpakai.Count];
            var kolom = new List<double[]>();
            for (int j = 0; j < p; j++) kolom.Add(new double[barisTerpakai.Count]);

            for (int i = 0; i < barisTerpakai.Count; i++)
            {
                int r = barisTerpakai[i];
                y[i] = Dataset.ToDouble(ds.Rows[r][ds.IndexOf(dependent)])!.Value;
                for (int j = 0; j < p; j++)
                    kolom[j][i] = Dataset.ToDouble(ds.Rows[r][ds.IndexOf(predictors[j])])!.Value;
            }

            var model = Fit(y, kolom);
            if (model == null)
            {
                blocks.Add(Blocks.Note(
                    "Matriks variabel bebas singular (ada variabel yang merupakan gabungan linear "
                    + "dari variabel lain). Hapus salah satunya lalu ulangi.", NoteKind.Error));
                return blocks;
            }

            int n = model.N;
            double msRes = model.MsRes;
            double seKonstanta = model.Se[0];
            double tKonstanta = seKonstanta > 0 ? model.Beta[0] / seKonstanta : double.NaN;
            double tCrit = Distributions.StudentTInv(1 - alpha / 2.0, model.DfRes);

            
            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.RegresiLinear, DaftarRumus.KoefisienTerstandar,
                DaftarRumus.GalatBakuTaksiran, DaftarRumus.Rerata, DaftarRumus.RagamSampel));

            var langkah = new List<LangkahHitung>
            {
                new()
                {
                    Uraian = "Banyak amatan",
                    Hitungan = $"n = {Fmt.Int(n)}   prediktor = {Fmt.Int(p)}   (konstanta disertakan)"
                }
            };

            
            var suku = new List<string> { Fmt.Num(model.Beta[0], 3) };
            for (int j = 0; j < p; j++)
                suku.Add($"{Fmt.Num(model.Beta[j + 1], 3)}·{predictors[j]}");
            langkah.Add(new LangkahHitung
            {
                Uraian = "Persamaan regresi",
                Hitungan = $"ŷ = {string.Join("  +  ", suku)}"
            });

            langkah.Add(new LangkahHitung
            {
                Uraian = "Jumlah kuadrat",
                Hitungan = $"SS_total = {Fmt.Num(model.Sst, 3)}   SS_residual = {Fmt.Num(model.Sse, 3)}"
                           + $"   SS_regresi = {Fmt.Num(model.Ssr, 3)}"
            });
            langkah.Add(new LangkahHitung
            {
                Uraian = "Koefisien determinasi",
                Hitungan = $"R² = 1 − SS_res/SS_tot = 1 − {Fmt.Num(model.Sse, 3)} / {Fmt.Num(model.Sst, 3)} = {Fmt.Num(model.R2, 3)}"
            });
            langkah.Add(new LangkahHitung
            {
                Uraian = "Galat baku taksiran",
                Hitungan = $"s_e = √(SS_res / df_res) = √({Fmt.Num(model.Sse, 3)} / {Fmt.Int(model.DfRes)})"
                           + $" = {Fmt.Num(model.SeEstimasi, 3)}"
            });
            langkah.Add(new LangkahHitung
            {
                Uraian = "Koefisien terstandar",
                Hitungan = string.Join("   ", Enumerable.Range(0, p).Select(j =>
                    $"Beta_{j + 1} = {Fmt.Num(model.Beta[j + 1], 3)}·(s_{j + 1}/s_y) = {Fmt.Num(model.BetaTerstandar[j + 1], 3)}"))
            });
            langkah.Add(new LangkahHitung
            {
                Uraian = "Uji F keseluruhan",
                Hitungan = $"F = MS_reg / MS_res = {Fmt.Num(model.Ssr / model.DfReg, 3)} / {Fmt.Num(model.MsRes, 3)}"
                           + $" = {Fmt.Num(model.F, 3)}   df = ({Fmt.Int(model.DfReg)}; {Fmt.Int(model.DfRes)})   →   p = {Fmt.P(model.FP)}"
            });
            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data", langkah));

            var coefCols = new[] { "Variabel", "B", "Galat baku", "Beta", "t", "p", "Selang kepercayaan 95%", "VIF" };
            var coefRows = new List<List<string>>
            {
                new()
                {
                    "(Konstanta)", Fmt.Num(model.Beta[0], 3), Fmt.Num(seKonstanta, 3),
                    "—", Fmt.Num(tKonstanta, 3),
                    Fmt.P(Distributions.StudentTTwoSided(tKonstanta, model.DfRes)),
                    $"[{Fmt.Num(model.Beta[0] - tCrit * seKonstanta, 3)}; {Fmt.Num(model.Beta[0] + tCrit * seKonstanta, 3)}]",
                    "—"
                }
            };

            for (int j = 0; j < p; j++)
            {
                int idx = j + 1;
                double se = model.Se[idx];
                double t = se > 0 ? model.Beta[idx] / se : double.NaN;
                double pv = Distributions.StudentTTwoSided(t, model.DfRes);
                double betaStd = model.BetaTerstandar[idx];
                double vif = 1.0 / Math.Max(1e-12, 1.0 - R2Of(ds, predictors, j, keep));

                coefRows.Add(new List<string>
                {
                    predictors[j], Fmt.Num(model.Beta[idx], 3), Fmt.Num(se, 3), Fmt.Num(betaStd, 3),
                    Fmt.Num(t, 3), Fmt.P(pv),
                    $"[{Fmt.Num(model.Beta[idx] - tCrit * se, 3)}; {Fmt.Num(model.Beta[idx] + tCrit * se, 3)}]",
                    Fmt.Num(vif, 2)
                });
            }

            blocks.Add(Blocks.Table("Koefisien", coefCols, coefRows,
                "Beta = koefisien terstandar. VIF &gt; 5 patut dicurigai, &gt; 10 berarti multikolinearitas serius."));

            blocks.Add(Blocks.Table("Ringkasan model",
                new[] { "R", "R²", "R² tersesuaikan", "Galat baku taksiran", "Durbin–Watson", "N" },
                new[]
                {
                    new[]
                    {
                        Fmt.Num(Math.Sqrt(Math.Max(0, model.R2)), 3), Fmt.Num(model.R2, 3), Fmt.Num(model.AdjR2, 3),
                        Fmt.Num(model.SeEstimasi, 3), Fmt.Num(DurbinWatson(model.Residuals), 3), Fmt.Int(n)
                    }
                },
                "Durbin–Watson mendekati 2 berarti residual saling bebas; jauh di bawah 2 menandakan autokorelasi positif."));

            blocks.Add(Blocks.Table("ANOVA",
                new[] { "Sumber", "Jumlah kuadrat", "df", "Kuadrat tengah", "F", "p" },
                new[]
                {
                    new[] { "Regresi", Fmt.Num(model.Ssr, 3), Fmt.Int(model.DfReg),
                            Fmt.Num(model.Ssr / model.DfReg, 3), Fmt.Num(model.F, 3), Fmt.P(model.FP) },
                    new[] { "Residual", Fmt.Num(model.Sse, 3), Fmt.Int(model.DfRes),
                            Fmt.Num(model.MsRes, 3), Fmt.NA, Fmt.NA },
                    new[] { "Total", Fmt.Num(model.Sst, 3), Fmt.Int(n - 1), Fmt.NA, Fmt.NA, Fmt.NA }
                },
                $"H0: semua koefisien sama dengan nol. {Compare.Keputusan(model.FP, alpha)}"));

            return blocks;
        }

        

        public static double[,]? Inverse(double[,] a)
        {
            int n = a.GetLength(0);
            var m = new double[n, 2 * n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) { m[i, j] = a[i, j]; m[i, j + n] = i == j ? 1.0 : 0.0; }
            }

            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < n; r++)
                    if (Math.Abs(m[r, col]) > Math.Abs(m[pivot, col])) pivot = r;

                // Ditulis "!(... > tol)" bukan "< tol": Math.Abs(NaN) < tol
                // bernilai salah, sehingga matriks singular ber-NaN lolos
                // penjaga ini dan matriks balikan ikut menjadi NaN.
                if (!(Math.Abs(m[pivot, col]) > 1e-12)) return null;

                if (pivot != col)
                    for (int j = 0; j < 2 * n; j++)
                        (m[col, j], m[pivot, j]) = (m[pivot, j], m[col, j]);

                double div = m[col, col];
                for (int j = 0; j < 2 * n; j++) m[col, j] /= div;

                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    double factor = m[r, col];
                    if (Math.Abs(factor) < 1e-15) continue;
                    for (int j = 0; j < 2 * n; j++) m[r, j] -= factor * m[col, j];
                }
            }

            var result = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) result[i, j] = m[i, j + n];
            return result;
        }

        private static double R2Of(Dataset ds, List<string> predictors, int j, List<int> rows)
        {
            var others = predictors.Where((_, idx) => idx != j).ToList();
            if (others.Count == 0) return 0.0;

            // Sel berisi teks membuat ToDouble null, dan !.Value melempar. Saring
            // baris yang semua selnya benar-benar angka, seperti barisTerpakai.
            int idY = ds.IndexOf(predictors[j]);
            var idLain = others.Select(o => ds.IndexOf(o)).ToList();
            var pakai = rows.Where(r =>
                Dataset.ToDouble(ds.Rows[r][idY]).HasValue
                && idLain.All(c => Dataset.ToDouble(ds.Rows[r][c]).HasValue)).ToList();

            int n = pakai.Count;
            if (n == 0) return 0.0;
            var y = new double[n];
            var kolom = new List<double[]>();
            for (int t = 0; t < others.Count; t++) kolom.Add(new double[n]);

            for (int i = 0; i < n; i++)
            {
                int r = pakai[i];
                y[i] = Dataset.ToDouble(ds.Rows[r][idY])!.Value;
                for (int t = 0; t < others.Count; t++)
                    kolom[t][i] = Dataset.ToDouble(ds.Rows[r][idLain[t]])!.Value;
            }

            var model = Fit(y, kolom);
            return model != null ? model.R2 : 0.0;
        }

        private static double DurbinWatson(double[] resid)
        {
            double num = 0, den = 0;
            for (int i = 1; i < resid.Length; i++) num += Math.Pow(resid[i] - resid[i - 1], 2);
            for (int i = 0; i < resid.Length; i++) den += resid[i] * resid[i];
            return den > 0 ? num / den : double.NaN;
        }
    }
}
