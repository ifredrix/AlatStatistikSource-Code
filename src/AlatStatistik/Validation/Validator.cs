using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AlatStatistik.Models;
using AlatStatistik.Statistics;

namespace AlatStatistik.Validation
{
    public class HasilUji
    {
        public string Nama { get; set; } = "";
        public double Kita { get; set; }
        public double Acuan { get; set; }
        public double Selisih { get; set; }
        public bool Lulus { get; set; }
        public string Keterangan { get; set; } = "";

        public bool Terlewat { get; set; }
    }

    public static class Validator
    {
        public static List<HasilUji> Jalankan(string pathAcuan, Dataset? data)
        {
            var hasil = new List<HasilUji>();
            if (!File.Exists(pathAcuan)) return hasil;

            using var dokumen = JsonDocument.Parse(File.ReadAllText(pathAcuan));
            if (!dokumen.RootElement.TryGetProperty("uji", out var uji)) return hasil;

            var peta = SiapkanPeta(data);

            foreach (var entri in uji.EnumerateObject())
            {
                string nama = entri.Name;
                double acuan = entri.Value.GetDouble();

                
                
                
                
                
                
                
                
                
                Match cocok = Regex.Match(nama, @"^([\w_]+)\s*\(\s*([^)]*)\s*\)$");
                double? kita = null;

                if (cocok.Success)
                {
                    string fungsi = cocok.Groups[1].Value;
                    var angka = Regex.Matches(cocok.Groups[2].Value, @"[-+]?\d*\.?\d+(?:[eE][-+]?\d+)?");
                    if (angka.Count > 0)
                    {
                        var argumen = angka.Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
                        kita = HitungFungsi(fungsi, argumen);
                    }
                }

                if (kita == null && peta.TryGetValue(nama, out var hitung))
                {
                    try { kita = hitung(); } catch { kita = null; }
                }

                
                
                
                if (kita == null || double.IsNaN(kita.Value))
                {
                    hasil.Add(new HasilUji
                    {
                        Nama = nama,
                        Kita = double.NaN,
                        Acuan = acuan,
                        Selisih = double.NaN,
                        Lulus = false,
                        Terlewat = true,
                        Keterangan = "tidak terpetakan / gagal dihitung"
                    });
                    continue;
                }

                double selisih = Math.Abs(kita.Value - acuan);
                double toleransi = 1e-8 * Math.Max(1.0, Math.Abs(acuan));

                hasil.Add(new HasilUji
                {
                    Nama = nama,
                    Kita = kita.Value,
                    Acuan = acuan,
                    Selisih = selisih,
                    Lulus = selisih <= toleransi,
                    Keterangan = selisih <= toleransi ? "sesuai" : "menyimpang"
                });
            }

            return hasil;
        }

        

        private static double? HitungFungsi(string nama, List<double> a)
        {
            try
            {
                switch (nama)
                {
                    case "gamma_p" when a.Count == 2: return Special.GammaP(a[0], a[1]);
                    case "gamma_q" when a.Count == 2: return Special.GammaQ(a[0], a[1]);
                    case "beta_inc" when a.Count == 3: return Special.BetaInc(a[0], a[1], a[2]);
                    case "normal_cdf" when a.Count == 1: return Distributions.NormalCdf(a[0]);
                    case "t_two_sided" when a.Count == 2: return Distributions.StudentTTwoSided(a[0], a[1]);
                    case "t_cdf" when a.Count == 2: return Distributions.StudentTCdf(a[0], a[1]);
                    case "chi2_upper" when a.Count == 2: return Distributions.ChiSquareUpper(a[0], a[1]);
                    case "f_upper" when a.Count == 3: return Distributions.FUpper(a[0], a[1], a[2]);
                    case "t_inv" when a.Count == 2: return Distributions.StudentTInv(a[0], a[1]);
                    default: return null;
                }
            }
            catch { return null; }
        }

        

