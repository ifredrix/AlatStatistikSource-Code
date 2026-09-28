using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.DataPrep
{

    public enum CaraPengkodean
    {
        OneHot,   
        Label     
    }

    public class HasilPengkodean
    {
        public CaraPengkodean Cara = CaraPengkodean.OneHot;
        public List<string> KolomBaru = new();

        public List<List<string>> Rincian = new();

        public List<string> Catatan = new();
        public bool AdaPerubahan => KolomBaru.Count > 0;

        public string Ringkasan()
        {
            if (!AdaPerubahan) return "tidak ada yang berubah";

            
            
            string cara = Cara == CaraPengkodean.OneHot ? "one-hot" : "nomor kategori";
            return $"{KolomBaru.Count} kolom baru dibuat ({cara})";
        }

        public List<ResultBlock> KeBlok()
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Kategori menjadi angka", 1) };

            if (!AdaPerubahan)
            {
                blok.Add(Blocks.Note("Tidak ada kolom baru yang dibuat. "
                         + string.Join(" ", Catatan), NoteKind.Warning));
                return blok;
            }

            blok.Add(Blocks.Table("Per variabel",
                new[] { "Variabel asli", "Kategori", "Kolom yang dibuat" },
                Rincian,
                Cara == CaraPengkodean.OneHot
                    ? "Setiap kategori menjadi kolom sendiri berisi 1 bila baris itu "
                      + "memakai kategori tersebut, dan 0 bila tidak."
                    : "Setiap kategori diberi nomor urut sesuai urutan kemunculannya di data."));

            if (Cara == CaraPengkodean.Label)
                blok.Add(Blocks.Note(
                    "Nomor kategori mengandung urutan yang sebenarnya tidak ada. Untuk "
                    + "kategori tanpa urutan (misalnya warna), one-hot lebih aman karena "
                    + "model tidak akan mengira warna 2 'lebih besar' dari warna 1.",
                    NoteKind.Warning));

            foreach (string c in Catatan)
                blok.Add(Blocks.Note(c, NoteKind.Info));

            return blok;
        }
    }

    public static class Pengkodean
    {
        public static HasilPengkodean Jalankan(Dataset ds, IEnumerable<string> kolom,
                                               CaraPengkodean cara, bool buangAsli = false)
        {
            var hasil = new HasilPengkodean { Cara = cara };
            var akanDibuang = new List<string>();

            foreach (string nama in kolom)
            {
                int i = ds.IndexOf(nama);
                if (i < 0) continue;

                if (ds.Variables[i].Measure == Measure.Skala)
                {
                    hasil.Catatan.Add($"{nama}: dilewati, sudah berupa angka.");
                    continue;
                }

                var teks = Enumerable.Range(0, ds.RowCount).Select(r => ds.Ambil(r, i)).ToList();
                var kategori = new List<string>();
                foreach (string? v in teks)
                {
                    if (string.IsNullOrWhiteSpace(v)) continue;
                    if (!kategori.Contains(v!)) kategori.Add(v!);
                }

                if (kategori.Count == 0)
                {
                    hasil.Catatan.Add($"{nama}: dilewati, tidak ada kategori yang terisi.");
                    continue;
                }

                var dibuat = new List<string>();

                if (cara == CaraPengkodean.OneHot)
                {
                    foreach (string k in kategori)
                    {
                        List<string?> nilai = teks
                            .Select(t => (string?)(string.IsNullOrWhiteSpace(t)
                                                    ? "0"
                                                    : (t == k ? "1" : "0")))
                            .ToList();
                        int baru = Bantu.TambahKolom(ds, $"{nama}_{k}", nilai, Measure.Nominal, 0);
                        dibuat.Add(ds.Variables[baru].Name);
                    }
                }
                else
                {
                    var nilai = teks.Select(t => string.IsNullOrWhiteSpace(t)
                                                    ? (string?)null
                                                    : (kategori.IndexOf(t!) + 1).ToString()).ToList();
                    int baru = Bantu.TambahKolom(ds, nama + "_kode", nilai, Measure.Ordinal, 0);
                    dibuat.Add(ds.Variables[baru].Name);
                }

                hasil.KolomBaru.AddRange(dibuat);
                hasil.Rincian.Add(new List<string>
                {
                    nama, kategori.Count.ToString(), string.Join(", ", dibuat)
                });

                if (buangAsli) akanDibuang.Add(nama);
            }

            
            
            foreach (string nama in akanDibuang) Bantu.BuangKolom(ds, nama);

            return hasil;
        }
    }
}
