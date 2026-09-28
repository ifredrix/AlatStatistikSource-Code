using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AlatStatistik.Models;

namespace AlatStatistik.DataPrep
{

    public class JendelaBaris : Window
    {
        private readonly Dataset _data;

        private readonly ComboBox _operasi = new();

        private readonly StackPanel _grupUrut = new();
        private readonly ListBox _kunci = new();
        private readonly ComboBox _arah = new();

        private readonly StackPanel _grupPilih = new();
        private readonly ComboBox _kolomSyarat = new();
        private readonly ComboBox _operator = new();
        private readonly TextBox _nilai = new();
        private readonly ListBox _daftarSyarat = new();
        private readonly CheckBox _semuaHarusBenar = new() { Content = "Semua syarat harus terpenuhi (AND)", IsChecked = true };

        private readonly List<Syarat> _syarat = new();

        private readonly TextBlock _pratinjau = Bantu.KotakPratinjau();

        public JendelaBaris(Dataset data, int operasi = 0)
        {
            _data = data;

            Title = "Pekerjaan baris";
            Width = 520;
            Height = 640;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            Background = Bantu.Warna("Latar");

            var panel = new StackPanel { Margin = new Thickness(18) };

            
            _operasi.SelectionChanged += (_, _) => PerbaruiTampilan();
            _operasi.Items.Add("Urutkan baris");
            _operasi.Items.Add("Pilih kasus (saring baris)");
            _operasi.SelectedIndex = 0;
            panel.Children.Add(Bantu.BeriLabel("Operasi", _operasi));

            
            _kunci = Bantu.DaftarKolom(data, 130);
            _grupUrut.Children.Add(Bantu.BeriLabel(
                "Kunci pengurutan (pilih lebih dari satu untuk berurutan; urutan pilihan = urutan kunci)",
                _kunci));

            _arah.Items.Add("Menaik (A ke Z, kecil ke besar)");
            _arah.Items.Add("Menurun (Z ke A, besar ke kecil)");
            _arah.SelectedIndex = 0;
            _grupUrut.Children.Add(Bantu.BeriLabel("Arah", _arah));
            panel.Children.Add(_grupUrut);

            
            foreach (string nama in data.Names) _kolomSyarat.Items.Add(nama);
            if (_kolomSyarat.Items.Count > 0) _kolomSyarat.SelectedIndex = 0;
            _grupPilih.Children.Add(Bantu.BeriLabel("Variabel yang disyaratkan", _kolomSyarat));

            foreach (string op in OlahBaris.Operator) _operator.Items.Add(op);
            _operator.SelectedIndex = 0;

            var barisSyarat = new StackPanel { Orientation = Orientation.Horizontal };
            barisSyarat.Children.Add(new TextBlock
            {
                Text = "Operator",
                FontSize = 11.5,
                Foreground = Bantu.Warna("TeksRedup"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            _operator.Width = 70;
            barisSyarat.Children.Add(_operator);
            _nilai.Width = 150;
            _nilai.Margin = new Thickness(10, 0, 0, 0);
            barisSyarat.Children.Add(_nilai);
            _grupPilih.Children.Add(Bantu.BeriLabel("Syarat", barisSyarat));

            var tombolTambah = new Button { Content = "Tambah syarat", Padding = new Thickness(10, 4, 10, 4) };
            tombolTambah.Click += (_, _) => TambahSyarat();
            var tombolHapus = new Button { Content = "Hapus syarat", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(8, 0, 0, 0) };
            tombolHapus.Click += (_, _) => HapusSyarat();

            var barisTombol = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            barisTombol.Children.Add(tombolTambah);
            barisTombol.Children.Add(tombolHapus);
            _grupPilih.Children.Add(barisTombol);

            _daftarSyarat.Height = 110;
            _daftarSyarat.Background = Bantu.Warna("Panel2");
            _daftarSyarat.Foreground = Bantu.Warna("Teks");
            _daftarSyarat.BorderBrush = Bantu.Warna("Garis");
            _grupPilih.Children.Add(Bantu.BeriLabel("Daftar syarat", _daftarSyarat));
            _grupPilih.Children.Add(_semuaHarusBenar);
            panel.Children.Add(_grupPilih);

            
            var tombolPratinjau = new Button { Content = "Hitung pratinjau", Padding = new Thickness(10, 4, 10, 4) };
            tombolPratinjau.Click += (_, _) => HitungPratinjau();
            panel.Children.Add(tombolPratinjau);
            panel.Children.Add(_pratinjau);

            
            var batal = new Button { Content = "Batal", Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 0) };
            batal.Click += (_, _) => DialogResult = false;

            var terapkan = new Button { Content = "Terapkan", Padding = new Thickness(14, 6, 14, 6) };
            terapkan.Click += (_, _) =>
            {
                if (_operasi.SelectedIndex == 0)
                {
                    if (_kunci.SelectedItems.Count == 0)
                    {
                        MessageBox.Show("Pilih sedikitnya satu variabel sebagai kunci pengurutan.",
                                        "Belum lengkap", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
                else if (_syarat.Count == 0)
                {
                    MessageBox.Show("Tambahkan sedikitnya satu syarat.",
                                    "Belum lengkap", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                DialogResult = true;
            };

            var baris = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            baris.Children.Add(batal);
            baris.Children.Add(terapkan);
            panel.Children.Add(baris);

            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = panel
            };

            if (operasi >= 0 && operasi < _operasi.Items.Count) _operasi.SelectedIndex = operasi;
            PerbaruiTampilan();
        }

        private void PerbaruiTampilan()
        {
            int op = _operasi.SelectedIndex;
            _grupUrut.Visibility = op == 0 ? Visibility.Visible : Visibility.Collapsed;
            _grupPilih.Visibility = op == 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TambahSyarat()
        {
            if (_kolomSyarat.SelectedItem is not string kolom) return;

            string nilai = (_nilai.Text ?? "").Trim();
            if (nilai.Length == 0)
            {
                MessageBox.Show("Isi nilai yang disyaratkan lebih dulu.", "Belum lengkap",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var s = new Syarat
            {
                Kolom = kolom,
                Operator = _operator.SelectedItem as string ?? "==",
                Nilai = nilai
            };
            _syarat.Add(s);
            _daftarSyarat.Items.Add(s.ToString());
        }

        private void HapusSyarat()
        {
            int i = _daftarSyarat.SelectedIndex;
            if (i < 0 || i >= _syarat.Count) return;
            _syarat.RemoveAt(i);
            _daftarSyarat.Items.RemoveAt(i);
        }

        private void HitungPratinjau()
        {
            var uji = _data.Clone();
            try
            {
                _pratinjau.Text = _operasi.SelectedIndex == 0
                    ? OlahBaris.Urutkan(uji, Kunci()).Ringkasan() + Contoh(uji)
                    : OlahBaris.PilihKasus(uji, _syarat, _semuaHarusBenar.IsChecked == true).Ringkasan() + Contoh(uji);
            }
            catch (Exception galat)
            {
                _pratinjau.Text = "Gagal menghitung pratinjau: " + galat.Message;
            }
        }

        private string Contoh(Dataset uji)
        {
            if (uji.RowCount == 0) return "\n\nTidak ada baris tersisa.";

            int kolom = uji.IndexOf(_operasi.SelectedIndex == 0
                ? (Kunci().FirstOrDefault()?.Kolom ?? uji.Names[0])
                : (_syarat.Count > 0 ? _syarat[0].Kolom : uji.Names[0]));
            if (kolom < 0) kolom = 0;

            var contoh = uji.Rows.Take(3).Select(r => kolom < r.Length ? (r[kolom] ?? "(kosong)") : "(kosong)");
            return "\n\nTiga baris pertama, kolom " + uji.Names[kolom] + ": " + string.Join(" | ", contoh);
        }

        public List<ResultBlock> Jalankan(Dataset ds)
            => _operasi.SelectedIndex == 0
                ? OlahBaris.Urutkan(ds, Kunci()).KeBlok()
                : OlahBaris.PilihKasus(ds, _syarat, _semuaHarusBenar.IsChecked == true).KeBlok();

        private List<KunciUrut> Kunci()
        {
            bool menurun = _arah.SelectedIndex == 1;
            return _kunci.SelectedItems.Cast<string>()
                   .Select(n => new KunciUrut { Kolom = n, Menurun = menurun })
                   .ToList();
        }
    }
}
