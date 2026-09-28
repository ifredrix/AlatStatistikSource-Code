using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using AlatStatistik.Analysis;
using AlatStatistik.Models;

namespace AlatStatistik.Views
{

    public class FormulirAnalisis
    {
        
        
        
        private sealed class Baris
        {
            public FieldSpec Field = null!;
            public ListBox Target = null!;
            public TextBlock Hitung = null!;
            public GroupBox Grup = null!;
        }

        private readonly List<Baris> _baris = new();
        private readonly Dictionary<string, Control> _opsi = new();
        private readonly ListBox _sumber = new();
        private readonly TextBlock _status = new();
        private readonly Func<FieldKind, List<string>> _daftarVariabel;

        
        
        
        private readonly Dictionary<FieldKind, List<string>> _kandidat = new();

        public UIElement Panel { get; }

        
        
        
        public static string JudulGrup(FieldSpec field)
            => field.Label + (field.Multiple ? $"  ·  pilih {field.Min}–{field.Max}" : "");

        public FormulirAnalisis(AnalysisSpec spec, Func<FieldKind, List<string>> daftarVariabel)
        {
            _daftarVariabel = daftarVariabel;

            foreach (var field in spec.Fields)
                if (!_kandidat.ContainsKey(field.Kind))
                    _kandidat[field.Kind] = daftarVariabel(field.Kind);

            var kisi = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            kisi.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(232) });
            kisi.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            kisi.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            
            
            
            var grupSumber = new GroupBox { Header = "Variabel tersedia" };
            _sumber.SelectionMode = SelectionMode.Extended;
            _sumber.MinHeight = 140;
            _sumber.MouseDoubleClick += (_, _) => PindahOtomatis();
            AutomationProperties.SetName(_sumber, "Variabel tersedia");
            IsiSumber();
            grupSumber.Content = _sumber;

            Grid.SetColumn(grupSumber, 0);
            Grid.SetRow(grupSumber, 0);
            Grid.SetRowSpan(grupSumber, Math.Max(1, spec.Fields.Count));
            kisi.Children.Add(grupSumber);

            if (_sumber.Items.Count == 0)
            {
                grupSumber.Content = new TextBlock
                {
                    Text = "Belum ada variabel. Buka berkas data dulu, atau tambah variabel di tab Variabel.",
                    Foreground = (Brush)Application.Current.Resources["Peringatan"],
                    TextWrapping = TextWrapping.Wrap
                };
            }

            for (int i = 0; i < spec.Fields.Count; i++)
                BuatBaris(kisi, spec.Fields[i], i);

            var pembungkus = new StackPanel();
            pembungkus.Children.Add(kisi);
            pembungkus.Children.Add(BuatPanelOpsi(spec));

            _status.FontSize = 11.5;
            _status.Foreground = (Brush)Application.Current.Resources["Peringatan"];
            _status.TextWrapping = TextWrapping.Wrap;
            _status.Margin = new Thickness(0, 4, 0, 0);
            _status.Visibility = Visibility.Collapsed;
            pembungkus.Children.Add(_status);

