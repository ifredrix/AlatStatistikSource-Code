using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public enum ModelKurva
    {

        Eksponensial,

        Pangkat,
    }

    public static class KurvaNonlinear
    {
        public const int IterasiMaks = 300;
        public const int PercobaanLambdaMaks = 60;
        public const double LambdaAwal = 1e-3;
        public const double Toleransi = 1e-13;

        public static string Nama(ModelKurva m)
            => m == ModelKurva.Eksponensial ? "Eksponensial" : "Pangkat";

        public static string Bentuk(ModelKurva m)
            => m == ModelKurva.Eksponensial ? "y = a · e^(b·x)" : "y = a · x^b";

        public static string BentukBernama(ModelKurva m, string namaX, string namaY)
            => m == ModelKurva.Eksponensial
                ? $"{namaY} = a · e^(b·{namaX})"
                : $"{namaY} = a · {namaX}^b";

        public static string[] NamaParameter(ModelKurva m) => new[] { "a", "b" };

        public static double F(ModelKurva m, double x, double a, double b)
            => m == ModelKurva.Eksponensial
                ? a * Math.Exp(b * x)
                : a * Math.Pow(x, b);

        public static double[] Jacobi(ModelKurva m, double x, double a, double b)
        {
            if (m == ModelKurva.Eksponensial)
            {
                double e = Math.Exp(b * x);
                return new[] { e, a * x * e };
            }
            double p = Math.Pow(x, b);
            return new[] { p, a * p * Math.Log(x) };
        }

        public static double[] TebakanAwal(ModelKurva m, double[] x, double[] y)
        {
            if (m == ModelKurva.Eksponensial)
            {
                if (y.All(v => v > 0) && x.Length > 2)
                {
                    var (b0, c0) = RegresiSederhana(x, y.Select(v => Math.Log(v)).ToArray());
                    if (b0 > -50 && b0 < 50 && c0 > -50 && c0 < 50 && !double.IsNaN(b0))
                        return new[] { Math.Exp(c0), b0 };
                }
                double maks = y.Length > 0 ? y.Max() : 1.0;
                return new[] { maks > 0 ? maks : 1.0, 0.1 };
            }

            if (x.All(v => v > 0) && y.All(v => v > 0) && x.Length > 2)
            {
                var (b0, c0) = RegresiSederhana(x.Select(v => Math.Log(v)).ToArray(),
                                                y.Select(v => Math.Log(v)).ToArray());
                if (b0 > -50 && b0 < 50 && c0 > -50 && c0 < 50 && !double.IsNaN(b0))
                    return new[] { Math.Exp(c0), b0 };
            }
            return new[] { 1.0, 1.0 };
        }

        private static (double B, double C) RegresiSederhana(double[] x, double[] y)
        {
            int n = x.Length;
            double mx = x.Average(), my = y.Average();
            double sxy = 0, sxx = 0;
            for (int i = 0; i < n; i++) { sxy += (x[i] - mx) * (y[i] - my); sxx += (x[i] - mx) * (x[i] - mx); }
            if (sxx <= 0) return (double.NaN, double.NaN);
            double b = sxy / sxx;
            return (b, my - b * mx);
        }

        public sealed class Hasil
        {
            public ModelKurva Model;
            public string NamaModel = "";
            public string[] NamaParam = Array.Empty<string>();
            public double[] Param = Array.Empty<double>();
            public double[] Se = Array.Empty<double>();
            public double[] T = Array.Empty<double>();
            public double[] P = Array.Empty<double>();
            public double[] Tebakan = Array.Empty<double>();
            public int N, K, Iterasi;
            public double Sse, Sst, R2, R2Adj, Sigma2;
            public bool Konvergen;
            public string Catatan = "";

            public double[] GradienAkhir = Array.Empty<double>();
            public double GradienMaks;

            public double LangkahNewtonRel;
        }

        public static Hasil? Fit(double[] x, double[] y, ModelKurva model)
        {
            int n = x.Length;
            const int k = 2;
            if (n < k + 2 || y.Length != n) return null;
            if (model == ModelKurva.Pangkat && x.Any(v => v <= 0)) return null;
            if (y.Any(v => double.IsNaN(v)) || x.Any(v => double.IsNaN(v))) return null;

            var theta = TebakanAwal(model, x, y);
            double lambda = LambdaAwal;
            double sse = JumlahKuadrat(x, y, model, theta);

            var jac = new double[n][];
            for (int i = 0; i < n; i++) jac[i] = new double[k];

            int iterasi = 0;
            bool konvergen = false;
            string catatan = "";

            for (iterasi = 1; iterasi <= IterasiMaks; iterasi++)
            {
                double[] sisa = Sisa(x, y, model, theta);
                for (int i = 0; i < n; i++) jac[i] = Jacobi(model, x[i], theta[0], theta[1]);

                var jtj = new double[k, k];
                var jtr = new double[k];
                for (int i = 0; i < n; i++)
                    for (int a = 0; a < k; a++)
                    {
                        jtr[a] += jac[i][a] * sisa[i];
                        for (int b = 0; b < k; b++) jtj[a, b] += jac[i][a] * jac[i][b];
                    }

                bool membaik = false;
                double[] delta = new double[k];

                for (int coba = 1; coba <= PercobaanLambdaMaks; coba++)
                {
                    var a = (double[,])jtj.Clone();
                    for (int d = 0; d < k; d++)
                        a[d, d] += lambda * (Math.Abs(jtj[d, d]) > 0 ? Math.Abs(jtj[d, d]) : 1.0);

                    var kebalikanCoba = Regression.Inverse(a);
                    if (kebalikanCoba is null) { lambda *= 10; continue; }

                    var usul = new double[k];
                    for (int d = 0; d < k; d++)
                    {
                        double s = 0;
                        for (int e = 0; e < k; e++) s += kebalikanCoba[d, e] * jtr[e];
                        usul[d] = s;
                    }

                    var thetaBaru = new[] { theta[0] + usul[0], theta[1] + usul[1] };
                    double sseBaru = JumlahKuadrat(x, y, model, thetaBaru);

                    if (sseBaru < sse)
                    {
                        theta = thetaBaru;
                        sse = sseBaru;
                        delta = usul;
                        lambda = Math.Max(lambda / 10.0, 1e-15);
                        membaik = true;
                        break;
                    }
                    lambda *= 10.0;
                }

                if (!membaik)
                {
                    konvergen = true;
                    catatan = $"Berhenti di iterasi {iterasi}: tidak ada λ yang memperkecil SSE lagi "
                              + "(sudah di titik minimum menurut presisi mesin).";
                    break;
                }

                double maks = Math.Max(Math.Abs(delta[0]), Math.Abs(delta[1]));
                if (maks < Toleransi)
                {
                    konvergen = true;
                    catatan = $"Konvergen di iterasi {iterasi}: perubahan parameter < {Toleransi:G0}.";
                    break;
                }
            }

            if (!konvergen && iterasi > IterasiMaks)
                catatan = $"Berhenti karena mencapai batas {IterasiMaks} iterasi tanpa konvergen.";

            
            double[] sisaAkhir = Sisa(x, y, model, theta);
            var jtrAkhir = new double[k];
            for (int i = 0; i < n; i++)
            {
                var jj = Jacobi(model, x[i], theta[0], theta[1]);
                for (int a = 0; a < k; a++) jtrAkhir[a] += jj[a] * sisaAkhir[i];
            }
            double gradMaks = jtrAkhir.Max(Math.Abs);

            double sigma2 = sse / (n - k);
            double[,] inv;
            {
                var jtj = new double[k, k];
                for (int i = 0; i < n; i++)
                {
                    var jj = Jacobi(model, x[i], theta[0], theta[1]);
                    for (int a = 0; a < k; a++)
                        for (int b = 0; b < k; b++) jtj[a, b] += jj[a] * jj[b];
                }
                var kebalikan = Regression.Inverse((double[,])jtj.Clone());
                if (kebalikan is null) return null;
                inv = kebalikan;
            }

            var cov = new double[k, k];
            for (int a = 0; a < k; a++)
                for (int b = 0; b < k; b++) cov[a, b] = sigma2 * inv[a, b];

            
            double langkahRel = 0;
            for (int d = 0; d < k; d++)
            {
                double s = 0;
                for (int e = 0; e < k; e++) s += inv[d, e] * jtrAkhir[e];
                langkahRel = Math.Max(langkahRel,
                    Math.Abs(s) / Math.Max(1.0, Math.Abs(theta[d])));
            }

            var se = new double[k];
            var t = new double[k];
            var p = new double[k];
            for (int a = 0; a < k; a++)
            {
                se[a] = Math.Sqrt(Math.Max(0, cov[a, a]));
                t[a] = se[a] > 0 ? theta[a] / se[a] : double.NaN;
                p[a] = Distributions.StudentTTwoSided(t[a], n - k);
            }

            double rataY = y.Average();
            double sst = 0;
            for (int i = 0; i < n; i++) { double d = y[i] - rataY; sst += d * d; }
            double r2 = sst > 0 ? 1 - sse / sst : double.NaN;

            return new Hasil
            {
                Model = model, NamaModel = Nama(model), NamaParam = NamaParameter(model),
                Param = theta, Se = se, T = t, P = p, Tebakan = TebakanAwal(model, x, y),
                N = n, K = k, Iterasi = iterasi,
                Sse = sse, Sst = sst, R2 = r2,
                R2Adj = 1 - (1 - r2) * (n - 1) / (n - k),
                Sigma2 = sigma2, Konvergen = konvergen, Catatan = catatan,
                GradienAkhir = jtrAkhir, GradienMaks = gradMaks,
                LangkahNewtonRel = langkahRel,
            };
        }

        private static double JumlahKuadrat(double[] x, double[] y, ModelKurva m, double[] th)
        {
            double s = 0;
            for (int i = 0; i < x.Length; i++)
            {
                double e = y[i] - F(m, x[i], th[0], th[1]);
                if (double.IsNaN(e) || double.IsInfinity(e)) return double.PositiveInfinity;
                s += e * e;
            }
            return s;
        }

        private static double[] Sisa(double[] x, double[] y, ModelKurva m, double[] th)
        {
            var r = new double[x.Length];
            for (int i = 0; i < x.Length; i++) r[i] = y[i] - F(m, x[i], th[0], th[1]);
            return r;
        }

        

        public static List<ResultBlock> KurvaBlocks(Dataset ds, string varX, string varY, ModelKurva model)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading($"Regresi non-linear — kurva {Nama(model).ToLowerInvariant()}", 1)
            };

            var baris = ds.CompleteRows(new[] { varX, varY });
            if (baris.Count < 6)
            {
                blocks.Add(Blocks.Note(
                    $"Baris lengkap hanya {baris.Count}. Kurva dua parameter butuh minimal "
                    + "6 baris supaya derajat bebasnya tersisa.", NoteKind.Error));
                return blocks;
            }

            var semuaX = ds.Numeric(varX);
            var semuaY = ds.Numeric(varY);
            var x = new double[baris.Count];
            var y = new double[baris.Count];
            for (int i = 0; i < baris.Count; i++)
            {
                x[i] = semuaX[baris[i]] ?? double.NaN;
                y[i] = semuaY[baris[i]] ?? double.NaN;
            }

            if (model == ModelKurva.Pangkat && x.Any(v => v <= 0))
            {
                blocks.Add(Blocks.Note(
                    $"Kurva pangkat membutuhkan {varX} > 0 (ada akar dan logaritma di dalamnya), "
                    + $"tetapi ada {x.Count(v => v <= 0)} nilai yang nol atau negatif. "
                    + "Pilih model eksponensial, atau periksa variabelnya.", NoteKind.Error));
                return blocks;
            }

            var h = Fit(x, y, model);
            if (h is null)
            {
                blocks.Add(Blocks.Note(
                    "Kurva tidak bisa disesuaikan. Periksa apakah variabelnya beragam "
                    + "dan tidak ada nilai yang hilang.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.KurvaNonlinear, DaftarRumus.KurvaGalatBaku));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Sumbu X", varX),
                ("Sumbu Y", varY),
                ("Bentuk kurva", Bentuk(model)),
                ("Banyak baris lengkap", $"n = {Fmt.Int(h.N)}"),
                ("Tebakan awal (a, b)",
                 $"({Fmt.Num(h.Tebakan[0], 6)}, {Fmt.Num(h.Tebakan[1], 6)})"),
                ("Iterasi sampai berhenti", $"{Fmt.Int(h.Iterasi)} ({(h.Konvergen ? "konvergen" : "TIDAK konvergen")})")));

            blocks.Add(Blocks.Table(
                "Taksiran parameter",
                new[] { "Parameter", "Taksiran", "Galat baku", "t", "p" },
                Enumerable.Range(0, h.K).Select(j => new[]
                {
                    h.NamaParam[j], Fmt.Num(h.Param[j], 6), Fmt.Num(h.Se[j], 6),
                    Fmt.Num(h.T[j], 4), Fmt.P(h.P[j]),
                }).ToArray(),
                "Galat baku memakai Cov = s²·(J′J)⁻¹, dengan J matriks turunan model "
                + "terhadap parameternya. Bentuknya sama dengan regresi linear — "
                + "hanya X digantikan J."));

            blocks.Add(Blocks.Table(
                "Kesesuaian model",
                new[] { "Besaran", "Nilai" },
                new[]
                {
                    new[] { "Bentuk kurva", BentukBernama(model, varX, varY) },
                    new[] { "Jumlah kuadrat sisa (SSE)", Fmt.Num(h.Sse, 6) },
                    new[] { "Derajat bebas sisa", Fmt.Int(h.N - h.K) },
                    new[] { "R²", Fmt.Num(h.R2, 6) },
                    new[] { "R² disesuaikan", Fmt.Num(h.R2Adj, 6) },
                    new[] { "Galat baku taksiran", Fmt.Num(Math.Sqrt(h.Sigma2), 6) },
                },
                "R² kurva non-linear tetap dihitung sebagai 1 − SSE/SST seperti pada "
                + "regresi linear, supaya bisa dibandingkan langsung."));

            blocks.Add(Blocks.Note(
                "**Hati-hati menafsirkan.** Kurva non-linear bisa menyesuaikan diri dengan "
                + "hampir semua data; R² tinggi bukan bukti bahwa bentuk kurvanya benar. "
                + "Yang menguji bentuknya adalah sisa yang tersebar acak — bukan R². "
                + "Dan ramalan di luar rentang data jauh lebih berbahaya di sini daripada "
                + "pada garis lurus, sebab kurva eksponensial melesat cepat.",
                NoteKind.Warning));

            if (!h.Konvergen)
                blocks.Add(Blocks.Note(h.Catatan, NoteKind.Warning));
            else if (h.Catatan.Length > 0)
                blocks.Add(Blocks.Note(h.Catatan, NoteKind.Info));

            return blocks;
        }
    }
}
