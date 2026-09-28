using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class TwoStep
    {
        public sealed class Hasil
        {
            public int N;
            public int KMaks;
            public int KAuto;
            public double[] Bic = Array.Empty<double>();
            public double[] XiTotal = Array.Empty<double>();
            public int[] Label = Array.Empty<int>();
            public int[] Ukuran = Array.Empty<int>();
            public double SiluetMean;
            public double[] Siluet = Array.Empty<double>();
            public double[][] ProfilNum = Array.Empty<double[]>();
            public int[][][] ProfilKat = Array.Empty<int[][]>();
            public List<string> NamaNum = new();
            public List<string> NamaKat = new();
            public List<string[]> Aras = new();
            public double JarakGabungMin;
        }

        private static double Xi(List<int> anggota, double[][] z,
                                 string[][] kats, List<string[]> aras)
        {
            int nv = anggota.Count;
            double s = 0;
            int ka = z[0].Length;
            for (int k = 0; k < ka; k++)
            {
                double tot = 0;
                foreach (int i in anggota) tot += z[i][k];
                double mean = tot / nv;
                double ss = 0;
                foreach (int i in anggota)
                {
                    double d = z[i][k] - mean;
                    ss += d * d;
                }
                s += 0.5 * Math.Log(1.0 + ss / nv);
            }
            for (int k = 0; k < aras.Count; k++)
            {
                foreach (string lv in aras[k])
                {
                    int c = 0;
                    foreach (int i in anggota)
                        if (kats[i][k] == lv) c++;
                    if (c > 0)
                    {
                        double p = (double)c / nv;
                        s -= p * Math.Log(p);
                    }
                }
            }
            return -nv * s;
        }

        private static List<int> Gabung(List<int> a, List<int> b)
        {
            var keluar = new List<int>(a.Count + b.Count);
            int i = 0, j = 0;
            while (i < a.Count && j < b.Count)
            {
                if (a[i] <= b[j]) keluar.Add(a[i++]);
                else keluar.Add(b[j++]);
            }
            while (i < a.Count) keluar.Add(a[i++]);
            while (j < b.Count) keluar.Add(b[j++]);
            return keluar;
        }

        private static (int[] Label, int[] Ukuran) Kanonis(List<List<int>> klasters, int n)
        {
            var urut = klasters.OrderByDescending(m => m.Count)
                               .ThenBy(m => m[0]).ToList();
            var lab = new int[n];
            var uk = new int[urut.Count];
            for (int c = 0; c < urut.Count; c++)
            {
                uk[c] = urut[c].Count;
                foreach (int i in urut[c]) lab[i] = c;
            }
            return (lab, uk);
        }

        public static double[,] JarakGower(double[][] x, string[][] kats, double[] rentang)
        {
            int n = x.Length;
            int ka = x[0].Length, kb = kats[0].Length;
            var d = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    double s = 0;
                    for (int k = 0; k < ka; k++)
                        s += Math.Abs(x[i][k] - x[j][k]) / rentang[k];
                    for (int k = 0; k < kb; k++)
                        if (kats[i][k] != kats[j][k]) s += 1.0;
                    double v = s / (ka + kb);
                    d[i, j] = v;
                    d[j, i] = v;
                }
            return d;
        }

        public static Hasil? Pasang(double[][] xNum, string[][] xKat,
                                    List<string> namaNum, List<string> namaKat,
                                    int kMaks)
        {
            int n = xNum.Length;
            if (n < 6 || xKat.Length != n) return null;
            int ka = namaNum.Count, kb = namaKat.Count;
            if (ka < 1 || kMaks < 2 || kMaks > 12) return null;
            if (xNum.Any(r => r.Length != ka)) return null;
            if (kb > 0 && xKat.Any(r => r.Length != kb)) return null;
            if (xNum.Any(r => r.Any(double.IsNaN))) return null;

            var aras = new List<string[]>();
            for (int k = 0; k < kb; k++)
            {
                var lv = xKat.Select(r => r[k]).Distinct()
                             .OrderBy(t => t, StringComparer.Ordinal).ToArray();
                if (lv.Length < 2) return null;
                aras.Add(lv);
            }

            // Baku-z sekuensial (simpangan populasi) — sama dengan acuan.
            var z = new double[n][];
            for (int i = 0; i < n; i++) z[i] = new double[ka];
            for (int k = 0; k < ka; k++)
            {
                double tot = 0;
                for (int i = 0; i < n; i++) tot += xNum[i][k];
                double mean = tot / n;
                double ss = 0;
                for (int i = 0; i < n; i++)
                {
                    double d = xNum[i][k] - mean;
                    ss += d * d;
                }
                double var = ss / n;
                if (var <= 0) return null;
                double sd = Math.Sqrt(var);
                for (int i = 0; i < n; i++) z[i][k] = (xNum[i][k] - mean) / sd;
            }

            var aktif = Enumerable.Range(0, n).Select(i => new List<int> { i }).ToList();
            var potret = new Dictionary<int, (int[] Lab, int[] Uk, double Xit)>();
            double dmin = double.PositiveInfinity;
            while (aktif.Count > 1)
            {
                int ba = -1, bb = -1;
                double bd = double.PositiveInfinity;
                var xis = aktif.Select(m => Xi(m, z, xKat, aras)).ToList();
                for (int a = 0; a < aktif.Count; a++)
                    for (int b = a + 1; b < aktif.Count; b++)
                    {
                        double dd = xis[a] + xis[b]
                                    - Xi(Gabung(aktif[a], aktif[b]), z, xKat, aras);
                        if (dd < bd) { bd = dd; ba = a; bb = b; }
                    }
                if (bd < dmin) dmin = bd;
                aktif[ba] = Gabung(aktif[ba], aktif[bb]);
                aktif.RemoveAt(bb);
                int k = aktif.Count;
                if (k <= kMaks)
                {
                    var (lab, uk) = Kanonis(aktif, n);
                    double xit = 0;
                    foreach (var m in aktif) xit += Xi(m, z, xKat, aras);
                    potret[k] = (lab, uk, xit);
                }
            }

            double ln = Math.Log(n);
            int m1 = 2 * ka + aras.Sum(lv => lv.Length - 1);
            var bic = new double[kMaks];
            var xits = new double[kMaks];
            for (int k = 1; k <= kMaks; k++)
            {
                xits[k - 1] = potret[k].Xit;
                bic[k - 1] = -2.0 * potret[k].Xit + k * m1 * ln;
            }
            int kAuto = 1;
            for (int k = 2; k <= kMaks; k++)
                if (bic[k - 1] < bic[kAuto - 1]) kAuto = k;

            var h = new Hasil
            {
                N = n, KMaks = kMaks, KAuto = kAuto,
                Bic = bic, XiTotal = xits,
                NamaNum = new List<string>(namaNum),
                NamaKat = new List<string>(namaKat),
                Aras = aras, JarakGabungMin = dmin,
            };
            var fin = potret[kAuto];
            h.Label = fin.Lab;
            h.Ukuran = fin.Uk;

            var rentang = new double[ka];
            for (int k = 0; k < ka; k++)
            {
                double mn = xNum[0][k], mx = xNum[0][k];
                for (int i = 1; i < n; i++)
                {
                    if (xNum[i][k] < mn) mn = xNum[i][k];
                    if (xNum[i][k] > mx) mx = xNum[i][k];
                }
                rentang[k] = mx - mn;
            }
            var g = kb > 0 ? JarakGower(xNum, xKat, rentang)
                           : JarakGower(xNum, NolKat(n), rentang);
            var sil = new double[n];
            for (int i = 0; i < n; i++)
            {
                var sama = new List<int>();
                for (int j = 0; j < n; j++)
                    if (j != i && h.Label[j] == h.Label[i]) sama.Add(j);
                if (sama.Count == 0) { sil[i] = 0; continue; }
                double a = 0;
                foreach (int j in sama) a += g[i, j];
                a /= sama.Count;
                double b = double.PositiveInfinity;
                for (int c = 0; c < kAuto; c++)
                {
                    if (c == h.Label[i]) continue;
                    double r = 0;
                    int cnt = 0;
                    for (int j = 0; j < n; j++)
                        if (h.Label[j] == c) { r += g[i, j]; cnt++; }
                    if (cnt == 0) continue;
                    r /= cnt;
                    if (r < b) b = r;
                }
                sil[i] = double.IsPositiveInfinity(b) ? 0 : (b - a) / Math.Max(a, b);
            }
            h.Siluet = sil;
            h.SiluetMean = sil.Sum() / n;

            h.ProfilNum = new double[kAuto][];
            for (int c = 0; c < kAuto; c++)
            {
                h.ProfilNum[c] = new double[ka];
                int cnt = 0;
                for (int i = 0; i < n; i++)
                {
                    if (h.Label[i] != c) continue;
                    cnt++;
                    for (int k = 0; k < ka; k++) h.ProfilNum[c][k] += xNum[i][k];
                }
                for (int k = 0; k < ka; k++) h.ProfilNum[c][k] /= cnt;
            }
            h.ProfilKat = new int[kAuto][][];
            for (int c = 0; c < kAuto; c++)
            {
                h.ProfilKat[c] = new int[kb][];
                for (int k = 0; k < kb; k++)
                {
                    h.ProfilKat[c][k] = new int[aras[k].Length];
                    for (int i = 0; i < n; i++)
                    {
                        if (h.Label[i] != c) continue;
                        int li = Array.IndexOf(aras[k], xKat[i][k]);
                        h.ProfilKat[c][k][li]++;
                    }
                }
            }
            return h;
        }

        private static string[][] NolKat(int n)
        {
            var keluar = new string[n][];
            for (int i = 0; i < n; i++) keluar[i] = Array.Empty<string>();
            return keluar;
        }

        public static List<ResultBlock> TwoStepBlocks(Dataset ds, List<string> numerik,
            List<string> kategori, int kMaks, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Klaster TwoStep (campuran angka + kategori)", 1)
            };

            var perlu = new List<string>(numerik);
            perlu.AddRange(kategori);
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < 6)
            {
                blocks.Add(Blocks.Note("Baris lengkap kurang dari 6.",
                                       NoteKind.Error));
                return blocks;
            }
            if (numerik.Count < 1)
            {
                blocks.Add(Blocks.Note("Pilih sedikitnya satu variabel angka.",
                                       NoteKind.Error));
                return blocks;
            }

            var semuaNum = numerik.Select(v => ds.Numeric(v)).ToList();
            var x = new double[baris.Count][];
            for (int i = 0; i < baris.Count; i++)
            {
                x[i] = new double[numerik.Count];
                for (int j = 0; j < numerik.Count; j++)
                    x[i][j] = semuaNum[j][baris[i]] ?? double.NaN;
            }
            var semuaKat = kategori.Select(v => ds.Text(v)).ToList();
            var kk = new string[baris.Count][];
            for (int i = 0; i < baris.Count; i++)
            {
                kk[i] = new string[kategori.Count];
                for (int j = 0; j < kategori.Count; j++)
                    kk[i][j] = semuaKat[j][baris[i]] ?? "(kosong)";
            }
            if (x.Any(r => r.Any(double.IsNaN)))
            {
                blocks.Add(Blocks.Note("Variabel angka punya nilai hilang.",
                                       NoteKind.Error));
                return blocks;
            }

            var h = Pasang(x, kk, numerik, kategori,
                           Math.Min(12, Math.Max(2, kMaks)));
            if (h is null)
            {
                blocks.Add(Blocks.Note("Model tidak bisa disesuaikan — periksa "
                                       + "ragam variabel angka dan aras kategori.",
                                       NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.TwoStep));
            blocks.Add(Blocks.Rumus("Jarak Gower & siluet (pemeriksa mutu)",
                                    DaftarRumus.GowerSiluet));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Variabel angka (dibakukan-z)", string.Join(", ", numerik)),
                ("Variabel kategori",
                 kategori.Count > 0 ? string.Join(", ", kategori) : "—"),
                ("Banyak baris", $"n = {Fmt.Int(h.N)}"),
                ("Otomatis: k = ", $"{Fmt.Int(h.KAuto)} klaster "
                 + $"(BIC terkecil = {Fmt.Num(h.Bic[h.KAuto - 1], 2)})"),
                ("Siluet rerata (Gower)", Fmt.Num(h.SiluetMean, 4))));

            blocks.Add(Blocks.Table(
                "BIC per banyak klaster (pilih yang terkecil)",
                new[] { "k", "BIC", "Δ dari k−1" },
                Enumerable.Range(1, h.KMaks).Select(k =>
                {
                    string delta = k == 1 ? "—"
                        : Fmt.Num(h.Bic[k - 1] - h.Bic[k - 2], 2);
                    string kk2 = k == h.KAuto ? $"{k} ← dipilih" : $"{k}";
                    return new[] { kk2, Fmt.Num(h.Bic[k - 1], 2), delta };
                }).ToArray(),
                "BIC = −2Σξ + m·ln n (Schwarz). k terkecil yang meminimumkan "
                + "dipilih otomatis."));

            blocks.Add(Blocks.Table(
                $"Profil klaster (k = {h.KAuto})",
                new[] { "Klaster" }.Concat(numerik.Select(v => $"rerata {v}"))
                     .Concat(new[] { "Anggota" }).ToArray(),
                Enumerable.Range(0, h.KAuto).Select(c =>
                    new[] { $"C{c + 1}" }.Concat(
                        Enumerable.Range(0, numerik.Count).Select(j =>
                            Fmt.Num(h.ProfilNum[c][j], 4)))
                    .Concat(new[] { Fmt.Int(h.Ukuran[c]) }).ToArray()
                ).ToArray(),
                "Rerata dalam satuan asli. Id klaster diurutkan dari yang "
                + "terbesar (deterministik)."));

            return blocks;
        }
    }
}
