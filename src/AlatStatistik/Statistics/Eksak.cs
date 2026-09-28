using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class Eksak
    {
        public const int BatasCacah = 2000000;

        // Batas jumlah simpul yang dijelajahi. Tabel lebar (banyak aras di kedua
        // sisi) menumbuhkan pohon penjelajahan jauh lebih cepat daripada jumlah
        // tabelnya, sehingga batas cacah saja tidak pernah tercapai tepat waktu.
        public const int LangkahMaks = 2000000;
        public const ulong Benih = Bootstrap.BenihBawaan;

        public sealed class Hasil
        {
            public List<string> Baris = new();
            public List<string> Kolom = new();
            public int[,] Tabel = new int[0, 0];
            public int[] Rs = Array.Empty<int>();
            public int[] Cs = Array.Empty<int>();
            public int N;
            public long NTabel;
            public double LogP0;
            public bool AdaEksak;
            public double PExact;
            public double JumlahPeluang = double.NaN;
            public int Ulangan;
            public long McHitung;
            public double PMc = double.NaN;
            public double SeMc = double.NaN;
            public double CiBawahMc = double.NaN;
            public double CiAtasMc = double.NaN;
        }

        private static double[] LogFak(int n)
        {
            var lf = new double[n + 1];
            double s = 0;
            lf[0] = 0;
            for (int k = 1; k <= n; k++) { s += Math.Log(k); lf[k] = s; }
            return lf;
        }

        private static double LogP(int[,] t, int[] rs, int[] cs, double[] lf)
        {
            double s = 0;
            foreach (int v in rs) s += lf[v];
            foreach (int v in cs) s += lf[v];
            s -= lf[rs.Sum()];
            foreach (int v in t) s -= lf[v];
            return s;
        }

        // Peluang tiap tabel dijumlahkan begitu tabel itu terbentuk. Menampung
        // jutaan tabel dulu baru menjumlahkannya menghabiskan memori jauh
        // sebelum hasilnya terpakai.
        private sealed class AkumPeluang
        {
            private readonly int[] _rs, _cs;
            private readonly double[] _lf;
            private readonly double _logP0;
            public double Jumlah, Cocok;

            public AkumPeluang(int[] rs, int[] cs, double[] lf, double logP0)
            {
                _rs = rs; _cs = cs; _lf = lf; _logP0 = logP0;
            }

            public void Tambah(int[,] t)
            {
                double lp = LogP(t, _rs, _cs, _lf);
                double p = Math.Exp(lp);
                Jumlah += p;
                if (Math.Exp(lp - _logP0) <= 1 + 1e-9) Cocok += p;
            }
        }

        private static void Rek(int r, int c, int R, int C, int[] sisaR,
                                int[] sisaC, int[,] grid, AkumPeluang? akum,
                                ref long cacah, long batas, ref long langkah)
        {
            // Dua pembatas: cacah menghentikan penjelajahan setelah cukup banyak
            // tabel terkumpul, langkah menghentikannya bila pohonnya terlalu
            // lebar. Batas kedua yang penting: tabel 20x20 dengan 20 amatan
            if (++langkah > LangkahMaks) return;
            if (cacah > batas) return;
            if (r == R - 1 && c == C - 1)
            {
                if (sisaR[r] == sisaC[c])
                {
                    grid[r, c] = sisaR[r];
                    cacah++;
                    if (cacah <= batas) akum?.Tambah(grid);
                }
                return;
            }
            if (c == C - 1)
            {
                int v = sisaR[r];
                if (v >= 0 && v <= sisaC[c])
                {
                    grid[r, c] = v;
                    var nr = (int[])sisaR.Clone(); nr[r] -= v;
                    var nc = (int[])sisaC.Clone(); nc[c] -= v;
                    Rek(r + 1, 0, R, C, nr, nc, grid, akum, ref cacah, batas, ref langkah);
                }
                return;
            }
            if (r == R - 1)
            {
                int v = sisaC[c];
                if (v >= 0 && v <= sisaR[r])
                {
                    grid[r, c] = v;
                    var nr = (int[])sisaR.Clone(); nr[r] -= v;
                    var nc = (int[])sisaC.Clone(); nc[c] -= v;
                    Rek(r, c + 1, R, C, nr, nc, grid, akum, ref cacah, batas, ref langkah);
                }
                return;
            }
            for (int v = 0; v <= Math.Min(sisaR[r], sisaC[c]); v++)
            {
                if (cacah > batas || langkah > LangkahMaks) break;
                grid[r, c] = v;
                var nr = (int[])sisaR.Clone(); nr[r] -= v;
                var nc = (int[])sisaC.Clone(); nc[c] -= v;
                if (c + 1 < C) Rek(r, c + 1, R, C, nr, nc, grid, akum, ref cacah, batas, ref langkah);
                else Rek(r + 1, 0, R, C, nr, nc, grid, akum, ref cacah, batas, ref langkah);
            }
        }

        public static Hasil? Pasang(string[] baris, string[] kolom,
                                    bool pakaiEksak, bool pakaiMc, int ulangan)
        {
            int n = baris.Length;
            if (n < 4 || kolom.Length != n) return null;
            var lvB = baris.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            var lvK = kolom.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (lvB.Count < 2 || lvK.Count < 2) return null;
            int R = lvB.Count, C = lvK.Count;
            var tab = new int[R, C];
            for (int t = 0; t < n; t++)
                tab[lvB.IndexOf(baris[t]), lvK.IndexOf(kolom[t])]++;
            var rs = new int[R];
            var cs = new int[C];
            for (int i = 0; i < R; i++)
                for (int j = 0; j < C; j++) { rs[i] += tab[i, j]; cs[j] += tab[i, j]; }

            var h = new Hasil
            {
                Baris = lvB, Kolom = lvK, Tabel = tab, Rs = rs, Cs = cs, N = n,
                Ulangan = Math.Min(100000, Math.Max(100, ulangan)),
            };
            var lf = LogFak(n);
            h.LogP0 = LogP(tab, rs, cs, lf);

            if (pakaiEksak)
            {
                var akum = new AkumPeluang(rs, cs, lf, h.LogP0);
                long cacah = 0, langkah = 0;
                Rek(0, 0, R, C, (int[])rs.Clone(), (int[])cs.Clone(),
                    new int[R, C], akum, ref cacah, BatasCacah, ref langkah);
                h.NTabel = cacah;

                // Penjelajahan yang dipotong tidak boleh dilaporkan sebagai
                // p eksak: sebagian tabel belum dihitung, jadi pembilang dan
                // penyebutnya sama-sama kurang dan hasilnya menyesatkan.
                bool lengkap = langkah <= LangkahMaks && cacah <= BatasCacah;
                if (lengkap)
                {
                    h.JumlahPeluang = akum.Jumlah;
                    h.PExact = akum.Cocok / akum.Jumlah;
                    h.AdaEksak = true;
                }
            }

            if (pakaiMc)
            {
                var acak = new Bootstrap.Acak(Benih);
                var idx = Enumerable.Range(0, n).ToArray();
                long hitung = 0;
                var tt = new int[R, C];
                for (int b = 0; b < h.Ulangan; b++)
                {
                    for (int t = 0; t < n; t++) idx[t] = t;
                    for (int i = n - 1; i >= 1; i--)
                    {
                        int j = acak.Indeks(i + 1);
                        (idx[i], idx[j]) = (idx[j], idx[i]);
                    }
                    Array.Clear(tt, 0, tt.Length);
                    for (int t = 0; t < n; t++)
                        tt[lvB.IndexOf(baris[t]), lvK.IndexOf(kolom[idx[t]])]++;
                    if (LogP(tt, rs, cs, lf) <= h.LogP0 + 1e-12) hitung++;
                }
                h.McHitung = hitung;
                h.PMc = (double)hitung / h.Ulangan;
                h.SeMc = Math.Sqrt(h.PMc * (1 - h.PMc) / h.Ulangan);
                h.CiBawahMc = Math.Max(0, h.PMc - 2.576 * h.SeMc);
                h.CiAtasMc = Math.Min(1, h.PMc + 2.576 * h.SeMc);
            }
            return h;
        }

        public static List<ResultBlock> EksakBlocks(Dataset ds, string baris,
            string kolom, string metode, int ulangan)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Uji eksak R × C (Fisher–Freeman–Halton)", 1)
            };
            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Eksak));

            var tb = ds.Text(baris);
            var tk = ds.Text(kolom);
            int n = tb.Length;
            var b = new string[n];
            var k = new string[n];
            for (int i = 0; i < n; i++)
            {
                if (string.IsNullOrWhiteSpace(tb[i]) || string.IsNullOrWhiteSpace(tk[i]))
                {
                    blocks.Add(Blocks.Note("Ada sel hilang pada baris/kolom.",
                                           NoteKind.Error));
                    return blocks;
                }
                b[i] = tb[i]!.Trim();
                k[i] = tk[i]!.Trim();
            }
            bool mc = metode.StartsWith("Monte", StringComparison.OrdinalIgnoreCase)
                      || metode.StartsWith("Keduanya", StringComparison.OrdinalIgnoreCase)
                      || metode.StartsWith("Both", StringComparison.OrdinalIgnoreCase);
            bool eksak = !metode.StartsWith("Monte", StringComparison.OrdinalIgnoreCase);
            var h = Pasang(b, k, eksak, mc, ulangan);
            if (h is null)
            {
                blocks.Add(Blocks.Note("Tabel tidak bisa dibentuk (perlu ≥ 2 aras).",
                                       NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Baris", $"{baris} ({string.Join(", ", h.Baris)})"),
                ("Kolom", $"{kolom} ({string.Join(", ", h.Kolom)})"),
                ("Banyak amatan", $"n = {Fmt.Int(h.N)}"),
                ("Banyak tabel semargin",
                 h.NTabel > 0 ? Fmt.Int((int)Math.Min(h.NTabel, int.MaxValue)) : "—")));

            blocks.Add(Blocks.Table(
                "Tabel teramati",
                new[] { $"{baris} \\ {kolom}" }.Concat(h.Kolom).ToArray(),
                h.Baris.Select((r, i) => new[] { r }.Concat(
                    Enumerable.Range(0, h.Kolom.Count).Select(j =>
                        Fmt.Int(h.Tabel[i, j]))).ToArray()).ToArray(),
                "Cacah sel. Uji bersyarat pada total baris/kolom (margin)."));

            if (h.AdaEksak)
                blocks.Add(Blocks.Table(
                    "Uji eksak (enumerasi penuh)",
                    new[] { "Ukuran", "Nilai" },
                    new[]
                    {
                        new[] { "p eksak", Fmt.P(h.PExact) },
                        new[] { "Keputusan (α = 0,05)",
                                Compare.Keputusan(h.PExact, 0.05) },
                    },
                    $"p = Σ P(T) atas {Fmt.Int((int)h.NTabel)} tabel dengan "
                    + "P(T) ≤ P(T₀)."));
            else if (eksak)
                blocks.Add(Blocks.Note("Ruang tabel melebihi batas cacah "
                                       + $"({Fmt.Int(BatasCacah)}); hanya Monte Carlo "
                                       + "yang dilaporkan.", NoteKind.Warning));

            if (!double.IsNaN(h.PMc))
                blocks.Add(Blocks.Table(
                    $"Monte Carlo eksak ({Fmt.Int(h.Ulangan)} ulangan, benih tetap)",
                    new[] { "Ukuran", "Nilai" },
                    new[]
                    {
                        new[] { "p Monte Carlo", Fmt.P(h.PMc) },
                        new[] { "Galat baku", Fmt.Num(h.SeMc, 4) },
                        new[] { "SK 99%", $"{Fmt.P(h.CiBawahMc)} – {Fmt.P(h.CiAtasMc)}" },
                    },
                    "Proporsi tabel permutasi dengan P(T) ≤ P(T₀); menaksir "
                    + "p eksak (Hope 1968). Benih tetap → bisa diulang persis."));

            return blocks;
        }
    }
}
