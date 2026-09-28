using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class Campur
    {
        public sealed class Blup
        {
            public string Grup = "";
            public int N;
            public double Nilai;
        }

        public sealed class Hasil
        {
            public List<string> Nama = new();
            public double[] B = Array.Empty<double>();
            public double[] Se = Array.Empty<double>();
            public double[] T = Array.Empty<double>();
            public double[] P = Array.Empty<double>();
            public int N, G;
            public double Lambda, Sigma2u, Sigma2, Icc, LogLik;
            public double LogLikOls, LrChi2, LrP;
            public List<Blup> Blups = new();
            public bool Konvergen;
            public int Iterasi;
        }

        private sealed class RingkasGrup
        {
            public string Nama = "";
            public int N;
            public double[] Sx = Array.Empty<double>();
            public double[,] Sxx = new double[0, 0];
            public double[] Sxy = Array.Empty<double>();
            public double Sy;
            public double Syy;
        }

        private static double CariEmas(Func<double, double> f, double lo, double hi,
                                       double tol, int maks, out int iterasi)
        {
            double gr = (Math.Sqrt(5) - 1) / 2;
            double c = hi - gr * (hi - lo), d = lo + gr * (hi - lo);
            double fc = f(c), fd = f(d);
            iterasi = 0;
            while (hi - lo > tol && iterasi < maks)
            {
                iterasi++;
                if (fc < fd) { hi = d; d = c; fd = fc; c = hi - gr * (hi - lo); fc = f(c); }
                else { lo = c; c = d; fc = fd; d = lo + gr * (hi - lo); fd = f(d); }
            }
            return 0.5 * (lo + hi);
        }

        public static Hasil? Fit(double[] y, List<double[]> pred, List<string> namaPred,
                                 string[] grup)
        {
            int n = y.Length;
            if (pred.Any(v => v.Length != n) || grup.Length != n) return null;
            int p = pred.Count + 1;
            if (n < p + 3 || p < 2) return null;

            var urutan = new List<string>();
            var indeks = new Dictionary<string, int>(StringComparer.Ordinal);
            var gid = new int[n];
            for (int i = 0; i < n; i++)
            {
                if (!indeks.TryGetValue(grup[i], out int g))
                {
                    g = urutan.Count;
                    urutan.Add(grup[i]);
                    indeks[grup[i]] = g;
                }
                gid[i] = g;
            }
            int G = urutan.Count;
            if (G < 2 || n - G < p) return null;

            var xcol = new double[p][];
            xcol[0] = Enumerable.Repeat(1.0, n).ToArray();
            for (int j = 0; j < pred.Count; j++) xcol[j + 1] = pred[j];

            var rg = new RingkasGrup[G];
            for (int g = 0; g < G; g++)
                rg[g] = new RingkasGrup
                {
                    Nama = urutan[g],
                    Sx = new double[p], Sxx = new double[p, p], Sxy = new double[p],
                };
            double syyTotal = 0;
            for (int i = 0; i < n; i++)
            {
                var r = rg[gid[i]];
                r.N++;
                r.Sy += y[i];
                r.Syy += y[i] * y[i];
                syyTotal += y[i] * y[i];
                for (int a = 0; a < p; a++)
                {
                    r.Sx[a] += xcol[a][i];
                    r.Sxy[a] += xcol[a][i] * y[i];
                    for (int b = 0; b < p; b++) r.Sxx[a, b] += xcol[a][i] * xcol[b][i];
                }
            }
            if (rg.Any(r => r.N < 1)) return null;

            double[,] MatriksA(double lam, out double[] vecB, out double qyy)
            {
                var A = new double[p, p];
                vecB = new double[p];
                qyy = syyTotal;
                var sxx = new double[p, p];
                var sxy = new double[p];
                for (int a = 0; a < p; a++)
                {
                    for (int b = 0; b < p; b++)
                        foreach (var r in rg) sxx[a, b] += r.Sxx[a, b];
                    foreach (var r in rg) sxy[a] += r.Sxy[a];
                }
                for (int a = 0; a < p; a++)
                {
                    for (int b = 0; b < p; b++) A[a, b] = sxx[a, b];
                    vecB[a] = sxy[a];
                }
                foreach (var r in rg)
                {
                    double c = lam / (1 + lam * r.N);
                    for (int a = 0; a < p; a++)
                    {
                        for (int b = 0; b < p; b++) A[a, b] -= c * r.Sx[a] * r.Sx[b];
                        vecB[a] -= c * r.Sx[a] * r.Sy;
                    }
                    qyy -= c * r.Sy * r.Sy;
                }
                return A;
            }

            double Kriteria(double t)
            {
                double lam = Math.Exp(t);
                var A = MatriksA(lam, out var b, out double qyy);
                var inv = Regression.Inverse((double[,])A.Clone());
                if (inv is null) return double.PositiveInfinity;
                double q = qyy;
                var beta = new double[p];
                for (int a = 0; a < p; a++)
                {
                    double s = 0;
                    for (int bb = 0; bb < p; bb++) s += inv[a, bb] * b[bb];
                    beta[a] = s;
                    q -= beta[a] * b[a];
                }
                if (q <= 0) return double.PositiveInfinity;
                double s2 = q / (n - p);
                double det = Aljabar.Determinan(A);
                if (double.IsNaN(det) || det <= 0) return double.PositiveInfinity;
                double logDet = 0;
                for (int g = 0; g < G; g++) logDet += Math.Log(1 + lam * rg[g].N);
                return (n - p) * (Math.Log(s2) + 1.8378770654093455) + logDet
                       + Math.Log(det) + (n - p);
            }

            double tHat = CariEmas(Kriteria, -15, 10, 1e-10, 300, out int iter);
            double lamHat = Math.Exp(tHat);
            var Af = MatriksA(lamHat, out var bf, out double qyyf);
            var invf = Regression.Inverse((double[,])Af.Clone());
            if (invf is null) return null;
            var betaHat = new double[p];
            double qf = qyyf;
            for (int a = 0; a < p; a++)
            {
                double s = 0;
                for (int b = 0; b < p; b++) s += invf[a, b] * bf[b];
                betaHat[a] = s;
                qf -= betaHat[a] * bf[a];
            }
            if (qf <= 0) return null;
            double s2Hat = qf / (n - p);

            var se = new double[p];
            var tt = new double[p];
            var pv = new double[p];
            var invSendi = InformasiGabungan(y, xcol, gid, rg, betaHat, lamHat, s2Hat);
            if (invSendi is null) return null;
            for (int a = 0; a < p; a++)
            {
                se[a] = Math.Sqrt(Math.Max(0, invSendi[a, a]));
                tt[a] = se[a] > 0 ? betaHat[a] / se[a] : double.NaN;
                pv[a] = 2 * Distributions.NormalCdf(-Math.Abs(tt[a]));
            }

            var blups = new List<Blup>();
            foreach (var r in rg)
            {
                double resid = r.Sy;
                for (int a = 0; a < p; a++) resid -= r.Sx[a] * betaHat[a];
                blups.Add(new Blup
                {
                    Grup = r.Nama, N = r.N,
                    Nilai = lamHat * resid / (1 + lamHat * r.N),
                });
            }

            var Aols = MatriksA(0.0, out var bols, out double qyyOls);
            var invOls = Regression.Inverse((double[,])Aols.Clone());
            double qOls = qyyOls;
            if (invOls is not null)
            {
                var bOls = new double[p];
                for (int a = 0; a < p; a++)
                {
                    double s = 0;
                    for (int b = 0; b < p; b++) s += invOls[a, b] * bols[b];
                    bOls[a] = s;
                    qOls -= bOls[a] * bols[a];
                }
            }
            double s2Ols = qOls / (n - p);
            double detOls = Aljabar.Determinan(Aols);
            double m2Ols = (n - p) * (Math.Log(s2Ols) + 1.8378770654093455)
                           + Math.Log(detOls) + (n - p);
            double m2 = Kriteria(tHat);
            double lr = Math.Max(0, m2Ols - m2);

            var nama = new List<string> { "(Konstanta)" };
            nama.AddRange(namaPred);
            return new Hasil
            {
                Nama = nama, B = betaHat, Se = se, T = tt, P = pv,
                N = n, G = G,
                Lambda = lamHat, Sigma2u = lamHat * s2Hat, Sigma2 = s2Hat,
                Icc = lamHat / (1 + lamHat),
                LogLik = -m2 / 2, LogLikOls = -m2Ols / 2,
                LrChi2 = lr, LrP = Distributions.ChiSquareUpper(lr, 1),
                Blups = blups, Konvergen = true, Iterasi = iter,
            };
        }

        private static double[,] Kali2(double[,] a, double[,] b)
        {
            int m = a.GetLength(0), k = a.GetLength(1), n = b.GetLength(1);
            var h = new double[m, n];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++)
                {
                    double s = 0;
                    for (int t = 0; t < k; t++) s += a[i, t] * b[t, j];
                    h[i, j] = s;
                }
            return h;
        }

        private static double[,]? InformasiGabungan(double[] y, double[][] xcol, int[] gid,
            RingkasGrup[] rg, double[] beta, double lam, double s2)
        {
            int n = y.Length, p = beta.Length, G = rg.Length;
            int m = p + 2;

            var resid = new double[n];
            for (int i = 0; i < n; i++)
            {
                double e = y[i];
                for (int a = 0; a < p; a++) e -= xcol[a][i] * beta[a];
                resid[i] = e;
            }
            var jmlR = new double[G];
            for (int i = 0; i < n; i++) jmlR[gid[i]] += resid[i];

            var A = new double[p, p];
            var Ap = new double[p, p];
            var App = new double[p, p];
            var xwr = new double[p];
            double dQdl = 0;
            for (int g = 0; g < G; g++)
            {
                var r = rg[g];
                double d1 = 1 + lam * r.N;
                double c1 = 1 / (d1 * d1);
                double c2 = -2 * r.N / (d1 * d1 * d1);
                for (int a = 0; a < p; a++)
                {
                    for (int b = 0; b < p; b++)
                    {
                        A[a, b] += r.Sxx[a, b] - lam / d1 * r.Sx[a] * r.Sx[b];
                        Ap[a, b] -= c1 * r.Sx[a] * r.Sx[b];
                        App[a, b] -= c2 * r.Sx[a] * r.Sx[b];
                    }
                    xwr[a] += r.Sxy[a] - lam / d1 * r.Sx[a] * r.Sy;
                }
                dQdl += c1 * jmlR[g] * jmlR[g];
            }
            for (int a = 0; a < p; a++) xwr[a] -= DotBaris(A, a, beta);

            var invA = Regression.Inverse((double[,])A.Clone());
            if (invA is null) return null;
            var invAp = Kali2(invA, Ap);
            var invApp = Kali2(invA, App);
            double t1 = 0, t2 = 0, t3 = 0;
            for (int a = 0; a < p; a++)
                for (int b = 0; b < p; b++)
                {
                    t1 += invA[a, b] * Ap[b, a];
                    t2 += invA[a, b] * App[b, a];
                    t3 += invAp[a, b] * invAp[b, a];
                }

            double d2Q = 0;
            for (int g = 0; g < G; g++)
            {
                var r = rg[g];
                double d1 = 1 + lam * r.N;
                d2Q += -2 * r.N / (d1 * d1 * d1) * jmlR[g] * jmlR[g];
            }

            var H = new double[m, m];
            for (int a = 0; a < p; a++)
                for (int b = 0; b < p; b++) H[a, b] = 2 * A[a, b] / s2;
            for (int g = 0; g < G; g++)
            {
                var r = rg[g];
                double d1 = 1 + lam * r.N;
                double c1 = 1 / (d1 * d1);
                for (int a = 0; a < p; a++)
                    H[a, p] += 2 / s2 * c1 * r.Sx[a] * jmlR[g];
            }
            for (int a = 0; a < p; a++)
            {
                H[a, p + 1] = 2 * xwr[a] / (s2 * s2);
                H[p, a] = H[a, p];
                H[p + 1, a] = H[a, p + 1];
            }
            double hll = 0;
            for (int g = 0; g < G; g++)
                hll += -rg[g].N * rg[g].N / Math.Pow(1 + lam * rg[g].N, 2);
            H[p, p] = hll + (t2 - t3) - d2Q / s2;
            H[p, p + 1] = dQdl / (s2 * s2);
            H[p + 1, p] = H[p, p + 1];
            H[p + 1, p + 1] = (n - 2 * p) / (s2 * s2);
            for (int a = 0; a < m; a++)
                for (int b = 0; b < m; b++) H[a, b] *= 0.5;
            return Regression.Inverse(H);
        }

        private static double DotBaris(double[,] a, int baris, double[] v)
        {
            double s = 0;
            for (int j = 0; j < v.Length; j++) s += a[baris, j] * v[j];
            return s;
        }

        public static List<ResultBlock> CampurBlocks(Dataset ds, string dependen,
            List<string> bebas, string grup, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Model campuran (intersep acak per kelompok)", 1)
            };

            var perlu = new List<string> { dependen, grup };
            perlu.AddRange(bebas);
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < bebas.Count + 6)
            {
                blocks.Add(Blocks.Note(
                    "Baris lengkap kurang. Model campuran tidak bisa dijalankan.",
                    NoteKind.Error));
                return blocks;
            }

            double[] AmbilN(string v)
            {
                var semua = ds.Numeric(v);
                var a = new double[baris.Count];
                for (int i = 0; i < baris.Count; i++) a[i] = semua[baris[i]] ?? double.NaN;
                return a;
            }

            string[] AmbilT(string v)
            {
                var semua = ds.Text(v);
                var a = new string[baris.Count];
                for (int i = 0; i < baris.Count; i++) a[i] = semua[baris[i]] ?? "(kosong)";
                return a;
            }

            var y = AmbilN(dependen);
            if (y.Any(double.IsNaN))
            {
                blocks.Add(Blocks.Note(
                    $"Respon '{dependen}' punya nilai hilang di baris lengkap.",
                    NoteKind.Error));
                return blocks;
            }
            var pred = bebas.Select(AmbilN).ToList();
            if (pred.Any(v => v.Any(double.IsNaN)))
            {
                blocks.Add(Blocks.Note(
                    "Prediktor punya nilai hilang di baris lengkap.", NoteKind.Error));
                return blocks;
            }

            var h = Fit(y, pred, bebas, AmbilT(grup));
            if (h is null)
            {
                blocks.Add(Blocks.Note(
                    "Model tidak bisa disesuaikan. Kemungkinan: kelompok kurang "
                    + "dari dua, atau matriksnya singular.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.CampurModel, DaftarRumus.CampurReml));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Respon", dependen),
                ("Prediktor tetap", bebas.Count > 0 ? string.Join(", ", bebas) : "(hanya konstanta)"),
                ("Kelompok", $"{grup} ({Fmt.Int(h.G)} kelompok)"),
                ("Banyak baris lengkap", $"n = {Fmt.Int(h.N)}"),
                ("Nisbah ragam λ = σ²u/σ²", Fmt.Num(h.Lambda, 6)),
                ("REML −2ℓ", Fmt.Num(-2 * h.LogLik, 4))));

            blocks.Add(Blocks.Table(
                "Efek tetap",
                new[] { "Suku", "B", "Galat baku", "t", "p" },
                h.Nama.Select((nm, j) => new[]
                {
                    nm, Fmt.Num(h.B[j], 6), Fmt.Num(h.Se[j], 6),
                    Fmt.Num(h.T[j], 4), Fmt.P(h.P[j]),
                }).ToArray(),
                "Galat baku GLS dari σ²·(X′V⁻¹X)⁻¹. Uji memakai sebaran normal."));

            blocks.Add(Blocks.Table(
                "Komponen ragam",
                new[] { "Besaran", "Nilai" },
                new[]
                {
                    new[] { "Ragam antar-kelompok σ²u", Fmt.Num(h.Sigma2u, 6) },
                    new[] { "Ragam sisa σ²", Fmt.Num(h.Sigma2, 6) },
                    new[] { "ICC = σ²u/(σ²u+σ²)", Fmt.Num(h.Icc, 6) },
                    new[] { "ln L (REML)", Fmt.Num(h.LogLik, 4) },
                    new[] { "Uji nisbah kemiripan lawan OLS: χ²", Fmt.Num(h.LrChi2, 4) },
                    new[] { "p (χ², df 1)", Fmt.P(h.LrP) },
                },
                "ICC = proporsi ragam yang berasal dari perbedaan antar kelompok. "
                + "Uji lawan OLS memakai χ² ber-df 1 — itu hampiran konservatif, "
                + "karena σ²u = 0 berada di batas ruang parameter. AIC tidak "
                + "dilaporkan: kriteria REML tidak sebanding antar model dengan "
                + "efek tetap yang berbeda."));

            blocks.Add(Blocks.Table(
                "Dugaan acak tiap kelompok (BLUP)",
                new[] { "Kelompok", "n", "û" },
                h.Blups.Select(b => new[]
                {
                    b.Grup, Fmt.Int(b.N), Fmt.Num(b.Nilai, 4),
                }).ToArray(),
                "BLUP menyusutkan rerata residual kelompok ke nol — makin kecil "
                + "kelompoknya, makin kuat susutannya. Ini bukan rerata mentah."));

            return blocks;
        }
    }
}
