using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AlatStatistik.Models;
using AlatStatistik.Statistics;

namespace AlatStatistik.DataPrep
{
    public partial class JendelaCustomTables : Window
    {
        private Dataset _data;
        private CustomTableSpec _spec;

        // Null sampai tombol OK ditekan; pemanggil memeriksa DialogResult dulu.
        public CustomTableSpec? ResultSpec { get; private set; }

        public JendelaCustomTables(Dataset data)
        {
            InitializeComponent();
            _data = data;
            _spec = new CustomTableSpec();
            _spec.TableNumber = "1";
            _spec.TableTitle = "Tabel Kustom";
            
            InitializeUI();
            LoadVariables();
        }

        private void InitializeUI()
        {
            // Setup row dimensions list
            listRowDimensions.ItemsSource = _spec.RowDimensions;
            
            // Setup column dimensions list
            listColumnDimensions.ItemsSource = _spec.ColumnDimensions;
            
            // Setup statistics list
            listStatistics.ItemsSource = Enum.GetValues(typeof(StatistikType)).Cast<StatistikType>();
            
            // Setup percent type combo
            comboPercentType.ItemsSource = Enum.GetValues(typeof(PercentType));
            comboPercentType.SelectedItem = PercentType.None;
            
            // Default statistics
            _spec.Statistics.Add(StatistikType.Count);
            _spec.Statistics.Add(StatistikType.Percent);
        }

        private void LoadVariables()
        {
            comboRowVariable.Items.Clear();
            comboColumnVariable.Items.Clear();
            
            foreach (var col in _data.Names)
            {
                comboRowVariable.Items.Add(col);
                comboColumnVariable.Items.Add(col);
            }
            
            if (comboRowVariable.Items.Count > 0)
                comboRowVariable.SelectedIndex = 0;
            
            if (comboColumnVariable.Items.Count > 0)
                comboColumnVariable.SelectedIndex = 0;
        }

        private void AddRowDimension_Click(object sender, RoutedEventArgs e)
        {
            if (comboRowVariable.SelectedItem == null) return;
            
            var variable = comboRowVariable.SelectedItem.ToString();
            if (string.IsNullOrEmpty(variable)) return;
            
            var dim = new DimensionSpec
            {
                Variable = variable,
                Label = variable,
                Order = _spec.RowDimensions.Count,
                ShowTotal = checkRowTotal.IsChecked ?? true,
                ShowSubtotals = checkRowSubtotal.IsChecked ?? false
            };
            
            _spec.RowDimensions.Add(dim);
            listRowDimensions.Items.Refresh();
        }

        private void RemoveRowDimension_Click(object sender, RoutedEventArgs e)
        {
            if (listRowDimensions.SelectedItem is DimensionSpec dim)
            {
                _spec.RowDimensions.Remove(dim);
                // Reorder
                for (int i = 0; i < _spec.RowDimensions.Count; i++)
                {
                    _spec.RowDimensions[i].Order = i;
                }
                listRowDimensions.Items.Refresh();
            }
        }

        private void AddColumnDimension_Click(object sender, RoutedEventArgs e)
        {
            if (comboColumnVariable.SelectedItem == null) return;
            
            var variable = comboColumnVariable.SelectedItem.ToString();
            if (string.IsNullOrEmpty(variable)) return;
            
            var dim = new DimensionSpec
            {
                Variable = variable,
                Label = variable,
                Order = _spec.ColumnDimensions.Count,
                ShowTotal = checkColumnTotal.IsChecked ?? true,
                ShowSubtotals = checkColumnSubtotal.IsChecked ?? false
            };
            
            _spec.ColumnDimensions.Add(dim);
            listColumnDimensions.Items.Refresh();
        }

        private void RemoveColumnDimension_Click(object sender, RoutedEventArgs e)
        {
            if (listColumnDimensions.SelectedItem is DimensionSpec dim)
            {
                _spec.ColumnDimensions.Remove(dim);
                // Reorder
                for (int i = 0; i < _spec.ColumnDimensions.Count; i++)
                {
                    _spec.ColumnDimensions[i].Order = i;
                }
                listColumnDimensions.Items.Refresh();
            }
        }

        private void AddStatistic_Click(object sender, RoutedEventArgs e)
        {
            if (listStatistics.SelectedItem is StatistikType stat)
            {
                if (!_spec.Statistics.Contains(stat))
                {
                    _spec.Statistics.Add(stat);
                    RefreshSelectedStatistics();
                }
            }
        }

