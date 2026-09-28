using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class Gee
    {
        public sealed class Hasil
        {
            public string Keluarga = "";
            public string Korelasi = "";
            public int N;
            public int NGrup;
            public List<string> NamaKoef = new();
            public double[] Koef = Array.Empty<double>();
            public double[] SeRobust = Array.Empty<double>();
            public double[] SeNaive = Array.Empty<double>();
            public double Skala;
            public double Alpha = double.NaN;
            public double[] Fitted = Array.Empty<double>();
            public int Iterasi;
        }

        private static double[,] Kali(double[,] a, double[,] b)
        {
            int n = a.GetLength(0), m = a.GetLength(1), p = b.GetLength(1);
            var c = new double[n, p];
            for (int i = 0; i < n; i++)
                for (int k = 0; k < m; k++)
                {
                    double aik = a[i, k];
                    for (int j = 0; j < p; j++) c[i, j] += aik * b[k, j];
                }
            return c;
        }

        private static double[,] Transpos(double[,] a)
        {
            int n = a.GetLength(0), m = a.GetLength(1);
            var c = new double[m, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++) c[j, i] = a[i, j];
            return c;
        }

        private static double Logistik(double eta)
        {
            if (eta >= 0)
            {
                double e = Math.Exp(-eta);
                return 1.0 / (1.0 + e);
            }
            double e2 = Math.Exp(eta);
            return e2 / (1.0 + e2);
        }

        public static Hasil? Pasang(double[] y, List<double[]> pred,
                                    List<string> namaPred, int[] grup,
                                    bool binom, bool exchangeable)
        {
            int n = y.Length, p = pred.Count;
            if (n < 10 || p < 1 || grup.Length != n) return null;
            if (pred.Any(v => v.Length != n)) return null;
            if (y.Any(double.IsNaN) || pred.Any(c => c.Any(double.IsNaN))) return null;
            if (binom && y.Any(v => !(v == 0 || v == 1))) return null;
            var gvals = grup.Distinct().OrderBy(g => g).ToList();
            int G = gvals.Count;
            if (G < 2) return null;
            int k = p + 1;

            // Anggota per grup (urutan baris dipertahankan).
            var anggota = new List<int>[G];
            for (int g = 0; g < G; g++) anggota[g] = new List<int>();
            for (int i = 0; i < n; i++) anggota[grup[i]].Add(i);
            if (anggota.Any(a => a.Count < 2)) return null;
            int maxn = anggota.Max(a => a.Count);

            var beta = new double[k];
            if (!binom)
            {
                // Mulai OLS.
                var xtx = new double[k, k];
                var xty = new double[k];
                for (int i = 0; i < n; i++)
                {
                    double[] xi = new double[k];
                    xi[0] = 1.0;
                    for (int j = 0; j < p; j++) xi[j + 1] = pred[j][i];
                    for (int a = 0; a < k; a++)
                    {
                        xty[a] += xi[a] * y[i];
                        for (int b = 0; b < k; b++) xtx[a, b] += xi[a] * xi[b];
                    }
                }
                var inv0 = Regression.Inverse((double[,])xtx.Clone());
                if (inv0 is null) return null;
                for (int a = 0; a < k; a++)
                {
                    double s = 0;
                    for (int b = 0; b < k; b++) s += inv0[a, b] * xty[b];
                    beta[a] = s;
                }
            }

            double phi = 1.0, alpha = 0.0;
            int iterasi = 0;
            var mu = new double[n];
            for (iterasi = 1; iterasi <= 100; iterasi++)
            {
                for (int i = 0; i < n; i++)
                {
                    double eta = beta[0];
                    for (int j = 0; j < p; j++) eta += beta[j + 1] * pred[j][i];
                    mu[i] = binom ? Logistik(eta) : eta;
                }
                var e = new double[n];
                var vv = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double v = binom
                        ? Math.Max(mu[i] * (1 - mu[i]), 1e-12)
                        : 1.0;
                    vv[i] = v;
                    e[i] = (y[i] - mu[i]) / Math.Sqrt(v);
                }
                double se2 = 0;
                for (int i = 0; i < n; i++) se2 += e[i] * e[i];
                phi = se2 / (n - k);
                if (!(phi > 0)) return null;
                if (exchangeable)
                {
                    // Sama persis dengan Exchangeable.update statsmodels:
                    // alpha = [Σ_{t<s} e_te_s / φ] / (P − ddof), P = Σn(n−1)/2,
                    // ddof = banyak parameter. BUKAN dibagi P mentah
                    double atas2 = 0, pairs = 0;
                    foreach (var ag in anggota)
                    {
                        for (int a = 0; a < ag.Count; a++)
                            for (int b = a + 1; b < ag.Count; b++)
                                atas2 += e[ag[a]] * e[ag[b]];
                        pairs += ag.Count * (ag.Count - 1) / 2.0;
                    }
                    alpha = (atas2 / Math.Max(phi, 1e-300)) / Math.Max(pairs - k, 1.0);
                    double lo = -1.0 / (maxn - 1) + 1e-6;
                    alpha = Math.Max(lo, Math.Min(1 - 1e-6, alpha));
                }

                var m0 = new double[k, k];
                var sk = new double[k];
                foreach (var ag in anggota)
                {
                    int ni = ag.Count;
                    var Vi = new double[ni, ni];
                    for (int a = 0; a < ni; a++)
                        for (int b = 0; b < ni; b++)
                        {
                            double r = !exchangeable ? 0.0
                                : (a == b ? 0.0 : alpha);
                            double kab = a == b ? 1.0 : r;
                            Vi[a, b] = phi * Math.Sqrt(vv[ag[a]] * vv[ag[b]]) * kab;
                        }
                    var inv = Regression.Inverse((double[,])Vi.Clone());
                    if (inv is null) return null;
                    var Di = new double[ni, k];
                    var ri = new double[ni, 1];
                    for (int a = 0; a < ni; a++)
                    {
                        int gi = ag[a];
                        Di[a, 0] = vv[gi];
                        for (int j = 0; j < p; j++) Di[a, j + 1] = vv[gi] * pred[j][gi];
                        ri[a, 0] = y[gi] - mu[gi];
                    }
                    var Z = Kali(inv, Di);
                    var q = Kali(inv, ri);
                    var Dt = Transpos(Di);
                    var addM = Kali(Dt, Z);
                    var addS = Kali(Dt, q);
                    for (int a = 0; a < k; a++)
                    {
                        sk[a] += addS[a, 0];
                        for (int b = 0; b < k; b++) m0[a, b] += addM[a, b];
                    }
                }
                var invM = Regression.Inverse((double[,])m0.Clone());
                if (invM is null) return null;
                double beda = 0;
                for (int a = 0; a < k; a++)
                {
                    double s = 0;
                    for (int b = 0; b < k; b++) s += invM[a, b] * sk[b];
                    beda = Math.Max(beda, Math.Abs(s));
                    beta[a] += s;
                }
                if (beda < 1e-10) break;
            }

            // Kovariansi sandwich + naif pada taksiran akhir.
            for (int i = 0; i < n; i++)
            {
                double eta = beta[0];
                for (int j = 0; j < p; j++) eta += beta[j + 1] * pred[j][i];
                mu[i] = binom ? Logistik(eta) : eta;
            }
            var m0f = new double[k, k];
            var m1f = new double[k, k];
            foreach (var ag in anggota)
            {
                int ni = ag.Count;
                var Vi = new double[ni, ni];
                var vv2 = new double[ni];
                for (int a = 0; a < ni; a++)
                {
                    int gi = ag[a];
                    vv2[a] = binom ? Math.Max(mu[gi] * (1 - mu[gi]), 1e-12) : 1.0;
                }
                for (int a = 0; a < ni; a++)
                    for (int b = 0; b < ni; b++)
                    {
                        double r = !exchangeable ? 0.0 : (a == b ? 0.0 : alpha);
                        double kab = a == b ? 1.0 : r;
                        Vi[a, b] = phi * Math.Sqrt(vv2[a] * vv2[b]) * kab;
                    }
                var inv = Regression.Inverse((double[,])Vi.Clone());
                if (inv is null) return null;
                var Di = new double[ni, k];
                var ri = new double[ni, 1];
                for (int a = 0; a < ni; a++)
                {
                    int gi = ag[a];
                    Di[a, 0] = vv2[a];
                    for (int j = 0; j < p; j++) Di[a, j + 1] = vv2[a] * pred[j][gi];
                    ri[a, 0] = y[gi] - mu[gi];
                }
                var Z = Kali(inv, Di);
                var Dt = Transpos(Di);
                var addM = Kali(Dt, Z);
                for (int a = 0; a < k; a++)
                    for (int b = 0; b < k; b++) m0f[a, b] += addM[a, b];
                var Ci = Kali(ri, Transpos(ri));
                var W = Kali(Ci, Z);
                var addM1 = Kali(Transpos(Z), W);
                for (int a = 0; a < k; a++)
                    for (int b = 0; b < k; b++) m1f[a, b] += addM1[a, b];
            }
            var invM0 = Regression.Inverse((double[,])m0f.Clone());
            if (invM0 is null) return null;
            var tengah = Kali(m1f, invM0);
            var robust = Kali(invM0, tengah);
            var seR = new double[k];
            var seN = new double[k];
            // Naif statsmodels = bmati·skala dengan bmati dari V TANPA φ:
            // Gauss: skala = φ Pearson → naif = M0⁻¹ (V ber-φ) apa adanya;
            // Binomial: skala final = 1,0 (Pearson hanya untuk update
            double bagiNaif = binom ? Math.Max(phi, 1e-300) : 1.0;
            for (int a = 0; a < k; a++)
            {
                seR[a] = Math.Sqrt(Math.Max(0, robust[a, a]));
                seN[a] = Math.Sqrt(Math.Max(0, invM0[a, a] / bagiNaif));
            }

            var nama = new List<string> { "Intercept" };
            for (int j = 0; j < p && j < namaPred.Count; j++) nama.Add(namaPred[j]);
            for (int j = nama.Count; j <= p; j++) nama.Add($"x{j}");
            // Skala FINAL: Binomial selalu 1,0 (estimate_scale statsmodels);
            // Pearson hanya dipakai di dalam iterasi (alpha).
            double skalaAkhir = binom ? 1.0 : phi;
            return new Hasil
            {
                Keluarga = binom ? "Binomial" : "Gauss",
                Korelasi = exchangeable ? "Exc" : "Ind",
                N = n, NGrup = G,
                NamaKoef = nama.Take(k).ToList(), Koef = beta,
                SeRobust = seR, SeNaive = seN,
                Skala = skalaAkhir, Alpha = exchangeable ? alpha : double.NaN,
                Fitted = (double[])mu.Clone(), Iterasi = iterasi,
            };
        }

        public static List<ResultBlock> GeeBlocks(Dataset ds, string dependen,
            List<string> bebas, string grup, string keluarga, string korelasi)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("GEE (data longitudinal / klaster)", 1)
            };
            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Gee));

            bool binom = keluarga.StartsWith("Binom", StringComparison.OrdinalIgnoreCase);
            bool exc = !korelasi.StartsWith("Ind", StringComparison.OrdinalIgnoreCase);
            var perlu = new List<string>(bebas) { dependen, grup };
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < 10)
            {
                blocks.Add(Blocks.Note("Baris lengkap kurang dari 10.",
                                       NoteKind.Error));
                return blocks;
            }
            if (ds.IndexOf(grup) < 0)
            {
                blocks.Add(Blocks.Note($"Variabel grup '{grup}' tidak ada.",
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
            var tk = ds.Text(grup);
            var lv = baris.Select(i => tk[i] ?? "").Distinct().ToList();
            var gi = baris.Select(i => lv.IndexOf(tk[i] ?? "")).ToArray();
            if (y.Any(double.IsNaN) || pred.Any(c => c.Any(double.IsNaN)))
            {
                blocks.Add(Blocks.Note("Ada nilai hilang.",
                                       NoteKind.Error));
                return blocks;
            }

            var h = Pasang(y, pred, bebas, gi, binom, exc);
            if (h is null)
            {
                blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.",
                                       NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Keluarga", binom ? "Binomial (logit)" : "Gauss (identitas)"),
                ("Korelasi kerja", exc ? "Exchangeable" : "Independence"),
                ("Dependen", dependen),
                ("Prediktor", string.Join(", ", bebas)),
                ("Grup", $"{grup} ({h.NGrup} grup, n = {h.N})"),
                ("Skala (Pearson)", Fmt.Num(h.Skala, 4)),
                ("Alpha", exc ? Fmt.Num(h.Alpha, 4) : "—"),
                ("Iterasi", Fmt.Int(h.Iterasi))));

            blocks.Add(Blocks.Table(
                "Koefisien GEE (sandwich + naif)",
                new[] { "Parameter", "Nilai", "SE sandwich", "SE naif" },
                h.NamaKoef.Select((nm, i) => new[]
                {
                    nm, Fmt.Num(h.Koef[i], 6),
                    Fmt.Num(h.SeRobust[i], 6), Fmt.Num(h.SeNaive[i], 6),
                }).ToArray(),
                "Sandwich kebal terhadap salah-spesifikasi korelasi; naif "
                + "mengasumsikan korelasi-kerja benar."));

            return blocks;
        }
    }
}
