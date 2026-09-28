using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.DataPrep
{

    public class AturanUbah
    {
        public double? Dari;
        public double? Sampai;
        public double? Jadi;
        public bool UntukLainnya;

        public override string ToString()
        {
            if (UntukLainnya) return $"nilai lain -> {(Jadi.HasValue ? Jadi.Value.ToString("G10") : "(kosong)")}";
            string kiri = Dari.HasValue ? Dari.Value.ToString("G10") : "terendah";
            string kanan = Sampai.HasValue ? Sampai.Value.ToString("G10") : "tertinggi";
            string rentang = Dari.HasValue && Sampai.HasValue && Math.Abs(Dari.Value - Sampai.Value) < 1e-12
                ? kiri
                : $"{kiri} s/d {kanan}";
            return $"{rentang} -> {(Jadi.HasValue ? Jadi.Value.ToString("G10") : "(kosong)")}";
        }
    }

    public enum CaraIsi
    {
        RerataSeri,
        MedianSeri,
        InterpolasiLinier,
        TetanggaSebelumnya,
        TetanggaBerikutnya,
        NilaiTetap
    }

    public static class UbahNilai
    {
        public class HasilUbah
        {
            public List<double?> Nilai = new();
            public int Berubah, Tetap, JadiKosong, HilangDilalui;
            public string? Galat;
            public bool Sukses => Galat is null;
        }

        public static HasilUbah Ubah(Dataset ds, string kolom, List<AturanUbah> aturan)
        {
            var h = new HasilUbah();
            int k = ds.IndexOf(kolom);
            if (k < 0) { h.Galat = $"Variabel “{kolom}” tidak ada dalam data."; return h; }
            if (aturan is null || aturan.Count == 0) { h.Galat = "Belum ada aturan."; return h; }

            var biasa = aturan.Where(a => !a.UntukLainnya).ToList();
            var lainnya = aturan.Where(a => a.UntukLainnya).ToList();
            if (lainnya.Count > 1) { h.Galat = "Aturan “nilai lain” hanya boleh satu."; return h; }

            for (int r = 0; r < ds.RowCount; r++)
            {
                string? mentah = k < ds.Rows[r].Length ? ds.Rows[r][k] : null;
                double? v = Dataset.ToDouble(mentah);

                if (v is null)
                {
                    
                    
                    
                    h.Nilai.Add(null);
                    h.HilangDilalui++;
                    continue;
                }

                AturanUbah? kena = null;
                foreach (var a in biasa)
                {
                    bool bawah = !a.Dari.HasValue || v.Value >= a.Dari.Value - 1e-12;
                    bool atas = !a.Sampai.HasValue || v.Value <= a.Sampai.Value + 1e-12;
                    if (bawah && atas) { kena = a; break; }
                }
                kena ??= lainnya.FirstOrDefault();

                if (kena is null)
                {
                    h.Nilai.Add(v);          
                    h.Tetap++;
                    continue;
                }

                if (kena.Jadi is null) { h.Nilai.Add(null); h.JadiKosong++; }
                else if (Math.Abs(kena.Jadi.Value - v.Value) < 1e-12) { h.Nilai.Add(v); h.Tetap++; }
                else { h.Nilai.Add(kena.Jadi); h.Berubah++; }
            }

            return h;
        }

        public static HasilUbah Isi(Dataset ds, string kolom, CaraIsi cara, double tetap = 0)
        {
            var h = new HasilUbah();
            int k = ds.IndexOf(kolom);
            if (k < 0) { h.Galat = $"Variabel “{kolom}” tidak ada dalam data."; return h; }

            var nilai = new List<double?>();
            for (int r = 0; r < ds.RowCount; r++)
                nilai.Add(k < ds.Rows[r].Length ? Dataset.ToDouble(ds.Rows[r][k]) : null);

            var ada = nilai.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (ada.Count == 0)
            {
                h.Galat = $"Variabel “{kolom}” tidak punya satu pun nilai terisi, "
                        + "jadi tidak ada yang bisa dipakai untuk mengisi.";
                return h;
            }

            for (int r = 0; r < nilai.Count; r++)
            {
                if (nilai[r].HasValue) { h.Nilai.Add(nilai[r]); h.Tetap++; continue; }

                double? ganti = cara switch
                {
                    CaraIsi.RerataSeri        => ada.Average(),
                    CaraIsi.MedianSeri        => Median(ada),
                    CaraIsi.NilaiTetap        => tetap,
                    CaraIsi.InterpolasiLinier => Interpolasi(nilai, r),
                    CaraIsi.TetanggaSebelumnya=> Sebelumnya(nilai, r),
                    CaraIsi.TetanggaBerikutnya=> Berikutnya(nilai, r),
                    _                         => null
                };

                if (ganti is null) { h.Nilai.Add(null); h.HilangDilalui++; }
                else { h.Nilai.Add(ganti); h.Berubah++; }
            }

            return h;
        }

        private static double Median(List<double> ada)
        {
            var u = ada.OrderBy(x => x).ToList();
            int n = u.Count;
            return n % 2 == 1 ? u[n / 2] : 0.5 * (u[n / 2 - 1] + u[n / 2]);
        }

        private static double? Interpolasi(List<double?> nilai, int i)
        {
            int k = -1, m = -1;
            for (int j = i - 1; j >= 0; j--) if (nilai[j].HasValue) { k = j; break; }
            for (int j = i + 1; j < nilai.Count; j++) if (nilai[j].HasValue) { m = j; break; }

            if (k >= 0 && m >= 0)
                return nilai[k]!.Value + (nilai[m]!.Value - nilai[k]!.Value) * (i - k) / (double)(m - k);
            if (k >= 0) return nilai[k]!.Value;
            if (m >= 0) return nilai[m]!.Value;
            return null;
        }

        private static double? Sebelumnya(List<double?> nilai, int i)
        {
            for (int j = i - 1; j >= 0; j--) if (nilai[j].HasValue) return nilai[j];
            return null;
        }

        private static double? Berikutnya(List<double?> nilai, int i)
        {
            for (int j = i + 1; j < nilai.Count; j++) if (nilai[j].HasValue) return nilai[j];
            return null;
        }
    }
}
