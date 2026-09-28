using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlatStatistik.DataPrep;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class MergeFiles
    {
        public class HasilGabung
        {
            public List<string> Kolom = new();
            public List<List<string>> Baris = new();
        }

        private static string Unik(HashSet<string> dipakai, string nama)
        {
            if (!dipakai.Contains(nama)) return nama;
            int n = 2;
            while (dipakai.Contains(nama + "_" + n)) n++;
            return nama + "_" + n;
        }

        public static HasilGabung Hitung(Dataset a, Dataset b, string cara)
        {
            var hasil = new HasilGabung();

            if (cara == "tambah_variabel")
            {
                
                var dipakai = new HashSet<string>(a.Variables.Select(v => v.Name), StringComparer.OrdinalIgnoreCase);
                var namaB = new List<string>();
                foreach (var v in b.Variables)
                {
                    string n = Unik(dipakai, v.Name);
                    dipakai.Add(n);
                    namaB.Add(n);
                }
                hasil.Kolom.AddRange(a.Variables.Select(v => v.Name));
                hasil.Kolom.AddRange(namaB);

                int baris = Math.Max(a.RowCount, b.RowCount);
                for (int r = 0; r < baris; r++)
                {
                    var row = new List<string>();
                    for (int c = 0; c < a.Variables.Count; c++)
                        row.Add(r < a.RowCount && c < a.Rows[r].Length ? (a.Rows[r][c] ?? "") : "");
                    for (int c = 0; c < b.Variables.Count; c++)
                        row.Add(r < b.RowCount && c < b.Rows[r].Length ? (b.Rows[r][c] ?? "") : "");
                    hasil.Baris.Add(row);
                }
                return hasil;
            }

            
            var dipakaiK = new HashSet<string>(a.Variables.Select(v => v.Name), StringComparer.OrdinalIgnoreCase);
            var tambahan = new List<string>();
            foreach (var v in b.Variables)
            {
                if (dipakaiK.Contains(v.Name)) continue;
                string n = Unik(dipakaiK, v.Name);
                dipakaiK.Add(n);
                tambahan.Add(n);
            }
            hasil.Kolom.AddRange(a.Variables.Select(v => v.Name));
            hasil.Kolom.AddRange(tambahan);

            
            int[] PetaA(Dataset d)
                => d.Variables.Select((v, i) =>
                    hasil.Kolom.FindIndex(k => string.Equals(k, v.Name, StringComparison.OrdinalIgnoreCase)))
                    .Select(x => x < 0 ? -1 : x).ToArray();
            var petaA = PetaA(a);
            var petaB = PetaA(b);

            foreach (var d in new[] { a, b })
            {
                var peta = d == a ? petaA : petaB;
                for (int r = 0; r < d.RowCount; r++)
                {
                    var row = Enumerable.Repeat("", hasil.Kolom.Count).ToList();
                    for (int c = 0; c < peta.Length && c < d.Rows[r].Length; c++)
                        if (peta[c] >= 0) row[peta[c]] = d.Rows[r][c] ?? "";
                    hasil.Baris.Add(row);
                }
            }
            return hasil;
        }

        public static string? CariBerkas(Dataset ds, string jalur)
        {
            if (string.IsNullOrWhiteSpace(jalur)) return null;
            if (File.Exists(jalur)) return jalur;
            if (Path.IsPathRooted(jalur)) return null;

            string? direktori = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(ds.SourcePath))
                    direktori = Path.GetDirectoryName(ds.SourcePath);
            }
            catch { direktori = null; }

            if (!string.IsNullOrWhiteSpace(direktori))
            {
                string calon = Path.Combine(direktori!, jalur);
                if (File.Exists(calon)) return calon;
            }
            string dariCwd = Path.Combine(Directory.GetCurrentDirectory(), jalur);
            if (File.Exists(dariCwd)) return dariCwd;

            string dariAplikasi = Path.Combine(AppContext.BaseDirectory, jalur);
            return File.Exists(dariAplikasi) ? dariAplikasi : null;
        }

        public static string ContohJalur()
            => Path.Combine(AppContext.BaseDirectory, "samples", "agregat_sintetik.csv");

        public static List<ResultBlock> MergeFilesBlocks(Dataset ds, string jalur, string cara)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading("Merge Files (pratinjau)", 1) };

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.GabungBerkas));

            string? berkas = CariBerkas(ds, jalur);
            if (berkas is null)
            {
                blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                    ("Berkas kedua", string.IsNullOrWhiteSpace(jalur) ? "(belum diisi)" : jalur),
                    ("Hasil pencarian", "tidak ditemukan")));
                blocks.Add(Blocks.Note(
                    "Berkas kedua tidak ditemukan. Isi opsi 'Berkas kedua' dengan jalur lengkap "
                    + "(atau jalur relatif terhadap folder kerja). Contoh: "
                    + ContohJalur(), NoteKind.Error));
                return blocks;
            }

            Dataset lain;
            try { lain = DataImport.LoadCsv(berkas); }
            catch (Exception ex)
            {
                blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                    ("Berkas kedua", berkas), ("Hasil baca", "gagal")));
                blocks.Add(Blocks.Note($"Berkas kedua tidak bisa dibaca: {ex.Message}", NoteKind.Error));
                return blocks;
            }

            var g = Hitung(ds, lain, cara);
            string mode = cara == "tambah_variabel" ? "Tambah variabel (Add Variables)"
                                                    : "Tambah kasus (Add Cases)";

            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Cara", mode),
                ("Berkas utama", $"{ds.RowCount} baris × {ds.Variables.Count} variabel"),
                ("Berkas kedua", $"{lain.RowCount} baris × {lain.Variables.Count} variabel"),
                ("Hasil", $"{g.Baris.Count} baris × {g.Kolom.Count} kolom"),
                ("Berkas kedua", berkas)));

            const int batas = 30;
            var tampil = g.Baris.Take(batas).ToList();
            blocks.Add(Blocks.Table("Pratinjau hasil gabungan", g.Kolom, tampil,
                $"Menampilkan {tampil.Count} dari {g.Baris.Count} baris. Ini hanya pratinjau — "
                + $"dataset utama tidak diubah. Di acuan, Merge Files mengubah dataset."));

            return blocks;
        }
    }
}
