using System;
using System.Collections.Generic;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public class HasilBinomial
    {
        public int N;                
        public int K;                
        public double PHipotesis;    
        public double PeluangSukses; 
        public double PValue;
        public string Cara = "dua-sisi"; 
        public string? Catatan;
    }

    public static class Binomial
    {
        public const double TOL_PMF = 1e-12;

        public static double PmfLog(int k, int n, double p)
        {
            if (k < 0 || k > n) return double.NegativeInfinity;
            if (p <= 0 || p >= 1) return double.NegativeInfinity;
            return Special.LnGamma(n + 1) - Special.LnGamma(k + 1) - Special.LnGamma(n - k + 1)
                 + k * Math.Log(p) + (n - k) * Math.Log(1 - p);
        }

        public static double Pmf(int k, int n, double p) => Math.Exp(PmfLog(k, n, p));

        public static double Cdf(int k, int n, double p)
        {
            if (k < 0) return 0;
            if (k >= n) return 1;
            double sum = 0;
            for (int i = 0; i <= k; i++) sum += Pmf(i, n, p);
            return Math.Min(sum, 1);
        }

        public static HasilBinomial? Uji(int k, int n, double p = 0.5, string cara = "dua-sisi")
        {
            if (n < 1) return null;
            if (k < 0 || k > n) return null;
            if (p <= 0 || p >= 1) return null;

            var h = new HasilBinomial { N = n, K = k, PHipotesis = p,
                                         PeluangSukses = (double)k / n, Cara = cara };

            switch (cara)
            {
                case "kurang":
                    h.PValue = Cdf(k, n, p);
                    break;
                case "lebih":
                    
                    h.PValue = k == 0 ? 1.0 : 1.0 - Cdf(k - 1, n, p);
                    break;
                default: 
                    double refPmf = Pmf(k, n, p);
                    double sum = 0;
                    for (int j = 0; j <= n; j++)
                        if (Pmf(j, n, p) <= refPmf * (1 + TOL_PMF)) sum += Pmf(j, n, p);
                    h.PValue = Math.Min(sum, 1.0);
                    break;
            }

            
            
            
            if (n > 10000) h.Catatan = "n besar; pembuktian numerik dapat melambat.";
            return h;
        }

        public static List<ResultBlock> BinomialBlocks(HasilBinomial h)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Uji binomial") };

            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.UjiBinomial));

            blok.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Banyak percobaan", $"n = {h.N}"),
                ("Banyak sukses yang diobservasi", $"k = {h.K}"),
                ("Peluang sukses aktual", $"K / N = {Fmt.Num(h.PeluangSukses, 4)}"),
                ("Peluang yang dihipotesiskan", $"p₀ = {Fmt.Num(h.PHipotesis, 4)}"),
                ("Varian", h.Cara)));

            
            int tengah = Math.Min(h.N, 30);
            var baris = new List<List<string>>();
            for (int i = 0; i <= Math.Min(h.N, 5); i++) baris.Add(new List<string>
            {
                i.ToString(), Fmt.Num(Pmf(i, h.N, h.PHipotesis), 6),
                Fmt.Num(Cdf(i, h.N, h.PHipotesis), 6)
            });
            blok.Add(Blocks.Table($"Sebaran binomial p = {Fmt.Num(h.PHipotesis, 4)} (awal)",
                                  new[] { "k", "pmf(k)", "P(X ≤ k)" }, baris,
                                  $"P(X ≤ k) dijumlah sampai k = {tengah}; di atasnya penuh (1)."));

            blok.Add(Blocks.Note($"p-value ({h.Cara}) = **{h.PValue:E4}** "
                                 + (h.PValue < 0.05 ? "(nyata pada taraf 5%)" : "(tidak nyata pada taraf 5%)")
                                 + "."));

            return blok;
        }
    }
}
