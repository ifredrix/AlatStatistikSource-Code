using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class CountValues
    {

        public class Hasil
        {
            public List<int> PerVar = new();
            public int TotalSel;
            public int TotalCocok;
            public List<(List<int> Cocok, int Jumlah)> PerBaris = new();
        }

        public static Hasil Hitung(Dataset ds, List<string> vars, double target,
                                    bool pakaiRentang, double bawah, double atas)
        {
            var hasil = new Hasil();
            bool Cocok(double? x)
            {
                if (!x.HasValue) return false;
                return pakaiRentang ? x.Value >= bawah && x.Value <= atas : Math.Abs(x.Value - target) < 1e-12;
            }

            var indeks = vars.Select(v => ds.IndexOf(v)).ToList();
            hasil.PerVar = vars.Select(_ => 0).ToList();

            for (int r = 0; r < ds.RowCount; r++)
            {
                var cocok = new List<int>();
                int jumlah = 0;
                for (int j = 0; j < indeks.Count; j++)
                {
                    int iv = indeks[j];
                    double? angka = iv >= 0 && iv < ds.Rows[r].Length ? Dataset.ToDouble(ds.Rows[r][iv]) : null;
                    bool c = Cocok(angka);
                    cocok.Add(c ? 1 : 0);
                    if (c) { jumlah++; hasil.PerVar[j]++; hasil.TotalCocok++; }
                    if (angka.HasValue) hasil.TotalSel++;
                }
                hasil.PerBaris.Add((cocok, jumlah));
            }
            return hasil;
        }

        public static List<ResultBlock> CountValuesBlocks(Dataset ds, List<string> vars, double target,
                                                bool pakaiRentang, double bawah, double atas)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading("Count Values within Cases (pratinjau)", 1) };
            if (vars.Count == 0)
            {
                blocks.Add(Blocks.Note("Butuh sedikitnya satu variabel yang dihitung.", NoteKind.Error));
                return blocks;
            }

            var hasil = Hitung(ds, vars, target, pakaiRentang, bawah, atas);
            var indeks = vars.Select(v => ds.IndexOf(v)).ToList();
            var perBaris = hasil.PerBaris;
            int totalSel = hasil.TotalSel, totalCocok = hasil.TotalCocok;
            var cocokPerVar = hasil.PerVar;

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.HitungNilaiCocok));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Variabel yang dihitung", string.Join(", ", vars)),
                ("Cara pencocokan", pakaiRentang
                    ? $"nilai dalam rentang [{Fmt.Num(bawah)}, {Fmt.Num(atas)}]"
                    : $"nilai sama dengan {Fmt.Num(target)}"),
                ("Total sel numerik diperiksa", $"{totalSel}"),
                ("Total kecocokan", $"{totalCocok} ({Fmt.Num(totalSel > 0 ? 100.0 * totalCocok / totalSel : 0, 1)}%)")));

            
            var kolomRingkas = new[] { "Variabel", "Banyak cocok", "Persen" };
            var barisRingkas = new List<List<string>>();
            for (int j = 0; j < vars.Count; j++)
            {
                int sel = perBaris.Count > 0 ? Enumerable.Range(0, ds.RowCount).Count(r =>
                {
                    int iv = indeks[j];
                    return iv >= 0 && iv < ds.Rows[r].Length && Dataset.ToDouble(ds.Rows[r][iv]).HasValue;
                }) : 0;
                barisRingkas.Add(new List<string>
                {
                    vars[j], Fmt.Int(cocokPerVar[j]),
                    Fmt.Num(sel > 0 ? 100.0 * cocokPerVar[j] / sel : 0, 1)
                });
            }
            blocks.Add(Blocks.Table("Ringkasan per variabel", kolomRingkas, barisRingkas,
                "Persen relatif terhadap banyaknya sel numerik (bukan kosong) tiap variabel."));

            
            int pratinjau = Math.Min(perBaris.Count, 50);
            var kolomBaris = new List<string> { "Baris" };
            kolomBaris.AddRange(vars);
            kolomBaris.Add("Jumlah");
            var barisBaris = new List<List<string>>();
            for (int r = 0; r < pratinjau; r++)
            {
                var rb = new List<string> { Fmt.Int(r + 1) };
                rb.AddRange(perBaris[r].Cocok.Select(c => c.ToString()));
                rb.Add(Fmt.Int(perBaris[r].Jumlah));
                barisBaris.Add(rb);
            }
            blocks.Add(Blocks.Table("Pratinjau per baris", kolomBaris, barisBaris,
                $"Menampilkan {pratinjau} dari {perBaris.Count} baris. Di acuan, hasil ini "
                + $"menjadi variabel baru; di sini hanya ditampilkan sebagai laporan."));

            return blocks;
        }
    }
}
