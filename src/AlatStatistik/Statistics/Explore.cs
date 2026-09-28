using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Explore
    {

        public sealed class BarisEksplor
        {
            public string Label = "";
            public int N;
            public double Rerata = double.NaN;
            public double Median = double.NaN;
            public double Sd = double.NaN;
            public double Min = double.NaN;
            public double Max = double.NaN;
            public double Q1 = double.NaN;
            public double Q3 = double.NaN;
            public double ShapiroP = double.NaN;   
        }

        private static List<(List<string> Label, List<double> Nilai)> Kelompok(
            Dataset ds, List<string> factors, string value)
        {
            int iv = ds.IndexOf(value);
            var indeksFaktor = factors.Select(f => ds.IndexOf(f)).ToList();
            var grup = new Dictionary<string, (List<string> Label, List<double> Nilai)>();
            var urutan = new List<string>();
            for (int r = 0; r < ds.RowCount; r++)
            {
                var label = new List<string>();
                bool lengkap = true;
                foreach (int ik in indeksFaktor)
                {
                    string? t = ik >= 0 && ik < ds.Rows[r].Length ? ds.Rows[r][ik] : null;
                    if (string.IsNullOrWhiteSpace(t)) { lengkap = false; break; }
                    label.Add(t!);
                }
                if (!lengkap) continue;
                string kunci = string.Join("|", label);
                if (!grup.TryGetValue(kunci, out var entri))
                {
                    entri = (label, new List<double>());
                    grup[kunci] = entri;
                    urutan.Add(kunci);
                }
                double? angka = iv >= 0 && iv < ds.Rows[r].Length ? Dataset.ToDouble(ds.Rows[r][iv]) : null;
                if (angka.HasValue) entri.Nilai.Add(angka.Value);
            }
            return urutan.Select(k => grup[k]).ToList();
        }

        private static BarisEksplor BuatBaris(string label, List<double> nilai,
                                              KuantilDefinisi definisi)
        {
            var b = new BarisEksplor { Label = label, N = nilai.Count };
            if (nilai.Count == 0) return b;
            var s = Descriptives.Summarize(nilai, definisi);
            b.Rerata = s.Mean;
            b.Median = s.Median;
            b.Sd = s.Sd;
            b.Min = s.Min;
            b.Max = s.Max;
            b.Q1 = s.Q1;
            b.Q3 = s.Q3;
            if (nilai.Count >= 3)
            {
                var sw = Normality.ShapiroWilk(nilai);
                if (sw != null) b.ShapiroP = sw.P;
            }
            return b;
        }

        public static List<BarisEksplor> Hitung(Dataset ds, List<string> faktor, string nilai,
                                                KuantilDefinisi definisi = KuantilDefinisi.Satu)
        {
            var hasil = new List<BarisEksplor>();
            var perKelompok = Kelompok(ds, faktor, nilai);
            foreach (var (label, angka) in perKelompok)
                hasil.Add(BuatBaris(string.Join(", ", label), angka, definisi));

            var total = new List<double>();
            foreach (var (_, angka) in perKelompok) total.AddRange(angka);
            hasil.Add(BuatBaris("Total", total, definisi));
            return hasil;
        }

        public static List<ResultBlock> ExploreBlocks(Dataset ds, List<string> dependen,
                                                      List<string> faktor,
                                                      KuantilDefinisi definisi = KuantilDefinisi.Satu)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading("Explore — deskriptif & kenormalan per kelompok", 1) };
            if (dependen.Count == 0)
            {
                blocks.Add(Blocks.Note("Butuh sedikitnya satu variabel terikat.", NoteKind.Error));
                return blocks;
            }

            var rumusKuantil = definisi == KuantilDefinisi.TukeyHinges
                ? DaftarRumus.KuartilTukey
                : DaftarRumus.MedianKuartil;

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.Rerata, DaftarRumus.RagamSampel, rumusKuantil, DaftarRumus.UjiShapiroWilk));

            string namaDefinisi = definisi switch
            {
                KuantilDefinisi.TukeyHinges => "Tukey's Hinges",
                KuantilDefinisi.Linear => "Linear (n−1)·p — default NumPy/pandas",
                _ => "Weighted Average (Definition 1) — bawaan",
            };

            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Variabel terikat", string.Join(", ", dependen)),
                ("Variabel kelompok", faktor.Count > 0 ? string.Join(", ", faktor) : "(tidak ada — keseluruhan)"),
                ("Definisi kuantil Q1/Q3", namaDefinisi)));

            var kolom = new[] { "Kelompok", "N", "Rerata", "Median", "Simp. Baku", "Min", "Max", "Q1", "Q3", "Shapiro–Wilk p" };

            foreach (string v in dependen)
            {
                var baris = new List<List<string>>();
                foreach (var b in Hitung(ds, faktor, v, definisi))
                {
                    if (b.N == 0)
                    {
                        baris.Add(new List<string> { b.Label, "0", Fmt.NA, Fmt.NA, Fmt.NA, Fmt.NA, Fmt.NA, Fmt.NA, Fmt.NA, Fmt.NA });
                        continue;
                    }
                    baris.Add(new List<string>
                    {
                        b.Label, Fmt.Int(b.N), Fmt.Num(b.Rerata), Fmt.Num(b.Median), Fmt.Num(b.Sd),
                        Fmt.Num(b.Min), Fmt.Num(b.Max), Fmt.Num(b.Q1), Fmt.Num(b.Q3),
                        double.IsNaN(b.ShapiroP) ? "—" : Fmt.P(b.ShapiroP)
                    });
                }

                blocks.Add(Blocks.Table($"Deskriptif — {v}", kolom, baris,
                    $"Q1/Q3 memakai definisi {namaDefinisi}. Shapiro–Wilk dihitung bila n ≥ 3."));
            }

            return blocks;
        }
    }
}
