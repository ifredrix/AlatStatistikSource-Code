using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class Cacah
    {
        public sealed class Hasil
        {
            public string Keluarga = "";
            public int N;
            public List<string> NamaKoef = new();
            public double[] Koef = Array.Empty<double>();
            public double Pi;
            public double Alpha = double.NaN;
            public double Llf;
            public double Aic;
            public double[] Fitted = Array.Empty<double>();
            public int Iterasi;
        }

        private static double[] IrlsPoissonBerbobot(double[] y, double[][] x,
                                                    double[] bobot)
        {
            int n = y.Length, k = x[0].Length;
            var beta = new double[k];
            for (int iter = 0; iter < 100; iter++)
            {
                var xtwx = new double[k, k];
                var xtwz = new double[k];
                for (int i = 0; i < n; i++)
                {
                    double eta = 0;
                    for (int a = 0; a < k; a++) eta += x[i][a] * beta[a];
                    double mu = Math.Max(Math.Exp(eta), 1e-12);
                    double z = eta + (y[i] - mu) / mu;
                    double w = bobot[i] * mu;
                    for (int a = 0; a < k; a++)
                    {
                        xtwz[a] += x[i][a] * w * z;
                        for (int b = 0; b < k; b++)
                            xtwx[a, b] += x[i][a] * w * x[i][b];
                    }
                }
                var inv = Regression.Inverse((double[,])xtwx.Clone());
                if (inv is null) return beta;
                double beda = 0;
                for (int a = 0; a < k; a++)
                {
                    double s = 0;
                    for (int b = 0; b < k; b++) s += inv[a, b] * xtwz[b];
                    beda = Math.Max(beda, Math.Abs(s - beta[a]));
                    beta[a] = s;
                }
                if (beda < 1e-10) break;
            }
            return beta;
        }

        private static double LlfZip(double[] y, double[] lam, double pi)
        {
            double s = 0;
            for (int i = 0; i < y.Length; i++)
            {
                if (y[i] == 0)
                    s += Math.Log(Math.Max(pi + (1 - pi) * Math.Exp(-lam[i]), 1e-300));
                else
                    s += Math.Log(Math.Max(1 - pi, 1e-300)) + y[i] * Math.Log(lam[i])
                         - lam[i] - Special.LnGamma(y[i] + 1.0);
            }
            return s;
        }

        public static Hasil? PasangZip(double[] y, List<double[]> pred,
                                       List<string> namaPred)
        {
            int n = y.Length, p = pred.Count;
            if (n < 10 || p < 1) return null;
            if (pred.Any(v => v.Length != n)) return null;
            if (y.Any(v => v < 0 || double.IsNaN(v) || Math.Floor(v) != v)) return null;
            int k = p + 1;
            var x = new double[n][];
            for (int i = 0; i < n; i++)
            {
                x[i] = new double[k];
                x[i][0] = 1.0;
                for (int j = 0; j < p; j++) x[i][j + 1] = pred[j][i];
            }

            var satu = Enumerable.Repeat(1.0, n).ToArray();
            var beta = IrlsPoissonBerbobot(y, x, satu);
            double pi = 0.2;
            double llLama = double.NegativeInfinity;
            int iterasi = 0;
            var w = new double[n];
            for (iterasi = 1; iterasi <= 500; iterasi++)
            {
                var lam = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double eta = 0;
                    for (int a = 0; a < k; a++) eta += x[i][a] * beta[a];
                    lam[i] = Math.Max(Math.Exp(eta), 1e-12);
                }
                for (int i = 0; i < n; i++)
                    w[i] = y[i] == 0
                        ? pi / Math.Max(pi + (1 - pi) * Math.Exp(-lam[i]), 1e-300)
                        : 0.0;
                var wb = new double[n];
                for (int i = 0; i < n; i++) wb[i] = 1 - w[i];
                beta = IrlsPoissonBerbobot(y, x, wb);
                pi = w.Sum() / n;
                double ll = LlfZip(y, lam, pi);
                if (Math.Abs(ll - llLama) < 1e-13) break;
                llLama = ll;
            }

            var lamAkhir = new double[n];
            for (int i = 0; i < n; i++)
            {
                double eta = 0;
                for (int a = 0; a < k; a++) eta += x[i][a] * beta[a];
                lamAkhir[i] = Math.Max(Math.Exp(eta), 1e-12);
            }
            double llf = LlfZip(y, lamAkhir, pi);
            var nama = new List<string> { "inflate (pi)" };
            var kv = new List<double> { pi };
            nama.Add("const");
            kv.Add(beta[0]);
            for (int j = 0; j < p; j++)
            {
                nama.Add(namaPred[j]);
                kv.Add(beta[j + 1]);
            }
            return new Hasil
            {
                Keluarga = "ZIP", N = n, NamaKoef = nama, Koef = kv.ToArray(),
                Pi = pi, Llf = llf, Aic = -2 * llf + 2 * (k + 1),
                Fitted = lamAkhir.Select((l, i) => (1 - pi) * l).ToArray(),
                Iterasi = iterasi,
            };
        }

        private static double LogNbPmf(double y, double mu, double alpha)
        {
            double r = 1.0 / Math.Max(alpha, 1e-9);
            return Special.LnGamma(y + r) - Special.LnGamma(r)
                   - Special.LnGamma(y + 1.0)
                   - (y + r) * Math.Log(1 + alpha * mu)
                   + y * Math.Log(Math.Max(alpha * mu, 1e-300));
        }

        private static double NllNbW(double[] t, double[] y, double[][] x,
                                     double[] bobot)
        {
            int n = y.Length, k = x[0].Length;
            var beta = new double[k];
            for (int a = 0; a < k; a++) beta[a] = t[a];
            double alpha = Math.Exp(t[k]);
            if (!(alpha > 1e-9) || double.IsInfinity(alpha)) return 1e100;
            double s = 0;
            for (int i = 0; i < n; i++)
            {
                if (bobot[i] <= 0) continue;
                double eta = 0;
                for (int a = 0; a < k; a++) eta += x[i][a] * beta[a];
                double mu = Math.Max(Math.Exp(eta), 1e-12);
                s += bobot[i] * LogNbPmf(y[i], mu, alpha);
            }
            return double.IsNaN(s) || double.IsInfinity(s) ? 1e100 : -s;
        }

        private static double LlfZinb(double[] y, double[] mu, double alpha,
                                      double pi)
        {
            double s = 0;
            for (int i = 0; i < y.Length; i++)
            {
                double pnb = Math.Exp(LogNbPmf(y[i], mu[i], alpha));
                if (y[i] == 0)
                    s += Math.Log(Math.Max(pi + (1 - pi) * pnb, 1e-300));
                else
                    s += Math.Log(Math.Max(1 - pi, 1e-300)) + Math.Log(Math.Max(pnb, 1e-300));
            }
            return s;
        }

        public static Hasil? PasangZinb(double[] y, List<double[]> pred,
                                        List<string> namaPred)
        {
            int n = y.Length, p = pred.Count;
            if (n < 10 || p < 1) return null;
            if (pred.Any(v => v.Length != n)) return null;
            if (y.Any(v => v < 0 || double.IsNaN(v) || Math.Floor(v) != v)) return null;
            int k = p + 1;
            var x = new double[n][];
            for (int i = 0; i < n; i++)
            {
                x[i] = new double[k];
                x[i][0] = 1.0;
                for (int j = 0; j < p; j++) x[i][j + 1] = pred[j][i];
            }

            var satu = Enumerable.Repeat(1.0, n).ToArray();
            var beta = IrlsPoissonBerbobot(y, x, satu);
            double alpha = 1.0, pi = 0.2;
            double llLama = double.NegativeInfinity;
            int iterasi = 0;
            var w = new double[n];
            var wb = new double[n];
            var mu = new double[n];
            Func<double[], double> f = t => NllNbW(t, y, x, wb);
            var langkah = Enumerable.Repeat(0.2, k + 1).ToArray();
            langkah[0] = 0.5;
            for (iterasi = 1; iterasi <= 200; iterasi++)
            {
                for (int i = 0; i < n; i++)
                {
                    double eta = 0;
                    for (int a = 0; a < k; a++) eta += x[i][a] * beta[a];
                    mu[i] = Math.Max(Math.Exp(eta), 1e-12);
                }
                for (int i = 0; i < n; i++)
                {
                    if (y[i] != 0) { w[i] = 0.0; continue; }
                    double pnb = Math.Exp(LogNbPmf(0, mu[i], alpha));
                    w[i] = pi / Math.Max(pi + (1 - pi) * pnb, 1e-300);
                }
                // Bobot NB = komplemen bobot nol-struktural (sama pola PasangZip).
                for (int i = 0; i < n; i++) wb[i] = 1.0 - w[i];
                var x0 = new double[k + 1];
                for (int a = 0; a < k; a++) x0[a] = beta[a];
                x0[k] = Math.Log(Math.Max(alpha, 1e-6));
                var xs = Arima.NelderMead(f, x0, langkah, out _);
                for (int a = 0; a < k; a++) beta[a] = xs[a];
                alpha = Math.Exp(xs[k]);
                pi = w.Sum() / n;
                double ll = LlfZinb(y, mu, alpha, pi);
                if (Math.Abs(ll - llLama) < 1e-10) break;
                llLama = ll;
            }

            // Poles akurasi: optimasi langsung ln L marginal ZINB (objek SAMA
            // dengan statsmodels) di atas hasil EM, supaya π/α/koefisien cocok
            // ke presisi mesin, bukan cuma ~6 angka penting. EM memberi titik
            {
                Func<double[], double> g = t =>
                {
                    var bb = new double[k];
                    for (int a = 0; a < k; a++) bb[a] = t[a];
                    double al = Math.Exp(t[k]);
                    double pl = 1.0 / (1.0 + Math.Exp(-t[k + 1]));
                    if (!(al > 1e-12) || double.IsInfinity(al) || pl <= 0 || pl >= 1)
                        return 1e100;
                    double s = 0;
                    for (int i = 0; i < n; i++)
                    {
                        double eta = 0;
                        for (int a = 0; a < k; a++) eta += x[i][a] * bb[a];
                        double muI = Math.Max(Math.Exp(eta), 1e-12);
                        double pnb = Math.Exp(LogNbPmf(y[i], muI, al));
                        double pr = y[i] == 0 ? pl + (1 - pl) * pnb : (1 - pl) * pnb;
                        if (pr <= 0) return 1e100;
                        s += Math.Log(pr);
                    }
                    return -s;
                };
                var t0 = new double[k + 2];
                for (int a = 0; a < k; a++) t0[a] = beta[a];
                t0[k] = Math.Log(Math.Max(alpha, 1e-6));
                t0[k + 1] = Math.Log(Math.Max(pi / (1 - pi), 1e-6));
                var langkah2 = Enumerable.Repeat(0.1, k + 2).ToArray();
                langkah2[0] = 0.3;
                var tp = Arima.NelderMead(g, t0, langkah2, out _);
                for (int a = 0; a < k; a++) beta[a] = tp[a];
                alpha = Math.Exp(tp[k]);
                pi = 1.0 / (1.0 + Math.Exp(-tp[k + 1]));
            }

            for (int i = 0; i < n; i++)
            {
                double eta = 0;
                for (int a = 0; a < k; a++) eta += x[i][a] * beta[a];
                mu[i] = Math.Max(Math.Exp(eta), 1e-12);
            }
            double llf = LlfZinb(y, mu, alpha, pi);
            var nama = new List<string> { "inflate (pi)" };
            var kv = new List<double> { pi };
            nama.Add("const");
            kv.Add(beta[0]);
            for (int j = 0; j < p; j++)
            {
                nama.Add(namaPred[j]);
                kv.Add(beta[j + 1]);
            }
            nama.Add("alpha");
            kv.Add(alpha);
            return new Hasil
            {
                Keluarga = "ZINB", N = n, NamaKoef = nama, Koef = kv.ToArray(),
                Pi = pi, Alpha = alpha, Llf = llf, Aic = -2 * llf + 2 * (k + 2),
                Fitted = mu.Select((m, i) => (1 - pi) * m).ToArray(),
                Iterasi = iterasi,
            };
        }
        private static double NllNb(double[] t, double[] y, double[][] x)
        {
            int n = y.Length, k = x[0].Length;
            var beta = new double[k];
            for (int a = 0; a < k; a++) beta[a] = t[a];
            double alpha = Math.Exp(t[k]);
            if (!(alpha > 1e-9) || double.IsInfinity(alpha)) return 1e100;
            double r = 1.0 / alpha;
            double s = 0;
            for (int i = 0; i < n; i++)
            {
                double eta = 0;
                for (int a = 0; a < k; a++) eta += x[i][a] * beta[a];
                double mu = Math.Max(Math.Exp(eta), 1e-12);
                s += Special.LnGamma(y[i] + r) - Special.LnGamma(r)
                     - Special.LnGamma(y[i] + 1.0)
                     - (y[i] + r) * Math.Log(1 + alpha * mu)
                     + y[i] * Math.Log(Math.Max(alpha * mu, 1e-300));
            }
            return double.IsNaN(s) || double.IsInfinity(s) ? 1e100 : -s;
        }

        public static Hasil? PasangNb(double[] y, List<double[]> pred,
                                      List<string> namaPred)
        {
            int n = y.Length, p = pred.Count;
            if (n < 10 || p < 1) return null;
            if (pred.Any(v => v.Length != n)) return null;
            if (y.Any(v => v < 0 || double.IsNaN(v) || Math.Floor(v) != v)) return null;
            int k = p + 1;
            var x = new double[n][];
            for (int i = 0; i < n; i++)
            {
                x[i] = new double[k];
                x[i][0] = 1.0;
                for (int j = 0; j < p; j++) x[i][j + 1] = pred[j][i];
            }
            double mean = y.Sum() / n;
            var x0 = new double[k + 1];
            x0[0] = Math.Log(Math.Max(mean, 1e-6));
            x0[k] = 0.0;
            var langkah = Enumerable.Repeat(0.2, k + 1).ToArray();
            langkah[0] = 0.5;
            Func<double[], double> f = t => NllNb(t, y, x);
            var xA = Arima.NelderMead(f, x0, langkah, out _);
            var xB = new double[k + 1];
            xB[0] = Math.Log(Math.Max(mean, 1e-6));
            xB[k] = -0.5;
            var sB = Arima.NelderMead(f, xB, langkah, out int itB);
            var xs = f(xA) <= f(sB) ? xA : sB;

            var beta = new double[k];
            for (int a = 0; a < k; a++) beta[a] = xs[a];
            double alpha = Math.Exp(xs[k]);
            double llf = -f(xs);
            var nama = new List<string> { "const" };
            var kv = new List<double> { beta[0] };
            for (int j = 0; j < p; j++)
            {
                nama.Add(namaPred[j]);
                kv.Add(beta[j + 1]);
            }
            nama.Add("alpha");
            kv.Add(alpha);
            var fit = new double[n];
            for (int i = 0; i < n; i++)
            {
                double eta = 0;
                for (int a = 0; a < k; a++) eta += x[i][a] * beta[a];
                fit[i] = Math.Exp(eta);
            }
            return new Hasil
            {
                Keluarga = "NB", N = n, NamaKoef = nama, Koef = kv.ToArray(),
                Alpha = alpha, Llf = llf, Aic = -2 * llf + 2 * (k + 1),
                Fitted = fit, Iterasi = itB,
            };
        }

        public static List<ResultBlock> CacahBlocks(Dataset ds, string cacahan,
            List<string> bebas, string keluarga)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Regresi cacahan lanjut (ZIP / NB / ZINB)", 1)
            };
            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Cacah));

            bool nb = keluarga.StartsWith("Binomial", StringComparison.OrdinalIgnoreCase)
                      || keluarga.StartsWith("Negatif", StringComparison.OrdinalIgnoreCase)
                      || keluarga.StartsWith("NB", StringComparison.OrdinalIgnoreCase);
            bool zinb = !nb && (keluarga.StartsWith("ZINB", StringComparison.OrdinalIgnoreCase)
                      || keluarga.StartsWith("Zero", StringComparison.OrdinalIgnoreCase));
            var perlu = new List<string>(bebas) { cacahan };
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < 10)
            {
                blocks.Add(Blocks.Note("Baris lengkap kurang dari 10.",
                                       NoteKind.Error));
                return blocks;
            }
            var sn = ds.Numeric(cacahan);
            var y = baris.Select(i => sn[i] ?? double.NaN).ToArray();
            if (y.Any(v => double.IsNaN(v) || v < 0 || Math.Floor(v) != v))
            {
                blocks.Add(Blocks.Note("Cacahan harus bilangan bulat ≥ 0.",
                                       NoteKind.Error));
                return blocks;
            }
            var pred = new List<double[]>();
            foreach (string v in bebas)
            {
                var semua = ds.Numeric(v);
                pred.Add(baris.Select(i => semua[i] ?? double.NaN).ToArray());
            }
            if (pred.Any(c => c.Any(double.IsNaN)))
            {
                blocks.Add(Blocks.Note("Prediktor punya nilai hilang.",
                                       NoteKind.Error));
                return blocks;
            }

            var h = zinb ? PasangZinb(y, pred, bebas)
                      : nb ? PasangNb(y, pred, bebas) : PasangZip(y, pred, bebas);
            if (h is null)
            {
                blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.",
                                       NoteKind.Error));
                return blocks;
            }

            var langkah = new List<(string, string)>
            {
                ("Keluarga", zinb ? "Zero-Inflated Negatif Binomial (EM)"
                                : nb ? "Binomial Negatif P=2 (Var = μ + αμ²)"
                                : "Zero-Inflated Poisson (EM)"),
                ("Cacahan", cacahan),
                ("Prediktor", string.Join(", ", bebas)),
                ("Banyak baris", $"n = {Fmt.Int(h.N)}"),
                ("ln L", Fmt.Num(h.Llf, 4)),
                ("AIC", Fmt.Num(h.Aic, 4)),
                ("Iterasi", Fmt.Int(h.Iterasi)),
            };
            if (!nb) langkah.Add(("Peluang nol-struktural", $"π̂ = {Fmt.Num(h.Pi, 4)}"));
            if (nb || zinb) langkah.Add(("Dispersi", $"α̂ = {Fmt.Num(h.Alpha, 4)} (α = 0 → Poisson)"));
            blocks.Add(Blocks.Substitusi("Pemasukan nilai", langkah.ToArray()));

            blocks.Add(Blocks.Table(
                "Koefisien (MLE)",
                new[] { "Parameter", "Nilai" },
                h.NamaKoef.Select((nm, i) =>
                    new[] { nm, Fmt.Num(h.Koef[i], 6) }).ToArray(),
                nb || zinb ? "α ditaksir bersama β lewat Nelder–Mead."
                   : "π = rerata bobot-nol EM; β lewat Poisson terbobot. "
                   + "Galat baku tak-dilaporkan untuk ZIP (dinyatakan)."));

            return blocks;
        }
    }
}
