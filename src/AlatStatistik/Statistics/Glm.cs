using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Glm
    {
        private const int IterasiMaks = 100;
        private const double ToleransiIri = 1e-12;

        

        public sealed class HasilWls
        {
            public List<string> Nama = new();
            public double[] B = Array.Empty<double>();
            public double[] Se = Array.Empty<double>();
            public double[] T = Array.Empty<double>();
            public double[] P = Array.Empty<double>();
            public int N, DfModel, DfResid;
            public double Sigma2, R2, R2Adj, F, FP, Ssr, SstBobot;
        }

        public static HasilWls? FitWls(double[] y, List<double[]> pred, List<string> namaPred,
                                       double[] bobot)
        {
            int n = y.Length, p = pred.Count, k = p + 1;
            if (n < k + 1 || p < 1) return null;
            if (pred.Any(v => v.Length != n) || bobot.Length != n) return null;
            if (bobot.Any(w => !(w > 0) || double.IsNaN(w))) return null;

            var x = new double[n][];
            for (int i = 0; i < n; i++)
            {
                x[i] = new double[k];
                x[i][0] = 1.0;
                for (int j = 0; j < p; j++) x[i][j + 1] = pred[j][i];
            }

            var xtwx = new double[k, k];
            var xtwy = new double[k];
            for (int i = 0; i < n; i++)
                for (int a = 0; a < k; a++)
                {
                    xtwy[a] += x[i][a] * bobot[i] * y[i];
                    for (int b = 0; b < k; b++) xtwx[a, b] += x[i][a] * bobot[i] * x[i][b];
                }

            var inv = Regression.Inverse((double[,])xtwx.Clone());
            if (inv is null) return null;

            var beta = new double[k];
            for (int a = 0; a < k; a++)
            {
                double s = 0;
                for (int b = 0; b < k; b++) s += inv[a, b] * xtwy[b];
                beta[a] = s;
            }

            double ssr = 0;
            for (int i = 0; i < n; i++)
            {
                double prediksi = 0;
                for (int a = 0; a < k; a++) prediksi += beta[a] * x[i][a];
                double e = y[i] - prediksi;
                ssr += bobot[i] * e * e;
            }

            int dfRes = n - k, dfMod = p;
            double sigma2 = ssr / dfRes;

            
            
            
            double jmlBobot = 0, rataBobot = 0;
            for (int i = 0; i < n; i++) { jmlBobot += bobot[i]; rataBobot += bobot[i] * y[i]; }
            rataBobot /= jmlBobot;

            double sst = 0;
            for (int i = 0; i < n; i++)
            {
                double d = y[i] - rataBobot;
                sst += bobot[i] * d * d;
            }

            double r2 = sst > 0 ? 1 - ssr / sst : double.NaN;
            double r2adj = 1 - (1 - r2) * (n - 1) / (n - k);
            double msMod = (sst - ssr) / dfMod;
            double f = sigma2 > 0 ? msMod / sigma2 : double.NaN;

            var nama = new List<string> { "(Konstanta)" };
            nama.AddRange(namaPred);

            var se = new double[k];
            var t = new double[k];
            var pv = new double[k];
            for (int a = 0; a < k; a++)
            {
                se[a] = Math.Sqrt(Math.Max(0, sigma2 * inv[a, a]));
                t[a] = se[a] > 0 ? beta[a] / se[a] : double.NaN;
                pv[a] = Distributions.StudentTTwoSided(t[a], dfRes);
            }

            return new HasilWls
            {
                Nama = nama, B = beta, Se = se, T = t, P = pv,
                N = n, DfModel = dfMod, DfResid = dfRes,
                Sigma2 = sigma2, R2 = r2, R2Adj = r2adj,
                F = f, FP = Distributions.FUpper(f, dfMod, dfRes),
                Ssr = ssr, SstBobot = sst,
            };
        }

        public static List<ResultBlock> WlsBlocks(Dataset ds, string dependen, List<string> bebas,
                                                  string bobotVar, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading("Regresi kuadrat terkecil berbobot (WLS)", 1) };

            var perlu = new List<string> { dependen, bobotVar };
            perlu.AddRange(bebas);
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < bebas.Count + 3)
            {
                blocks.Add(Blocks.Note(
                    "Baris lengkap untuk variabel terpilih kurang dari "
                    + $"{bebas.Count + 3}. WLS tidak bisa dijalankan.", NoteKind.Error));
                return blocks;
            }

            double[] Ambil(string v)
            {
                var semua = ds.Numeric(v);
                var a = new double[baris.Count];
                for (int i = 0; i < baris.Count; i++) a[i] = semua[baris[i]] ?? double.NaN;
                return a;
            }

            var y = Ambil(dependen);
            var w = Ambil(bobotVar);
            var pred = bebas.Select(Ambil).ToList();

            var h = FitWls(y, pred, bebas, w);
            if (h is null)
            {
                blocks.Add(Blocks.Note(
                    "Model tidak bisa disesuaikan. Periksa apakah semua bobot positif "
                    + "dan tidak ada prediktor yang saling bergantung sempurna.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.Wls, DaftarRumus.WlsGalatBaku));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Dependen", dependen),
                ("Prediktor", string.Join(", ", bebas)),
                ("Bobot", bobotVar),
                ("Banyak baris lengkap", $"n = {Fmt.Int(h.N)}"),
                ("Derajat bebas", $"regresi = {Fmt.Int(h.DfModel)}, residual = {Fmt.Int(h.DfResid)}")));

            blocks.Add(Blocks.Table(
                "Koefisien",
                new[] { "Suku", "B", "Galat baku", "t", "p" },
                h.Nama.Select((nm, j) => new[]
                {
                    nm, Fmt.Num(h.B[j], 6), Fmt.Num(h.Se[j], 6),
                    Fmt.Num(h.T[j], 4), Fmt.P(h.P[j]),
                }).ToArray(),
                "Galat baku memakai σ² = Σwᵢeᵢ²/(n−k). t = B / galat baku, "
                + $"dibandingkan dengan sebaran t berderajat bebas {Fmt.Int(h.DfResid)}."));

            blocks.Add(Blocks.Table(
                "Ringkasan model",
                new[] { "Besaran", "Nilai" },
                new[]
                {
                    new[] { "R² (berbobot)", Fmt.Num(h.R2, 6) },
                    new[] { "R² disesuaikan", Fmt.Num(h.R2Adj, 6) },
                    new[] { "F", Fmt.Num(h.F, 4) },
                    new[] { "p (F)", Fmt.P(h.FP) },
                    new[] { "σ² (ragam residual)", Fmt.Num(h.Sigma2, 6) },
                    new[] { "Jumlah kuadrat residual berbobot", Fmt.Num(h.Ssr, 6) },
                    new[] { "Jumlah kuadrat total berbobot", Fmt.Num(h.SstBobot, 6) },
                },
                "R² memakai rerata BERBOBOT sebagai pusat, bukan rerata biasa — "
                + "kalau tidak, angkanya tidak akan sama dengan acuan mana pun."));

            return blocks;
        }

        

        public sealed class HasilPoisson
        {
            public List<string> Nama = new();
            public double[] B = Array.Empty<double>();
            public double[] Se = Array.Empty<double>();
            public double[] Z = Array.Empty<double>();
            public double[] P = Array.Empty<double>();
            public double[] Rasio = Array.Empty<double>();
            public int N, K, DfResid, Iterasi;
            public double Deviance, PearsonChi2, LogLik, LogLikNol, PseudoR2, Aic;
            public bool Konvergen;
            public string? Catatan;
        }

        // Larik sepanjang n berisi NaN, dipakai bila model tidak konvergen
        // supaya tabel koefisien tetap punya baris yang lengkap.
        private static double[] IsiNaN(int n)
        {
            var a = new double[n];
            Array.Fill(a, double.NaN);
            return a;
        }

        public static HasilPoisson? FitPoisson(double[] y, List<double[]> pred, List<string> namaPred)
        {
            int n = y.Length, p = pred.Count, k = p + 1;
            var nama = new List<string> { "(Konstanta)" };
            nama.AddRange(namaPred);

            if (n < k + 2 || p < 1) return null;
            if (pred.Any(v => v.Length != n)) return null;
            if (y.Any(v => v < 0 || double.IsNaN(v))) return null;

            var x = new double[n][];
            for (int i = 0; i < n; i++)
            {
                x[i] = new double[k];
                x[i][0] = 1.0;
                for (int j = 0; j < p; j++) x[i][j + 1] = pred[j][i];
            }

            var beta = new double[k];
            double[,]? inv = null;
            int iterasi = 0;
            bool konvergen = false;

            for (iterasi = 1; iterasi <= IterasiMaks; iterasi++)
            {
                var mu = new double[n];
                var z = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double eta = 0;
                    for (int a = 0; a < k; a++) eta += x[i][a] * beta[a];
                    mu[i] = Math.Max(Math.Exp(eta), 1e-12);
                    z[i] = eta + (y[i] - mu[i]) / mu[i];
                }

                var xtwx = new double[k, k];
                var xtwz = new double[k];
                for (int i = 0; i < n; i++)
                    for (int a = 0; a < k; a++)
                    {
                        xtwz[a] += x[i][a] * mu[i] * z[i];
                        for (int b = 0; b < k; b++) xtwx[a, b] += x[i][a] * mu[i] * x[i][b];
                    }

                inv = Regression.Inverse((double[,])xtwx.Clone());
                if (inv is null)
                    return new HasilPoisson
                    {
                        // Larik koefisien harus sepanjang Nama; kalau dibiarkan
                        // kosong, pembuat tabel mengakses B[j] di luar jangkauan.
                        Nama = nama, N = n, K = p, Iterasi = iterasi, Konvergen = false,
                        B = IsiNaN(k), Se = IsiNaN(k), Z = IsiNaN(k), P = IsiNaN(k), Rasio = IsiNaN(k),
                        Catatan = "Matriks informasi tidak bisa dibalik — mungkin ada "
                                  + "prediktor yang saling bergantung.",
                    };

                var baru = new double[k];
                for (int a = 0; a < k; a++)
                {
                    double s = 0;
                    for (int b = 0; b < k; b++) s += inv[a, b] * xtwz[b];
                    baru[a] = s;
                }

                double beda = 0;
                for (int a = 0; a < k; a++)
                {
                    beda = Math.Max(beda, Math.Abs(baru[a] - beta[a]));
                    beta[a] = baru[a];
                }
                if (beda < ToleransiIri) { konvergen = true; break; }
            }

            double deviance = 0, pearson = 0, ll = 0;
            for (int i = 0; i < n; i++)
            {
                double eta = 0;
                for (int a = 0; a < k; a++) eta += x[i][a] * beta[a];
                double m = Math.Max(Math.Exp(eta), 1e-12);

                
                double suku = y[i] > 0 ? y[i] * Math.Log(y[i] / m) : 0.0;
                deviance += 2 * (suku - (y[i] - m));
                pearson += (y[i] - m) * (y[i] - m) / m;
                ll += y[i] * Math.Log(m) - m - Special.LnGamma(y[i] + 1.0);
            }

            double rataY = y.Average();
            double llNol = 0;
            for (int i = 0; i < n; i++)
                llNol += y[i] * Math.Log(Math.Max(rataY, 1e-12)) - rataY
                         - Special.LnGamma(y[i] + 1.0);

            var se = new double[k];
            var zz = new double[k];
            var pv = new double[k];
            var rasio = new double[k];
            for (int a = 0; a < k; a++)
            {
                se[a] = Math.Sqrt(Math.Max(0, inv![a, a]));
                zz[a] = se[a] > 0 ? beta[a] / se[a] : double.NaN;
                pv[a] = 2 * Distributions.NormalCdf(-Math.Abs(zz[a]));
                rasio[a] = Math.Exp(beta[a]);
            }

            return new HasilPoisson
            {
                Nama = nama, B = beta, Se = se, Z = zz, P = pv, Rasio = rasio,
                N = n, K = p, DfResid = n - k, Iterasi = iterasi, Konvergen = konvergen,
                Deviance = deviance, PearsonChi2 = pearson,
                LogLik = ll, LogLikNol = llNol,
                PseudoR2 = llNol != 0 ? 1 - ll / llNol : double.NaN,
                Aic = -2 * ll + 2 * k,
            };
        }

        public static List<ResultBlock> PoissonBlocks(Dataset ds, string dependen, List<string> bebas)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Regresi Poisson (data cacahan)", 1)
            };

            var perlu = new List<string> { dependen };
            perlu.AddRange(bebas);
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < bebas.Count + 3)
            {
                blocks.Add(Blocks.Note(
                    $"Baris lengkap kurang dari {bebas.Count + 3}. Poisson tidak bisa dijalankan.",
                    NoteKind.Error));
                return blocks;
            }

            double[] Ambil(string v)
            {
                var semua = ds.Numeric(v);
                var a = new double[baris.Count];
                for (int i = 0; i < baris.Count; i++) a[i] = semua[baris[i]] ?? double.NaN;
                return a;
            }

            var y = Ambil(dependen);
            if (y.Any(v => v < 0))
            {
                blocks.Add(Blocks.Note(
                    $"Variabel '{dependen}' punya nilai negatif. Regresi Poisson hanya untuk "
                    + "cacahan (0, 1, 2, …).", NoteKind.Error));
                return blocks;
            }
            if (y.Any(v => Math.Abs(v - Math.Round(v)) > 1e-9))
            {
                blocks.Add(Blocks.Note(
                    $"Variabel '{dependen}' punya nilai bukan bilangan bulat. Poisson untuk "
                    + "cacahan; kalau ini rasio atau laju, sebarannya berbeda.", NoteKind.Warning));
            }

            var h = FitPoisson(y, bebas.Select(Ambil).ToList(), bebas);
            if (h is null)
            {
                blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.RegresiPoisson, DaftarRumus.DeviancePoisson));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Dependen (cacahan)", dependen),
                ("Prediktor", string.Join(", ", bebas)),
                ("Banyak baris lengkap", $"n = {Fmt.Int(h.N)}"),
                ("Iterasi IRLS", $"{Fmt.Int(h.Iterasi)} ({(h.Konvergen ? "konvergen" : "TIDAK konvergen")})"),
                ("Derajat bebas residual", Fmt.Int(h.DfResid))));

            blocks.Add(Blocks.Table(
                "Koefisien",
                new[] { "Suku", "B", "Galat baku", "Wald z", "p", "exp(B)" },
                h.Nama.Select((nm, j) => new[]
                {
                    nm, Fmt.Num(h.B[j], 6), Fmt.Num(h.Se[j], 6),
                    Fmt.Num(h.Z[j], 4), Fmt.P(h.P[j]), Fmt.Num(h.Rasio[j], 4),
                }).ToArray(),
                "exp(B) adalah rasio laju: exp(B) = 1,5 berarti cacahan yang diharapkan "
                + "naik 50% untuk setiap kenaikan satu satuan prediktor."));

            double rasioPearson = h.DfResid > 0 ? h.PearsonChi2 / h.DfResid : double.NaN;
            blocks.Add(Blocks.Table(
                "Kesesuaian model",
                new[] { "Besaran", "Nilai" },
                new[]
                {
                    new[] { "Deviance", Fmt.Num(h.Deviance, 4) },
                    new[] { "Derajat bebas residual", Fmt.Int(h.DfResid) },
                    new[] { "p (deviance, χ²)", Fmt.P(Distributions.ChiSquareUpper(h.Deviance, h.DfResid)) },
                    new[] { "Pearson χ²", Fmt.Num(h.PearsonChi2, 4) },
                    new[] { "Pearson χ² / df", Fmt.Num(rasioPearson, 4) },
                    new[] { "ln L", Fmt.Num(h.LogLik, 4) },
                    new[] { "ln L (model nol)", Fmt.Num(h.LogLikNol, 4) },
                    new[] { "Pseudo R² (McFadden)", Fmt.Num(h.PseudoR2, 6) },
                    new[] { "AIC", Fmt.Num(h.Aic, 4) },
                },
                rasioPearson > 2
                    ? "**Peringatan: kemungkinan overdispersi.** Pearson χ²/df = "
                      + $"{Fmt.Num(rasioPearson, 2)} (jauh di atas 1), artinya ragam datanya lebih besar "
                      + "daripada yang diasumsikan Poisson. Galat bakunya karena itu cenderung "
                      + "terlalu kecil, dan p-nya terlalu optimistis."
                    : "Pearson χ²/df dekat 1 berarti asumsi ragam = rerata wajar untuk data ini."));

            if (h.Catatan is not null)
                blocks.Add(Blocks.Note(h.Catatan, NoteKind.Warning));

            return blocks;
        }
    }
}
