using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public class HasilFriedman
    {
        public double Chi2;
        public int Df;
        public double P;
        public int N;                       
        public int K;                        
        public double KoreksiIkatan = 1.0;
        public List<double> PeringkatRerata = new();
    }

    public class HasilRuns
    {
        public int Run;
        public int NPos;
        public int NNeg;
        public double Z;
        public double P;
        public double Potongan;

        public int N => NPos + NNeg;
    }

    public enum CaraPotong { Median, Rerata, NilaiSendiri }

    public static class Nonparametrik
    {
        

        public static HasilFriedman? Friedman(List<List<double>> perlakuan)
        {
            if (perlakuan is null || perlakuan.Count < 3) return null;

            int k = perlakuan.Count;
            int n = perlakuan[0].Count;
            if (n == 0 || perlakuan.Any(p => p.Count != n)) return null;

            
            double ikatan = 0;
            var peringkat = new double[n, k];

            for (int i = 0; i < n; i++)
            {
                var baris = Enumerable.Range(0, k).Select(j => perlakuan[j][i]).ToList();
                var r = Compare.Ranks(baris);

                
                foreach (var g in baris.GroupBy(v => Math.Round(v, 10)).Where(g => g.Count() > 1))
                {
                    double t = g.Count();
                    ikatan += t * (t * t - 1);
                }

                for (int j = 0; j < k; j++) peringkat[i, j] = r[j];
            }

            double c = 1 - ikatan / (k * (k * k - 1.0) * n);
            if (c <= 0) return null;

            double ssbn = 0;
            var jumlah = new double[k];
            for (int j = 0; j < k; j++)
            {
                double s = 0;
                for (int i = 0; i < n; i++) s += peringkat[i, j];
                jumlah[j] = s;
                ssbn += s * s;
            }

            double chi2 = (12.0 / (k * n * (k + 1.0)) * ssbn - 3.0 * n * (k + 1.0)) / c;
            int df = k - 1;

            return new HasilFriedman
            {
                Chi2 = chi2,
                Df = df,
                P = Distributions.ChiSquareUpper(chi2, df),
                N = n,
                K = k,
                KoreksiIkatan = c,
                PeringkatRerata = jumlah.Select(s => s / n).ToList()
            };
        }

        

        public static HasilRuns? Runs(List<double> nilai, CaraPotong cara = CaraPotong.Median,
                                      double potonganSendiri = 0)
        {
            var x = (nilai ?? new List<double>()).Where(v => !double.IsNaN(v)).ToList();
            if (x.Count < 3) return null;

            double potong = cara switch
            {
                CaraPotong.Rerata => x.Average(),
                CaraPotong.NilaiSendiri => potonganSendiri,
                _ => Median(x)
            };

            var tanda = x.Select(v => v >= potong ? 1 : 0).ToList();
            int nPos = tanda.Count(v => v == 1);
            int nNeg = tanda.Count - nPos;
            int n = nPos + nNeg;
            if (nPos == 0 || nNeg == 0) return null;

            int run = 1;
            for (int i = 1; i < tanda.Count; i++)
                if (tanda[i] != tanda[i - 1]) run++;

            double npn = (double)nPos * nNeg;
            double rmean = 2.0 * npn / n + 1;
            double rvar = 2.0 * npn * (2.0 * npn - n) / (n * n) / (n - 1.0);
            double rstd = Math.Sqrt(rvar);
            if (rstd <= 0) return null;

            double z;
            double p;

            if (run == 1)
            {
                
                double ekor = 1.0 / Math.Pow(2.0, Math.Min(n, 1024) - 1);
                z = -Distributions.NormalInv(1 - ekor);
                p = ekor * 2;
            }
            else
            {
                double selisih = run - rmean;
                double terbenar = n >= 50 ? selisih
                                : selisih > 0.5 ? selisih - 0.5
                                : selisih < 0.5 ? selisih + 0.5
                                : 0.0;

                z = terbenar / rstd;
                p = 2.0 * Distributions.NormalCdf(-Math.Abs(z));
            }

            return new HasilRuns
            {
                Run = run, NPos = nPos, NNeg = nNeg,
                Z = z, P = p, Potongan = potong
            };
        }

        private static double Median(List<double> x)
        {
            var s = x.OrderBy(v => v).ToList();
            int n = s.Count;
            return n % 2 == 1 ? s[n / 2] : 0.5 * (s[n / 2 - 1] + s[n / 2]);
        }

        

        public static List<ResultBlock> FriedmanBlocks(Dataset ds, List<string> nama, double alpha = 0.05)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Uji Friedman") };

            if (nama is null || nama.Count < 3)
            {
                blok.Add(Blocks.Note("Uji Friedman butuh sedikitnya 3 perlakuan.", NoteKind.Error));
                return blok;
            }
            if (nama.Any(v => ds.IndexOf(v) < 0))
            {
                blok.Add(Blocks.Note("Ada variabel yang tidak ditemukan dalam data.", NoteKind.Error));
                return blok;
            }

            var keep = ds.CompleteRows(nama);
            var kolom = nama.Select(v =>
            {
                int c = ds.IndexOf(v);
                return keep.Select(r => Dataset.ToDouble(ds.Rows[r][c]) ?? double.NaN).ToList();
            }).ToList();

            var sah = Enumerable.Range(0, keep.Count)
                .Where(i => kolom.All(k => !double.IsNaN(k[i]))).ToList();

            var data = kolom.Select(k => sah.Select(i => k[i]).ToList()).ToList();
            var h = Friedman(data);

            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.UjiFriedman));

            if (h is null)
            {
                blok.Add(Blocks.Note($"Data tidak cukup: {sah.Count} baris lengkap.", NoteKind.Error));
                return blok;
            }

            
            double ssbn = h.PeringkatRerata.Sum(m => (m * h.N) * (m * h.N));

            blok.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Banyak blok dan perlakuan", $"n = {h.N} blok, k = {h.K} perlakuan"),
                ("Jumlah kuadrat peringkat", $"ΣR_j² = {Fmt.Num(ssbn, 3)}"),
                ("Statistik", $"χ² = [12/(k·n·(k+1))·ΣR_j² − 3·n·(k+1)] / C = {Fmt.Num(h.Chi2, 4)}"),
                ("Koreksi ikatan", $"C = 1 − Σt(t²−1) / (k·(k²−1)·n) = {Fmt.Num(h.KoreksiIkatan, 6)}"),
                ("Derajat bebas", $"df = k − 1 = {h.Df}")));

            var baris = nama.Select((v, j) => new[]
            {
                v,
                Fmt.Num(h.PeringkatRerata.Count > j ? h.PeringkatRerata[j] : double.NaN, 3)
            }).ToList();

            blok.Add(Blocks.Table("Peringkat rerata tiap perlakuan",
                new[] { "Perlakuan", "Peringkat rerata" }, baris,
                "Peringkat dihitung di dalam setiap blok; makin besar, makin tinggi nilainya."));

            blok.Add(Blocks.Table("Uji Friedman",
                new[] { "χ²", "df", "p", "Keputusan (α = 0,05)" },
                new[] { new[] { Fmt.Num(h.Chi2, 4), h.Df.ToString(), Fmt.P(h.P),
                        h.P < alpha ? "signifikan" : "tidak signifikan" } },
                $"N = {h.N} blok lengkap. Peringkat rerata yang berjauhan menandakan "
                + "perlakuannya berbeda."));

            return blok;
        }

        public static List<ResultBlock> RunsBlocks(Dataset ds, string nama, CaraPotong cara,
                                                   double potonganSendiri, double alpha = 0.05)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Uji runs") };

            if (ds.IndexOf(nama) < 0)
            {
                blok.Add(Blocks.Note("Variabel tidak ditemukan dalam data.", NoteKind.Error));
                return blok;
            }

            int c = ds.IndexOf(nama);
            var nilai = ds.Rows.Select(r => Dataset.ToDouble(c < r.Length ? r[c] : null))
                               .Where(v => v.HasValue).Select(v => v!.Value).ToList();

            var h = Runs(nilai, cara, potonganSendiri);

            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.UjiRuns));

            if (h is null)
            {
                blok.Add(Blocks.Note("Data tidak cukup, atau semua nilainya berada di satu sisi "
                                     + "potongan sehingga tidak ada run yang bisa dihitung.", NoteKind.Error));
                return blok;
            }

            string caraTeks = cara switch
            {
                CaraPotong.Rerata => "rerata",
                CaraPotong.NilaiSendiri => "nilai sendiri",
                _ => "median"
            };

            double rerataRun = 2.0 * h.NPos * h.NNeg / (double)h.N + 1;

            blok.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Potongan", $"{caraTeks} = {Fmt.Num(h.Potongan, 4)}"),
                ("Tanda", $"n₁ (≥ potongan) = {h.NPos}, n₂ (&lt; potongan) = {h.NNeg}, n = {h.N}"),
                ("Banyak run yang diamati", $"R = {h.Run}"),
                ("Run yang diharapkan", $"E[R] = 2·n₁·n₂/n + 1 = {Fmt.Num(rerataRun, 4)}"),
                ("Uji z", $"z = {Fmt.Num(h.Z, 4)}")));

            blok.Add(Blocks.Table("Uji runs (Wald–Wolfowitz)",
                new[] { "Run", "Run diharapkan", "n₁", "n₂", "z", "p", "Keputusan (α = 0,05)" },
                new[] { new[] { h.Run.ToString(), Fmt.Num(rerataRun, 3), h.NPos.ToString(),
                        h.NNeg.ToString(), Fmt.Num(h.Z, 4), Fmt.P(h.P),
                        h.P < alpha ? "signifikan" : "tidak signifikan" } },
                "Run terlalu sedikit berarti nilainya menggerombol; terlalu banyak berarti "
                + "berosilasi. Keduanya menandakan urutannya tidak acak."));

            return blok;
        }
    }
}
