using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Proporsi
    {
        
        public class HasilPropSatu
        {
            public string Variabel = "";
            public string Sukses = "";
            public int N, X;
            public double P0, Phat;
            public double Z, PNormal;
            public double WilsonLo, WilsonHi;
            public double PBinomial;
        }

        public class HasilPropDua
        {
            public string Variabel = "", Sukses = "", Grup = "";
            public string G1 = "", G2 = "";
            public int N1, X1, N2, X2;
            public double P1, P2;
            public double WilsonLo1, WilsonHi1, WilsonLo2, WilsonHi2;
            public double Selisih, SePool, Z, PNormal;
            public double SelisihLo, SelisihHi;
            public double Chi2, PChi;
            public double PFisher;
            public double OR, ORLo, ORHi;
            public double RR, RRLo, RRHi;
        }

        
        static List<string> Bersih(Dataset ds, string nama)
        {
            var t = ds.Text(nama);
            var outp = new List<string>();
            foreach (var v in t)
                if (!string.IsNullOrWhiteSpace(v)) outp.Add(v!.Trim());
            return outp;
        }

        static List<string> Level(IEnumerable<string> nilai)
        {
            var set = new HashSet<string>(nilai);
            bool semuaAngka = set.All(s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _));
            if (semuaAngka)
                return set.OrderBy(s => double.Parse(s, CultureInfo.InvariantCulture)).ToList();
            return set.OrderBy(s => s, StringComparer.Ordinal).ToList();
        }

        static (double lo, double hi) Wilson(double p, int n, double z)
        {
            if (n <= 0) return (double.NaN, double.NaN);
            double z2 = z * z;
            double denom = 1 + z2 / n;
            double center = (p + z2 / (2.0 * n)) / denom;
            double half = z / denom * Math.Sqrt(p * (1 - p) / n + z2 / (4.0 * n * n));
            return (center - half, center + half);
        }

        static double LnKombinasi(int n, int k)
            => Special.LnGamma(n + 1) - Special.LnGamma(k + 1) - Special.LnGamma(n - k + 1);

        static double FisherDuaSisi(int a, int b, int c, int d)
        {
            int N = a + b + c + d;
            int tSucc = a + c;   
            int tG1 = a + b;     
            int lo = Math.Max(0, tSucc - (N - tG1));
            int hi = Math.Min(tG1, tSucc);
            double obsP = HyperPmf(a, b, c, d, N, tSucc, tG1);
            if (obsP <= 0) return 1.0;
            double sum = 0;
            for (int k = lo; k <= hi; k++)
            {
                int ka = k, kb = tG1 - k, kc = tSucc - k, kd = (N - tG1) - (tSucc - k);
                double p = HyperPmf(ka, kb, kc, kd, N, tSucc, tG1);
                if (p <= obsP * (1 + 1e-12)) sum += p;
            }
            return Math.Min(sum, 1.0);
        }

        static double HyperPmf(int a, int b, int c, int d, int N, int tSucc, int tG1)
        {
            double num = LnKombinasi(tSucc, a) + LnKombinasi(N - tSucc, tG1 - a);
            double den = LnKombinasi(N, tG1);
            return Math.Exp(num - den);
        }

        
        public static HasilPropSatu? HitungSatu(Dataset ds, string varNama, double p0, double alpha = 0.05)
        {
            var nilai = Bersih(ds, varNama);
            if (nilai.Count == 0) return null;
            var level = Level(nilai);
            if (level.Count < 1) return null;
            string sukses = level[0];
            int x = nilai.Count(v => v == sukses);
            int n = nilai.Count;
            double phat = (double)x / n;
            double zKritis = Distributions.NormalInv(1 - alpha / 2);

            double z = p0 > 0 && p0 < 1 ? (phat - p0) / Math.Sqrt(p0 * (1 - p0) / n) : double.NaN;
            double pNormal = double.IsNaN(z) ? double.NaN : 2 * (1 - Distributions.NormalCdf(Math.Abs(z)));
            var (lo, hi) = Wilson(phat, n, zKritis);
            var b = Binomial.Uji(x, n, p0, "dua-sisi");

            return new HasilPropSatu
            {
                Variabel = varNama,
                Sukses = sukses,
                N = n,
                X = x,
                P0 = p0,
                Phat = phat,
                Z = z,
                PNormal = pNormal,
                WilsonLo = lo,
                WilsonHi = hi,
                PBinomial = b?.PValue ?? double.NaN
            };
        }

        
        public static HasilPropDua? HitungDua(Dataset ds, string varKeluar, string varGrup, double alpha = 0.05)
        {
            var o = ds.Text(varKeluar);
            var g = ds.Text(varGrup);
            var pasangan = new List<(string O, string G)>();
            for (int i = 0; i < o.Length; i++)
            {
                string? ov = o[i];
                string? gv = g[i];
                if (string.IsNullOrWhiteSpace(ov) || string.IsNullOrWhiteSpace(gv)) continue;
                pasangan.Add((ov.Trim(), gv.Trim()));
            }
            if (pasangan.Count == 0) return null;

            var levelO = Level(pasangan.Select(p => p.O));
            var levelG = Level(pasangan.Select(p => p.G));
            if (levelO.Count != 2 || levelG.Count != 2) return null;

            string sukses = levelO[0];
            string g1 = levelG[0], g2 = levelG[1];

            int x1 = 0, n1 = 0, x2 = 0, n2 = 0;
            foreach (var (O, G) in pasangan)
            {
                if (G == g1) { n1++; if (O == sukses) x1++; }
                else if (G == g2) { n2++; if (O == sukses) x2++; }
            }
            if (n1 == 0 || n2 == 0) return null;

            double p1 = (double)x1 / n1;
            double p2 = (double)x2 / n2;
            double zKritis = Distributions.NormalInv(1 - alpha / 2);
            var (lo1, hi1) = Wilson(p1, n1, zKritis);
            var (lo2, hi2) = Wilson(p2, n2, zKritis);

            double selisih = p1 - p2;
            double pPool = (double)(x1 + x2) / (n1 + n2);
            double sePool = Math.Sqrt(pPool * (1 - pPool) * (1.0 / n1 + 1.0 / n2));
            double z = sePool > 0 ? selisih / sePool : double.NaN;
            double pNormal = double.IsNaN(z) ? double.NaN : 2 * (1 - Distributions.NormalCdf(Math.Abs(z)));

            
            
            int a = x1, b = n1 - x1, c = x2, d = n2 - x2;
            double chi2 = (n1 + n2) * Math.Pow((double)a * d - (double)b * c, 2)
                         / ((a + b) * (c + d) * (a + c) * (b + d));
            double pChi = Distributions.ChiSquareUpper(chi2, 1);
            double pFisher = FisherDuaSisi(a, b, c, d);

            
            double or = (a * (double)d) / ((b * (double)c));
            double lnOr = Math.Log(or);
            double seLnOr = Math.Sqrt(1.0 / a + 1.0 / b + 1.0 / c + 1.0 / d);
            double orLo = Math.Exp(lnOr - zKritis * seLnOr);
            double orHi = Math.Exp(lnOr + zKritis * seLnOr);

            double rr = p1 / p2;
            double lnRr = Math.Log(rr);
            double seLnRr = Math.Sqrt((1 - p1) / (p1 * n1) + (1 - p2) / (p2 * n2));
            double rrLo = Math.Exp(lnRr - zKritis * seLnRr);
            double rrHi = Math.Exp(lnRr + zKritis * seLnRr);

            return new HasilPropDua
            {
                Variabel = varKeluar,
                Sukses = sukses,
                Grup = varGrup,
                G1 = g1,
                G2 = g2,
                N1 = n1, X1 = x1, N2 = n2, X2 = x2,
                P1 = p1, P2 = p2,
                WilsonLo1 = lo1, WilsonHi1 = hi1, WilsonLo2 = lo2, WilsonHi2 = hi2,
                Selisih = selisih, SePool = sePool, Z = z, PNormal = pNormal,
                SelisihLo = selisih - zKritis * sePool,
                SelisihHi = selisih + zKritis * sePool,
                Chi2 = chi2, PChi = pChi, PFisher = pFisher,
                OR = or, ORLo = orLo, ORHi = orHi,
                RR = rr, RRLo = rrLo, RRHi = rrHi
            };
        }

        
        public static List<ResultBlock> SatuBlocks(Dataset ds, string varNama, double p0, double alpha = 0.05)
        {
            var h = HitungSatu(ds, varNama, p0, alpha);
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading($"Uji proporsi satu sampel ({varNama})", 1)
            };
            if (h == null)
            {
                blocks.Add(Blocks.Note("Data tidak cukup atau variabel tidak ditemukan.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.UjiProporsiSatu, DaftarRumus.SelangWilson));
            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Kategori sukses (level pertama)", $"\"{h.Sukses}\""),
                ("Banyak amatan (n)", $"n = {h.N}"),
                ("Banyak sukses (k)", $"k = {h.X}"),
                ("Proporsi teramati (p̂)", $"p̂ = {Fmt.Num(h.Phat, 4)}"),
                ("Proporsi uji (H₀)", $"p₀ = {Fmt.Num(h.P0, 4)}"),
                ("Statistik z (uji normal)", $"z = {Fmt.Num(h.Z, 4)}"),
                ("Batas Wilson 95%", $"[{Fmt.Num(h.WilsonLo, 4)}, {Fmt.Num(h.WilsonHi, 4)}]")));

            blocks.Add(Blocks.Table("Uji proporsi satu sampel",
                new[] { "Ukuran", "Nilai" },
                new List<List<string>>
                {
                    new() { "Banyak amatan (n)", h.N.ToString() },
                    new() { "Banyak sukses (k)", h.X.ToString() },
                    new() { "Proporsi teramati (p̂)", Fmt.Num(h.Phat, 4) },
                    new() { "Batas bawah Wilson 95%", Fmt.Num(h.WilsonLo, 4) },
                    new() { "Batas atas Wilson 95%", Fmt.Num(h.WilsonHi, 4) },
                    new() { "p binomial eksak (dua sisi)", Fmt.P(h.PBinomial) },
                    new() { "Statistik z", Fmt.Num(h.Z, 4) },
                    new() { "p uji normal (z)", Fmt.P(h.PNormal) }
                },
                Compare.Keputusan(h.PBinomial, alpha)));

            blocks.Add(Blocks.Note(
                "Uji binomial eksak memakai sebaran binomial tepat (padanan 'Binomial' dan "
                + "scipy.stats.binomtest). Selang Wilson lebih andal daripada Wald untuk n kecil. "
                + "Uji normal memakai z = (p̂ − p₀)/√(p₀(1−p₀)/n)."));
            return blocks;
        }

        public static List<ResultBlock> DuaBlocks(Dataset ds, string varKeluar, string varGrup, double alpha = 0.05)
        {
            var h = HitungDua(ds, varKeluar, varGrup, alpha);
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading($"Uji proporsi dua sampel ({varKeluar} menurut {varGrup})", 1)
            };
            if (h == null)
            {
                blocks.Add(Blocks.Note("Perlu tepat dua kategori pada variabel keluar dan pada variabel kelompok, serta baris yang cukup.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.UjiProporsiDua, DaftarRumus.SelangWilson, DaftarRumus.RasioPeluang));
            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Kategori sukses (level pertama keluar)", $"\"{h.Sukses}\""),
                ($"Kelompok 1 ({h.G1})", $"n₁ = {h.N1}, k₁ = {h.X1}, p̂₁ = {Fmt.Num(h.P1, 4)}"),
                ($"Kelompok 2 ({h.G2})", $"n₂ = {h.N2}, k₂ = {h.X2}, p̂₂ = {Fmt.Num(h.P2, 4)}"),
                ("Selisih (p̂₁ − p̂₂)", $"d = {Fmt.Num(h.Selisih, 4)}"),
                ("Statistik z", $"z = {Fmt.Num(h.Z, 4)}"),
                ("Chi-kuadrat Pearson", $"χ² = {Fmt.Num(h.Chi2, 4)}"),
                ("Odds ratio", $"OR = {Fmt.Num(h.OR, 4)}"),
                ("Risk ratio", $"RR = {Fmt.Num(h.RR, 4)}")));

            blocks.Add(Blocks.Table("Proporsi tiap kelompok",
                new[] { "Kelompok", "n", "Sukses", "p̂", "Wilson bawah", "Wilson atas" },
                new List<List<string>>
                {
                    new() { h.G1, h.N1.ToString(), h.X1.ToString(), Fmt.Num(h.P1, 4),
                            Fmt.Num(h.WilsonLo1, 4), Fmt.Num(h.WilsonHi1, 4) },
                    new() { h.G2, h.N2.ToString(), h.X2.ToString(), Fmt.Num(h.P2, 4),
                            Fmt.Num(h.WilsonLo2, 4), Fmt.Num(h.WilsonHi2, 4) }
                }));

            blocks.Add(Blocks.Table("Perbandingan dua proporsi",
                new[] { "Ukuran", "Nilai" },
                new List<List<string>>
                {
                    new() { "Selisih (p̂₁ − p̂₂)", Fmt.Num(h.Selisih, 4) },
                    new() { "SE (dipool)", Fmt.Num(h.SePool, 4) },
                    new() { $"Batas {Fmt.Num(100*(1-alpha),0)}% selisih", $"[{Fmt.Num(h.SelisihLo, 4)}, {Fmt.Num(h.SelisihHi, 4)}]" },
                    new() { "Statistik z", Fmt.Num(h.Z, 4) },
                    new() { "p uji normal (z)", Fmt.P(h.PNormal) },
                    new() { "Chi-kuadrat Pearson", Fmt.Num(h.Chi2, 4) },
                    new() { "p chi-kuadrat", Fmt.P(h.PChi) },
                    new() { "p Fisher eksak (dua sisi)", Fmt.P(h.PFisher) },
                    new() { "Odds ratio", Fmt.Num(h.OR, 4) },
                    new() { $"Batas {Fmt.Num(100*(1-alpha),0)}% OR", $"[{Fmt.Num(h.ORLo, 4)}, {Fmt.Num(h.ORHi, 4)}]" },
                    new() { "Risk ratio", Fmt.Num(h.RR, 4) },
                    new() { $"Batas {Fmt.Num(100*(1-alpha),0)}% RR", $"[{Fmt.Num(h.RRLo, 4)}, {Fmt.Num(h.RRHi, 4)}]" }
                },
                Compare.Keputusan(h.PNormal, alpha)));

            blocks.Add(Blocks.Note(
                "Selisih memakai sebaran normal dengan galat baku dipool. Chi-kuadrat Pearson tanpa "
                + "koreksi menyamai scipy.stats.chi2_contingency(correction=False); p Fisher eksak "
                + "menyamai scipy.stats.fisher_exact(alternative='two-sided'). Odds ratio = (k₁(n₂−k₂))/((n₁−k₁)k₂)."));
            return blocks;
        }
    }
}
