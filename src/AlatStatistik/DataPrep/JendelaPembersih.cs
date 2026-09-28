using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AlatStatistik.Models;

namespace AlatStatistik.DataPrep
{

    public class JendelaPembersih : Window
    {
        private readonly Dataset _data;

        private readonly ComboBox _operasi = new();
        private readonly ListBox _kolom = new();
        private readonly ComboBox _caraIsi = new();
        private readonly TextBox _nilaiTetap = new();
        private readonly ComboBox _caraPencilan = new();
        private readonly StackPanel _grupKolom = new();
        private readonly StackPanel _grupIsi = new();
        private readonly StackPanel _grupPencilan = new();
        private readonly TextBlock _pratinjau = new();

        public JendelaPembersih(Dataset data, int operasiAwal = 0)
        {
            _data = data;

            Title = "Pembersihan data";
            Width = 520;
            Height = 560;
            MinWidth = 460;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            
            
            
            
            ResizeMode = ResizeMode.CanResize;
            Background = (Brush)Application.Current.Resources["Latar"];

            var panel = new StackPanel { Margin = new Thickness(18) };

            
            _operasi.SelectionChanged += (_, _) => PerbaruiTampilan();
            _operasi.Items.Add("Isi nilai kosong");
            _operasi.Items.Add("Buang baris yang ada kosong");
            _operasi.Items.Add("Buang baris duplikat");
            _operasi.Items.Add("Buang baris yang memuat pencilan");
            _operasi.Items.Add("Batasi pencilan ke batas 1,5 x IQR");
            _operasi.SelectedIndex = 0;
            if (operasiAwal >= 0 && operasiAwal < _operasi.Items.Count) _operasi.SelectedIndex = operasiAwal;
            panel.Children.Add(BeriLabel("Operasi", _operasi));

            
            _kolom.SelectionMode = SelectionMode.Extended;
            _kolom.Height = 150;
            _kolom.Background = (Brush)Application.Current.Resources["Panel2"];
            _kolom.Foreground = (Brush)Application.Current.Resources["Teks"];
            _kolom.BorderBrush = (Brush)Application.Current.Resources["Garis"];
            foreach (string nama in data.Names) _kolom.Items.Add(nama);
            _grupKolom.Children.Add(BeriLabel("Variabel yang diproses (bisa pilih beberapa)", _kolom));
            panel.Children.Add(_grupKolom);

            
            _caraIsi.Items.Add("Rerata (variabel skala)");
            _caraIsi.Items.Add("Median (variabel skala)");
            _caraIsi.Items.Add("Modus (nilai paling sering)");
            _caraIsi.Items.Add("Nilai tetap");
            _caraIsi.SelectedIndex = 0;
            _grupIsi.Children.Add(BeriLabel("Cara mengisi", _caraIsi));

            _nilaiTetap.Text = "0";
            _grupIsi.Children.Add(BeriLabel("Nilai tetap (bila dipilih)", _nilaiTetap));
            panel.Children.Add(_grupIsi);

            
            _caraPencilan.Items.Add("Buang barisnya");
            _caraPencilan.Items.Add("Jepit nilainya ke batas");
            _caraPencilan.SelectedIndex = 0;
            _grupPencilan.Children.Add(BeriLabel("Penanganan pencilan", _caraPencilan));
            panel.Children.Add(_grupPencilan);

            
            var tombolPratinjau = new Button
            {
                Content = "Hitung pratinjau",
                Padding = new Thickness(16, 7, 16, 7),
                Margin = new Thickness(0, 10, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            tombolPratinjau.Click += (_, _) => HitungPratinjau();
            panel.Children.Add(tombolPratinjau);

            _pratinjau.Text = "Tekan 'Hitung pratinjau' untuk melihat apa yang akan berubah.";
            _pratinjau.TextWrapping = TextWrapping.Wrap;
            _pratinjau.Foreground = (Brush)Application.Current.Resources["Teks"];
            _pratinjau.Background = (Brush)Application.Current.Resources["Panel2"];
            _pratinjau.Padding = new Thickness(10);
            _pratinjau.MinHeight = 70;
            panel.Children.Add(_pratinjau);

            
            var baris = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
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
                if (!AdaKolomDipilih() && MembutuhkanKolom())
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

        

        private bool MembutuhkanKolom() => _operasi.SelectedIndex switch
        {
            0 => true,      
            1 => true,      
            2 => false,     
            3 => true,      
            4 => true,      
            _ => true
        };

        private bool AdaKolomDipilih() => _kolom.SelectedItems.Count > 0;

        private List<string> KolomDipilih()
            => _kolom.SelectedItems.Cast<string>().ToList();

        private void PerbaruiTampilan()
        {
            int op = _operasi.SelectedIndex;

            _grupKolom.Visibility = MembutuhkanKolom() ? Visibility.Visible : Visibility.Collapsed;
            _grupIsi.Visibility = op == 0 ? Visibility.Visible : Visibility.Collapsed;
            _grupPencilan.Visibility = op is 3 or 4 ? Visibility.Visible : Visibility.Collapsed;

            _pratinjau.Text = "Tekan 'Hitung pratinjau' untuk melihat apa yang akan berubah.";
        }

        private void HitungPratinjau()
        {
            try
            {
                var uji = _data.Clone();
                var hasil = Jalankan(uji);

                _pratinjau.Text = "Yang akan terjadi: " + hasil.Ringkasan();
                if (hasil.Rincian.Count > 0)
                    _pratinjau.Text += "\n\n" + string.Join("\n", hasil.Rincian);
            }
            catch (Exception galat)
            {
                _pratinjau.Text = "Gagal menghitung pratinjau: " + galat.Message;
            }
        }

        public HasilPembersihan Jalankan(Dataset ds)
        {
            var kolom = KolomDipilih();
            if (kolom.Count == 0) kolom = ds.Names;      

            return _operasi.SelectedIndex switch
            {
                0 => PembersihData.IsiKosong(ds, kolom, CaraIsiTerpilih(), NilaiTetap()),
                1 => PembersihData.BuangBarisKosong(ds, kolom, false),
                2 => PembersihData.BuangDuplikat(ds),
                3 => PembersihData.TanganiPencilan(ds, kolom, CaraPencilan.BuangBaris),
                4 => PembersihData.TanganiPencilan(ds, kolom, CaraPencilan.BatasiNilai),
                _ => new HasilPembersihan()
            };
        }

        private CaraIsiKosong CaraIsiTerpilih() => _caraIsi.SelectedIndex switch
        {
            0 => CaraIsiKosong.Rerata,
            1 => CaraIsiKosong.Median,
            2 => CaraIsiKosong.Modus,
            _ => CaraIsiKosong.NilaiTetap
        };

        private double NilaiTetap()
            => double.TryParse(_nilaiTetap.Text.Replace(",", "."), NumberStyles.Float,
                               CultureInfo.InvariantCulture, out double v) ? v : 0.0;

        

        private static StackPanel BeriLabel(string teks, Control kontrol)
        {
            var bungkus = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            bungkus.Children.Add(new TextBlock
            {
                Text = teks,
                FontSize = 11.5,
                Foreground = (Brush)Application.Current.Resources["TeksRedup"],
                Margin = new Thickness(0, 0, 0, 3)
            });
            bungkus.Children.Add(kontrol);
            return bungkus;
        }
    }
}
