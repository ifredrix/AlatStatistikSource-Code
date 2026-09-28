using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class Cox
    {
        private const int IterasiMaks = 100;
        private const double Toleransi = 1e-12;

        public sealed class Hasil
        {
            public List<string> Nama = new();
            public double[] B = Array.Empty<double>();
            public double[] Se = Array.Empty<double>();
            public double[] Z = Array.Empty<double>();
            public double[] P = Array.Empty<double>();
            public double[] Hr = Array.Empty<double>();
            public double[] HrBawah = Array.Empty<double>();
            public double[] HrAtas = Array.Empty<double>();
            public int N, Kejadian, Df, Iterasi;
            public double LogLik, LogLikNol, LrChi2, LrP, Aic;
            public bool Konvergen;
            public double[] Waktu = Array.Empty<double>();
            public double[] H0 = Array.Empty<double>();
            public double[] S0 = Array.Empty<double>();
            public double MedianDasar = double.NaN;
        }

        public static Hasil? Fit(double[] waktu, int[] status,
                                 List<double[]> pred, List<string> namaPred)
        {
            int n = waktu.Length;
            if (status.Length != n || pred.Any(v => v.Length != n)) return null;
            int p = pred.Count;
            if (p < 0 || n < p + 3) return null;
            if (status.Sum() < 5) return null;

            var urut = Enumerable.Range(0, n).OrderBy(i => waktu[i]).ToArray();
            var w = urut.Select(i => waktu[i]).ToArray();
            var s = urut.Select(i => status[i]).ToArray();
            var xc = pred.Select(v => urut.Select(i => v[i]).ToArray()).ToList();

            var beta = new double[p];
            var inv = new double[p, p];
            int iterasi = 0;
            bool konvergen = p == 0;
            for (iterasi = 1; p > 0 && iterasi <= IterasiMaks; iterasi++)
            {
                var eta = new double[n];
                double maks = double.NegativeInfinity;
                for (int i = 0; i < n; i++)
                {
                    double e = 0;
                    for (int a = 0; a < p; a++) e += xc[a][i] * beta[a];
                    eta[i] = e;
                    if (e > maks) maks = e;
                }
                var skor = new double[p];
                var info = new double[p, p];
                double s0 = 0;
                var s1 = new double[p];
                var s2 = new double[p, p];
                int ujung = n;
                int i0 = n - 1;
                while (i0 >= 0)
                {
                    double tau = w[i0];
                    int awal = i0;
                    while (awal >= 0 && w[awal] == tau) awal--;
                    for (int i = awal + 1; i < ujung; i++)
                    {
                        double e = Math.Exp(eta[i] - maks);
                        s0 += e;
                        for (int a = 0; a < p; a++)
                        {
                            s1[a] += e * xc[a][i];
                            for (int b = 0; b < p; b++) s2[a, b] += e * xc[a][i] * xc[b][i];
                        }
                    }
                    int d = 0;
                    var xe = new double[p];
                    for (int i = awal + 1; i <= i0; i++)
                        if (s[i] == 1)
                        {
                            d++;
                            for (int a = 0; a < p; a++) xe[a] += xc[a][i];
                        }
                    if (d > 0 && s0 > 0)
                    {
                        for (int a = 0; a < p; a++)
                        {
                            skor[a] += xe[a] - d * s1[a] / s0;
                            for (int b = 0; b < p; b++)
                                info[a, b] += d * (s2[a, b] / s0
                                                   - s1[a] * s1[b] / (s0 * s0));
                        }
                    }
                    ujung = awal + 1;
                    i0 = awal;
                }
                var invBaru = Regression.Inverse((double[,])info.Clone());
                if (invBaru is null) return null;
                var delta = new double[p];
                double beda = 0;
                for (int a = 0; a < p; a++)
                {
                    double t = 0;
                    for (int b = 0; b < p; b++) t += invBaru[a, b] * skor[b];
                    delta[a] = t;
                    beda = Math.Max(beda, Math.Abs(t));
                    beta[a] += t;
                }
                inv = invBaru;
                if (beda < Toleransi) { konvergen = true; break; }
            }

            double ll = 0;
            {
                var eta = new double[n];
                double maks = double.NegativeInfinity;
                for (int i = 0; i < n; i++)
                {
                    double e = 0;
                    for (int a = 0; a < p; a++) e += xc[a][i] * beta[a];
                    eta[i] = e;
                    if (e > maks) maks = e;
                }
                double s0 = 0;
                int ujung = n;
                int i0 = n - 1;
                while (i0 >= 0)
                {
                    double tau = w[i0];
                    int awal = i0;
                    while (awal >= 0 && w[awal] == tau) awal--;
                    for (int i = awal + 1; i < ujung; i++)
                        s0 += Math.Exp(eta[i] - maks);
                    int d = 0;
                    double jumlahEta = 0;
                    for (int i = awal + 1; i <= i0; i++)
                        if (s[i] == 1) { d++; jumlahEta += eta[i]; }
                    if (d > 0)
                        ll += jumlahEta - d * (Math.Log(s0) + maks);
                    ujung = awal + 1;
                    i0 = awal;
                }
            }

            double llNol = 0;
            {
                int ujung = n;
                int i0 = n - 1;
                while (i0 >= 0)
                {
                    double tau = w[i0];
                    int awal = i0;
                    while (awal >= 0 && w[awal] == tau) awal--;
                    int d = 0;
                    for (int i = awal + 1; i <= i0; i++)
                        if (s[i] == 1) d++;
                    if (d > 0) llNol -= d * Math.Log(n - (awal + 1));
                    ujung = awal + 1;
                    i0 = awal;
                }
            }

            var wakt = new List<double>();
            var h0 = new List<double>();
            {
                var eta = new double[n];
                double maks = double.NegativeInfinity;
                for (int i = 0; i < n; i++)
                {
                    double e = 0;
                    for (int a = 0; a < p; a++) e += xc[a][i] * beta[a];
                    eta[i] = e;
                    if (e > maks) maks = e;
                }
                double s0 = 0;
                int ujung = n;
                int i0 = n - 1;
                while (i0 >= 0)
                {
                    double tau = w[i0];
                    int awal = i0;
                    while (awal >= 0 && w[awal] == tau) awal--;
                    for (int i = awal + 1; i < ujung; i++)
                        s0 += Math.Exp(eta[i] - maks);
                    int d = 0;
                    for (int i = awal + 1; i <= i0; i++)
                        if (s[i] == 1) d++;
                    if (d > 0)
                    {
                        wakt.Add(tau);
                        h0.Add(d / (s0 * Math.Exp(maks)));
                    }
                    ujung = awal + 1;
                    i0 = awal;
                }
                wakt.Reverse();
                h0.Reverse();
            }
            var H0 = new double[wakt.Count];
            var S0 = new double[wakt.Count];
            double kum = 0;
            double median = double.NaN;
            for (int j = 0; j < wakt.Count; j++)
            {
                kum += h0[j];
                H0[j] = kum;
                S0[j] = Math.Exp(-kum);
                if (double.IsNaN(median) && S0[j] <= 0.5) median = wakt[j];
            }

            var nama = new List<string>(namaPred);
            var se = new double[p];
            var zz = new double[p];
            var pv = new double[p];
            var hr = new double[p];
            var hb = new double[p];
            var ha = new double[p];
            for (int a = 0; a < p; a++)
            {
                se[a] = Math.Sqrt(Math.Max(0, inv[a, a]));
                zz[a] = se[a] > 0 ? beta[a] / se[a] : double.NaN;
                pv[a] = 2 * Distributions.NormalCdf(-Math.Abs(zz[a]));
                hr[a] = Math.Exp(beta[a]);
                hb[a] = Math.Exp(beta[a] - 1.96 * se[a]);
                ha[a] = Math.Exp(beta[a] + 1.96 * se[a]);
            }

            double lr = 2 * (ll - llNol);
            return new Hasil
            {
                Nama = nama, B = (double[])beta.Clone(), Se = se, Z = zz, P = pv,
                Hr = hr, HrBawah = hb, HrAtas = ha,
                N = n, Kejadian = status.Sum(), Df = p, Iterasi = iterasi,
                LogLik = ll, LogLikNol = llNol,
                LrChi2 = lr, LrP = Distributions.ChiSquareUpper(lr, p),
                Aic = -2 * ll + 2 * p, Konvergen = konvergen,
                Waktu = wakt.ToArray(), H0 = H0, S0 = S0, MedianDasar = median,
            };
        }

        public static double[] NelsonAalen(double[] waktu, int[] status)
        {
            int n = waktu.Length;
            var urut = Enumerable.Range(0, n).OrderBy(i => waktu[i]).ToArray();
            var w = urut.Select(i => waktu[i]).ToArray();
            var s = urut.Select(i => status[i]).ToArray();
            var keluar = new List<double>();
            double kum = 0;
            int i0 = 0;
            while (i0 < n)
            {
                double tau = w[i0];
                int awal = i0;
                while (awal < n && w[awal] == tau) awal++;
                int d = 0;
                for (int i = i0; i < awal; i++)
                    if (s[i] == 1) d++;
                int r = n - i0;
                if (d > 0) { kum += (double)d / r; keluar.Add(kum); }
                i0 = awal;
            }
            return keluar.ToArray();
        }

        public static List<ResultBlock> CoxBlocks(Dataset ds, string waktu, string status,
            List<string> bebas, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Regresi Cox proportional hazards", 1)
            };

            var perlu = new List<string> { waktu, status };
            perlu.AddRange(bebas);
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < bebas.Count + 6)
            {
                blocks.Add(Blocks.Note(
                    "Baris lengkap kurang. Model Cox tidak bisa dijalankan.",
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

            var t = AmbilN(waktu);
            var st = AmbilN(status);
            if (t.Any(double.IsNaN) || st.Any(double.IsNaN))
            {
                blocks.Add(Blocks.Note(
                    "Waktu atau status punya nilai hilang di baris lengkap.",
                    NoteKind.Error));
                return blocks;
            }
            var stInt = st.Select(v => (int)Math.Round(v)).ToArray();
            if (stInt.Any(v => v != 0 && v != 1))
            {
                blocks.Add(Blocks.Note(
                    $"Status '{status}' harus 0 (tersensor) atau 1 (kejadian).",
                    NoteKind.Error));
                return blocks;
            }

            var pred = bebas.Select(AmbilN).ToList();
            if (pred.Any(v => v.Any(double.IsNaN)))
            {
                blocks.Add(Blocks.Note("Prediktor punya nilai hilang.", NoteKind.Error));
                return blocks;
            }

            var h = Fit(t, stInt, pred, bebas);
            if (h is null)
            {
                blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.CoxModel, DaftarRumus.CoxBreslow));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Waktu", waktu),
                ("Status (1 = kejadian)", status),
                ("Kovariat", string.Join(", ", bebas)),
                ("Banyak baris (kejadian)", $"n = {Fmt.Int(h.N)} ({Fmt.Int(h.Kejadian)} kejadian)"),
                ("Iterasi Newton", $"{Fmt.Int(h.Iterasi)} ({(h.Konvergen ? "konvergen" : "TIDAK konvergen")})")));

            blocks.Add(Blocks.Table(
                "Koefisien",
                new[] { "Kovariat", "B", "Galat baku", "Wald z", "p", "HR = exp(B)",
                        "SK 95% bawah", "SK 95% atas" },
                h.Nama.Select((nm, j) => new[]
                {
                    nm, Fmt.Num(h.B[j], 6), Fmt.Num(h.Se[j], 6),
                    Fmt.Num(h.Z[j], 4), Fmt.P(h.P[j]), Fmt.Num(h.Hr[j], 4),
                    Fmt.Num(h.HrBawah[j], 4), Fmt.Num(h.HrAtas[j], 4),
                }).ToArray(),
                "HR > 1 berarti laju kejadian lebih tinggi. Selang kepercayaan "
                + "dari exp(B ± 1,96·galat baku)."));

            blocks.Add(Blocks.Table(
                "Kesesuaian model",
                new[] { "Besaran", "Nilai" },
                new[]
                {
                    new[] { "ln L parsial", Fmt.Num(h.LogLik, 4) },
                    new[] { "ln L (model nol)", Fmt.Num(h.LogLikNol, 4) },
                    new[] { "Uji nisbah kemiripan: χ²", Fmt.Num(h.LrChi2, 4) },
                    new[] { "df", Fmt.Int(h.Df) },
                    new[] { "p (χ²)", Fmt.P(h.LrP) },
                    new[] { "AIC", Fmt.Num(h.Aic, 4) },
                },
                "Model nol = tanpa kovariat: ln L-nya tertutup, "
                + "−Σ d·ln R. Bila χ² tidak menolak, kovariat tidak menambah apa pun."));

            var idxT = Enumerable.Range(0, h.Waktu.Length).ToList();
            blocks.Add(Blocks.Table(
                "Fungsi dasar Breslow (x = 0)",
                new[] { "Waktu", "H0 kumulatif", "S0 (ketahanan)" },
                idxT.Select(j => new[]
                {
                    Fmt.Num(h.Waktu[j], 4), Fmt.Num(h.H0[j], 6), Fmt.Num(h.S0[j], 6),
                }).ToArray(),
                "Hazard dasar ditaksir Breslow: h0 = d/R tertimbang risiko. "
                + (double.IsNaN(h.MedianDasar)
                    ? "Median tak tercapai pada rentang amatan."
                    : $"Median ketahanan dasar = {Fmt.Num(h.MedianDasar, 4)}.")));

            if (!h.Konvergen)
                blocks.Add(Blocks.Note(
                    "Newton–Raphson tidak konvergen dalam batas iterasi. "
                    + "Angka di atas belum titik tetap — jangan dipakai.",
                    NoteKind.Warning));

            return blocks;
        }
    }
}
