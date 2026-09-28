using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public enum KuantilDefinisi
    {
        Satu,
        TukeyHinges,

        Linear
    }

    public static class Descriptives
    {

        public const KuantilDefinisi DefinisiPagarPencilan = KuantilDefinisi.Linear;

        public static string NamaDefinisi(KuantilDefinisi definisi) => definisi switch
        {
            KuantilDefinisi.TukeyHinges => "Tukey's Hinges",
            KuantilDefinisi.Linear => "posisi (n−1)·p — bawaan NumPy/pandas",
            _ => "Weighted Average (Definition 1) — bawaan, posisi (n+1)·p",
        };

        public static Summary Summarize(IEnumerable<double> values,
                                        KuantilDefinisi definisi = KuantilDefinisi.Satu)
        {
            var x = values.ToList();
            var s = new Summary { N = x.Count };
            if (s.N == 0) return s;

            s.Mean = x.Average();
            s.Min = x.Min();
            s.Max = x.Max();
            s.Median = Quantile(x, 0.5, definisi);
            s.Q1 = Quantile(x, 0.25, definisi);
            s.Q3 = Quantile(x, 0.75, definisi);
            s.Iqr = s.Q3 - s.Q1;

            if (s.N > 1)
            {
                double ss = x.Sum(v => (v - s.Mean) * (v - s.Mean));
                s.Variance = ss / (s.N - 1);
                s.Sd = Math.Sqrt(s.Variance);
                s.Sem = s.Sd / Math.Sqrt(s.N);

                double tCrit = Distributions.StudentTInv(0.975, s.N - 1);
                s.CiLower = s.Mean - tCrit * s.Sem;
                s.CiUpper = s.Mean + tCrit * s.Sem;

                
                double m2 = ss / s.N;
                double m3 = x.Sum(v => Math.Pow(v - s.Mean, 3)) / s.N;
                double m4 = x.Sum(v => Math.Pow(v - s.Mean, 4)) / s.N;

                double g1 = m2 > 0 ? m3 / Math.Pow(m2, 1.5) : double.NaN;      
                double g2 = m2 > 0 ? m4 / (m2 * m2) - 3.0 : double.NaN;        

                s.Skewness = s.N > 2 ? g1 * Math.Sqrt((double)s.N * (s.N - 1)) / (s.N - 2) : double.NaN;

                if (s.N > 3)
                {
                    double n = s.N;
                    double a = n * (n + 1.0) / ((n - 1) * (n - 2) * (n - 3));
                    double b = 3.0 * (n - 1) * (n - 1) / ((n - 2) * (n - 3));
                    double z4 = x.Sum(v => Math.Pow((v - s.Mean) / s.Sd, 4));
                    s.Kurtosis = a * z4 - b;

                    s.SeSkewness = Math.Sqrt(6.0 * n * (n - 1) / ((n - 2) * (n + 1) * (n + 3)));
                    s.SeKurtosis = Math.Sqrt(24.0 * n * (n - 1) * (n - 1) /
                                             ((n - 3) * (n - 2) * (n + 3) * (n + 5)));
                }

                
                if (m2 > 0)
                {
                    s.JarqueBera = s.N / 6.0 * (g1 * g1 + 0.25 * g2 * g2);
                    s.JarqueBeraP = Distributions.ChiSquareUpper(s.JarqueBera, 2);
                }
            }

            if (Math.Abs(s.Mean) > 1e-12 && s.N > 1)
                s.CoefVariation = s.Sd / Math.Abs(s.Mean) * 100.0;

            return s;
        }

        public static double Quantile(List<double> sortedOrNot, double q,
                                      KuantilDefinisi definisi = KuantilDefinisi.Satu)
        {
            var x = sortedOrNot.OrderBy(v => v).ToList();
            int n = x.Count;
            if (n == 0) return double.NaN;
            if (n == 1) return x[0];

            switch (definisi)
            {
                case KuantilDefinisi.TukeyHinges:
                    return TukeyHinges(x, q);

                case KuantilDefinisi.Linear:
                    {
                        double posL = (n - 1) * q;
                        int loL = (int)Math.Floor(posL);
                        int hiL = (int)Math.Ceiling(posL);
                        if (loL == hiL) return x[loL];
                        return x[loL] + (posL - loL) * (x[hiL] - x[loL]);
                    }

                default:                                   
                    {
                        double pos = (n + 1) * q;          
                        if (pos <= 1.0) return x[0];
                        if (pos >= n) return x[n - 1];
                        int lo = (int)Math.Floor(pos);
                        double f = pos - lo;
                        return x[lo - 1] + f * (x[lo] - x[lo - 1]);
                    }
            }
        }

        private static double TukeyHinges(List<double> x, double q)
        {
            int n = x.Count;
            int setengah = n / 2;
            bool ganjil = n % 2 == 1;
            var bawah = x.Take(ganjil ? setengah + 1 : setengah).ToList();
            var atas = x.Skip(setengah).ToList();

            if (q < 0.5) return Quantile(bawah, 0.5, KuantilDefinisi.Satu);
            if (q > 0.5) return Quantile(atas, 0.5, KuantilDefinisi.Satu);
            return Quantile(x, 0.5, KuantilDefinisi.Satu);
        }

        public static List<ResultBlock> Describe(Dataset ds, IEnumerable<string> names, bool normality = true)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading("Statistik Deskriptif", 1) };

            var columns = new[]
            {
                "Variabel", "N", "Rerata", "Galat Baku", "Simpangan Baku", "Ragam",
                "Condong", "Keruncing", "Min", "Q1", "Median", "Q3", "Maks"
            };
            var rows = new List<List<string>>();
            var langkah = new List<LangkahHitung>();

            foreach (string name in names)
            {
                var values = CleanNumbers(ds, name);
                if (values.Count == 0)
                {
                    blocks.Add(Blocks.Note($"Variabel '{name}' tidak punya nilai numerik yang sah; dilewati.", NoteKind.Warning));
                    continue;
                }
                var s = Summarize(values);
                rows.Add(new List<string>
                {
                    name, Fmt.Int(s.N), Fmt.Num(s.Mean), Fmt.Num(s.Sem), Fmt.Num(s.Sd), Fmt.Num(s.Variance),
                    Fmt.Num(s.Skewness, 3), Fmt.Num(s.Kurtosis, 3), Fmt.Num(s.Min), Fmt.Num(s.Q1),
                    Fmt.Num(s.Median), Fmt.Num(s.Q3), Fmt.Num(s.Max)
                });

                
                double jumlah = values.Sum();
                langkah.Add(new LangkahHitung
                {
                    Uraian = $"Rerata — {name}",
                    Hitungan = $"x̄ = Σxᵢ / n = {Fmt.Num(jumlah)} / {Fmt.Int(s.N)} = {Fmt.Num(s.Mean)}"
                });

                double ss = values.Sum(v => (v - s.Mean) * (v - s.Mean));
                langkah.Add(new LangkahHitung
                {
                    Uraian = $"Ragam & simpangan baku — {name}",
                    Hitungan = s.N > 1
                        ? $"s² = Σ(xᵢ − x̄)² / (n − 1) = {Fmt.Num(ss)} / {Fmt.Int(s.N - 1)} = {Fmt.Num(s.Variance)}"
                          + $"   →   s = √{Fmt.Num(s.Variance)} = {Fmt.Num(s.Sd)}"
                        : "s² butuh sedikitnya 2 amatan."
                });

                langkah.Add(new LangkahHitung
                {
                    Uraian = $"Kuartil — {name}",
                    Hitungan = $"{NamaDefinisi(KuantilDefinisi.Satu)}"
                               + $"   →   Q1 = {Fmt.Num(s.Q1)}   Median = {Fmt.Num(s.Median)}   Q3 = {Fmt.Num(s.Q3)}"
                               + $"   (IQR = {Fmt.Num(s.Iqr)})"
                });
            }

            
            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.Rerata, DaftarRumus.RagamSampel, DaftarRumus.MedianKuartil,
                DaftarRumus.Skewness, DaftarRumus.Kurtosis));

            if (langkah.Count > 0)
                blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data", langkah));

            blocks.Add(Blocks.Table("Statistik deskriptif", columns, rows,
                "Keruncing dilaporkan sebagai excess kurtosis (normal = 0). "
                + "Condong dan keruncing memakai koreksi bias (seperti SciPy bias=False)."));

            if (normality)
            {
                
                
                
                var nCol = new[] { "Variabel", "Uji", "Statistik", "p", "Keputusan (α = 0,05)" };
                var nRows = new List<List<string>>();
                var nLangkah = new List<LangkahHitung>();
                bool adaLilliefors = false;

                foreach (string name in names)
                {
                    var values = CleanNumbers(ds, name);
                    if (values.Count < 4) continue;
                    var s = Summarize(values);

                    void Baris(string uji, double stat, double p, int angka)
                        => nRows.Add(new List<string>
                        {
                            name, uji, Fmt.Num(stat, angka), Fmt.P(p),
                            p < 0.05 ? "Tidak normal (H0 ditolak)" : "Tidak cukup bukti menyimpang dari normal"
                        });

                    Baris("Jarque–Bera", s.JarqueBera, s.JarqueBeraP, 3);
                    nLangkah.Add(new LangkahHitung
                    {
                        Uraian = $"Jarque–Bera — {name}",
                        Hitungan = $"JB = ({Fmt.Int(s.N)} / 6) · ({Fmt.Num(s.Skewness, 3)}² "
                                   + $"+ {Fmt.Num(s.Kurtosis, 3)}²/4) = {Fmt.Num(s.JarqueBera, 3)}"
                                   + $"   →   p = {Fmt.P(s.JarqueBeraP)}"
                    });

                    var sw = Normality.ShapiroWilk(values);
                    if (sw != null)
                    {
                        Baris(sw.Nama, sw.Statistik, sw.P, 4);
                        nLangkah.Add(new LangkahHitung
                        {
                            Uraian = $"Shapiro–Wilk — {name}",
                            Hitungan = $"W = (Σ aᵢ·x₍ᵢ₎)² / Σ(xᵢ − x̄)²  dengan n = {Fmt.Int(sw.N)}"
                                       + $" dan Σ(xᵢ − x̄)² = {Fmt.Num(values.Sum(v => (v - s.Mean) * (v - s.Mean)))}"
                                       + $"   →   W = {Fmt.Num(sw.Statistik, 4)}, p = {Fmt.P(sw.P)}"
                        });
                    }

                    var ks = Normality.KolmogorovSmirnov(values);
                    if (ks != null)
                    {
                        adaLilliefors = true;
                        Baris(ks.Nama, ks.Statistik, ks.P, 4);
                        nLangkah.Add(new LangkahHitung
                        {
                            Uraian = $"Kolmogorov–Smirnov — {name}",
                            
                            
                            
                            
                            Hitungan = $"D = sup |Fₙ(x) − F(x)| = {Fmt.Num(ks.Statistik, 4)}"
                                       + $"   →   λ = √n · D = {Fmt.Num(Math.Sqrt(ks.N) * ks.Statistik, 4)}"
                                       + $", p = {Fmt.P(ks.P)}"
                        });
                    }
                }

                if (nRows.Count > 0)
                {
                    blocks.Add(Blocks.Rumus("Rumus uji kenormalan",
                        DaftarRumus.UjiJarqueBera, DaftarRumus.UjiShapiroWilk, DaftarRumus.UjiKolmogorovSmirnov));
                    blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data", nLangkah));
                    blocks.Add(Blocks.Table("Uji kenormalan", nCol, nRows,
                        "H0: data berdistribusi normal. Shapiro–Wilk paling bertenaga untuk n kecil; "
                        + "Jarque–Bera berbasis momen dan butuh n besar."
                        + (adaLilliefors
                            ? " Nilai p Kolmogorov–Smirnov memakai sebaran asimptotik tanpa koreksi "
                              + "Lilliefors, karena rerata dan simpangan baku ditaksir dari data yang sama — "
                              + "angkanya cenderung terlalu besar."
                            : "")));
                }
            }

            return blocks;
        }

        public sealed class BarisFrekuensi
        {
            public string Kategori = "";
            public int Frekuensi;
            public double Persen;            
            public double PersenValid;       
            public double PersenKumulatif;   
        }

        public static List<BarisFrekuensi> HitungFrekuensi(
            Dataset ds, string name, out int total, out int valid, out int hilang)
        {
            var raw = ds.Text(name);
            var counts = new Dictionary<string, int>();
            hilang = 0;
            foreach (string? v in raw)
            {
                if (string.IsNullOrWhiteSpace(v)) { hilang++; continue; }
                counts[v] = counts.TryGetValue(v, out int c) ? c + 1 : 1;
            }
            total = raw.Length;
            valid = total - hilang;

            bool numerik = counts.Count > 0 && counts.Keys.All(k => Dataset.ToDouble(k).HasValue);
            var urut = numerik
                ? counts.Keys.OrderBy(k => Dataset.ToDouble(k)!.Value).ToList()
                : counts.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

            var hasil = new List<BarisFrekuensi>();
            int kumulatif = 0;
            foreach (string k in urut)
            {
                kumulatif += counts[k];
                hasil.Add(new BarisFrekuensi
                {
                    Kategori = k,
                    Frekuensi = counts[k],
                    Persen = total > 0 ? 100.0 * counts[k] / total : 0,
                    PersenValid = valid > 0 ? 100.0 * counts[k] / valid : 0,
                    PersenKumulatif = valid > 0 ? 100.0 * kumulatif / valid : 0,
                });
            }
            return hasil;
        }

        public static List<ResultBlock> Frequencies(Dataset ds, string name, bool withChart = true)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading($"Frekuensi — {name}", 1) };

            var daftar = HitungFrekuensi(ds, name, out int total, out int valid, out int missing);

            var columns = new[] { "Kategori", "Frekuensi", "Persen", "Persen valid", "Persen kumulatif" };
            var rows = new List<List<string>>();
            var langkah = new List<LangkahHitung>();
            var spec = new ChartSpec { Kind = ChartKind.Bar, Title = name, XTitle = name, YTitle = "Frekuensi" };

            foreach (var b in daftar)
            {
                rows.Add(new List<string>
                {
                    b.Kategori,
                    Fmt.Int(b.Frekuensi),
                    Fmt.Num(b.Persen, 1),
                    Fmt.Num(b.PersenValid, 1),
                    Fmt.Num(b.PersenKumulatif, 1)
                });
                spec.Bars.Add((b.Kategori, b.Frekuensi));

                langkah.Add(new LangkahHitung
                {
                    Uraian = $"Frekuensi '{b.Kategori}'",
                    Hitungan = $"f = {Fmt.Int(b.Frekuensi)}   p = {Fmt.Int(b.Frekuensi)} / {Fmt.Int(valid)}"
                               + $" = {Fmt.Num(b.PersenValid, 1)}%"
                               + $"   F = {Fmt.Num(b.PersenKumulatif, 1)}%"
                });
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Frekuensi));
            if (langkah.Count > 0)
                blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data", langkah));

            blocks.Add(Blocks.Table("Frekuensi", columns, rows,
                $"Valid: {valid} · Hilang: {missing} · Total: {total}. "
                + "Baris diurutkan menurut kategori, bukan menurut frekuensi — "
                + "kolom kumulatif hanya punya arti dalam urutan kategori."));

            if (withChart && spec.Bars.Count > 0)
                blocks.Add(Blocks.Chart($"Diagram batang — {name}", spec));

            return blocks;
        }

        public static List<double> CleanNumbers(Dataset ds, string name)
            => ds.Numeric(name).Where(v => v.HasValue).Select(v => v!.Value).ToList();

        public static List<ResultBlock> Distribution(Dataset ds, string name)
        {
            var values = CleanNumbers(ds, name);
            var blocks = new List<ResultBlock>();
            if (values.Count == 0)
            {
                blocks.Add(Blocks.Note($"Variabel '{name}' tidak punya nilai numerik.", NoteKind.Warning));
                return blocks;
            }

            var hist = new ChartSpec { Kind = ChartKind.Histogram, Title = $"Histogram — {name}", XTitle = name, YTitle = "Frekuensi" };
            hist.Series[name] = values;

            var box = new ChartSpec { Kind = ChartKind.BoxPlot, Title = $"Box plot — {name}", XTitle = name, YTitle = "Nilai" };
            box.Series[name] = values;

            var s = Summarize(values, DefinisiPagarPencilan);

            blocks.Add(Blocks.Heading($"Distribusi — {name}", 1));
            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.MedianKuartil, DaftarRumus.RagamSampel));
            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Rerata", $"x̄ = Σxᵢ / n = {Fmt.Num(values.Sum())} / {Fmt.Int(s.N)} = {Fmt.Num(s.Mean)}"),
                ("Simpangan baku", $"s = √[ Σ(xᵢ − x̄)² / (n − 1) ] = {Fmt.Num(s.Sd)}"),
                ($"Kuartil ({NamaDefinisi(DefinisiPagarPencilan)})",
                 $"Q1 = {Fmt.Num(s.Q1)}   Median = {Fmt.Num(s.Median)}   Q3 = {Fmt.Num(s.Q3)}"),
                ("Pagar pencilan (1,5 × IQR)",
                 $"IQR = {Fmt.Num(s.Iqr)}   bawah = {Fmt.Num(s.Q1 - 1.5 * s.Iqr)}   atas = {Fmt.Num(s.Q3 + 1.5 * s.Iqr)}")));

            blocks.Add(Blocks.Chart(hist.Title, hist));
            blocks.Add(Blocks.Chart(box.Title, box));
            return blocks;
        }
    }

    public class Summary
    {
        public int N { get; set; }
        public double Mean { get; set; } = double.NaN;
        public double Sd { get; set; } = double.NaN;
        public double Variance { get; set; } = double.NaN;
        public double Sem { get; set; } = double.NaN;
        public double Min { get; set; } = double.NaN;
        public double Max { get; set; } = double.NaN;
        public double Median { get; set; } = double.NaN;
        public double Q1 { get; set; } = double.NaN;
        public double Q3 { get; set; } = double.NaN;
        public double Iqr { get; set; } = double.NaN;
        public double Skewness { get; set; } = double.NaN;
        public double Kurtosis { get; set; } = double.NaN;
        public double SeSkewness { get; set; } = double.NaN;
        public double SeKurtosis { get; set; } = double.NaN;
        public double JarqueBera { get; set; } = double.NaN;
        public double JarqueBeraP { get; set; } = double.NaN;
        public double CiLower { get; set; } = double.NaN;
        public double CiUpper { get; set; } = double.NaN;
        public double CoefVariation { get; set; } = double.NaN;
    }
}
