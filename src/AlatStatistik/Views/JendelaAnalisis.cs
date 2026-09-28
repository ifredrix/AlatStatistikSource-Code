using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AlatStatistik.Analysis;
using AlatStatistik.Models;

namespace AlatStatistik.Views
{

    public class JendelaAnalisis : Window
    {
        private readonly FormulirAnalisis _formulir;
        private readonly AnalysisSpec _spec;
        private Dataset _data;

        
        
        
        public RunContext? Konteks { get; private set; }

        public JendelaAnalisis(Dataset data, AnalysisSpec spec, Window? pemilik)
        {
            _data = data;
            _spec = spec;

            
            
            
            _formulir = new FormulirAnalisis(spec, DaftarVariabel);

            Title = spec.Name + " — AlatStatistik";
            MinWidth = 720;
            MinHeight = 480;
            Width = 880;
            Height = 640;
            this.PusatkanDiLayarAktif(pemilik);

            if (pemilik != null) Owner = pemilik;
            ShowInTaskbar = false;

            var akar = new DockPanel { LastChildFill = true };

            
            
            
            var kepala = new Border
            {
                Background = (Brush)Application.Current.Resources["Panel"],
                BorderBrush = (Brush)Application.Current.Resources["Garis"],
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(18, 14, 18, 12)
            };
            DockPanel.SetDock(kepala, Dock.Top);

            var isiKepala = new StackPanel();

            var barisJudul = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 4) };

            var lencana = new Border
            {
                Background = (Brush)Application.Current.Resources["Panel2"],
                BorderBrush = (Brush)Application.Current.Resources["Garis"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            lencana.Child = new TextBlock
            {
                Text = spec.IsML ? "ML" : spec.Category,
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TeksRedup"]
            };
            DockPanel.SetDock(lencana, Dock.Right);
            barisJudul.Children.Add(lencana);

            barisJudul.Children.Add(new TextBlock
            {
                Text = spec.Name,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["Aksen"]
            });
            isiKepala.Children.Add(barisJudul);

            isiKepala.Children.Add(new TextBlock
            {
                Text = spec.Description,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TeksRedup"],
                TextWrapping = TextWrapping.Wrap
            });

            kepala.Child = isiKepala;
            akar.Children.Add(kepala);

            
            
            
            var kaki = new Border
            {
                Background = (Brush)Application.Current.Resources["Panel"],
                BorderBrush = (Brush)Application.Current.Resources["Garis"],
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(18, 10, 18, 10)
            };
            DockPanel.SetDock(kaki, Dock.Bottom);

            var barisKaki = new DockPanel { LastChildFill = false };

            var tombolBatal = new Button
            {
                Content = "Batal",
                Padding = new Thickness(20, 7, 20, 7),
                IsCancel = true,
                ToolTip = "Tutup tanpa menjalankan (Esc)"
            };
            tombolBatal.Click += (_, _) => { DialogResult = false; Close(); };
            DockPanel.SetDock(tombolBatal, Dock.Right);
            barisKaki.Children.Add(tombolBatal);

            var tombolOk = new Button
            {
                Content = "OK",
                Padding = new Thickness(24, 7, 24, 7),
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true,
                Style = (Style)Application.Current.Resources["TombolUtama"],
                ToolTip = "Jalankan analisis (Enter)"
            };
            tombolOk.Click += (_, _) => Ok();
            DockPanel.SetDock(tombolOk, Dock.Right);
            barisKaki.Children.Add(tombolOk);

            var tombolOtomatis = new Button
            {
                Content = "Isi otomatis",
                Padding = new Thickness(14, 7, 14, 7),
                ToolTip = "Isi semua kotak variabel dengan tebakan terbaik dari data ini"
            };
            tombolOtomatis.Click += (_, _) => _formulir.IsiOtomatis();
            DockPanel.SetDock(tombolOtomatis, Dock.Left);
            barisKaki.Children.Add(tombolOtomatis);

            var tombolKosong = new Button
            {
                Content = "Kosongkan",
                Padding = new Thickness(14, 7, 14, 7),
                Margin = new Thickness(10, 0, 0, 0),
                ToolTip = "Keluarkan semua variabel dari kotak kanan"
            };
            tombolKosong.Click += (_, _) => _formulir.Kosongkan();
            DockPanel.SetDock(tombolKosong, Dock.Left);
            barisKaki.Children.Add(tombolKosong);

            kaki.Child = barisKaki;
            akar.Children.Add(kaki);

            
            
            
            var gulir = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(18, 14, 18, 10)
            };

            gulir.Content = _formulir.Panel;
            akar.Children.Add(gulir);

            Content = akar;

            Loaded += (_, _) =>
            {
                if (data.IsEmpty)
                {
                    tombolOk.IsEnabled = false;
                    return;
                }
                _formulir.IsiOtomatis();
                tombolOk.Focus();
            };
        }

        private List<string> DaftarVariabel(FieldKind jenis) => jenis switch
        {
            FieldKind.Numeric => _data.NumericColumns(),
            FieldKind.Categorical => _data.CategoricalColumns(),
            _ => _data.Names
        };

        private void Ok()
        {
            if (_data.IsEmpty)
            {
                MessageBox.Show("Belum ada data. Buka berkas data atau muat contoh data dulu.",
                                "Belum ada data", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var ctx = _formulir.Kumpulkan(_spec, _data);
                if (ctx == null) return;

                Konteks = ctx;
                DialogResult = true;
                Close();
            }
            catch (Exception galat)
            {
                MessageBox.Show(galat.Message, "Analisis tidak bisa dijalankan",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        
        
        
        
        public static void Buka(Dataset data, AnalysisSpec spec, Window pemilik,
                                Action<AnalysisSpec, RunContext> jalankan)
        {
            var jendela = new JendelaAnalisis(data, spec, pemilik);
            bool? hasil;

            try { hasil = jendela.ShowDialog(); }
            catch (Exception galat)
            {
                MessageBox.Show(galat.Message, "Dialog analisis gagal dibuka",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (hasil != true || jendela.Konteks == null) return;

            jalankan(spec, jendela.Konteks);
        }
    }
}
