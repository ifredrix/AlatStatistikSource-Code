using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class NonparametrikBerhubungan
    {
        #region Uji tanda (Sign test)

        public class HasilTanda
        {
            public int N;          
            public int Positif;    
            public int Negatif;    
            public int Nol;        
            public double P;       
            public string? Catatan;
        }

        public static HasilTanda? UjiTanda(List<double> x, List<double> y)
        {
            int n = Math.Min(x.Count, y.Count);
            int pos = 0, neg = 0, nol = 0;
            for (int i = 0; i < n; i++)
            {
                if (double.IsNaN(x[i]) || double.IsNaN(y[i])) continue;
                double d = x[i] - y[i];
                if (d > 0) pos++;
                else if (d < 0) neg++;
                else nol++;
            }
            int total = pos + neg;
            if (total < 1) return null;

            var h = new HasilTanda { N = total, Positif = pos, Negatif = neg, Nol = nol };
            
            var b = Binomial.Uji(Math.Min(pos, neg), total, 0.5, "dua-sisi");
            if (b is null) return null;
            h.P = b.PValue;
            if (total > 0 && Math.Min(pos, neg) < 1)
                h.Catatan = "Satu sisi sama sekali (semua tanda serupa); p = 1,0 bila dua sisi eksak.";
            return h;
        }

        public static List<ResultBlock> TandaBlocks(HasilTanda h)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Uji tanda (Sign test)") };
            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.UjiTanda));
            blok.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Banyak pasangan beda > 0", $"n₊ = {h.Positif}"),
                ("Banyak pasangan beda < 0", $"n₋ = {h.Negatif}"),
                ("Banyak pasangan beda = 0 (dibuang)", $"n₀ = {h.Nol}"),
                ("Total tanda", $"N = {h.N}")));
            blok.Add(Blocks.Table("Ringkasan tanda",
                new[] { "Tanda", "Banyak" },
                new List<List<string>>
                {
                    new() { "Positif (x > y)", h.Positif.ToString() },
                    new() { "Negatif (x < y)", h.Negatif.ToString() },
                    new() { "Nol (x = y)", h.Nol.ToString() },
                    new() { "Total", h.N.ToString() }
                },
                $"Uji dua sisi eksak (Binomial N={h.N}, p=0,5): p = {Fmt.P(h.P)}."));
            blok.Add(Blocks.Note(
                $"p-value (dua sisi eksak, Binomial N={h.N}, p=0,5) = **{h.P:E4}** "
                + (h.P < 0.05 ? "(nyata pada taraf 5%)." : "(tidak nyata pada taraf 5%).")
                + " Bila jumlah tanda positif dan negatif berbeda jauh, median bedanya "
                + "menyimpang dari 0."));
            return blok;
        }

        #endregion

        #region McNemar

        public class HasilMcNemar
        {
            public int N00, N01, N10, N11;     
            public double Chi2CC;               
            public double PExact;              
            public double PCC;                  
            public string? Catatan;
        }

        public static HasilMcNemar? UjiMcNemar(List<double> a, List<double> b)
        {
            int n = Math.Min(a.Count, b.Count);
            int n00 = 0, n01 = 0, n10 = 0, n11 = 0;
            for (int i = 0; i < n; i++)
            {
                if (double.IsNaN(a[i]) || double.IsNaN(b[i])) continue;
                
                int x = a[i] > 0.5 ? 1 : 0;
                int y = b[i] > 0.5 ? 1 : 0;
                if (x == 0 && y == 0) n00++;
                else if (x == 0 && y == 1) n01++;
                else if (x == 1 && y == 0) n10++;
                else n11++;
            }
            int d = n01 + n10;
            if (d < 1) return null;

            var h = new HasilMcNemar { N00 = n00, N01 = n01, N10 = n10, N11 = n11 };
            var eksak = Binomial.Uji(Math.Min(n01, n10), d, 0.5, "dua-sisi");
            h.PExact = eksak?.PValue ?? 1.0;

            double selisih = Math.Abs(n01 - n10);
            double chi2 = (d > 0) ? Math.Pow(selisih - 1, 2) / d : 0.0;
            h.Chi2CC = chi2;
            
            h.PCC = Special.Erfc(Math.Sqrt(chi2 / 2.0));
            return h;
        }

        public static List<ResultBlock> McNemarBlocks(HasilMcNemar h)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Uji McNemar") };
            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.UjiMcNemar));
            blok.Add(Blocks.Substitusi("Pemasukan nilai (tabel 2×2)",
                ("Konsisten: (0,0) dan (1,1)", $"{h.N00} dan {h.N11}"),
                ("Diskordan (0,1) = b", $"{h.N01}"),
                ("Diskordan (1,0) = c", $"{h.N10}"),
                ("χ² koreksi = (|b−c|−1)²/(b+c)", $"{Fmt.Num(h.Chi2CC, 4)}")));
            var baris = new List<List<string>>
            {
                new() { "0", h.N00.ToString(), h.N01.ToString() },
                new() { "1", h.N10.ToString(), h.N11.ToString() }
            };
            blok.Add(Blocks.Table("Tabel diskordansi (baris = a, kolom = b)",
                                  new[] { "a \\ b", "0", "1" }, baris));
            blok.Add(Blocks.Note(
                $"p eksak (binomial dua sisi pada b+c={h.N01 + h.N10}) = **{h.PExact:E4}**; "
                + $"p koreksi kesinambungan (χ²(1) = {Fmt.Num(h.Chi2CC, 4)}) = **{h.PCC:E4}**. "
                + (h.PExact < 0.05 ? "Beda antar dua pengukuran nyata pada 5%."
                                   : "Beda kedua pengukuran tidak nyata pada 5%.")));
            return blok;
        }

        #endregion

        #region Kendall's W

        public class HasilKendallW
        {
            public int K;          // banyak perlakuan (variabel)
            public int N;          // banyak subjek (blok)
            public double W;       // koefisien kesepakatan
            public double Chi2;    // k·(n−1)·W
            public double P;       // p dari chi²(n−1)
        }


        public static HasilKendallW? UjiKendallW(List<List<double>> groups)
        {
            int k = groups.Count;
            if (k < 3) return null;
            int n = groups.Min(g => g.Count);
            if (n < 2) return null;

            
            
            var Rj = new double[k];
            double sumTieCubes = 0;
            for (int i = 0; i < n; i++)
            {
                
                var baris = new List<(double v, int j)>();
                for (int j = 0; j < k && i < groups[j].Count; j++)
                    if (!double.IsNaN(groups[j][i])) baris.Add((groups[j][i], j));
                
                var urut = baris.OrderBy(t => t.v).ToList();
                for (int t = 0; t < urut.Count; )
                {
                    int u = t;
                    while (u + 1 < urut.Count && urut[u + 1].v == urut[t].v) u++;
                    double rata = (t + 1 + u + 1) / 2.0;
                    int ukuran = u - t + 1;
                    if (ukuran > 1) sumTieCubes += (double)ukuran * ukuran * ukuran - ukuran;
                    for (int idx = t; idx <= u; idx++) Rj[urut[idx].j] += rata;
                    t = u + 1;
                }
            }

            
            double ssbn = Rj.Sum(R => R * R);
            double chi2unc = 12.0 * ssbn / (k * n * (k + 1.0)) - 3.0 * n * (k + 1.0);
            double koreksi = 1.0 - sumTieCubes / (k * (k * (double)k - 1.0) * n);
            if (koreksi <= 1e-12) koreksi = 1.0;
            double chi2 = chi2unc / koreksi;

            
            
            double W = chi2 / (k * (n - 1.0));
            double p = Special.GammaQ((n - 1) / 2.0, chi2 / 2.0);

            return new HasilKendallW { K = k, N = n, W = W, Chi2 = chi2, P = p };
        }

        public static List<ResultBlock> KendallWBlocks(HasilKendallW h)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Uji kesepakatan Kendall's W") };
            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.KendallW));
            blok.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Banyak perlakuan (k)", $"k = {h.K}"),
                ("Banyak subjek (n)", $"n = {h.N}"),
                ("χ² = k·(n−1)·W", $"{Fmt.Num(h.Chi2, 4)}")));
            blok.Add(Blocks.Table("Ringkasan Kendall's W",
                new[] { "Ukuran", "Nilai" },
                new List<List<string>>
                {
                    new() { "k (penilai)", h.K.ToString() },
                    new() { "n (subjek)", h.N.ToString() },
                    new() { "W", Fmt.Num(h.W, 4) },
                    new() { "chi-kuadrat", Fmt.Num(h.Chi2, 4) },
                    new() { "df", (h.N - 1).ToString() },
                    new() { "p", Fmt.P(h.P) }
                }));
            blok.Add(Blocks.Note(
                $"Kendall's W = **{Fmt.Num(h.W, 4)}**, χ²({h.N - 1}) = {Fmt.Num(h.Chi2, 4)}, "
                + $"p = **{h.P:E4}**" + (h.P < 0.05 ? " (nyata)." : " (tidak nyata).")
                + " W mendekati 1 bila para penilai sepakat sepenuhnya."));
            return blok;
        }

        #endregion

        #region Cochran's Q

        public class HasilCochranQ
        {
            public int K;
            public int N;
            public double Q;       // statistik Cochran
            public double P;       // p dari chi²(k−1)
        }


        public static HasilCochranQ? UjiCochranQ(List<List<double>> groups)
        {
            int k = groups.Count;
            if (k < 2) return null;
            int n = groups.Min(g => g.Count);
            if (n < 2) return null;

            var Gj = new double[k];
            var Li = new double[n];
            int G = 0;
            for (int j = 0; j < k; j++)
                for (int i = 0; i < n; i++)
                {
                    int v = double.IsNaN(groups[j][i]) ? 0 : (groups[j][i] > 0.5 ? 1 : 0);
                    Gj[j] += v; Li[i] += v; G += v;
                }

            double sumG2 = Gj.Sum(g => g * g);
            double sumL2 = Li.Sum(l => l * l);
            double penyebut = k * G - sumL2;
            if (penyebut <= 0) return null;

            double Q = (k - 1.0) * (k * sumG2 - G * G) / penyebut;
            double p = Special.GammaQ((k - 1) / 2.0, Q / 2.0);
            return new HasilCochranQ { K = k, N = n, Q = Q, P = p };
        }

        public static List<ResultBlock> CochranQBlocks(HasilCochranQ h)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Uji Cochran's Q") };
            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.CochranQ));
            blok.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Banyak ukuran (k)", $"k = {h.K}"),
                ("Banyak subjek (n)", $"n = {h.N}"),
                ("Q = (k−1)(k·ΣG_j² − G²) / (kG − ΣL_i²)", Fmt.Num(h.Q, 4))));
            blok.Add(Blocks.Table("Ringkasan Cochran's Q",
                new[] { "Ukuran", "Nilai" },
                new List<List<string>>
                {
                    new() { "k (ukuran)", h.K.ToString() },
                    new() { "n (subjek)", h.N.ToString() },
                    new() { "Q", Fmt.Num(h.Q, 4) },
                    new() { "df", (h.K - 1).ToString() },
                    new() { "p", Fmt.P(h.P) }
                }));
            blok.Add(Blocks.Note(
                $"Cochran's Q = **{Fmt.Num(h.Q, 4)}**, df = {h.K - 1}, p = **{h.P:E4}**"
                + (h.P < 0.05 ? " (setidaknya satu ukuran berbeda nyata)."
                               : " (tidak ada ukuran yang berbeda nyata).")));
            return blok;
        }

        #endregion

        #region Bantuan konversi dataset → daftar angka

        public static List<double> KolomDouble(Dataset ds, string nama)
        {
            var kol = ds.Numeric(nama);
            var outp = new List<double>(kol.Length);
            foreach (var v in kol) outp.Add(v ?? double.NaN);
            return outp;
        }

        public static List<List<double>> KolomKelompokDouble(Dataset ds, List<string> nama)
        {
            var outp = new List<List<double>>(nama.Count);
            foreach (var nm in nama) outp.Add(KolomDouble(ds, nm));
            return outp;
        }

        #endregion
    }
}
