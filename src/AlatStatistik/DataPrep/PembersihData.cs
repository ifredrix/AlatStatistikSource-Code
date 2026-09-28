using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AlatStatistik.Models;
using AlatStatistik.Statistics;

namespace AlatStatistik.DataPrep
{

    public enum CaraIsiKosong
    {
        Rerata,     
        Median,     
        Modus,      
        NilaiTetap  
    }

    public enum CaraPencilan
    {
        BuangBaris,   
        BatasiNilai   
    }

    public class HasilPembersihan
    {
        public int BarisSebelum;
        public int BarisSesudah;
        public int BarisDibuang;
        public int SelDiisi;
        public int NilaiDibatasi;
        public List<string> Rincian = new();

        public bool AdaPerubahan => BarisDibuang > 0 || SelDiisi > 0 || NilaiDibatasi > 0;

        public string Ringkasan()
        {
            if (!AdaPerubahan) return "tidak ada yang berubah";

            var bagian = new List<string>();
            if (BarisDibuang > 0) bagian.Add($"{BarisDibuang} baris dibuang");
            if (SelDiisi > 0) bagian.Add($"{SelDiisi} sel diisi");
            if (NilaiDibatasi > 0) bagian.Add($"{NilaiDibatasi} nilai dijepit ke batas");

            return string.Join(", ", bagian)
                 + $" ({BarisSebelum} menjadi {BarisSesudah} baris)";
        }

        public List<ResultBlock> KeBlok()
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Pembersihan data", 1) };

            blok.Add(Blocks.Table("Ringkasan",
                new[] { "Ukuran", "Nilai" },
                new[]
                {
                    new List<string> { "Baris sebelum", Fmt.Int(BarisSebelum) },
                    new List<string> { "Baris sesudah", Fmt.Int(BarisSesudah) },
                    new List<string> { "Baris dibuang", Fmt.Int(BarisDibuang) },
                    new List<string> { "Sel diisi", Fmt.Int(SelDiisi) },
                    new List<string> { "Nilai dijepit ke batas", Fmt.Int(NilaiDibatasi) }
                },
                ""));

            blok.Add(Blocks.Note(AdaPerubahan ? Ringkasan() : "Tidak ada yang berubah.",
                                 AdaPerubahan ? NoteKind.Ok : NoteKind.Info));

            foreach (string r in Rincian) blok.Add(Blocks.Note(r, NoteKind.Info));

