using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Anova
    {

        public sealed class BarisKelompok
        {
            public string Kelompok = "";
            public int N;
            public double Mean, Sd, Sem, Min, Max, CiLower, CiUpper;
        }

        public sealed class HasilAnova
        {

            public Dictionary<string, List<double>> Grup = new();

            public List<BarisKelompok> Kelompok = new();
            public int K, N;
            public double Grand;
            public double SsBetween, SsWithin, SsTotal;
            public int DfBetween, DfWithin;
            public double MsBetween, MsWithin, F, P;
            public double Eta2, Omega2;
            public double LeveneF, LeveneP;

            public double LeveneMedianF, LeveneMedianP;

            public double LeveneTerpangkasF, LeveneTerpangkasP;
            public double WelchF, WelchDf2, WelchP;
            public int WelchDf1;
            public double Alpha = 0.05;
        }

        public static HasilAnova? Hitung(Dataset ds, string valueVar, string groupVar, double alpha = 0.05)
        {
            var groups = Compare.GroupValues(ds, valueVar, groupVar);
            if (groups.Count < 2) return null;

            int k = groups.Count;
            int N = groups.Values.Sum(g => g.Count);
            if (N - k <= 0) return null;

            var h = new HasilAnova { Grup = groups, K = k, N = N, Alpha = alpha };

            h.Grand = groups.Values.SelectMany(g => g).Average();

            foreach (var kv in groups)
            {
                var s = Descriptives.Summarize(kv.Value);
                h.Kelompok.Add(new BarisKelompok
                {
                    Kelompok = kv.Key, N = s.N, Mean = s.Mean, Sd = s.Sd, Sem = s.Sem,
                    Min = s.Min, Max = s.Max, CiLower = s.CiLower, CiUpper = s.CiUpper
                });
                h.SsBetween += s.N * Math.Pow(s.Mean - h.Grand, 2);
                h.SsWithin += (s.N - 1) * s.Variance;
            }

            h.DfBetween = k - 1;
            h.DfWithin = N - k;
            h.MsBetween = h.SsBetween / h.DfBetween;
            h.MsWithin = h.SsWithin / h.DfWithin;
            h.F = h.MsWithin > 0 ? h.MsBetween / h.MsWithin : double.NaN;
            h.P = Distributions.FUpper(h.F, h.DfBetween, h.DfWithin);

            h.SsTotal = h.SsBetween + h.SsWithin;
            h.Eta2 = h.SsTotal > 0 ? h.SsBetween / h.SsTotal : double.NaN;
            h.Omega2 = (h.SsBetween - h.DfBetween * h.MsWithin) / (h.SsTotal + h.MsWithin);

            h.LeveneF = Compare.LevenePusat(groups.Values, Compare.PusatLevene.Rerata, out double leveneP);
            h.LeveneP = leveneP;
            h.LeveneMedianF = Compare.LevenePusat(groups.Values, Compare.PusatLevene.Median, out double lmP);
            h.LeveneMedianP = lmP;
            h.LeveneTerpangkasF = Compare.LevenePusat(groups.Values, Compare.PusatLevene.RerataTerpangkas, out double ltP);
            h.LeveneTerpangkasP = ltP;

            var welch = Welch(groups.Values);
            h.WelchF = welch.F;
            h.WelchDf1 = welch.Df1;
            h.WelchDf2 = welch.Df2;
            h.WelchP = welch.P;

            return h;
        }

        public static List<ResultBlock> OneWay(Dataset ds, string valueVar, string groupVar, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading($"ANOVA satu arah — {valueVar} menurut {groupVar}", 1)
            };

            var h = Hitung(ds, valueVar, groupVar, alpha);
            if (h == null)
            {
                var groups = Compare.GroupValues(ds, valueVar, groupVar);
                blocks.Add(Blocks.Note(groups.Count < 2
                    ? "Butuh sedikitnya 2 kelompok."
                    : "Jumlah amatan tidak cukup.", NoteKind.Error));
                return blocks;
            }

            int k = h.K, N = h.N;
            int dfB = h.DfBetween, dfW = h.DfWithin;
            double grand = h.Grand;
            double ssBetween = h.SsBetween, ssWithin = h.SsWithin, ssTotal = h.SsTotal;
            double msBetween = h.MsBetween, msWithin = h.MsWithin, f = h.F, p = h.P;
            double eta2 = h.Eta2, omega2 = h.Omega2;

            var descRows = h.Kelompok.Select(r => new List<string>
            {
                r.Kelompok, Fmt.Int(r.N), Fmt.Num(r.Mean), Fmt.Num(r.Sd), Fmt.Num(r.Sem),
                $"[{Fmt.Num(r.CiLower)}; {Fmt.Num(r.CiUpper)}]", Fmt.Num(r.Min), Fmt.Num(r.Max)
            }).ToList();

            
            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.AnovaSatuArah, DaftarRumus.UjiLevene, DaftarRumus.LeveneMedian,
                DaftarRumus.RerataTerpangkas, DaftarRumus.AnovaWelch,
                DaftarRumus.RentangTerstudentkan, DaftarRumus.TukeyHsd));
            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Rerata keseluruhan", $"x̄ = {Fmt.Num(grand)}   N = {Fmt.Int(N)}   k = {Fmt.Int(k)} kelompok"),
                ("Levene — titik pusat simpangan",
                 $"rerata: W = {Fmt.Num(h.LeveneF, 3)}   median: W = {Fmt.Num(h.LeveneMedianF, 3)}   "
                 + $"rerata terpangkas 5%: W = {Fmt.Num(h.LeveneTerpangkasF, 3)}"),
                ("Levene — pemangkasan pecahan",
                 "5% × (" + string.Join(", ", h.Kelompok.Select(r => Fmt.Int(r.N))) + ") = "
                 + string.Join(" / ", h.Kelompok.Select(r => Fmt.Num(0.05 * r.N, 2)))
                 + " kasus dipangkas tiap ujung; bagian pecahannya diberi bobot sisa, bukan dibulatkan"),
                ("Jumlah kuadrat antar", $"SS_antar = Σ nⱼ(x̄ⱼ − x̄)² = {Fmt.Num(ssBetween, 3)}   df = {Fmt.Int(dfB)}"),
                ("Jumlah kuadrat dalam", $"SS_dalam = Σ (nⱼ − 1)sⱼ² = {Fmt.Num(ssWithin, 3)}   df = {Fmt.Int(dfW)}"),
                ("Kuadrat tengah", $"MS_antar = {Fmt.Num(msBetween, 3)}   MS_dalam = {Fmt.Num(msWithin, 3)}"),
                ("Statistik F", $"F = MS_antar / MS_dalam = {Fmt.Num(msBetween, 3)} / {Fmt.Num(msWithin, 3)} = {Fmt.Num(f, 3)}"
                                + $"   →   p = {Fmt.P(p)}"),
                ("Besaran efek", $"η² = SS_antar / SS_total = {Fmt.Num(eta2, 3)}   ω² = {Fmt.Num(omega2, 3)}")));

            blocks.Add(Blocks.Table("Statistik per kelompok",
                new[] { "Kelompok", "N", "Rerata", "Simpangan baku", "Galat baku", "Selang kepercayaan 95%", "Min", "Maks" },
                descRows));

            blocks.Add(Blocks.Table("Tabel ANOVA",
                new[] { "Sumber", "Jumlah kuadrat", "df", "Kuadrat tengah", "F", "p" },
                new[]
                {
                    new[] { "Antar kelompok", Fmt.Num(ssBetween, 3), Fmt.Int(dfB), Fmt.Num(msBetween, 3), Fmt.Num(f, 3), Fmt.P(p) },
                    new[] { "Dalam kelompok", Fmt.Num(ssWithin, 3), Fmt.Int(dfW), Fmt.Num(msWithin, 3), Fmt.NA, Fmt.NA },
                    new[] { "Total", Fmt.Num(ssTotal, 3), Fmt.Int(N - 1), Fmt.NA, Fmt.NA, Fmt.NA }
                },
                $"H0: semua rerata sama. {Compare.Keputusan(p, alpha)}"));

            blocks.Add(Blocks.Table("Besaran efek",
                new[] { "Ukuran", "Nilai", "Tafsir" },
                new[]
                {
                    new[] { "Eta kuadrat (η²)", Fmt.Num(eta2, 3), LabelEta(eta2) },
                    new[] { "Omega kuadrat (ω²)", Fmt.Num(omega2, 3), LabelEta(omega2) }
                },
                "η² = proporsi ragam yang diterangkan oleh kelompok. Pedoman Cohen: 0,01 kecil, 0,06 sedang, 0,14 besar."));

            
            double leveneP = h.LeveneP;
            blocks.Add(Blocks.Table("Uji kesamaan ragam (Levene)",
                new[] { "Titik pusat simpangan", "F", "df1", "df2", "p", "Kesimpulan" },
                new[]
                {
                    new[]
                    {
                        "Rerata kelompok", Fmt.Num(h.LeveneF, 3), Fmt.Int(dfB), Fmt.Int(dfW), Fmt.P(h.LeveneP),
                        h.LeveneP >= alpha ? "Ragam homogen" : "Ragam tidak homogen — perhatikan Welch"
                    },
                    new[]
                    {
                        "Median kelompok", Fmt.Num(h.LeveneMedianF, 3), Fmt.Int(dfB), Fmt.Int(dfW), Fmt.P(h.LeveneMedianP),
                        "Lebih tahan terhadap pencilan daripada rerata"
                    },
                    new[]
                    {
                        "Rerata terpangkas 5%", Fmt.Num(h.LeveneTerpangkasF, 3), Fmt.Int(dfB), Fmt.Int(dfW), Fmt.P(h.LeveneTerpangkasP),
                        "Pemotongan pecahan, mengikuti aturan yang lazim"
                    }
                },
                "acuan menampilkan baris keempat — \"based on median and with adjusted df\" — "
                + "dengan F yang sama tetapi df2 lebih kecil. Rumus penyesuaian df itu belum "
                + "berhasil kami reproduksi (51 kandidat rumus dicoba, tak satu pun menghasilkan "
                + "angka acuan), jadi baris itu sengaja tidak ditampilkan daripada menampilkan "
                + "angka yang tidak dapat dipertanggungjawabkan."));

            
            blocks.Add(Blocks.Table("ANOVA Welch (ragam tidak sama)",
                new[] { "F", "df1", "df2", "p", "Kesimpulan" },
                new[]
                {
                    new[]
                    {
                        Fmt.Num(h.WelchF, 3), Fmt.Int(h.WelchDf1), Fmt.Num(h.WelchDf2, 1), Fmt.P(h.WelchP),
                        Compare.Keputusan(h.WelchP, alpha)
                    }
                },
                "Pakai baris ini bila Levene signifikan — tidak menuntut ragam sama."));

            
            if (p < alpha || h.WelchP < alpha)
            {
                var tukey = TukeyHsd(h.Grup, msWithin, dfW, alpha);
                
                
                
                double qKrit = tukey.Count > 0 ? tukey[0].QKrit : double.NaN;

                blocks.Add(Blocks.Heading("Perbandingan berganda (Tukey HSD)", 2));
                blocks.Add(Blocks.Table("Tukey HSD",
                    new[] { "Pasangan", "Beda rerata", "Galat baku", "q", "p", "Selang kepercayaan 95%", "Kesimpulan" },
                    tukey.Select(t => new List<string>
                    {
                        $"{t.Kiri} − {t.Kanan}", Fmt.Num(t.Beda), Fmt.Num(t.Se), Fmt.Num(t.Q, 3),
                        Fmt.P(t.P),
                        $"[{Fmt.Num(t.Bawah)}; {Fmt.Num(t.Atas)}]",
                        t.Signifikan ? "berbeda nyata" : "tidak berbeda nyata"
                    }).ToList(),
                    $"q dibandingkan dengan kuantil rentang terstudentkan q({Fmt.Num((1 - alpha) * 100, 0)}%; "
                    + $"{Fmt.Int(k)} kelompok, {Fmt.Int(dfW)} df) = {Fmt.Num(qKrit, 3)}. "
                    + "Selangnya memakai kuantil q itu, bukan t — itulah yang membuat Tukey "
                    + "menjaga peluang galat keluarga tetap pada α."));

                blocks.Add(Blocks.Substitusi("Pemasukan nilai — Tukey HSD",
                    ("Kuantil rentang terstudentkan",
                     $"q({Fmt.Num((1 - alpha) * 100, 0)}%; k = {Fmt.Int(k)}, df = {Fmt.Int(dfW)}) = {Fmt.Num(qKrit, 4)}"),
                    ("Jumlah pasangan", $"{Fmt.Int(k)} kelompok → {Fmt.Int(tukey.Count)} pasangan, "
                                        + "bukan " + Fmt.Int(k * (k - 1)) + " uji terpisah; koreksinya lewat q, bukan lewat α"),
                    ("Contoh pasangan pertama",
                     tukey.Count > 0
                        ? $"q = |{Fmt.Num(tukey[0].Beda, 3)}| / {Fmt.Num(tukey[0].Se, 3)} = {Fmt.Num(tukey[0].Q, 4)}"
                          + $"   →   p = {Fmt.P(tukey[0].P)}"
                        : "tidak ada pasangan yang diuji")));
            }

            var spec = new ChartSpec
            {
                Kind = ChartKind.BoxPlot, Title = $"Box plot — {valueVar} per {groupVar}",
                XTitle = groupVar, YTitle = valueVar
            };
            foreach (var kv in h.Grup) spec.Series[kv.Key] = kv.Value;
            blocks.Add(Blocks.Chart(spec.Title, spec));

            return blocks;
        }

        public sealed class BarisTukey
        {
            public string Kiri = "", Kanan = "";
            public double Beda, Se, Q, P, Bawah, Atas;
            public bool Signifikan;

            public double QKrit;
        }

        public static List<BarisTukey> TukeyHsd(Dictionary<string, List<double>> groups,
                                                double msWithin, int dfWithin, double alpha = 0.05)
        {
            var keys = groups.Keys.ToList();
            var hasil = new List<BarisTukey>();
            if (keys.Count < 2 || dfWithin <= 0 || msWithin <= 0) return hasil;

            double qKrit = Distributions.StudentizedRangeInv(1 - alpha, keys.Count, dfWithin);

            for (int i = 0; i < keys.Count; i++)
            {
                for (int j = i + 1; j < keys.Count; j++)
                {
                    var a = groups[keys[i]];
                    var b = groups[keys[j]];
                    double beda = a.Average() - b.Average();
                    double se = Math.Sqrt(msWithin / 2.0 * (1.0 / a.Count + 1.0 / b.Count));
                    double q = se > 0 ? Math.Abs(beda) / se : double.NaN;
                    double p = double.IsNaN(q) ? double.NaN
                             : 1.0 - Distributions.StudentizedRangeCdf(q, keys.Count, dfWithin);

                    hasil.Add(new BarisTukey
                    {
                        Kiri = keys[i], Kanan = keys[j],
                        Beda = beda, Se = se, Q = q, P = p,
                        Bawah = beda - qKrit * se, Atas = beda + qKrit * se,
                        Signifikan = p < alpha, QKrit = qKrit
                    });
                }
            }
            return hasil;
        }

        private static (double F, int Df1, double Df2, double P) Welch(IEnumerable<List<double>> groups)
        {
            var g = groups.Where(x => x.Count > 1).ToList();
            int k = g.Count;
            if (k < 2) return (double.NaN, 0, double.NaN, double.NaN);

            var w = g.Select(x => x.Count / Variance(x)).ToList();
            var m = g.Select(x => x.Average()).ToList();
            double sumW = w.Sum();
            double grand = 0;
            for (int i = 0; i < k; i++) grand += w[i] * m[i];
            grand /= sumW;

            double numerator = 0, denominatorTerm = 0;
            for (int i = 0; i < k; i++)
            {
                numerator += w[i] * Math.Pow(m[i] - grand, 2);
                denominatorTerm += Math.Pow(1 - w[i] / sumW, 2) / (g[i].Count - 1);
            }
            numerator /= (k - 1);

            double denom = 1 + (2.0 * (k - 2) / (k * k - 1)) * denominatorTerm;
            double f = numerator / denom;
            double df2 = (k * k - 1) / (3.0 * denominatorTerm);
            double p = Distributions.FUpper(f, k - 1, df2);
            return (f, k - 1, df2, p);
        }

        private static double Variance(List<double> x)
        {
            double mean = x.Average();
            return x.Sum(v => (v - mean) * (v - mean)) / (x.Count - 1);
        }

        private static string LabelEta(double v)
        {
            if (double.IsNaN(v)) return "tidak dapat dihitung";
            if (v < 0.01) return "sangat kecil";
            if (v < 0.06) return "kecil";
            if (v < 0.14) return "sedang";
            return "besar";
        }
    }
}