        private void RemoveStatistic_Click(object sender, RoutedEventArgs e)
        {
            if (listSelectedStatistics.SelectedItem is StatistikType stat)
            {
                _spec.Statistics.Remove(stat);
                RefreshSelectedStatistics();
            }
        }

        private void RefreshSelectedStatistics()
        {
            listSelectedStatistics.ItemsSource = null;
            listSelectedStatistics.ItemsSource = _spec.Statistics;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            if (_spec.RowDimensions.Count == 0 && _spec.ColumnDimensions.Count == 0)
            {
                MessageBox.Show("Minimal satu dimensi (baris atau kolom) harus didefinisikan.", 
                              "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (_spec.Statistics.Count == 0)
            {
                MessageBox.Show("Minimal satu statistik harus dipilih.", 
                              "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Set final spec
            _spec.TableNumber = textTableNumber.Text;
            _spec.TableTitle = textTableTitle.Text;
            _spec.PercentType = (PercentType)(comboPercentType.SelectedItem ?? PercentType.None);
            _spec.ShowRowPercent = checkRowPercent.IsChecked ?? false;
            _spec.ShowColumnPercent = checkColumnPercent.IsChecked ?? false;
            _spec.ShowTotalPercent = checkTotalPercent.IsChecked ?? false;
            
            ResultSpec = _spec;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void MoveUpRow_Click(object sender, RoutedEventArgs e)
        {
            if (listRowDimensions.SelectedItem is DimensionSpec dim)
            {
                int index = _spec.RowDimensions.IndexOf(dim);
                if (index > 0)
                {
                    _spec.RowDimensions.RemoveAt(index);
                    _spec.RowDimensions.Insert(index - 1, dim);
                    // Reorder
                    for (int i = 0; i < _spec.RowDimensions.Count; i++)
                    {
                        _spec.RowDimensions[i].Order = i;
                    }
                    listRowDimensions.Items.Refresh();
                    listRowDimensions.SelectedIndex = index - 1;
                }
            }
        }

        private void MoveDownRow_Click(object sender, RoutedEventArgs e)
        {
            if (listRowDimensions.SelectedItem is DimensionSpec dim)
            {
                int index = _spec.RowDimensions.IndexOf(dim);
                if (index < _spec.RowDimensions.Count - 1)
                {
                    _spec.RowDimensions.RemoveAt(index);
                    _spec.RowDimensions.Insert(index + 1, dim);
                    // Reorder
                    for (int i = 0; i < _spec.RowDimensions.Count; i++)
                    {
                        _spec.RowDimensions[i].Order = i;
                    }
                    listRowDimensions.Items.Refresh();
                    listRowDimensions.SelectedIndex = index + 1;
                }
            }
        }

        private void MoveUpColumn_Click(object sender, RoutedEventArgs e)
        {
            if (listColumnDimensions.SelectedItem is DimensionSpec dim)
            {
                int index = _spec.ColumnDimensions.IndexOf(dim);
                if (index > 0)
                {
                    _spec.ColumnDimensions.RemoveAt(index);
                    _spec.ColumnDimensions.Insert(index - 1, dim);
                    // Reorder
                    for (int i = 0; i < _spec.ColumnDimensions.Count; i++)
                    {
                        _spec.ColumnDimensions[i].Order = i;
                    }
                    listColumnDimensions.Items.Refresh();
                    listColumnDimensions.SelectedIndex = index - 1;
                }
            }
        }

        private void MoveDownColumn_Click(object sender, RoutedEventArgs e)
        {
            if (listColumnDimensions.SelectedItem is DimensionSpec dim)
            {
                int index = _spec.ColumnDimensions.IndexOf(dim);
                if (index < _spec.ColumnDimensions.Count - 1)
                {
                    _spec.ColumnDimensions.RemoveAt(index);
                    _spec.ColumnDimensions.Insert(index + 1, dim);
                    // Reorder
                    for (int i = 0; i < _spec.ColumnDimensions.Count; i++)
                    {
                        _spec.ColumnDimensions[i].Order = i;
                    }
                    listColumnDimensions.Items.Refresh();
                    listColumnDimensions.SelectedIndex = index + 1;
                }
            }
        }
    }
}