using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class Musiman
    {
        public sealed class HasilDekomposisi
        {
            public int N, Periode;
            public double[] Tren = Array.Empty<double>();
            public double[] MusimanArr = Array.Empty<double>();
            public double[] Residu = Array.Empty<double>();
        }

        public sealed class HasilHw
        {
            public int N, Periode, Horizon;
            public double Alpha, Beta, Gamma;
            public double AwalLevel, AwalTren;
            public double[] AwalMusim = Array.Empty<double>();
            public double[] Pas = Array.Empty<double>();
            public double[] Ramal = Array.Empty<double>();
            public double Sse;
        }

        public static HasilDekomposisi? Dekomposisi(double[] y, int m)
        {
            int n = y.Length;
            if (m < 2 || n <= m) return null;
            if (y.Any(double.IsNaN)) return null;

            var tren = new double[n];
            for (int i = 0; i < n; i++) tren[i] = double.NaN;
            if (m % 2 == 0)
            {
                int h = m / 2;
                for (int i = h; i < n - h; i++)
                {
                    double s = 0.5 * y[i - h] + 0.5 * y[i + h];
                    for (int j = i - h + 1; j <= i + h - 1; j++) s += y[j];
                    tren[i] = s / m;
                }
            }
            else
            {
                int h = m / 2;
                for (int i = h; i < n - h; i++)
                {
                    double s = 0;
                    for (int j = i - h; j <= i + h; j++) s += y[j];
                    tren[i] = s / m;
                }
            }

            var mus = new double[m];
            var cacah = new int[m];
            for (int i = 0; i < n; i++)
            {
                if (double.IsNaN(tren[i])) continue;
                mus[i % m] += y[i] - tren[i];
                cacah[i % m]++;
            }
            for (int j = 0; j < m; j++)
                mus[j] = cacah[j] > 0 ? mus[j] / cacah[j] : 0;
            double rataMus = mus.Average();
            for (int j = 0; j < m; j++) mus[j] -= rataMus;

            var tiled = new double[n];
            var residu = new double[n];
            for (int i = 0; i < n; i++)
            {
                tiled[i] = mus[i % m];
                residu[i] = double.IsNaN(tren[i]) ? double.NaN : y[i] - tren[i] - tiled[i];
            }

            return new HasilDekomposisi
            {
                N = n, Periode = m, Tren = tren, MusimanArr = tiled, Residu = residu,
            };
        }

        public static HasilHw? HoltWinters(double[] y, int m, double alpha,
                                           double beta, double gamma, int horizon)
        {
            int n = y.Length;
            if (m < 2 || n < 2 * m) return null;
            if (y.Any(double.IsNaN)) return null;
            if (alpha <= 0 || alpha > 1 || beta < 0 || beta > 1
                || gamma < 0 || gamma > 1 || horizon < 1) return null;

            double l0 = 0;
            for (int j = 0; j < m; j++) l0 += y[j];
            l0 /= m;
            var musim = new double[m];
            for (int j = 0; j < m; j++) musim[j] = y[j] - l0;
            double l1 = 0;
            for (int j = m; j < 2 * m; j++) l1 += y[j];
            double b0 = (l1 / m - l0) / m;

            double l = l0, b = b0;
            var pas = new double[n];
            for (int i = 0; i < n; i++)
            {
                int j = i % m;
                pas[i] = l + b + musim[j];
                double lBaru = alpha * (y[i] - musim[j]) + (1 - alpha) * (l + b);
                double bBaru = beta * (lBaru - l) + (1 - beta) * b;
                musim[j] = gamma * (y[i] - l - b) + (1 - gamma) * musim[j];
                l = lBaru;
                b = bBaru;
            }

            double sse = 0;
            for (int i = 0; i < n; i++) sse += (y[i] - pas[i]) * (y[i] - pas[i]);

            var ramal = new double[horizon];
            for (int h = 1; h <= horizon; h++)
                ramal[h - 1] = l + h * b + musim[(n + h - 1) % m];

            var awalMusim = new double[m];
            for (int j = 0; j < m; j++) awalMusim[j] = y[j] - l0;
            return new HasilHw
            {
                N = n, Periode = m, Horizon = horizon,
                Alpha = alpha, Beta = beta, Gamma = gamma,
                AwalLevel = l0, AwalTren = b0, AwalMusim = awalMusim,
                Pas = pas, Ramal = ramal, Sse = sse,
            };
        }

        public static List<ResultBlock> MusimanBlocks(Dataset ds, string variabel,
            string metode, int periode, double alpha, double beta, double gamma,
            int horizon, double alphaUji = 0.05)
        {
            bool hw = metode.StartsWith("Holt", StringComparison.OrdinalIgnoreCase);
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading(hw ? "Pemulusan Holt-Winters aditif"
                                  : "Dekomposisi musiman aditif", 1)
            };

            var perlu = new List<string> { variabel };
            var baris = ds.CompleteRows(perlu);
            var semua = ds.Numeric(variabel);
            var y = new double[baris.Count];
            for (int i = 0; i < baris.Count; i++) y[i] = semua[baris[i]] ?? double.NaN;
            if (y.Length < periode + 1 || y.Any(double.IsNaN))
            {
                blocks.Add(Blocks.Note(
                    "Data kurang atau punya nilai hilang. Deret waktu butuh "
                    + $"minimal {periode + 1} amatan lengkap.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                hw ? DaftarRumus.HoltWinters : DaftarRumus.DekomposisiMusiman));

            if (!hw)
            {
                var h = Dekomposisi(y, periode);
                if (h is null)
                {
                    blocks.Add(Blocks.Note("Dekomposisi tidak bisa dijalankan.",
                                           NoteKind.Error));
                    return blocks;
                }
                blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                    ("Variabel", variabel),
                    ("Periode musiman", $"m = {Fmt.Int(periode)}"),
                    ("Banyak amatan", $"n = {Fmt.Int(h.N)}")));
                var idx = Enumerable.Range(0, h.N).ToList();
                blocks.Add(Blocks.Table(
                    "Dekomposisi (y = tren + musiman + residu)",
                    new[] { "Waktu", "Y", "Tren", "Musiman", "Residu" },
                    idx.Select(i => new[]
                    {
                        Fmt.Int(i + 1), Fmt.Num(y[i], 4),
                        double.IsNaN(h.Tren[i]) ? "(ujung)" : Fmt.Num(h.Tren[i], 4),
                        Fmt.Num(h.MusimanArr[i], 4),
                        double.IsNaN(h.Residu[i]) ? "(ujung)" : Fmt.Num(h.Residu[i], 4),
                    }).ToArray(),
                    "Tren = rata-rata bergerak terpusat (ujungnya kosong sepanjang "
                    + $"m/2 = {Fmt.Int(periode / 2)} titik). Musiman berulang tiap "
                    + $"{Fmt.Int(periode)} titik dan jumlah satu periodenya nol."));
                return blocks;
            }

            var hh = HoltWinters(y, periode, alpha, beta, gamma, horizon);
            if (hh is null)
            {
                blocks.Add(Blocks.Note(
                    "Holt-Winters tidak bisa dijalankan. Periksa: n ≥ 2m, "
                    + "0 < α ≤ 1, 0 ≤ β,γ ≤ 1, horizon ≥ 1.", NoteKind.Error));
                return blocks;
            }
            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Variabel", variabel),
                ("Periode musiman", $"m = {Fmt.Int(periode)}"),
                ("Parameter", $"α = {Fmt.Num(alpha, 4)}, β = {Fmt.Num(beta, 4)}, "
                               + $"γ = {Fmt.Num(gamma, 4)}"),
                ("Horizon ramalan", $"{Fmt.Int(horizon)} langkah"),
                ("Banyak amatan", $"n = {Fmt.Int(hh.N)}")));
            var idx2 = Enumerable.Range(0, hh.N).ToList();
            blocks.Add(Blocks.Table(
                "Pas (satu langkah ke depan)",
                new[] { "Waktu", "Y", "Pas" },
                idx2.Select(i => new[]
                {
                    Fmt.Int(i + 1), Fmt.Num(y[i], 4), Fmt.Num(hh.Pas[i], 4),
                }).ToArray(),
                $"SSE = {Fmt.Num(hh.Sse, 4)} — jumlah kuadrat galat satu langkah."));
            blocks.Add(Blocks.Table(
                "Ramalan",
                new[] { "Langkah", "Nilai" },
                hh.Ramal.Select((v, i) => new[]
                {
                    Fmt.Int(i + 1), Fmt.Num(v, 4),
                }).ToArray(),
                "Ramalan = level + h·tren + musiman yang berputar. Makin jauh "
                + "horizonnya, makin lebar selang yang seharusnya — tetapi "
                + "selang ramalan belum dihitung alat ini."));
            return blocks;
        }
    }
}
