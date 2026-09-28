using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;
using AlatStatistik.Statistics;

namespace AlatStatistik.DataPrep
{

    public static class KualitasData
    {
        public static List<ResultBlock> Laporan(Dataset ds)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Laporan kualitas data", 1) };

            if (ds.IsEmpty)
            {
                blok.Add(Blocks.Note("Belum ada data yang dibuka.", NoteKind.Error));
                return blok;
            }

            var kolom = new[]
            {
                "Variabel", "Tingkat ukur", "Terisi", "Kosong", "% kosong",
                "Nilai unik", "Min", "Max", "Pencilan"
            };

            var baris = new List<List<string>>();
            int totalKosong = 0, totalSel = 0;

            foreach (var v in ds.Variables)
            {
                int i = ds.IndexOf(v.Name);
                var teks = Enumerable.Range(0, ds.RowCount).Select(r => ds.Ambil(r, i)).ToList();

                int terisi = teks.Count(t => !string.IsNullOrWhiteSpace(t));
                int kosong = teks.Count - terisi;
                double persen = teks.Count > 0 ? 100.0 * kosong / teks.Count : 0.0;

                var takKosong = teks.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
                int unik = takKosong.Distinct().Count();

                string min = Fmt.NA, max = Fmt.NA, pencilan = Fmt.NA;

                if (v.Measure == Measure.Skala)
                {
                    var angka = takKosong.Select(Dataset.ToDouble)
                                         .Where(x => x.HasValue)
                                         .Select(x => x!.Value)
                                         .ToList();
                    if (angka.Count > 0)
                    {
                        min = Fmt.Num(angka.Min(), 3);
                        max = Fmt.Num(angka.Max(), 3);
                        pencilan = Fmt.Int(PembersihData.HitungPencilan(ds, v.Name));
                    }
                }

                totalKosong += kosong;
                totalSel += teks.Count;

                baris.Add(new List<string>
                {
                    v.Name, LabelUkur(v.Measure), Fmt.Int(terisi), Fmt.Int(kosong),
                    Fmt.Num(persen, 1), Fmt.Int(unik), min, max, pencilan
                });
            }

            blok.Add(Blocks.Table("Per variabel", kolom, baris,
                "Pencilan dihitung dengan batas 1,5 x IQR, sama dengan box plot. "
                + "Kosong berarti sel tanpa nilai."));

            
            int duplikat = PembersihData.HitungDuplikat(ds);
            int barisLengkap = ds.CompleteRows(ds.Names).Count;
            double persenKosong = totalSel > 0 ? 100.0 * totalKosong / totalSel : 0.0;

            blok.Add(Blocks.Table("Ringkasan",
                new[] { "Ukuran", "Nilai" },
                new[]
                {
                    new[] { "Baris", Fmt.Int(ds.RowCount) },
                    new[] { "Variabel", Fmt.Int(ds.ColumnCount) },
                    new[] { "Baris lengkap (tanpa sel kosong)", Fmt.Int(barisLengkap) },
                    new[] { "Baris duplikat", Fmt.Int(duplikat) },
                    new[] { "Sel kosong", $"{Fmt.Int(totalKosong)} dari {Fmt.Int(totalSel)} ({Fmt.Num(persenKosong, 1)}%)" }
                },
                ""));

            
            var kolongBermasalah = ds.Variables
                .Where(v => PersenKosong(ds, v.Name) > 0)
                .Select(v => v.Name)
                .ToList();

            if (kolongBermasalah.Count > 0)
            {
                blok.Add(Blocks.Note(
                    "Ada nilai kosong pada: " + string.Join(", ", kolongBermasalah)
                    + ". Untuk statistik klasik barisnya boleh dibuang, tetapi untuk "
                    + "pelatihan model AI lebih baik diisi supaya data latih tidak hilang.",
                    NoteKind.Warning));
            }
            else
            {
                blok.Add(Blocks.Note("Tidak ada sel yang kosong.", NoteKind.Info));
            }

            if (duplikat > 0)
                blok.Add(Blocks.Note($"{duplikat} baris merupakan duplikat dari baris lain.",
                                     NoteKind.Warning));

            return blok;
        }

        private static double PersenKosong(Dataset ds, string nama)
        {
            int i = ds.IndexOf(nama);
            if (i < 0 || ds.RowCount == 0) return 0.0;
            int kosong = Enumerable.Range(0, ds.RowCount)
                                   .Count(r => string.IsNullOrWhiteSpace(ds.Ambil(r, i)));
            return 100.0 * kosong / ds.RowCount;
        }

        private static string LabelUkur(Measure m) => m switch
        {
            Measure.Skala => "Skala",
            Measure.Ordinal => "Ordinal",
            _ => "Nominal"
        };
    }
}
