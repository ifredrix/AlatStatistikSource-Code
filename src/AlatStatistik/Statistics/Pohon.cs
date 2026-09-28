using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class Pohon
    {
        public sealed class Simpul
        {
            public bool Daun;
            public int Fitur;
            public double Ambang;
            public Simpul? Kiri, Kanan;
            public int Kelas;
            public double Nilai;
            public int[] Cacah = Array.Empty<int>();
            public int N;
            public double Takmurni;
        }

        public sealed class HasilKlas
        {
            public List<string> Kelas = new();
            public List<string> Fitur = new();
            public Simpul Akar = new();
            public int[] Label = Array.Empty<int>();
            public int Kedalaman;
            public int Simpul;
            public double[] Kepentingan = Array.Empty<double>();
            public int MaksKedalaman;
            public int MinBelah;
        }

        public sealed class HasilReg
        {
            public List<string> Fitur = new();
            public Simpul Akar = new();
            public double[] Duga = Array.Empty<double>();
            public int Kedalaman;
            public int Simpul;
            public double[] Kepentingan = Array.Empty<double>();
            public int MaksKedalaman;
            public int MinBelah;
        }

        private static double Gini(int[] cacah, int n)
        {
            if (n <= 0) return 0;
            double s = 0;
            foreach (int c in cacah)
            {
                double p = (double)c / n;
                s += p * p;
            }
            return 1 - s;
        }

        private static double Sse(double[] y, int[] idx, int awal, int akhir,
                                  out double mean)
        {
            double s = 0;
            for (int t = awal; t < akhir; t++) s += y[idx[t]];
            int n = akhir - awal;
            mean = n > 0 ? s / n : 0;
            double ss = 0;
            for (int t = awal; t < akhir; t++)
            {
                double d = y[idx[t]] - mean;
                ss += d * d;
            }
            return ss;
        }

        private static double GiniBobot(double[] cacah, double n)
        {
            if (n <= 0) return 0;
            double s = 0;
            foreach (double c in cacah)
            {
                double p = c / n;
                s += p * p;
            }
            return 1 - s;
        }

        private static double SseBobot(double[] y, double[] w, int[] idx, int awal, int akhir,
                                       out double mean)
        {
            double sw = 0, s = 0;
            for (int t = awal; t < akhir; t++) { sw += w[idx[t]]; s += w[idx[t]] * y[idx[t]]; }
            mean = sw > 0 ? s / sw : 0;
            double ss = 0;
            for (int t = awal; t < akhir; t++)
            {
                double d = y[idx[t]] - mean;
                ss += w[idx[t]] * d * d;
            }
            return ss;
        }

        private sealed class BelahCari
        {
            public int[] Idx = Array.Empty<int>();
            public int[] Y = Array.Empty<int>();
            public double[] Yr = Array.Empty<double>();
            public double[][] X = Array.Empty<double[]>();
            public int NKelas;
            public bool Regresi;
            public int MaksDalam;
            public int MinBelah;
            public double[] Penting = Array.Empty<double>();
            public int Kedalaman;
            public int CacahSimpul;
            // Hook hutan acak: kandidat fitur tiap belahan. Null = semua
            // fitur (perilaku CART biasa, dipakai semua uji yang ada).
            public Func<int[]>? Kandidat;
            // Bobot baris (boosting). Selalu terisi (bobot 1,0 bila
            // pemanggil tak-memberi) dengan urutan jumlah yang sama
            // seperti tanpa bobot, sehingga perilaku lama bit-identik.
            public double[] Bobot = Array.Empty<double>();
        }

        private static int[] SemuaFitur(int p)
        {
            var keluar = new int[p];
            for (int f = 0; f < p; f++) keluar[f] = f;
            return keluar;
        }

        private static Simpul Tumbuh(BelahCari b, int[] idx, int dalam)
        {
            int n = idx.Length;
            var node = new Simpul { N = n };
            b.CacahSimpul++;
            if (dalam > b.Kedalaman) b.Kedalaman = dalam;
            int p = b.X[0].Length;

            if (b.Regresi)
            {
                double mean;
                double imp = SseBobot(b.Yr, b.Bobot, idx, 0, n, out mean);
                node.Takmurni = n > 0 ? imp / n : 0;
                node.Nilai = mean;
                if (dalam >= b.MaksDalam || n < b.MinBelah || n < 2)
                {
                    node.Daun = true;
                    return node;
                }
                double terbaik = 0;
                int fTerbaik = -1;
                double ambTerbaik = 0;
                int[] kandidat = b.Kandidat?.Invoke() ?? SemuaFitur(p);
                foreach (int f in kandidat)
                {
                    var urut = idx.OrderBy(i => b.X[i][f]).ToArray();
                    for (int t = 1; t < n; t++)
                    {
                        if (b.X[urut[t - 1]][f] >= b.X[urut[t]][f]) continue;
                        double mL, mR;
                        double ssL = SseBobot(b.Yr, b.Bobot, urut, 0, t, out mL);
                        double ssR = SseBobot(b.Yr, b.Bobot, urut, t, n, out mR);
                        double turun = imp - ssL - ssR;
                        if (turun > terbaik)
                        {
                            terbaik = turun;
                            fTerbaik = f;
                            ambTerbaik = 0.5 * (b.X[urut[t - 1]][f] + b.X[urut[t]][f]);
                        }
                    }
                }
                if (fTerbaik < 0)
                {
                    node.Daun = true;
                    return node;
                }
                node.Fitur = fTerbaik;
                node.Ambang = ambTerbaik;
                b.Penting[fTerbaik] += terbaik;
                var kiri = new List<int>();
                var kanan = new List<int>();
                foreach (int i in idx)
                    (b.X[i][fTerbaik] <= ambTerbaik ? kiri : kanan).Add(i);
                node.Kiri = Tumbuh(b, kiri.ToArray(), dalam + 1);
                node.Kanan = Tumbuh(b, kanan.ToArray(), dalam + 1);
                return node;
            }
            else
            {
                var cacah = new int[b.NKelas];
                var wCacah = new double[b.NKelas];
                double wTotal = 0;
                foreach (int i in idx)
                {
                    cacah[b.Y[i]]++;
                    wCacah[b.Y[i]] += b.Bobot[i];
                    wTotal += b.Bobot[i];
                }
                node.Cacah = cacah;
                node.Takmurni = GiniBobot(wCacah, wTotal);
                int menang = 0;
                for (int c = 1; c < b.NKelas; c++)
                    if (wCacah[c] > wCacah[menang]) menang = c;
                node.Kelas = menang;
                if (dalam >= b.MaksDalam || n < b.MinBelah || n < 2
                    || node.Takmurni <= 0)
                {
                    node.Daun = true;
                    return node;
                }
                double terbaik = 0;
                int fTerbaik = -1;
                double ambTerbaik = 0;
                int[] kandidat = b.Kandidat?.Invoke() ?? SemuaFitur(p);
                foreach (int f in kandidat)
                {
                    var urut = idx.OrderBy(i => b.X[i][f]).ToArray();
                    var kiriC = new double[b.NKelas];
                    var kananC = (double[])wCacah.Clone();
                    double wKiri = 0;
                    for (int t = 1; t < n; t++)
                    {
                        int c = b.Y[urut[t - 1]];
                        double w = b.Bobot[urut[t - 1]];
                        kiriC[c] += w;
                        kananC[c] -= w;
                        wKiri += w;
                        if (b.X[urut[t - 1]][f] >= b.X[urut[t]][f]) continue;
                        double turun = wTotal * node.Takmurni
                                       - wKiri * GiniBobot(kiriC, wKiri)
                                       - (wTotal - wKiri) * GiniBobot(kananC, wTotal - wKiri);
                        if (turun > terbaik)
                        {
                            terbaik = turun;
                            fTerbaik = f;
                            ambTerbaik = 0.5 * (b.X[urut[t - 1]][f] + b.X[urut[t]][f]);
                        }
                    }
                }
                if (fTerbaik < 0)
                {
                    node.Daun = true;
                    return node;
                }
                node.Fitur = fTerbaik;
                node.Ambang = ambTerbaik;
                b.Penting[fTerbaik] += terbaik;
                var kiri = new List<int>();
                var kanan = new List<int>();
                foreach (int i in idx)
                    (b.X[i][fTerbaik] <= ambTerbaik ? kiri : kanan).Add(i);
                node.Kiri = Tumbuh(b, kiri.ToArray(), dalam + 1);
                node.Kanan = Tumbuh(b, kanan.ToArray(), dalam + 1);
                return node;
            }
        }

        private static int DugaKelas(Simpul node, double[] x)
        {
            while (!node.Daun)
                node = x[node.Fitur] <= node.Ambang ? node.Kiri! : node.Kanan!;
            return node.Kelas;
        }

        private static double DugaNilai(Simpul node, double[] x)
        {
            while (!node.Daun)
                node = x[node.Fitur] <= node.Ambang ? node.Kiri! : node.Kanan!;
            return node.Nilai;
        }

        private static double[] Proba(Simpul node, double[] x, int nKelas)
        {
            while (!node.Daun)
                node = x[node.Fitur] <= node.Ambang ? node.Kiri! : node.Kanan!;
            var keluar = new double[nKelas];
            for (int c = 0; c < nKelas; c++)
                keluar[c] = node.N > 0 ? (double)node.Cacah[c] / node.N : 0;
            return keluar;
        }

        private static void Normalisasi(BelahCari b, double[] penting)
        {
            double jumlah = penting.Sum();
            if (jumlah > 0)
                for (int i = 0; i < penting.Length; i++) penting[i] /= jumlah;
        }

        public static HasilKlas? PasangKlasifikasi(double[][] x, string[] y,
            List<string> fitur, int maksKedalaman, int minBelah,
            Func<int[]>? kandidat = null, double[]? bobot = null)
        {
            int n = x.Length;
            if (n < 4 || y.Length != n) return null;
            var kelas = y.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (kelas.Count < 2) return null;
            int p = x[0].Length;
            if (x.Any(r => r.Length != p || r.Any(double.IsNaN))) return null;
            double[] w = bobot ?? Enumerable.Repeat(1.0, n).ToArray();
            if (w.Length != n || w.Any(v => double.IsNaN(v) || v < 0)) return null;
            var yi = y.Select(t => kelas.IndexOf(t)).ToArray();
            var b = new BelahCari
            {
                X = x, Y = yi, NKelas = kelas.Count, Regresi = false,
                MaksDalam = Math.Max(1, maksKedalaman),
                MinBelah = Math.Max(2, minBelah),
                Penting = new double[p], Kandidat = kandidat, Bobot = w,
            };
            var akar = Tumbuh(b, Enumerable.Range(0, n).ToArray(), 0);
            Normalisasi(b, b.Penting);
            var h = new HasilKlas
            {
                Kelas = kelas, Fitur = new List<string>(fitur), Akar = akar,
                Kedalaman = b.Kedalaman, Simpul = b.CacahSimpul,
                Kepentingan = b.Penting,
                MaksKedalaman = b.MaksDalam, MinBelah = b.MinBelah,
                Label = new int[n],
            };
            for (int i = 0; i < n; i++) h.Label[i] = DugaKelas(akar, x[i]);
            return h;
        }

        public static HasilReg? PasangRegresi(double[][] x, double[] y,
            List<string> fitur, int maksKedalaman, int minBelah,
            Func<int[]>? kandidat = null, double[]? bobot = null)
        {
            int n = x.Length;
            if (n < 4 || y.Length != n) return null;
            int p = x[0].Length;
            if (x.Any(r => r.Length != p || r.Any(double.IsNaN))) return null;
            if (y.Any(double.IsNaN)) return null;
            double[] w = bobot ?? Enumerable.Repeat(1.0, n).ToArray();
            if (w.Length != n || w.Any(v => double.IsNaN(v) || v < 0)) return null;
            var b = new BelahCari
            {
                X = x, Yr = y, Regresi = true,
                MaksDalam = Math.Max(1, maksKedalaman),
                MinBelah = Math.Max(2, minBelah),
                Penting = new double[p], Kandidat = kandidat, Bobot = w,
            };
            var akar = Tumbuh(b, Enumerable.Range(0, n).ToArray(), 0);
            Normalisasi(b, b.Penting);
            var h = new HasilReg
            {
                Fitur = new List<string>(fitur), Akar = akar,
                Kedalaman = b.Kedalaman, Simpul = b.CacahSimpul,
                Kepentingan = b.Penting,
                MaksKedalaman = b.MaksDalam, MinBelah = b.MinBelah,
                Duga = new double[n],
            };
            for (int i = 0; i < n; i++) h.Duga[i] = DugaNilai(akar, x[i]);
            return h;
        }

        public static int[] DugakanKelas(HasilKlas h, double[][] xx)
        {
            var keluar = new int[xx.Length];
            for (int i = 0; i < xx.Length; i++) keluar[i] = DugaKelas(h.Akar, xx[i]);
            return keluar;
        }

        public static double[][] ProbaKelas(HasilKlas h, double[][] xx)
        {
            var keluar = new double[xx.Length][];
            for (int i = 0; i < xx.Length; i++) keluar[i] = Proba(h.Akar, xx[i], h.Kelas.Count);
            return keluar;
        }

        public static double[] DugakanNilai(HasilReg h, double[][] xx)
        {
            var keluar = new double[xx.Length];
            for (int i = 0; i < xx.Length; i++) keluar[i] = DugaNilai(h.Akar, xx[i]);
            return keluar;
        }

        public static List<ResultBlock> PohonBlocks(Dataset ds, List<string> fitur,
            string target, string mode, int maksKedalaman, int minBelah)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Pohon keputusan CART", 1)
            };
            var perlu = new List<string>(fitur) { target };
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < 4)
            {
                blocks.Add(Blocks.Note("Baris lengkap kurang dari 4.",
                                       NoteKind.Error));
                return blocks;
            }
            bool regresi;
            if (mode.StartsWith("Klas", StringComparison.OrdinalIgnoreCase)) regresi = false;
            else if (mode.StartsWith("Reg", StringComparison.OrdinalIgnoreCase)) regresi = true;
            else regresi = ds.Variables[ds.IndexOf(target)].Measure == Measure.Skala;

            var semua = fitur.Select(v => ds.Numeric(v)).ToList();
            var x = new double[baris.Count][];
            for (int i = 0; i < baris.Count; i++)
            {
                x[i] = new double[fitur.Count];
                for (int j = 0; j < fitur.Count; j++)
                    x[i][j] = semua[j][baris[i]] ?? double.NaN;
            }
            if (x.Any(r => r.Any(double.IsNaN)))
            {
                blocks.Add(Blocks.Note("Fitur punya nilai hilang.",
                                       NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Pohon));

            if (!regresi)
            {
                var tk = ds.Text(target);
                var y = baris.Select(i => tk[i] ?? "(kosong)").ToArray();
                var h = PasangKlasifikasi(x, y, fitur, maksKedalaman, minBelah);
                if (h is null)
                {
                    blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.",
                                           NoteKind.Error));
                    return blocks;
                }
                var bingung = new int[h.Kelas.Count, h.Kelas.Count];
                for (int i = 0; i < y.Length; i++)
                    bingung[h.Kelas.IndexOf(y[i]), h.Label[i]]++;
                int benar = 0;
                for (int c = 0; c < h.Kelas.Count; c++) benar += bingung[c, c];
                string belahAkar = h.Akar.Daun ? "— (langsung daun)"
                    : h.Akar.FiturOpsi(fitur) + " ≤ " + Fmt.Num(h.Akar.Ambang, 4);
                blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                    ("Fitur", string.Join(", ", fitur)),
                    ("Target", $"{target} ({string.Join(", ", h.Kelas)})"),
                    ("Batas", $"kedalaman ≤ {Fmt.Int(h.MaksKedalaman)}, belah ≥ {Fmt.Int(h.MinBelah)}"),
                    ("Simpul / kedalaman", $"{Fmt.Int(h.Simpul)} / {Fmt.Int(h.Kedalaman)}"),
                    ("Akurasi latih", Fmt.Num((double)benar / y.Length, 4)),
                    ("Belah akar", belahAkar)));
                blocks.Add(Blocks.Table(
                    "Matriks kebingungan (latih)",
                    new[] { "Asli \\ Duga" }.Concat(h.Kelas).ToArray(),
                    h.Kelas.Select((kl, i) => new[] { kl }.Concat(
                        Enumerable.Range(0, h.Kelas.Count).Select(j =>
                            Fmt.Int(bingung[i, j]))).ToArray()).ToArray(),
                    "Diagonal = benar."));
                blocks.Add(Blocks.Table(
                    "Kepentingan fitur (total penurunan Gini ternormalisasi)",
                    new[] { "Fitur", "Kepentingan" },
                    fitur.Select((f, j) => new[]
                        { f, Fmt.Num(h.Kepentingan[j], 4) }).ToArray(),
                    "Menjumlah 1."));
            }
            else
            {
                var sn = ds.Numeric(target);
                var y = baris.Select(i => sn[i] ?? double.NaN).ToArray();
                if (y.Any(double.IsNaN))
                {
                    blocks.Add(Blocks.Note("Target punya nilai hilang.",
                                           NoteKind.Error));
                    return blocks;
                }
                var h = PasangRegresi(x, y, fitur, maksKedalaman, minBelah);
                if (h is null)
                {
                    blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.",
                                           NoteKind.Error));
                    return blocks;
                }
                double rerata = y.Sum() / y.Length;
                double st = y.Sum(v => (v - rerata) * (v - rerata));
                double ss = 0;
                for (int i = 0; i < y.Length; i++) ss += (y[i] - h.Duga[i]) * (y[i] - h.Duga[i]);
                string belahAkarR = h.Akar.Daun ? "— (langsung daun)"
                    : h.Akar.FiturOpsi(fitur) + " ≤ " + Fmt.Num(h.Akar.Ambang, 4);
                blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                    ("Fitur", string.Join(", ", fitur)),
                    ("Target", target),
                    ("Batas", $"kedalaman ≤ {Fmt.Int(h.MaksKedalaman)}, belah ≥ {Fmt.Int(h.MinBelah)}"),
                    ("Simpul / kedalaman", $"{Fmt.Int(h.Simpul)} / {Fmt.Int(h.Kedalaman)}"),
                    ("R² latih", Fmt.Num(st > 0 ? 1 - ss / st : double.NaN, 4)),
                    ("Belah akar", belahAkarR)));
                blocks.Add(Blocks.Table(
                    "Kepentingan fitur (total penurunan SSE ternormalisasi)",
                    new[] { "Fitur", "Kepentingan" },
                    fitur.Select((f, j) => new[]
                        { f, Fmt.Num(h.Kepentingan[j], 4) }).ToArray(),
                    "Menjumlah 1."));
            }
            return blocks;
        }

        private static string FiturOpsi(this Simpul node, List<string> fitur)
            => node.Fitur >= 0 && node.Fitur < fitur.Count ? fitur[node.Fitur]
                                                           : $"X{node.Fitur + 1}";
    }
}
