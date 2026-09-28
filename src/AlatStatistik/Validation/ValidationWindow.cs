using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AlatStatistik.Validation
{

    public class ValidationWindow : Window
    {
        public ValidationWindow(List<HasilUji> hasil)
        {
            Title = "Uji validasi — AlatStatistik vs SciPy";
            Width = 860;
            Height = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.Resources["Latar"];

            int lulus = hasil.Count(h => h.Lulus);
            int gagal = hasil.Count - lulus;

            var ringkasan = new TextBlock
            {
                Text = hasil.Count == 0
                    ? "Tidak ada uji yang dijalankan. Pastikan docs/acuan_scipy.json ada."
                    : $"{hasil.Count} uji · lulus {lulus} · menyimpang {gagal}"
                      + (gagal == 0 ? "  —  semua angka sama dengan SciPy."
                                    : "  —  periksa baris yang menyimpang."),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources[gagal == 0 ? "Sukses" : "Peringatan"],
                Margin = new Thickness(16, 14, 16, 10)
            };

            var keterangan = new TextBlock
            {
                Text = "Toleransi: selisih relatif di bawah 1e-8. "
                     + "Uji berbasis data memakai samples/data_uji.csv; "
                     + "uji fungsi (distribusi) dihitung langsung dari argumennya.",
                FontSize = 11.5,
                Foreground = (Brush)Application.Current.Resources["TeksRedup"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16, 0, 16, 12)
            };

            var grid = new DataGrid
            {
                ItemsSource = hasil,
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Background = (Brush)Application.Current.Resources["Panel"],
                Foreground = (Brush)Application.Current.Resources["Teks"],
                BorderBrush = (Brush)Application.Current.Resources["Garis"],
                RowBackground = (Brush)Application.Current.Resources["Panel"],
                AlternatingRowBackground = (Brush)Application.Current.Resources["Panel2"],
                Margin = new Thickness(16, 0, 16, 8),
                HeadersVisibility = DataGridHeadersVisibility.Column
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Uji",
                Binding = new System.Windows.Data.Binding(nameof(HasilUji.Nama)),
                Width = 300
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "AlatStatistik",
                Binding = new System.Windows.Data.Binding(nameof(HasilUji.Kita)) { StringFormat = "0.##########" },
                Width = 160
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "SciPy",
                Binding = new System.Windows.Data.Binding(nameof(HasilUji.Acuan)) { StringFormat = "0.##########" },
                Width = 160
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Selisih",
                Binding = new System.Windows.Data.Binding(nameof(HasilUji.Selisih)) { StringFormat = "0.00E+0" },
                Width = 110
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Status",
                Binding = new System.Windows.Data.Binding(nameof(HasilUji.Keterangan)),
                Width = 100
            });

            var bingkai = new Border
            {
                Background = (Brush)Application.Current.Resources["Panel"],
                BorderBrush = (Brush)Application.Current.Resources["Garis"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(16, 0, 16, 16),
                Child = grid
            };

            Content = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
                },
                Children = { ringkasan, keterangan, bingkai }
            };

            Grid.SetRow(ringkasan, 0);
            Grid.SetRow(keterangan, 1);
            Grid.SetRow(bingkai, 2);
        }
    }
}
