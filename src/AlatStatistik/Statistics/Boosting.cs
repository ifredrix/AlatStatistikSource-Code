using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    // Boosting sekuensial (deterministik penuh — tanpa pembangkit acak):
    //  regresi = gradient boosting galat-kuadrat (Friedman 2001): tiap
    //    ronde memasang pohon pada sisa (residu), duga += lr · pohon;
    public static class Boosting
    {
        public sealed class HasilReg
        {
            public List<string> Fitur = new();
            public List<Pohon.HasilReg> Pohon = new();
            public double F0;
            public double Lr;
            public int Ronde, MaksDalam, MinBelah;
            public double[] SseTahap = Array.Empty<double>();
            public double[] R2Tahap = Array.Empty<double>();
            public double[] Duga = Array.Empty<double>();
            public double[] Kepentingan = Array.Empty<double>();
        }

        public sealed class HasilKlas
        {
            public List<string> Kelas = new();
            public List<string> Fitur = new();
            public List<Pohon.HasilKlas> Pohon = new();
            public double[] Alfa = Array.Empty<double>();
            public double Lr;
            public int Ronde, MaksDalam, MinBelah;
            public double[] GalatTahap = Array.Empty<double>();
            public double[] RugiTahap = Array.Empty<double>();
            public int[] Label = Array.Empty<int>();
            public double[] Kepentingan = Array.Empty<double>();
        }

        public static HasilReg? PasangRegresi(double[][] x, double[] y,
            List<string> fitur, int ronde, double lr,
            int maksDalam, int minBelah)
        {
            int n = x.Length;
            if (n < 4 || y.Length != n) return null;
            int p = x[0].Length;
            if (x.Any(r => r.Length != p || r.Any(double.IsNaN))) return null;
            if (y.Any(double.IsNaN)) return null;
            int B = Math.Max(1, Math.Min(ronde, 2000));
            double eta = Math.Min(Math.Max(lr, 1e-9), 1.0);
            double f0 = y.Average();
            var F = Enumerable.Repeat(f0, n).ToArray();
            double rataY = f0;
            double st = y.Sum(v => (v - rataY) * (v - rataY));

            var pohon = new List<Pohon.HasilReg>();
            var sseTahap = new List<double>();
            var r2Tahap = new List<double>();
            var penting = new double[p];
            for (int b = 0; b < B; b++)
            {
                var sisa = new double[n];
                for (int i = 0; i < n; i++) sisa[i] = y[i] - F[i];
                var h = Pohon.PasangRegresi(x, sisa, fitur, maksDalam, minBelah);
                if (h is null) return null;
                var d = Pohon.DugakanNilai(h, x);
                for (int i = 0; i < n; i++) F[i] += eta * d[i];
                pohon.Add(h);
                double ss = 0;
                for (int i = 0; i < n; i++) ss += (y[i] - F[i]) * (y[i] - F[i]);
                sseTahap.Add(ss);
                r2Tahap.Add(st > 0 ? 1 - ss / st : double.NaN);
                for (int j = 0; j < p; j++) penting[j] += h.Kepentingan[j];
            }
            for (int j = 0; j < p; j++) penting[j] /= pohon.Count;
            return new HasilReg
            {
                Fitur = new List<string>(fitur), Pohon = pohon, F0 = f0, Lr = eta,
                Ronde = pohon.Count, MaksDalam = maksDalam, MinBelah = minBelah,
                SseTahap = sseTahap.ToArray(), R2Tahap = r2Tahap.ToArray(),
                Duga = F, Kepentingan = penting,
            };
        }

        public static HasilKlas? PasangKlasifikasi(double[][] x, string[] y,
            List<string> fitur, int ronde, double lr,
            int maksDalam, int minBelah)
        {
            int n = x.Length;
            if (n < 4 || y.Length != n) return null;
            var kelas = y.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (kelas.Count < 2) return null;
            int p = x[0].Length;
            if (x.Any(r => r.Length != p || r.Any(double.IsNaN))) return null;
            int K = kelas.Count;
            int B = Math.Max(1, Math.Min(ronde, 2000));
            double eta = Math.Min(Math.Max(lr, 1e-9), 1.0);

            var yi = y.Select(t => kelas.IndexOf(t)).ToArray();
            var w = Enumerable.Repeat(1.0 / n, n).ToArray();
            var pohon = new List<Pohon.HasilKlas>();
            var alfa = new List<double>();
            var galatTahap = new List<double>();
            var rugiTahap = new List<double>();
            var suara = new double[n, K];
            var penting = new double[p];
            for (int b = 0; b < B; b++)
            {
                var h = Pohon.PasangKlasifikasi(x, y, fitur, maksDalam, minBelah,
                                                null, (double[])w.Clone());
                if (h is null) return null;
                var d = Pohon.DugakanKelas(h, x);
                double salah = 0, total = 0;
                for (int i = 0; i < n; i++)
                {
                    total += w[i];
                    if (d[i] != yi[i]) salah += w[i];
                }
                double err = total > 0 ? salah / total : 0;
                // Degenerasi: sempurna (berhenti, sudah menang) atau lebih
                // buruk dari acak (berhenti, tak-dapat-dipakai).
                if (err <= 0 || err >= 1.0 - 1.0 / K) break;
                double a = eta * (Math.Log((1 - err) / err) + Math.Log(K - 1));
                for (int i = 0; i < n; i++)
                {
                    if (d[i] != yi[i]) w[i] *= Math.Exp(a);
                    suara[i, d[i]] += a;
                }
                double sw = w.Sum();
                for (int i = 0; i < n; i++) w[i] /= sw;
                pohon.Add(h);
                alfa.Add(a);
                int salahKeras = 0;
                for (int i = 0; i < n; i++)
                {
                    int menang = 0;
                    for (int c = 1; c < K; c++)
                        if (suara[i, c] > suara[i, menang]) menang = c;
                    if (menang != yi[i]) salahKeras++;
                }
                galatTahap.Add((double)salahKeras / n);
                // Rugi eksponensial SAMME: Σ exp(−suara-benar).
                // Dipakai monoton-turunnya saja sebagai penjaga.
                double rugiSamme = 0;
                for (int i = 0; i < n; i++)
                    rugiSamme += Math.Exp(-suara[i, yi[i]]);
                rugiTahap.Add(rugiSamme / n);
                for (int j = 0; j < p; j++) penting[j] += h.Kepentingan[j];
            }
            if (pohon.Count == 0) return null;
            for (int j = 0; j < p; j++) penting[j] /= pohon.Count;
            var label = new int[n];
            for (int i = 0; i < n; i++)
            {
                int menang = 0;
                for (int c = 1; c < K; c++)
                    if (suara[i, c] > suara[i, menang]) menang = c;
                label[i] = menang;
            }
            return new HasilKlas
            {
                Kelas = kelas, Fitur = new List<string>(fitur),
                Pohon = pohon, Alfa = alfa.ToArray(), Lr = eta,
                Ronde = pohon.Count, MaksDalam = maksDalam, MinBelah = minBelah,
                GalatTahap = galatTahap.ToArray(), RugiTahap = rugiTahap.ToArray(),
                Label = label, Kepentingan = penting,
            };
        }

        public static int[] DugakanKelas(HasilKlas h, double[][] xx)
        {
            var keluar = new int[xx.Length];
            for (int i = 0; i < xx.Length; i++)
            {
                var suara = new double[h.Kelas.Count];
                for (int b = 0; b < h.Pohon.Count; b++)
                {
                    int d = Pohon.DugakanKelas(h.Pohon[b], new[] { xx[i] })[0];
                    suara[d] += h.Alfa[b];
                }
                int menang = 0;
                for (int c = 1; c < h.Kelas.Count; c++)
                    if (suara[c] > suara[menang]) menang = c;
                keluar[i] = menang;
            }
            return keluar;
        }

        public static double[] DugakanNilai(HasilReg h, double[][] xx)
        {
            var keluar = new double[xx.Length];
            for (int i = 0; i < xx.Length; i++)
            {
                double s = h.F0;
                for (int b = 0; b < h.Pohon.Count; b++)
                    s += h.Lr * Pohon.DugakanNilai(h.Pohon[b], new[] { xx[i] })[0];
                keluar[i] = s;
            }
            return keluar;
        }

        public static List<ResultBlock> BoostingBlocks(Dataset ds, List<string> fitur,
            string target, string mode, int ronde, double lr,
            int maksDalam, int minBelah)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Boosting (gradien / AdaBoost SAMME)", 1)
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
                DaftarRumus.BoostingGradien, DaftarRumus.BoostingSamme));

            int B = Math.Max(1, Math.Min(ronde, 2000));
            string batas = $"ronde {Fmt.Int(B)}, lr {Fmt.Num(Math.Min(Math.Max(lr, 1e-9), 1.0), 3)}, "
                           + $"kedalaman ≤ {Fmt.Int(maksDalam)}, belah ≥ {Fmt.Int(minBelah)}";
            if (!regresi)
            {
                var tk = ds.Text(target);
                var y = baris.Select(i => tk[i] ?? "(kosong)").ToArray();
                var h = PasangKlasifikasi(x, y, fitur, B, lr, maksDalam, minBelah);
                if (h is null)
                {
                    blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.",
                                           NoteKind.Error));
                    return blocks;
                }
                int benar = h.Label.Zip(y, (d, t) => h.Kelas[d] == t ? 1 : 0).Sum();
                blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                    ("Fitur", string.Join(", ", fitur)),
                    ("Target", $"{target} ({string.Join(", ", h.Kelas)})"),
                    ("Batas", batas),
                    ("Ronde terpakai", $"{Fmt.Int(h.Ronde)} dari {Fmt.Int(B)}"),
                    ("Akurasi latih", Fmt.Num((double)benar / y.Length, 4)),
                    ("Galat akhir", Fmt.Num(h.GalatTahap[^1], 4))));
                blocks.Add(Blocks.Table(
                    "Jejak ronde (galat latih tak-naik)",
                    new[] { "Ronde", "Galat", "Rugi eksponensial" },
                    h.GalatTahap.Select((g, i) => new[]
                        { Fmt.Int(i + 1), Fmt.Num(g, 4), Fmt.Num(h.RugiTahap[i], 4) })
                        .Take(12).ToArray(),
                    h.Ronde > 12 ? $"12 dari {h.Ronde} ronde ditampilkan." : "Semua ronde."));
                blocks.Add(Blocks.Table(
                    "Kepentingan fitur (rerata penurunan Gini)",
                    new[] { "Fitur", "Kepentingan" },
                    fitur.Select((f, j) => new[]
                        { f, Fmt.Num(h.Kepentingan[j], 4) }).ToArray(),
                    "Rerata per pohon; tiap pohon berjumlah 1 sebelum direrata."));
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
                var h = PasangRegresi(x, y, fitur, B, lr, maksDalam, minBelah);
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
                    ("R² latih akhir", Fmt.Num(h.R2Tahap[^1], 4)),
                    ("SSE akhir", Fmt.Num(h.SseTahap[^1], 4))));
                blocks.Add(Blocks.Table(
                    "Jejak ronde (SSE latih tak-naik)",
                    new[] { "Ronde", "SSE", "R²" },
                    h.SseTahap.Select((s, i) => new[]
                        { Fmt.Int(i + 1), Fmt.Num(s, 4), Fmt.Num(h.R2Tahap[i], 4) })
                        .Take(12).ToArray(),
                    h.Ronde > 12 ? $"12 dari {h.Ronde} ronde ditampilkan." : "Semua ronde."));
                blocks.Add(Blocks.Table(
                    "Kepentingan fitur (rerata penurunan SSE)",
                    new[] { "Fitur", "Kepentingan" },
                    fitur.Select((f, j) => new[]
                        { f, Fmt.Num(h.Kepentingan[j], 4) }).ToArray(),
                    "Rerata per pohon."));
            }
            return blocks;
        }
    }
}