            return blok;
        }
    }

    public static class PembersihData
    {
        private static bool Kosong(string? s) => string.IsNullOrWhiteSpace(s);

        

        public static HasilPembersihan IsiKosong(Dataset ds, IEnumerable<string> kolom,
                                                 CaraIsiKosong cara, double nilaiTetap = 0)
        {
            var hasil = new HasilPembersihan { BarisSebelum = ds.RowCount, BarisSesudah = ds.RowCount };

            foreach (string nama in kolom)
            {
                int i = ds.IndexOf(nama);
                if (i < 0) continue;

                bool angka = ds.Variables[i].Measure == Measure.Skala;
                string? pengisi = TentukanPengisi(ds, i, cara, nilaiTetap, angka);

                if (pengisi == null)
                {
                    hasil.Rincian.Add($"{nama}: dilewati, tidak ada nilai yang bisa dipakai");
                    continue;
                }

                int diisi = 0;
                foreach (var baris in ds.Rows)
                {
                    if (!Kosong(baris[i])) continue;
                    baris[i] = pengisi;
                    diisi++;
                }

                hasil.SelDiisi += diisi;
                if (diisi > 0)
                    hasil.Rincian.Add($"{nama}: {diisi} sel diisi dengan {pengisi}");
            }

            return hasil;
        }

        private static string? TentukanPengisi(Dataset ds, int i, CaraIsiKosong cara,
                                               double nilaiTetap, bool angka)
        {
            if (cara == CaraIsiKosong.NilaiTetap)
                return nilaiTetap.ToString(CultureInfo.InvariantCulture);

            if (cara == CaraIsiKosong.Modus)
            {
                var hitung = new Dictionary<string, int>();
                foreach (var baris in ds.Rows)
                {
                    string? v = baris[i];
                    if (Kosong(v)) continue;
                    hitung[v!] = hitung.TryGetValue(v!, out int n) ? n + 1 : 1;
                }
                if (hitung.Count == 0) return null;

                return hitung.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
            }

            
            if (!angka) return null;

            var nilai = ds.Rows
                .Select(r => Dataset.ToDouble(r[i]))
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            if (nilai.Count == 0) return null;

            double pengisi = cara == CaraIsiKosong.Rerata
                ? nilai.Average()
                : Descriptives.Quantile(nilai, 0.5);

            return pengisi.ToString("G10", CultureInfo.InvariantCulture);
        }

        public static HasilPembersihan BuangBarisKosong(Dataset ds, IEnumerable<string> kolom,
                                                        bool semuaHarusKosong = false)
        {
            var hasil = new HasilPembersihan { BarisSebelum = ds.RowCount };
            var idx = kolom.Select(ds.IndexOf).Where(i => i >= 0).ToList();

            if (idx.Count == 0)
            {
                hasil.BarisSesudah = ds.RowCount;
                hasil.Rincian.Add("Tidak ada kolom yang dipilih.");
                return hasil;
            }

            var tersisa = new List<string?[]>();
            foreach (var baris in ds.Rows)
            {
                int jumlahKosong = idx.Count(i => Kosong(baris[i]));
                bool buang = semuaHarusKosong ? jumlahKosong == idx.Count : jumlahKosong > 0;

                if (buang) hasil.BarisDibuang++;
                else tersisa.Add(baris);
            }

            GantiBaris(ds, tersisa);
            hasil.BarisSesudah = ds.RowCount;
            hasil.Rincian.Add($"{idx.Count} kolom diperiksa, "
                + (semuaHarusKosong ? "baris dibuang bila semuanya kosong"
                                    : "baris dibuang bila ada yang kosong"));

            return hasil;
        }

        

        public static HasilPembersihan BuangDuplikat(Dataset ds)
        {
            var hasil = new HasilPembersihan { BarisSebelum = ds.RowCount };
            var pernah = new HashSet<string>();
            var tersisa = new List<string?[]>();

            foreach (var baris in ds.Rows)
            {
                
                string kunci = string.Join("\u0001", baris.Select(v => v ?? ""));

                if (!pernah.Add(kunci)) { hasil.BarisDibuang++; continue; }
                tersisa.Add(baris);
            }

            GantiBaris(ds, tersisa);
            hasil.BarisSesudah = ds.RowCount;

            return hasil;
        }

        public static int HitungDuplikat(Dataset ds)
        {
            var pernah = new HashSet<string>();
            int duplikat = 0;

            foreach (var baris in ds.Rows)
            {
                string kunci = string.Join("\u0001", baris.Select(v => v ?? ""));
                if (!pernah.Add(kunci)) duplikat++;
            }

            return duplikat;
        }

        

        public static HasilPembersihan TanganiPencilan(Dataset ds, IEnumerable<string> kolom,
                                                       CaraPencilan cara)
        {
            var hasil = new HasilPembersihan { BarisSebelum = ds.RowCount };

            var idx = kolom.Select(n => ds.IndexOf(n))
                           .Where(i => i >= 0 && ds.Variables[i].Measure == Measure.Skala)
                           .ToList();

            if (idx.Count == 0)
            {
                hasil.BarisSesudah = ds.RowCount;
                hasil.Rincian.Add("Pilih sedikitnya satu variabel bertingkat ukur skala.");
                return hasil;
            }

            var batas = new Dictionary<int, (double Bawah, double Atas)>();
            foreach (int i in idx)
            {
                var nilai = ds.Rows
                    .Select(r => Dataset.ToDouble(r[i]))
                    .Where(v => v.HasValue)
                    .Select(v => v!.Value)
                    .ToList();

                if (nilai.Count < 4) continue;

                
                
                var s = Descriptives.Summarize(nilai, Descriptives.DefinisiPagarPencilan);
                batas[i] = (s.Q1 - 1.5 * s.Iqr, s.Q3 + 1.5 * s.Iqr);
                hasil.Rincian.Add($"{ds.Variables[i].Name}: batas {Fmt.Num(batas[i].Bawah, 3)} sampai {Fmt.Num(batas[i].Atas, 3)}");
            }

            if (batas.Count == 0)
            {
                hasil.BarisSesudah = ds.RowCount;
                hasil.Rincian.Add("Data terlalu sedikit untuk menghitung kuartil.");
                return hasil;
            }

            if (cara == CaraPencilan.BatasiNilai)
            {
                foreach (var baris in ds.Rows)
                {
                    foreach (var kv in batas)
                    {
                        var sekarang = Dataset.ToDouble(baris[kv.Key]);
                        if (!sekarang.HasValue) continue;

                        double v = sekarang.Value;
                        if (v < kv.Value.Bawah) v = kv.Value.Bawah;
                        else if (v > kv.Value.Atas) v = kv.Value.Atas;
                        else continue;

                        baris[kv.Key] = v.ToString("G10", CultureInfo.InvariantCulture);
                        hasil.NilaiDibatasi++;
                    }
                }

                hasil.BarisSesudah = ds.RowCount;
            }
            else
            {
                var hapus = new HashSet<int>();
                for (int r = 0; r < ds.Rows.Count; r++)
                {
                    foreach (var kv in batas)
                    {
                        var v = Dataset.ToDouble(kv.Key < ds.Rows[r].Length ? ds.Rows[r][kv.Key] : null);
                        if (v.HasValue && (v.Value < kv.Value.Bawah || v.Value > kv.Value.Atas))
                        {
                            hapus.Add(r);
                            break;
                        }
                    }
                }

                var tersisa = ds.Rows.Where((_, r) => !hapus.Contains(r)).ToList();
                hasil.BarisDibuang = hapus.Count;
                GantiBaris(ds, tersisa);
                hasil.BarisSesudah = ds.RowCount;
            }

            return hasil;
        }

        public static int HitungPencilan(Dataset ds, string nama)
        {
            int i = ds.IndexOf(nama);
            if (i < 0 || ds.Variables[i].Measure != Measure.Skala) return 0;

            var nilai = ds.Rows
                .Select(r => Dataset.ToDouble(r[i]))
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            if (nilai.Count < 4) return 0;

            
                
                var s = Descriptives.Summarize(nilai, Descriptives.DefinisiPagarPencilan);
            double bawah = s.Q1 - 1.5 * s.Iqr;
            double atas = s.Q3 + 1.5 * s.Iqr;

            return nilai.Count(v => v < bawah || v > atas);
        }

        

        private static void GantiBaris(Dataset ds, List<string?[]> tersisa)
        {
            ds.Rows.Clear();
            foreach (var r in tersisa) ds.Rows.Add(r);
        }
    }
}
