using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AlatStatistik.Models;

namespace AlatStatistik.DataPrep
{

    public class JendelaOlah : Window
    {
        private readonly Dataset _data;

        private readonly ComboBox _operasi = new();
        private readonly ListBox _kolom = new();
        private readonly StackPanel _grupKolom = new();

        private readonly StackPanel _grupNormal = new();
        private readonly ComboBox _caraNormal = new();
        private readonly CheckBox _timpa = new() { Content = "Timpa kolom aslinya" };
        private readonly CheckBox _dariLatih = new() { Content = "Hitung statistik dari baris latih saja" };

        private readonly StackPanel _grupKode = new();
        private readonly ComboBox _caraKode = new();

        private readonly StackPanel _grupBagi = new();
        private readonly TextBox _porsi = new() { Text = "0,8" };
        private readonly TextBox _benih = new() { Text = "42" };

        
        
        private readonly TextBlock _pratinjau = Bantu.KotakPratinjau();

        public JendelaOlah(Dataset data, int operasi = 0)
        {
            _data = data;

            Title = "Persiapan data";
            Width = 520;
            Height = 620;
            MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            Background = Bantu.Warna("Latar");

            var panel = new StackPanel { Margin = new Thickness(18) };

            
            _operasi.SelectionChanged += (_, _) => PerbaruiTampilan();
            _operasi.Items.Add("Normalisasi (setarakan skala)");
            _operasi.Items.Add("Ubah kategori jadi angka");
            _operasi.Items.Add("Bagi data latih & uji");
            _operasi.SelectedIndex = 0;
            panel.Children.Add(Bantu.BeriLabel("Operasi", _operasi));
            if (operasi >= 0 && operasi < _operasi.Items.Count) _operasi.SelectedIndex = operasi;

            
            _kolom = Bantu.DaftarKolom(data);
            _grupKolom.Children.Add(Bantu.BeriLabel("Variabel yang diproses (bisa pilih beberapa)", _kolom));
            panel.Children.Add(_grupKolom);

            
            _caraNormal.Items.Add("Min–maks (hasil 0 sampai 1)");
            _caraNormal.Items.Add("Z-skor (rerata 0, simpangan baku 1)");
            _caraNormal.SelectedIndex = 0;
            _grupNormal.Children.Add(Bantu.BeriLabel("Cara menormalisasi", _caraNormal));

            _timpa.Foreground = Bantu.Warna("Teks");
            _timpa.Margin = new Thickness(0, 4, 0, 2);
            _grupNormal.Children.Add(_timpa);

            _dariLatih.Foreground = Bantu.Warna("Teks");
            _dariLatih.Margin = new Thickness(0, 0, 0, 4);
            _grupNormal.Children.Add(_dariLatih);

            _grupNormal.Children.Add(new TextBlock
            {
                Text = "Bila tidak ditimpa, hasilnya disimpan di kolom baru "
                     + "(nama_minmaks atau nama_z) supaya aslinya tetap ada.",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Bantu.Warna("TeksRedup")
            });
            panel.Children.Add(_grupNormal);

            
            _caraKode.Items.Add("One-hot (satu kolom per kategori)");
            _caraKode.Items.Add("Nomor kategori (1, 2, 3, ...)");
            _caraKode.SelectedIndex = 0;
            _grupKode.Children.Add(Bantu.BeriLabel("Cara mengubah", _caraKode));
            panel.Children.Add(_grupKode);

            
            _porsi.Background = Bantu.Warna("Panel2");
            _porsi.Foreground = Bantu.Warna("Teks");
            _grupBagi.Children.Add(Bantu.BeriLabel("Porsi data latih (0,8 berarti 80%)", _porsi));

            _benih.Background = Bantu.Warna("Panel2");
            _benih.Foreground = Bantu.Warna("Teks");
            _grupBagi.Children.Add(Bantu.BeriLabel("Benih acak (angka sama = pembagian sama)", _benih));
            panel.Children.Add(_grupBagi);

            
            var tombolPratinjau = new Button
            {
                Content = "Hitung pratinjau",
                Padding = new Thickness(16, 7, 16, 7),
                Margin = new Thickness(0, 10, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            tombolPratinjau.Click += (_, _) =>
                _pratinjau.Text = "Yang akan terjadi: " + Coba(_data.Clone());
            panel.Children.Add(tombolPratinjau);

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

            var terapkan = new Button
            {
                Content = "Terapkan",
                Padding = new Thickness(18, 7, 18, 7),
                Style = (Style)Application.Current.Resources["TombolUtama"]
            };
            terapkan.Click += (_, _) =>
            {
                if (_operasi.SelectedIndex != 2 && _kolom.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Pilih sedikitnya satu variabel.", "Belum lengkap",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                DialogResult = true;
            };

            baris.Children.Add(batal);
            baris.Children.Add(terapkan);
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
            int op = _operasi.SelectedIndex;

            _grupKolom.Visibility = op == 2 ? Visibility.Collapsed : Visibility.Visible;
            _grupNormal.Visibility = op == 0 ? Visibility.Visible : Visibility.Collapsed;
            _grupKode.Visibility = op == 1 ? Visibility.Visible : Visibility.Collapsed;
            _grupBagi.Visibility = op == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        private string Coba(Dataset uji)
        {
            try
            {
                var kolom = Bantu.KolomDipilih(_kolom);

                return _operasi.SelectedIndex switch
                {
                    0 => Normalisasi.Jalankan(uji, kolom, CaraNormal(), _timpa.IsChecked == true,
                                               BarisAcuan(uji)).Ringkasan(),
                    1 => Pengkodean.Jalankan(uji, kolom, CaraKode()).Ringkasan(),
                    _ => BagiData.Jalankan(uji, Porsi(), Benih()).Ringkasan()
                };
            }
            catch (Exception galat)
            {
                return "Gagal menghitung pratinjau: " + galat.Message;
            }
        }

        public List<ResultBlock> Jalankan(Dataset ds)
        {
            var kolom = Bantu.KolomDipilih(_kolom);

            return _operasi.SelectedIndex switch
            {
                0 => Normalisasi.Jalankan(ds, kolom, CaraNormal(), _timpa.IsChecked == true,
                                          BarisAcuan(ds)).KeBlok(),
                1 => Pengkodean.Jalankan(ds, kolom, CaraKode()).KeBlok(),
                _ => BagiData.Jalankan(ds, Porsi(), Benih()).KeBlok()
            };
        }

        private CaraNormalisasi CaraNormal()
            => _caraNormal.SelectedIndex == 1 ? CaraNormalisasi.ZScore : CaraNormalisasi.MinMax;

        private CaraPengkodean CaraKode()
            => _caraKode.SelectedIndex == 1 ? CaraPengkodean.Label : CaraPengkodean.OneHot;

        private List<int>? BarisAcuan(Dataset ds)
        {
            if (_dariLatih.IsChecked != true) return null;

            int i = ds.IndexOf("Peran");
            if (i < 0) return null;

            var baris = new List<int>();
            for (int r = 0; r < ds.Rows.Count; r++)
            {
                string? peran = i < ds.Rows[r].Length ? ds.Rows[r][i] : null;
                if (string.Equals(peran, "latih", StringComparison.OrdinalIgnoreCase)) baris.Add(r);
            }

            return baris.Count > 0 ? baris : null;
        }

        private double Porsi()
            => double.TryParse(_porsi.Text.Replace(",", "."), NumberStyles.Float,
                               CultureInfo.InvariantCulture, out double v)
                ? v
                : 0.8;

        private int Benih()
            => int.TryParse(_benih.Text, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int v)
                ? v
                : 42;
    }
}
