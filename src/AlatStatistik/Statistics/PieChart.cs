using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class PieChart
    {

        public sealed class HasilPie
        {
            public int Total;
            public int Valid;
            public int Hilang;

            public List<(string Kategori, int N)> Kategori = new();
        }

        public static HasilPie Hitung(Dataset ds, string name)
        {
            var h = new HasilPie { Total = ds.RowCount };
            int idx = ds.IndexOf(name);
            if (idx < 0) return h;

            var hitung = new Dictionary<string, (int N, int Pertama)>();
            int urutan = 0;
            for (int r = 0; r < ds.RowCount; r++)
            {
                string? t = ds.Rows[r][idx];
                if (string.IsNullOrWhiteSpace(t)) { h.Hilang++; continue; }
                if (hitung.TryGetValue(t!, out var e)) hitung[t!] = (e.N + 1, e.Pertama);
                else hitung[t!] = (1, urutan);
                urutan++;
            }

            h.Valid = h.Total - h.Hilang;
            h.Kategori = hitung
                .OrderBy(kv => -kv.Value.N)
                .ThenBy(kv => kv.Value.Pertama)
                .Select(kv => (kv.Key, kv.Value.N))
                .ToList();
            return h;
        }

        public static List<ResultBlock> PieChartBlocks(Dataset ds, string name)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading($"Diagram lingkaran — {name}", 1) };

            var h = Hitung(ds, name);
            int idx = ds.IndexOf(name);
            if (idx < 0)
            {
                blocks.Add(Blocks.Note($"Variabel '{name}' tidak ditemukan.", NoteKind.Error));
                return blocks;
            }

            var spec = new ChartSpec
            {
                Kind = ChartKind.Pie, Title = name, XTitle = name, YTitle = "Frekuensi",
                TotalN = h.Total
            };
            foreach (var (kategori, n) in h.Kategori) spec.Bars.Add((kategori, n));

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Frekuensi));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Variabel", name),
                ("Banyak kategori", $"{h.Kategori.Count}"),
                ("Valid / hilang", $"{h.Valid} / {h.Hilang}")));

            var kolom = new[] { "Kategori", "Frekuensi", "Persen", "Persen valid" };
            var baris = new List<List<string>>();
            foreach (var (kategori, n) in h.Kategori)
                baris.Add(new List<string>
                {
                    kategori, Fmt.Int(n),
                    Fmt.Num(h.Total > 0 ? 100.0 * n / h.Total : 0, 1),
                    Fmt.Num(h.Valid > 0 ? 100.0 * n / h.Valid : 0, 1)
                });
            blocks.Add(Blocks.Table("Frekuensi", kolom, baris,
                $"Valid: {h.Valid} · Hilang: {h.Hilang} · Total: {h.Total}"));

            blocks.Add(Blocks.Chart($"Diagram lingkaran — {name}", spec));

            return blocks;
        }
    }
}