        private static Dictionary<string, Func<double>> SiapkanPeta(Dataset? ds)
        {
            var peta = new Dictionary<string, Func<double>>();
            if (ds == null) return peta;

            double[] Kolom(string nama)
                => ds.Numeric(nama).Where(v => v.HasValue).Select(v => v!.Value).ToArray();

            bool Ada(params string[] nama) => nama.All(n => ds.Names.Contains(n));

            var x1 = Ada("x1") ? Kolom("x1") : Array.Empty<double>();
            var x2 = Ada("x2") ? Kolom("x2") : Array.Empty<double>();
            var y = Ada("y") ? Kolom("y") : Array.Empty<double>();
            var before = Ada("before") ? Kolom("before") : Array.Empty<double>();
            var after = Ada("after") ? Kolom("after") : Array.Empty<double>();

            if (x1.Length > 0)
            {
                var s = Descriptives.Summarize(x1.ToList());
                peta["rerata"] = () => s.Mean;
                peta["simpangan baku"] = () => s.Sd;
                peta["condong (skew)"] = () => s.Skewness;
                peta["keruncing (kurt)"] = () => s.Kurtosis;
                peta["Jarque-Bera"] = () => s.JarqueBera;
                peta["Jarque-Bera p"] = () => s.JarqueBeraP;

                peta["t satu sampel"] = () => (s.Mean - 50.0) / s.Sem;
                peta["t satu sampel p"] = () => Distributions.StudentTTwoSided((s.Mean - 50.0) / s.Sem, s.N - 1);

                
                
                
                var sw = Normality.ShapiroWilk(x1);
                if (sw != null)
                {
                    peta["Shapiro-Wilk W"] = () => sw.Statistik;
                    peta["Shapiro-Wilk p"] = () => sw.P;
                }

                var ks = Normality.KolmogorovSmirnov(x1);
                if (ks != null)
                {
                    peta["Kolmogorov-Smirnov D"] = () => ks.Statistik;
                    peta["Kolmogorov-Smirnov p (asimptotik)"] = () => ks.P;
                }
            }

            if (Ada("x1", "kelompok"))
            {
                var grup = Compare.GroupValues(ds, "x1", "kelompok");
                var kunci = grup.Keys.ToList();
                if (kunci.Count >= 2)
                {
                    var a = grup[kunci[0]];
                    var b = grup[kunci[1]];
                    var sa = Descriptives.Summarize(a);
                    var sb = Descriptives.Summarize(b);

                    int dfPooled = sa.N + sb.N - 2;
                    double sp2 = ((sa.N - 1) * sa.Variance + (sb.N - 1) * sb.Variance) / dfPooled;
                    double sePooled = Math.Sqrt(sp2 * (1.0 / sa.N + 1.0 / sb.N));
                    double tPooled = (sa.Mean - sb.Mean) / sePooled;

                    double v1 = sa.Variance / sa.N, v2 = sb.Variance / sb.N;
                    double seWelch = Math.Sqrt(v1 + v2);
                    double tWelch = (sa.Mean - sb.Mean) / seWelch;
                    double dfWelch = Math.Pow(v1 + v2, 2) / (v1 * v1 / (sa.N - 1) + v2 * v2 / (sb.N - 1));

                    peta["t Student"] = () => tPooled;
                    peta["t Student p"] = () => Distributions.StudentTTwoSided(tPooled, dfPooled);
                    peta["t Welch"] = () => tWelch;
                    peta["t Welch p"] = () => Distributions.StudentTTwoSided(tWelch, dfWelch);
                    peta["df Welch"] = () => dfWelch;
                    peta["Levene F"] = () => Compare.Levene(new[] { a, b }, out _);
                    peta["Levene p"] = () => { Compare.Levene(new[] { a, b }, out double pl); return pl; };

                    
                    
                    
                    var mw = Compare.MannWhitneyU(a, b);
                    peta["Mann-Whitney U"] = () => mw.U;
                    peta["Mann-Whitney p"] = () => mw.P;
                }
            }

            
            if (Ada("x1", "tingkat"))
            {
                var gKw = Compare.GroupValues(ds, "x1", "tingkat");
                if (gKw.Count >= 2)
                {
                    var kw = Compare.KruskalWallisH(gKw.Values);
                    peta["Kruskal-Wallis H"] = () => kw.HTerkoreksi;
                    peta["Kruskal-Wallis p"] = () => kw.P;
                }
            }

            if (before.Length > 0 && after.Length == before.Length)
            {
                var beda = before.Zip(after, (p, q) => p - q).ToList();
                var sb2 = Descriptives.Summarize(beda);
                peta["t berpasangan"] = () => sb2.Mean / sb2.Sem;
                peta["t berpasangan p"] = () => Distributions.StudentTTwoSided(sb2.Mean / sb2.Sem, sb2.N - 1);

                var bedaNol = beda.Where(v => Math.Abs(v) > 1e-12).ToList();
                if (bedaNol.Count >= 5)
                {
                    var w = Compare.WilcoxonSigned(bedaNol);
                    peta["Wilcoxon W+"] = () => w.WPlus;
                    peta["Wilcoxon p"] = () => w.P;
                }
            }

            
            
            
            
            
            
            
            {
                var tabel = new int[,] { { 20, 15, 10 }, { 12, 25, 18 }, { 8, 10, 22 } };
                var stat = Crosstab.ChiSquareTable(tabel);
                peta["chi-kuadrat"] = () => stat.Chi2;
                peta["chi-kuadrat p"] = () => stat.P;
                peta["Cramer V"] = () => stat.CramersV;
                peta["rasio kemungkinan G2"] = () => stat.G2;

                
                peta["Fisher tepat 2x2"] = () => Crosstab.FisherExact2x2(3, 1, 2, 6);
            }

            
            if (Ada("i1", "i2", "i3", "i4", "i5"))
            {
                var aitem = new List<string> { "i1", "i2", "i3", "i4", "i5" };
                var keep = ds.CompleteRows(aitem);
                if (keep.Count >= 3)
                {
                    var data = new double[aitem.Count][];
                    for (int j = 0; j < aitem.Count; j++)
                    {
                        int col = ds.IndexOf(aitem[j]);
                        data[j] = keep.Select(r => Dataset.ToDouble(ds.Rows[r][col])!.Value).ToArray();
                    }
                    var alfa = Reliability.Alfa(data);
                    peta["alfa Cronbach"] = () => alfa.Alpha;
                }
            }

            if (Ada("x1", "tingkat"))
            {
                var grup = Compare.GroupValues(ds, "x1", "tingkat");
                var daftar = grup.Values.ToList();
                if (daftar.Count >= 2)
                {
                    double grand = daftar.SelectMany(g => g).Average();
                    double ssb = daftar.Sum(g => g.Count * Math.Pow(g.Average() - grand, 2));
                    double ssw = daftar.Sum(g => (g.Count - 1) * Descriptives.Summarize(g).Variance);
                    int k = daftar.Count, n = daftar.Sum(g => g.Count);
                    double f = (ssb / (k - 1)) / (ssw / (n - k));

                    peta["ANOVA F"] = () => f;
                    peta["ANOVA p"] = () => Distributions.FUpper(f, k - 1, n - k);
                    peta["eta kuadrat"] = () => ssb / (ssb + ssw);
                }
            }

            if (x1.Length > 0 && y.Length > 0)
            {
                var lx = x1.ToList();
                var ly = y.ToList();
                peta["Pearson r"] = () => Correlation.Pearson(lx, ly);
                peta["Pearson p"] = () => Correlation.PearsonP(Correlation.Pearson(lx, ly), lx.Count);
                peta["Spearman rho"] = () => Correlation.Spearman(lx, ly);
                peta["Kendall tau-b"] = () => Correlation.KendallTauB(lx, ly, out _);
            }

            if (x1.Length > 0 && x2.Length > 0 && y.Length > 0)
            {
                var model = Regression.Fit(y, new List<double[]> { x1, x2 });
                if (model != null)
                {
                    int n = y.Length, p = 2;
                    double f = (model.Ssr / p) / (model.Sse / (n - p - 1));

                    peta["regresi konstanta"] = () => model.Beta[0];
                    peta["regresi SE konstanta"] = () => model.Se[0];
                    peta["regresi b_x1"] = () => model.Beta[1];
                    peta["regresi SE b_x1"] = () => model.Se[1];
                    peta["regresi b_x2"] = () => model.Beta[2];
                    peta["regresi SE b_x2"] = () => model.Se[2];
                    peta["regresi R2"] = () => model.R2;
                    peta["regresi R2 adj"] = () => model.AdjR2;
                    peta["regresi F"] = () => f;
                    peta["regresi F p"] = () => Distributions.FUpper(f, p, n - p - 1);

                    var vif1 = Regression.Fit(x1, new List<double[]> { x2 });
                    var vif2 = Regression.Fit(x2, new List<double[]> { x1 });
                    if (vif1 != null) peta["VIF x1"] = () => 1.0 / (1.0 - vif1.R2);
                    if (vif2 != null) peta["VIF x2"] = () => 1.0 / (1.0 - vif2.R2);

                    peta["Durbin-Watson"] = () =>
                    {
                        double num = 0, den = 0;
                        for (int i = 1; i < model.Residuals.Length; i++)
                            num += Math.Pow(model.Residuals[i] - model.Residuals[i - 1], 2);
                        foreach (double r in model.Residuals) den += r * r;
                        return den > 0 ? num / den : double.NaN;
                    };
                }
            }

            return peta;
        }
    }
}
