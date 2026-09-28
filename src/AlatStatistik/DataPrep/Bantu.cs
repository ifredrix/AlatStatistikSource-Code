using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AlatStatistik.Models;

namespace AlatStatistik.DataPrep
{

    public static class Bantu
    {

        public static string BersihNama(string nama)
        {
            var hasil = new System.Text.StringBuilder();
            foreach (char c in nama.Trim())
                hasil.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');

            string bersih = hasil.ToString().Trim('_');
            if (bersih.Length == 0) bersih = "kolom";
            if (char.IsDigit(bersih[0])) bersih = "k_" + bersih;

            return bersih;
        }

        public static string NamaUnik(Dataset ds, string dasar)
        {
            string nama = BersihNama(dasar);
            string calon = nama;
            int nomor = 2;

            while (ds.IndexOf(calon) >= 0) calon = nama + "_" + nomor++;

            return calon;
        }

        public static int TambahKolom(Dataset ds, string nama, IList<string?> nilai,
                                     Measure ukur = Measure.Skala, int desimal = 4)
        {
            string namaUnik = NamaUnik(ds, nama);

            ds.Variables.Add(new Variable
            {
                Name = namaUnik,
                Label = nama,
                Measure = ukur,
                Decimals = desimal
            });

            // Panjang baris dipatok ke jumlah variabel, bukan ke panjang
            // lamanya: baris yang lebih pendek ikut terisi, yang terlalu
            // panjang terpotong, sehingga ukurannya selalu sejalan.
            int kolom = ds.Variables.Count;
            for (int r = 0; r < ds.Rows.Count; r++)
            {
                string?[] lama = ds.Rows[r];
                var baru = new string?[kolom];
                Array.Copy(lama, baru, Math.Min(lama.Length, kolom - 1));
                baru[kolom - 1] = r < nilai.Count ? nilai[r] : null;
                ds.Rows[r] = baru;
            }

            return ds.Variables.Count - 1;
        }

        public static bool BuangKolom(Dataset ds, string nama)
        {
            int i = ds.IndexOf(nama);
            if (i < 0) return false;

            ds.Variables.RemoveAt(i);

            // `lama.Length - 1` melempar pengecualian bila barisnya kosong,
            // jadi ukuran baris diambil dari jumlah variabel tersisa.
            int kolom = ds.Variables.Count;
            for (int r = 0; r < ds.Rows.Count; r++)
            {
                string?[] lama = ds.Rows[r];
                var baru = new string?[kolom];
                for (int c = 0, t = 0; c < lama.Length && t < kolom; c++)
                    if (c != i) baru[t++] = lama[c];
                ds.Rows[r] = baru;
            }

            return true;
        }

        public static List<double?> Angka(Dataset ds, int i)
            => ds.Rows.Select(r => Dataset.ToDouble(i < r.Length ? r[i] : null)).ToList();

        public static List<double> AngkaTerisi(Dataset ds, int i)
            => Angka(ds, i).Where(v => v.HasValue).Select(v => v!.Value).ToList();

        

        public static Brush Warna(string kunci)
            => (Brush)Application.Current.Resources[kunci];

        public static StackPanel BeriLabel(string teks, UIElement kontrol)
        {
            var bungkus = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            bungkus.Children.Add(new TextBlock
            {
                Text = teks,
                FontSize = 11.5,
                Foreground = Warna("TeksRedup"),
                Margin = new Thickness(0, 0, 0, 3)
            });
            bungkus.Children.Add(kontrol);
            return bungkus;
        }

        public static TextBlock KotakPratinjau()
        {
            return new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = Warna("Teks"),
                Background = Warna("Panel2"),
                Padding = new Thickness(10),
                MinHeight = 70,
                Margin = new Thickness(0, 0, 0, 8)
            };
        }

        public static ListBox DaftarKolom(Dataset ds, double tinggi = 150)
        {
            var daftar = new ListBox
            {
                SelectionMode = SelectionMode.Extended,
                Height = tinggi,
                Background = Warna("Panel2"),
                Foreground = Warna("Teks"),
                BorderBrush = Warna("Garis")
            };
            foreach (string nama in ds.Names) daftar.Items.Add(nama);
            return daftar;
        }

        public static List<string> KolomDipilih(ListBox daftar)
            => daftar.SelectedItems.Cast<string>().ToList();
    }
}
