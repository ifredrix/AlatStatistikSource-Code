using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public class HasilQuantile
    {
        public double Q;
        public double[] Beta = Array.Empty<double>();   
        public double[] Se = Array.Empty<double>();
        public double[] T = Array.Empty<double>();
        public double[] P = Array.Empty<double>();
        public int N, K, Df;
        public int Iterasi;
        public double Sparsity;       
        public double Bandwidth;      
        public double PseudoR2;
    }

    public static class QuantileRegression
    {
        public static HasilQuantile? Fit(double[] y, List<double[]> prediktor, double q,
                                         int maxIter = 1000, double pTol = 1e-6)
        {
            int n = y.Length;
            int p = prediktor.Count;
            if (n < 3 || p < 0) return null;
            if (prediktor.Any(v => v.Length != n)) return null;
            if (q <= 0 || q >= 1) return null;

            int k = p + 1;
            
            var X = new double[n * k];
            for (int i = 0; i < n; i++)
            {
                X[i * k] = 1.0;
                for (int j = 0; j < p; j++) X[i * k + j + 1] = prediktor[j][i];
            }

            
            var beta = new double[k];
            for (int j = 0; j < k; j++) beta[j] = 1.0;     
            var xstar = (double[])X.Clone();
            double diff = 10.0;
            int iter = 0;
            while (iter < maxIter && diff > pTol)
            {
                iter++;
                var beta0 = (double[])beta.Clone();
                
                var xtx = new double[k * k];
                var xty = new double[k];
                for (int i = 0; i < n; i++)
                {
                    for (int a = 0; a < k; a++)
                    {
                        double xs = xstar[i * k + a];
                        xty[a] += xs * y[i];
                        for (int b = 0; b < k; b++) xtx[a * k + b] += xs * X[i * k + b];
                    }
                }
                var inv = Inverse(xtx, k);
                if (inv is null) return null;
                var betaNew = new double[k];
                for (int a = 0; a < k; a++)
                {
                    double s = 0;
                    for (int b = 0; b < k; b++) s += inv[a * k + b] * xty[b];
                    betaNew[a] = s;
                }
                
                var resid = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double r = y[i];
                    for (int j = 0; j < k; j++) r -= X[i * k + j] * betaNew[j];
                    resid[i] = r;
                }
                for (int i = 0; i < n; i++)
                {
                    if (Math.Abs(resid[i]) < 1e-6)
                        resid[i] = (resid[i] >= 0 ? 1.0 : -1.0) * 1e-6;
                    resid[i] = resid[i] < 0 ? q * resid[i] : (1 - q) * resid[i];
                    resid[i] = Math.Abs(resid[i]);
                }
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < k; j++) xstar[i * k + j] = X[i * k + j] / resid[i];
                diff = 0;
                for (int j = 0; j < k; j++) diff = Math.Max(diff, Math.Abs(betaNew[j] - beta0[j]));
                beta = betaNew;
            }

            
            var e = new double[n];
            for (int i = 0; i < n; i++)
            {
                double r = y[i];
                for (int j = 0; j < k; j++) r -= X[i * k + j] * beta[j];
                e[i] = r;
            }

            
            double iqre = Percentile(e, 75) - Percentile(e, 25);
            double h = HallSheather(n, q);
            double sigmaY = StdPop(y);
            h = Math.Min(sigmaY, iqre / 1.34) * (NormPpf(q + h) - NormPpf(q - h));
            double fhat0 = 0;
            for (int i = 0; i < n; i++) fhat0 += Epanechnikov(e[i] / h);
            fhat0 = 1.0 / (n * h) * fhat0;
            double sparsity = 1.0 / fhat0;

            
            var d = new double[n];
            for (int i = 0; i < n; i++)
                d[i] = e[i] > 0 ? Math.Pow(q / fhat0, 2) : Math.Pow((1 - q) / fhat0, 2);
            var xtx2 = new double[k * k];
            for (int a = 0; a < k; a++)
                for (int b = 0; b < k; b++)
                {
                    double s = 0;
                    for (int i = 0; i < n; i++) s += X[i * k + a] * X[i * k + b];
                    xtx2[a * k + b] = s;
                }
            var xtxi = Inverse(xtx2, k);
            if (xtxi is null) return null;
            var xtdx = new double[k * k];
            for (int a = 0; a < k; a++)
                for (int b = 0; b < k; b++)
                {
                    double s = 0;
                    for (int i = 0; i < n; i++) s += X[i * k + a] * d[i] * X[i * k + b];
                    xtdx[a * k + b] = s;
                }
            var tmp = MatMul(xtxi, xtdx, k);
            var vcov = MatMul(tmp, xtxi, k);

            var se = new double[k];
            for (int j = 0; j < k; j++) se[j] = Math.Sqrt(Math.Max(0, vcov[j * k + j]));

            int df = n - k;
            var t = new double[k];
            var pval = new double[k];
            for (int j = 0; j < k; j++)
            {
                t[j] = se[j] != 0 ? beta[j] / se[j] : 0;
                pval[j] = 2 * (1 - StudentTcdf(Math.Abs(t[j]), df));
            }

            
            double vhat = 0, v0 = 0;
            double yq = Percentile(y, q * 100);
            for (int i = 0; i < n; i++)
            {
                vhat += Math.Abs(e[i] < 0 ? (1 - q) * e[i] : q * e[i]);
                double er = y[i] - yq;
                v0 += Math.Abs(er < 0 ? (1 - q) * er : q * er);
            }
            double pseudoR2 = v0 != 0 ? 1 - vhat / v0 : 0;

            return new HasilQuantile
            {
                Q = q, Beta = beta, Se = se, T = t, P = pval,
                N = n, K = k, Df = df, Iterasi = iter,
                Sparsity = sparsity, Bandwidth = h, PseudoR2 = pseudoR2
            };
        }

        
        public static List<ResultBlock> QuantileBlocks(Dataset ds, string dependen,
            List<string> prediktor, string? opsiKuantil)
        {
            var blok = new List<ResultBlock>();

            var kuantil = ParseKuantil(opsiKuantil);
            if (kuantil.Count == 0)
            {
                blok.Add(Blocks.Note(
                    "Tidak ada nilai kuantil (τ) yang sah. Pakai τ = 0,25; 0,5; 0,75.",
                    NoteKind.Error));
                kuantil = new List<double> { 0.25, 0.5, 0.75 };
            }

            int p = prediktor.Count;
            var vars = new List<string> { dependen };
            vars.AddRange(prediktor);
            var keep = ds.CompleteRows(vars);
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
                    $"Butuh sedikitnya {p + 2} baris dengan angka sah untuk {p} variabel bebas, " +
                    $"tersedia {barisSah.Count}.", NoteKind.Error));
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

            blok.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.RegresiQuantile, DaftarRumus.RegresiLinear));

            var langkah = new List<LangkahHitung>
            {
                new() { Uraian = "Banyak amatan", Hitungan = $"n = {Fmt.Int(barisSah.Count)}   prediktor = {Fmt.Int(p)}" },
                new() { Uraian = "Kuantil diminta (τ)", Hitungan = string.Join(", ", kuantil.Select(q => Fmt.Num(q, 2))) },
                new() { Uraian = "Penyelesaian", Hitungan = "Iterative Weighted Least Squares (IWLS), batas 1000 langkah, tol 1e-6" }
            };
            blok.Add(Blocks.Substitusi("Pemasukan nilai dari data", langkah));

            foreach (double q in kuantil)
            {
                var model = Fit(y, kolom, q);
                if (model is null)
                {
                    blok.Add(Blocks.Note(
                        $"τ = {Fmt.Num(q, 2)}: matriks tidak bisa diinvers (mungkin prediktor kolinear).",
                        NoteKind.Error));
                    continue;
                }

                blok.Add(Blocks.Heading($"Kuantil τ = {Fmt.Num(q, 2)}", 2));

                var barisK = new List<List<string>>();
                for (int j = 0; j < p + 1; j++)
                {
                    barisK.Add(new List<string>
                    {
                        j == 0 ? "(Konstanta)" : prediktor[j - 1],
                        Fmt.Num(model.Beta[j], 4),
                        Fmt.Num(model.Se[j], 4),
                        Fmt.Num(model.T[j], 3),
                        Fmt.P(model.P[j])
                    });
                }
                blok.Add(Blocks.Table($"Koefisien kuantil τ = {Fmt.Num(q, 2)}",
                    new[] { "Variabel", "B(τ)", "Galat baku (robust)", "t", "p" },
                    barisK,
                    "Galat baku heteroskedastisitas-robust (sandwich, Hall–Sheather + Epanechnikov). " +
                    "t memakai distribusi Student-t dengan df = n − k."));

                blok.Add(Blocks.Table($"Ringkasan τ = {Fmt.Num(q, 2)}",
                    new[] { "τ", "Pseudo R²", "Sparsity (1/f̂₀)", "Bandwidth (h)", "N", "df" },
                    new[]
                    {
                        new[]
                        {
                            Fmt.Num(model.Q, 2),
                            Fmt.Num(model.PseudoR2, 4),
                            Fmt.Num(model.Sparsity, 4),
                            Fmt.Num(model.Bandwidth, 4),
                            Fmt.Int(model.N),
                            Fmt.Int(model.Df)
                        }
                    },
                    $"Pseudo R² = 1 − Σρ_τ(residual) / Σρ_τ(y − kuantil(y, τ)); " +
                    $"konvergen dalam {Fmt.Int(model.Iterasi)} langkah IWLS."));
            }

            blok.Add(Blocks.Note(
                "Regresi kuantil melengkapi OLS: koefisien B(τ) adalah slopa dari hubungan " +
                "antara-kuantil, bukan rerata. Baca beberapa τ bersama-sama untuk melihat " +
                "bagaimana hubungan berubah di sepanjang sebaran y (mis. heteroskedastisitas " +
                "atau efek asimetris).", NoteKind.Info));

            return blok;
        }

        
        private static List<double> ParseKuantil(string? teks)
        {
            var out_ = new List<double>();
            if (string.IsNullOrWhiteSpace(teks)) return out_;
            foreach (var bagian in teks.Split(',', ';', ' '))
            {
                if (double.TryParse(bagian.Trim(),
                        System.Globalization.CultureInfo.InvariantCulture, out double q)
                    && q > 0 && q < 1 && !out_.Contains(q))
                    out_.Add(q);
            }
            out_.Sort();
            return out_.Count <= 10 ? out_ : out_.GetRange(0, 10);
        }

        

        private static double[]? Inverse(double[] a, int k)
        {
            var m = new double[k * k];
            Array.Copy(a, m, k * k);
            var inv = new double[k * k];
            for (int i = 0; i < k; i++) inv[i * k + i] = 1.0;
            for (int col = 0; col < k; col++)
            {
                int pivot = col;
                double max = Math.Abs(m[col * k + col]);
                for (int r = col + 1; r < k; r++)
                {
                    double v = Math.Abs(m[r * k + col]);
                    if (v > max) { max = v; pivot = r; }
                }
                if (max < 1e-14) return null;
                if (pivot != col)
                {
                    for (int c = 0; c < k; c++)
                    {
                        (m[col * k + c], m[pivot * k + c]) = (m[pivot * k + c], m[col * k + c]);
                        (inv[col * k + c], inv[pivot * k + c]) = (inv[pivot * k + c], inv[col * k + c]);
                    }
                }
                double d = m[col * k + col];
                for (int c = 0; c < k; c++)
                {
                    m[col * k + c] /= d;
                    inv[col * k + c] /= d;
                }
                for (int r = 0; r < k; r++)
                {
                    if (r == col) continue;
                    double f = m[r * k + col];
                    if (f == 0) continue;
                    for (int c = 0; c < k; c++)
                    {
                        m[r * k + c] -= f * m[col * k + c];
                        inv[r * k + c] -= f * inv[col * k + c];
                    }
                }
            }
            return inv;
        }

        private static double[] MatMul(double[] a, double[] b, int k)
        {
            var c = new double[k * k];
            for (int i = 0; i < k; i++)
                for (int j = 0; j < k; j++)
                {
                    double s = 0;
                    for (int t = 0; t < k; t++) s += a[i * k + t] * b[t * k + j];
                    c[i * k + j] = s;
                }
            return c;
        }

        

        private static double Percentile(double[] a, double persen)
        {
            int n = a.Length;
            if (n == 0) return 0;
            var s = (double[])a.Clone();
            Array.Sort(s);
            if (n == 1) return s[0];
            double rank = (n - 1) * (persen / 100.0);
            int lo = (int)Math.Floor(rank);
            int hi = (int)Math.Ceiling(rank);
            if (lo == hi) return s[lo];
            double frac = rank - lo;
            return s[lo] + frac * (s[hi] - s[lo]);
        }

        private static double StdPop(double[] a)
        {
            int n = a.Length;
            if (n == 0) return 0;
            double mu = 0;
            foreach (var v in a) mu += v;
            mu /= n;
            double ss = 0;
            foreach (var v in a) { double d = v - mu; ss += d * d; }
            return Math.Sqrt(ss / n);
        }

        private static double Epanechnikov(double u) => Math.Abs(u) <= 1 ? 0.75 * (1 - u * u) : 0;

        private static double HallSheather(int n, double q)
        {
            double z = NormPpf(q);
            double num = 1.5 * NormPdf(z) * NormPdf(z);
            double den = 2 * z * z + 1;
            return Math.Pow(n, -1.0 / 3) * Math.Pow(NormPpf(1 - 0.05 / 2), 2.0 / 3)
                   * Math.Pow(num / den, 1.0 / 3);
        }

        private static double NormPdf(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2 * Math.PI);

        private static double NormPpf(double p)
        {
            double[] a = { -3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02,
                           1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00 };
            double[] b = { -5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02,
                           6.680131188771972e+01, -1.328068155288572e+01 };
            double[] c = { -7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00,
                          -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00 };
            double[] d = { 7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00,
                           3.754408661907416e+00 };
            const double plow = 0.02425;
            if (p < plow)
            {
                double q = Math.Sqrt(-2 * Math.Log(p));
                return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5])
                       / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
            }
            if (p <= 1 - plow)
            {
                double q = p - 0.5;
                double r = q * q;
                return (((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q
                       / (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
            }
            double qq = Math.Sqrt(-2 * Math.Log(1 - p));
            return -(((((c[0] * qq + c[1]) * qq + c[2]) * qq + c[3]) * qq + c[4]) * qq + c[5])
                   / ((((d[0] * qq + d[1]) * qq + d[2]) * qq + d[3]) * qq + 1);
        }

        private static double StudentTcdf(double x, int df)
        {
            if (df <= 0) return 0.5;
            double x2 = x * x;
            double ib = RegIncBeta(df / 2.0, 0.5, df / (df + x2));
            return x > 0 ? 1 - 0.5 * ib : 0.5 * ib;
        }

        private static double RegIncBeta(double a, double b, double x)
        {
            if (x <= 0) return 0;
            if (x >= 1) return 1;
            double lbeta = GammaLn(a + b) - GammaLn(a) - GammaLn(b) + a * Math.Log(x) + b * Math.Log(1 - x);
            double bt = Math.Exp(lbeta);
            if (x < (a + 1) / (a + b + 2))
                return bt * BetaCf(a, b, x) / a;
            return 1 - bt * BetaCf(b, a, 1 - x) / b;
        }

        private static double BetaCf(double a, double b, double x)
        {
            const int maxit = 300;
            const double eps = 1e-14;
            const double fpmin = 1e-300;
            double qab = a + b, qap = a + 1, qam = a - 1;
            double c = 1, d = 1 - qab * x / qap;
            if (Math.Abs(d) < fpmin) d = fpmin;
            d = 1 / d;
            double h = d;
            for (int m = 1; m <= maxit; m++)
            {
                int m2 = 2 * m;
                double aa = m * (b - m) * x / ((qam + m2) * (a + m2));
                d = 1 + aa * d;
                if (Math.Abs(d) < fpmin) d = fpmin;
                c = 1 + aa / c;
                if (Math.Abs(c) < fpmin) c = fpmin;
                d = 1 / d;
                h *= d * c;
                aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
                d = 1 + aa * d;
                if (Math.Abs(d) < fpmin) d = fpmin;
                c = 1 + aa / c;
                if (Math.Abs(c) < fpmin) c = fpmin;
                d = 1 / d;
                double de = d * c;
                h *= de;
                if (Math.Abs(de - 1) < eps) break;
            }
            return h;
        }

        private static double GammaLn(double x)
        {
            double[] cof = { 76.18009172947146, -86.50532032941677, 24.01409824083091,
                            -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5 };
            double y = x, tmp = x + 5.5;
            tmp -= (x + 0.5) * Math.Log(tmp);
            double ser = 1.000000000190015;
            for (int j = 0; j < 6; j++) { y += 1; ser += cof[j] / y; }
            return -tmp + Math.Log(2.5066282746310005 * ser / x);
        }
    }
}
