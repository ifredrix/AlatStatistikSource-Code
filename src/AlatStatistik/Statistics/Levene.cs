using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Levene
    {
        public class HasilLevene
        {
            public int K;
            public int N;
            public double WMean, PMean;
            public double WMedian, PMedian;
            public double WTrim, PTrim;
            public double WMedAdj, Df2MedAdj, PMedAdj;
        }

        public static HasilLevene? Hitung(Dataset ds, string nilaiVar, string grupVar)
        {
            var groups = Compare.GroupValues(ds, nilaiVar, grupVar);
            if (groups.Count < 2) return null;

            var h = new HasilLevene
            {
                K = groups.Count,
                N = groups.Values.Sum(g => g.Count)
            };

            h.WMean = Compare.LevenePusat(groups.Values, Compare.PusatLevene.Rerata, out double p1);
            h.PMean = p1;
            h.WMedian = Compare.LevenePusat(groups.Values, Compare.PusatLevene.Median, out double p2);
            h.PMedian = p2;
            h.WTrim = Compare.LevenePusat(groups.Values, Compare.PusatLevene.RerataTerpangkas, out double p3);
            h.PTrim = p3;

            
            
            
            h.WMedAdj = h.WMedian;
            var g = groups.Values.Where(x => x.Count > 0).ToList();
            double num = 0, den = 0;
            for (int i = 0; i < g.Count; i++)
            {
                double med = Descriptives.Quantile(g[i].OrderBy(v => v).ToList(), 0.5);
                var ad = g[i].Select(v => Math.Abs(v - med)).ToList();
                double varAd = Descriptives.Summarize(ad).Variance;
                double ui = varAd / g[i].Count;
                num += ui;
                den += ui * ui / (g[i].Count - 1);
            }
            h.Df2MedAdj = den > 0 ? (num * num) / den : double.NaN;
            h.PMedAdj = double.IsNaN(h.Df2MedAdj)
                ? double.NaN
                : Distributions.FUpper(h.WMedAdj, h.K - 1, h.Df2MedAdj);
            return h;
        }

        public static List<ResultBlock> Bloks(Dataset ds, string nilaiVar, string grupVar, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading($"Uji Levene — kesamaan ragam ({nilaiVar} menurut {grupVar})", 1)
            };

            var h = Hitung(ds, nilaiVar, grupVar);
            if (h == null)
            {
                var groups = Compare.GroupValues(ds, nilaiVar, grupVar);
                blocks.Add(Blocks.Note(groups.Count < 2 ? "Butuh sedikitnya 2 kelompok."
                                                        : "Jumlah amatan tidak cukup.", NoteKind.Error));
                return blocks;
            }

            
            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.UjiLevene, DaftarRumus.LeveneMedian, DaftarRumus.RerataTerpangkas));
            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Banyak kelompok (k)", $"k = {h.K}"),
                ("Banyak amatan (N)", $"N = {h.N}"),
                ("Levene berdasar rerata", $"W = {Fmt.Num(h.WMean, 4)}   p = {Fmt.P(h.PMean)}"),
                ("Levene berdasar median", $"W = {Fmt.Num(h.WMedian, 4)}   p = {Fmt.P(h.PMedian)}"),
                ("Levene median + df disesuaikan",
                 $"W = {Fmt.Num(h.WMedAdj, 4)}   df2 = {Fmt.Num(h.Df2MedAdj, 2)}   p = {Fmt.P(h.PMedAdj)}"),
                ("Levene berdasar rerata terpangkas 5%",
                 $"W = {Fmt.Num(h.WTrim, 4)}   p = {Fmt.P(h.PTrim)}")));

            blocks.Add(Blocks.Table("Uji Levene (4 varian titik pusat)",
                new[] { "Varian", "df1", "df2", "Statistik W", "p" },
                new List<List<string>>
                {
                    new() { "Berdasar rerata", (h.K - 1).ToString(), (h.N - h.K).ToString(),
                            Fmt.Num(h.WMean, 4), Fmt.P(h.PMean) },
                    new() { "Berdasar median", (h.K - 1).ToString(), (h.N - h.K).ToString(),
                            Fmt.Num(h.WMedian, 4), Fmt.P(h.PMedian) },
                    new() { "Berdasar median, df disesuaikan", (h.K - 1).ToString(),
                            Fmt.Num(h.Df2MedAdj, 2), Fmt.Num(h.WMedAdj, 4), Fmt.P(h.PMedAdj) },
                    new() { "Berdasar rerata terpangkas 5%", (h.K - 1).ToString(),
                            (h.N - h.K).ToString(), Fmt.Num(h.WTrim, 4), Fmt.P(h.PTrim) }
                },
                $"H0: ragam semua kelompok sama. {Compare.Keputusan(h.PMedian, alpha)}"));

            blocks.Add(Blocks.Note(
                "Keempat varian memakai simpangan mutlak terhadap titik pusat masing-masing kelompok, "
                + "lalu uji-F satu arah atas simpangan itu. Varian 'median, df disesuaikan' (Brown-Forsythe) "
                + "memakai derajat kebebasan penyebut Welch-Satterthwaite supaya lebih tangguh bila ragam "
                + "tidak sama dan sebaran menyimpang dari normal."));
            return blocks;
        }
    }
}
