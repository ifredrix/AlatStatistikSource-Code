using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public class NormalityResult
    {
        public string Nama = "";
        public double Statistik;
        public double P;
        public int N;

        public string Catatan = "";

        public bool Signifikan05 => P < 0.05;
    }

    public static class Normality
    {

        public const int MinimumN = 3;

        public static NormalityResult? ShapiroWilk(IEnumerable<double> sample)
        {
            double[] y = sample.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToArray();
            int n = y.Length;
            if (n < MinimumN) return null;

            double mean = y.Average();
            double ss = y.Sum(v => (v - mean) * (v - mean));
            if (ss <= 0) return null;              

            
            
            
            if (n == 3)
            {
                double c = Math.Sqrt(2) / 2;
                double w3 = Math.Pow(-c * y[0] + c * y[2], 2) / ss;
                w3 = Math.Clamp(w3, 0.75, 1.0);
                double p3 = Math.Clamp(1.0 - 6.0 / Math.PI * Math.Acos(Math.Sqrt(w3)), 0.0, 1.0);
                return new NormalityResult { Nama = "Shapiro–Wilk", N = n, Statistik = w3, P = p3 };
            }

            
            
            
            var m = new double[n];
            double mTm = 0;
            for (int i = 0; i < n; i++)
            {
                m[i] = Distributions.NormalInv((i + 1 - 0.375) / (n + 0.25));
                mTm += m[i] * m[i];
            }

            double u = 1.0 / Math.Sqrt(n);
            double cn = m[n - 1] / Math.Sqrt(mTm);
            double cnm1 = m[n - 2] / Math.Sqrt(mTm);

            double an = cn + 0.221157 * u - 0.147981 * u * u
                        - 2.071190 * u * u * u + 4.434685 * Math.Pow(u, 4) - 2.706056 * Math.Pow(u, 5);

            double anm1 = cnm1 + 0.042981 * u - 0.293762 * u * u
                          - 1.752461 * u * u * u + 5.682633 * Math.Pow(u, 4) - 3.582633 * Math.Pow(u, 5);

            
            
            double phi;
            if (n <= 5)
                phi = (mTm - 2 * m[n - 1] * m[n - 1]) / (1 - 2 * an * an);
            else
                phi = (mTm - 2 * m[n - 1] * m[n - 1] - 2 * m[n - 2] * m[n - 2])
                      / (1 - 2 * an * an - 2 * anm1 * anm1);

            var a = new double[n];
            for (int i = 0; i < n; i++) a[i] = m[i] / Math.Sqrt(phi);

            
            
            
            a[n - 1] = an;
            a[0] = -an;
            if (n > 5)
            {
                a[n - 2] = anm1;
                a[1] = -anm1;
            }

            double num = 0;
            for (int i = 0; i < n; i++) num += a[i] * y[i];

            double W = num * num / ss;

            
            double gW, mu, sigma;
            if (n <= 11)
            {
                double gamma = -2.273 + 0.459 * n;
                mu = 0.5440 - 0.39978 * n + 0.025054 * n * n - 0.0006714 * n * n * n;
                sigma = Math.Exp(1.3822 - 0.77857 * n + 0.062767 * n * n - 0.0020322 * n * n * n);
                gW = -Math.Log(gamma - Math.Log(1 - W));
            }
            else
            {
                double L = Math.Log(n);
                mu = -1.5861 - 0.31082 * L - 0.083751 * L * L + 0.0038915 * L * L * L;
                sigma = Math.Exp(-0.4803 - 0.082676 * L + 0.0030302 * L * L);
                gW = Math.Log(1 - W);
            }

            double z = (gW - mu) / sigma;
            double p = Distributions.NormalCdf(-z);

            return new NormalityResult
            {
                Nama = "Shapiro–Wilk",
                N = n,
                Statistik = Math.Clamp(W, 0.0, 1.0),
                P = Math.Clamp(p, 0.0, 1.0),
                Catatan = n < 20
                    ? "n kecil — uji kurang bertenaga; hasil tidak signifikan bukan bukti data normal."
                    : ""
            };
        }

        public readonly struct LillieforsHasil
        {
            public double P { get; init; }          
            public double GalatBaku { get; init; }  
            public int Replikasi { get; init; }
        }

        public const int ReplikasiLilliefors = 10000;

        public static LillieforsHasil LillieforsP(double d, int n,
                                                  int replikasi = ReplikasiLilliefors)
        {
            if (n < 4 || replikasi <= 0)
                return new LillieforsHasil
                {
                    P = double.NaN, GalatBaku = double.NaN, Replikasi = replikasi
                };

            
            ulong benih = 20260921UL;
            double Seragam()
            {
                benih += 0x9E3779B97F4A7C15UL;
                ulong z = benih;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                return (z >> 11) * (1.0 / 9007199254740992.0);
            }
            double Normal()
            {
                double u1 = Math.Max(Seragam(), 1e-12);   
                double u2 = Seragam();
                return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            }

            var x = new double[n];
            int kena = 0;
            for (int r = 0; r < replikasi; r++)
            {
                for (int i = 0; i < n; i++) x[i] = Normal();

                
                
                
                
                
                
                double rata = 0;
                for (int i = 0; i < n; i++) rata += x[i];
                rata /= n;
                double ss = 0;
                for (int i = 0; i < n; i++) { double t = x[i] - rata; ss += t * t; }
                double s = Math.Sqrt(ss / (n - 1));
                if (s <= 0) continue;
                for (int i = 0; i < n; i++) x[i] = (x[i] - rata) / s;

                Array.Sort(x);
                double dst = 0;
                for (int i = 0; i < n; i++)
                {
                    double F = Distributions.NormalCdf(x[i]);
                    dst = Math.Max(dst,
                                   Math.Max(F - (double)i / n, (double)(i + 1) / n - F));
                }
                if (dst >= d) kena++;
            }

            double p = (double)kena / replikasi;
            return new LillieforsHasil
            {
                P = p,
                GalatBaku = Math.Sqrt(p * (1 - p) / replikasi),
                Replikasi = replikasi
            };
        }

        public class KolmogorovSmirnovDetail
        {
            public int N;
            public double Mean;     
            public double Sd;       
            public double D;        
            public double Lambda;   
            public double P;        
            public string Catatan = "";

            
            public double PLilliefors = double.NaN;
            public double SeLilliefors = double.NaN;
            public int ReplikasiLilliefors = 0;
        }

        public static KolmogorovSmirnovDetail? KSDetail(IEnumerable<double> sample,
                                                        bool denganLilliefors = false)
        {
            double[] x = sample.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToArray();
            int n = x.Length;
            if (n < MinimumN) return null;

            double mean = x.Average();
            double sd = Math.Sqrt(x.Sum(v => (v - mean) * (v - mean)) / (n - 1));
            if (sd <= 0) return null;

            
            double d = 0;
            for (int i = 0; i < n; i++)
            {
                double z = (x[i] - mean) / sd;
                double F = Distributions.NormalCdf(z);
                d = Math.Max(d, Math.Max(F - (double)i / n, (double)(i + 1) / n - F));
            }

            
            
            
            
            double en = Math.Sqrt(n);
            double lambda = en * d;

            
            double p = 0;
            for (int k = 1; k <= 100; k++)
                p += ((k % 2 == 1) ? 2.0 : -2.0) * Math.Exp(-2.0 * k * k * lambda * lambda);
            p = Math.Clamp(p, 0.0, 1.0);

            double pLf = double.NaN, seLf = double.NaN;
            int repLf = 0;
            if (denganLilliefors)
            {
                var lf = LillieforsP(d, n);
                pLf = lf.P;
                seLf = lf.GalatBaku;
                repLf = lf.Replikasi;
            }

            string catatan =
                "p memakai sebaran asimptotik K–S tanpa koreksi Lilliefors, "
                + "karena rerata dan simpangan baku ditaksir dari data yang sama. "
                + "Angkanya cenderung terlalu besar (konservatif); bandingkan dengan "
                + "uji Shapiro–Wilk yang lebih bertenaga untuk n kecil.";
            if (denganLilliefors && !double.IsNaN(pLf))
                catatan += $" p terkoreksi Lilliefors (simulasi Monte Carlo, "
                           + $"{Fmt.Int(repLf)} ulangan, benih tetap) = {Fmt.P(pLf)}; "
                           + "itulah angka yang dipakai untuk memutuskan.";

            return new KolmogorovSmirnovDetail
            {
                N = n,
                Mean = mean,
                Sd = sd,
                D = d,
                Lambda = lambda,
                P = p,
                PLilliefors = pLf,
                SeLilliefors = seLf,
                ReplikasiLilliefors = repLf,
                Catatan = catatan
            };
        }

        public static NormalityResult? KolmogorovSmirnov(IEnumerable<double> sample)
        {
            var det = KSDetail(sample);
            if (det is null) return null;
            return new NormalityResult
            {
                Nama = "Kolmogorov–Smirnov",
                N = det.N,
                Statistik = det.D,
                P = det.P,
                Catatan = det.Catatan
            };
        }

        public static List<ResultBlock> KSBlocks(Dataset ds, string name)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading($"Uji Kolmogorov–Smirnov satu sampel — {name}", 1)
            };

            var values = Descriptives.CleanNumbers(ds, name);
            var det = KSDetail(values, denganLilliefors: true);
            if (det is null)
            {
                blocks.Add(Blocks.Note(
                    "Butuh sedikitnya 3 amatan numerik yang sah untuk uji ini.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.UjiKolmogorovSmirnov, DaftarRumus.KoreksiLilliefors));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Rerata (ditaksir dari data)",
                 $"x̄ = Σxᵢ / n = {Fmt.Num(values.Sum())} / {Fmt.Int(det.N)} = {Fmt.Num(det.Mean)}"),
                ("Simpangan baku (ditaksir)",
                 $"s = √[ Σ(xᵢ − x̄)² / (n − 1) ] = {Fmt.Num(det.Sd)}"),
                ("Statistik D",
                 $"D = sup |Fₙ(x) − F(x)| = {Fmt.Num(det.D, 4)}"),
                ("λ = √n · D  (tanpa koreksi kontinuitas)",
                 $"λ = √{Fmt.Int(det.N)} · D = {Fmt.Num(det.Lambda, 4)}"),
                ("p asimptotik",
                 $"p = 2·Σ(−1)ᵏ⁻¹·e^(−2k²λ²) = {Fmt.P(det.P)}"),
                ("p terkoreksi Lilliefors",
                 double.IsNaN(det.PLilliefors)
                     ? "tidak dihitung"
                     : $"disimulasikan {Fmt.Int(det.ReplikasiLilliefors)} kali dengan benih tetap "
                       + $"→ p = {Fmt.P(det.PLilliefors)} "
                       + $"(galat baku Monte Carlo {Fmt.Num(det.SeLilliefors, 4)})")));

            
            
            blocks.Add(Blocks.Table("Statistik uji",
                new[] { "N", "Rerata", "Simpangan baku", "D", "λ", "p asimptotik",
                        "p Lilliefors", "Kesimpulan (α = 0,05)" },
                new[]
                {
                    new[]
                    {
                        Fmt.Int(det.N), Fmt.Num(det.Mean), Fmt.Num(det.Sd),
                        Fmt.Num(det.D, 4), Fmt.Num(det.Lambda, 4), Fmt.P(det.P),
                        double.IsNaN(det.PLilliefors) ? Fmt.NA : Fmt.P(det.PLilliefors),
                        KeputusanKS(det)
                    }
                },
                $"H0: data berasal dari sebaran normal dengan rerata {Fmt.Num(det.Mean)} "
                + $"dan simpangan baku {Fmt.Num(det.Sd)}. Keputusan memakai kolom "
                + "**p Lilliefors**; kolom asimptotik ditampilkan sebagai pembanding "
                + "supaya terlihat berapa besar koreksinya."));

            blocks.Add(Blocks.Note(det.Catatan, NoteKind.Warning));

            return blocks;
        }

        private static string KeputusanKS(KolmogorovSmirnovDetail det)
        {
            double p = double.IsNaN(det.PLilliefors) ? det.P : det.PLilliefors;
            if (double.IsNaN(p)) return Fmt.NA;
            return p < 0.05
                ? "Tidak normal (H0 ditolak)"
                : "Tidak cukup bukti menyimpang dari normal";
        }
    }
}
