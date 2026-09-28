using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.DataPrep
{

    public enum ModeUrutVariabel
    {

        NamaNaik,

        NamaTurun,

        TingkatUkur,

        Balik
    }

    public static class UrutVariabel
    {

        public static List<ResultBlock> Terapkan(Dataset data, ModeUrutVariabel mode)
        {
            var blok = new List<ResultBlock>();
            if (data.IsEmpty)
            {
                blok.Add(Blocks.Note("Tidak ada variabel yang bisa diurutkan.", NoteKind.Warning));
                return blok;
            }

            var urutan = Susun(data, mode);

            bool sama = urutan.Select((kolom, i) => kolom == i).All(x => x);
            string namaMode = NamaMode(mode);

            blok.Add(Blocks.Heading("Urutkan variabel", 1));
            blok.Add(Blocks.Note(
                $"Cara urut: {namaMode}. Jumlah variabel: {data.Variables.Count}. "
                + "Perubahan ini menyusun ulang kolom, bukan baris, dan tidak diurungkan dengan Ctrl+Z.",
                NoteKind.Info));

            if (sama)
            {
                blok.Add(Blocks.Note(
                    "Urutan variabel sudah sesuai dengan cara urut yang dipilih — tidak ada yang berubah.",
                    NoteKind.Ok));
                return blok;
            }

            string sebelum = string.Join(", ", data.Variables.Select(v => v.Name));
            var baru = new List<Variable>();
            foreach (int i in urutan) baru.Add(data.Variables[i]);

            foreach (var baris in data.Rows)
            {
                if (baris == null) continue;
                var lama = (string?[])baris.Clone();
                for (int i = 0; i < urutan.Count && i < baris.Length; i++)
                    // Indeks yang dipakai adalah urutan[i], bukan i: baris bisa
                    // lebih pendek dari jumlah kolom setelah variabel ditambah.
                    baris[i] = urutan[i] < lama.Length ? lama[urutan[i]] : null;
            }

            data.Variables.Clear();
            data.Variables.AddRange(baru);
            SegarkanIndeks(data);

            string sesudah = string.Join(", ", data.Variables.Select(v => v.Name));

            blok.Add(Blocks.Table("Urutan baru",
                new[] { "Ke", "Variabel", "Tingkat ukur" },
                data.Variables.Select((v, i) => new List<string>
                {
                    (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    v.Name,
                    v.Measure.ToString()
                })));

            blok.Add(Blocks.Note($"Sebelum: {sebelum}\nSesudah: {sesudah}", NoteKind.Ok));
            return blok;
        }

        public static List<int> Susun(Dataset data, ModeUrutVariabel mode)
        {
            int n = data.Variables.Count;
            var indeks = Enumerable.Range(0, n).ToList();

            switch (mode)
            {
                case ModeUrutVariabel.NamaNaik:
                    return indeks
                        .OrderBy(i => data.Variables[i].Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(i => i)
                        .ToList();

                case ModeUrutVariabel.NamaTurun:
                    return indeks
                        .OrderByDescending(i => data.Variables[i].Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(i => i)
                        .ToList();

                case ModeUrutVariabel.TingkatUkur:
                    return indeks
                        .OrderBy(i => Pangkat(data.Variables[i].Measure))
                        .ThenBy(i => data.Variables[i].Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(i => i)
                        .ToList();

                case ModeUrutVariabel.Balik:
                    indeks.Reverse();
                    return indeks;

                default:
                    return indeks;
            }
        }

        public static string NamaMode(ModeUrutVariabel mode) => mode switch
        {
            ModeUrutVariabel.NamaNaik => "nama A → Z",
            ModeUrutVariabel.NamaTurun => "nama Z → A",
            ModeUrutVariabel.TingkatUkur => "tingkat ukur (Skala → Ordinal → Nominal)",
            ModeUrutVariabel.Balik => "balik urutan sekarang",
            _ => "urutan apa adanya"
        };

        private static int Pangkat(Measure m) => m switch
        {
            Measure.Skala => 0,
            Measure.Ordinal => 1,
            Measure.Nominal => 2,
            _ => 3
        };

        private static void SegarkanIndeks(Dataset data)
        {
            
            
            
        }
    }
}
