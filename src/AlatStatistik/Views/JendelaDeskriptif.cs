using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AlatStatistik.Analysis;
using AlatStatistik.Models;

namespace AlatStatistik.Views
{

    public class JendelaDeskriptif : Window
    {
        private readonly Dataset _data;
        private readonly AnalysisSpec _spec;
        private readonly FormulirAnalisis _formulir;
        private readonly StackPanel _panelHasil = new();
        private readonly TextBlock _petunjuk = new();

        public JendelaDeskriptif(Dataset data, AnalysisSpec spec)
        {
            _data = data;
            _spec = spec;

            Title = "Deskriptif — " + spec.Name;
            Width = 1080;
            Height = 680;
            MinWidth = 880;
            MinHeight = 520;
            this.PusatkanDiLayarAktif();
            Background = (Brush)Application.Current.Resources["Latar"];

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            
            var kiri = new DockPanel { Margin = new Thickness(16), LastChildFill = true };

            var judul = new TextBlock
            {
                Text = spec.Name,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["Aksen"],
                Margin = new Thickness(0, 0, 0, 6)
            };
            DockPanel.SetDock(judul, Dock.Top);
            kiri.Children.Add(judul);

            var deskripsi = new TextBlock
            {
                Text = spec.Description,
                FontSize = 11.5,
                Foreground = (Brush)Application.Current.Resources["TeksRedup"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14)
            };
            DockPanel.SetDock(deskripsi, Dock.Top);
            kiri.Children.Add(deskripsi);

            var barisTombol = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 12, 0, 0)
            };
            DockPanel.SetDock(barisTombol, Dock.Bottom);

            var jalankan = new Button
            {
                Content = "Jalankan",
                Padding = new Thickness(18, 7, 18, 7),
                Margin = new Thickness(0, 0, 10, 0),
                Style = (Style)Application.Current.Resources["TombolUtama"]
            };
            jalankan.Click += (_, _) => Jalankan();

            var tutup = new Button { Content = "Tutup", Padding = new Thickness(18, 7, 18, 7) };
            tutup.Click += (_, _) => Close();

            barisTombol.Children.Add(jalankan);
            barisTombol.Children.Add(tutup);
            kiri.Children.Add(barisTombol);

            _formulir = new FormulirAnalisis(spec, DaftarVariabel);
            var gulir = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _formulir.Panel
            };
            kiri.Children.Add(gulir);

            var bingkaiKiri = new Border
            {
                Background = (Brush)Application.Current.Resources["Panel"],
                BorderBrush = (Brush)Application.Current.Resources["Garis"],
                BorderThickness = new Thickness(0, 0, 1, 0),
                Child = kiri
            };
            Grid.SetColumn(bingkaiKiri, 0);
            grid.Children.Add(bingkaiKiri);

            
            _petunjuk.Text = "Pilih variabel di kiri, lalu klik Jalankan. "
                             + "Rumus yang dipakai akan ditampilkan sebelum angka hasilnya.";
            _petunjuk.FontSize = 12;
            _petunjuk.Foreground = (Brush)Application.Current.Resources["TeksRedup"];
            _petunjuk.TextWrapping = TextWrapping.Wrap;

            var kanan = new DockPanel { Margin = new Thickness(14, 16, 16, 16), LastChildFill = true };
            var judulHasil = new TextBlock
            {
                Text = "Hasil",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["Teks"],
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(judulHasil, Dock.Top);
            kanan.Children.Add(judulHasil);

            var panelIsi = new StackPanel();
            panelIsi.Children.Add(_petunjuk);
            panelIsi.Children.Add(_panelHasil);

            kanan.Children.Add(new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                
                
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = panelIsi
            });

            Grid.SetColumn(kanan, 1);
            grid.Children.Add(kanan);

            Content = grid;
        }

        private List<string> DaftarVariabel(FieldKind jenis) => jenis switch
        {
            FieldKind.Numeric => _data.NumericColumns(),
            FieldKind.Categorical => _data.CategoricalColumns(),
            _ => _data.Names
        };

        private void Jalankan()
        {
            if (_data.IsEmpty)
            {
                MessageBox.Show("Belum ada data. Buka berkas CSV atau muat contoh data dulu.",
                                "Belum ada data", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var ctx = _formulir.Kumpulkan(_spec, _data);
                if (ctx == null) return;

                var blok = _spec.Run!(ctx);
                _petunjuk.Visibility = Visibility.Collapsed;
                _panelHasil.Children.Clear();
                _panelHasil.Children.Add(ResultsRenderer.Render(blok));
            }
            catch (Exception galat)
            {
                MessageBox.Show(galat.Message, "Analisis gagal",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
