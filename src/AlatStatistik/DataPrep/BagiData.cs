using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;
using AlatStatistik.Statistics;

namespace AlatStatistik.DataPrep
{

    public class HasilPembagian
    {
        public int BarisLatih;
        public int BarisUji;
        public int BarisTotal;
        public double PorsiLatih;
        public int Benih;
        public string KolomPeran = "";
        public List<string> Catatan = new();

        public bool AdaPerubahan => KolomPeran.Length > 0;

        public string Ringkasan()
            => AdaPerubahan
                ? $"{BarisLatih} baris latih, {BarisUji} baris uji"
                : "tidak ada yang berubah";

        public List<ResultBlock> KeBlok()
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Pembagian data latih dan uji", 1) };

            if (!AdaPerubahan)
            {
                blok.Add(Blocks.Note("Tidak ada yang dibagi. " + string.Join(" ", Catatan),
                                     NoteKind.Warning));
                return blok;
            }

            double persenLatih = BarisTotal > 0 ? 100.0 * BarisLatih / BarisTotal : 0.0;
            double persenUji = BarisTotal > 0 ? 100.0 * BarisUji / BarisTotal : 0.0;

            blok.Add(Blocks.Table("Hasil",
                new[] { "Bagian", "Baris", "Persen" },
                new[]
                {
                    new List<string> { "Latih", Fmt.Int(BarisLatih), Fmt.Num(persenLatih, 1) + "%" },
                    new List<string> { "Uji", Fmt.Int(BarisUji), Fmt.Num(persenUji, 1) + "%" }
                },
                $"Baris ditandai di kolom '{KolomPeran}'. Benih acak {Benih}: angka yang sama "
                + "selalu menghasilkan pembagian yang sama persis, jadi percobaan bisa diulang."));

            blok.Add(Blocks.Note(
                "Urutan yang benar untuk pelatihan model: bagi dulu, baru hitung "
                + "statistik penyekalaan dari data latih saja. Kalau menyekala dulu baru "
                + "membagi, nilai data uji ikut menentukan min/maks atau rerata/sd, dan "
                + "hasil uji akan terlalu bagus (kebocoran data).",
                NoteKind.Warning));

            blok.Add(Blocks.Note(
                "Baris dikocok sebelum dibagi. Itu tepat untuk data yang barisnya saling "
                + "bebas, tetapi SALAH untuk deret waktu: untuk data berurutan waktu, "
                + "bagian latih seharusnya masa lalu dan bagian uji masa depan. "
                + "Alat ini belum menyediakan pembagian berurutan waktu.",
                NoteKind.Warning));

            foreach (string c in Catatan)
                blok.Add(Blocks.Note(c, NoteKind.Info));

            return blok;
        }
    }

    public static class BagiData
    {
        public static HasilPembagian Jalankan(Dataset ds, double porsiLatih, int benih,
                                              string namaKolom = "Peran")
        {
            var hasil = new HasilPembagian
            {
                BarisTotal = ds.RowCount,
                PorsiLatih = Math.Clamp(porsiLatih, 0.01, 0.99),
                Benih = benih
            };

            if (ds.RowCount == 0)
            {
                hasil.Catatan.Add("Tidak ada baris untuk dibagi.");
                return hasil;
            }

            if (Math.Abs(hasil.PorsiLatih - porsiLatih) > 1e-9)
                hasil.Catatan.Add($"Porsi {Fmt.Num(porsiLatih, 3)} dijepit ke {Fmt.Num(hasil.PorsiLatih, 3)} "
                                  + "supaya kedua bagian tidak ada yang kosong.");

            // Math.Clamp(x, 1, 0) melempar bila RowCount == 1; satu baris tidak
            // bisa dibagi menjadi latih dan uji sekaligus.
            if (ds.RowCount == 1)
            {
                hasil.Catatan.Add("Data hanya 1 baris — tidak bisa dibagi menjadi latih dan uji.");
                return hasil;
            }

            var urutan = Enumerable.Range(0, ds.RowCount).ToList();
            Kocok(urutan, benih);

            int jumlahLatih = (int)Math.Round(hasil.PorsiLatih * ds.RowCount, MidpointRounding.AwayFromZero);
            jumlahLatih = Math.Clamp(jumlahLatih, 1, ds.RowCount - 1);

            var peran = new string?[ds.RowCount];
            for (int r = 0; r < ds.RowCount; r++) peran[r] = "uji";

            foreach (int r in urutan.Take(jumlahLatih)) peran[r] = "latih";

            int idx = Bantu.TambahKolom(ds, namaKolom, peran, Measure.Nominal, 0);
            hasil.KolomPeran = ds.Variables[idx].Name;
            hasil.BarisLatih = peran.Count(p => p == "latih");
            hasil.BarisUji = peran.Count(p => p == "uji");

            return hasil;
        }

        

        private static void Kocok(List<int> urutan, int benih)
        {
            ulong keadaan = benih == 0 ? 0x9E3779B97F4A7C15UL : (ulong)benih;

            for (int i = urutan.Count - 1; i > 0; i--)
            {
                keadaan ^= keadaan << 13;
                keadaan ^= keadaan >> 7;
                keadaan ^= keadaan << 17;

                int j = (int)(keadaan % (ulong)(i + 1));

                (urutan[i], urutan[j]) = (urutan[j], urutan[i]);
            }
        }
    }
}
