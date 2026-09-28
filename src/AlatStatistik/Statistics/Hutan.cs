using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    // Hutan acak (random forest, Breiman 2001): B pohon CART di atas
    // contoh bootstrap, tiap belahan hanya menimbang mCoba fitur acak.
    // Dugaan = suara mayoritas (klasifikasi) / rerata (regresi); galat
    public static class Hutan
    {
        private sealed class Lcg
        {
            private uint _s;
            public Lcg(uint benih) { _s = benih == 0 ? 1u : benih; }
            public uint Next()
            {
                _s = _s * 1664525u + 1013904223u;
                return _s;
            }
            public int NextInt(int batas)
            {
                if (batas <= 1) return 0;
                return (int)((Next() >> 8) % (uint)batas);
            }
            // mCoba indeks berbeda dari 0..p-1 (parsial Fisher–Yates).
            public int[] KocokAmbil(int p, int m)
            {
                var idx = new int[p];
                for (int i = 0; i < p; i++) idx[i] = i;
                for (int i = 0; i < m && i < p; i++)
                {
                    int j = i + NextInt(p - i);
                    (idx[i], idx[j]) = (idx[j], idx[i]);
                }
                return idx.Take(Math.Min(m, p)).ToArray();
            }
        }

        public sealed class HasilKlas
        {
            public List<string> Kelas = new();
            public List<string> Fitur = new();
            public int NPohon, MCoba, MaksKedalaman, MinBelah;
            public uint Benih;
            public int[] DugaOob = Array.Empty<int>();
            public bool[] AdaOob = Array.Empty<bool>();
            public int[,] Suara = new int[0, 0];
            public double AkurasiOob;
            public int[,] BingungOob = new int[0, 0];
            public double[] Kepentingan = Array.Empty<double>();
            public double KedalamanRerata;
            public List<Pohon.HasilKlas> Pohon = new();
        }

        public sealed class HasilReg
        {
            public List<string> Fitur = new();
            public int NPohon, MCoba, MaksKedalaman, MinBelah;
            public uint Benih;
            public double[] DugaOob = Array.Empty<double>();
            public bool[] AdaOob = Array.Empty<bool>();
            public double RmseOob, R2Oob;
            public double[] Kepentingan = Array.Empty<double>();
            public double KedalamanRerata;
            public List<Pohon.HasilReg> Pohon = new();
        }

        public static int BakuMCoba(int p, bool regresi)
            => regresi ? Math.Max(1, p / 3) : Math.Max(1, (int)Math.Round(Math.Sqrt(p)));

        public static HasilKlas? PasangKlasifikasi(double[][] x, string[] y,
            List<string> fitur, int nPohon, int mCoba,
            int maksKedalaman, int minBelah, uint benih)
        {
            int n = x.Length;
            if (n < 4 || y.Length != n) return null;
            int p = x[0].Length;
            if (x.Any(r => r.Length != p || r.Any(double.IsNaN))) return null;
            var kelas = y.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (kelas.Count < 2) return null;
            int B = Math.Max(1, Math.Min(nPohon, 2000));
            int m = mCoba <= 0 ? BakuMCoba(p, false) : Math.Min(mCoba, p);
            var rng = new Lcg(benih);

            var suara = new int[n, kelas.Count];
            var oobKumpul = new int[n];
            var penting = new double[p];
            double kedalaman = 0;
            var pohon = new List<Pohon.HasilKlas>();
            var masker = new List<bool[]>();

            for (int b = 0; b < B; b++)
            {
                var contoh = new int[n];
                var terambil = new bool[n];
                for (int i = 0; i < n; i++)
                {
                    int k = rng.NextInt(n);
                    contoh[i] = k;
                    terambil[k] = true;
                }
                var xb = contoh.Select(k => x[k]).ToArray();
                var yb = contoh.Select(k => y[k]).ToArray();
                var h = Pohon.PasangKlasifikasi(xb, yb, fitur, maksKedalaman,
                                                minBelah, () => rng.KocokAmbil(p, m));
                if (h is null) return null;
                kedalaman += h.Kedalaman;
                pohon.Add(h);
                masker.Add(terambil);
                var duga = Pohon.DugakanKelas(h, x);
                for (int i = 0; i < n; i++)
                    if (!terambil[i]) { suara[i, duga[i]]++; oobKumpul[i]++; }
            }

            int DugaOobBaris(double[][] xa, int i)
            {
                var hitungL = new int[kelas.Count];
                for (int b = 0; b < B; b++)
                    if (!masker[b][i])
                        hitungL[Pohon.DugakanKelas(pohon[b], new[] { xa[i] })[0]]++;
                int menang = 0;
                for (int c = 1; c < kelas.Count; c++)
                    if (hitungL[c] > hitungL[menang]) menang = c;
                return menang;
            }

            double AkurasiOobPada(double[][] xa)
            {
                int benarL = 0, hitungL = 0;
                for (int i = 0; i < n; i++)
                {
                    if (oobKumpul[i] == 0) continue;
                    if (kelas[DugaOobBaris(xa, i)] == y[i]) benarL++;
                    hitungL++;
                }
                return hitungL > 0 ? (double)benarL / hitungL : double.NaN;
            }

            double akurasiBersih = AkurasiOobPada(x);
            // Kepentingan permutasi tingkat-hutan (definisi sklearn:
            // jatuhnya akurasi OOB hutan setelah satu fitur diacak,
            // rerata 5 ulangan) — bukan rerata per-pohon.
            var pentingHt = new double[p];
            for (int ulang = 0; ulang < 5; ulang++)
                for (int f = 0; f < p; f++)
                {
                    var campur = Enumerable.Range(0, n).Select(i => x[i][f]).ToList();
                    for (int i = campur.Count - 1; i > 0; i--)
                    {
                        int j = rng.NextInt(i + 1);
                        (campur[i], campur[j]) = (campur[j], campur[i]);
                    }
                    var xa = x.Select(r => (double[])r.Clone()).ToArray();
                    for (int i = 0; i < n; i++) xa[i][f] = campur[i];
                    pentingHt[f] += (akurasiBersih - AkurasiOobPada(xa)) / 5;
                }
            penting = pentingHt;

            var dugaOob = new int[n];
            var adaOob = new bool[n];
            var bingung = new int[kelas.Count, kelas.Count];
            int benar = 0, hitung = 0;
            for (int i = 0; i < n; i++)
            {
                if (oobKumpul[i] == 0) continue;
                int menang = 0;
                for (int c = 1; c < kelas.Count; c++)
                    if (suara[i, c] > suara[i, menang]) menang = c;
                dugaOob[i] = menang;
                adaOob[i] = true;
                bingung[kelas.IndexOf(y[i]), menang]++;
                if (kelas[menang] == y[i]) benar++;
                hitung++;
            }
            return new HasilKlas
            {
                Kelas = kelas, Fitur = new List<string>(fitur),
                NPohon = B, MCoba = m, MaksKedalaman = maksKedalaman,
                MinBelah = minBelah, Benih = benih,
                DugaOob = dugaOob, AdaOob = adaOob, Suara = suara,
                AkurasiOob = hitung > 0 ? (double)benar / hitung : double.NaN,
                BingungOob = bingung, Kepentingan = penting,
                KedalamanRerata = kedalaman / B, Pohon = pohon,
            };
        }

        public static HasilReg? PasangRegresi(double[][] x, double[] y,
            List<string> fitur, int nPohon, int mCoba,
            int maksKedalaman, int minBelah, uint benih)
        {
            int n = x.Length;
            if (n < 4 || y.Length != n) return null;
            int p = x[0].Length;
            if (x.Any(r => r.Length != p || r.Any(double.IsNaN))) return null;
            if (y.Any(double.IsNaN)) return null;
            int B = Math.Max(1, Math.Min(nPohon, 2000));
            int m = mCoba <= 0 ? BakuMCoba(p, true) : Math.Min(mCoba, p);
            var rng = new Lcg(benih);

            var jumlah = new double[n];
            var oobKumpul = new int[n];
            var penting = new double[p];
            double kedalaman = 0;
            var pohon = new List<Pohon.HasilReg>();
            var maskerR = new List<bool[]>();

            for (int b = 0; b < B; b++)
            {
                var contoh = new int[n];
                var terambil = new bool[n];
                for (int i = 0; i < n; i++)
                {
                    int k = rng.NextInt(n);
                    contoh[i] = k;
                    terambil[k] = true;
                }
                var xb = contoh.Select(k => x[k]).ToArray();
                var yb = contoh.Select(k => y[k]).ToArray();
                var h = Pohon.PasangRegresi(xb, yb, fitur, maksKedalaman,
                                            minBelah, () => rng.KocokAmbil(p, m));
                if (h is null) return null;
                kedalaman += h.Kedalaman;
                pohon.Add(h);
                maskerR.Add(terambil);
                var duga = Pohon.DugakanNilai(h, x);
                for (int i = 0; i < n; i++)
                    if (!terambil[i]) { jumlah[i] += duga[i]; oobKumpul[i]++; }
            }

            double DugaOobBarisR(double[][] xa, int i)
            {
                double s = 0;
                int c = 0;
                for (int b = 0; b < B; b++)
                    if (!maskerR[b][i])
                    {
                        s += Pohon.DugakanNilai(pohon[b], new[] { xa[i] })[0];
                        c++;
                    }
                return c > 0 ? s / c : double.NaN;
            }

            double MseOobPada(double[][] xa)
            {
                double ss = 0;
                int c = 0;
                for (int i = 0; i < n; i++)
                {
                    if (oobKumpul[i] == 0) continue;
                    double e = DugaOobBarisR(xa, i) - y[i];
                    ss += e * e;
                    c++;
                }
                return c > 0 ? ss / c : double.NaN;
            }

            double mseBersih = MseOobPada(x);
            // Kepentingan permutasi tingkat-hutan (definisi sklearn:
            // naiknya MSE OOB hutan setelah satu fitur diacak, rerata
            // 5 ulangan) — bukan rerata per-pohon.
            var pentingR = new double[p];
            for (int ulang = 0; ulang < 5; ulang++)
                for (int f = 0; f < p; f++)
                {
                    var campur = Enumerable.Range(0, n).Select(i => x[i][f]).ToList();
                    for (int i = campur.Count - 1; i > 0; i--)
                    {
                        int j = rng.NextInt(i + 1);
                        (campur[i], campur[j]) = (campur[j], campur[i]);
                    }
                    var xa = x.Select(r => (double[])r.Clone()).ToArray();
                    for (int i = 0; i < n; i++) xa[i][f] = campur[i];
                    pentingR[f] += (MseOobPada(xa) - mseBersih) / 5;
                }
            penting = pentingR;

            var dugaOob = new double[n];
            var adaOob = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (oobKumpul[i] == 0) continue;
                dugaOob[i] = jumlah[i] / oobKumpul[i];
                adaOob[i] = true;
            }
            var pasang = Enumerable.Range(0, n).Where(i => adaOob[i]).ToList();
            double rmse = double.NaN, r2 = double.NaN;
            if (pasang.Count > 0)
            {
                double ss = pasang.Sum(i => (dugaOob[i] - y[i]) * (dugaOob[i] - y[i]));
                rmse = Math.Sqrt(ss / pasang.Count);
                double rata = pasang.Average(i => y[i]);
                double st = pasang.Sum(i => (y[i] - rata) * (y[i] - rata));
                r2 = st > 0 ? 1 - ss / st : double.NaN;
            }
            return new HasilReg
            {
                Fitur = new List<string>(fitur),
                NPohon = B, MCoba = m, MaksKedalaman = maksKedalaman,
                MinBelah = minBelah, Benih = benih,
                DugaOob = dugaOob, AdaOob = adaOob,
                RmseOob = rmse, R2Oob = r2, Kepentingan = penting,
                KedalamanRerata = kedalaman / B, Pohon = pohon,
            };
        }

        public static int[] DugakanKelas(HasilKlas h, double[][] xx)
        {
            var keluar = new int[xx.Length];
            for (int i = 0; i < xx.Length; i++)
            {
                var hitung = new int[h.Kelas.Count];
                foreach (var t in h.Pohon)
                    hitung[Pohon.DugakanKelas(t, new[] { xx[i] })[0]]++;
                int menang = 0;
                for (int c = 1; c < h.Kelas.Count; c++)
                    if (hitung[c] > hitung[menang]) menang = c;
                keluar[i] = menang;
            }
            return keluar;
        }

        public static double[] DugakanNilai(HasilReg h, double[][] xx)
        {
            var keluar = new double[xx.Length];
            for (int i = 0; i < xx.Length; i++)
                keluar[i] = h.Pohon.Average(t => Pohon.DugakanNilai(t, new[] { xx[i] })[0]);
            return keluar;
        }

        public static List<ResultBlock> HutanBlocks(Dataset ds, List<string> fitur,
            string target, string mode, int nPohon, int mCoba,
            int maksKedalaman, int minBelah, uint benih)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Random forest", 1)
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

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.HutanModel, DaftarRumus.HutanOob));

            int p = fitur.Count;
            int mm = mCoba <= 0 ? BakuMCoba(p, regresi) : Math.Min(mCoba, p);
            string batas = $"pohon {Fmt.Int(Math.Max(1, Math.Min(nPohon, 2000)))}, "
                           + $"mCoba {Fmt.Int(mm)} dari {Fmt.Int(p)}, "
                           + $"kedalaman ≤ {Fmt.Int(maksKedalaman)}, "
                           + $"belah ≥ {Fmt.Int(minBelah)}, benih {benih}";

            if (!regresi)
            {
                var tk = ds.Text(target);
                var y = baris.Select(i => tk[i] ?? "(kosong)").ToArray();
                var h = PasangKlasifikasi(x, y, fitur, nPohon, mCoba,
                                          maksKedalaman, minBelah, benih);
                if (h is null)
                {
                    blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.",
                                           NoteKind.Error));
                    return blocks;
                }
                blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                    ("Fitur", string.Join(", ", fitur)),
                    ("Target", $"{target} ({string.Join(", ", h.Kelas)})"),
                    ("Batas", batas),
                    ("Akurasi OOB", Fmt.Num(h.AkurasiOob, 4)),
                    ("Kedalaman rerata pohon", Fmt.Num(h.KedalamanRerata, 2))));
                blocks.Add(Blocks.Table(
                    "Matriks kebingungan (OOB)",
                    new[] { "Asli \\ Duga" }.Concat(h.Kelas).ToArray(),
                    h.Kelas.Select((kl, i) => new[] { kl }.Concat(
                        Enumerable.Range(0, h.Kelas.Count).Select(j =>
                            Fmt.Int(h.BingungOob[i, j]))).ToArray()).ToArray(),
                    "Baris tanpa suara OOB dilewati — diagonal = benar."));
                blocks.Add(Blocks.Table(
                    "Kepentingan fitur (penurunan akurasi OOB oleh pengacakan)",
                    new[] { "Fitur", "Kepentingan" },
                    fitur.Select((f, j) => new[]
                        { f, Fmt.Num(h.Kepentingan[j], 4) }).ToArray(),
                    "Rerata per pohon; boleh negatif bila fitur tak membantu."));
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
                var h = PasangRegresi(x, y, fitur, nPohon, mCoba,
                                      maksKedalaman, minBelah, benih);
                if (h is null)
                {
                    blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.",
                                           NoteKind.Error));
                    return blocks;
                }
                blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                    ("Fitur", string.Join(", ", fitur)),
                    ("Target", target),
                    ("Batas", batas),
                    ("RMSE OOB", Fmt.Num(h.RmseOob, 4)),
                    ("R² OOB", Fmt.Num(h.R2Oob, 4)),
                    ("Kedalaman rerata pohon", Fmt.Num(h.KedalamanRerata, 2))));
                blocks.Add(Blocks.Table(
                    "Kepentingan fitur (kenaikan MSE OOB oleh pengacakan)",
                    new[] { "Fitur", "Kepentingan" },
                    fitur.Select((f, j) => new[]
                        { f, Fmt.Num(h.Kepentingan[j], 4) }).ToArray(),
                    "Rerata per pohon; boleh negatif bila fitur tak membantu."));
            }
            return blocks;
        }
    }
}
