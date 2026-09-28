using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public class HasilProbit
    {
        public List<string> Nama = new();
        public double[] B = Array.Empty<double>();
        public double[] Se = Array.Empty<double>();
        public double[] Z = Array.Empty<double>();
        public double[] P = Array.Empty<double>();

        public int N;
        public int K;
        public double LogLik;
        public double LogLikNol;
        public double PseudoR2;
        public int Iterasi;
        public bool Konvergen;
        public string? Catatan;
    }

    public static class Probit
    {
        private const int ITERASI_MAKS = 100;
        private const double TOLERANSI = 1e-12;

        public static HasilProbit? Fit(IReadOnlyList<double> y, List<double[]> prediktor)
        {
            int n = y.Count;
            int p = prediktor.Count;
            int k = p + 1;
            if (n < k + 2 || p < 1) return null;
            if (prediktor.Any(v => v.Length != n)) return null;

            var unik = y.Distinct().OrderBy(v => v).ToList();
            if (unik.Count != 2) return null;
            double rendah = unik[0], tinggi = unik[1];
            var yb = y.Select(v => Math.Abs(v - tinggi) < 1e-12 ? 1.0
                                 : Math.Abs(v - rendah) < 1e-12 ? 0.0
                                 : double.NaN).ToList();
            if (yb.Any(double.IsNaN)) return null;

            var x = new double[n][];
            for (int i = 0; i < n; i++)
            {
                x[i] = new double[k];
                x[i][0] = 1.0;
                for (int j = 0; j < p; j++) x[i][j + 1] = prediktor[j][i];
            }

            var beta = new double[k];
            int iterasi = 0;
            bool konvergen = false;

            for (iterasi = 1; iterasi <= ITERASI_MAKS; iterasi++)
            {
                var mu = new double[n];
                var w = new double[n];
                var zk = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double eta = 0;
                    for (int j = 0; j < k; j++) eta += x[i][j] * beta[j];
                    double c = Distributions.NormalCdf(eta);
                    double d = Math.Max(Distributions.NormalPdf(eta), 1e-15);
                    mu[i] = c;
                    double v = Math.Max(c * (1 - c), 1e-12);
                    w[i] = d * d / v;
                    zk[i] = eta + (yb[i] - c) / d;
                }

                var A = new double[k, k];
                var bvec = new double[k];
                for (int i = 0; i < n; i++)
                {
                    for (int a = 0; a < k; a++)
                    {
                        bvec[a] += w[i] * x[i][a] * zk[i];
                        for (int b = 0; b < k; b++) A[a, b] += w[i] * x[i][a] * x[i][b];
                    }
                }

                var salinan = (double[,])A.Clone();
                var inv = Regression.Inverse(salinan);
                if (inv is null)
                {
                    return new HasilProbit
                    {
                        N = n, K = p, Iterasi = iterasi, Konvergen = false,
                        Catatan = "Matriks informasi tidak bisa dibalik (prediktor bergantung atau "
                                  + "pemisahan sempurna)."
                    };
                }

                var betaLama = (double[])beta.Clone();
                for (int a = 0; a < k; a++)
                {
                    double s = 0;
                    for (int b = 0; b < k; b++) s += inv[a, b] * bvec[b];
                    beta[a] = s;
                }
                double beda = 0;
                for (int a = 0; a < k; a++) beda = Math.Max(beda, Math.Abs(beta[a] - betaLama[a]));
                if (beda < TOLERANSI) { konvergen = true; break; }
            }

            
            var muAkhir = new double[n];
            var wAkhir = new double[n];
            for (int i = 0; i < n; i++)
            {
                double eta = 0;
                for (int j = 0; j < k; j++) eta += x[i][j] * beta[j];
                double c = Distributions.NormalCdf(eta);
                double d = Math.Max(Distributions.NormalPdf(eta), 1e-15);
                muAkhir[i] = c;
                double v = Math.Max(c * (1 - c), 1e-12);
                wAkhir[i] = d * d / v;
            }
            var info = new double[k, k];
            for (int a = 0; a < k; a++)
                for (int b = 0; b < k; b++)
                {
                    double s = 0;
                    for (int i = 0; i < n; i++) s += x[i][a] * wAkhir[i] * x[i][b];
                    info[a, b] = s;
                }
            var invAkhir = Regression.Inverse((double[,])info.Clone());
            if (invAkhir is null)
            {
                return new HasilProbit
                {
                    N = n, K = p, Iterasi = iterasi, Konvergen = false,
                    Catatan = "Matriks informasi tidak bisa dibalik pada beta akhir."
                };
            }

            var hasil = new HasilProbit
            {
                N = n, K = p, Iterasi = iterasi, Konvergen = konvergen,
                B = beta, Se = new double[k], Z = new double[k], P = new double[k]
            };
            for (int a = 0; a < k; a++)
            {
                double ragam = invAkhir[a, a];
                hasil.Se[a] = ragam > 0 ? Math.Sqrt(ragam) : double.NaN;
                hasil.Z[a] = hasil.Se[a] > 0 ? beta[a] / hasil.Se[a] : double.NaN;
                hasil.P[a] = double.IsNaN(hasil.Z[a])
                    ? double.NaN
                    : 2.0 * Distributions.NormalCdf(-Math.Abs(hasil.Z[a]));
            }

            hasil.LogLik = LogLik(yb, muAkhir.ToList());
            double pbar = yb.Average();
            pbar = Math.Min(Math.Max(pbar, 1e-12), 1 - 1e-12);
            hasil.LogLikNol = LogLik(yb, yb.Select(_ => pbar).ToList());
            hasil.PseudoR2 = Math.Abs(hasil.LogLikNol) > 1e-12
                ? 1 - hasil.LogLik / hasil.LogLikNol
                : double.NaN;

            if (!konvergen)
                hasil.Catatan = $"Tidak konvergen setelah {ITERASI_MAKS} iterasi; koefisien belum stabil.";

            return hasil;
        }

        private static double LogLik(List<double> y, List<double> mu)
        {
            double total = 0;
            for (int i = 0; i < y.Count; i++)
            {
                double m = Math.Min(Math.Max(mu[i], 1e-12), 1 - 1e-12);
                total += y[i] * Math.Log(m) + (1 - y[i]) * Math.Log(1 - m);
            }
            return total;
        }

        public static List<ResultBlock> ProbitBlocks(Dataset ds, string dependen, List<string> prediktor,
                                                     double alfaT = 0.05)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Regresi Probit biner") };

            if (ds.IndexOf(dependen) < 0 || prediktor.Any(v => ds.IndexOf(v) < 0))
            {
                blok.Add(Blocks.Note("Variabel yang diminta tidak ada dalam data.", NoteKind.Error));
                return blok;
            }

            var butuh = new List<string> { dependen };
            butuh.AddRange(prediktor);
            var keep = ds.CompleteRows(butuh);
            int id = ds.IndexOf(dependen);

            string? labelNol = null, labelSatu = null;
            var teksY = keep.Select(r => (ds.Rows[r][id] ?? "").Trim()).Where(t => t.Length > 0).ToList();
            bool angkaSemua = teksY.Count > 0 && teksY.All(t => Dataset.ToDouble(t).HasValue);
            if (!angkaSemua)
            {
                var unikTeks = teksY.Distinct(StringComparer.Ordinal).OrderBy(t => t).ToList();
                if (unikTeks.Count != 2)
                {
                    blok.Add(Blocks.Note($"Variabel terikat harus punya tepat dua macam nilai; "
                                         + $"ditemukan {unikTeks.Count}.", NoteKind.Error));
                    return blok;
                }
                labelNol = unikTeks[0];
                labelSatu = unikTeks[1];
            }

            var nilaiY = new List<double>();
            var nilaiX = prediktor.Select(v => new List<double>()).ToList();
            foreach (int r in keep)
            {
                double? yv = labelNol is null
                    ? Dataset.ToDouble(ds.Rows[r][id])
                    : string.Equals((ds.Rows[r][id] ?? "").Trim(), labelNol, StringComparison.Ordinal) ? 0.0
                    : string.Equals((ds.Rows[r][id] ?? "").Trim(), labelSatu, StringComparison.Ordinal) ? 1.0
                    : (double?)null;
                bool boleh = yv.HasValue;
                var isi = new List<double>();
                for (int j = 0; j < prediktor.Count; j++)
                {
                    double? xv = Dataset.ToDouble(ds.Rows[r][ds.IndexOf(prediktor[j])]);
                    if (xv.HasValue) isi.Add(xv.Value); else boleh = false;
                }
                if (!boleh) continue;
                nilaiY.Add(yv!.Value);
                for (int j = 0; j < prediktor.Count; j++) nilaiX[j].Add(isi[j]);
            }

            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.RegresiProbit));

            if (nilaiY.Distinct().Count() != 2)
            {
                blok.Add(Blocks.Note($"Variabel terikat harus punya tepat dua macam nilai; "
                                     + $"ditemukan {nilaiY.Distinct().Count()}.", NoteKind.Error));
                return blok;
            }

            var h = Fit(nilaiY, nilaiX.Select(v => v.ToArray()).ToList());
            if (h is null)
            {
                blok.Add(Blocks.Note($"Data tidak cukup: {nilaiY.Count} baris lengkap untuk "
                                     + $"{prediktor.Count} prediktor.", NoteKind.Error));
                return blok;
            }

            string teksDua = labelNol is null
                ? "dua nilai angka diubah jadi 0/1"
                : "\u201C" + labelNol + "\u201D diubah jadi 0, \u201C" + labelSatu + "\u201D diubah jadi 1";
            blok.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Banyak amatan", $"n = {h.N}, prediktor = {h.K} (konstanta disertakan)"),
                ("Dua macam nilai terikat", teksDua),
                ("Iterasi penskoran Fisher", $"{h.Iterasi} kali, konvergen = {(h.Konvergen ? "ya" : "tidak")}"),
                ("Log-peluang", $"ln L = {Fmt.Num(h.LogLik, 4)}, ln L₀ = {Fmt.Num(h.LogLikNol, 4)}"),
                ("Pseudo R² (McFadden)", $"1 − ln L / ln L₀ = {Fmt.Num(h.PseudoR2, 4)}")));

            var nama = new List<string> { "(Konstanta)" };
            nama.AddRange(prediktor);
            h.Nama = nama;

            if (!h.Konvergen)
            {
                blok.Add(Blocks.Note(h.Catatan ?? "Model tidak konvergen.", NoteKind.Warning));
                return blok;
            }

            var baris = new List<List<string>>();
            for (int j = 0; j < nama.Count; j++)
            {
                double b = h.B[j], se = h.Se[j];
                double batas = 1.959963984540054 * se;
                baris.Add(new List<string>
                {
                    nama[j],
                    Fmt.Num(b, 4),
                    Fmt.Num(se, 4),
                    Fmt.Num(h.Z[j], 3),
                    Fmt.P(h.P[j]),
                    $"[{Fmt.Num(b - batas, 4)}; {Fmt.Num(b + batas, 4)}]"
                });
            }
            blok.Add(Blocks.Table("Koefisien",
                new[] { "Variabel", "B", "Galat baku", "z", "p", "Selang 95% untuk B" },
                baris,
                "B berarti perubahan z (nilai kejadian) per kenaikan satu unit prediktor. "
                + "Uji z memakai sebaran normal, sama seperti statsmodels."));

            blok.Add(Blocks.Table("Ringkasan model",
                new[] { "N", "ln L", "ln L₀", "Pseudo R²", "Iterasi" },
                new[]
                {
                    new[]
                    {
                        h.N.ToString(), Fmt.Num(h.LogLik, 4), Fmt.Num(h.LogLikNol, 4),
                        Fmt.Num(h.PseudoR2, 4), h.Iterasi.ToString()
                    }
                },
                "Pseudo R² McFadden untuk probit biasanya jauh lebih kecil daripada R² regresi linear."));

            return blok;
        }
    }
}
