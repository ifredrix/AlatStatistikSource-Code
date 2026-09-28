using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Transpose
    {

        public static (List<string> Kolom, List<List<string>> Baris) Hitung(Dataset ds, int maksKolom)
        {
            int batas = Math.Min(ds.RowCount, Math.Max(1, maksKolom));
            var kolom = new List<string> { "Variabel" };
            for (int r = 0; r < batas; r++) kolom.Add("Kasus" + (r + 1));

            var baris = new List<List<string>>();
            for (int c = 0; c < ds.Variables.Count; c++)
            {
                var rb = new List<string> { ds.Variables[c].Name };
                for (int r = 0; r < batas; r++)
                {
                    string? t = r < ds.Rows.Count && c < ds.Rows[r].Length ? ds.Rows[r][c] : null;
                    rb.Add(string.IsNullOrWhiteSpace(t) ? "" : t!);
                }
                baris.Add(rb);
            }

            return (kolom, baris);
        }

        public static List<ResultBlock> TransposeBlocks(Dataset ds, int maksKolom)
        {
            var (kolom, baris) = Hitung(ds, maksKolom);
            int batas = kolom.Count - 1;

            var blocks = new List<ResultBlock> { Blocks.Heading("Transpose — pratinjau (dataset tidak diubah)", 1) };

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Transpose));
            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Bentuk asli", $"{ds.RowCount} baris × {ds.Variables.Count} variabel"),
                ("Bentuk transpos", $"{ds.Variables.Count} baris × {ds.RowCount} kasus"),
                ("Kolom kasus ditampilkan", $"{batas} dari {ds.RowCount}")));

            blocks.Add(Blocks.Table("Pratinjau transpose", kolom, baris,
                "Variabel menjadi baris, kasus menjadi kolom. Ini hanya pratinjau — "
                + "dataset asli tidak diubah. Di acuan, Transpose menciptakan dataset baru."));

            return blocks;
        }
    }
}
