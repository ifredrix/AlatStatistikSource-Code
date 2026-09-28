using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    // GLMM intersep-acak (Binomial taut-logit, Poisson taut-log) via PQL.
    //
    // Cara kerja (Breslow & Clayton 1993): di sekitar taksiran sekarang,
    public static class Glmm
    {
        public enum Keluarga { Binomial, Poisson }

        private const double Jepit = 1e-9;
        private const int PqlMaks = 100;
        private const double PqlTol = 1e-8;

        public sealed class Hasil
        {
            public List<string> Nama = new();
            public double[] B = Array.Empty<double>();
            public double[] Se = Array.Empty<double>();
            public double[] Z = Array.Empty<double>();
            public double[] P = Array.Empty<double>();
            public int N, G;
            public double Sigma2u, Sigma2, Icc;
            public double Deviance, PearsonChi2;
            public double LrChi2, LrP;
            public List<Campur.Blup> Blups = new();
            public bool Konvergen;
            public int Iterasi;
        }

        private sealed class Ringkas
        {
            public string Nama = "";
            public double Sw;
            public double[] Sx = Array.Empty<double>();
            public double[,] Sxx = new double[0, 0];
            public double[] Sxz = Array.Empty<double>();
            public double Sz, Szz;
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

        private static double Logit(double p)
        {
            p = Math.Min(Math.Max(p, Jepit), 1 - Jepit);
            return Math.Log(p / (1 - p));
        }

        // Satu langkah PQL penuh. Mengembalikan null bila matriks singular.
        public static Hasil? Fit(double[] y, double[] trials, List<double[]> pred,
                                 List<string> namaPred, string[] grup, Keluarga fam)
        {
            int n = y.Length;
            if (trials.Length != n || grup.Length != n) return null;
            if (pred.Any(v => v.Length != n)) return null;
            int p = pred.Count + 1;
            if (n < p + 3 || p < 2) return null;
            if (fam == Keluarga.Poisson && y.Any(v => v < 0 || double.IsNaN(v))) return null;
            if (fam == Keluarga.Binomial
                && y.Zip(trials, (k, m) => k < 0 || k > m || m < 1 || double.IsNaN(k)).Any(b => b))
                return null;

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

            // Nilai tengah untuk start: proporsi (Binomial) atau rerata (Poisson).
            double tengah = fam == Keluarga.Binomial
                ? y.Zip(trials, (k, m) => k / m).Average()
                : Math.Max(y.Average(), Jepit);
            var beta = new double[p];
            beta[0] = fam == Keluarga.Binomial ? Logit(tengah) : Math.Log(Math.Max(tengah, Jepit));
            var blup = new double[G];
            double lam = 1.0;

            var z = new double[n];
            var w = new double[n];
            var mu = new double[n];
            int iterPql = 0;
            bool konvergen = false;

            // Respon-kerja dan bobot dari (beta, blup) sekarang.
            void Kerja()
            {
                for (int i = 0; i < n; i++)
                {
                    double eta = beta[0] + blup[gid[i]];
                    for (int j = 0; j < pred.Count; j++) eta += beta[j + 1] * pred[j][i];
                    if (fam == Keluarga.Binomial)
                    {
                        double m = eta >= 0
                            ? 1.0 / (1.0 + Math.Exp(-eta))
                            : Math.Exp(eta) / (1.0 + Math.Exp(eta));
                        m = Math.Min(Math.Max(m, Jepit), 1 - Jepit);
                        mu[i] = m;
                        double dk = y[i] / trials[i];
                        double d = m * (1 - m);
                        z[i] = eta + (dk - m) / d;
                        w[i] = Math.Max(trials[i] * d, 1e-12);
                    }
                    else
                    {
                        double m = Math.Max(Math.Exp(Math.Min(eta, 700)), Jepit);
                        mu[i] = m;
                        z[i] = eta + (y[i] - m) / m;
                        w[i] = Math.Max(m, 1e-12);
                    }
                }
            }

            for (iterPql = 1; iterPql <= PqlMaks; iterPql++)
            {
                Kerja();

                var rg = new Ringkas[G];
                for (int g = 0; g < G; g++)
                    rg[g] = new Ringkas
                    {
                        Nama = urutan[g],
                        Sx = new double[p], Sxx = new double[p, p], Sxz = new double[p],
                    };
                double szz = 0;
                for (int i = 0; i < n; i++)
                {
                    var r = rg[gid[i]];
                    double wi = w[i], zi = z[i];
                    r.Sw += wi;
                    r.Sz += wi * zi;
                    r.Szz += wi * zi * zi;
                    szz += wi * zi * zi;
                    for (int a = 0; a < p; a++)
                    {
                        r.Sx[a] += wi * xcol[a][i];
                        r.Sxz[a] += wi * xcol[a][i] * zi;
                        for (int b = 0; b < p; b++) r.Sxx[a, b] += wi * xcol[a][i] * xcol[b][i];
                    }
                }

                double[,] MatriksA(double l, out double[] vb, out double qz)
                {
                    var A = new double[p, p];
                    vb = new double[p];
                    qz = szz;
                    var sxx = new double[p, p];
                    var sxz = new double[p];
                    for (int a = 0; a < p; a++)
                    {
                        for (int b = 0; b < p; b++)
                            foreach (var r in rg) sxx[a, b] += r.Sxx[a, b];
                        foreach (var r in rg) sxz[a] += r.Sxz[a];
                    }
                    for (int a = 0; a < p; a++)
                    {
                        for (int b = 0; b < p; b++) A[a, b] = sxx[a, b];
                        vb[a] = sxz[a];
                    }
                    foreach (var r in rg)
                    {
                        double c = l / (1 + l * r.Sw);
                        for (int a = 0; a < p; a++)
                        {
                            for (int b = 0; b < p; b++) A[a, b] -= c * r.Sx[a] * r.Sx[b];
                            vb[a] -= c * r.Sx[a] * r.Sz;
                        }
                        qz -= c * r.Sz * r.Sz;
                    }
                    return A;
                }

                double Kriteria(double t)
                {
                    double l = Math.Exp(t);
                    var A = MatriksA(l, out var b, out double q);
                    var inv = Regression.Inverse((double[,])A.Clone());
                    if (inv is null) return double.PositiveInfinity;
                    var be = new double[p];
                    for (int a = 0; a < p; a++)
                    {
                        double s = 0;
                        for (int bb = 0; bb < p; bb++) s += inv[a, bb] * b[bb];
                        be[a] = s;
                        q -= be[a] * b[a];
                    }
                    if (q <= 0) return double.PositiveInfinity;
                    double det = Aljabar.Determinan(A);
                    if (double.IsNaN(det) || det <= 0) return double.PositiveInfinity;
                    double logDet = 0;
                    for (int g = 0; g < G; g++) logDet += Math.Log(1 + l * rg[g].Sw);
                    return (n - p) * (Math.Log(q / (n - p)) + 1.8378770654093455)
                           + logDet + Math.Log(det) + (n - p);
                }

                double tHat = CariEmas(Kriteria, -15, 10, 1e-10, 300, out _);
                double lamBaru = Math.Exp(tHat);
                var Af = MatriksA(lamBaru, out var bf, out double qf);
                var invf = Regression.Inverse((double[,])Af.Clone());
                if (invf is null) return null;
                var betaBaru = new double[p];
                double q2 = qf;
                for (int a = 0; a < p; a++)
                {
                    double s = 0;
                    for (int b = 0; b < p; b++) s += invf[a, b] * bf[b];
                    betaBaru[a] = s;
                    q2 -= betaBaru[a] * bf[a];
                }
                if (q2 <= 0) return null;

                double geser = 0;
                for (int a = 0; a < p; a++)
                    geser = Math.Max(geser, Math.Abs(betaBaru[a] - beta[a]));
                double geserL = Math.Abs(Math.Log(lamBaru) - Math.Log(lam));
                beta = betaBaru;
                lam = lamBaru;
                for (int g = 0; g < G; g++)
                {
                    double resid = rg[g].Sz;
                    for (int a = 0; a < p; a++) resid -= rg[g].Sx[a] * beta[a];
                    blup[g] = lam * resid / (1 + lam * rg[g].Sw);
                }
                if (geser < PqlTol && geserL < 1e-6) { konvergen = true; break; }
            }

            Kerja();
            double s2 = 0, pearson = 0, dev = 0;
            if (fam == Keluarga.Binomial)
            {
                for (int i = 0; i < n; i++)
                {
                    double m = mu[i], dk = y[i] / trials[i], t = trials[i];
                    double v = Math.Max(m * (1 - m), Jepit) / t;
                    pearson += (dk - m) * (dk - m) / v;
                    double p0 = Math.Max(dk, Jepit), p1 = Math.Max(1 - dk, Jepit);
                    double q0 = Math.Max(m, Jepit), q1 = Math.Max(1 - m, Jepit);
                    dev += 2 * t * (dk * Math.Log(p0 / q0) + (1 - dk) * Math.Log(p1 / q1));
                }
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    double m = mu[i];
                    pearson += (y[i] - m) * (y[i] - m) / m;
                    dev += 2 * ((y[i] > 0 ? y[i] * Math.Log(y[i] / m) : 0.0) - (y[i] - m));
                }
            }

            // Galat baku GLS pada skala kerja: s2·(X′V⁻¹X)⁻¹.
            var rg2 = new Ringkas[G];
            for (int g = 0; g < G; g++)
                rg2[g] = new Ringkas
                {
                    Nama = urutan[g],
                    Sx = new double[p], Sxx = new double[p, p], Sxz = new double[p],
                };
            for (int i = 0; i < n; i++)
            {
                var r = rg2[gid[i]];
                r.Sw += w[i];
                r.Sz += w[i] * z[i];
                for (int a = 0; a < p; a++)
                {
                    r.Sx[a] += w[i] * xcol[a][i];
                    for (int b = 0; b < p; b++) r.Sxx[a, b] += w[i] * xcol[a][i] * xcol[b][i];
                }
            }
            var Ase = new double[p, p];
            for (int a = 0; a < p; a++)
                for (int b = 0; b < p; b++)
                    foreach (var r in rg2) Ase[a, b] += r.Sxx[a, b];
            foreach (var r in rg2)
            {
                double c = lam / (1 + lam * r.Sw);
                for (int a = 0; a < p; a++)
                    for (int b = 0; b < p; b++) Ase[a, b] -= c * r.Sx[a] * r.Sx[b];
            }
            var invSe = Regression.Inverse((double[,])Ase.Clone());
            if (invSe is null) return null;
            // Sisa kuadrat kerja setelah GLS — penyebutnya n−p seperti Campur.
            // Hitung langsung: q = Σw(z − Xβ − u)² + Σu²/λ.
            double q = 0;
            for (int i = 0; i < n; i++)
            {
                double e = z[i] - beta[0] - blup[gid[i]];
                for (int j = 0; j < pred.Count; j++) e -= beta[j + 1] * pred[j][i];
                q += w[i] * e * e;
            }
            for (int g = 0; g < G; g++) q += blup[g] * blup[g] / lam;
            s2 = q / (n - p);

            var se = new double[p];
            var zz = new double[p];
            var pv = new double[p];
            for (int a = 0; a < p; a++)
            {
                se[a] = Math.Sqrt(Math.Max(0, s2 * invSe[a, a]));
                zz[a] = se[a] > 0 ? beta[a] / se[a] : double.NaN;
                pv[a] = 2 * Distributions.NormalCdf(-Math.Abs(zz[a]));
            }

            // Uji lawan GLM tetap (tanpa efek acak): penurunan deviansi.
            double devTetap = double.NaN;
            if (fam == Keluarga.Binomial)
            {
                var h0 = GlmMulti.FitBinom(y, trials, pred, namaPred);
                if (h0 is not null) devTetap = h0.Deviance;
            }
            else
            {
                var h0 = Glm.FitPoisson(y, pred, namaPred);
                if (h0 is not null) devTetap = h0.Deviance;
            }
            double lr = double.IsNaN(devTetap) ? double.NaN : Math.Max(0, devTetap - dev);

            var nama = new List<string> { "(Konstanta)" };
            nama.AddRange(namaPred);
            var blups = new List<Campur.Blup>();
            var hitung = new int[G];
            for (int i = 0; i < n; i++) hitung[gid[i]]++;
            for (int g = 0; g < G; g++)
                blups.Add(new Campur.Blup { Grup = urutan[g], N = hitung[g], Nilai = blup[g] });

            return new Hasil
            {
                Nama = nama, B = beta, Se = se, Z = zz, P = pv,
                N = n, G = G,
                Sigma2u = lam * s2, Sigma2 = s2,
                Icc = lam / (1 + lam),
                Deviance = dev, PearsonChi2 = pearson,
                LrChi2 = lr, LrP = double.IsNaN(lr) ? double.NaN : Distributions.ChiSquareUpper(lr, 1),
                Blups = blups, Konvergen = konvergen, Iterasi = iterPql,
            };
        }

        public static List<ResultBlock> GlmmBlocks(Dataset ds, string respon,
            string percobaan, List<string> bebas, string grup, string keluarga,
            double alpha = 0.05)
        {
            bool binom = keluarga.StartsWith("Binom", StringComparison.OrdinalIgnoreCase);
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading(binom ? "GLMM Binomial (logit + intersep acak)"
                                     : "GLMM Poisson (log + intersep acak)", 1)
            };

            var perlu = new List<string> { respon, grup };
            if (binom)
            {
                if (string.IsNullOrEmpty(percobaan))
                {
                    blocks.Add(Blocks.Note(
                        "Binomial butuh kolom 'banyak percobaan' (m). Isi dulu, "
                        + "atau pakai keluarga Poisson.", NoteKind.Error));
                    return blocks;
                }
                perlu.Add(percobaan);
            }
            perlu.AddRange(bebas);
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < bebas.Count + 6)
            {
                blocks.Add(Blocks.Note(
                    "Baris lengkap kurang. Model ini tidak bisa dijalankan.",
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

            var y = AmbilN(respon);
            var t = binom ? AmbilN(percobaan) : Enumerable.Repeat(1.0, baris.Count).ToArray();
            var pred = bebas.Select(AmbilN).ToList();
            if (y.Any(double.IsNaN) || pred.Any(v => v.Any(double.IsNaN)))
            {
                blocks.Add(Blocks.Note(
                    "Respon atau prediktor punya nilai hilang di baris lengkap.",
                    NoteKind.Error));
                return blocks;
            }

            var h = Fit(y, t, pred, bebas, AmbilT(grup),
                        binom ? Keluarga.Binomial : Keluarga.Poisson);
            if (h is null)
            {
                blocks.Add(Blocks.Note(
                    "Model tidak bisa disesuaikan. Kemungkinan: kelompok kurang "
                    + "dari dua, atau matriksnya singular.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.GlmmModel, DaftarRumus.GlmmPql));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Keluarga", binom ? "Binomial, taut logit" : "Poisson, taut log"),
                ("Respon", respon),
                ("Prediktor tetap", bebas.Count > 0 ? string.Join(", ", bebas) : "(hanya konstanta)"),
                ("Kelompok", $"{grup} ({Fmt.Int(h.G)} kelompok)"),
                ("Banyak baris lengkap", $"n = {Fmt.Int(h.N)}"),
                ("Iterasi PQL", $"{Fmt.Int(h.Iterasi)} ({(h.Konvergen ? "konvergen" : "TIDAK konvergen")})"),
                ("Nisbah ragam λ = σ²u/σ²", Fmt.Num(h.Sigma2u / h.Sigma2, 6))));

            blocks.Add(Blocks.Table(
                "Efek tetap",
                new[] { "Suku", "B", "Galat baku", "Wald z", "p" },
                h.Nama.Select((nm, j) => new[]
                {
                    nm, Fmt.Num(h.B[j], 6), Fmt.Num(h.Se[j], 6),
                    Fmt.Num(h.Z[j], 4), Fmt.P(h.P[j]),
                }).ToArray(),
                "Galat baku GLS pada skala kerja. Uji memakai sebaran normal."));

            blocks.Add(Blocks.Table(
                "Komponen ragam",
                new[] { "Besaran", "Nilai" },
                new[]
                {
                    new[] { "Ragam antar-kelompok σ²u", Fmt.Num(h.Sigma2u, 6) },
                    new[] { "Skala kerja σ² (≈1 bila model pas)", Fmt.Num(h.Sigma2, 6) },
                    new[] { "ICC skala taut = σ²u/(σ²u+σ²)", Fmt.Num(h.Icc, 6) },
                    new[] { "Devians", Fmt.Num(h.Deviance, 4) },
                    new[] { "Pearson χ²", Fmt.Num(h.PearsonChi2, 4) },
                    new[] { "Uji lawan GLM tetap: penurunan devians", Fmt.Num(h.LrChi2, 4) },
                    new[] { "p (χ², df 1)", Fmt.P(h.LrP) },
                },
                "Skala kerja jauh dari 1 menandakan sebaran-berlebih. "
                + "Uji lawan GLM memakai χ² ber-df 1 — hampiran konservatif, "
                + "karena σ²u = 0 berada di batas ruang parameter."));

            blocks.Add(Blocks.Table(
                "Dugaan acak tiap kelompok (BLUP)",
                new[] { "Kelompok", "n", "û" },
                h.Blups.Select(b => new[]
                {
                    b.Grup, Fmt.Int(b.N), Fmt.Num(b.Nilai, 4),
                }).ToArray(),
                "BLUP pada skala taut (logit/log) — menyusut ke nol untuk "
                + "kelompok kecil."));

            return blocks;
        }
    }
}
