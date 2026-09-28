using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AlatStatistik.Models;

namespace AlatStatistik.DataPrep
{

    public class JendelaHitung : Window
    {
        private readonly Dataset _data;

        private readonly TextBox _nama = new();
        private readonly TextBox _ekspresi = new();
        private readonly ListBox _daftarKolom = new();
        private readonly ListBox _daftarFungsi = new();
        private readonly TextBlock _keterangan = new()
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Foreground = Bantu.Warna("TeksRedup"),
            Margin = new Thickness(0, 2, 0, 8)
        };
        private readonly TextBlock _pratinjau = Bantu.KotakPratinjau();

        public JendelaHitung(Dataset data)
        {
            _data = data;

            Title = "Hitung variabel";
            Width = 620;
            Height = 720;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            Background = Bantu.Warna("Latar");

            var panel = new StackPanel { Margin = new Thickness(18) };

            
            _nama.Text = Bantu.NamaUnik(data, "hasil");
            panel.Children.Add(Bantu.BeriLabel("Variabel tujuan", _nama));

            
            _ekspresi.Height = 58;
            _ekspresi.AcceptsReturn = true;
            _ekspresi.TextWrapping = TextWrapping.Wrap;
            _ekspresi.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _ekspresi.Text = "";
            _ekspresi.TextChanged += (_, _) => PerbaruiKeterangan();
            panel.Children.Add(Bantu.BeriLabel("Ekspresi", _ekspresi));

            var contoh = new TextBlock
            {
                Text = "Contoh: (skor - MEAN(skor)) / SD(skor)      |      "
                     + "LN(berat) / LN(tinggi)      |      skor > 70 & usia >= 30",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = Bantu.Warna("TeksRedup"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(contoh);

            
            var dua = new Grid();
            dua.ColumnDefinitions.Add(new ColumnDefinition());
            dua.ColumnDefinitions.Add(new ColumnDefinition());

            _daftarKolom = Bantu.DaftarKolom(data, 150);
            _daftarKolom.MouseDoubleClick += (_, _) => Sisip(_daftarKolom);
            Grid.SetColumn(_daftarKolom, 0);
            dua.Children.Add(_daftarKolom);

            foreach (string f in Ekspresi.Fungsi.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                _daftarFungsi.Items.Add(f);
            _daftarFungsi.Height = 150;
            _daftarFungsi.Background = Bantu.Warna("Panel2");
            _daftarFungsi.Foreground = Bantu.Warna("Teks");
            _daftarFungsi.BorderBrush = Bantu.Warna("Garis");
            _daftarFungsi.Margin = new Thickness(10, 0, 0, 0);
            _daftarFungsi.MouseDoubleClick += (_, _) => SisipFungsi();
            Grid.SetColumn(_daftarFungsi, 1);
            dua.Children.Add(_daftarFungsi);

            panel.Children.Add(new TextBlock
            {
                Text = "Klik dua kali untuk menyisipkan (kiri: variabel, kanan: fungsi)",
                FontSize = 11,
                Foreground = Bantu.Warna("TeksRedup"),
                Margin = new Thickness(0, 0, 0, 4)
            });
            panel.Children.Add(dua);

            var tombolSisip = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            foreach (string op in new[] { "+", "-", "*", "/", "^", "(", ")", ",", "&", "|", "~", "=", "<>", "<", ">" })
            {
                var b = new Button
                {
                    Content = op,
                    Padding = new Thickness(7, 3, 7, 3),
                    Margin = new Thickness(0, 0, 4, 0),
                    MinWidth = 26
                };
                string salin = op;
                b.Click += (_, _) =>
                {
                    int p = _ekspresi.CaretIndex;
                    _ekspresi.Text = _ekspresi.Text.Insert(p, salin);
                    _ekspresi.CaretIndex = p + salin.Length;
                    _ekspresi.Focus();
                };
                tombolSisip.Children.Add(b);
            }
            panel.Children.Add(tombolSisip);
            panel.Children.Add(_keterangan);

            
            var tombolPratinjau = new Button { Content = "Hitung pratinjau", Padding = new Thickness(10, 4, 10, 4) };
            tombolPratinjau.Click += (_, _) => HitungPratinjau();
            panel.Children.Add(tombolPratinjau);
            panel.Children.Add(_pratinjau);

            
            var batal = new Button { Content = "Batal", Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 0) };
            batal.Click += (_, _) => DialogResult = false;

            var terapkan = new Button { Content = "Terapkan", Padding = new Thickness(14, 6, 14, 6) };
            terapkan.Click += (_, _) =>
            {
                if (Bantu.BersihNama(_nama.Text).Length == 0)
                {
                    MessageBox.Show("Isi nama variabel tujuan lebih dulu.", "Belum lengkap",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var uji = Ekspresi.Hitung(_ekspresi.Text ?? "", _data);
                if (!uji.Sukses)
                {
                    MessageBox.Show(uji.Galat, "Ekspresi tidak bisa dipakai",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
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

            PerbaruiKeterangan();
        }

        private void Sisip(ListBox daftar)
        {
            if (daftar.SelectedItem is not string teks) return;
            int p = _ekspresi.CaretIndex;
            _ekspresi.Text = _ekspresi.Text.Insert(p, teks);
            _ekspresi.CaretIndex = p + teks.Length;
            _ekspresi.Focus();
        }

        private void SisipFungsi()
        {
            if (_daftarFungsi.SelectedItem is not string f) return;
            string teks = f == "POWER" || f == "MOD" ? f + "(, )" : f + "()";
            int p = _ekspresi.CaretIndex;
            _ekspresi.Text = _ekspresi.Text.Insert(p, teks);
            
            _ekspresi.CaretIndex = p + f.Length + 1;
            _ekspresi.Focus();
        }

        private void PerbaruiKeterangan()
        {
            string teks = _ekspresi.Text ?? "";
            var pohon = Ekspresi.Terjemah(teks, _data.Names, out string? galat);
            if (pohon is null)
            {
                bool kosong = teks.Trim().Length == 0;
                _keterangan.Text = kosong ? "" : galat ?? "";
                _keterangan.Foreground = kosong ? Bantu.Warna("TeksRedup") : Bantu.Warna("Bahaya");
            }
            else
            {
                _keterangan.Text = "Ekspresi bisa dipakai.";
                _keterangan.Foreground = Bantu.Warna("Sukses");
            }
        }

        private void HitungPratinjau()
        {
            var h = Ekspresi.Hitung(_ekspresi.Text ?? "", _data);
            if (!h.Sukses)
            {
                _pratinjau.Text = "Tidak bisa dihitung: " + h.Galat;
                return;
            }

            var ci = CultureInfo.InvariantCulture;
            int hilang = h.Nilai.Count(v => v is null);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Baris dihitung: {h.Nilai.Count}, nilai hilang (kosong): {hilang}");
            if (h.KolomDipakai.Count > 0)
                sb.AppendLine("Variabel yang dipakai: " + string.Join(", ", h.KolomDipakai));
            sb.AppendLine();

            int batas = Math.Min(6, h.Nilai.Count);
            sb.AppendLine("Baris   Nilai");
            for (int i = 0; i < batas; i++)
            {
                string v = h.Nilai[i] is { } x ? x.ToString("G10", ci) : "(hilang)";
                sb.AppendLine($"   {i + 1}    {v}");
            }
            if (h.Nilai.Count > batas) sb.AppendLine($"   ... {h.Nilai.Count - batas} baris lagi");

            var ada = h.Nilai.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (ada.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Rerata {ada.Average().ToString("G8", ci)}, "
                              + $"terkecil {ada.Min().ToString("G8", ci)}, terbesar {ada.Max().ToString("G8", ci)}");
            }

            _pratinjau.Text = sb.ToString().TrimEnd();
        }

        public string NamaTujuan => Bantu.BersihNama(_nama.Text);

        public string TeksEkspresi => _ekspresi.Text ?? "";
    }
}
