using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
using AlatStatistik.Models;

namespace AlatStatistik.Views
{

    public static class ResultsRenderer
    {
        public static UIElement Judul(string teks, int level)
        {
            var tb = new TextBlock
            {
                Text = teks,
                FontSize = level == 1 ? 16 : 13.5,
                FontWeight = level == 1 ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = level == 1
                    ? (Brush)Application.Current.Resources["Teks"]
                    : (Brush)Application.Current.Resources["Aksen"],
                Margin = new Thickness(level == 1 ? 0 : 0, level == 1 ? 18 : 12, 0, 6)
            };
            return tb;
        }

        public static UIElement Tabel(string? judul, List<string> kolom, List<List<string>> baris, string? catatan)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 10) };

            if (!string.IsNullOrWhiteSpace(judul))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = judul,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["Aksen"],
                    Margin = new Thickness(0, 4, 0, 6)
                });
            }

            var grid = new Grid();
            AutomationProperties.SetName(grid,
                string.IsNullOrWhiteSpace(judul) ? "Tabel hasil" : "Tabel: " + judul);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int c = 1; c < kolom.Count; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < kolom.Count; c++)
            {
                var sel = new Border
                {
                    Background = (Brush)Application.Current.Resources["Panel2"],
                    BorderBrush = (Brush)Application.Current.Resources["Garis"],
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 6, 10, 6),
                    Child = new TextBlock
                    {
                        Text = kolom[c],
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)Application.Current.Resources["Teks"],
                        FontSize = 12.5
                    }
                };
                Grid.SetRow(sel, 0);
                Grid.SetColumn(sel, c);
                grid.Children.Add(sel);
            }

            
            for (int r = 0; r < baris.Count; r++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Brush latar = r % 2 == 1
                    ? (Brush)Application.Current.Resources["Panel2"]
                    : (Brush)Application.Current.Resources["Panel"];

                for (int c = 0; c < kolom.Count; c++)
                {
                    string isi = c < baris[r].Count ? baris[r][c] : "";
                    var sel = new Border
                    {
                        Background = latar,
                        BorderBrush = (Brush)Application.Current.Resources["Garis"],
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(10, 5, 10, 5),
                        Child = new TextBlock
                        {
                            Text = isi,
                            Foreground = (Brush)Application.Current.Resources["Teks"],
                            FontSize = 12.5,
                            TextAlignment = c == 0 ? TextAlignment.Left : TextAlignment.Right
                        }
                    };
                    grid.ColumnDefinitions[c].Width = GridLength.Auto;
                    Grid.SetRow(sel, r + 1);
                    Grid.SetColumn(sel, c);
                    grid.Children.Add(sel);
                }
            }

            var bingkai = new Border
            {
                BorderBrush = (Brush)Application.Current.Resources["Garis"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = grid,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            panel.Children.Add(bingkai);

            if (!string.IsNullOrWhiteSpace(catatan))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = StripHtml(catatan),
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources["TeksRedup"],
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(2, 6, 0, 0),
                    MaxWidth = 760
                });
            }

            return panel;
        }

        public static UIElement Catatan(string teks, NoteKind jenis)
        {
            string kunci = jenis switch
            {
                NoteKind.Ok => "Sukses",
                NoteKind.Warning => "Peringatan",
                NoteKind.Error => "Bahaya",
                _ => "Aksen"
            };

            return new Border
            {
                Background = (Brush)Application.Current.Resources["Panel"],
                BorderBrush = (Brush)Application.Current.Resources[kunci],
                BorderThickness = new Thickness(3, 1, 1, 1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 8, 0, 8),
                Child = new TextBlock
                {
                    Text = StripHtml(teks),
                    Foreground = (Brush)Application.Current.Resources["Teks"],
                    FontSize = 12.5,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 760
                }
            };
        }

        public static UIElement Rumus(string? judul, List<RumusInfo> daftar)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 12) };

            if (!string.IsNullOrWhiteSpace(judul))
                panel.Children.Add(new TextBlock
                {
                    Text = judul,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["Aksen"],
                    Margin = new Thickness(0, 4, 0, 6)
                });

            foreach (var r in daftar)
                panel.Children.Add(KartuRumus(r));

            return panel;
        }

        private static UIElement KartuRumus(RumusInfo r)
        {
            var isi = new StackPanel { Margin = new Thickness(12, 9, 12, 10) };

            
            isi.Children.Add(new TextBlock
            {
                Text = r.Nama,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["Teks"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            });

            
            isi.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources["Panel"],
                BorderBrush = (Brush)Application.Current.Resources["Garis"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 7),
                Child = new TextBlock
                {
                    Text = r.Bentuk,
                    FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New, monospace"),
                    FontSize = 14,
                    Foreground = (Brush)Application.Current.Resources["AksenTua"],
                    TextWrapping = TextWrapping.Wrap
                }
            });

            
            var meta = new WrapPanel { Margin = new Thickness(0, 0, 0, 3) };
            meta.Children.Add(ChipMeta("Jenis", r.Jenis));
            meta.Children.Add(ChipMeta("Penemu", r.LabelTahun));
            isi.Children.Add(meta);

            if (!string.IsNullOrWhiteSpace(r.Catatan))
                isi.Children.Add(new TextBlock
                {
                    Text = r.Catatan,
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources["TeksRedup"],
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 700,
                    Margin = new Thickness(0, 3, 0, 0)
                });

            return new Border
            {
                Background = (Brush)Application.Current.Resources["Panel2"],
                BorderBrush = (Brush)Application.Current.Resources["Garis"],
                BorderThickness = new Thickness(1),
                
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 8),
                Child = new Grid
                {
                    Children =
                    {
                        new Border
                        {
                            Width = 4,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Background = (Brush)Application.Current.Resources["Aksen"],
                            CornerRadius = new CornerRadius(6, 0, 0, 6)
                        },
                        new Border { Margin = new Thickness(4, 0, 0, 0), Child = isi }
                    }
                }
            };
        }

        private static UIElement ChipMeta(string label, string nilai)
        {
            if (string.IsNullOrWhiteSpace(nilai)) return new TextBlock();

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 14, 2)
            };
            panel.Children.Add(new TextBlock
            {
                Text = label + ": ",
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TeksRedup"]
            });
            panel.Children.Add(new TextBlock
            {
                Text = nilai,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["Teks"],
                TextWrapping = TextWrapping.Wrap
            });
            return panel;
        }

        public static UIElement Substitusi(string? judul, List<LangkahHitung> langkah)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 14) };

            if (!string.IsNullOrWhiteSpace(judul))
                panel.Children.Add(new TextBlock
                {
                    Text = judul,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["Aksen"],
                    Margin = new Thickness(0, 4, 0, 6)
                });

            var kiri = new ColumnDefinition { Width = GridLength.Auto, MinWidth = 120, MaxWidth = 210 };
            var grid = new Grid { Margin = new Thickness(0) };
            grid.ColumnDefinitions.Add(kiri);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            for (int i = 0; i < langkah.Count; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var selKiri = new Border
                {
                    Background = (Brush)Application.Current.Resources["Panel2"],
                    BorderBrush = (Brush)Application.Current.Resources["Garis"],
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(9, 6, 9, 6),
                    Child = new TextBlock
                    {
                        Text = langkah[i].Uraian,
                        FontSize = 11.5,
                        Foreground = (Brush)Application.Current.Resources["TeksRedup"],
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 200
                    }
                };

                var selKanan = new Border
                {
                    Background = (Brush)Application.Current.Resources["Panel"],
                    BorderBrush = (Brush)Application.Current.Resources["Garis"],
                    BorderThickness = new Thickness(0, 1, 1, 1),
                    Padding = new Thickness(11, 6, 11, 6),
                    Child = new TextBlock
                    {
                        Text = langkah[i].Hitungan,
                        FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New, monospace"),
                        FontSize = 12.5,
                        Foreground = (Brush)Application.Current.Resources["Teks"],
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 620
                    }
                };

                Grid.SetRow(selKiri, i); Grid.SetColumn(selKiri, 0);
                Grid.SetRow(selKanan, i); Grid.SetColumn(selKanan, 1);
                grid.Children.Add(selKiri);
                grid.Children.Add(selKanan);
            }

            panel.Children.Add(new Border
            {
                BorderBrush = (Brush)Application.Current.Resources["Garis"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = grid,
                HorizontalAlignment = HorizontalAlignment.Left
            });

            return panel;
        }

        public static UIElement Grafik(string? judul, ChartSpec spec)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 14) };

            if (!string.IsNullOrWhiteSpace(judul))
                panel.Children.Add(new TextBlock
                {
                    Text = judul,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["Aksen"],
                    Margin = new Thickness(0, 4, 0, 6)
                });

            var chart = new ChartControl { Height = 260, MinWidth = 620 };
            AutomationProperties.SetName(chart,
                string.IsNullOrWhiteSpace(judul) ? "Diagram hasil" : "Diagram: " + judul);
            chart.Gambar(spec);
            chart.Loaded += (_, _) => chart.Gambar(spec);   

            panel.Children.Add(new Border
            {
                BorderBrush = (Brush)Application.Current.Resources["Garis"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = chart
            });

            panel.Children.Add(BilahEkspor(spec, judul));

            
            
            
            var cara = PenjelasanGrafik.Untuk(spec.Kind);
            if (cara != null) panel.Children.Add(PanelCaraMembaca(cara));

            return panel;
        }

        public static UIElement BilahEkspor(ChartSpec spec, string? judul)
        {
            var baris = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var salin = new Button
            {
                Content = "Salin gambar",
                Style = (Style)Application.Current.Resources["TombolUtama"],
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Menyalin gambar 2560 × 1200 piksel (384 dpi). "
                        + "Tempel ke Word dengan Ctrl+V, atau Sisipkan ▸ Gambar bila menyimpannya dulu."
            };
            salin.Click += (_, _) =>
            {
                try
                {
                    EksporGrafik.SalinKePapanKlip(spec);
                    Pesan(baris, "Gambar disalin — tempel ke Word dengan Ctrl+V.");
                }
                catch (Exception ex)
                {
                    Pesan(baris, "Gagal menyalin: " + ex.Message, "Bahaya");
                }
            };
            baris.Children.Add(salin);

            baris.Children.Add(TombolSimpan(
                spec, judul, baris,
                "Simpan PNG", "Gambar PNG (*.png)|*.png", ".png",
                "Menyimpan gambar 2560 × 1200 piksel (384 dpi) — tajam sampai cetak.",
                EksporGrafik.SimpanPng));

            baris.Children.Add(TombolSimpan(
                spec, judul, baris,
                "Simpan SVG", "Gambar vektor SVG (*.svg)|*.svg", ".svg",
                "Gambar vektor: bisa diperbesar sebesar apa pun tanpa pecah. "
                + "Word 2016 ke atas bisa menyisipkan SVG.",
                EksporGrafik.SimpanSvg));

            baris.Children.Add(TombolSimpan(
                spec, judul, baris,
                "Simpan data (CSV)", "Berkas CSV (*.csv)|*.csv", ".csv",
                "Angka di balik grafik ini, untuk membuat Bagan asli di Word atau Excel.",
                EksporGrafik.SimpanDataCsv));

            return baris;
        }

        private static Button TombolSimpan(ChartSpec spec, string? judul, StackPanel baris,
                                           string label, string filter, string ekstensi,
                                           string tooltip, Action<ChartSpec, string> simpan)
        {
            var tombol = new Button
            {
                Content = label,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = tooltip
            };
            tombol.Click += (_, _) =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = label,
                    Filter = filter,
                    DefaultExt = ekstensi,
                    FileName = EksporGrafik.NamaAman(judul) + ekstensi
                };
                if (dlg.ShowDialog() != true) return;

                try
                {
                    simpan(spec, dlg.FileName);
                    Pesan(baris, "Disimpan: " + dlg.FileName);
                }
                catch (Exception ex)
                {
                    Pesan(baris, "Gagal menyimpan: " + ex.Message, "Bahaya");
                }
            };
            return tombol;
        }

        private static void Pesan(StackPanel baris, string teks, string warnaKunci = "Sukses")
        {
            TextBlock? pesan = null;
            foreach (var anak in baris.Children)
                if (anak is TextBlock t && (t.Tag as string) == "pesan") pesan = t;

            if (pesan == null)
            {
                pesan = new TextBlock
                {
                    Tag = "pesan",
                    FontSize = 11.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 400
                };
                baris.Children.Add(pesan);
            }

            pesan.Text = teks;
            pesan.Foreground = (Brush)Application.Current.Resources[warnaKunci];
        }

        public static UIElement PanelCaraMembaca(CaraMembaca cara)
        {
            var isi = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            isi.Children.Add(BlokCara("Apa yang dibaca", cara.Bacaan, "Aksen"));
            isi.Children.Add(BlokCara("Yang perlu diperhatikan", cara.Perhatikan, "Aksen"));
            isi.Children.Add(BlokCara("Hati-hati", cara.HatiHati, "Peringatan"));

            var kepala = new TextBlock
            {
                Text = "Cara membaca diagram ini",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["Teks"],
                VerticalAlignment = VerticalAlignment.Center
            };

            var tombol = new Button
            {
                Content = "Sembunyikan",
                FontSize = 11.5,
                Padding = new Thickness(8, 3, 8, 3),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            tombol.Click += (_, _) =>
            {
                bool tampil = isi.Visibility != Visibility.Visible;
                isi.Visibility = tampil ? Visibility.Visible : Visibility.Collapsed;
                tombol.Content = tampil ? "Sembunyikan" : "Tampilkan";
            };

            var judulBaris = new Grid();
            judulBaris.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            judulBaris.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(kepala, 0);
            Grid.SetColumn(tombol, 1);
            judulBaris.Children.Add(kepala);
            judulBaris.Children.Add(tombol);

            var dalam = new StackPanel();
            dalam.Children.Add(judulBaris);
            dalam.Children.Add(isi);

            return new Border
            {
                Background = (Brush)Application.Current.Resources["Panel"],
                BorderBrush = (Brush)Application.Current.Resources["Garis"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 9, 12, 10),
                Margin = new Thickness(0, 10, 0, 0),
                MaxWidth = 760,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = dalam
            };
        }

        private static UIElement BlokCara(string label, string teks, string warnaKunci)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            if (string.IsNullOrWhiteSpace(teks)) return panel;

            var baris = new StackPanel { Orientation = Orientation.Horizontal };
            baris.Children.Add(new Border
            {
                Width = 3,
                Background = (Brush)Application.Current.Resources[warnaKunci],
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 2, 7, 2)
            });
            baris.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources[warnaKunci],
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(baris);

            panel.Children.Add(new TextBlock
            {
                Text = StripHtml(teks),
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["Teks"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10, 3, 0, 0),
                MaxWidth = 700
            });

            return panel;
        }

        public static UIElement Render(List<ResultBlock> blocks)
        {
            var panel = new StackPanel { Margin = new Thickness(12) };

            if (blocks.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "Belum ada hasil. Pilih analisis di kiri, isi variabelnya, lalu klik Jalankan.",
                    Foreground = (Brush)Application.Current.Resources["TeksRedup"],
                    FontSize = 13, Margin = new Thickness(0, 20, 0, 0)
                });
                return panel;
            }

            foreach (var b in blocks)
            {
                switch (b.Kind)
                {
                    case BlockKind.Heading: panel.Children.Add(Judul(b.Text, b.Level)); break;
                    case BlockKind.Table: panel.Children.Add(Tabel(b.Title, b.Columns, b.Rows, b.Footnote)); break;
                    case BlockKind.Note: panel.Children.Add(Catatan(b.Text, b.Note)); break;
                    case BlockKind.Chart:
                        if (b.Chart != null) panel.Children.Add(Grafik(b.Title, b.Chart));
                        break;
                    case BlockKind.Rumus:
                        if (b.RumusList.Count > 0) panel.Children.Add(Rumus(b.Title, b.RumusList));
                        break;
                    case BlockKind.Substitusi:
                        if (b.Langkah.Count > 0) panel.Children.Add(Substitusi(b.Title, b.Langkah));
                        break;
                }
            }

            return panel;
        }

        private static string StripHtml(string s)
            => s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
    }
}
