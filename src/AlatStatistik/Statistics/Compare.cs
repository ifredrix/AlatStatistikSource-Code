using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Compare
    {
        

        public static List<ResultBlock> OneSampleT(Dataset ds, string name, double mu, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading($"Uji-t satu sampel — {name}", 1) };
            var x = Descriptives.CleanNumbers(ds, name);
            if (x.Count < 2)
            {
                blocks.Add(Blocks.Note("Butuh sedikitnya 2 amatan.", NoteKind.Error));
                return blocks;
            }

            var s = Descriptives.Summarize(x);
            double t = (s.Mean - mu) / s.Sem;
            int df = s.N - 1;
            double p = Distributions.StudentTTwoSided(t, df);
            double tCrit = Distributions.StudentTInv(1 - alpha / 2.0, df);
            double diff = s.Mean - mu;
            double d = s.Sd > 0 ? diff / s.Sd : double.NaN;

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.UjiTSatuSampel, DaftarRumus.Rerata, DaftarRumus.RagamSampel));
            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Rerata", $"x̄ = Σxᵢ / n = {Fmt.Num(x.Sum())} / {Fmt.Int(s.N)} = {Fmt.Num(s.Mean)}"),
                ("Simpangan baku", $"s = √[ Σ(xᵢ − x̄)² / (n − 1) ] = {Fmt.Num(s.Sd)}"),
                ("Galat baku rerata", $"SE = s / √n = {Fmt.Num(s.Sd)} / √{Fmt.Int(s.N)} = {Fmt.Num(s.Sem)}"),
                ("Statistik t", $"t = (x̄ − μ₀) / (s / √n) = ({Fmt.Num(s.Mean)} − {Fmt.Num(mu)}) / {Fmt.Num(s.Sem)} = {Fmt.Num(t, 3)}"),
                ("Derajat bebas & p", $"df = n − 1 = {Fmt.Int(df)}   →   p = {Fmt.P(p)}")));

            blocks.Add(Blocks.Table(
                "Statistik satu sampel",
                new[] { "N", "Rerata", "Simpangan baku", "Galat baku rerata" },
                new[] { new[] { Fmt.Int(s.N), Fmt.Num(s.Mean), Fmt.Num(s.Sd), Fmt.Num(s.Sem) } }));

            blocks.Add(Blocks.Table(
                "Uji terhadap nilai hipotesis",
                new[] { "Nilai uji", "t", "df", "p (2 sisi)", "Beda rerata", "Selang kepercayaan 95%", "Cohen's d" },
                new[]
                {
                    new[]
                    {
                        Fmt.Num(mu), Fmt.Num(t, 3), Fmt.Int(df), Fmt.P(p), Fmt.Num(diff),
                        $"[{Fmt.Num(diff - tCrit * s.Sem)}; {Fmt.Num(diff + tCrit * s.Sem)}]",
                        Fmt.Num(d, 3)
                    }
                },
                $"H0: rerata = {Fmt.Num(mu)}. {Keputusan(p, alpha)} Besaran efek d: {LabelD(d)}."));

            return blocks;
        }

        public sealed class HasilT
        {
            public string KunciA = "";
            public string KunciB = "";
            public List<double> A = new();
            public List<double> B = new();

            public double MeanA = double.NaN, MeanB = double.NaN;
            public double SdA = double.NaN, SdB = double.NaN;
            public double SemA = double.NaN, SemB = double.NaN;
            public double VarA = double.NaN, VarB = double.NaN;

            public double LeveneF = double.NaN, LeveneP = double.NaN;
            public bool RagamSama;

            public double Diff = double.NaN;
            public double Sp2 = double.NaN, Sp = double.NaN;

            public double SePooled = double.NaN, TPooled = double.NaN, PPooled = double.NaN;
            public int DfPooled;
            public double TCritP = double.NaN;

            public double SeWelch = double.NaN, TWelch = double.NaN, PWelch = double.NaN;
            public double DfWelch = double.NaN, TCritW = double.NaN;

            public double CohenD = double.NaN;
        }

        public static HasilT? HitungT(Dataset ds, string valueVar, string groupVar, double alpha = 0.05)
        {
            var groups = GroupValues(ds, valueVar, groupVar);
            if (groups.Count != 2) return null;

            var keys = groups.Keys.ToList();
            var a = groups[keys[0]];
            var b = groups[keys[1]];
            if (a.Count < 2 || b.Count < 2) return null;

            var sa = Descriptives.Summarize(a);
            var sb = Descriptives.Summarize(b);

            var h = new HasilT
            {
                KunciA = keys[0], KunciB = keys[1], A = a, B = b,
                MeanA = sa.Mean, MeanB = sb.Mean,
                SdA = sa.Sd, SdB = sb.Sd,
                SemA = sa.Sem, SemB = sb.Sem,
                VarA = sa.Variance, VarB = sb.Variance
            };

            h.LeveneF = Levene(new[] { a, b }, out double leveneP);
            h.LeveneP = leveneP;
            h.RagamSama = leveneP >= alpha;

            h.Diff = sa.Mean - sb.Mean;

            h.DfPooled = sa.N + sb.N - 2;
            h.Sp2 = ((sa.N - 1) * sa.Variance + (sb.N - 1) * sb.Variance) / h.DfPooled;
            h.SePooled = Math.Sqrt(h.Sp2 * (1.0 / sa.N + 1.0 / sb.N));
            h.TPooled = h.Diff / h.SePooled;
            h.PPooled = Distributions.StudentTTwoSided(h.TPooled, h.DfPooled);
            h.TCritP = Distributions.StudentTInv(1 - alpha / 2.0, h.DfPooled);

            double v1 = sa.Variance / sa.N, v2 = sb.Variance / sb.N;
            h.SeWelch = Math.Sqrt(v1 + v2);
            h.TWelch = h.Diff / h.SeWelch;
            h.DfWelch = Math.Pow(v1 + v2, 2) /
                        (v1 * v1 / (sa.N - 1) + v2 * v2 / (sb.N - 1));
            h.PWelch = Distributions.StudentTTwoSided(h.TWelch, h.DfWelch);
            h.TCritW = Distributions.StudentTInv(1 - alpha / 2.0, h.DfWelch);

            h.Sp = Math.Sqrt(h.Sp2);
            h.CohenD = h.Sp > 0 ? h.Diff / h.Sp : double.NaN;
            return h;
        }

        public static List<ResultBlock> IndependentT(Dataset ds, string valueVar, string groupVar, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading($"Uji-t dua sampel bebas — {valueVar} menurut {groupVar}", 1)
            };

            var h = HitungT(ds, valueVar, groupVar, alpha);
            if (h == null)
            {
                var groups = GroupValues(ds, valueVar, groupVar);
                if (groups.Count != 2)
                {
                    blocks.Add(Blocks.Note(
                        $"Variabel pengelompokan '{groupVar}' harus punya tepat 2 kategori, "
                        + $"ditemukan {groups.Count}.", NoteKind.Error));
                }
                else
                {
                    blocks.Add(Blocks.Note("Setiap kelompok butuh sedikitnya 2 amatan.", NoteKind.Error));
                }
                return blocks;
            }

            var keys = new[] { h.KunciA, h.KunciB };
            var a = h.A;
            var b = h.B;

            var sa = Descriptives.Summarize(a);
            var sb = Descriptives.Summarize(b);

            double leveneF = h.LeveneF, leveneP = h.LeveneP;
            bool equalVariance = h.RagamSama;
            double diff = h.Diff;
            double sp2 = h.Sp2, sp = h.Sp;
            double sePooled = h.SePooled, tPooled = h.TPooled;
            int dfPooled = h.DfPooled;
            double pPooled = h.PPooled, tCritP = h.TCritP;
            double seWelch = h.SeWelch, tWelch = h.TWelch, dfWelch = h.DfWelch;
            double pWelch = h.PWelch, tCritW = h.TCritW;
            double cohenD = h.CohenD;

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.UjiTStudent, DaftarRumus.UjiTWelch, DaftarRumus.UjiLevene));

            
            
            
            blocks.Add(Blocks.Substitusi($"Pemasukan nilai — '{keys[0]}' lawan '{keys[1]}'",
                ("Beda rerata", $"x̄₁ − x̄₂ = {Fmt.Num(sa.Mean)} − {Fmt.Num(sb.Mean)} = {Fmt.Num(diff)}"),
                ("Ragam gabungan (Student)",
                 $"sₚ² = ((n₁−1)s₁² + (n₂−1)s₂²) / (n₁+n₂−2) = (({Fmt.Int(sa.N - 1)})·{Fmt.Num(sa.Variance)} "
                 + $"+ ({Fmt.Int(sb.N - 1)})·{Fmt.Num(sb.Variance)}) / {Fmt.Int(dfPooled)} = {Fmt.Num(sp2)}"
                 + $"   →   sₚ = {Fmt.Num(sp)}"),
                ("Galat baku beda (Student)",
                 $"SE = sₚ · √(1/n₁ + 1/n₂) = {Fmt.Num(sp)} · √(1/{Fmt.Int(sa.N)} + 1/{Fmt.Int(sb.N)}) = {Fmt.Num(sePooled)}"),
                ("t Student", $"t = {Fmt.Num(diff)} / {Fmt.Num(sePooled)} = {Fmt.Num(tPooled, 3)}"
                              + $"   df = {Fmt.Int(dfPooled)}   →   p = {Fmt.P(pPooled)}"),
                ("Galat baku beda (Welch)",
                 $"SE = √(s₁²/n₁ + s₂²/n₂) = √({Fmt.Num(sa.Variance)}/{Fmt.Int(sa.N)} + {Fmt.Num(sb.Variance)}/{Fmt.Int(sb.N)}) = {Fmt.Num(seWelch)}"),
                ("t Welch", $"t = {Fmt.Num(diff)} / {Fmt.Num(seWelch)} = {Fmt.Num(tWelch, 3)}"
                            + $"   df = {Fmt.Num(dfWelch, 1)}   →   p = {Fmt.P(pWelch)}"),
                ("Kesamaan ragam (Levene)",
                 $"F = {Fmt.Num(leveneF, 3)}   p = {Fmt.P(leveneP)}   →   "
                 + (equalVariance ? "ragam dianggap sama, pakai baris Student"
                                  : "ragam berbeda, pakai baris Welch"))));

            blocks.Add(Blocks.Table(
                "Statistik kelompok",
                new[] { "Kelompok", "N", "Rerata", "Simpangan baku", "Galat baku rerata" },
                new[]
                {
                    new[] { keys[0], Fmt.Int(sa.N), Fmt.Num(sa.Mean), Fmt.Num(sa.Sd), Fmt.Num(sa.Sem) },
                    new[] { keys[1], Fmt.Int(sb.N), Fmt.Num(sb.Mean), Fmt.Num(sb.Sd), Fmt.Num(sb.Sem) }
                }));

            blocks.Add(Blocks.Table(
                "Uji kesamaan ragam (Levene)",
                new[] { "F", "df1", "df2", "p", "Kesimpulan" },
                new[]
                {
                    new[]
                    {
                        Fmt.Num(leveneF, 3), "1", Fmt.Int(sa.N + sb.N - 2), Fmt.P(leveneP),
                        equalVariance ? "Ragam dapat dianggap sama" : "Ragam berbeda — pakai baris Welch"
                    }
                },
                "H0: ragam kedua kelompok sama."));

            blocks.Add(Blocks.Table(
                "Uji-t terhadap beda rerata",
                new[] { "Jenis uji", "t", "df", "p (2 sisi)", "Beda rerata", "Galat baku beda", "Selang kepercayaan 95%" },
                new[]
                {
                    new[]
                    {
                        "Student (ragam sama)", Fmt.Num(tPooled, 3), Fmt.Int(dfPooled), Fmt.P(pPooled),
                        Fmt.Num(diff), Fmt.Num(sePooled),
                        $"[{Fmt.Num(diff - tCritP * sePooled)}; {Fmt.Num(diff + tCritP * sePooled)}]"
                    },
                    new[]
                    {
                        "Welch (ragam beda)", Fmt.Num(tWelch, 3), Fmt.Num(dfWelch, 1), Fmt.P(pWelch),
                        Fmt.Num(diff), Fmt.Num(seWelch),
                        $"[{Fmt.Num(diff - tCritW * seWelch)}; {Fmt.Num(diff + tCritW * seWelch)}]"
                    }
                },
                $"H0: rerata kedua kelompok sama. Baris yang dianjurkan: "
                + (equalVariance ? "Student" : "Welch")
                + $". Besaran efek Cohen's d = {Fmt.Num(cohenD, 3)} ({LabelD(cohenD)})."));

            var spec = new ChartSpec
            {
                Kind = ChartKind.BoxPlot, Title = $"Box plot — {valueVar} per {groupVar}",
                XTitle = groupVar, YTitle = valueVar
            };
            spec.Series[keys[0]] = a;
            spec.Series[keys[1]] = b;
            blocks.Add(Blocks.Chart(spec.Title, spec));

            return blocks;
        }

        public static List<ResultBlock> PairedT(Dataset ds, string varA, string varB, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading($"Uji-t berpasangan — {varA} & {varB}", 1) };

            var (xa, xb) = PairedNumbers(ds, varA, varB);
            if (xa.Count < 2)
            {
                blocks.Add(Blocks.Note("Butuh sedikitnya 2 pasang amatan yang lengkap.", NoteKind.Error));
                return blocks;
            }

            var d = xa.Zip(xb, (p, q) => p - q).ToList();
            var sd = Descriptives.Summarize(d);
            var sa = Descriptives.Summarize(xa);
            var sb = Descriptives.Summarize(xb);

            int df = sd.N - 1;
            double t = sd.Mean / sd.Sem;
            double p = Distributions.StudentTTwoSided(t, df);
            double tCrit = Distributions.StudentTInv(1 - alpha / 2.0, df);
            double dz = sd.Sd > 0 ? sd.Mean / sd.Sd : double.NaN;
            double r = Correlation.Pearson(xa, xb);

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.UjiTPasangan, DaftarRumus.Rerata, DaftarRumus.RagamSampel));
            blocks.Add(Blocks.Substitusi($"Pemasukan nilai — '{varA}' lawan '{varB}'",
                ("Beda tiap pasangan", $"dᵢ = x₁ᵢ − x₂ᵢ   →   n = {Fmt.Int(sd.N)} pasang lengkap"),
                ("Rerata beda", $"d̄ = Σdᵢ / n = {Fmt.Num(d.Sum())} / {Fmt.Int(sd.N)} = {Fmt.Num(sd.Mean)}"),
                ("Simpangan baku beda", $"s_d = √[ Σ(dᵢ − d̄)² / (n − 1) ] = {Fmt.Num(sd.Sd)}"),
                ("Galat baku", $"SE = s_d / √n = {Fmt.Num(sd.Sd)} / √{Fmt.Int(sd.N)} = {Fmt.Num(sd.Sem)}"),
                ("Statistik t", $"t = d̄ / (s_d / √n) = {Fmt.Num(sd.Mean)} / {Fmt.Num(sd.Sem)} = {Fmt.Num(t, 3)}"),
                ("Derajat bebas & p", $"df = n − 1 = {Fmt.Int(df)}   →   p = {Fmt.P(p)}")));

            blocks.Add(Blocks.Table(
                "Statistik pasangan",
                new[] { "Variabel", "N", "Rerata", "Simpangan baku", "Galat baku rerata" },
                new[]
                {
                    new[] { varA, Fmt.Int(sa.N), Fmt.Num(sa.Mean), Fmt.Num(sa.Sd), Fmt.Num(sa.Sem) },
                    new[] { varB, Fmt.Int(sb.N), Fmt.Num(sb.Mean), Fmt.Num(sb.Sd), Fmt.Num(sb.Sem) }
                }));

            blocks.Add(Blocks.Table(
                "Korelasi pasangan",
                new[] { "N", "Korelasi Pearson", "p" },
                new[]
                {
                    new[]
                    {
                        Fmt.Int(sa.N), Fmt.Num(r, 3),
                        Fmt.P(Distributions.StudentTTwoSided(
                            r * Math.Sqrt((sa.N - 2) / Math.Max(1e-12, 1 - r * r)), sa.N - 2))
                    }
                }));

            blocks.Add(Blocks.Table(
                "Uji terhadap beda pasangan",
                new[] { "Rerata beda", "Simpangan baku", "Galat baku", "t", "df", "p (2 sisi)", "Selang kepercayaan 95%", "Cohen's dz" },
                new[]
                {
                    new[]
                    {
                        Fmt.Num(sd.Mean), Fmt.Num(sd.Sd), Fmt.Num(sd.Sem), Fmt.Num(t, 3), Fmt.Int(df),
                        Fmt.P(p), $"[{Fmt.Num(sd.Mean - tCrit * sd.Sem)}; {Fmt.Num(sd.Mean + tCrit * sd.Sem)}]",
                        Fmt.Num(dz, 3)
                    }
                },
                $"H0: rerata beda = 0. {Keputusan(p, alpha)}"));

            return blocks;
        }

        

        public static List<ResultBlock> MannWhitney(Dataset ds, string valueVar, string groupVar, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading($"Mann–Whitney U — {valueVar} menurut {groupVar}", 1) };
            var groups = GroupValues(ds, valueVar, groupVar);
            if (groups.Count != 2)
            {
                blocks.Add(Blocks.Note($"Butuh tepat 2 kelompok, ditemukan {groups.Count}.", NoteKind.Error));
                return blocks;
            }

            var keys = groups.Keys.ToList();
            var a = groups[keys[0]];
            var b = groups[keys[1]];

            var uji = MannWhitneyU(a, b);
            int n1 = a.Count, n2 = b.Count;
            double u1 = uji.U, u2 = n1 * n2 - u1;
            double r1 = uji.R1, r2 = uji.R2;
            double sigma = uji.Sigma;
            double mu = uji.Mu;
            double z = uji.Z;
            double p = uji.P;

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.MannWhitney));
            blocks.Add(Blocks.Substitusi($"Pemasukan nilai — '{keys[0]}' lawan '{keys[1]}'",
                ("Jumlah peringkat", $"R₁ = {Fmt.Num(r1, 1)}   n₁ = {Fmt.Int(n1)}   n₂ = {Fmt.Int(n2)}"),
                ("Statistik U", $"U₁ = R₁ − n₁(n₁+1)/2 = {Fmt.Num(r1, 1)} − {Fmt.Num(n1 * (n1 + 1) / 2.0, 1)} = {Fmt.Num(u1, 1)}"),
                ("U kelompok kedua", $"U₂ = n₁n₂ − U₁ = {Fmt.Int(n1 * n2)} − {Fmt.Num(u1, 1)} = {Fmt.Num(u2, 1)}"),
                ("Rerata & simpangan baku U", $"μ_U = n₁n₂/2 = {Fmt.Num(mu, 1)}   σ_U = {Fmt.Num(sigma, 3)}"),
                ("Statistik z", $"z = (U − μ_U ∓ 0,5) / σ_U = ({Fmt.Num(u1, 1)} − {Fmt.Num(mu, 1)}) / {Fmt.Num(sigma, 3)} = {Fmt.Num(z, 3)}"),
                ("Nilai p", $"p = 2 · (1 − Φ(|z|)) = {Fmt.P(p)}")));

            blocks.Add(Blocks.Table(
                "Peringkat",
                new[] { "Kelompok", "N", "Jumlah peringkat", "Rerata peringkat", "U" },
                new[]
                {
                    new[] { keys[0], Fmt.Int(n1), Fmt.Num(r1, 1), Fmt.Num(r1 / n1), Fmt.Num(u1, 1) },
                    new[] { keys[1], Fmt.Int(n2), Fmt.Num(r2, 1), Fmt.Num(r2 / n2), Fmt.Num(u2, 1) }
                }));

            blocks.Add(Blocks.Table(
                "Statistik uji",
                new[] { "Mann–Whitney U", "z", "p (2 sisi)", "Keputusan (α = 0,05)" },
                new[]
                {
                    new[] { Fmt.Num(u1, 1), Fmt.Num(z, 3), Fmt.P(p), Keputusan(p, alpha) }
                },
                "Memakai pendekatan normal dengan koreksi ikatan dan koreksi kesinambungan. "
                + "Uji ini menggantikan uji-t bebas bila asumsi normalitas tidak terpenuhi."));

            return blocks;
        }

        public static List<ResultBlock> Wilcoxon(Dataset ds, string varA, string varB, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading($"Wilcoxon signed-rank — {varA} & {varB}", 1) };
            var (xa, xb) = PairedNumbers(ds, varA, varB);
            var d = xa.Zip(xb, (p, q) => p - q).Where(v => Math.Abs(v) > 1e-12).ToList();

            if (d.Count < 5)
            {
                blocks.Add(Blocks.Note("Butuh sedikitnya 5 pasang dengan beda tidak nol.", NoteKind.Error));
                return blocks;
            }

            var uji = WilcoxonSigned(d);
            int n = d.Count;
            double wPlus = uji.WPlus, wMinus = uji.WMinus;
            double mu = uji.Mu, sigma = uji.Sigma, z = uji.Z, p = uji.P;

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.WilcoxonSigned));
            blocks.Add(Blocks.Substitusi($"Pemasukan nilai — '{varA}' lawan '{varB}'",
                ($"Beda tidak nol", $"n = {Fmt.Int(n)} dari {Fmt.Int(xa.Count)} pasang (beda nol dibuang)"),
                ("Jumlah peringkat", $"W⁺ = {Fmt.Num(wPlus, 1)}   W⁻ = {Fmt.Num(wMinus, 1)}"),
                ("Rerata & simpangan baku", $"μ_W = n(n+1)/4 = {Fmt.Num(mu, 1)}   σ_W = {Fmt.Num(sigma, 3)}"),
                ("Statistik z", $"z = (W⁺ − μ_W ∓ 0,5) / σ_W = ({Fmt.Num(wPlus, 1)} − {Fmt.Num(mu, 1)}) / {Fmt.Num(sigma, 3)} = {Fmt.Num(z, 3)}"),
                ("Nilai p", $"p = 2 · (1 − Φ(|z|)) = {Fmt.P(p)}")));

            blocks.Add(Blocks.Table(
                "Peringkat beda",
                new[] { "Beda", "N", "Jumlah peringkat", "Rerata peringkat" },
                new[]
                {
                    new[] { "Positif", Fmt.Int(d.Count(v => v > 0)), Fmt.Num(wPlus, 1), Fmt.Num(wPlus / Math.Max(1, d.Count(v => v > 0))) },
                    new[] { "Negatif", Fmt.Int(d.Count(v => v < 0)), Fmt.Num(wMinus, 1), Fmt.Num(wMinus / Math.Max(1, d.Count(v => v < 0))) },
                    new[] { "Nol (dibuang)", Fmt.Int(xa.Count - n), Fmt.NA, Fmt.NA }
                }));

            blocks.Add(Blocks.Table(
                "Statistik uji",
                new[] { "W+", "z", "p (2 sisi)", "Keputusan (α = 0,05)" },
                new[] { new[] { Fmt.Num(wPlus, 1), Fmt.Num(z, 3), Fmt.P(p), Keputusan(p, alpha) } },
                "Pendekatan normal dengan koreksi ikatan dan kesinambungan."));

            return blocks;
        }

        public static List<ResultBlock> KruskalWallis(Dataset ds, string valueVar, string groupVar, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading($"Kruskal–Wallis — {valueVar} menurut {groupVar}", 1) };
            var groups = GroupValues(ds, valueVar, groupVar);
            if (groups.Count < 2)
            {
                blocks.Add(Blocks.Note("Butuh sedikitnya 2 kelompok.", NoteKind.Error));
                return blocks;
            }

            var uji = KruskalWallisH(groups.Values);

            var all = groups.Values.SelectMany(v => v).ToList();
            int N = all.Count;
            var ranks = Ranks(all);

            var rows = new List<List<string>>();
            double sumTerm = 0;
            int offset = 0;
            foreach (var kv in groups)
            {
                double rSum = ranks.Skip(offset).Take(kv.Value.Count).Sum();
                rows.Add(new List<string>
                {
                    kv.Key, Fmt.Int(kv.Value.Count), Fmt.Num(rSum / kv.Value.Count), Fmt.Num(rSum, 1)
                });
                sumTerm += rSum * rSum / kv.Value.Count;
                offset += kv.Value.Count;
            }

            double h = uji.H;
            double hCorrected = uji.HTerkoreksi;
            int df = uji.Df;
            double p = uji.P;

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.KruskalWallis));
            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Jumlah amatan", $"N = {Fmt.Int(N)}   k = {Fmt.Int(groups.Count)} kelompok"),
                ("Jumlah Rⱼ²/nⱼ", $"Σ Rⱼ²/nⱼ = {Fmt.Num(sumTerm, 3)}"),
                ("Statistik H", $"H = (12 / (N(N+1))) · Σ(Rⱼ²/nⱼ) − 3(N+1) = {Fmt.Num(h, 3)}"),
                ("Koreksi ikatan", $"H_terkoreksi = {Fmt.Num(hCorrected, 3)}   df = k − 1 = {Fmt.Int(df)}"),
                ("Nilai p", $"p = P(χ²_{Fmt.Int(df)} ≥ H) = {Fmt.P(p)}")));

            blocks.Add(Blocks.Table("Peringkat per kelompok",
                new[] { "Kelompok", "N", "Rerata peringkat", "Jumlah peringkat" }, rows));

            blocks.Add(Blocks.Table(
                "Statistik uji",
                new[] { "Kruskal–Wallis H", "df", "p", "Keputusan (α = 0,05)" },
                new[] { new[] { Fmt.Num(hCorrected, 3), Fmt.Int(df), Fmt.P(p), Keputusan(p, alpha) } },
                "Sudah dikoreksi terhadap ikatan. Alternatif nonparametrik untuk ANOVA satu arah."));

            return blocks;
        }

        
        
        
        
        
        
        
        
        

        public readonly struct UjiMannWhitney
        {
            public double U { get; init; }
            public double R1 { get; init; }
            public double R2 { get; init; }
            public double Mu { get; init; }
            public double Sigma { get; init; }
            public double Z { get; init; }
            public double P { get; init; }
        }

        public static UjiMannWhitney MannWhitneyU(List<double> a, List<double> b)
        {
            int n1 = a.Count, n2 = b.Count, N = n1 + n2;

            var all = a.Concat(b).ToList();
            var ranks = Ranks(all);
            double r1 = ranks.Take(n1).Sum();
            double u1 = r1 - n1 * (n1 + 1) / 2.0;

            double tie = TieCorrection(all);
            double mu = n1 * n2 / 2.0;
            double sigma = Math.Sqrt(n1 * n2 / 12.0 * ((N + 1) - tie / (N * (N - 1.0))));
            double z = (u1 - mu + (u1 < mu ? 0.5 : -0.5)) / sigma;
            double p = 2.0 * (1.0 - Distributions.NormalCdf(Math.Abs(z)));

            return new UjiMannWhitney
            {
                U = u1, R1 = r1, R2 = ranks.Skip(n1).Sum(),
                Mu = mu, Sigma = sigma, Z = z, P = p
            };
        }

        public readonly struct UjiWilcoxon
        {
            public double WPlus { get; init; }
            public double WMinus { get; init; }
            public double Mu { get; init; }
            public double Sigma { get; init; }
            public double Z { get; init; }
            public double P { get; init; }
        }

        public static UjiWilcoxon WilcoxonSigned(List<double> beda)
        {
            var abs = beda.Select(Math.Abs).ToList();
            var ranks = Ranks(abs);
            double wPlus = 0, wMinus = 0;
            for (int i = 0; i < beda.Count; i++)
            {
                if (beda[i] > 0) wPlus += ranks[i]; else wMinus += ranks[i];
            }

            int n = beda.Count;
            double tie = TieCorrection(abs);
            double mu = n * (n + 1) / 4.0;
            double variance = n * (n + 1.0) * (2.0 * n + 1) / 24.0 - tie / 48.0;
            double sigma = Math.Sqrt(variance);
            double z = (wPlus - mu + (wPlus < mu ? 0.5 : -0.5)) / sigma;
            double p = 2.0 * (1.0 - Distributions.NormalCdf(Math.Abs(z)));

            return new UjiWilcoxon { WPlus = wPlus, WMinus = wMinus, Mu = mu, Sigma = sigma, Z = z, P = p };
        }

        public readonly struct UjiKruskalWallis
        {
            public double H { get; init; }
            public double HTerkoreksi { get; init; }
            public int Df { get; init; }
            public double P { get; init; }
        }

        public static UjiKruskalWallis KruskalWallisH(IEnumerable<List<double>> groups)
        {
            var daftar = groups.ToList();
            var all = daftar.SelectMany(v => v).ToList();
            int N = all.Count;
            var ranks = Ranks(all);

            double sumTerm = 0;
            int offset = 0;
            foreach (var g in daftar)
            {
                double rSum = ranks.Skip(offset).Take(g.Count).Sum();
                sumTerm += rSum * rSum / g.Count;
                offset += g.Count;
            }

            double h = 12.0 / (N * (N + 1.0)) * sumTerm - 3.0 * (N + 1.0);
            double tie = TieCorrection(all);
            double hCorrected = h / (1.0 - tie / (Math.Pow(N, 3) - N));
            int df = daftar.Count - 1;

            return new UjiKruskalWallis
            {
                H = h, HTerkoreksi = hCorrected, Df = df,
                P = Distributions.ChiSquareUpper(hCorrected, df)
            };
        }

        

        public static Dictionary<string, List<double>> GroupValues(Dataset ds, string valueVar, string groupVar)
        {
            var values = ds.Numeric(valueVar);
            var labels = ds.Text(groupVar);
            var result = new Dictionary<string, List<double>>();

            for (int i = 0; i < labels.Length && i < values.Length; i++)
            {
                if (!values[i].HasValue || string.IsNullOrWhiteSpace(labels[i])) continue;
                string key = labels[i]!;
                if (!result.TryGetValue(key, out var list)) result[key] = list = new List<double>();
                list.Add(values[i]!.Value);
            }
            return result;
        }

        public static (List<double> A, List<double> B) PairedNumbers(Dataset ds, string varA, string varB)
        {
            var a = ds.Numeric(varA);
            var b = ds.Numeric(varB);
            var ra = new List<double>();
            var rb = new List<double>();
            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                if (a[i].HasValue && b[i].HasValue) { ra.Add(a[i]!.Value); rb.Add(b[i]!.Value); }
            }
            return (ra, rb);
        }

        public static List<double> Ranks(List<double> values)
        {
            int n = values.Count;
            var order = Enumerable.Range(0, n).OrderBy(i => values[i]).ToList();
            var ranks = new double[n];

            int i2 = 0;
            while (i2 < n)
            {
                int j = i2;
                while (j < n - 1 && Math.Abs(values[order[j + 1]] - values[order[i2]]) < 1e-12) j++;
                double avgRank = (i2 + j + 2) / 2.0;     
                for (int k = i2; k <= j; k++) ranks[order[k]] = avgRank;
                i2 = j + 1;
            }
            return ranks.ToList();
        }

        public static double TieCorrection(List<double> values)
        {
            var counts = values.GroupBy(v => Math.Round(v, 10)).Select(g => g.Count()).Where(c => c > 1);
            return counts.Sum(t => Math.Pow(t, 3) - t);
        }

        public enum PusatLevene
        {

            Rerata,

            Median,

            RerataTerpangkas
        }

        public static double RerataTerpangkas(List<double> nilai, double proporsi = 0.05)
        {
            if (nilai.Count == 0) return double.NaN;
            var s = nilai.OrderBy(v => v).ToList();
            double x = proporsi * s.Count;
            int k = (int)Math.Floor(x);
            double f = x - k;
            if (s.Count - 2 * k - 2 < 0) return s.Average();   

            double jumlah = 0, bobot = 0;
            for (int i = k + 1; i < s.Count - k - 1; i++) { jumlah += s[i]; bobot += 1; }
            double sisa = 1 - f;
            jumlah += sisa * (s[k] + s[s.Count - 1 - k]);
            bobot += 2 * sisa;
            return bobot > 0 ? jumlah / bobot : double.NaN;
        }

        public static double LevenePusat(IEnumerable<List<double>> groups, PusatLevene pusat,
                                        out double p, double proporsiPangkas = 0.05)
        {
            var g = groups.Where(x => x.Count > 0).ToList();
            int k = g.Count;
            int N = g.Sum(x => x.Count);
            if (k < 2 || N - k <= 0) { p = double.NaN; return double.NaN; }

            var titik = new List<double>();
            for (int i = 0; i < k; i++)
            {
                titik.Add(pusat switch
                {
                    PusatLevene.Median => Descriptives.Quantile(g[i].OrderBy(v => v).ToList(), 0.5),
                    PusatLevene.RerataTerpangkas => RerataTerpangkas(g[i], proporsiPangkas),
                    _ => g[i].Average()
                });
            }

            var absDev = new List<List<double>>();
            for (int i = 0; i < k; i++)
                absDev.Add(g[i].Select(v => Math.Abs(v - titik[i])).ToList());

            var groupMeans = absDev.Select(x => x.Average()).ToList();
            double overall = absDev.SelectMany(x => x).Average();

            double numerator = 0, denominator = 0;
            for (int i = 0; i < k; i++)
            {
                numerator += g[i].Count * Math.Pow(groupMeans[i] - overall, 2);
                denominator += absDev[i].Sum(v => Math.Pow(v - groupMeans[i], 2));
            }

            if (denominator <= 0) { p = double.NaN; return double.NaN; }

            double f = (N - k) / (double)(k - 1) * numerator / denominator;
            p = Distributions.FUpper(f, k - 1, N - k);
            return f;
        }

        public static double Levene(IEnumerable<List<double>> groups, out double p)
            => LevenePusat(groups, PusatLevene.Rerata, out p);

        public static string Keputusan(double p, double alpha)
            => double.IsNaN(p) ? "Tidak dapat dihitung."
             : p < alpha ? $"H0 ditolak pada α = {Fmt.Num(alpha * 100, 0)}%."
             : $"H0 tidak ditolak pada α = {Fmt.Num(alpha * 100, 0)}%.";

        public static string LabelD(double d)
        {
            double a = Math.Abs(d);
            if (double.IsNaN(a)) return "tidak dapat dihitung";
            if (a < 0.2) return "sangat kecil";
            if (a < 0.5) return "kecil";
            if (a < 0.8) return "sedang";
            return "besar";
        }
    }
}
