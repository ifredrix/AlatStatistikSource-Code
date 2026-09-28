using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class Klasifikasi
    {
        public enum Metode { Knn, Lda, Qda }

        public sealed class Hasil
        {
            public List<string> Kelas = new();
            public List<string> Fitur = new();
            public int NLatih;
            public string[] DugaLatih = Array.Empty<string>();
            public double[][] ProbaLatih = Array.Empty<double[]>();
            public int[,] Bingung = new int[0, 0];
            public double Akurasi;
            public string[] DugaLoo = Array.Empty<string>();
            public double AkurasiLoo;
            public int K;
            public double[][]? Rerata;
            public double[][,] Covs = Array.Empty<double[,]>();
            public double[] Prior = Array.Empty<double>();
        }

        private static double Jarak2(double[] a, double[] b)
        {
            double s = 0;
            for (int j = 0; j < a.Length; j++) { double d = a[j] - b[j]; s += d * d; }
            return s;
        }

        private static string[] KnnDuga(double[][] xLatih, int[] yLatih,
                                        double[][] xUji, int k, int nKelas)
        {
            int n = xLatih.Length;
            var keluar = new string[xUji.Length];
            for (int i = 0; i < xUji.Length; i++)
            {
                var jarak = new List<(double, int)>();
                for (int j = 0; j < n; j++) jarak.Add((Jarak2(xUji[i], xLatih[j]), yLatih[j]));
                jarak.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                var suara = new int[nKelas];
                for (int t = 0; t < Math.Min(k, n); t++) suara[jarak[t].Item2]++;
                int menang = 0;
                for (int c = 1; c < nKelas; c++)
                    if (suara[c] > suara[menang]) menang = c;
                keluar[i] = menang.ToString();
            }
            return keluar;
        }

        private static double[,] KovTergabung(double[][] x, int[] y, int[] kelasIdx,
                                              double[][] rerata, out int df)
        {
            // Versi kemungkinan-maksimum (bagi n), mengikuti sklearn:
            // LinearDiscriminantAnalysis.covariance_ = S/n. Bukan versi
            // tak-bias S/(n−G) ala Fisher [Fis36] — bedanya hanya skala
            int n = x.Length, p = x[0].Length;
            var S = new double[p, p];
            df = n - kelasIdx.Length;
            foreach (int c in kelasIdx)
            {
                var idx = new List<int>();
                for (int i = 0; i < n; i++) if (y[i] == c) idx.Add(i);
                foreach (int i in idx)
                    for (int a = 0; a < p; a++)
                        for (int b = 0; b < p; b++)
                            S[a, b] += (x[i][a] - rerata[c][a]) * (x[i][b] - rerata[c][b]);
            }
            for (int a = 0; a < p; a++)
                for (int b = 0; b < p; b++) S[a, b] /= n;
            return S;
        }

        private static double[] SkorGauss(double[] x, double[] mu, double[,]? inv,
                                          double logDet, double logPrior)
        {
            int p = x.Length;
            var d = new double[p];
            for (int a = 0; a < p; a++) d[a] = x[a] - mu[a];
            var w = new double[p];
            for (int a = 0; a < p; a++)
            {
                double s = 0;
                for (int b = 0; b < p; b++) s += inv![a, b] * d[b];
                w[a] = s;
            }
            double q = 0;
            for (int a = 0; a < p; a++) q += d[a] * w[a];
            return new[] { -0.5 * q - 0.5 * logDet + logPrior };
        }

        private static double LogDetDariInv(double[,] inv, int p)
        {
            var asal = Regression.Inverse((double[,])inv.Clone());
            if (asal is null) return double.NaN;
            double det = Aljabar.Determinan(asal);
            return double.IsNaN(det) || det <= 0 ? double.NaN : Math.Log(det);
        }

        public static Hasil? Pasang(double[][] x, string[] y, List<string> fitur,
                                    Metode metode, int k)
        {
            int n = x.Length;
            if (n < 6 || y.Length != n) return null;
            var kelas = y.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            int G = kelas.Count;
            if (G < 2) return null;
            int p = x[0].Length;
            var yi = y.Select(t => kelas.IndexOf(t)).ToArray();
            foreach (int c in Enumerable.Range(0, G))
                if (yi.Count(v => v == c) < 2) return null;

            var h = new Hasil
            {
                Kelas = kelas, Fitur = new List<string>(fitur),
                NLatih = n, K = k,
            };

            if (metode == Metode.Knn)
            {
                if (k < 1) return null;
                var duga = new string[n];
                for (int i = 0; i < n; i++)
                {
                    var jarak = new List<(double, int)>();
                    for (int j = 0; j < n; j++)
                        if (j != i) jarak.Add((Jarak2(x[i], x[j]), yi[j]));
                    jarak.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                    var suara = new int[G];
                    for (int t = 0; t < Math.Min(k, n - 1); t++) suara[jarak[t].Item2]++;
                    int menang = 0;
                    for (int c = 1; c < G; c++)
                        if (suara[c] > suara[menang]) menang = c;
                    duga[i] = kelas[menang];
                }
                var penuh = KnnDuga(x, yi, x, k, G).Select(v => kelas[int.Parse(v)]).ToArray();
                h.DugaLatih = penuh;
                h.ProbaLatih = ProbaKnn(x, yi, x, k, G);
                h.DugaLoo = duga;
            }
            else
            {
                var rerata = new double[G][];
                var cacah = new int[G];
                for (int c = 0; c < G; c++)
                {
                    rerata[c] = new double[p];
                    foreach (int i in Enumerable.Range(0, n).Where(i => yi[i] == c))
                    {
                        cacah[c]++;
                        for (int a = 0; a < p; a++) rerata[c][a] += x[i][a];
                    }
                    for (int a = 0; a < p; a++) rerata[c][a] /= cacah[c];
                }
                var prior = cacah.Select(c => (double)c / n).ToArray();
                double[][,] covs;
                if (metode == Metode.Lda)
                {
                    var pooled = KovTergabung(x, yi, Enumerable.Range(0, G).ToArray(),
                                              rerata, out _);
                    covs = Enumerable.Range(0, G).Select(_ => pooled).ToArray();
                }
                else
                {
                    covs = new double[G][,];
                    for (int c = 0; c < G; c++)
                    {
                        var S = new double[p, p];
                        foreach (int i in Enumerable.Range(0, n).Where(i => yi[i] == c))
                            for (int a = 0; a < p; a++)
                                for (int b = 0; b < p; b++)
                                    S[a, b] += (x[i][a] - rerata[c][a])
                                               * (x[i][b] - rerata[c][b]);
                        for (int a = 0; a < p; a++)
                            for (int b = 0; b < p; b++) S[a, b] /= cacah[c];
                        covs[c] = S;
                    }
                }
                h.Rerata = rerata;
                h.Covs = covs;
                h.Prior = prior;

                var invs = new double[G][,];
                var logDets = new double[G];
                for (int c = 0; c < G; c++)
                {
                    var inv = Regression.Inverse((double[,])covs[c].Clone());
                    if (inv is null) return null;
                    invs[c] = inv;
                    logDets[c] = LogDetDariInv(inv, p);
                    if (double.IsNaN(logDets[c])) return null;
                }

                string[] DugaSkor(double[][] xx)
                {
                    var keluar = new string[xx.Length];
                    for (int i = 0; i < xx.Length; i++)
                    {
                        var sk = new double[G];
                        for (int c = 0; c < G; c++)
                            sk[c] = SkorGauss(xx[i], rerata[c], invs[c],
                                              logDets[c], Math.Log(prior[c]))[0];
                        int menang = 0;
                        for (int c = 1; c < G; c++)
                            if (sk[c] > sk[menang]) menang = c;
                        keluar[i] = kelas[menang];
                    }
                    return keluar;
                }

                double[][] ProbaSkor(double[][] xx)
                {
                    var keluar = new double[xx.Length][];
                    for (int i = 0; i < xx.Length; i++)
                    {
                        var sk = new double[G];
                        double maks = double.NegativeInfinity;
                        for (int c = 0; c < G; c++)
                        {
                            sk[c] = SkorGauss(xx[i], rerata[c], invs[c],
                                              logDets[c], Math.Log(prior[c]))[0];
                            if (sk[c] > maks) maks = sk[c];
                        }
                        double jumlah = 0;
                        keluar[i] = new double[G];
                        for (int c = 0; c < G; c++)
                        {
                            keluar[i][c] = Math.Exp(sk[c] - maks);
                            jumlah += keluar[i][c];
                        }
                        for (int c = 0; c < G; c++) keluar[i][c] /= jumlah;
                    }
                    return keluar;
                }

                h.DugaLatih = DugaSkor(x);
                h.ProbaLatih = ProbaSkor(x);
                var dugaLoo = new string[n];
                for (int i = 0; i < n; i++)
                {
                    var xl = x.Where((_, j) => j != i).ToArray();
                    var yl = yi.Where((_, j) => j != i).ToArray();
                    var kl = kelas;
                    var rl = new double[G][];
                    var cl = new int[G];
                    for (int c = 0; c < G; c++)
                    {
                        rl[c] = new double[p];
                        foreach (int j in Enumerable.Range(0, n - 1).Where(j => yl[j] == c))
                        {
                            cl[c]++;
                            for (int a = 0; a < p; a++) rl[c][a] += xl[j][a];
                        }
                        if (cl[c] == 0) { dugaLoo[i] = "?"; goto Lanjut; }
                        for (int a = 0; a < p; a++) rl[c][a] /= cl[c];
                    }
                    {
                        double[][,] cv;
                        if (metode == Metode.Lda)
                        {
                            var pooled = KovTergabung(xl, yl,
                                Enumerable.Range(0, G).ToArray(), rl, out _);
                            cv = Enumerable.Range(0, G).Select(_ => pooled).ToArray();
                        }
                        else
                        {
                            var arr = new double[G][,];
                            bool ok = true;
                            for (int c = 0; c < G; c++)
                            {
                                var S = new double[p, p];
                                foreach (int j in Enumerable.Range(0, n - 1).Where(j => yl[j] == c))
                                    for (int a = 0; a < p; a++)
                                        for (int b = 0; b < p; b++)
                                            S[a, b] += (xl[j][a] - rl[c][a])
                                                       * (xl[j][b] - rl[c][b]);
                                if (cl[c] < 2) { ok = false; break; }
                                for (int a = 0; a < p; a++)
                                    for (int b = 0; b < p; b++) S[a, b] /= cl[c];
                                arr[c] = S;
                            }
                            if (!ok) { dugaLoo[i] = "?"; goto Lanjut; }
                            cv = arr;
                        }
                        var pr = cl.Select(c => (double)c / (n - 1)).ToArray();
                        double terbaik = double.NegativeInfinity;
                        int menang = 0;
                        for (int c = 0; c < G; c++)
                        {
                            var inv = Regression.Inverse((double[,])cv[c].Clone());
                            if (inv is null) { menang = -1; break; }
                            double ld = LogDetDariInv(inv, p);
                            if (double.IsNaN(ld)) { menang = -1; break; }
                            double sk = SkorGauss(x[i], rl[c], inv, ld, Math.Log(pr[c]))[0];
                            if (sk > terbaik) { terbaik = sk; menang = c; }
                        }
                        dugaLoo[i] = menang < 0 ? "?" : kl[menang];
                    }
                Lanjut:;
                }
                h.DugaLoo = dugaLoo;
            }

            var bingung = new int[G, G];
            for (int i = 0; i < n; i++)
                bingung[yi[i], kelas.IndexOf(h.DugaLatih[i])]++;
            h.Bingung = bingung;
            int benar = 0;
            for (int c = 0; c < G; c++) benar += bingung[c, c];
            h.Akurasi = (double)benar / n;
            int benarLoo = 0, hitungLoo = 0;
            for (int i = 0; i < n; i++)
            {
                int d = kelas.IndexOf(h.DugaLoo[i]);
                if (d < 0) continue;
                hitungLoo++;
                if (d == yi[i]) benarLoo++;
            }
            h.AkurasiLoo = hitungLoo > 0 ? (double)benarLoo / hitungLoo : double.NaN;
            return h;
        }

        public static double[][] ProbaKnn(double[][] xLatih, int[] yLatih,
                                            double[][] xUji, int k, int nKelas)
        {
            int n = xLatih.Length;
            var keluar = new double[xUji.Length][];
            for (int i = 0; i < xUji.Length; i++)
            {
                var jarak = new List<(double, int)>();
                for (int j = 0; j < n; j++) jarak.Add((Jarak2(xUji[i], xLatih[j]), yLatih[j]));
                jarak.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                var suara = new int[nKelas];
                int ambil = Math.Min(k, n);
                for (int t = 0; t < ambil; t++) suara[jarak[t].Item2]++;
                keluar[i] = suara.Select(v => (double)v / ambil).ToArray();
            }
            return keluar;
        }

        public static List<ResultBlock> KlasifikasiBlocks(Dataset ds, List<string> fitur,
            string kelas, string metode, int k, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Klasifikasi (k-NN / LDA / QDA)", 1)
            };

            var perlu = new List<string>(fitur) { kelas };
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < 6)
            {
                blocks.Add(Blocks.Note("Baris lengkap kurang dari 6.",
                                       NoteKind.Error));
                return blocks;
            }

            Metode m = metode.StartsWith("LDA", StringComparison.OrdinalIgnoreCase)
                ? Metode.Lda
                : metode.StartsWith("QDA", StringComparison.OrdinalIgnoreCase)
                ? Metode.Qda : Metode.Knn;

            double[][] AmbilX()
            {
                var semua = fitur.Select(v => ds.Numeric(v)).ToList();
                var x = new double[baris.Count][];
                for (int i = 0; i < baris.Count; i++)
                {
                    x[i] = new double[fitur.Count];
                    for (int j = 0; j < fitur.Count; j++)
                        x[i][j] = semua[j][baris[i]] ?? double.NaN;
                }
                return x;
            }

            var tk = ds.Text(kelas);
            var y = baris.Select(i => tk[i] ?? "(kosong)").ToArray();
            var x = AmbilX();
            if (x.Any(r => r.Any(double.IsNaN)))
            {
                blocks.Add(Blocks.Note("Fitur punya nilai hilang.", NoteKind.Error));
                return blocks;
            }

            var h = Pasang(x, y, fitur, m, Math.Max(1, k));
            if (h is null)
            {
                blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.",
                                       NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                m == Metode.Knn ? DaftarRumus.Knn : DaftarRumus.LdaQda));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Fitur", string.Join(", ", fitur)),
                ("Kelas", $"{kelas} ({string.Join(", ", h.Kelas)})"),
                ("Metode", m == Metode.Knn ? $"k-NN (k = {Fmt.Int(h.K)})"
                            : m == Metode.Lda ? "LDA (kovarians tergabung)" : "QDA (kovarians per kelas)"),
                ("Banyak baris latih", $"n = {Fmt.Int(h.NLatih)}"),
                ("Akurasi latih", Fmt.Num(h.Akurasi, 4)),
                ("Akurasi tinggal-satu-keluar (LOO)", Fmt.Num(h.AkurasiLoo, 4))));

            blocks.Add(Blocks.Table(
                "Matriks kebingungan (latih)",
                new[] { "Asli \\ Duga" }.Concat(h.Kelas).ToArray(),
                h.Kelas.Select((kl, i) =>
                    new[] { kl }.Concat(
                        Enumerable.Range(0, h.Kelas.Count).Select(j => Fmt.Int(h.Bingung[i, j]))).ToArray()
                ).ToArray(),
                "Baris = kelas sebenarnya, kolom = dugaan. Diagonal = benar."));

            return blocks;
        }
    }
}
