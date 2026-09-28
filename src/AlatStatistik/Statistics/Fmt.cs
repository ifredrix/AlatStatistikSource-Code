using System;
using System.Globalization;

namespace AlatStatistik.Statistics
{

    public static class Fmt
    {
        public const string NA = "—";

        public static string Num(double value, int decimals = 2)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return NA;

            string pattern = decimals > 0 ? "0." + new string('0', decimals) : "0";
            string s = value.ToString(pattern, CultureInfo.InvariantCulture);

            
            string[] parts = s.Split('.');
            parts[0] = GroupThousands(parts[0]);
            return parts.Length > 1 ? parts[0] + "," + parts[1] : parts[0];
        }

        public static string Int(int value) => Num(value, 0);

        public static string P(double p)
        {
            if (double.IsNaN(p) || double.IsInfinity(p)) return NA;
            return p < 0.001 ? "< 0,001" : Num(p, 3);
        }

        public static string Percent(double value) => Num(value, 1) + "%";

        public static string Raw(double value)
            => double.IsNaN(value) ? "" : value.ToString("G12", CultureInfo.InvariantCulture);

        private static string GroupThousands(string digits)
        {
            bool negative = digits.StartsWith("-");
            if (negative) digits = digits.Substring(1);

            var result = "";
            int count = 0;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                result = digits[i] + result;
                if (++count % 3 == 0 && i > 0) result = "." + result;
            }
            return (negative ? "-" : "") + result;
        }
    }
}
