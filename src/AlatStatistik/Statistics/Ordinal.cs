using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public class HasilOrdinal
    {
        public double[] Beta = Array.Empty<double>();     
        public double[] Alpha = Array.Empty<double>();    
        public double[] Se = Array.Empty<double>();        
        public double[] Z = Array.Empty<double>();
        public double[] P = Array.Empty<double>();
        public List<string> Kategori = new();
        public int N, K, J;
        public double LogLik, LogLikNol, PseudoR2;
        public int Iterasi;
        public bool Konvergen;
        public string? Catatan;
    }

    public static class Ordinal
    {
        private const int ITERASI_MAKS = 100;
        private const double TOLERANSI = 1e-10;

        public static HasilOrdinal? Fit(IReadOnlyList<double> y, List<double[]> prediktor)
        {
            int n = y.Count;
            int p = prediktor.Count;
            int k = p;                              
            if (n < k + 2 || p < 1) return null;
            if (prediktor.Any(v => v.Length != n)) return null;

            var unik = y.Distinct().OrderBy(v => v).ToList();
            int J = unik.Count;
            if (J < 3) return null;                

            var yc = y.Select(v => unik.IndexOf(v)).ToList();
            int m = k + (J - 1);
            var theta = new double[m];
            for (int j = 0; j < J - 1; j++) theta[k + j] = -1.0 + 2.0 * j / (J - 1);

            int iterasi = 0;
            bool konvergen = false;

            void Hitung(double[] t, out double[] g, out double[,] I)
            {
                g = new double[m];
                I = new double[m, m];
                for (int i = 0; i < n; i++)
                {
                    double et = 0;
                    for (int c = 0; c < k; c++) et += prediktor[c][i] * t[c];
                    var w = new double[J - 1];
                    for (int j = 0; j < J - 1; j++)
                    {
                        double gam = t[k + j] - et;
                        double lam = 1.0 / (1.0 + Math.Exp(-gam));
                        w[j] = lam * (1.0 - lam);
                    }
                    var cum = new double[J - 1];
                    for (int j = 0; j < J - 1; j++)
                    {
                        double gam = t[k + j] - et;
                        cum[j] = 1.0 / (1.0 + Math.Exp(-gam));
                    }
                    
                    
                    
                    
                    var dd = new double[J - 1];
                    for (int j = 0; j < J - 1; j++) dd[j] = w[j] * (1.0 - 2.0 * cum[j]);
                    var pr = new double[J];
                    pr[0] = cum[0];
                    for (int j = 1; j < J - 1; j++) pr[j] = cum[j] - cum[j - 1];
                    pr[J - 1] = 1.0 - cum[J - 2];
                    int yi = yc[i];
                    double py = Math.Max(pr[yi], 1e-300);

                    var gp = new double[m];         
                    double dbeta;
                    if (yi == 0) dbeta = -w[0];
                    else if (yi == J - 1) dbeta = w[J - 2];
                    else dbeta = w[yi - 1] - w[yi];
                    for (int c = 0; c < k; c++) gp[c] = prediktor[c][i] * dbeta;
                    if (yi == 0) gp[k + 0] = w[0];
                    else if (yi == J - 1) gp[k + J - 2] = -w[J - 2];
                    else { gp[k + yi - 1] = -w[yi - 1]; gp[k + yi] = w[yi]; }

                    var Hp = new double[m, m];
                    double d2;
                    if (yi == 0) d2 = dd[0];
                    else if (yi == J - 1) d2 = -dd[J - 2];
                    else d2 = dd[yi] - dd[yi - 1];
                    for (int c = 0; c < k; c++)
                        for (int d = 0; d < k; d++)
                            Hp[c, d] = prediktor[c][i] * prediktor[d][i] * d2;
                    if (yi == 0) Hp[k + 0, k + 0] = dd[0];
                    else if (yi == J - 1) Hp[k + J - 2, k + J - 2] = -dd[J - 2];
                    else
                    {
                        Hp[k + yi - 1, k + yi - 1] = -dd[yi - 1];
                        Hp[k + yi, k + yi] = dd[yi];
                    }
                    double d2eaM, d2eaP = 0;
                    if (yi == 0) d2eaM = -dd[0];
                    else if (yi == J - 1) d2eaM = dd[J - 2];
                    else { d2eaM = dd[yi - 1]; d2eaP = -dd[yi]; }
                    for (int c = 0; c < k; c++)
                    {
                        if (yi == 0)
                            Hp[c, k + 0] = Hp[k + 0, c] = prediktor[c][i] * (-dd[0]);
                        else if (yi == J - 1)
                            Hp[c, k + J - 2] = Hp[k + J - 2, c] = prediktor[c][i] * dd[J - 2];
                        else
                        {
                            Hp[c, k + yi - 1] = Hp[k + yi - 1, c] = prediktor[c][i] * d2eaM;
                            Hp[c, k + yi] = Hp[k + yi, c] = prediktor[c][i] * d2eaP;
                        }
                    }

                    for (int a = 0; a < m; a++)
                    {
                        g[a] += gp[a] / py;
                        for (int b = 0; b < m; b++)
                            I[a, b] += -(1.0 / py) * Hp[a, b] + (1.0 / (py * py)) * gp[a] * gp[b];
                    }
                }
            }

            for (iterasi = 1; iterasi <= ITERASI_MAKS; iterasi++)
            {
                Hitung(theta, out var g, out var I);
                var inv = Regression.Inverse((double[,])I.Clone());
                if (inv is null)
                    return Gagal(n, k, J, iterasi, unik,
                        "Matriks informasi tidak bisa dibalik (prediktor bergantung).");
                var step = new double[m];
                for (int a = 0; a < m; a++)
                {
                    double s = 0;
                    for (int b = 0; b < m; b++) s += inv[a, b] * g[b];
                    step[a] = s;
                }
                double beda = 0;
                for (int a = 0; a < m; a++) { theta[a] += step[a]; beda = Math.Max(beda, Math.Abs(step[a])); }
                if (beda < TOLERANSI) { konvergen = true; break; }
            }

            Hitung(theta, out _, out var Ifin);
            var invFin = Regression.Inverse((double[,])Ifin.Clone());
            if (invFin is null)
                return Gagal(n, k, J, iterasi, unik,
                    "Matriks informasi tidak bisa dibalik pada beta akhir.");

            var beta = new double[k];
            var alpha = new double[J - 1];
            for (int c = 0; c < k; c++) beta[c] = theta[c];
            for (int j = 0; j < J - 1; j++) alpha[j] = theta[k + j];

            var Se = new double[m];
            var Z = new double[m];
            var Pv = new double[m];
            for (int a = 0; a < m; a++)
            {
                double sv = invFin[a, a];
                double se = sv > 0 ? Math.Sqrt(sv) : double.NaN;
                Se[a] = se;
                Z[a] = se > 0 ? theta[a] / se : double.NaN;
                Pv[a] = double.IsNaN(Z[a]) ? double.NaN
                    : 2.0 * Distributions.NormalCdf(-Math.Abs(Z[a]));
            }

            double ll = LogLik(theta, yc, prediktor, k, J);
            double llNol = LogLikNol(yc, n, J);
            double pseudo = Math.Abs(llNol) > 1e-12 ? 1 - ll / llNol : double.NaN;

            if (!konvergen)
                return new HasilOrdinal
                {
                    N = n, K = k, J = J, Iterasi = iterasi, Konvergen = false,
                    Beta = beta, Alpha = alpha, Se = Se, Z = Z, P = Pv,
                    Kategori = unik.Select(u => "Kategori " + Fmt.Num(u, 0)).ToList(),
                    LogLik = ll, LogLikNol = llNol, PseudoR2 = pseudo,
                    Catatan = $"Tidak konvergen setelah {ITERASI_MAKS} iterasi; koefisien belum stabil."
                };

            return new HasilOrdinal
            {
                N = n, K = k, J = J, Iterasi = iterasi, Konvergen = konvergen,
                Beta = beta, Alpha = alpha, Se = Se, Z = Z, P = Pv,
                Kategori = unik.Select(u => "Kategori " + Fmt.Num(u, 0)).ToList(),
                LogLik = ll, LogLikNol = llNol, PseudoR2 = pseudo
            };
        }

        private static HasilOrdinal Gagal(int n, int k, int J, int iter, List<double> unik, string pesan)
        {
            return new HasilOrdinal
            {
                N = n, K = k, J = J, Iterasi = iter, Konvergen = false,
                Kategori = unik.Select(u => "Kategori " + Fmt.Num(u, 0)).ToList(),
                Catatan = pesan
            };
        }

        private static double LogLik(double[] t, List<int> yc, List<double[]> prediktor, int k, int J)
        {
            double tot = 0;
            for (int i = 0; i < yc.Count; i++)
            {
                double et = 0;
                for (int c = 0; c < k; c++) et += prediktor[c][i] * t[c];
                var lam = new double[J - 1];
                for (int j = 0; j < J - 1; j++)
                {
                    double gam = t[k + j] - et;
                    lam[j] = 1.0 / (1.0 + Math.Exp(-gam));
                }
                var pr = new double[J];
                pr[0] = lam[0];
                for (int j = 1; j < J - 1; j++) pr[j] = lam[j] - lam[j - 1];
                pr[J - 1] = 1.0 - lam[J - 2];
                tot += Math.Log(Math.Max(pr[yc[i]], 1e-300));
            }
            return tot;
        }

        private static double LogLikNol(List<int> yc, int n, int J)
        {
            var hit = new int[J];
            foreach (int v in yc) hit[v]++;
            double tot = 0;
            for (int j = 0; j < J; j++)
                if (hit[j] > 0) tot += hit[j] * Math.Log((double)hit[j] / n);
            return tot;
        }

        public static List<ResultBlock> OrdinalBlocks(Dataset ds, string dependen, List<string> prediktor,
                                                       double alfaT = 0.05)
        {
            var blok = new List<ResultBlock> { Blocks.Heading($"Regresi Ordinal — {dependen}", 1) };

            if (ds.IndexOf(dependen) < 0 || prediktor.Any(v => ds.IndexOf(v) < 0))
            {
                blok.Add(Blocks.Note("Variabel yang diminta tidak ada dalam data.", NoteKind.Error));
                return blok;
            }
            var butuh = new List<string> { dependen };
            butuh.AddRange(prediktor);
            var keep = ds.CompleteRows(butuh);
            int id = ds.IndexOf(dependen);

            string[]? labelJ = null;
            var teksY = keep.Select(r => (ds.Rows[r][id] ?? "").Trim()).Where(t => t.Length > 0).ToList();
            bool angkaSemua = teksY.Count > 0 && teksY.All(t => Dataset.ToDouble(t).HasValue);
            if (!angkaSemua)
            {
                var unikTeks = teksY.Distinct(StringComparer.Ordinal)
                                   .OrderBy(t => t, StringComparer.Ordinal).ToList();
                if (unikTeks.Count < 3)
                {
                    blok.Add(Blocks.Note($"Variabel terikat harus punya sedikitnya tiga macam nilai "
                                         + $"(ordinal); ditemukan {unikTeks.Count}.", NoteKind.Error));
                    return blok;
                }
                labelJ = unikTeks.ToArray();
            }

            var nilaiY = new List<double>();
            var nilaiX = prediktor.Select(v => new List<double>()).ToList();
            foreach (int r in keep)
            {
                string teksR = (ds.Rows[r][id] ?? "").Trim();
                double? yv = labelJ is null
                    ? Dataset.ToDouble(teksR)
                    : (Array.IndexOf(labelJ, teksR) >= 0 ? (double)Array.IndexOf(labelJ, teksR)
                                                         : (double?)null);
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

            if (nilaiY.Distinct().Count() < 3)
            {
                blok.Add(Blocks.Note("Variabel terikat harus punya sedikitnya tiga macam nilai (ordinal).",
                    NoteKind.Error));
                return blok;
            }

            var h = Fit(nilaiY, nilaiX.Select(v => v.ToArray()).ToList());
            if (h is null)
            {
                blok.Add(Blocks.Note($"Data tidak cukup: {nilaiY.Count} baris lengkap untuk "
                                     + $"{prediktor.Count} prediktor.", NoteKind.Error));
                return blok;
            }
            if (labelJ is not null) h.Kategori = labelJ.ToList();

            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.RegresiOrdinal));

            blok.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Banyak amatan", $"n = {h.N}, prediktor = {h.K}"),
                ("Kategori terikat", string.Join(", ", h.Kategori)),
                ("Iterasi Newton–Raphson", $"{h.Iterasi} kali, konvergen = {(h.Konvergen ? "ya" : "tidak")}"),
                ("Log-peluang", $"ln L = {Fmt.Num(h.LogLik, 4)}, ln L₀ = {Fmt.Num(h.LogLikNol, 4)}"),
                ("Pseudo R² (McFadden)", $"1 − ln L / ln L₀ = {Fmt.Num(h.PseudoR2, 4)}")));

            if (!h.Konvergen)
            {
                blok.Add(Blocks.Note(h.Catatan ?? "Model tidak konvergen.", NoteKind.Warning));
                return blok;
            }

            var namaKol = new List<string>();
            namaKol.AddRange(prediktor);
            var baris = new List<List<string>>();
            for (int c = 0; c < h.K; c++)
            {
                double b = h.Beta[c], se = h.Se[c];
                double batas = 1.959963984540054 * se;
                baris.Add(new List<string>
                {
                    namaKol[c],
                    Fmt.Num(b, 4),
                    Fmt.Num(se, 4),
                    Fmt.Num(h.Z[c], 3),
                    Fmt.P(h.P[c]),
                    $"[{Fmt.Num(b - batas, 4)}; {Fmt.Num(b + batas, 4)}]"
                });
            }
            blok.Add(Blocks.Table("Koefisien prediktor (β)",
                new[] { "Prediktor", "β", "Galat baku", "z", "p", "Selang 95% untuk β" },
                baris,
                "β diartikan seperti regresi logistik: kenaikan 1 satuan prediktor menggeser "
                + "logit peluang kumulatif sebesar β (asumsi peluang sebanding)."));

            var barisA = new List<List<string>>();
            for (int j = 0; j < h.J - 1; j++)
            {
                double a = h.Alpha[j], se = h.Se[h.K + j];
                double batas = 1.959963984540054 * se;
                barisA.Add(new List<string>
                {
                    $"Titik potong α{j + 1} (vs ≥ kategori {j + 1})",
                    Fmt.Num(a, 4),
                    Fmt.Num(se, 4),
                    Fmt.Num(h.Z[h.K + j], 3),
                    Fmt.P(h.P[h.K + j]),
                    $"[{Fmt.Num(a - batas, 4)}; {Fmt.Num(a + batas, 4)}]"
                });
            }
            blok.Add(Blocks.Table("Titik potong (α)",
                new[] { "Titik potong", "α", "Galat baku", "z", "p", "Selang 95% untuk α" },
                barisA,
                "αⱼ memisahkan kategori ≤ j dari > j pada skala logit."));

            blok.Add(Blocks.Table("Ringkasan model",
                new[] { "N", "ln L", "ln L₀", "Pseudo R²", "Iterasi" },
                new[] { new[] { h.N.ToString(), Fmt.Num(h.LogLik, 4), Fmt.Num(h.LogLikNol, 4),
                               Fmt.Num(h.PseudoR2, 4), h.Iterasi.ToString() } },
                "Pseudo R² McFadden untuk ordinal biasanya jauh lebih kecil daripada R² regresi linear."));

            return blok;
        }
    }
}