            Panel = pembungkus;
        }

        
        
        
        private void IsiSumber()
        {
            _sumber.Items.Clear();

            var nama = new List<string>();
            foreach (var senarai in _kandidat.Values)
                foreach (string n in senarai)
                    if (!nama.Contains(n)) nama.Add(n);

            if (nama.Count == 0) return;

            bool adaAngka = _kandidat.ContainsKey(FieldKind.Numeric);
            bool adaKategori = _kandidat.ContainsKey(FieldKind.Categorical);
            bool campuran = adaAngka && adaKategori;

            foreach (string n in nama)
                _sumber.Items.Add(BuatButir(n, campuran));
        }

        
        
        
        private ListBoxItem BuatButir(string nama, bool tampilkanJenis)
        {
            var butir = new ListBoxItem { Tag = nama };
            AutomationProperties.SetName(butir, nama);

            var angka = _kandidat.TryGetValue(FieldKind.Numeric, out var num) && num.Contains(nama);
            var kategori = _kandidat.TryGetValue(FieldKind.Categorical, out var kat) && kat.Contains(nama);

            if (!tampilkanJenis)
            {
                butir.Content = nama;
                butir.ToolTip = angka ? "Angka (skala)" : kategori ? "Kategori" : "Sembarang";
                return butir;
            }

            var baris = new DockPanel { LastChildFill = true };
            var lencana = new TextBlock
            {
                Text = angka ? "123" : "abc",
                FontSize = 10.5,
                FontFamily = new FontFamily("Consolas"),
                Foreground = (Brush)Application.Current.Resources["TeksRedup"],
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(lencana, Dock.Right);
            baris.Children.Add(lencana);
            baris.Children.Add(new TextBlock { Text = nama, TextTrimming = TextTrimming.CharacterEllipsis });

            butir.Content = baris;
            butir.ToolTip = angka ? "Angka (skala) — bisa dipakai di semua kotak" 
                                  : "Kategori — hanya untuk kotak yang meminta kategori";
            return butir;
        }

        
        
        
        private void BuatBaris(Grid kisi, FieldSpec field, int indeks)
        {
            kisi.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            
            
            
            var panah = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, indeks == 0 ? 26 : 12, 0, 0)
            };

            var target = new ListBox
            {
                MinHeight = 92,
                MaxHeight = 190,
                SelectionMode = field.Multiple ? SelectionMode.Extended : SelectionMode.Single
            };

            var tombolMasuk = new Button
            {
                Content = "❯",
                Width = 38,
                Padding = new Thickness(0, 4, 0, 4),
                Margin = new Thickness(0, 0, 0, 6),
                FontSize = 11,
                ToolTip = $"Masukkan variabel terpilih ke “{field.Label}”"
            };
            tombolMasuk.Click += (_, _) => Masukkan(field, target);

            var tombolKeluar = new Button
            {
                Content = "❮",
                Width = 38,
                Padding = new Thickness(0, 4, 0, 4),
                FontSize = 11,
                ToolTip = $"Keluarkan variabel terpilih dari “{field.Label}”"
            };
            tombolKeluar.Click += (_, _) => Keluarkan(target);

            panah.Children.Add(tombolMasuk);
            panah.Children.Add(tombolKeluar);

            Grid.SetColumn(panah, 1);
            Grid.SetRow(panah, indeks);
            kisi.Children.Add(panah);

            
            
            
            var hitung = new TextBlock
            {
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TeksRedup"],
                Margin = new Thickness(0, 0, 0, 4)
            };

            var isi = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(hitung, Dock.Bottom);
            isi.Children.Add(hitung);
            isi.Children.Add(target);

            var grup = new GroupBox { Header = JudulGrup(field), Content = isi };

            Grid.SetColumn(grup, 2);
            Grid.SetRow(grup, indeks);
            kisi.Children.Add(grup);

            target.SelectionChanged += (_, _) => SegarkanHitung();

            var baris = new Baris { Field = field, Target = target, Hitung = hitung, Grup = grup };
            _baris.Add(baris);
            SegarkanHitung(baris);
        }

        
        
        
        private UIElement BuatPanelOpsi(AnalysisSpec spec)
        {
            if (spec.Options.Count == 0) return new StackPanel { Visibility = Visibility.Collapsed };

            var grup = new GroupBox { Header = "Opsi", Margin = new Thickness(0, 10, 0, 0) };
            var panel = new StackPanel();
            grup.Content = panel;

            foreach (var opt in spec.Options)
            {
                Control kontrol;
                switch (opt.Type)
                {
                    case OptionType.Bool:
                        kontrol = new CheckBox
                        {
                            Content = opt.Label,
                            IsChecked = opt.Checked,
                            Foreground = (Brush)Application.Current.Resources["Teks"]
                        };
                        break;
                    case OptionType.Choice:
                        var combo = new ComboBox { SelectedIndex = (int)opt.Value };
                        foreach (string pilihan in opt.Choices) combo.Items.Add(pilihan);
                        if (combo.Items.Count > 0 && combo.SelectedIndex < 0) combo.SelectedIndex = 0;
                        kontrol = combo;
                        break;
                    default:
                        // Opsi teks bebas (nama berkas, label, rentang) tidak
                        // punya angka awal yang berarti.
                        kontrol = new TextBox
                        {
                            Text = opt.Type == OptionType.Text
                                ? opt.Text
                                : opt.Value.ToString(CultureInfo.InvariantCulture)
                        };
                        break;
                }

                if (opt.Type != OptionType.Bool)
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = opt.Label,
                        FontSize = 11.5,
                        Foreground = (Brush)Application.Current.Resources["TeksRedup"],
                        Margin = new Thickness(0, 8, 0, 3)
                    });
                }

                kontrol.Margin = new Thickness(0, 0, 0, 6);
                _opsi[opt.Key] = kontrol;
                panel.Children.Add(kontrol);
            }

            return grup;
        }

        
        
        
        private List<string> Kandidat(FieldSpec field)
            => _kandidat.TryGetValue(field.Kind, out var senarai) ? senarai : new List<string>();

        private void Masukkan(FieldSpec field, ListBox target)
        {
            var dipilih = _sumber.SelectedItems.Cast<ListBoxItem>()
                                .Select(i => i.Tag as string ?? "")
                                .Where(s => s.Length > 0).ToList();

            if (dipilih.Count == 0)
            {
                Peringatkan("Pilih dulu variabel di daftar kiri, lalu klik ❯.");
                return;
            }

            var sudah = NamaTerpilih(target);
            var kandidat = Kandidat(field);
            int masuk = 0;

            foreach (string n in dipilih)
            {
                if (sudah.Contains(n)) continue;

                if (!kandidat.Contains(n))
                {
                    Peringatkan($"'{n}' tidak bisa dimasukkan ke “{field.Label}” karena jenisnya "
                                + $"tidak sesuai ({NamaJenis(field.Kind)}). Ubah tingkat ukurnya di tab Variabel.");
                    continue;
                }

                if (sudah.Count >= field.Max)
                {
                    Peringatkan($"“{field.Label}” sudah berisi {field.Max} variabel — itu batasnya. "
                                + $"Keluarkan salah satu dulu dengan ❮.");
                    break;
                }

                target.Items.Add(BuatButir(n, false));
                sudah.Add(n);
                masuk++;
            }

            if (masuk == 0) return;

            _status.Visibility = Visibility.Collapsed;
            SegarkanHitung();
        }

        private void Keluarkan(ListBox target)
        {
            var dipilih = target.SelectedItems.Cast<ListBoxItem>().ToList();
            if (dipilih.Count == 0)
            {
                Peringatkan("Pilih dulu variabel di kotak kanan, lalu klik ❮.");
                return;
            }

            foreach (var butir in dipilih) target.Items.Remove(butir);
            _status.Visibility = Visibility.Collapsed;
            SegarkanHitung();
        }

        
        
        
        
        private void PindahOtomatis()
        {
            var dipilih = _sumber.SelectedItems.Cast<ListBoxItem>()
                                .Select(i => i.Tag as string ?? "")
                                .Where(s => s.Length > 0).ToList();
            if (dipilih.Count == 0) return;

            foreach (string n in dipilih)
            {
                var tujuan = _baris.FirstOrDefault(b =>
                    NamaTerpilih(b.Target).Count < b.Field.Max
                    && !NamaTerpilih(b.Target).Contains(n)
                    && Kandidat(b.Field).Contains(n));

                if (tujuan == null) continue;

                tujuan.Target.Items.Add(BuatButir(n, false));
                SegarkanHitung(tujuan);
            }

            _status.Visibility = Visibility.Collapsed;
        }

        private static string NamaJenis(FieldKind jenis) => jenis switch
        {
            FieldKind.Numeric => "harus angka",
            FieldKind.Categorical => "harus kategori",
            _ => "sembarang"
        };

        private static List<string> NamaTerpilih(ListBox daftar)
            => daftar.Items.Cast<ListBoxItem>()
                     .Select(i => i.Tag as string ?? "")
                     .Where(s => s.Length > 0).ToList();

        private void Peringatkan(string pesan)
        {
            _status.Text = pesan;
            _status.Visibility = Visibility.Visible;
        }

        private void SegarkanHitung()
        {
            foreach (var b in _baris) SegarkanHitung(b);
        }

        private void SegarkanHitung(Baris b)
        {
            int n = b.Target.Items.Count;

            if (n == 0)
            {
                b.Hitung.Text = b.Field.Min > 0
                    ? $"Wajib diisi — sedikitnya {b.Field.Min} variabel."
                    : "Boleh dikosongkan.";
                return;
            }

            b.Hitung.Text = b.Field.Multiple
                ? $"{n} variabel terpilih" + (n < b.Field.Min ? $" — kurang {b.Field.Min - n} lagi." : ".")
                : "1 variabel terpilih";
        }

        
        
        
        
        
        
        public int IsiOtomatis()
        {
            Kosongkan();

            int total = 0;
            var dipakai = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var b in _baris)
            {
                var kandidat = Kandidat(b.Field);
                if (kandidat.Count == 0) continue;

                
                
                
                if (b.Field.Min < 1 && !AdaPetunjukNama(b.Field, kandidat)) continue;

                int target = b.Field.Multiple ? JumlahTarget(b.Field, kandidat.Count) : 1;

                var urut = kandidat
                           .OrderByDescending(n => SkorNama(n, b.Field))
                           .ThenBy(n => dipakai.Contains(n) ? 1 : 0)
                           .ToList();

                var diField = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string n in urut)
                {
                    if (diField.Count >= target) break;
                    if (dipakai.Contains(n)) continue;
                    b.Target.Items.Add(BuatButir(n, false));
                    diField.Add(n);
                }

                
                
                foreach (string n in urut)
                {
                    if (diField.Count >= Math.Max(b.Field.Min, 1)) break;
                    if (diField.Contains(n)) continue;
                    b.Target.Items.Add(BuatButir(n, false));
                    diField.Add(n);
                }

                if (diField.Count == 0) continue;

                foreach (string n in diField) dipakai.Add(n);
                total += diField.Count;
            }

            if (total > 0)
            {
                _status.Text = $"{total} variabel diisi otomatis. Periksa sebentar sebelum menjalankan "
                             + "— atau klik Kosongkan lalu pilih sendiri.";
                _status.Foreground = (Brush)Application.Current.Resources["TeksRedup"];
                _status.Visibility = Visibility.Visible;
            }

            SegarkanHitung();
            return total;
        }

        
        
        public void Kosongkan()
        {
            foreach (var b in _baris) b.Target.Items.Clear();
            _status.Visibility = Visibility.Collapsed;
            _status.Foreground = (Brush)Application.Current.Resources["Peringatan"];
            SegarkanHitung();
        }

        
        
        
        private static int JumlahTarget(FieldSpec field, int jumlahKandidat)
        {
            int maks = field.Max <= 6
                ? field.Max
                : Math.Max(field.Min, 3);

            return Math.Min(jumlahKandidat, Math.Max(field.Min, maks));
        }

        
        
        
        private static List<string> KataKunci(FieldSpec field)
        {
            var kata = new List<string>();
            foreach (string sumber in new[] { field.Key, field.Label })
            {
                foreach (string bagian in sumber.Split(new[] { ' ', '_', '-', '(', ')', '/', ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (bagian.Length >= 4) kata.Add(bagian.ToLowerInvariant());
                }
            }
            return kata;
        }

        private static bool AdaPetunjukNama(FieldSpec field, List<string> kandidat)
        {
            var kata = KataKunci(field);
            if (kata.Count == 0) return false;

            return kandidat.Any(n => kata.Any(k => n.Contains(k, StringComparison.OrdinalIgnoreCase)));
        }

        private static int SkorNama(string nama, FieldSpec field)
        {
            int skor = 0;
            foreach (string k in KataKunci(field))
                if (nama.Contains(k, StringComparison.OrdinalIgnoreCase)) skor += 3;
            return skor;
        }

        public RunContext? Kumpulkan(AnalysisSpec spec, Dataset data, double alpha = 0.05)
        {
            var ctx = new RunContext { Data = data, Alpha = alpha };

            foreach (var b in _baris)
            {
                var terpilih = NamaTerpilih(b.Target);

                if (terpilih.Count < b.Field.Min)
                {
                    MessageBox.Show($"Isi dulu “{b.Field.Label}” — sedikitnya {b.Field.Min} variabel.\n\n"
                                    + "Pilih variabel di daftar kiri, lalu klik ❯.",
                                    "Belum lengkap", MessageBoxButton.OK, MessageBoxImage.Information);
                    return null;
                }
                if (terpilih.Count > b.Field.Max)
                {
                    MessageBox.Show($"“{b.Field.Label}” paling banyak {b.Field.Max} variabel. "
                                    + $"Sekarang berisi {terpilih.Count}.\n\nKeluarkan kelebihannya dengan ❮.",
                                    "Terlalu banyak", MessageBoxButton.OK, MessageBoxImage.Information);
                    return null;
                }
                ctx.Fields[b.Field.Key] = terpilih;
            }

            foreach (var opt in spec.Options)
            {
                var salin = opt.Clone();
                if (_opsi.TryGetValue(opt.Key, out var kontrol))
                {
                    switch (kontrol)
                    {
                        case CheckBox cb: salin.Checked = cb.IsChecked == true; break;
                        case ComboBox cmb: salin.Value = cmb.SelectedIndex; break;
                        case TextBox tb:
                            // Teks yang diketik harus tersimpan juga; dulu hanya
                            // Value yang dibaca, jadi opsi teks selalu kosong.
                            salin.Text = tb.Text ?? "";
                            if (double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double angka))
                                salin.Value = angka;
                            break;
                    }
                }
                ctx.Options[opt.Key] = salin;
            }

            return ctx;
        }

        public Dictionary<string, List<string>> PilihanTersimpan()
        {
            var hasil = new Dictionary<string, List<string>>();
            foreach (var b in _baris) hasil[b.Field.Key] = NamaTerpilih(b.Target);
            return hasil;
        }
    }
}
