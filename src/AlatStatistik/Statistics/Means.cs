using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Means
    {

        public class MeansBaris
        {
            public List<string> Label = new();
            public int N;
            public double Rerata;
            public double Sd;
            public double Median;
        }

        private static List<(List<string> Label, List<List<double>> Nilai)> Kelompok(
            Dataset ds, List<string> breaks, List<string> values)
        {
            var indeksKunci = breaks.Select(b => ds.IndexOf(b)).ToList();
            var indeksNilai = values.Select(v => ds.IndexOf(v)).ToList();
            var grup = new Dictionary<string, (List<string> Label, List<List<double>> Nilai)>();
            var urutan = new List<string>();
            for (int r = 0; r < ds.RowCount; r++)
            {
                var label = new List<string>();
                bool lengkap = true;
                foreach (int ik in indeksKunci)
                {
                    string? t = ik >= 0 && ik < ds.Rows[r].Length ? ds.Rows[r][ik] : null;
                    if (string.IsNullOrWhiteSpace(t)) { lengkap = false; break; }
                    label.Add(t!);
                }
                if (!lengkap) continue;
                string kunci = string.Join("|", label);
                if (!grup.TryGetValue(kunci, out var entri))
                {
                    entri = (label, values.Select(_ => new List<double>()).ToList());
                    grup[kunci] = entri;
                    urutan.Add(kunci);
                }
                for (int j = 0; j < indeksNilai.Count; j++)
                {
                    int iv = indeksNilai[j];
                    double? angka = iv >= 0 && iv < ds.Rows[r].Length ? Dataset.ToDouble(ds.Rows[r][iv]) : null;
                    if (angka.HasValue) entri.Nilai[j].Add(angka.Value);
                }
            }
            return urutan.Select(k => grup[k]).ToList();
        }

        public static List<MeansBaris> Hitung(Dataset ds, List<string> breaks, List<string> values)
        {
            var hasil = new List<MeansBaris>();
            var perKelompok = Kelompok(ds, breaks, values);
            foreach (var (label, nilai) in perKelompok)
            {
                var mb = new MeansBaris { Label = label };
                if (nilai.Count > 0)
                {
                    var v0 = nilai[0];
                    mb.N = v0.Count;
                    mb.Rerata = v0.Count > 0 ? v0.Average() : double.NaN;
                    mb.Sd = v0.Count > 1 ? Math.Sqrt(v0.Sum(x => (x - mb.Rerata) * (x - mb.Rerata)) / (v0.Count - 1)) : double.NaN;
                    mb.Median = v0.Count > 0 ? Descriptives.Quantile(v0, 0.5) : double.NaN;
                }
                hasil.Add(mb);
            }
            return hasil;
        }

        public class MeansSel
        {
            public int N;
            public double Rerata = double.NaN;
            public double Sd = double.NaN;
            public double Median = double.NaN;
        }

        public class MeansBlok
        {
            public List<string> Label = new();
            public List<MeansSel> Sel = new();   
        }

        private static MeansSel Ringkas(List<double> daftar)
        {
            var s = new MeansSel { N = daftar.Count };
            if (daftar.Count == 0) return s;
            s.Rerata = daftar.Average();
            s.Sd = daftar.Count > 1
                ? Math.Sqrt(daftar.Sum(x => (x - s.Rerata) * (x - s.Rerata)) / (daftar.Count - 1))
                : double.NaN;
            s.Median = Descriptives.Quantile(daftar, 0.5);
            return s;
        }

        public static List<MeansBlok> HitungPerNilai(Dataset ds, List<string> breaks, List<string> values)
        {
            var perKelompok = Kelompok(ds, breaks, values);

            var hasil = new List<MeansBlok>();
            foreach (var (label, nilai) in perKelompok)
            {
                var blok = new MeansBlok { Label = label };
                foreach (var daftar in nilai) blok.Sel.Add(Ringkas(daftar));
                hasil.Add(blok);
            }

            
            var total = values.Select(_ => new List<double>()).ToList();
            foreach (var (_, nilai) in perKelompok)
                for (int j = 0; j < nilai.Count; j++) total[j].AddRange(nilai[j]);

            var blokTotal = new MeansBlok { Label = breaks.Select(_ => "Total").ToList() };
            foreach (var daftar in total) blokTotal.Sel.Add(Ringkas(daftar));
            hasil.Add(blokTotal);
            return hasil;
        }

        public static List<ResultBlock> MeansBlocks(Dataset ds, List<string> breaks, List<string> values)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading("Means — rerata per kelompok", 1) };
            if (breaks.Count == 0 || values.Count == 0)
            {
                blocks.Add(Blocks.Note("Butuh sedikitnya satu variabel kelompok dan satu variabel nilai.", NoteKind.Error));
                return blocks;
            }

            var semuaBlok = HitungPerNilai(ds, breaks, values);
            int banyakKelompok = semuaBlok.Count - 1;   

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Rerata, DaftarRumus.RagamSampel, DaftarRumus.MedianKuartil));

            
            var kolom = new List<string>(breaks);
            foreach (string v in values)
            {
                kolom.Add($"{v}_N");
                kolom.Add($"{v}_rerata");
                kolom.Add($"{v}_sd");
                kolom.Add($"{v}_median");
            }

            var baris = new List<List<string>>();
            foreach (var blok in semuaBlok)
            {
                var r = new List<string>(blok.Label);
                foreach (var sel in blok.Sel)
                {
                    if (sel.N == 0) { r.AddRange(new[] { "0", Fmt.NA, Fmt.NA, Fmt.NA }); continue; }
                    r.Add(Fmt.Int(sel.N));
                    r.Add(Fmt.Num(sel.Rerata));
                    r.Add(double.IsNaN(sel.Sd) ? Fmt.NA : Fmt.Num(sel.Sd));
                    r.Add(Fmt.Num(sel.Median));
                }
                baris.Add(r);
            }

            
            
            var blokContoh = semuaBlok.Count > 0 ? semuaBlok[0] : null;
            bool adaContoh = blokContoh != null && blokContoh.Sel.Count > 0 && blokContoh.Sel[0].N > 0;
            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Variabel kelompok", string.Join(", ", breaks)),
                ("Variabel nilai", string.Join(", ", values)),
                ("Banyak kelompok", $"{banyakKelompok}"),
                ("Contoh rerata", adaContoh
                    ? $"{values[0]} pada kelompok \"{string.Join(" + ", blokContoh!.Label)}\": "
                      + $"x̄ = Σxᵢ / n = {Fmt.Num(blokContoh.Sel[0].Rerata * blokContoh.Sel[0].N)} "
                      + $"/ {Fmt.Int(blokContoh.Sel[0].N)} = {Fmt.Num(blokContoh.Sel[0].Rerata)}"
                    : "—")));

            blocks.Add(Blocks.Table("Means per kelompok", kolom, baris,
                $"Rerata, simpangan baku (n−1), dan median tiap kelompok. Baris Total memuat "
                + $"seluruh {ds.RowCount} baris. Kelompok ber-nilai kunci kosong dilewati."));

            return blocks;
        }
    }
}
