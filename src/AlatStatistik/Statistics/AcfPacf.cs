using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class AcfPacf
    {
        public sealed class BarisAc
        {
            public int Lag;
            public double Acf;
            public double Pacf;
            public double Batas;
            public bool KeluarBatas;
        }

        public sealed class HasilLjung
        {
            public int Lag;
            public double Stat;
            public double P;
        }

        public sealed class Hasil
        {
            public int N;
            public int NLags;
            public double Batas;
            public List<BarisAc> Baris = new();
            public List<HasilLjung> LjungBox = new();
        }

        

        public static double[] Autocorr(double[] x, int nlags)
        {
            int n = x.Length;
            double mean = x.Average();
            double denom = 0;
            for (int i = 0; i < n; i++) denom += (x[i] - mean) * (x[i] - mean);

            var r = new double[nlags + 1];
            r[0] = 1.0;
            if (denom <= 0) return r;

            for (int k = 1; k <= nlags; k++)
            {
                double num = 0;
                for (int i = k; i < n; i++) num += (x[i] - mean) * (x[i - k] - mean);
                r[k] = num / denom;
            }
            return r;
        }

        public static double[] Pacf(double[] r, int nlags)
        {
            var p = new double[nlags + 1];
            p[0] = 1.0;
            if (nlags < 1) return p;

            
            var phi = new double[nlags + 1][];
            for (int k = 0; k <= nlags; k++) phi[k] = new double[nlags + 2];

            phi[1][1] = r[1];
            p[1] = r[1];

            for (int k = 2; k <= nlags; k++)
            {
                double num = r[k], den = 1.0;
                for (int j = 1; j <= k - 1; j++)
                {
                    num -= phi[k - 1][j] * r[k - j];
                    den -= phi[k - 1][j] * r[j];
                }
                phi[k][k] = Math.Abs(den) > 1e-15 ? num / den : 0.0;
                for (int j = 1; j <= k - 1; j++)
                    phi[k][j] = phi[k - 1][j] - phi[k][k] * phi[k - 1][k - j];
                p[k] = phi[k][k];
            }
            return p;
        }

        public static (double stat, double p) LjungBox(double[] r, int n, int m)
        {
            double q = 0;
            for (int k = 1; k <= m; k++)
                if (n - k > 0) q += r[k] * r[k] / (n - k);
            q = n * (n + 2) * q;
            double p = q > 0 ? Distributions.ChiSquareUpper(q, m) : 1.0;
            return (q, p);
        }

        public static Hasil Compute(Dataset ds, string var, int nlags = 12)
        {
            var x = Descriptives.CleanNumbers(ds, var);
            int n = x.Count;
            nlags = Math.Max(1, Math.Min(nlags, n - 2));

            var r = Autocorr(x.ToArray(), nlags);
            var p = Pacf(r, nlags);
            double batas = 1.96 / Math.Sqrt(n);

            var h = new Hasil { N = n, NLags = nlags, Batas = batas };
            for (int k = 1; k <= nlags; k++)
                h.Baris.Add(new BarisAc
                {
                    Lag = k,
                    Acf = r[k],
                    Pacf = p[k],
                    Batas = batas,
                    KeluarBatas = Math.Abs(r[k]) > batas,
                });

            foreach (int m in new[] { 1, 5, 10 })
            {
                if (m > nlags) continue;
                var (stat, pv) = LjungBox(r, n, m);
                h.LjungBox.Add(new HasilLjung { Lag = m, Stat = stat, P = pv });
            }
            return h;
        }

        

        public static List<ResultBlock> Hitung(Dataset ds, string var, int nlags = 12)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading($"ACF & PACF — {var}", 1) };

            var x = Descriptives.CleanNumbers(ds, var);
            if (x.Count < 3)
            {
                blocks.Add(Blocks.Note("Butuh sedikitnya 3 amatan berurutan.", NoteKind.Error));
                return blocks;
            }

            var h = Compute(ds, var, nlags);

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.Acf, DaftarRumus.Pacf, DaftarRumus.LjungBox));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Variabel", var),
                ("Banyak amatan", $"n = {Fmt.Int(h.N)}"),
                ("Lag maksimum", $"m = {Fmt.Int(h.NLags)}"),
                ("Batas galat baku", $"± 1,96/√n = ±1,96/√{Fmt.Int(h.N)} = ±{Fmt.Num(h.Batas, 4)}")));

            blocks.Add(Blocks.Table(
                "Autokorelasi (ACF) dan autokorelasi parsial (PACF)",
                new[] { "Lag", "ACF", "PACF", "Batas ±", "Keluar batas" },
                h.Baris.Select(b => new[]
                {
                    Fmt.Int(b.Lag),
                    Fmt.Num(b.Acf, 4),
                    Fmt.Num(b.Pacf, 4),
                    Fmt.Num(b.Batas, 4),
                    b.KeluarBatas ? "ya" : "tidak",
                }).ToArray(),
                "ACF yang meluruh perlahan menandakan deret tidak stasioner; "
                + "PACF yang terpotong tajam di lag p menandakan kandidat AR(p)."));

            blocks.Add(Blocks.Table(
                "Uji Ljung–Box (deret putih)",
                new[] { "Lag", "Q", "df", "p" },
                h.LjungBox.Select(b => new[]
                {
                    Fmt.Int(b.Lag), Fmt.Num(b.Stat, 4), Fmt.Int(b.Lag), Fmt.P(b.P),
                }).ToArray(),
                "H0: tidak ada autokorelasi sampai lag m. "
                + (h.LjungBox.Count > 0 && h.LjungBox.All(b => b.P >= 0.05)
                    ? "Tidak ada bukti autokorelasi (semua p ≥ 0,05)."
                    : "Ada autokorelasi yang nyata — deretnya bukan deret putih.")));

            
            
            var spec = new ChartSpec
            {
                Kind = ChartKind.Bar,
                XTitle = "Lag",
                YTitle = "ACF",
                TotalN = h.N,
            };
            foreach (var b in h.Baris) spec.Bars.Add((b.Lag.ToString(), b.Acf));
            blocks.Add(Blocks.Chart($"Korelogram ACF — {var}", spec));

            return blocks;
        }
    }
}
