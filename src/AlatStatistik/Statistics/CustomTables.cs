using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public enum StatistikType
    {
        Count,          // Jumlah kasus
        Mean,           // Rerata
        Median,         // Median
        Sum,            // Jumlah
        Min,            // Minimum
        Max,            // Maksimum
        StdDev,         // Simpangan baku
        Variance,       // Ragam
        Percent,        // Persentase
        ValidPercent,   // Persentase valid (tanpa missing)
        CumulativePercent // Persentase kumulatif
    }

    public enum PercentType
    {
        None,           // Tidak ada persentase
        Row,            // Persentase per baris
        Column,         // Persentase per kolom
        Total           // Persentase per total
    }

    public class DimensionSpec
    {
        public string Variable { get; set; } = "";
        public string Label { get; set; } = "";
        public bool ShowTotal { get; set; } = true;
        public bool ShowSubtotals { get; set; } = false;
        public int Order { get; set; } = 0; // Urutan layer (0 = terluar)
    }

    public class CustomTableSpec
    {
        public List<DimensionSpec> RowDimensions { get; set; } = new();
        public List<DimensionSpec> ColumnDimensions { get; set; } = new();
        public List<StatistikType> Statistics { get; set; } = new();
        public PercentType PercentType { get; set; } = PercentType.None;
        public string TableNumber { get; set; } = "";
        public string TableTitle { get; set; } = "";
        public bool ShowRowPercent { get; set; } = false;
        public bool ShowColumnPercent { get; set; } = false;
        public bool ShowTotalPercent { get; set; } = false;
    }

    public class TableCell
    {
        public string RowLabel { get; set; } = "";
        public string ColumnLabel { get; set; } = "";
        public Dictionary<StatistikType, double> Values { get; set; } = new();
        public int Count { get; set; } = 0;
        public bool IsTotal { get; set; } = false;
        public bool IsSubtotal { get; set; } = false;
        public int RowSpan { get; set; } = 1;
        public int ColSpan { get; set; } = 1;
    }

    public class CustomTableResult
    {
        public string TableNumber { get; set; } = "";
        public string TableTitle { get; set; } = "";
        public List<string> RowHeaders { get; set; } = new();
        public List<string> ColumnHeaders { get; set; } = new();
        public List<TableCell> Cells { get; set; } = new();
        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
        public List<StatistikType> DisplayStatistics { get; set; } = new();
    }

    public static class CustomTables
    {
        public static CustomTableResult BuatTabel(Dataset data, CustomTableSpec spec)
        {
            var result = new CustomTableResult
            {
                TableNumber = spec.TableNumber,
                TableTitle = spec.TableTitle,
                DisplayStatistics = spec.Statistics
            };

            // Validasi input
            if (spec.RowDimensions.Count == 0 && spec.ColumnDimensions.Count == 0)
            {
                throw new ArgumentException("Minimal satu dimensi (baris atau kolom) harus didefinisikan");
            }

            // Jika tidak ada dimensi baris, gunakan satu baris "Total"
            if (spec.RowDimensions.Count == 0)
            {
                spec.RowDimensions.Add(new DimensionSpec { Variable = "__total__", Label = "Total" });
            }

            // Jika tidak ada dimensi kolom, gunakan satu kolom "Total"
            if (spec.ColumnDimensions.Count == 0)
            {
                spec.ColumnDimensions.Add(new DimensionSpec { Variable = "__total__", Label = "Total" });
            }

            // Ambil data untuk variabel yang diperlukan
            var rowData = AmbilDataDimensi(data, spec.RowDimensions);
            var colData = AmbilDataDimensi(data, spec.ColumnDimensions);

            // Ambil data numerik untuk statistik
            var numericVars = spec.Statistics.Where(s => s != StatistikType.Count && 
                                                       s != StatistikType.Percent && 
                                                       s != StatistikType.ValidPercent &&
                                                       s != StatistikType.CumulativePercent)
                                              .ToList();
            
            Dictionary<string, List<double>> numericData = new();
            if (numericVars.Any())
            {
                // Untuk sementara, gunakan variabel pertama yang numerik
                var firstNumeric = data.Variables.FirstOrDefault(v => v.IsNumeric)?.Name;
                if (!string.IsNullOrEmpty(firstNumeric))
                {
                    numericData[firstNumeric] = data.Numeric(firstNumeric)
                                                    .Where(x => x.HasValue)
                                                    .Select(x => x!.Value)
                                                    .ToList();
                }
            }

            // Generate struktur tabel
            var (rowKeys, colKeys) = GenerateKeyCombinations(rowData, colData);
            
            // Hitung statistik per cell
            var cells = HitungCells(data, rowData, colData, rowKeys, colKeys, spec, numericData);

            result.Cells = cells;
            result.RowCount = rowKeys.Count;
            result.ColumnCount = colKeys.Count;
            result.RowHeaders = GenerateHeaders(rowData, spec.RowDimensions);
            result.ColumnHeaders = GenerateHeaders(colData, spec.ColumnDimensions);

            return result;
        }

        private static Dictionary<string, List<string>> AmbilDataDimensi(Dataset data, List<DimensionSpec> dimensions)
        {
            var result = new Dictionary<string, List<string>>();
            
            foreach (var dim in dimensions)
            {
                if (dim.Variable == "__total__")
                {
                    result[dim.Variable] = Enumerable.Repeat("Total", data.RowCount).ToList();
                }
                else
                {
                    int idx = data.IndexOf(dim.Variable);
                    if (idx >= 0)
                    {
                        result[dim.Variable] = data.Text(dim.Variable)
                                                   .Select(x => x ?? "")
                                                   .ToList();
                    }
                    else
                    {
                        result[dim.Variable] = Enumerable.Repeat("", data.RowCount).ToList();
                    }
                }
            }
            
            return result;
        }

        private static (List<List<string>>, List<List<string>>) GenerateKeyCombinations(
            Dictionary<string, List<string>> rowData,
            Dictionary<string, List<string>> colData)
        {
            var rowKeys = new List<List<string>>();
            var colKeys = new List<List<string>>();

            int rowCount = rowData.First().Value.Count;
            int colCount = colData.First().Value.Count;

            // Generate row keys
            for (int i = 0; i < rowCount; i++)
            {
                var key = rowData.OrderBy(k => k.Key).Select(k => k.Value[i]).ToList();
                rowKeys.Add(key);
            }

            // Generate column keys
            for (int i = 0; i < colCount; i++)
            {
                var key = colData.OrderBy(k => k.Key).Select(k => k.Value[i]).ToList();
                colKeys.Add(key);
            }

            // Get unique combinations
            var uniqueRowKeys = rowKeys.Distinct(new ListComparer()).ToList();
            var uniqueColKeys = colKeys.Distinct(new ListComparer()).ToList();

            return (uniqueRowKeys, uniqueColKeys);
        }

        private static List<TableCell> HitungCells(
            Dataset data,
            Dictionary<string, List<string>> rowData,
            Dictionary<string, List<string>> colData,
            List<List<string>> rowKeys,
            List<List<string>> colKeys,
            CustomTableSpec spec,
            Dictionary<string, List<double>> numericData)
        {
            var cells = new List<TableCell>();
            int rowCount = rowData.First().Value.Count;

            // Get numeric data if available
            List<double>? numericValues = null;
            string? numericVar = null;
            if (numericData.Any())
            {
                numericVar = numericData.First().Key;
                numericValues = numericData.First().Value;
            }

            foreach (var rowKey in rowKeys)
            {
                foreach (var colKey in colKeys)
                {
                    var cell = new TableCell
                    {
                        RowLabel = string.Join(" × ", rowKey),
                        ColumnLabel = string.Join(" × ", colKey)
                    };

                    // Find indices that match this combination
                    var matchingIndices = new List<int>();
                    for (int i = 0; i < rowCount; i++)
                    {
                        bool rowMatch = true;
                        bool colMatch = true;

                        for (int d = 0; d < rowData.Count; d++)
                        {
                            var varName = rowData.ElementAt(d).Key;
                            if (rowData[varName][i] != rowKey[d])
                            {
                                rowMatch = false;
                                break;
                            }
                        }

                        for (int d = 0; d < colData.Count; d++)
                        {
                            var varName = colData.ElementAt(d).Key;
                            if (colData[varName][i] != colKey[d])
                            {
                                colMatch = false;
                                break;
                            }
                        }

                        if (rowMatch && colMatch)
                        {
                            matchingIndices.Add(i);
                        }
                    }

                    cell.Count = matchingIndices.Count;

                    // Calculate statistics
                    if (numericValues != null && matchingIndices.Count > 0)
                    {
                        var values = matchingIndices.Select(i => numericValues[i]).Where(v => !double.IsNaN(v)).ToList();

                        foreach (var stat in spec.Statistics)
                        {
                            switch (stat)
                            {
                                case StatistikType.Count:
                                    cell.Values[stat] = matchingIndices.Count;
                                    break;
                                case StatistikType.Mean:
                                    cell.Values[stat] = values.Average();
                                    break;
                                case StatistikType.Median:
                                    cell.Values[stat] = Median(values);
                                    break;
                                case StatistikType.Sum:
                                    cell.Values[stat] = values.Sum();
                                    break;
                                case StatistikType.Min:
                                    cell.Values[stat] = values.Min();
                                    break;
                                case StatistikType.Max:
                                    cell.Values[stat] = values.Max();
                                    break;
                                case StatistikType.StdDev:
                                    cell.Values[stat] = StdDev(values);
                                    break;
                                case StatistikType.Variance:
                                    cell.Values[stat] = Variance(values);
                                    break;
                                case StatistikType.Percent:
                                case StatistikType.ValidPercent:
                                case StatistikType.CumulativePercent:
                                    // Calculated later
                                    break;
                            }
                        }
                    }
                    else if (spec.Statistics.Contains(StatistikType.Count))
                    {
                        cell.Values[StatistikType.Count] = matchingIndices.Count;
                    }

                    cells.Add(cell);
                }
            }

            // Calculate percentages
            if (spec.Statistics.Any(s => s == StatistikType.Percent || 
                                       s == StatistikType.ValidPercent ||
                                       s == StatistikType.CumulativePercent))
            {
                HitungPersentase(cells, spec);
            }

            return cells;
        }

        private static void HitungPersentase(List<TableCell> cells, CustomTableSpec spec)
        {
            double total = cells.Sum(c => c.Count);
            if (total == 0) return;

            // Row percentages
            if (spec.ShowRowPercent || spec.PercentType == PercentType.Row)
            {
                var rowGroups = cells.GroupBy(c => c.RowLabel);
                foreach (var group in rowGroups)
                {
                    double rowTotal = group.Sum(c => c.Count);
                    if (rowTotal > 0)
                    {
                        foreach (var cell in group)
                        {
                            cell.Values[StatistikType.Percent] = (cell.Count / rowTotal) * 100;
                        }
                    }
                }
            }

            // Column percentages
            if (spec.ShowColumnPercent || spec.PercentType == PercentType.Column)
            {
                var colGroups = cells.GroupBy(c => c.ColumnLabel);
                foreach (var group in colGroups)
                {
                    double colTotal = group.Sum(c => c.Count);
                    if (colTotal > 0)
                    {
                        foreach (var cell in group)
                        {
                            cell.Values[StatistikType.ValidPercent] = (cell.Count / colTotal) * 100;
                        }
                    }
                }
            }

            // Total percentages
            if (spec.ShowTotalPercent || spec.PercentType == PercentType.Total)
            {
                foreach (var cell in cells)
                {
                    cell.Values[StatistikType.CumulativePercent] = (cell.Count / total) * 100;
                }
            }

            // Tak satu pun jenis persentase dipilih — ini justru keadaan
            // bawaannya (PercentType = None, ketiga penanda = false), dan
            // dipakai alat `custom_tables` dari registri. Bila Percent ikut
            if (spec.PercentType == PercentType.None && !spec.ShowRowPercent
                && !spec.ShowColumnPercent && !spec.ShowTotalPercent)
            {
                foreach (var cell in cells)
                {
                    cell.Values[StatistikType.Percent] = (cell.Count / total) * 100;
                }
            }
        }

        private static List<string> GenerateHeaders(Dictionary<string, List<string>> data, List<DimensionSpec> dimensions)
        {
            var headers = new List<string>();
            
            foreach (var dim in dimensions.OrderBy(d => d.Order))
            {
                if (dim.Variable != "__total__")
                {
                    var uniqueValues = data[dim.Variable].Distinct().OrderBy(v => v).ToList();
                    headers.AddRange(uniqueValues);
                    
                    if (dim.ShowSubtotals)
                    {
                        headers.Add("Subtotal");
                    }
                }
                else
                {
                    headers.Add("Total");
                }
            }
            
            return headers;
        }

        private static double Median(List<double> values)
        {
            if (values.Count == 0) return double.NaN;
            
            var sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            
            if (n % 2 == 0)
            {
                return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
            }
            else
            {
                return sorted[n / 2];
            }
        }

        private static double StdDev(List<double> values)
        {
            if (values.Count < 2) return 0;
            double mean = values.Average();
            double sumSqDiff = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSqDiff / (values.Count - 1));
        }

        private static double Variance(List<double> values)
        {
            double std = StdDev(values);
            return std * std;
        }

        public static List<ResultBlock> CustomTableBlocks(Dataset data, CustomTableSpec spec)
        {
            try
            {
                var result = BuatTabel(data, spec);
                var blocks = new List<ResultBlock>();

                blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.TabelKustom));

                int nTotal = result.Cells.Sum(c => c.Count);
                var contoh = result.Cells.FirstOrDefault(c => c.Count > 0);
                var langkah = new List<(string, string)>
                {
                    ("Dimensi baris", string.Join(", ", spec.RowDimensions.Select(d => d.Variable))),
                    ("Dimensi kolom", string.Join(", ", spec.ColumnDimensions.Select(d => d.Variable))),
                    ("Sel terisi", $"{result.Cells.Count(c => c.Count > 0)} dari {result.Cells.Count}"),
                    ("Banyak amatan (N)", Fmt.Int(nTotal))
                };
                if (contoh is not null)
                {
                    double persen = nTotal > 0 ? contoh.Count * 100.0 / nTotal : 0.0;
                    langkah.Add(($"Contoh sel — baris '{contoh.RowLabel}'",
                                 $"n = {Fmt.Int(contoh.Count)},  % = {Fmt.Int(contoh.Count)} / "
                                 + $"{Fmt.Int(nTotal)} × 100% = {Fmt.Num(persen, 2)}%"));
                }
                blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data", langkah.ToArray()));

                blocks.AddRange(FormatResult(result));
                return blocks;
            }
            catch (Exception ex)
            {
                return new List<ResultBlock> { Blocks.Note($"Gagal membuat Custom Table: {ex.Message}", NoteKind.Error) };
            }
        }

        private static List<ResultBlock> FormatResult(CustomTableResult result)
        {
            var blocks = new List<ResultBlock>();

            // Table title
            if (!string.IsNullOrEmpty(result.TableNumber))
            {
                blocks.Add(Blocks.Heading($"Tabel {result.TableNumber}: {result.TableTitle}"));
            }
            else if (!string.IsNullOrEmpty(result.TableTitle))
            {
                blocks.Add(Blocks.Heading(result.TableTitle));
            }

            // Build table
            var baris = new List<List<string>>();
            
            // Column headers
            var headerRow = new List<string> { "" };
            headerRow.AddRange(result.ColumnHeaders);
            // Data rows. `Cells` datang berurutan rowKeys × colKeys, jadi sel
            // (i, j) ada di indeks i × ColumnCount + j. Dulu tiap sel dicetak
            // sebagai satu baris sendiri — akibatnya label baris mengulang
            for (int i = 0; i < result.RowCount; i++)
            {
                var row = new List<string>
                {
                    i < result.RowHeaders.Count ? result.RowHeaders[i] : ""
                };
                
                for (int j = 0; j < result.ColumnCount; j++)
                {
                    int idx = i * result.ColumnCount + j;
                    if (idx >= result.Cells.Count) { row.Add(""); continue; }

                    var sel = result.Cells[idx];
                    var bagian = new List<string>();
                    foreach (var stat in result.DisplayStatistics)
                    {
                        if (sel.Values.ContainsKey(stat))
                            bagian.Add(FormatValue(sel.Values[stat], stat));
                    }
                    row.Add(string.Join("  ", bagian));
                }
                
                baris.Add(row);
            }

            blocks.Add(Blocks.Table(result.TableTitle, headerRow, baris));

            // Summary
            blocks.Add(Blocks.Note($"Total {result.RowCount} baris × {result.ColumnCount} kolom", NoteKind.Info));

            return blocks;
        }

        private static string FormatValue(double value, StatistikType type)
        {
            if (double.IsNaN(value)) return "-";
            
            switch (type)
            {
                case StatistikType.Count:
                case StatistikType.Sum:
                case StatistikType.Min:
                case StatistikType.Max:
                    return value.ToString("N0");
                case StatistikType.Mean:
                case StatistikType.Median:
                case StatistikType.StdDev:
                case StatistikType.Variance:
                    return value.ToString("N2");
                case StatistikType.Percent:
                case StatistikType.ValidPercent:
                case StatistikType.CumulativePercent:
                    return value.ToString("N1") + "%";
                default:
                    return value.ToString("N2");
            }
        }

        private class ListComparer : IEqualityComparer<List<string>>
        {
            public bool Equals(List<string>? x, List<string>? y)
            {
                if (x == null || y == null) return false;
                if (x.Count != y.Count) return false;
                return x.SequenceEqual(y);
            }

            public int GetHashCode(List<string> obj)
            {
                return string.Join("|", obj).GetHashCode();
            }
        }
    }
}