using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Aggregate
    {

        public enum Fungsi { Rerata, Jumlah, N, Minimum, Maksimum, Median }

        public class AgregatGrup
        {
            public List<string> Label = new();

            public List<List<double>> Angka = new();

            public List<double> Hasil = new();
        }

        public class AgregatHasil
        {
            public bool Kosong;
            public string Galat = "";
            public List<string> Breaks = new();
            public List<string> Values = new();
            public Fungsi F;
            public List<AgregatGrup> Grup = new();
        }

        public static Fungsi Pakai(string nama)
            => nama switch
            {
                "jumlah" => Fungsi.Jumlah,
                "n" => Fungsi.N,
                "minimum" => Fungsi.Minimum,
                "maksimum" => Fungsi.Maksimum,
                "median" => Fungsi.Median,
                _ => Fungsi.Rerata
            };

        private static string Singkat(Fungsi f)
            => f switch
            {
                Fungsi.Jumlah => "jumlah",
                Fungsi.N => "n",
                Fungsi.Minimum => "min",
                Fungsi.Maksimum => "maks",
                Fungsi.Median => "median",
                _ => "rerata"
            };

        private static double Hitung(Fungsi f, List<double> v)
        {
            if (v.Count == 0) return double.NaN;
            switch (f)
            {
                case Fungsi.Jumlah: return v.Sum();
                case Fungsi.N: return v.Count;
                case Fungsi.Minimum: return v.Min();
                case Fungsi.Maksimum: return v.Max();
                case Fungsi.Median: return Descriptives.Quantile(v, 0.5);
                default: return v.Average();
            }
        }

        public static AgregatHasil Hitung(Dataset ds, List<string> breaks, List<string> values, string fungsi)
        {
            var hasil = new AgregatHasil { Breaks = breaks, Values = values, F = Pakai(fungsi) };

            if (breaks.Count == 0 || values.Count == 0)
            {
                hasil.Kosong = true;
                hasil.Galat = "Butuh sedikitnya satu variabel pengelompokan dan satu variabel yang diagregasi.";
                return hasil;
            }

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
                    double? angka = iv >= 0 && iv < ds.Rows[r].Length
                        ? Dataset.ToDouble(ds.Rows[r][iv]) : null;
                    if (angka.HasValue) entri.Nilai[j].Add(angka.Value);
                }
            }

            if (grup.Count == 0)
            {
                hasil.Kosong = true;
                hasil.Galat = "Tidak ada kelompok yang terbentuk (semua baris punya nilai kunci kosong).";
                return hasil;
            }

            foreach (string k in urutan)
            {
                var e = grup[k];
                var g = new AgregatGrup { Label = e.Label, Angka = e.Nilai };
                foreach (var daftar in e.Nilai) g.Hasil.Add(Hitung(hasil.F, daftar));
                hasil.Grup.Add(g);
            }
            return hasil;
        }

        public static List<ResultBlock> AgregatBlocks(Dataset ds, List<string> breaks, List<string> values, string fungsi)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading("Agregasi per kelompok", 1) };
            var h = Hitung(ds, breaks, values, fungsi);

            if (h.Kosong)
            {
                blocks.Add(Blocks.Note(h.Galat, NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Agregat));

            
            var contoh = h.Grup[0];
            var langkah = new List<(string, string)>
            {
                ("Variabel pengelompokan", string.Join(", ", h.Breaks)),
                ("Variabel yang diagregasi", string.Join(", ", h.Values)),
                ("Fungsi ringkasan", Singkat(h.F)),
                ("Banyak kelompok terbentuk", $"{h.Grup.Count}")
            };
            if (contoh.Angka.Count > 0)
            {
                var angka0 = contoh.Angka[0];
                if (h.F == Fungsi.Rerata && angka0.Count > 0)
                    langkah.Add(($"Contoh rerata — kelompok '{string.Join(", ", contoh.Label)}' ({h.Values[0]})",
                                 $"x̄ = Σxᵢ / n = {Fmt.Num(angka0.Sum())} "
                                 + $"/ {Fmt.Int(angka0.Count)} = {Fmt.Num(contoh.Hasil[0])}"));
                else if (h.F == Fungsi.Jumlah && angka0.Count > 0)
                    langkah.Add(($"Contoh jumlah — kelompok '{string.Join(", ", contoh.Label)}' ({h.Values[0]})",
                                 $"Σxᵢ = {Fmt.Num(angka0.Sum())}   (n = {Fmt.Int(angka0.Count)})"));
                else if (h.F == Fungsi.N)
                    langkah.Add(($"Contoh n — kelompok '{string.Join(", ", contoh.Label)}' ({h.Values[0]})",
                                 $"n = {Fmt.Int(angka0.Count)} amatan tidak kosong"));
            }
            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data", langkah.ToArray()));

            
            var kolom = new List<string>(h.Breaks);
            foreach (string v in h.Values) kolom.Add($"{v}_{Singkat(h.F)}");

            var baris = new List<List<string>>();
            foreach (var g in h.Grup)
            {
                var r = new List<string>(g.Label);
                foreach (double nilai in g.Hasil)
                    r.Add(double.IsNaN(nilai) ? Fmt.NA : Fmt.Num(nilai));
                baris.Add(r);
            }

            blocks.Add(Blocks.Table("Ringkasan per kelompok", kolom, baris,
                $"Fungsi: {Singkat(h.F)}. Kelompok dengan nilai kunci kosong dilewati. "
                + $"Total {h.Grup.Count} kelompok dari {ds.RowCount} baris."));

            return blocks;
        }
    }
}
