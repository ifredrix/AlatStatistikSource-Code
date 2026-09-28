using System;
using System.Data;
using System.Globalization;
using System.Windows.Data;

namespace AlatStatistik.Views
{
    public class PenukarNomorBaris : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DataRowView rowView && rowView.DataView?.Table != null)
                return rowView.DataView.Table.Rows.IndexOf(rowView.Row) + 1;
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => null;
    }
}
