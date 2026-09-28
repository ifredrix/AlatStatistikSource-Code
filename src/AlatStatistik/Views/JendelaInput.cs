using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AlatStatistik.Views
{

    public class JendelaInput : Window
    {
        private readonly TextBox _kotak1 = new();
        private readonly TextBox _kotak2 = new();
        private bool _diterima;

        private JendelaInput()
        {
            Title = "AlatStatistik";
            Width = 420;
            Height = 210;
            MinWidth = 380;
            MinHeight = 190;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["Latar"];
        }

        public static int? Angka(string judul, string pertanyaan, int nilaiAwal, int min = 1, int maks = 1_000_000)
        {
            var j = new JendelaInput { Title = judul };
            j.Bangun(pertanyaan, nilaiAwal.ToString(CultureInfo.InvariantCulture), null);
            return j.Tampil() ? j.BacaAngka(j._kotak1, min, maks) : null;
        }

        public static string? Teks(string judul, string pertanyaan, string nilaiAwal = "")
        {
            var j = new JendelaInput { Title = judul };
            j.Bangun(pertanyaan, nilaiAwal, null);
            return j.Tampil() ? j._kotak1.Text.Trim() : null;
        }

        public static int? Pilih(string judul, string pertanyaan,
                                 System.Collections.Generic.IReadOnlyList<string> opsi,
                                 int indeksAwal = 0)
        {
            if (opsi == null || opsi.Count == 0) return null;

            var j = new JendelaInput { Title = judul, Height = 250, MinHeight = 230 };
            var combo = new ComboBox { Width = 260, Margin = new Thickness(0, 4, 0, 0) };
            foreach (var o in opsi) combo.Items.Add(o);
            combo.SelectedIndex = Math.Clamp(indeksAwal, 0, opsi.Count - 1);

            
            
            j.Bangun(pertanyaan, "", combo, sembunyiTeks: true);
            return j.Tampil() ? combo.SelectedIndex : null;
        }

        public static (string Nama, Models.Measure Ukur)? VariabelBaru(string judul, string namaAwal)
        {
            var j = new JendelaInput { Title = judul, Height = 260, MinHeight = 240 };
            var combo = new ComboBox { Width = 200, Margin = new Thickness(0, 4, 0, 0) };
            combo.ItemsSource = Enum.GetValues(typeof(Models.Measure));
            combo.SelectedIndex = 0;

            j.Bangun("Nama variabel baru:", namaAwal, combo);
            if (!j.Tampil()) return null;

            string nama = j._kotak1.Text.Trim();
            var ukur = combo.SelectedItem is Models.Measure m ? m : Models.Measure.Skala;
            return (nama, ukur);
        }

        

        private void Bangun(string pertanyaan, string nilaiAwal, ComboBox? tambahan,
                            bool sembunyiTeks = false)
        {
            var akar = new StackPanel { Margin = new Thickness(18) };

            var tanya = new TextBlock
            {
                Text = pertanyaan,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5,
                Foreground = (Brush)Application.Current.Resources["Teks"],
                Margin = new Thickness(0, 0, 0, 10)
            };
            akar.Children.Add(tanya);

            _kotak1.Text = nilaiAwal;
            _kotak1.FontSize = 13;
            _kotak1.Padding = new Thickness(6, 4, 6, 4);
            if (!sembunyiTeks) akar.Children.Add(_kotak1);

            if (tambahan != null)
            {
                var label = new TextBlock
                {
                    Text = "Tingkat ukur:",
                    FontSize = 12,
                    Margin = new Thickness(0, 12, 0, 0),
                    Foreground = (Brush)Application.Current.Resources["TeksRedup"]
                };
                akar.Children.Add(label);
                akar.Children.Add(tambahan);
            }

            var baris = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 18, 0, 0)
            };

            var batal = new Button
            {
                Content = "Batal", Width = 90, Height = 30, IsCancel = true,
                Margin = new Thickness(0, 0, 10, 0)
            };
            var ok = new Button { Content = "OK", Width = 90, Height = 30, IsDefault = true };
            ok.Click += (_, _) => { _diterima = true; DialogResult = true; Close(); };
            batal.Click += (_, _) => { _diterima = false; DialogResult = false; Close(); };

            baris.Children.Add(batal);
            baris.Children.Add(ok);
            akar.Children.Add(baris);

            Content = akar;
            Loaded += (_, _) =>
            {
                if (!sembunyiTeks) { _kotak1.Focus(); _kotak1.SelectAll(); }
            };
        }

        private bool Tampil() => ShowDialog() == true && _diterima;

        private int? BacaAngka(TextBox kotak, int min, int maks)
        {
            string s = kotak.Text.Trim().Replace(",", "").Replace(".", "");
            if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            {
                MessageBox.Show($"Masukkan angka bulat antara {min} dan {maks}.",
                                "Nilai tidak valid", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            return Math.Clamp(v, min, maks);
        }
    }
}
