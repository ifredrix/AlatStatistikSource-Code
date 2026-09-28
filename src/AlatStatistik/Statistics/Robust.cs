using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class Robust
    {
        public const double HuberT = 1.345;
        public const double TukeyC = 4.685;
        public const double MadKonstan = 0.6744897501960817;

        public sealed class Hasil
        {
            public string Norma = "";
            public int N;
            public List<string> NamaKoef = new();
            public double[] Koef = Array.Empty<double>();
            public double[] Bobot = Array.Empty<double>();
            public double[] Fitted = Array.Empty<double>();
            public double Skala;
            public int Iterasi;
        }

        public static double Median(double[] x)
        {
            int n = x.Length;
            if (n == 0) return double.NaN;
            var y = (double[])x.Clone();
            Array.Sort(y);
            return n % 2 == 1 ? y[n / 2] : 0.5 * (y[n / 2 - 1] + y[n / 2]);
        }

        public static double SkalaMad(double[] resid)
        {
            // statsmodels: mad(resid, center=0) — median(|r|)/c, BUKAN
            // median(|r − median(r)|)/c. Sama sampai orde-ulp untuk residu
            // simetris, tetapi titik-tetap IRLS-nya beda (terukur 9e-4)!
            var dev = new double[resid.Length];
            for (int i = 0; i < resid.Length; i++)
                dev[i] = Math.Abs(resid[i]);
            return Median(dev) / MadKonstan;
        }

        private static double BobotSatu(double u, bool huber)
        {
            double a = Math.Abs(u);
            if (huber)
                return a <= HuberT ? 1.0 : HuberT / Math.Max(a, 1e-300);
            if (a >= TukeyC) return 0.0;
            double t = 1 - (u / TukeyC) * (u / TukeyC);
            return t * t;
        }

        private static double[] SolveWls(double[][] xmat, double[] y, double[] bobot)
        {
            // WLS lokal: bobot NOL diizinkan (pencilan Tukey) — Glm.FitWls
            // menolaknya, jadi tak-dipakai di sini. Urutan jumlah sama.
            int n = y.Length, k = xmat[0].Length;
            var xtwx = new double[k, k];
            var xtwy = new double[k];
            for (int i = 0; i < n; i++)
                for (int a = 0; a < k; a++)
                {
                    xtwy[a] += xmat[i][a] * bobot[i] * y[i];
                    for (int b = 0; b < k; b++)
                        xtwx[a, b] += xmat[i][a] * bobot[i] * xmat[i][b];
                }
            var inv = Regression.Inverse((double[,])xtwx.Clone());
            if (inv is null) return Array.Empty<double>();
            var beta = new double[k];
            for (int a = 0; a < k; a++)
            {
                double s = 0;
                for (int b = 0; b < k; b++) s += inv[a, b] * xtwy[b];
                beta[a] = s;
            }
            return beta;
        }

        public static Hasil? Pasang(double[] y, List<double[]> pred,
                                    List<string> namaPred, bool huber)
        {
            int n = y.Length, p = pred.Count;
            if (n < 10 || p < 1) return null;
            if (pred.Any(v => v.Length != n)) return null;
            if (y.Any(double.IsNaN)) return null;
            if (pred.Any(c => c.Any(double.IsNaN))) return null;
            int k = p + 1;
            var x = new double[n][];
            for (int i = 0; i < n; i++)
            {
                x[i] = new double[k];
                x[i][0] = 1.0;
                for (int j = 0; j < p; j++) x[i][j + 1] = pred[j][i];
            }

            var satu = Enumerable.Repeat(1.0, n).ToArray();
            var beta = SolveWls(x, y, satu);
            if (beta.Length == 0) return null;

            var bobot = (double[])satu.Clone();
            double skala = 1.0;
            int iterasi = 0;
            for (iterasi = 1; iterasi <= 200; iterasi++)
            {
                var resid = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double eta = beta[0];
                    for (int j = 0; j < p; j++) eta += beta[j + 1] * pred[j][i];
                    resid[i] = y[i] - eta;
                }
                skala = SkalaMad(resid);
                if (!(skala > 1e-12)) return null;
                for (int i = 0; i < n; i++)
                    bobot[i] = BobotSatu(resid[i] / skala, huber);
                var bn = SolveWls(x, y, bobot);
                if (bn.Length == 0) return null;
                double beda = 0;
                for (int a = 0; a < k; a++)
                    beda = Math.Max(beda, Math.Abs(bn[a] - beta[a]));
                beta = bn;
                if (beda < 1e-10) break;
            }

            var fitted = new double[n];
            for (int i = 0; i < n; i++)
            {
                double eta = beta[0];
                for (int j = 0; j < p; j++) eta += beta[j + 1] * pred[j][i];
                fitted[i] = eta;
            }
            var nama = new List<string> { "const" };
            nama.AddRange(namaPred);
            return new Hasil
            {
                Norma = huber ? "Huber" : "Tukey",
                N = n, NamaKoef = nama, Koef = beta,
                Bobot = bobot, Fitted = fitted,
                Skala = skala, Iterasi = iterasi,
            };
        }

        public static List<ResultBlock> RobustBlocks(Dataset ds, string dependen,
            List<string> bebas, string norma)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Regresi robust (kebal pencilan)", 1)
            };
            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Robust));

            bool huber = !(norma.StartsWith("Tukey", StringComparison.OrdinalIgnoreCase)
                           || norma.StartsWith("Bisquare", StringComparison.OrdinalIgnoreCase)
                           || norma.StartsWith("Dwibobot", StringComparison.OrdinalIgnoreCase));
            var perlu = new List<string>(bebas) { dependen };
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < 10)
            {
                blocks.Add(Blocks.Note("Baris lengkap kurang dari 10.",
                                       NoteKind.Error));
                return blocks;
            }
            var sn = ds.Numeric(dependen);
            var y = baris.Select(i => sn[i] ?? double.NaN).ToArray();
            var pred = new List<double[]>();
            foreach (string v in bebas)
            {
                var semua = ds.Numeric(v);
                pred.Add(baris.Select(i => semua[i] ?? double.NaN).ToArray());
            }
            if (y.Any(double.IsNaN) || pred.Any(c => c.Any(double.IsNaN)))
            {
                blocks.Add(Blocks.Note("Ada nilai hilang.",
                                       NoteKind.Error));
                return blocks;
            }

            var h = Pasang(y, pred, bebas, huber);
            if (h is null)
            {
                blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.",
                                       NoteKind.Error));
                return blocks;
            }

            double bobotMin = h.Bobot.Min();
            int nKecil = h.Bobot.Count(w => w < 0.5);
            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Norma", huber ? $"Huber (t = {Fmt.Num(HuberT, 3)})"
                                : $"Tukey dwibobot (c = {Fmt.Num(TukeyC, 3)})"),
                ("Dependen", dependen),
                ("Prediktor", string.Join(", ", bebas)),
                ("Banyak baris", $"n = {Fmt.Int(h.N)}"),
                ("Skala MAD", Fmt.Num(h.Skala, 4)),
                ("Iterasi IRLS", Fmt.Int(h.Iterasi)),
                ("Bobot terkecil", Fmt.Num(bobotMin, 4)),
                ($"Amatan dibobot < 0,5", Fmt.Int(nKecil))));

            blocks.Add(Blocks.Table(
                "Koefisien (M-estimasi IRLS)",
                new[] { "Parameter", "Nilai" },
                h.NamaKoef.Select((nm, i) =>
                    new[] { nm, Fmt.Num(h.Koef[i], 6) }).ToArray(),
                "Bobot = ψ(r/s)/(r/s); skala = MAD/0,6745 tiap putaran. "
                + "Tukey menolak pencilan jauh (bobot nol); Huber hanya "
                + "membatasi pengaruhnya."));

            return blocks;
        }
    }
}
