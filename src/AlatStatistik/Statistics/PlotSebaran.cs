using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class PlotSebaran
    {
        public class HasilPlot
        {
            public int N;
            public double Rerata, S;
            public List<double> Terurut = new();
            public List<double> KuantilTeoretis = new();     
            public List<double> KuantilSkala = new();        
            public List<double> PeluangEmpiris = new();      
            public List<double> PeluangTeoretis = new();     
            public List<double> SisaTerdetren = new();       
            public double KorelasiQQ = double.NaN;
        }

        internal static double PosisiPlotting(int i, int n) => (i - 0.375) / (n + 0.25);

        public static HasilPlot? Hitung(IEnumerable<double> nilai)
        {
            var x = (nilai ?? Array.Empty<double>()).Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
                                                    .OrderBy(v => v).ToList();
            int n = x.Count;
            if (n < 3) return null;

            double rerata = x.Average();
            double jk = x.Sum(v => (v - rerata) * (v - rerata));
            double s = Math.Sqrt(jk / (n - 1));
            if (!(s > 0)) return null;

            var h = new HasilPlot { N = n, Rerata = rerata, S = s, Terurut = x };
            for (int i = 0; i < n; i++)
            {
                double p = PosisiPlotting(i + 1, n);
                double z = Distributions.NormalInv(p);
                h.PeluangEmpiris.Add(p);
                h.KuantilTeoretis.Add(z);
                h.KuantilSkala.Add(rerata + s * z);
                h.PeluangTeoretis.Add(Distributions.NormalCdf((x[i] - rerata) / s));
                h.SisaTerdetren.Add(x[i] - (rerata + s * z));
            }
            h.KorelasiQQ = Correlation.Pearson(h.KuantilTeoretis, x);
            return h;
        }

        

        public static List<ResultBlock> PlotSebaranBlocks(Dataset ds, string var, string jenis)
        {
            string pilihan = (jenis ?? "keduanya").Trim().ToLowerInvariant();
            var blok = new List<ResultBlock>
            {
                Blocks.Heading($"Plot P–P dan Q–Q normal — {var}", 1)
            };

            int idx = ds.IndexOf(var);
            if (idx < 0)
            {
                blok.Add(Blocks.Note($"Variabel '{var}' tidak ditemukan.", NoteKind.Error));
                return blok;
            }

            var nilai = new List<double>();
            for (int r = 0; r < ds.RowCount; r++)
            {
                double? v = Dataset.ToDouble(ds.Rows[r][idx]);
                if (v.HasValue) nilai.Add(v.Value);
            }
            var h = Hitung(nilai);
            if (h is null)
            {
                blok.Add(Blocks.Note(
                    "Plot P–P/Q–Q butuh sedikitnya 3 angka yang tidak kosong dan simpangan baku "
                    + "lebih dari nol.", NoteKind.Error));
                return blok;
            }

            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.PosisiPlotting, DaftarRumus.PlotQQ, DaftarRumus.PlotPP));

            blok.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Banyak amatan", $"n = {h.N}"),
                ("Rerata", $"x̄ = {Fmt.Num(h.Rerata, 4)}"),
                ("Simpangan baku", $"s = {Fmt.Num(h.S, 4)}"),
                ("Posisi plotting pertama", $"p₁ = (1 − 0,375)/(n + 0,25) = {Fmt.Num(PosisiPlotting(1, h.N), 6)}"),
                ("Kuantil teoretis pertama", $"z₁ = Φ⁻¹({Fmt.Num(PosisiPlotting(1, h.N), 6)}) = {Fmt.Num(h.KuantilTeoretis[0], 6)}"),
                ("Posisi plotting terakhir", $"p_n = ({h.N} − 0,375)/({h.N} + 0,25) = {Fmt.Num(PosisiPlotting(h.N, h.N), 6)}"),
                ("Kuantil teoretis terakhir", $"z_n = {Fmt.Num(h.KuantilTeoretis[^1], 6)}"),
                ("Korelasi Q–Q (Filliben)", $"r = {Fmt.Num(h.KorelasiQQ, 6)}")));

            bool tampilQQ = pilihan is "qq" or "keduanya";
            bool tampilPP = pilihan is "pp" or "keduanya";

            if (tampilQQ)
            {
                var spec = new ChartSpec
                {
                    Kind = ChartKind.Scatter,
                    Title = $"Q–Q plot normal — {var}",
                    XTitle = "Kuantil teoretis (z)",
                    YTitle = "Nilai teramati"
                };
                for (int i = 0; i < h.N; i++) spec.Points.Add((h.KuantilTeoretis[i], h.Terurut[i]));

                
                
                spec.RegressionLine = (h.S, h.Rerata);
                blok.Add(Blocks.Chart("Q–Q plot", spec));
            }

            if (tampilPP)
            {
                var spec = new ChartSpec
                {
                    Kind = ChartKind.Scatter,
                    Title = $"P–P plot normal — {var}",
                    XTitle = "Peluang kumulatif teoretis",
                    YTitle = "Peluang kumulatif teramati"
                };
                for (int i = 0; i < h.N; i++) spec.Points.Add((h.PeluangTeoretis[i], h.PeluangEmpiris[i]));
                blok.Add(Blocks.Chart("P–P plot", spec));
            }

            var baris = new List<List<string>>();
            int tampil = Math.Min(h.N, 40);
            for (int i = 0; i < tampil; i++)
                baris.Add(new List<string>
                {
                    (i + 1).ToString(),
                    Fmt.Num(h.Terurut[i], 4),
                    Fmt.Num(h.KuantilTeoretis[i], 4),
                    Fmt.Num(h.KuantilSkala[i], 4),
                    Fmt.Num(h.PeluangEmpiris[i], 4),
                    Fmt.Num(h.PeluangTeoretis[i], 4),
                    Fmt.Num(h.SisaTerdetren[i], 4)
                });
            blok.Add(Blocks.Table("Koordinat plot",
                new[] { "Ke", "Teramati (terurut)", "Kuantil z", "Kuantil skala data",
                        "Peluang teramati", "Peluang teoretis", "Sisa terdetren" },
                baris,
                tampil < h.N ? $"Ditampilkan {tampil} dari {h.N} amatan." : null));

            blok.Add(Blocks.Note(
                "Kuantil teoretis memakai posisi plotting Blom pᵢ = (i − 0,375)/(n + 0,25), "
                + "sama dengan `scipy.stats.probplot`; koefisien garis rujukan pada Q–Q adalah "
                + "rerata dan simpangan baku datanya sendiri. Korelasi Q–Q (Filliben) mendekati 1 "
                + "bila titik-titiknya lurus, tetapi nilai kritisnya tidak ditabulasi di sini — "
                + "untuk keputusan formal pakai uji Shapiro–Wilk atau K–S di Statistik deskriptif. "
                + "Perbandingan angkanya ada di `docs/acuan_plotsebaran.json`."));

            return blok;
        }
    }
}
