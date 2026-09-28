using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public class HasilParsial
    {
        public double R;              
        public double RSederhana;     
        public double T;
        public double P;
        public int N;
        public int K;                 
        public int Df;                
    }

    public static class Correlation
    {

        public static double Pearson(List<double> x, List<double> y)
        {
            int n = Math.Min(x.Count, y.Count);
            if (n < 2) return double.NaN;

            double mx = x.Average(), my = y.Average();
            double sxy = 0, sxx = 0, syy = 0;
            for (int i = 0; i < n; i++)
            {
                double dx = x[i] - mx, dy = y[i] - my;
                sxy += dx * dy; sxx += dx * dx; syy += dy * dy;
            }
            if (sxx <= 0 || syy <= 0) return double.NaN;
            return sxy / Math.Sqrt(sxx * syy);
        }

        public static double PearsonP(double r, int n)
        {
            if (double.IsNaN(r) || n < 3) return double.NaN;
            double denom = 1 - r * r;
            if (denom <= 0) return 0.0;
            double t = r * Math.Sqrt((n - 2) / denom);
            return Distributions.StudentTTwoSided(t, n - 2);
        }

        public static HasilParsial? Parsial(List<double> x, List<double> y, List<List<double>>? kontrol)
        {
            var z = kontrol ?? new List<List<double>>();
            if (x.Count != y.Count) return null;

            int n = x.Count;
            int k = z.Count;
            if (z.Any(v => v.Count != n)) return null;
            if (n < k + 3) return null;

            List<double> ex, ey;
            if (k == 0)
            {
                
                ex = x; ey = y;
            }
            else
            {
                
                var prediktor = z.Select(v => v.ToArray()).ToList();
                var modelX = Regression.Fit(x.ToArray(), prediktor);
                var modelY = Regression.Fit(y.ToArray(), prediktor);
                if (modelX is null || modelY is null) return null;

                ex = modelX.Residuals.ToList();
                ey = modelY.Residuals.ToList();
            }

            double r = Pearson(ex, ey);
            if (double.IsNaN(r)) return null;

            int df = n - 2 - k;
            double denom = 1 - r * r;
            double t = denom > 0 ? r * Math.Sqrt(df / denom) : double.NaN;
            double p = double.IsNaN(t) ? double.NaN : Distributions.StudentTTwoSided(t, df);

            return new HasilParsial
            {
                R = r,
                RSederhana = Pearson(x, y),
                T = t,
                P = p,
                N = n,
                K = k,
                Df = df
            };
        }

        public static double Spearman(List<double> x, List<double> y)
            => Pearson(Compare.Ranks(x), Compare.Ranks(y));

        public static double KendallTauB(List<double> x, List<double> y, out double p)
        {
            int n = Math.Min(x.Count, y.Count);
            p = double.NaN;
            if (n < 2) return double.NaN;

            int concordant = 0, discordant = 0, tiesX = 0, tiesY = 0;

            for (int i = 0; i < n - 1; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    double dx = x[i] - x[j], dy = y[i] - y[j];
                    bool sameX = Math.Abs(dx) < 1e-12, sameY = Math.Abs(dy) < 1e-12;

                    if (sameX && sameY) { tiesX++; tiesY++; continue; }
                    if (sameX) { tiesX++; continue; }
                    if (sameY) { tiesY++; continue; }

                    if (dx * dy > 0) concordant++; else discordant++;
                }
            }

            double denom = Math.Sqrt((concordant + discordant + tiesX) * (double)(concordant + discordant + tiesY));
            if (denom == 0) return double.NaN;

            double tau = (concordant - discordant) / denom;
            double z = 3.0 * tau * Math.Sqrt(n * (n - 1.0)) / Math.Sqrt(2.0 * (2.0 * n + 5));
            p = 2.0 * (1.0 - Distributions.NormalCdf(Math.Abs(z)));
            return tau;
        }

        private static List<LangkahHitung> LangkahKorelasi(string method, List<string> vars,
                                                           List<List<double>> clean, int n)
        {
            var langkah = new List<LangkahHitung>();
            var a = clean[0];
            var b = clean[1];
            string pasangan = $"{vars[0]} ↔ {vars[1]}";

            double mx = a.Average(), my = b.Average();
            double sxy = 0, sxx = 0, syy = 0;
            for (int i = 0; i < n; i++)
            {
                sxy += (a[i] - mx) * (b[i] - my);
                sxx += (a[i] - mx) * (a[i] - mx);
                syy += (b[i] - my) * (b[i] - my);
            }

            langkah.Add(new LangkahHitung { Uraian = "Pasangan", Hitungan = pasangan + $"   n = {Fmt.Int(n)}" });
            langkah.Add(new LangkahHitung
            {
                Uraian = "Rerata",
                Hitungan = $"x̄ = {Fmt.Num(mx)}   ȳ = {Fmt.Num(my)}"
            });

            switch (method)
            {
                case "spearman":
                    var ra = Compare.Ranks(a);
                    var rb = Compare.Ranks(b);
                    double dd = 0;
                    for (int i = 0; i < n; i++) dd += Math.Pow(ra[i] - rb[i], 2);
                    double rho = Spearman(a, b);
                    langkah.Add(new LangkahHitung
                    {
                        Uraian = "Peringkat & Σd²",
                        Hitungan = $"Σdᵢ² = {Fmt.Num(dd, 1)}   (dᵢ = peringkat x − peringkat y)"
                    });
                    langkah.Add(new LangkahHitung
                    {
                        Uraian = "Koefisien ρ",
                        Hitungan = $"ρ = 1 − 6·{Fmt.Num(dd, 1)} / ({Fmt.Int(n)}({Fmt.Int(n)}² − 1)) = {Fmt.Num(rho, 3)}"
                                   + "   (nilai akhir dihitung sebagai Pearson atas peringkat, benar walau ada ikatan)"
                    });
                    langkah.Add(new LangkahHitung
                    {
                        Uraian = "Nilai p",
                        Hitungan = $"p = {Fmt.P(PearsonP(rho, n))}"
                    });
                    break;

                case "kendall":
                    double tau = KendallTauB(a, b, out double pk);
                    int jumlahPasang = n * (n - 1) / 2;
                    langkah.Add(new LangkahHitung
                    {
                        Uraian = "Pasangan berurutan",
                        Hitungan = $"n(n − 1)/2 = {Fmt.Int(jumlahPasang)} pasang dibandingkan"
                    });
                    langkah.Add(new LangkahHitung
                    {
                        Uraian = "Koefisien τ-b",
                        Hitungan = $"τ = (C − D) / √[(C + D + Tₓ)(C + D + Tᵧ)] = {Fmt.Num(tau, 3)}"
                    });
                    langkah.Add(new LangkahHitung
                    {
                        Uraian = "Nilai p",
                        Hitungan = $"p ≈ {Fmt.P(pk)}  (pendekatan normal)"
                    });
                    break;

                default:
                    double r = sxy / Math.Sqrt(sxx * syy);
                    langkah.Add(new LangkahHitung
                    {
                        Uraian = "Jumlah kuadrat & hasil kali",
                        Hitungan = $"Σ(xᵢ − x̄)(yᵢ − ȳ) = {Fmt.Num(sxy)}   "
                                   + $"Σ(xᵢ − x̄)² = {Fmt.Num(sxx)}   Σ(yᵢ − ȳ)² = {Fmt.Num(syy)}"
                    });
                    langkah.Add(new LangkahHitung
                    {
                        Uraian = "Koefisien r",
                        Hitungan = $"r = {Fmt.Num(sxy)} / √({Fmt.Num(sxx)} · {Fmt.Num(syy)}) = {Fmt.Num(r, 3)}"
                    });
                    langkah.Add(new LangkahHitung
                    {
                        Uraian = "Nilai p",
                        Hitungan = $"t = r·√((n − 2)/(1 − r²))   →   p = {Fmt.P(PearsonP(r, n))}"
                    });
                    break;
            }

            return langkah;
        }

        public static List<ResultBlock> ParsialBlocks(Dataset ds, string varX, string varY,
                                                      IEnumerable<string>? kontrol = null,
                                                      double alpha = 0.05)
        {
            var z = (kontrol ?? Enumerable.Empty<string>()).ToList();
            var blok = new List<ResultBlock> { Blocks.Heading("Korelasi parsial") };

            var butuh = new List<string> { varX, varY };
            butuh.AddRange(z);

            if (ds.IndexOf(varX) < 0 || ds.IndexOf(varY) < 0 || z.Any(v => ds.IndexOf(v) < 0))
            {
                blok.Add(Blocks.Note("Variabel yang diminta tidak ada dalam data.", NoteKind.Error));
                return blok;
            }

            var keep = ds.CompleteRows(butuh);
            var ambil = new Func<string, List<double>>(nama =>
            {
                int c = ds.IndexOf(nama);
                return keep.Select(r => Dataset.ToDouble(ds.Rows[r][c]) ?? double.NaN).ToList();
            });

            var x = ambil(varX);
            var y = ambil(varY);
            var dataZ = z.Select(ambil).ToList();

            var sah = Enumerable.Range(0, keep.Count)
                .Where(i => !double.IsNaN(x[i]) && !double.IsNaN(y[i]) && dataZ.All(c => !double.IsNaN(c[i])))
                .ToList();

            var cx = sah.Select(i => x[i]).ToList();
            var cy = sah.Select(i => y[i]).ToList();
            var cz = dataZ.Select(c => sah.Select(i => c[i]).ToList()).ToList();

            
            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.KorelasiParsial, DaftarRumus.Pearson));

            var hasil = Parsial(cx, cy, cz);
            if (hasil is null)
            {
                blok.Add(Blocks.Note($"Data tidak cukup: {sah.Count} baris lengkap untuk "
                                     + $"{butuh.Count} variabel. Butuh sedikitnya {z.Count + 3}.",
                                     NoteKind.Error));
                return blok;
            }

            if (z.Count == 1)
            {
                
                
                double rxy = Pearson(cx, cy);
                double rxz = Pearson(cx, cz[0]);
                double ryz = Pearson(cy, cz[0]);
                double yule = (rxy - rxz * ryz) / Math.Sqrt((1 - rxz * rxz) * (1 - ryz * ryz));

                blok.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                    ("Korelasi sederhana", $"r({varX},{varY}) = {Fmt.Num(rxy, 4)}"),
                    ("Kendali", $"r({varX},{z[0]}) = {Fmt.Num(rxz, 4)}   r({varY},{z[0]}) = {Fmt.Num(ryz, 4)}"),
                    ("Rumus Yule (satu kendali)",
                     $"({Fmt.Num(rxy, 4)} − {Fmt.Num(rxz, 4)}×{Fmt.Num(ryz, 4)}) / √[(1 − {Fmt.Num(rxz * rxz, 4)})(1 − {Fmt.Num(ryz * ryz, 4)})] = {Fmt.Num(yule, 4)}"),
                    ("Lewat residual regresi (jalan bakunya)", $"r = {Fmt.Num(hasil.R, 4)}   selisih kedua jalan = {Fmt.Num(Math.Abs(yule - hasil.R), 12)}"),
                    ("Derajat bebas", $"df = n − 2 − k = {hasil.N} − 2 − {hasil.K} = {hasil.Df}")));
            }
            else
            {
                blok.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                    ("Baris lengkap", $"n = {hasil.N}, variabel kendali k = {hasil.K}"),
                    ("Korelasi sederhana (sebelum dikendalikan)", $"r = {Fmt.Num(hasil.RSederhana, 4)}"),
                    ("Sisa setelah kendali dihilangkan (residual)", $"r = {Fmt.Num(hasil.R, 4)}"),
                    ("Uji t", $"t = r·√(df / (1 − r²)) = {Fmt.Num(hasil.T, 4)} dengan df = {hasil.Df}")));
            }

            
            string putusan = hasil.P < alpha ? $"signifikan pada α = {Fmt.Num(alpha, 2)}" : "tidak signifikan";

            blok.Add(Blocks.Table("Korelasi parsial",
                new[] { "Pasangan", "Kendali", "Korelasi sederhana", "Korelasi parsial", "t", "df", "p", "Keputusan" },
                new[]
                {
                    new[]
                    {
                        $"{varX} ↔ {varY}",
                        z.Count == 0 ? "(tidak ada)" : string.Join(", ", z),
                        Fmt.Num(hasil.RSederhana, 3),
                        Fmt.Num(hasil.R, 3),
                        Fmt.Num(hasil.T, 3),
                        hasil.Df.ToString(),
                        Fmt.P(hasil.P),
                        putusan
                    }
                },
                $"N = {hasil.N} baris lengkap. Derajat bebas n − 2 − k = {hasil.Df}. "
                + "Bila korelasi sederhana dan parsialnya jauh berbeda, variabel kendalinya "
                + "memang menjelaskan sebagian hubungan itu."));

            return blok;
        }

        public static List<ResultBlock> Matrix(Dataset ds, IEnumerable<string> names, string method,
                                               double alpha = 0.05, bool flagSignificant = true)
        {
            string label = method switch
            {
                "spearman" => "Korelasi Spearman",
                "kendall" => "Korelasi Kendall tau-b",
                _ => "Korelasi Pearson"
            };

            var blocks = new List<ResultBlock> { Blocks.Heading(label, 1) };
            var vars = names.ToList();
            if (vars.Count < 2)
            {
                blocks.Add(Blocks.Note("Pilih sedikitnya 2 variabel.", NoteKind.Error));
                return blocks;
            }

            
            var keep = ds.CompleteRows(vars);
            var data = vars.Select(v =>
                keep.Select(r => Dataset.ToDouble(ds.Rows[r][ds.IndexOf(v)]) ?? double.NaN).ToList()).ToList();

            
            var valid = Enumerable.Range(0, keep.Count)
                .Where(i => data.All(col => !double.IsNaN(col[i]))).ToList();

            var clean = data.Select(col => valid.Select(i => col[i]).ToList()).ToList();
            int n = valid.Count;

            if (n < 3)
            {
                blocks.Add(Blocks.Note($"Hanya {n} baris lengkap; butuh sedikitnya 3.", NoteKind.Error));
                return blocks;
            }

            
            var rumusUtama = method switch
            {
                "spearman" => DaftarRumus.Spearman,
                "kendall" => DaftarRumus.Kendall,
                _ => DaftarRumus.Pearson
            };
            blocks.Add(Blocks.Rumus("Rumus yang dipakai", rumusUtama));
            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data", LangkahKorelasi(method, vars, clean, n)));

            var columns = new List<string> { "Variabel" };
            columns.AddRange(vars);
            var rows = new List<List<string>>();

            for (int i = 0; i < vars.Count; i++)
            {
                var row = new List<string> { vars[i] };
                for (int j = 0; j < vars.Count; j++)
                {
                    if (i == j) { row.Add("1,000"); continue; }
                    double r = method switch
                    {
                        "spearman" => Spearman(clean[i], clean[j]),
                        "kendall" => KendallTauB(clean[i], clean[j], out _),
                        _ => Pearson(clean[i], clean[j])
                    };
                    row.Add(Fmt.Num(r, 3));
                }
                rows.Add(row);
            }

            blocks.Add(Blocks.Table($"{label} — matriks", columns, rows,
                $"N = {n} (baris lengkap untuk semua variabel terpilih)."));

            
            var pairCols = new[] { "Pasangan", "Koefisien", "p", "Keputusan (α = 0,05)" };
            var pairRows = new List<List<string>>();
            for (int i = 0; i < vars.Count; i++)
            {
                for (int j = i + 1; j < vars.Count; j++)
                {
                    double r, p;
                    switch (method)
                    {
                        case "spearman":
                            r = Spearman(clean[i], clean[j]); p = PearsonP(r, n); break;
                        case "kendall":
                            r = KendallTauB(clean[i], clean[j], out p); break;
                        default:
                            r = Pearson(clean[i], clean[j]); p = PearsonP(r, n); break;
                    }
                    pairRows.Add(new List<string>
                    {
                        $"{vars[i]} ↔ {vars[j]}", Fmt.Num(r, 3), Fmt.P(p), Compare.Keputusan(p, alpha)
                    });

                    if (flagSignificant && !double.IsNaN(r) && Math.Abs(r) >= 0.8)
                        blocks.Add(Blocks.Note(
                            $"Korelasi {vars[i]} ↔ {vars[j]} sangat kuat ({Fmt.Num(r, 3)}); "
                            + "periksa kemungkinan multikolinearitas sebelum regresi.", NoteKind.Warning));
                }
            }

            blocks.Add(Blocks.Table($"{label} — uji per pasangan", pairCols, pairRows,
                "Untuk Kendall, p memakai pendekatan normal; perkiraan bila banyak nilai sama."));

            if (vars.Count >= 2)
            {
                var spec = new ChartSpec
                {
                    Kind = ChartKind.Scatter, Title = $"Pencar — {vars[0]} vs {vars[1]}",
                    XTitle = vars[0], YTitle = vars[1]
                };
                for (int i = 0; i < clean[0].Count; i++) spec.Points.Add((clean[0][i], clean[1][i]));

                double r2 = Pearson(clean[0], clean[1]);
                if (!double.IsNaN(r2))
                {
                    double my = clean[1].Average(), mx = clean[0].Average();
                    double sxy = 0, sxx = 0;
                    for (int i = 0; i < clean[0].Count; i++)
                    {
                        sxy += (clean[0][i] - mx) * (clean[1][i] - my);
                        sxx += Math.Pow(clean[0][i] - mx, 2);
                    }
                    double slope = sxx > 0 ? sxy / sxx : 0;
                    spec.RegressionLine = (slope, my - slope * mx);
                }

                blocks.Add(Blocks.Chart(spec.Title, spec));
            }

            return blocks;
        }
    }
}
