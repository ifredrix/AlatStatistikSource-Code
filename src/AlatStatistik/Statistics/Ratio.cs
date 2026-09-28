using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Ratio
    {
        public class Hasil
        {
            public int N;
            public double Mean = double.NaN;
            public double Sd = double.NaN;
            public double Min = double.NaN;
            public double Max = double.NaN;
            public double Median = double.NaN;
            public double WeightedMean = double.NaN;
            public double Aad = double.NaN;
            public double Cod = double.NaN;
            public double Prd = double.NaN;
            public double CovMean = double.NaN;
            public double CovMedian = double.NaN;
        }

        public static Hasil Hitung(List<double> y, List<double> x)
        {
            var h = new Hasil();
            var r = new List<double>();
            double jumlahY = 0, jumlahX = 0;
            int m = Math.Min(y.Count, x.Count);
            for (int i = 0; i < m; i++)
            {
                if (Math.Abs(x[i]) < 1e-15) continue;      
                double nilai = y[i] / x[i];
                if (double.IsNaN(nilai) || double.IsInfinity(nilai)) continue;
                r.Add(nilai); jumlahY += y[i]; jumlahX += x[i];
            }
            h.N = r.Count;
            if (h.N == 0) return h;

            h.Mean = r.Average();
            h.Min = r.Min();
            h.Max = r.Max();
            h.Median = Descriptives.Quantile(r, 0.5);
            h.Sd = h.N > 1 ? Math.Sqrt(r.Sum(v => (v - h.Mean) * (v - h.Mean)) / (h.N - 1)) : 0;
            h.WeightedMean = Math.Abs(jumlahX) > 1e-15 ? jumlahY / jumlahX : double.NaN;

            h.Aad = r.Sum(v => Math.Abs(v - h.Median)) / h.N;
            if (Math.Abs(h.Median) > 1e-15)
            {
                h.Cod = h.Aad / h.Median;
                h.CovMedian = Math.Sqrt(r.Sum(v => (v - h.Median) * (v - h.Median)) / h.N) / h.Median;
            }
            if (Math.Abs(h.Mean) > 1e-15) h.CovMean = h.Sd / h.Mean;
            if (!double.IsNaN(h.WeightedMean) && Math.Abs(h.WeightedMean) > 1e-15) h.Prd = h.Mean / h.WeightedMean;

            return h;
        }

        public static List<(string Label, List<double> Y, List<double> X)> Kelompokkan(
            Dataset ds, string pembilang, string penyebut, string kelompok)
        {
            int iY = ds.IndexOf(pembilang), iX = ds.IndexOf(penyebut);
            int iK = string.IsNullOrWhiteSpace(kelompok) ? -1 : ds.IndexOf(kelompok);
            var urutan = new List<string>();
            var grup = new Dictionary<string, (List<double> Y, List<double> X)>();

            for (int r = 0; r < ds.RowCount; r++)
            {
                string kunci = "(semua)";
                if (iK >= 0)
                {
                    string? t = ds.Ambil(r, iK);
                    if (string.IsNullOrWhiteSpace(t)) continue;
                    kunci = t!;
                }
                if (iY < 0 || iX < 0) continue;
                double? y = Dataset.ToDouble(ds.Ambil(r, iY));
                double? x = Dataset.ToDouble(ds.Ambil(r, iX));
                if (!y.HasValue || !x.HasValue) continue;
                if (!grup.TryGetValue(kunci, out var e))
                {
                    e = (new List<double>(), new List<double>());
                    grup[kunci] = e;
                    urutan.Add(kunci);
                }
                e.Y.Add(y.Value);
                e.X.Add(x.Value);
            }
            return urutan.Select(k => (k, grup[k].Y, grup[k].X)).ToList();
        }

        public static List<ResultBlock> RatioBlocks(Dataset ds, string pembilang, string penyebut, string kelompok)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading("Rasio (Ratio Statistics)", 1) };

            int iY = ds.IndexOf(pembilang), iX = ds.IndexOf(penyebut);
            if (iY < 0 || iX < 0)
            {
                blocks.Add(Blocks.Note($"Pembilang '{pembilang}' atau penyebut '{penyebut}' tidak ditemukan.",
                                       NoteKind.Error));
                return blocks;
            }

            var perKelompok = Kelompokkan(ds, pembilang, penyebut, kelompok);
            if (perKelompok.Count == 0)
            {
                blocks.Add(Blocks.Note("Tidak ada baris dengan pembilang dan penyebut yang sah.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.Rasio, DaftarRumus.Rerata, DaftarRumus.RagamSampel, DaftarRumus.MedianKuartil));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Pembilang", pembilang),
                ("Penyebut", penyebut),
                ("Kelompok", string.IsNullOrWhiteSpace(kelompok) ? "(tidak ada)" : kelompok),
                ("Rasio", "rᵢ = yᵢ / xᵢ  (penyebut nol dikeluarkan)")));

            var kolom = new[] { "Kelompok", "N", "Rerata", "Simp. baku", "Median", "Min", "Max",
                                "Rerata berbobot", "AAD", "COD", "PRD", "COV (rerata)", "COV (median)" };
            var baris = new List<List<string>>();
            void Isi(string label, List<double> y, List<double> x)
            {
                var h = Hitung(y, x);
                if (h.N == 0)
                {
                    baris.Add(new List<string> { label, "0" }.Concat(
                        Enumerable.Repeat(Fmt.NA, 11)).ToList());
                    return;
                }
                baris.Add(new List<string>
                {
                    label, Fmt.Int(h.N), Fmt.Num(h.Mean), Fmt.Num(h.Sd, 4), Fmt.Num(h.Median, 4),
                    Fmt.Num(h.Min, 4), Fmt.Num(h.Max, 4), Fmt.Num(h.WeightedMean, 4),
                    Fmt.Num(h.Aad, 4), Fmt.Num(h.Cod, 4), Fmt.Num(h.Prd, 4),
                    Fmt.Num(h.CovMean, 4), Fmt.Num(h.CovMedian, 4)
                });
            }
            foreach (var (label, y, x) in perKelompok) Isi(label, y, x);

            
            var semuaY = new List<double>();
            var semuaX = new List<double>();
            foreach (var (_, y, x) in perKelompok) { semuaY.AddRange(y); semuaX.AddRange(x); }
            Isi("Total", semuaY, semuaX);

            blocks.Add(Blocks.Table("Statistik rasio", kolom, baris,
                "AAD = Σ|rᵢ − median|/n; COD = AAD/median; PRD = rerata/rerata berbobot; "
                + "COV(rerata) = s/rerata; COV(median) = √(Σ(rᵢ − median)²/n)/median."));

            return blocks;
        }
    }
}
