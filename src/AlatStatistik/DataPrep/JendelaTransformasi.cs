using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using AlatStatistik.Models;
using Ukur = AlatStatistik.Models.Measure;

namespace AlatStatistik.DataPrep
{

    public class JendelaTransformasi : Window
    {
        private readonly Dataset _data;

        private readonly ComboBox _operasi = new();

        
        private readonly StackPanel _grupPeringkat = new();
        private ListBox _kolomPeringkat = new();
        private readonly ComboBox _ikatan = new();
        private readonly ComboBox _jenis = new();
        private readonly CheckBox _menurun = new() { Content = "Nilai terbesar dapat peringkat 1" };

        
        private readonly StackPanel _grupUbah = new();
        private readonly ComboBox _kolomUbah = new();
        private readonly TextBox _dari = new();
        private readonly TextBox _sampai = new();
        private readonly TextBox _jadi = new();
        private readonly CheckBox _lainnya = new() { Content = "Berlaku untuk nilai lain (All other values)" };
        private readonly ListBox _daftarAturan = new();
        private readonly List<AturanUbah> _aturan = new();

        
        private readonly StackPanel _grupIsi = new();
        private readonly ComboBox _kolomIsi = new();
        private readonly ComboBox _caraIsi = new();
        private readonly TextBox _tetap = new();

        private readonly TextBlock _pratinjau = Bantu.KotakPratinjau();

        public JendelaTransformasi(Dataset data, int operasi = 0)
        {
            _data = data;

            Title = "Transformasi nilai";
            Width = 560;
            Height = 700;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            Background = Bantu.Warna("Latar");

            var panel = new StackPanel { Margin = new Thickness(18) };

            _operasi.SelectionChanged += (_, _) => PerbaruiTampilan();
            _operasi.Items.Add("Beri peringkat (Rank Cases)");
            _operasi.Items.Add("Ubah nilai (Recode)");
            _operasi.Items.Add("Isi nilai hilang");
            _operasi.SelectedIndex = 0;
            panel.Children.Add(Bantu.BeriLabel("Operasi", _operasi));

            
            _kolomPeringkat = Bantu.DaftarKolom(data, 140);
            _grupPeringkat.Children.Add(Bantu.BeriLabel("Variabel yang diberi peringkat", _kolomPeringkat));

            foreach (string c in new[] { "Rerata (bawaan & SciPy)", "Terkecil", "Terbesar", "Nomor urut unik" })
                _ikatan.Items.Add(c);
            _ikatan.SelectedIndex = 0;
            _grupPeringkat.Children.Add(Bantu.BeriLabel("Nilai yang sama (ikatan)", _ikatan));

            foreach (string j in new[] { "Peringkat", "Peringkat fraksional", "Persentil peringkat",
                                         "Skor normal Blom", "Ntiles" })
                _jenis.Items.Add(j);
            _jenis.SelectedIndex = 0;
            _grupPeringkat.Children.Add(Bantu.BeriLabel("Jenis skor", _jenis));
            _grupPeringkat.Children.Add(_menurun);
            panel.Children.Add(_grupPeringkat);

            
            foreach (string n in data.Names) { _kolomUbah.Items.Add(n); _kolomIsi.Items.Add(n); }
            if (_kolomUbah.Items.Count > 0) { _kolomUbah.SelectedIndex = 0; _kolomIsi.SelectedIndex = 0; }
            _grupUbah.Children.Add(Bantu.BeriLabel("Variabel sumber", _kolomUbah));

            var barisAturan = new StackPanel { Orientation = Orientation.Horizontal };
            _dari.Width = 80; _sampai.Width = 80; _jadi.Width = 80;
            _sampai.Margin = new Thickness(8, 0, 0, 0);
            _jadi.Margin = new Thickness(8, 0, 0, 0);
            barisAturan.Children.Add(_dari);
            barisAturan.Children.Add(_sampai);
            barisAturan.Children.Add(_jadi);
            _grupUbah.Children.Add(Bantu.BeriLabel("Dari (kosong = terendah) · Sampai (kosong = tertinggi) · Menjadi (kosong = hilang)", barisAturan));
            _grupUbah.Children.Add(_lainnya);

            var tombolTambah = new Button { Content = "Tambah aturan", Padding = new Thickness(10, 4, 10, 4) };
            tombolTambah.Click += (_, _) => TambahAturan();
            var tombolHapus = new Button { Content = "Hapus aturan", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(8, 0, 0, 0) };
            tombolHapus.Click += (_, _) => HapusAturan();
            var barisTombol = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            barisTombol.Children.Add(tombolTambah);
            barisTombol.Children.Add(tombolHapus);
            _grupUbah.Children.Add(barisTombol);

            _daftarAturan.Height = 110;
            _daftarAturan.Background = Bantu.Warna("Panel2");
            _daftarAturan.Foreground = Bantu.Warna("Teks");
            _daftarAturan.BorderBrush = Bantu.Warna("Garis");
            _grupUbah.Children.Add(Bantu.BeriLabel("Daftar aturan (diperiksa dari atas)", _daftarAturan));
            panel.Children.Add(_grupUbah);

            
            _grupIsi.Children.Add(Bantu.BeriLabel("Variabel yang diisi", _kolomIsi));
            foreach (string c in new[] { "Rerata seluruh seri", "Median seluruh seri",
                                         "Interpolasi linier", "Nilai tetangga sebelumnya",
                                         "Nilai tetangga berikutnya", "Nilai tetap" })
                _caraIsi.Items.Add(c);
            _caraIsi.SelectedIndex = 0;
            _caraIsi.SelectionChanged += (_, _) => _tetap.IsEnabled = _caraIsi.SelectedIndex == 5;
            _grupIsi.Children.Add(Bantu.BeriLabel("Cara mengisi", _caraIsi));
            _tetap.IsEnabled = false;
            _grupIsi.Children.Add(Bantu.BeriLabel("Nilai tetap (bila cara = nilai tetap)", _tetap));
            panel.Children.Add(_grupIsi);

            
            var tombolPratinjau = new Button { Content = "Hitung pratinjau", Padding = new Thickness(10, 4, 10, 4) };
            tombolPratinjau.Click += (_, _) => HitungPratinjau();
            panel.Children.Add(tombolPratinjau);
            panel.Children.Add(_pratinjau);

            
            var batal = new Button { Content = "Batal", Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 0) };
            batal.Click += (_, _) => DialogResult = false;

