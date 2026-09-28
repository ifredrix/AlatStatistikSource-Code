using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class Pakar
    {
        public static readonly int[] Origins = { 144, 156, 168 };

        public sealed class BarisTurnamen
        {
            public int P, D, Q, Ps, Ds, Qs;
            public double[] RmseOrg = Array.Empty<double>();
            public double Rmse;
            public bool Musiman;
            public string Ordo => Musiman ? $"({P},{D},{Q})({Ps},{Ds},{Qs},{SRef})"
                                          : $"({P},{D},{Q})";
            public int SRef;
        }

        public sealed class Hasil
        {
            public int S;
            public int H;
            public bool Musiman;
            public int[] OriginsPakai = Array.Empty<int>();
            public List<BarisTurnamen> Tabel = new();
            public int Menang;
            public double[] RmseNaiveOrg = Array.Empty<double>();
            public double RmseNaive;
            public Arima.Hasil? Penuh;
        }

        public static readonly int[][] GridMusiman =
        {
            new[] { 0, 1, 1, 0, 1, 1 },
            new[] { 1, 1, 1, 0, 1, 1 },
            new[] { 0, 1, 2, 0, 1, 1 },
            new[] { 2, 1, 0, 0, 1, 1 },
            new[] { 0, 1, 1, 1, 1, 0 },
            new[] { 1, 1, 1, 1, 1, 0 },
            new[] { 0, 1, 1, 0, 1, 0 },
            new[] { 1, 1, 0, 0, 1, 1 },
            new[] { 0, 1, 0, 0, 1, 1 },
            new[] { 2, 1, 1, 0, 1, 1 },
            new[] { 0, 1, 1, 1, 1, 1 },
            new[] { 1, 1, 0, 0, 1, 0 },
        };

        public static readonly int[][] GridBiasa =
        {
            new[] { 1, 0, 0 },
            new[] { 0, 0, 1 },
            new[] { 1, 0, 1 },
            new[] { 2, 0, 0 },
            new[] { 0, 0, 2 },
            new[] { 1, 1, 1 },
            new[] { 0, 1, 1 },
            new[] { 1, 1, 0 },
            new[] { 0, 1, 0 },
            new[] { 2, 1, 0 },
            new[] { 0, 1, 2 },
            new[] { 1, 0, 2 },
        };

        private static double Rmse(double[] a, double[] b)
        {
            double s = 0;
            for (int i = 0; i < a.Length; i++)
            {
                double d = a[i] - b[i];
                s += d * d;
            }
            return Math.Sqrt(s / a.Length);
        }

        public static Hasil? Turnamen(double[] y, int s, int h)
        {
            int n0 = y.Length;
            if (n0 < 24 || y.Any(double.IsNaN)) return null;
            bool musiman = s >= 2 && n0 >= 60;
            int hh;
            int[] origins;
            if (musiman)
            {
                if (s < 2 || s > 12) return null;
                hh = Math.Min(24, Math.Max(1, h));
                origins = Origins.Where(o => o + hh <= n0).ToArray();
                if (origins.Length == 0) return null;
            }
            else
            {
                s = 1;
                hh = Math.Min(Math.Max(1, h), Math.Max(4, (n0 - 12) / 4));
                origins = new[] { n0 - 3 * hh, n0 - 2 * hh, n0 - hh };
                if (origins[0] < 12) return null;
            }

            var hsil = new Hasil { S = s, H = hh, Musiman = musiman, OriginsPakai = origins };
            var naiveOrg = new List<double>();
            foreach (int o in origins)
            {
                var tahan = y.Skip(o).Take(hh).ToArray();
                var naive = new double[hh];
                for (int t = 0; t < hh; t++)
                    naive[t] = musiman ? y[o - s + (t % s)] : y[o - 1];
                naiveOrg.Add(Rmse(naive, tahan));
            }
            hsil.RmseNaiveOrg = naiveOrg.ToArray();
            hsil.RmseNaive = naiveOrg.Sum() / naiveOrg.Count;

            if (musiman)
            {
                foreach (var g in GridMusiman)
                {
                    var baris = new BarisTurnamen
                    {
                        P = g[0], D = g[1], Q = g[2],
                        Ps = g[3], Ds = g[4], Qs = g[5],
                        Musiman = true, SRef = s,
                    };
                    var rs = new List<double>();
                    bool ok = true;
                    foreach (int o in origins)
                    {
                        var potong = y.Take(o).ToArray();
                        var fit = Arima.PasangMusiman(potong, g[0], g[1], g[2],
                                                      g[3], g[4], g[5], s, hh);
                        if (fit is null) { ok = false; break; }
                        rs.Add(Rmse(fit.Ramal, y.Skip(o).Take(hh).ToArray()));
                    }
                    if (!ok) continue;
                    baris.RmseOrg = rs.ToArray();
                    baris.Rmse = rs.Sum() / rs.Count;
                    hsil.Tabel.Add(baris);
                }
            }
            else
            {
                foreach (var g in GridBiasa)
                {
                    var baris = new BarisTurnamen
                    {
                        P = g[0], D = g[1], Q = g[2],
                        Ps = 0, Ds = 0, Qs = 0, Musiman = false, SRef = 1,
                    };
                    var rs = new List<double>();
                    bool ok = true;
                    foreach (int o in origins)
                    {
                        var potong = y.Take(o).ToArray();
                        var fit = Arima.Pasang(potong, g[0], g[1], g[2], hh);
                        if (fit is null) { ok = false; break; }
                        rs.Add(Rmse(fit.Ramal, y.Skip(o).Take(hh).ToArray()));
                    }
                    if (!ok) continue;
                    baris.RmseOrg = rs.ToArray();
                    baris.Rmse = rs.Sum() / rs.Count;
                    hsil.Tabel.Add(baris);
                }
            }
            if (hsil.Tabel.Count == 0) return null;
            int menang = 0;
            for (int i = 1; i < hsil.Tabel.Count; i++)
                if (hsil.Tabel[i].Rmse < hsil.Tabel[menang].Rmse) menang = i;
            hsil.Menang = menang;
            var w = hsil.Tabel[menang];
            hsil.Penuh = musiman
                ? Arima.PasangMusiman(y, w.P, w.D, w.Q, w.Ps, w.Ds, w.Qs, s, hh)
                : Arima.Pasang(y, w.P, w.D, w.Q, hh);
            return hsil;
        }

        public static List<ResultBlock> PakarBlocks(Dataset ds, string deret, int h, int s = 12)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Expert Modeler lite (turnamen RMSE rolling-origin)", 1)
            };
            var nilai = Descriptives.CleanNumbers(ds, deret)
                                   .Where(v => !double.IsNaN(v)).ToArray();
            if (nilai.Length < 24)
            {
                blocks.Add(Blocks.Note("Turnamen butuh sedikitnya 24 amatan.",
                                       NoteKind.Error));
                return blocks;
            }

            var hh = Turnamen(nilai, s, Math.Min(24, Math.Max(1, h)));
            if (hh is null || hh.Penuh is null)
            {
                blocks.Add(Blocks.Note("Turnamen tidak bisa diselesaikan.",
                                       NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Pakar));

            var menang = hh.Tabel[hh.Menang];
            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Deret", hh.Musiman ? $"{deret} (musiman {hh.S})" : $"{deret} (tak-musiman)"),
                ("Origin", string.Join(", ", hh.OriginsPakai)),
                ("Tahan per origin", $"H = {Fmt.Int(hh.H)}"),
                ("Pemenang", $"{menang.Ordo} (RMSE rerata = {Fmt.Num(menang.Rmse, 4)})"),
                ("Naif", $"RMSE rerata = {Fmt.Num(hh.RmseNaive, 4)}"),
                ("Ljung-Box pemenang",
                 $"Q = {Fmt.Num(hh.Penuh.LbStat, 3)}, p = {Fmt.Num(hh.Penuh.LbP, 4)}")));

            blocks.Add(Blocks.Table(
                $"Turnamen {hh.Tabel.Count} ordo (rerata RMSE {hh.OriginsPakai.Length} origin)",
                new[] { "Ordo", "RMSE" },
                hh.Tabel.Select((t, i) => new[]
                {
                    i == hh.Menang ? t.Ordo + " ← dipilih" : t.Ordo,
                    Fmt.Num(t.Rmse, 4),
                }).ToArray(),
                "Tiap ordo ditaksir ulang per origin lalu meramal data-tahan; "
                + "kriteria = rerata RMSE (Hyndman–Athanasopoulos)."));

            blocks.Add(Blocks.Table(
                $"Ramalan {hh.H} langkah (pemenang {menang.Ordo})",
                new[] { "Horizon", "Ramalan" },
                hh.Penuh.Ramal.Select((v, t) =>
                    new[] { $"T+{t + 1}", Fmt.Num(v, 4) }).ToArray(),
                "SE tak dilaporkan (model terintegrasi; lihat ARIMA)."));

            return blocks;
        }
    }
}
