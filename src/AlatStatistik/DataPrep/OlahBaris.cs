using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.DataPrep
{

    public class KunciUrut
    {
        public string Kolom = "";
        public bool Menurun;

        public override string ToString() => Kolom + (Menurun ? " menurun" : " menaik");
    }

    public class HasilUrut
    {
        public int Baris;
        public List<string> Kunci = new();
        public string Catatan = "";

        public string Ringkasan()
            => Kunci.Count == 0
                ? "Tidak ada kunci pengurutan."
                : $"Mengurutkan {Baris} baris menurut {string.Join(", lalu ", Kunci)}."
                  + (Catatan.Length > 0 ? " " + Catatan : "");

        public List<ResultBlock> KeBlok()
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Urutkan baris") };

            blok.Add(Blocks.Table("Kunci pengurutan",
                new[] { "Urutan ke", "Variabel", "Arah" },
                Kunci.Select((k, i) => new[] { (i + 1).ToString(), k, "" })));

            blok.Add(Blocks.Note(
                Kunci.Count == 0
                    ? "Tidak ada kunci pengurutan, jadi tidak ada yang berubah."
                    : $"{Baris} baris diurutkan menurut {string.Join(", lalu ", Kunci)}."
                      + (Catatan.Length > 0 ? " " + Catatan : ""),
                Kunci.Count == 0 ? NoteKind.Warning : NoteKind.Ok));

            return blok;
        }
    }

    public class Syarat
    {
        public string Kolom = "";
        public string Operator = "==";
        public string Nilai = "";

        public override string ToString() => $"{Kolom} {Operator} {Nilai}";
    }

    public class HasilPilih
    {
        public int BarisSebelum;
        public int BarisSesudah;
        public int BarisDibuang;
        public string Catatan = "";

        public string Ringkasan()
            => $"{BarisSesudah} dari {BarisSebelum} baris tersisa ({BarisDibuang} dibuang)."
               + (Catatan.Length > 0 ? " " + Catatan : "");

        public List<ResultBlock> KeBlok()
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Pilih kasus") };

            blok.Add(Blocks.Table("Hasil penyaringan",
                new[] { "Baris sebelum", "Baris sesudah", "Dibuang" },
                new[] { new[] { BarisSebelum.ToString(), BarisSesudah.ToString(), BarisDibuang.ToString() } }));

            blok.Add(Blocks.Note(Ringkasan(),
                BarisDibuang == 0 ? NoteKind.Warning : NoteKind.Ok));

            return blok;
        }
    }

    public static class OlahBaris
    {

        public static readonly string[] Operator = { "==", "!=", ">", ">=", "<", "<=" };

        

        public static HasilUrut Urutkan(Dataset ds, IEnumerable<KunciUrut>? kunci)
        {
            var daftar = (kunci ?? Enumerable.Empty<KunciUrut>())
                         .Where(k => ds.IndexOf(k.Kolom) >= 0)
                         .ToList();

            var hasil = new HasilUrut { Baris = ds.RowCount };
            if (daftar.Count == 0)
            {
                hasil.Catatan = "Tidak ada kunci pengurutan yang dikenali.";
                return hasil;
            }
            hasil.Kunci = daftar.Select(k => k.ToString()).ToList();

            
            
            var urut = Enumerable.Range(0, ds.Rows.Count).ToList();
            for (int k = daftar.Count - 1; k >= 0; k--)
            {
                int kolom = ds.IndexOf(daftar[k].Kolom);
                bool menurun = daftar[k].Menurun;
                bool angka = ds.Variables[kolom].Measure == Measure.Skala;

                urut = urut.OrderBy(idx => idx, Comparer<int>.Create((a, b) =>
                    Banding(ds, kolom, angka, menurun, a, b))).ToList();
            }

            var baru = urut.Select(idx => ds.Rows[idx]).ToList();
            ds.Rows.Clear();
            foreach (var r in baru) ds.Rows.Add(r);

            return hasil;
        }

        private static int Banding(Dataset ds, int kolom, bool angka, bool menurun, int a, int b)
        {
            string? va = kolom < ds.Rows[a].Length ? ds.Rows[a][kolom] : null;
            string? vb = kolom < ds.Rows[b].Length ? ds.Rows[b][kolom] : null;

            bool ka = string.IsNullOrWhiteSpace(va);
            bool kb = string.IsNullOrWhiteSpace(vb);
            if (ka || kb) return ka && kb ? 0 : (ka ? 1 : -1);

            int h;
            if (angka)
            {
                double? na = Dataset.ToDouble(va), nb = Dataset.ToDouble(vb);
                h = na.HasValue && nb.HasValue
                    ? na.Value.CompareTo(nb.Value)
                    : string.Compare(va, vb, StringComparison.CurrentCultureIgnoreCase);
            }
            else
            {
                h = string.Compare(va, vb, StringComparison.CurrentCultureIgnoreCase);
            }

            return menurun ? -h : h;
        }

        

        public static HasilPilih PilihKasus(Dataset ds, IEnumerable<Syarat>? syarat, bool semuaHarusBenar)
        {
            var daftar = (syarat ?? Enumerable.Empty<Syarat>())
                         .Where(s => ds.IndexOf(s.Kolom) >= 0)
                         .ToList();

            var hasil = new HasilPilih { BarisSebelum = ds.RowCount };
            if (daftar.Count == 0)
            {
                hasil.BarisSesudah = ds.RowCount;
                hasil.Catatan = "Tidak ada syarat, semua baris tetap dipakai.";
                return hasil;
            }

            var tersisa = new List<string?[]>();
            for (int r = 0; r < ds.Rows.Count; r++)
            {
                bool lolos = semuaHarusBenar
                    ? daftar.All(s => Cocok(s, ds, r))
                    : daftar.Any(s => Cocok(s, ds, r));
                if (lolos) tersisa.Add(ds.Rows[r]);
            }

            
            
            
            if (tersisa.Count == 0)
            {
                hasil.BarisSesudah = hasil.BarisSebelum;
                hasil.BarisDibuang = 0;
                hasil.Catatan = "Tidak ada baris yang lolos, jadi data tidak diubah.";
                return hasil;
            }

            ds.Rows.Clear();
            foreach (var r in tersisa) ds.Rows.Add(r);

            hasil.BarisSesudah = ds.RowCount;
            hasil.BarisDibuang = hasil.BarisSebelum - hasil.BarisSesudah;

            return hasil;
        }

        private static bool Cocok(Syarat s, Dataset ds, int r)
        {
            int i = ds.IndexOf(s.Kolom);
            string? v = i < ds.Rows[r].Length ? ds.Rows[r][i] : null;
            if (string.IsNullOrWhiteSpace(v)) return false;   

            bool angka = ds.Variables[i].Measure == Measure.Skala;
            double? na = Dataset.ToDouble(v);
            double? nb = Dataset.ToDouble(s.Nilai);

            if (angka && na.HasValue && nb.HasValue)
            {
                return s.Operator switch
                {
                    "==" => Math.Abs(na.Value - nb.Value) < 1e-12,
                    "!=" => Math.Abs(na.Value - nb.Value) >= 1e-12,
                    ">"  => na.Value > nb.Value,
                    ">=" => na.Value >= nb.Value,
                    "<"  => na.Value < nb.Value,
                    "<=" => na.Value <= nb.Value,
                    _    => Math.Abs(na.Value - nb.Value) < 1e-12
                };
            }

            int c = string.Compare(v, s.Nilai, StringComparison.CurrentCultureIgnoreCase);
            return s.Operator switch
            {
                "==" => c == 0,
                "!=" => c != 0,
                ">"  => c > 0,
                ">=" => c >= 0,
                "<"  => c < 0,
                "<=" => c <= 0,
                _    => c == 0
            };
        }
    }
}