            var terapkan = new Button { Content = "Terapkan", Padding = new Thickness(14, 6, 14, 6) };
            terapkan.Click += (_, _) =>
            {
                string? keluhan = PeriksaLengkap();
                if (keluhan is not null)
                {
                    MessageBox.Show(keluhan, "Belum lengkap", MessageBoxButton.OK, MessageBoxImage.Information);
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
            _grupPeringkat.Visibility = op == 0 ? Visibility.Visible : Visibility.Collapsed;
            _grupUbah.Visibility = op == 1 ? Visibility.Visible : Visibility.Collapsed;
            _grupIsi.Visibility = op == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        private string? PeriksaLengkap()
        {
            if (_operasi.SelectedIndex == 0 && _kolomPeringkat.SelectedItems.Count == 0)
                return "Pilih sedikitnya satu variabel yang diberi peringkat.";
            if (_operasi.SelectedIndex == 1 && _aturan.Count == 0)
                return "Tambahkan sedikitnya satu aturan ubah nilai.";
            return null;
        }

        private void TambahAturan()
        {
            var a = new AturanUbah
            {
                Dari = string.IsNullOrWhiteSpace(_dari.Text) ? null : Angka(_dari.Text),
                Sampai = string.IsNullOrWhiteSpace(_sampai.Text) ? null : Angka(_sampai.Text),
                Jadi = string.IsNullOrWhiteSpace(_jadi.Text) ? null : Angka(_jadi.Text),
                UntukLainnya = _lainnya.IsChecked == true
            };

            if (!a.UntukLainnya && a.Dari is null && a.Sampai is null)
            {
                MessageBox.Show("Isi batas bawah atau batas atas lebih dulu, atau centang "
                                + "“berlaku untuk nilai lain”.", "Belum lengkap",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _aturan.Add(a);
            _daftarAturan.Items.Add(a.ToString());
        }

        private void HapusAturan()
        {
            int i = _daftarAturan.SelectedIndex;
            if (i < 0 || i >= _aturan.Count) return;
            _aturan.RemoveAt(i);
            _daftarAturan.Items.RemoveAt(i);
        }

        private static double? Angka(string teks)
            => double.TryParse(teks.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
               || double.TryParse(teks.Trim().Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                ? v : (double?)null;

        private CaraIkatan CaraIkatanYangDipilih() => _ikatan.SelectedIndex switch
        {
            1 => DataPrep.CaraIkatan.Terkecil,
            2 => DataPrep.CaraIkatan.Terbesar,
            3 => DataPrep.CaraIkatan.UrutUnik,
            _ => DataPrep.CaraIkatan.Rerata
        };

        private JenisPeringkat JenisYangDipilih() => _jenis.SelectedIndex switch
        {
            1 => JenisPeringkat.PeringkatFraksional,
            2 => JenisPeringkat.PeringkatPersen,
            3 => JenisPeringkat.SkorNormalBlom,
            4 => JenisPeringkat.Ntiles,
            _ => JenisPeringkat.Peringkat
        };

        private CaraIsi CaraIsiYangDipilih() => _caraIsi.SelectedIndex switch
        {
            1 => CaraIsi.MedianSeri,
            2 => CaraIsi.InterpolasiLinier,
            3 => CaraIsi.TetanggaSebelumnya,
            4 => CaraIsi.TetanggaBerikutnya,
            5 => CaraIsi.NilaiTetap,
            _ => CaraIsi.RerataSeri
        };

        private void HitungPratinjau()
        {
            var uji = _data.Clone();
            try
            {
                _pratinjau.Text = _operasi.SelectedIndex switch
                {
                    0 => Ringkas(PeringkatSemua(uji)) + Contoh(uji),
                    1 => Ringkas(UbahSemua(uji)),
                    _ => Ringkas(IsiSemua(uji))
                };
            }
            catch (Exception galat) { _pratinjau.Text = "Gagal menghitung pratinjau: " + galat.Message; }
        }

        private string Contoh(Dataset uji)
        {
            var sb = new StringBuilder("\n\nTiga baris pertama:\n");
            int batas = Math.Min(3, uji.RowCount);
            for (int i = 0; i < batas; i++)
                sb.AppendLine("   " + string.Join(" | ", uji.Rows[i].Select(v => v ?? "(kosong)")));
            return sb.ToString();
        }

        public List<ResultBlock> Jalankan(Dataset ds)
        {
            return _operasi.SelectedIndex switch
            {
                0 => PeringkatSemua(ds),
                1 => UbahSemua(ds),
                _ => IsiSemua(ds)
            };
        }

        private List<ResultBlock> PeringkatSemua(Dataset ds)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Beri peringkat (Rank Cases)") };
            var kolom = Bantu.KolomDipilih(_kolomPeringkat);
            var cara = CaraIkatanYangDipilih();
            var jenis = JenisYangDipilih();

            foreach (string k in kolom)
            {
                int kk = ds.IndexOf(k);
                if (kk < 0) continue;

                var nilai = new List<double?>();
                for (int r = 0; r < ds.RowCount; r++)
                    nilai.Add(Dataset.ToDouble(kk < ds.Rows[r].Length ? ds.Rows[r][kk] : null));

                var p = Peringkat.Beri(nilai, cara, _menurun.IsChecked == true, jenis);
                var baru = p.Select(v => v.HasValue ? v.Value.ToString("G10", CultureInfo.InvariantCulture) : null).ToList();
                int i = Bantu.TambahKolom(ds, "peringkat_" + k, baru, Ukur.Skala, 3);

                blok.Add(Blocks.Note($"Kolom **{ds.Names[i]}** dibuat dari **{k}**; "
                                     + $"ikatan = {_ikatan.SelectedItem}, jenis = {_jenis.SelectedItem}"
                                     + (_menurun.IsChecked == true ? ", besar = peringkat 1" : "")
                                     + $". Nilai hilang dilewati ({p.Count(v => v is null)} baris)."));
            }
            return blok;
        }

        private List<ResultBlock> UbahSemua(Dataset ds)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Ubah nilai (Recode)") };
            string k = _kolomUbah.SelectedItem as string ?? "";
            var h = UbahNilai.Ubah(ds, k, _aturan);
            if (!h.Sukses) { blok.Add(Blocks.Note(h.Galat ?? "Gagal.", NoteKind.Error)); return blok; }

            var baru = h.Nilai.Select(v => v.HasValue ? v.Value.ToString("G10", CultureInfo.InvariantCulture) : null).ToList();
            int i = Bantu.TambahKolom(ds, "ubah_" + k, baru);

            blok.Add(Blocks.Note($"Kolom **{ds.Names[i]}** dibuat dari **{k}**."));
            blok.Add(Blocks.Note($"{h.Berubah} baris diubah, {h.Tetap} tetap, "
                                 + $"{h.JadiKosong} dijadikan kosong, {h.HilangDilalui} sudah kosong sebelumnya."));
            blok.Add(Blocks.Table("Aturan yang dipakai", new[] { "Aturan" },
                                  _aturan.Select(a => new List<string> { a.ToString() }).ToList(),
                                  "Diperiksa dari atas ke bawah; aturan pertama yang cocok yang dipakai."));
            return blok;
        }

        private List<ResultBlock> IsiSemua(Dataset ds)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Isi nilai hilang") };
            string k = _kolomIsi.SelectedItem as string ?? "";
            var cara = CaraIsiYangDipilih();
            var h = UbahNilai.Isi(ds, k, cara, Angka(_tetap.Text) ?? 0);
            if (!h.Sukses) { blok.Add(Blocks.Note(h.Galat ?? "Gagal.", NoteKind.Error)); return blok; }

            var baru = h.Nilai.Select(v => v.HasValue ? v.Value.ToString("G10", CultureInfo.InvariantCulture) : null).ToList();
            int i = Bantu.TambahKolom(ds, "isi_" + k, baru);

            blok.Add(Blocks.Note($"Kolom **{ds.Names[i]}** dibuat dari **{k}**; cara = {_caraIsi.SelectedItem}."));
            blok.Add(Blocks.Note($"{h.Berubah} nilai kosong diisi, {h.Tetap} baris sudah terisi."));
            return blok;
        }

        private static string Ringkas(List<ResultBlock> blok)
        {
            var sb = new StringBuilder();
            foreach (var b in blok)
            {
                if (b.Kind == BlockKind.Note) sb.AppendLine("· " + b.Text);
            }
            return sb.Length > 0 ? sb.ToString().TrimEnd() : "Tidak ada keterangan.";
        }
    }
}
