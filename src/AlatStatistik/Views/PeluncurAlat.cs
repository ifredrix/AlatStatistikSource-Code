using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation;
using System.Windows.Media;
using AlatStatistik.Analysis;

namespace AlatStatistik.Views
{

    public class PeluncurAlat : Window
    {
        private readonly List<ItemAlat> _semua;
        private readonly TextBox _kotakCari = new();
        private readonly ListBox _daftar = new();
        private readonly TextBlock _info = new();

        public AnalysisSpec? Terpilih { get; private set; }

        public PeluncurAlat(IEnumerable<ItemAlat> semua, Window? pemilik)
        {
            _semua = semua.ToList();

            Title = "Cari alat analisis";
            Width = 620;
            Height = 520;
            MinWidth = 460;
            MinHeight = 340;
            ResizeMode = ResizeMode.CanResize;
            this.PusatkanDiLayarAktif(pemilik);
            if (pemilik != null) Owner = pemilik;
            ShowInTaskbar = false;

            var akar = new DockPanel { LastChildFill = true, Margin = new Thickness(14) };

            _kotakCari.FontSize = 14;
            _kotakCari.Padding = new Thickness(8, 6, 8, 6);
            _kotakCari.Margin = new Thickness(0, 0, 0, 8);
            AutomationProperties.SetName(_kotakCari, "Cari alat analisis");
            _kotakCari.TextChanged += (_, _) => Segarkan();
            _kotakCari.PreviewKeyDown += KotakCari_Tombol;
            DockPanel.SetDock(_kotakCari, Dock.Top);
            akar.Children.Add(_kotakCari);

            _info.FontSize = 11.5;
            _info.Foreground = (Brush)Application.Current.Resources["TeksRedup"];
            _info.Margin = new Thickness(0, 0, 0, 8);
            DockPanel.SetDock(_info, Dock.Top);
            akar.Children.Add(_info);

            var barisBawah = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 10, 0, 0) };
            DockPanel.SetDock(barisBawah, Dock.Bottom);

            var buka = new Button
            {
                Content = "Buka",
                Padding = new Thickness(20, 7, 20, 7),
                IsDefault = true,
                Style = (Style)Application.Current.Resources["TombolUtama"]
            };
            buka.Click += (_, _) => Terima();
            DockPanel.SetDock(buka, Dock.Right);
            barisBawah.Children.Add(buka);

            var batal = new Button { Content = "Batal", Padding = new Thickness(16, 7, 16, 7), IsCancel = true };
            DockPanel.SetDock(batal, Dock.Right);
            batal.Margin = new Thickness(0, 0, 10, 0);
            barisBawah.Children.Add(batal);

            var petunjuk = new TextBlock
            {
                Text = "Enter = buka · ↑↓ = pindah · Esc = batal",
                FontSize = 11.5,
                Foreground = (Brush)Application.Current.Resources["TeksRedup"],
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(petunjuk, Dock.Left);
            barisBawah.Children.Add(petunjuk);

            akar.Children.Add(barisBawah);

            _daftar.SelectionMode = SelectionMode.Single;
            _daftar.MouseDoubleClick += (_, _) => Terima();
            _daftar.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) { Terima(); e.Handled = true; }
            };
            AutomationProperties.SetName(_daftar, "Daftar alat analisis");
            _daftar.ItemTemplate = TemplateButir();
            akar.Children.Add(_daftar);

            Content = akar;

            Loaded += (_, _) =>
            {
                Segarkan();
                _kotakCari.Focus();
            };
        }

        private static DataTemplate TemplateButir()
        {
            var pabrik = new FrameworkElementFactory(typeof(StackPanel));
            pabrik.SetValue(StackPanel.MarginProperty, new Thickness(0, 2, 0, 2));

            var fNama = new FrameworkElementFactory(typeof(TextBlock));
            fNama.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Nama"));
            fNama.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            pabrik.AppendChild(fNama);

            var fKat = new FrameworkElementFactory(typeof(TextBlock));
            fKat.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Kategori"));
            fKat.SetValue(TextBlock.FontSizeProperty, 11.0);
            fKat.SetValue(TextBlock.ForegroundProperty, Application.Current.Resources["TeksRedup"]);
            pabrik.AppendChild(fKat);

            return new DataTemplate { VisualTree = pabrik };
        }

        private void Segarkan()
        {
            var cocok = Katalog.Cari(_semua, _kotakCari.Text);

            _daftar.ItemsSource = null;
            _daftar.ItemsSource = cocok;
            if (cocok.Count > 0) _daftar.SelectedIndex = 0;

            _info.Text = cocok.Count == 0
                ? $"Tidak ada alat yang cocok dengan “{_kotakCari.Text.Trim()}”. "
                  + "Coba kata lain: t-test, anova, korelasi, regresi, cronbach, chi-square."
                : $"{cocok.Count} dari {_semua.Count} alat";
        }

        private void KotakCari_Tombol(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                int i = _daftar.SelectedIndex + 1;
                if (i < _daftar.Items.Count) _daftar.SelectedIndex = i;
                if (_daftar.SelectedItem is not null) _daftar.ScrollIntoView(_daftar.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                int i = _daftar.SelectedIndex - 1;
                if (i >= 0) _daftar.SelectedIndex = i;
                if (_daftar.SelectedItem is not null) _daftar.ScrollIntoView(_daftar.SelectedItem);
                e.Handled = true;
            }
        }

        private void Terima()
        {
            if (_daftar.SelectedItem is ItemAlat item && item.Spec != null)
            {
                Terpilih = item.Spec;
                DialogResult = true;
                Close();
                return;
            }

            if (_daftar.Items.Count == 1 && _daftar.Items[0] is ItemAlat tunggal && tunggal.Spec != null)
            {
                Terpilih = tunggal.Spec;
                DialogResult = true;
                Close();
            }
        }

        
        
        
        public static AnalysisSpec? Tanya(IEnumerable<ItemAlat> semua, Window pemilik)
        {
            var jendela = new PeluncurAlat(semua, pemilik);
            bool? hasil;

            try { hasil = jendela.ShowDialog(); }
            catch (Exception galat)
            {
                MessageBox.Show(galat.Message, "Pencarian alat gagal",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            return hasil == true ? jendela.Terpilih : null;
        }
    }
}
