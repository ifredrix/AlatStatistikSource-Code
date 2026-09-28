using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AlatStatistik.DataPrep;
using AlatStatistik.Models;

namespace AlatStatistik.Views
{

    public class JendelaGrafik : Window
    {
        private readonly Dataset _data;

        private readonly ComboBox _jenis = new();
        private readonly ListBox _kolom = new();
        private readonly StackPanel _grupKelompok = new();
        private readonly ComboBox _kelompok = new();
        private readonly StackPanel _grupGaris = new();
        private readonly CheckBox _garis = new() { Content = "Tambah garis regresi" };
        private readonly TextBlock _pratinjau = Bantu.KotakPratinjau();

        public JendelaGrafik(Dataset data, int jenis = 0)
        {
            _data = data;

            Title = "Buat grafik";
            Width = 520;
            Height = 560;
            MinHeight = 440;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            Background = Bantu.Warna("Latar");

            var panel = new StackPanel { Margin = new Thickness(18) };

            
            _jenis.SelectionChanged += (_, _) => PerbaruiTampilan();
            _jenis.Items.Add("Histogram (sebaran nilai)");
            _jenis.Items.Add("Box plot (kuartil & pencilan)");
            _jenis.Items.Add("Diagram pencar (dua variabel)");
            _jenis.Items.Add("Diagram batang (frekuensi kategori)");
            _jenis.Items.Add("Diagram garis (perubahan nilai)");
            _jenis.Items.Add("Diagram lingkaran (komposisi)");
            _jenis.Items.Add("Peta panas (korelasi antar variabel)");
            _jenis.SelectedIndex = 0;
            panel.Children.Add(Bantu.BeriLabel("Jenis grafik", _jenis));
            if (jenis >= 0 && jenis < _jenis.Items.Count) _jenis.SelectedIndex = jenis;

            
            _kolom = Bantu.DaftarKolom(data);
            panel.Children.Add(Bantu.BeriLabel("Variabel yang digambar (bisa pilih beberapa)", _kolom));

            
            _kelompok.Items.Add("(tanpa pengelompokan)");
            foreach (string nama in data.CategoricalColumns()) _kelompok.Items.Add(nama);
            _kelompok.SelectedIndex = 0;
            _grupKelompok.Children.Add(Bantu.BeriLabel("Warnai per kategori (opsional)", _kelompok));
            panel.Children.Add(_grupKelompok);

            
            _garis.Foreground = Bantu.Warna("Teks");
            _grupGaris.Children.Add(_garis);
            panel.Children.Add(_grupGaris);

            
            var tombol = new Button
            {
                Content = "Hitung pratinjau",
                Padding = new Thickness(16, 7, 16, 7),
                Margin = new Thickness(0, 10, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            tombol.Click += (_, _) => HitungPratinjau();
            panel.Children.Add(tombol);

            _pratinjau.Text = "Pilih variabelnya, lalu tekan 'Hitung pratinjau'.";
            panel.Children.Add(_pratinjau);

            
            var baris = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var batal = new Button { Content = "Batal", Padding = new Thickness(18, 7, 18, 7), Margin = new Thickness(0, 0, 10, 0) };
            batal.Click += (_, _) => DialogResult = false;

            var buat = new Button
            {
                Content = "Buat grafik",
                Padding = new Thickness(18, 7, 18, 7),
                Style = (Style)Application.Current.Resources["TombolUtama"]
            };
            buat.Click += (_, _) =>
            {
                if (_kolom.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Pilih sedikitnya satu variabel.", "Belum lengkap",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                DialogResult = true;
            };

            baris.Children.Add(batal);
            baris.Children.Add(buat);
            panel.Children.Add(baris);

            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = panel
            };
            PerbaruiTampilan();
        }

        private void PerbaruiTampilan()
        {
            int jenis = _jenis.SelectedIndex;

            
            
            _grupKelompok.Visibility = jenis is 0 or 1 or 4 ? Visibility.Visible : Visibility.Collapsed;
            _grupGaris.Visibility = jenis == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HitungPratinjau()
        {
            try
            {
                var blok = Buat(_data.Clone());
                int grafik = blok.Count(b => b.Kind == BlockKind.Chart);
                var catatan = blok.Where(b => b.Kind == BlockKind.Note).Select(b => b.Text).ToList();

                _pratinjau.Text = grafik > 0
                    ? $"{grafik} grafik akan digambar."
                    : "Tidak ada grafik yang bisa digambar dari pilihan ini.";

                if (catatan.Count > 0) _pratinjau.Text += "\n\n" + string.Join("\n", catatan);
            }
            catch (Exception galat)
            {
                _pratinjau.Text = "Gagal menghitung pratinjau: " + galat.Message;
            }
        }

        public List<ResultBlock> Buat(Dataset ds)
        {
            var kolom = Bantu.KolomDipilih(_kolom);
            string kelompok = _kelompok.SelectedIndex > 0
                ? _kelompok.SelectedItem?.ToString() ?? ""
                : "";

            return PembuatGrafik.Buat((JenisGrafik)_jenis.SelectedIndex, ds, kolom,
                                      kelompok, _garis.IsChecked == true);
        }
    }
}
