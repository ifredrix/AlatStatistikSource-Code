using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class GlmMulti
    {
        public enum Keluarga { Gamma, Binomial }

        private const int IterasiMaks = 200;
        private const double Toleransi = 1e-12;
        private const double Jepit = 1e-12;

        public sealed class Tetes
        {
            public string Faktor = "";
            public double DevNaik;
            public int Df;
            public double P;
        }

        public sealed class Hasil
        {
            public List<string> Nama = new();
            public double[] B = Array.Empty<double>();
            public double[] Se = Array.Empty<double>();
            public double[] Z = Array.Empty<double>();
            public double[] P = Array.Empty<double>();
            public int N, K, DfResid, Iterasi;
            public double Deviance, PearsonChi2, Skala;
            public double LogLik, LogLikNol, PseudoR2, Aic;
            public bool Konvergen;
            public List<Tetes> TetesFaktor = new();
        }

        private static double[,] Rancang(double[][] kolom)
        {
            int n = kolom[0].Length, k = kolom.Length + 1;
            var x = new double[n, k];
            for (int i = 0; i < n; i++)
            {
                x[i, 0] = 1.0;
                for (int j = 0; j < kolom.Length; j++) x[i, j + 1] = kolom[j][i];
            }
            return x;
        }

        private static double[] KuadratTerkecil(double[,] x, double[] w, double[] z)
        {
            int n = x.GetLength(0), k = x.GetLength(1);
            var xtwx = new double[k, k];
            var xtwz = new double[k];
            for (int i = 0; i < n; i++)
                for (int a = 0; a < k; a++)
                {
                    xtwz[a] += x[i, a] * w[i] * z[i];
                    for (int b = 0; b < k; b++) xtwx[a, b] += x[i, a] * w[i] * x[i, b];
                }
            var inv = Regression.Inverse((double[,])xtwx.Clone());
            if (inv is null) return Array.Empty<double>();
            var beta = new double[k];
            for (int a = 0; a < k; a++)
            {
                double s = 0;
                for (int b = 0; b < k; b++) s += inv[a, b] * xtwz[b];
                beta[a] = s;
            }
            return beta;
        }

        private static Hasil? Pasang(double[] y, double[] papar, double[,] x, List<string> nama,
                                     Func<double, double> muDariEta,
                                     Func<double, double, double> dDariMu,
                                     Func<double, double, double> vDariMu,
                                     Func<double[], double[], double> deviance,
                                     Func<double[], double[], double, double> logLik,
                                     double skalaTetap)
        {
            int n = y.Length, k = x.GetLength(1);
            if (n < k + 2) return null;
            var beta = new double[k];
            var mu = new double[n];
            var w = new double[n];
            var z = new double[n];
            int iterasi = 0;
            bool konvergen = false;
            for (iterasi = 1; iterasi <= IterasiMaks; iterasi++)
            {
                for (int i = 0; i < n; i++)
                {
                    double eta = 0;
                    for (int a = 0; a < k; a++) eta += x[i, a] * beta[a];
                    mu[i] = muDariEta(eta);
                    double d = dDariMu(mu[i], papar[i]);
                    w[i] = d * d / vDariMu(mu[i], papar[i]);
                    z[i] = eta + (y[i] - mu[i]) / d;
                }
                var baru = KuadratTerkecil(x, w, z);
                if (baru.Length == 0) return null;
                double beda = 0;
                for (int a = 0; a < k; a++)
                {
                    beda = Math.Max(beda, Math.Abs(baru[a] - beta[a]));
                    beta[a] = baru[a];
                }
                if (beda < Toleransi) { konvergen = true; break; }
            }
            for (int i = 0; i < n; i++)
            {
                double eta = 0;
                for (int a = 0; a < k; a++) eta += x[i, a] * beta[a];
                mu[i] = muDariEta(eta);
            }

            double pearson = 0;
            for (int i = 0; i < n; i++)
            {
                double v = vDariMu(mu[i], papar[i]);
                pearson += (y[i] - mu[i]) * (y[i] - mu[i]) / v;
            }
            double skala = skalaTetap > 0 ? skalaTetap : pearson / (n - k);

            var xtwx = new double[k, k];
            for (int i = 0; i < n; i++)
            {
                double d = dDariMu(mu[i], papar[i]);
                double wi = d * d / vDariMu(mu[i], papar[i]);
                for (int a = 0; a < k; a++)
                    for (int b = 0; b < k; b++) xtwx[a, b] += x[i, a] * wi * x[i, b];
            }
            var inv = Regression.Inverse((double[,])xtwx.Clone());
            if (inv is null) return null;

            var se = new double[k];
            var zz = new double[k];
            var pv = new double[k];
            for (int a = 0; a < k; a++)
            {
                se[a] = Math.Sqrt(Math.Max(0, skala * inv[a, a]));
                zz[a] = se[a] > 0 ? beta[a] / se[a] : double.NaN;
                pv[a] = 2 * Distributions.NormalCdf(-Math.Abs(zz[a]));
            }

            return new Hasil
            {
                Nama = new List<string>(nama), B = beta, Se = se, Z = zz, P = pv,
                N = n, K = k - 1, DfResid = n - k, Iterasi = iterasi,
                Deviance = deviance(y, mu), PearsonChi2 = pearson, Skala = skala,
                LogLik = logLik(y, mu, skala), Konvergen = konvergen,
            };
        }

        private static double LogLikGamma(double[] y, double[] mu, double s)
        {
            double ll = 0;
            for (int i = 0; i < y.Length; i++)
            {
                double a = 1.0 / s;
                ll += (a - 1) * Math.Log(y[i]) - y[i] / (mu[i] * s)
                      - a * Math.Log(mu[i] * s) - Special.LnGamma(a);
            }
            return ll;
        }

        public static Hasil? FitGamma(double[] y, List<double[]> pred, List<string> namaPred)
        {
            if (y.Any(v => v <= 0 || double.IsNaN(v))) return null;
            if (pred.Any(v => v.Length != y.Length)) return null;
            var x = Rancang(pred.Select(v => v.ToArray()).ToArray());
            var nama = new List<string> { "(Konstanta)" };
            nama.AddRange(namaPred);
            var h = Pasang(y, Enumerable.Repeat(1.0, y.Length).ToArray(), x, nama,
                eta => Math.Max(Math.Exp(eta), Jepit),
                (mu, m) => Math.Max(mu, Jepit),
                (mu, m) => Math.Max(mu, Jepit) * Math.Max(mu, Jepit),
                (yy, mu) =>
                {
                    double d = 0;
                    for (int i = 0; i < yy.Length; i++)
                        d += 2 * ((yy[i] - mu[i]) / mu[i] - Math.Log(yy[i] / mu[i]));
                    return d;
                },
                (yy, mu, s) => LogLikGamma(yy, mu, s),
                0.0);
            if (h is null) return null;
            double rata = y.Average();
            double sNol = 0;
            for (int i = 0; i < y.Length; i++)
                sNol += (y[i] - rata) * (y[i] - rata) / (rata * rata);
            sNol /= y.Length - 1;
            h.LogLikNol = LogLikGamma(y, Enumerable.Repeat(rata, y.Length).ToArray(), sNol);
            h.PseudoR2 = h.LogLikNol != 0 ? 1 - h.LogLik / h.LogLikNol : double.NaN;
            h.Aic = -2 * h.LogLik + 2 * (h.K + 1);
            return h;
        }

        public static Hasil? FitBinom(double[] sukses, double[] percobaan,
                                      List<double[]> pred, List<string> namaPred)
        {
            int n = sukses.Length;
            if (percobaan.Length != n || pred.Any(v => v.Length != n)) return null;
            if (sukses.Zip(percobaan, (k, m) => k < 0 || k > m || m < 1).Any(b => b))
                return null;
            var prop = new double[n];
            for (int i = 0; i < n; i++) prop[i] = sukses[i] / percobaan[i];
            var x = Rancang(pred.Select(v => v.ToArray()).ToArray());
            var nama = new List<string> { "(Konstanta)" };
            nama.AddRange(namaPred);
            var mSalin = percobaan.ToArray();
            var h = Pasang(prop, mSalin, x, nama,
                eta =>
                {
                    double m = eta >= 0
                        ? 1.0 / (1.0 + Math.Exp(-eta))
                        : Math.Exp(eta) / (1.0 + Math.Exp(eta));
                    return Math.Min(Math.Max(m, Jepit), 1 - Jepit);
                },
                (mu, m) => Math.Max(mu * (1 - mu), Jepit),
                (mu, m) => Math.Max(mu * (1 - mu), Jepit) / Math.Max(m, Jepit),
                (yy, mu) =>
                {
                    double d = 0;
                    for (int i = 0; i < yy.Length; i++)
                    {
                        double m = mSalin[i];
                        double p0 = Math.Max(yy[i], Jepit), p1 = Math.Max(1 - yy[i], Jepit);
                        double q0 = Math.Max(mu[i], Jepit), q1 = Math.Max(1 - mu[i], Jepit);
                        d += 2 * m * (yy[i] * Math.Log(p0 / q0)
                                      + (1 - yy[i]) * Math.Log(p1 / q1));
                    }
                    return d;
                },
                (yy, mu, s) =>
                {
                    double ll = 0;
                    for (int i = 0; i < yy.Length; i++)
                    {
                        double m = mSalin[i], k = yy[i] * m;
                        ll += k * Math.Log(Math.Max(mu[i], Jepit))
                              + (m - k) * Math.Log(Math.Max(1 - mu[i], Jepit))
                              + Special.LnGamma(m + 1) - Special.LnGamma(k + 1)
                              - Special.LnGamma(m - k + 1);
                    }
                    return ll;
                },
                1.0);
            if (h is null) return null;
            double rata = prop.Average();
            double llNol = 0;
            for (int i = 0; i < n; i++)
            {
                double m = mSalin[i], k = prop[i] * m;
                llNol += k * Math.Log(Math.Max(rata, Jepit))
                         + (m - k) * Math.Log(Math.Max(1 - rata, Jepit))
                         + Special.LnGamma(m + 1) - Special.LnGamma(k + 1)
                         - Special.LnGamma(m - k + 1);
            }
            h.LogLikNol = llNol;
            h.PseudoR2 = llNol != 0 ? 1 - h.LogLik / llNol : double.NaN;
            h.Aic = -2 * h.LogLik + 2 * (h.K + 1);
            return h;
        }

        public static double SkorMaks(double[] y, double[] papar, double[,] x, double[] mu,
                                      Func<double, double, double> dDariMu,
                                      Func<double, double, double> vDariMu)
        {
            int n = y.Length, k = x.GetLength(1);
            double maks = 0;
            for (int a = 0; a < k; a++)
            {
                double s = 0;
                for (int i = 0; i < n; i++)
                    s += x[i, a] * dDariMu(mu[i], papar[i]) * (y[i] - mu[i])
                         / vDariMu(mu[i], papar[i]);
                maks = Math.Max(maks, Math.Abs(s));
            }
            return maks;
        }

        public static double[] PasangMu(Hasil h, double[] y, double[,] x, Keluarga fam)
        {
            int n = y.Length, k = x.GetLength(1);
            var mu = new double[n];
            for (int i = 0; i < n; i++)
            {
                double eta = 0;
                for (int a = 0; a < k; a++) eta += x[i, a] * h.B[a];
                mu[i] = fam == Keluarga.Gamma
                    ? Math.Max(Math.Exp(eta), Jepit)
                    : Math.Min(Math.Max(1.0 / (1.0 + Math.Exp(-eta)), Jepit), 1 - Jepit);
            }
            return mu;
        }

        public static double[,] RancangUji(List<double[]> pred)
            => Rancang(pred.Select(v => v.ToArray()).ToArray());

        public static List<ResultBlock> GlmMultiBlocks(Dataset ds, string respon,
            string percobaan, List<string> faktor, List<string> kovariat,
            string keluarga, double alpha = 0.05)
        {
            bool gamma = keluarga.StartsWith("Gamma", StringComparison.OrdinalIgnoreCase);
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading(gamma ? "GLM Gamma (respon positif, taut log)"
                                     : "GLM Binomial (proporsi, taut logit)", 1)
            };

            var perlu = new List<string> { respon };
            if (!gamma)
            {
                if (string.IsNullOrEmpty(percobaan))
                {
                    blocks.Add(Blocks.Note(
                        "Binomial butuh kolom 'banyak percobaan' (m). Isi dulu, "
                        + "atau pakai keluarga Gamma.", NoteKind.Error));
                    return blocks;
                }
                perlu.Add(percobaan);
            }
            perlu.AddRange(faktor);
            perlu.AddRange(kovariat);
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < faktor.Count + kovariat.Count + 4)
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

            var pred = new List<double[]>();
            var namaPred = new List<string>();
            var rentangFaktor = new List<(string Faktor, int[] Kolom)>();
            foreach (var f in faktor)
            {
                var lv = AmbilT(f).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
                if (lv.Count < 2)
                {
                    blocks.Add(Blocks.Note(
                        $"Faktor '{f}' hanya punya {lv.Count} tingkat. "
                        + "GLM multifaktor butuh minimal dua tingkat per faktor.",
                        NoteKind.Error));
                    return blocks;
                }
                var teks = AmbilT(f);
                var kols = new List<int>();
                for (int j = 1; j < lv.Count; j++)
                {
                    var d = new double[baris.Count];
                    for (int i = 0; i < baris.Count; i++)
                        d[i] = teks[i] == lv[j] ? 1.0 : 0.0;
                    kols.Add(pred.Count);
                    pred.Add(d);
                    namaPred.Add($"{f}={lv[j]}");
                }
                rentangFaktor.Add((f, kols.ToArray()));
            }
            foreach (var c in kovariat) { pred.Add(AmbilN(c)); namaPred.Add(c); }

            Hasil? h;
            double[] yFit;
            double[] mFit = Array.Empty<double>();
            if (gamma)
            {
                var y = AmbilN(respon);
                if (y.Any(v => v <= 0 || double.IsNaN(v)))
                {
                    blocks.Add(Blocks.Note(
                        $"Respon '{respon}' harus positif untuk GLM Gamma.",
                        NoteKind.Error));
                    return blocks;
                }
                h = FitGamma(y, pred, namaPred);
                yFit = y;
            }
            else
            {
                var kk = AmbilN(respon);
                var mm = AmbilN(percobaan);
                h = FitBinom(kk, mm, pred, namaPred);
                yFit = new double[baris.Count];
                for (int i = 0; i < baris.Count; i++) yFit[i] = kk[i] / mm[i];
                mFit = mm;
            }
            if (h is null)
            {
                blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.", NoteKind.Error));
                return blocks;
            }

            foreach (var (f, kols) in rentangFaktor)
            {
                var kurang = pred.Where((_, j) => !kols.Contains(j)).ToList();
                var namaKurang = namaPred.Where((_, j) => !kols.Contains(j)).ToList();
                Hasil? hr = gamma
                    ? FitGamma(yFit, kurang, namaKurang)
                    : FitBinom(yFit.Zip(mFit, (p, m) => p * m).ToArray(),
                               mFit, kurang, namaKurang);
                if (hr is null) continue;
                double naik = hr.Deviance - h.Deviance;
                int df = kols.Length;
                h.TetesFaktor.Add(new Tetes
                {
                    Faktor = f, DevNaik = naik, Df = df,
                    P = Distributions.ChiSquareUpper(naik, df),
                });
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                gamma ? DaftarRumus.GlmGamma : DaftarRumus.GlmBinom));

            var sub = new List<(string, string)>
            {
                ("Keluarga", gamma ? "Gamma, taut log" : "Binomial, taut logit"),
                ("Respon", respon),
                ("Faktor", string.Join(", ", faktor)),
                ("Kovariat", kovariat.Count > 0 ? string.Join(", ", kovariat) : "(tidak ada)"),
                ("Banyak baris lengkap", $"n = {Fmt.Int(h.N)}"),
                ("Iterasi IRLS", $"{Fmt.Int(h.Iterasi)} ({(h.Konvergen ? "konvergen" : "TIDAK konvergen")})"),
            };
            if (!gamma) sub.Insert(2, ("Percobaan", percobaan));
            blocks.Add(Blocks.Substitusi("Pemasukan nilai", sub.ToArray()));

            blocks.Add(Blocks.Table(
                "Koefisien",
                new[] { "Suku", "B", "Galat baku", "Wald z", "p" },
                h.Nama.Select((nm, j) => new[]
                {
                    nm, Fmt.Num(h.B[j], 6), Fmt.Num(h.Se[j], 6),
                    Fmt.Num(h.Z[j], 4), Fmt.P(h.P[j]),
                }).ToArray(),
                "Galat baku dari akar diagonal (X'WX)⁻¹ dikali skala "
                + (gamma ? "(Pearson χ²/df)." : "(tetap 1 untuk Binomial).")));

            blocks.Add(Blocks.Table(
                "Kesesuaian model",
                new[] { "Besaran", "Nilai" },
                new[]
                {
                    new[] { "Deviance", Fmt.Num(h.Deviance, 4) },
                    new[] { "Derajat bebas residual", Fmt.Int(h.DfResid) },
                    new[] { "Pearson χ²", Fmt.Num(h.PearsonChi2, 4) },
                    new[] { "Skala (dispersi)", Fmt.Num(h.Skala, 6) },
                    new[] { "ln L", Fmt.Num(h.LogLik, 4) },
                    new[] { "ln L (model nol)", Fmt.Num(h.LogLikNol, 4) },
                    new[] { "Pseudo R² (McFadden)", Fmt.Num(h.PseudoR2, 6) },
                    new[] { "AIC", Fmt.Num(h.Aic, 4) },
                },
                gamma ? "Skala diestimasi dari Pearson χ²/df — bukan 1."
                      : "Skala Binomial tetap 1 menurut definisi."));

            if (h.TetesFaktor.Count > 0)
                blocks.Add(Blocks.Table(
                    "Uji tiap faktor (penurunan deviansi)",
                    new[] { "Faktor", "Kenaikan deviansi", "df", "p (χ²)" },
                    h.TetesFaktor.Select(t => new[]
                    {
                        t.Faktor, Fmt.Num(t.DevNaik, 4),
                        Fmt.Int(t.Df), Fmt.P(t.P),
                    }).ToArray(),
                    "Model penuh lawan model tanpa faktor itu. "
                    + "Faktor yang tidak menaikkan deviansi secara nyata tidak "
                    + "menambah apa pun pada model."));

            return blocks;
        }
    }
}
