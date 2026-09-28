using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class NonparametrikBebas
    {
        #region Uji Kolmogorov–Smirnov dua sampel

        public class HasilKS2
        {
            public int N1;          
            public int N2;          
            public double D;        
            public double X;        
            public double P;        
        }

        public static HasilKS2? UjiKS2(List<double> a, List<double> b)
        {
            int n1 = a.Count, n2 = b.Count;
            if (n1 < 1 || n2 < 1) return null;

            var sa = a.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToList();
            var sb = b.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToList();
            int m1 = sa.Count, m2 = sb.Count;
            if (m1 < 1 || m2 < 1) return null;

            
            var titik = sa.Concat(sb).Distinct().OrderBy(v => v).ToList();
            double d = 0;
            foreach (var x in titik)
            {
                int ca = sa.Count(v => v <= x);
                int cb = sb.Count(v => v <= x);
                double fa = (double)ca / m1;
                double fb = (double)cb / m2;
                d = Math.Max(d, Math.Abs(fa - fb));
            }

            double en = (double)m1 * m2 / (m1 + m2);
            double xx = Math.Sqrt(en) * d;
            int nEf = (int)Math.Round(en, MidpointRounding.ToEven); 
            double p = KolmogorovDuaSisiSurvival(nEf, d);

            return new HasilKS2 { N1 = m1, N2 = m2, D = d, X = xx, P = p };
        }

        private static double KolmogorovDuaSisiSurvival(int n, double d)
        {
            if (d <= 0.0) return 1.0;
            if (d >= 1.0) return 0.0;
            double cdf = DurbinMatrixCdf(n, d);
            double sf = 1.0 - cdf;
            return sf < 0.0 ? 0.0 : (sf > 1.0 ? 1.0 : sf);
        }

        private static double DurbinMatrixCdf(int n, double d)
        {
            double nd = n * d;
            if (nd <= 0.5) return 0.0;
            int k = (int)Math.Ceiling(nd);
            double h = k - nd;
            int m = 2 * k - 1;
            
            
            if (m > 600)
                return 1.0 - KolmogorovLimitingSurvival(Math.Sqrt(n) * d);

            double[] H = new double[m * m];
            double[] v = new double[m];
            double[] w = new double[m];
            double fac = 1.0;
            for (int j = 1; j <= m; j++)
            {
                w[j - 1] = fac;
                fac /= j;
                v[j - 1] = (1.0 - Math.Pow(h, j)) * fac;
            }
            double tt = Math.Pow(Math.Max(2.0 * h - 1.0, 0.0), m) - 2.0 * Math.Pow(h, m);
            v[m - 1] = (1.0 + tt) * fac;

            
            for (int i = 1; i < m; i++)
                for (int c = 0; c <= m - i; c++)
                    H[((i - 1 + c) * m) + i] = w[c];
            
            for (int r = 0; r < m; r++) H[r * m + 0] = v[r];
            
            for (int c = 0; c < m; c++) H[(m - 1) * m + c] = v[m - 1 - c];

            double[] Hpwr = new double[m * m];
            for (int r = 0; r < m; r++) Hpwr[r * m + r] = 1.0;
            int nn = n;
            while (nn > 0)
            {
                if ((nn & 1) == 1) Hpwr = MatMul(Hpwr, H, m);
                H = MatMul(H, H, m);
                nn >>= 1;
            }
            double p = Hpwr[(k - 1) * m + (k - 1)];
            for (int i = 1; i <= n; i++) p = (double)i * p / n;
            return p < 0.0 ? 0.0 : (p > 1.0 ? 1.0 : p);
        }

        private static double[] MatMul(double[] A, double[] B, int m)
        {
            double[] C = new double[m * m];
            for (int r = 0; r < m; r++)
                for (int c = 0; c < m; c++)
                {
                    double s = 0.0;
                    for (int t = 0; t < m; t++) s += A[r * m + t] * B[t * m + c];
                    C[r * m + c] = s;
                }
            return C;
        }

        private static double KolmogorovLimitingSurvival(double x)
        {
            if (x <= 0.0) return 1.0;
            double jumlah = 0.0, suku;
            for (int j = 1; j < 400; j++)
            {
                suku = (j % 2 == 1 ? 1.0 : -1.0) * Math.Exp(-2.0 * j * j * x * x);
                jumlah += suku;
                if (Math.Abs(suku) < 1e-17) break;
            }
            return 2.0 * jumlah;
        }

        public static List<ResultBlock> KS2Blocks(HasilKS2 h, string namaA, string namaB)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Uji Kolmogorov–Smirnov dua sampel") };
            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.UjiKS2));
            blok.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Banyak kasus tiap sampel", $"n₁ = {h.N1}  (sampel '{namaA}'),  n₂ = {h.N2}  (sampel '{namaB}')"),
                ("Statistik D", $"D = sup |F̂₁(x) − F̂₂(x)| = {Fmt.Num(h.D, 4)}"),
                ("Skala", $"√(n₁·n₂/(n₁+n₂)) · D = {Fmt.Num(h.X, 4)}"),
                ("Nilai p", $"p = Q_KS(√(n₁n₂/(n₁+n₂))·D) = {Fmt.P(h.P)}")));
            blok.Add(Blocks.Table("Ringkasan dua sampel",
                new[] { "Sampel", "n" },
                new List<List<string>>
                {
                    new List<string> { namaA, h.N1.ToString() },
                    new List<string> { namaB, h.N2.ToString() }
                },
                $"D = {Fmt.Num(h.D, 4)}, p = {Fmt.P(h.P)} — sebaran Kolmogorov dua sisi berhingga-n (kstwo.sf)."));
            blok.Add(Blocks.Note(
                $"D = **{Fmt.Num(h.D, 4)}**, p = **{Fmt.P(h.P)}**"
                + (h.P < 0.05
                    ? " — kedua sebaran berbeda nyata pada taraf 5%."
                    : " — tidak cukup bukti bahwa kedua sebaran berbeda (taraf 5%).")
                + " Nilai p memakai pendekatan asimptotik sebaran Kolmogorov."));
            return blok;
        }

        #endregion

        #region Uji median (k sampel)

        public class HasilMedianTest
        {
            public int K;                      
            public int N;                      
            public double Median;              
            public int[,]? Tabel;              
            public List<string>? Kelompok;     
            public double Chi2;                
            public int Df;                     
            public double P;                   
        }

        public static HasilMedianTest? UjiMedianTest(List<List<double>> groups)
        {
            int k = groups.Count;
            if (k < 2) return null;

            var semua = groups.SelectMany(g => g).Where(v => !double.IsNaN(v)).ToList();
            int N = semua.Count;
            if (N < k + 1) return null;

            double median = Median(semua);
            var tabel = new int[2, k];
            for (int c = 0; c < k; c++)
            {
                int atas = 0, bawah = 0;
                foreach (var v in groups[c])
                {
                    if (double.IsNaN(v)) continue;
                    if (v > median) atas++; else bawah++;   
                }
                tabel[0, c] = atas;
                tabel[1, c] = bawah;
            }

            
            int[] barisJumlah = { 0, 0 };
            var kolomJumlah = new int[k];
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < k; c++)
                {
                    barisJumlah[r] += tabel[r, c];
                    kolomJumlah[c] += tabel[r, c];
                }

            double chi2 = 0;
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < k; c++)
                {
                    double e = (double)barisJumlah[r] * kolomJumlah[c] / N;
                    if (e > 0) chi2 += Math.Pow(tabel[r, c] - e, 2) / e;
                }
            int df = k - 1;
            double p = Distributions.ChiSquareUpper(chi2, df);

            return new HasilMedianTest
            {
                K = k, N = N, Median = median, Tabel = tabel,
                Chi2 = chi2, Df = df, P = p
            };
        }

        public static List<ResultBlock> MedianTestBlocks(HasilMedianTest h, List<string> namaKelompok)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Uji median (k sampel)") };
            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.MedianTest));

            var barisTabel = new List<List<string>>();
            for (int r = 0; r < 2; r++)
            {
                var baris = new List<string> { r == 0 ? "> median" : "≤ median" };
                for (int c = 0; c < h.K; c++) baris.Add(h.Tabel![r, c].ToString());
                barisTabel.Add(baris);
            }
            var kolomHead = new List<string> { "Posisi" };
            kolomHead.AddRange(namaKelompok);

            double eAtas = (double)h.N / 2.0;   
            blok.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Banyak kelompok & kasus", $"k = {h.K} kelompok, N = {h.N} kasus"),
                ("Median gabungan", $"M = {Fmt.Num(h.Median, 4)}"),
                ("Harapan tiap sel (simetris)", $"E ≈ {Fmt.Num(eAtas, 2)}")));
            blok.Add(Blocks.Table("Tabel pengamatan (di atas / di bawah median)",
                                  kolomHead.ToArray(), barisTabel,
                                  "Kasus sama dengan median digolongkan '≤ median'."));
            blok.Add(Blocks.Substitusi("Uji chi-kuadrat",
                ("Statistik", $"χ² = Σ (O−E)²/E = {Fmt.Num(h.Chi2, 4)}"),
                ("Derajat bebas", $"df = k − 1 = {h.Df}"),
                ("Nilai p", $"p = χ².sf({Fmt.Num(h.Chi2, 4)}, {h.Df}) = {Fmt.P(h.P)}")));
            blok.Add(Blocks.Note(
                $"χ²({h.Df}) = **{Fmt.Num(h.Chi2, 4)}**, p = **{Fmt.P(h.P)}**"
                + (h.P < 0.05
                    ? " — setidaknya satu median kelompok berbeda nyata pada 5%."
                    : " — tidak cukup bukti perbedaan median antar kelompok (5%).")
                + " Uji median lebih tahan pencilan daripada ANOVA, tetapi kurang bertenaga."));
            return blok;
        }

        #endregion

        #region Uji chi-kuadrat kesesuaian (goodness-of-fit)

        public class HasilChiGOF
        {
            public int K;                  
            public int N;                  
            public List<string>? Kategori; 
            public List<int>? Observed;    
            public List<double>? Expected; 
            public double Chi2;
            public int Df;                 
            public double P;
        }

        public static HasilChiGOF UjiChiGOF(List<string> kategori, List<int> observed, List<double> props)
        {
            int k = observed.Count;
            int N = observed.Sum();
            double jumlahProp = props.Sum();
            var norm = props.Select(p => p / jumlahProp).ToList();
            var expected = norm.Select(p => p * N).ToList();

            double chi2 = 0;
            for (int i = 0; i < k; i++)
            {
                double e = expected[i];
                if (e > 0) chi2 += Math.Pow(observed[i] - e, 2) / e;
            }
            int df = k - 1;
            double p = Distributions.ChiSquareUpper(chi2, df);

            return new HasilChiGOF
            {
                K = k, N = N, Kategori = kategori, Observed = observed,
                Expected = expected, Chi2 = chi2, Df = df, P = p
            };
        }

        public static List<ResultBlock> ChiGOFBlocks(HasilChiGOF h, string namaVar)
        {
            var blok = new List<ResultBlock> { Blocks.Heading($"Uji chi-kuadrat kesesuaian — {namaVar}") };
            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.ChiGOF));

            var baris = new List<List<string>>();
            for (int i = 0; i < h.K; i++)
                baris.Add(new List<string>
                {
                    h.Kategori![i],
                    h.Observed![i].ToString(),
                    Fmt.Num(h.Expected![i], 3)
                });
            blok.Add(Blocks.Table("Pengamatan vs harapan",
                new[] { "Kategori", "O (pengamatan)", "E (harapan)" }, baris,
                "Eᵢ = N · proporsi harapanᵢ; proporsi dinormalisasi supaya jumlah 1."));
            blok.Add(Blocks.Substitusi("Uji chi-kuadrat",
                ("Banyak kategori & kasus", $"k = {h.K}, N = {h.N}"),
                ("Statistik", $"χ² = Σ (O−E)²/E = {Fmt.Num(h.Chi2, 4)}"),
                ("Derajat bebas", $"df = k − 1 = {h.Df}"),
                ("Nilai p", $"p = χ².sf({Fmt.Num(h.Chi2, 4)}, {h.Df}) = {Fmt.P(h.P)}")));
            blok.Add(Blocks.Note(
                $"χ²({h.Df}) = **{Fmt.Num(h.Chi2, 4)}**, p = **{Fmt.P(h.P)}**"
                + (h.P < 0.05
                    ? " — distribusi kategori menyimpang nyata dari harapan pada 5%."
                    : " — tidak cukup bukti penyimpangan dari harapan (5%).")
                + " Uji ini dapat dipercaya bila sebagian besar Eᵢ ≥ 5."));
            return blok;
        }

        #endregion

        #region Bantuan konversi dataset

        public static List<List<double>> KelompokDouble(Dataset ds, string nilai, string faktor)
        {
            var dict = Compare.GroupValues(ds, nilai, faktor);
            var urutan = ds.Levels(faktor);
            var outp = new List<List<double>>();
            foreach (var lv in urutan)
                if (dict.TryGetValue(lv, out var l)) outp.Add(l);
            return outp;
        }

        public static (List<string> Kategori, List<int> Hitung) HitungKategori(Dataset ds, string var)
        {
            var level = ds.Levels(var);
            var teks = ds.Text(var);
            var hitung = level.ToDictionary(lv => lv, _ => 0);
            foreach (var t in teks)
                if (t is not null && hitung.ContainsKey(t)) hitung[t!]++;
            return (level, level.Select(lv => hitung[lv]).ToList());
        }

        private static double Median(List<double> x)
        {
            var s = x.OrderBy(v => v).ToList();
            int n = s.Count;
            return n % 2 == 1 ? s[n / 2] : 0.5 * (s[n / 2 - 1] + s[n / 2]);
        }

        #endregion
    }
}
