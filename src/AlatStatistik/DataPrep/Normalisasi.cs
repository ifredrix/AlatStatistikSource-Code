using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AlatStatistik.Models;
using AlatStatistik.Statistics;

namespace AlatStatistik.DataPrep
{

    public enum CaraNormalisasi
    {
        MinMax,   
        ZScore    
    }

    public class HasilNormalisasi
    {
        public CaraNormalisasi Cara = CaraNormalisasi.MinMax;

        public int BarisAcuan;

        public int BarisTotal;

        public List<string> KolomHasil = new();

        public List<List<string>> Rincian = new();

        public List<string> Catatan = new();

        public bool AdaPerubahan => KolomHasil.Count > 0;

        public string Ringkasan()
            => AdaPerubahan
                ? $"{KolomHasil.Count} variabel dinormalisasi ({LabelCara(Cara)})"
                : "tidak ada yang berubah";

        public static string LabelCara(CaraNormalisasi c)
            => c == CaraNormalisasi.MinMax ? "min–maks 0–1" : "z-skor";

        public List<ResultBlock> KeBlok()
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Normalisasi", 1) };

            if (!AdaPerubahan)
            {
                blok.Add(Blocks.Note("Tidak ada variabel yang diproses. " +
                         string.Join(" ", Catatan), NoteKind.Warning));
                return blok;
            }

            blok.Add(Blocks.Table("Per variabel",
                new[] { "Variabel asli", "Statistik acuan", "Disimpan di" },
                Rincian,
                Cara == CaraNormalisasi.MinMax
                    ? "Rumus: (x − min) / (maks − min), hasilnya 0 sampai 1."
                    : "Rumus: (x − rerata) / simpangan baku, hasilnya rerata 0 dan simpangan baku 1."));

            bool sebagian = BarisAcuan < BarisTotal;
            if (sebagian)
            {
                blok.Add(Blocks.Note(
                    $"Statistik dihitung hanya dari {BarisAcuan} baris acuan, lalu diterapkan ke "
                    + $"seluruh {BarisTotal} baris. Ini yang benar untuk pelatihan model: "
                    + "min/maks atau rerata/sd data uji tidak boleh ikut menentukan "
                    + "penyekalaan data latih, kalau tidak hasil uji akan terlalu bagus.",
                    NoteKind.Ok));
            }
            else
            {
                blok.Add(Blocks.Note(
                    $"Statistik dihitung dari seluruh {BarisTotal} baris. Untuk pelatihan model "
                    + "seharusnya dihitung dari data latih saja lalu diterapkan ke data uji — "
                    + "kalau tidak, ada kebocoran data dan nilai uji akan terlalu optimistis.",
                    NoteKind.Warning));
            }

            foreach (string c in Catatan)
                blok.Add(Blocks.Note(c, NoteKind.Info));

            return blok;
        }
    }

    public static class Normalisasi
    {
        public static HasilNormalisasi Jalankan(Dataset ds, IEnumerable<string> kolom,
                                                CaraNormalisasi cara, bool timpa = false,
                                                IEnumerable<int>? barisAcuan = null)
        {
            var hasil = new HasilNormalisasi { Cara = cara, BarisTotal = ds.RowCount };

            var acuan = barisAcuan?.ToList() ?? Enumerable.Range(0, ds.RowCount).ToList();
            hasil.BarisAcuan = acuan.Count;

            foreach (string nama in kolom)
            {
                int i = ds.IndexOf(nama);
                if (i < 0) continue;

                if (ds.Variables[i].Measure != Measure.Skala)
                {
                    hasil.Catatan.Add($"{nama}: dilewati, bukan variabel skala (angka).");
                    continue;
                }

                var nilaiAcuan = acuan
                    .Where(r => r >= 0 && r < ds.Rows.Count)
                    .Select(r => Dataset.ToDouble(i < ds.Rows[r].Length ? ds.Rows[r][i] : null))
                    .Where(v => v.HasValue)
                    .Select(v => v!.Value)
                    .ToList();

                if (nilaiAcuan.Count == 0)
                {
                    hasil.Catatan.Add($"{nama}: dilewati, tidak ada angka yang bisa dipakai.");
                    continue;
                }

                double min = nilaiAcuan.Min();
                double maks = nilaiAcuan.Max();
                double rerata = nilaiAcuan.Average();
                double sd = SimpanganBaku(nilaiAcuan);

                string statistik = cara == CaraNormalisasi.MinMax
                    ? $"min {Fmt.Num(min, 4)}, maks {Fmt.Num(maks, 4)}"
                    : $"rerata {Fmt.Num(rerata, 4)}, sd {Fmt.Num(sd, 4)}";

                
                
                
                if (cara == CaraNormalisasi.MinMax && maks - min == 0)
                    hasil.Catatan.Add($"{nama}: semua nilainya sama, hasilnya 0 untuk semua baris.");
                if (cara == CaraNormalisasi.ZScore && sd == 0)
                    hasil.Catatan.Add($"{nama}: semua nilainya sama, hasilnya 0 untuk semua baris.");

                var keluaran = new List<string?>();
                foreach (var baris in ds.Rows)
                {
                    var sekarang = Dataset.ToDouble(baris[i]);
                    if (!sekarang.HasValue) { keluaran.Add(null); continue; }

                    double v = cara == CaraNormalisasi.MinMax
                        ? (maks - min == 0 ? 0.0 : (sekarang.Value - min) / (maks - min))
                        : (sd == 0 ? 0.0 : (sekarang.Value - rerata) / sd);

                    keluaran.Add(v.ToString("G10", CultureInfo.InvariantCulture));
                }

                if (timpa)
                {
                    for (int r = 0; r < ds.Rows.Count; r++)
                    {
                        if (i >= ds.Rows[r].Length)
                        {
                            var rapih = new string?[ds.Variables.Count];
                            Array.Copy(ds.Rows[r], rapih, ds.Rows[r].Length);
                            ds.Rows[r] = rapih;
                        }
                        if (r < keluaran.Count) ds.Rows[r][i] = keluaran[r];
                    }
                    hasil.KolomHasil.Add(nama);
                }
                else
                {
                    string akhiran = cara == CaraNormalisasi.MinMax ? "_minmaks" : "_z";
                    int baru = Bantu.TambahKolom(ds, nama + akhiran, keluaran);
                    hasil.KolomHasil.Add(ds.Variables[baru].Name);
                }

                hasil.Rincian.Add(new List<string> { nama, statistik, hasil.KolomHasil[^1] });
            }

            return hasil;
        }

        private static double SimpanganBaku(List<double> nilai)
        {
            if (nilai.Count == 0) return 0.0;
            double rerata = nilai.Average();
            return Math.Sqrt(nilai.Sum(v => (v - rerata) * (v - rerata)) / nilai.Count);
        }
    }
}
