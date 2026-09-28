using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Data;
using AlatStatistik.Models;

namespace AlatStatistik.Views
{

    public class PenukarLabelNilai : IValueConverter
    {

        public static bool Aktif { get; set; }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (!Aktif || value is null) return value;
            if (parameter is not Variable v) return value;

            string teks = value switch
            {
                string s => s,
                double d => d.ToString(CultureInfo.InvariantCulture),
                _ => value.ToString() ?? ""
            };
            if (string.IsNullOrEmpty(teks)) return value;

            
            if (double.TryParse(teks, NumberStyles.Float, CultureInfo.InvariantCulture, out double angka)
                && v.ValueLabels.TryGetValue(angka, out string? labelNum))
                return labelNum;

            
            if (v.ValueLabelsTeks.TryGetValue(teks, out string? labelTeks))
                return labelTeks;

            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (!Aktif || value is null) return value;
            if (parameter is not Variable v) return value;

            string teks = value as string ?? value.ToString() ?? "";
            if (string.IsNullOrEmpty(teks)) return value;

            
            foreach (var kv in v.ValueLabels)
                if (string.Equals(kv.Value, teks, StringComparison.Ordinal))
                    return kv.Key.ToString(CultureInfo.InvariantCulture);
            foreach (var kv in v.ValueLabelsTeks)
                if (string.Equals(kv.Value, teks, StringComparison.Ordinal))
                    return kv.Key;

            
            return value;
        }
    }

    public class RingkasanLabelNilai : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not Variable v) return "";
            if (v.ValueLabels.Count == 0 && v.ValueLabelsTeks.Count == 0) return "";

            var sb = new StringBuilder();
            foreach (var kv in v.ValueLabels.OrderBy(k => k.Key))
                sb.Append($"{kv.Key.ToString(CultureInfo.InvariantCulture)} = {kv.Value}; ");
            foreach (var kv in v.ValueLabelsTeks.OrderBy(k => k.Key, StringComparer.Ordinal))
                sb.Append($"{kv.Key} = {kv.Value}; ");

            string hasil = sb.ToString().TrimEnd(' ', ';');
            return hasil;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => null;
    }
}
