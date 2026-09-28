using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public class HasilMultinomial
    {
        public double[,] B = new double[0, 0];   
        public double[,] Se = new double[0, 0];
        public double[,] Z = new double[0, 0];
        public double[,] P = new double[0, 0];
        public List<string> Kategori = new();     
        public int N, K, J;
        public double LogLik, LogLikNol, PseudoR2;
        public int Iterasi;
        public bool Konvergen;
        public string? Catatan;
    }

    public static class Multinomial
    {
        private const int ITERASI_MAKS = 100;
        private const double TOLERANSI = 1e-12;

        public static HasilMultinomial? Fit(IReadOnlyList<double> y, List<double[]> prediktor)
        {
            int n = y.Count;
            int p = prediktor.Count;
            int k = p + 1;
            if (n < k + 2 || p < 1) return null;
            if (prediktor.Any(v => v.Length != n)) return null;

            var unik = y.Distinct().OrderBy(v => v).ToList();
            int J = unik.Count;
            if (J < 2) return null;

            var yc = y.Select(v => unik.IndexOf(v)).ToList();   
            var x = new double[n][];
            for (int i = 0; i < n; i++)
            {
                x[i] = new double[k];
                x[i][0] = 1.0;
                for (int j = 0; j < p; j++) x[i][j + 1] = prediktor[j][i];
            }

            int m = (J - 1) * k;            
            var beta = new double[m];       
            int iterasi = 0;
            bool konvergen = false;

            
            void Hitung(double[] b, out double[,] Pmat, out double[] g, out double[,] I)
            {
                Pmat = new double[n, J - 1];
                g = new double[m];
                I = new double[m, m];
                for (int i = 0; i < n; i++)
                {
                    var eta = new double[J - 1];
                    for (int j = 1; j < J; j++)
                    {
                        double s = 0;
                        for (int c = 0; c < k; c++) s += x[i][c] * b[(j - 1) * k + c];
                        eta[j - 1] = s;
                    }
                    double sumExp = 0;
                    for (int j = 1; j < J; j++) { Pmat[i, j - 1] = Math.Exp(eta[j - 1]); sumExp += Pmat[i, j - 1]; }
                    double denom = 1 + sumExp;
                    for (int j = 1; j < J; j++) Pmat[i, j - 1] /= denom;

                    double pi0 = 1.0 / denom;
                    for (int j = 1; j < J; j++)
                    {
                        int rj = (j - 1) * k;
                        double yij = yc[i] == j ? 1.0 : 0.0;
                        double resid = yij - Pmat[i, j - 1];
                        for (int c = 0; c < k; c++) g[rj + c] += x[i][c] * resid;
                    }
                    for (int a = 1; a < J; a++)
                        for (int jb = 1; jb < J; jb++)
                        {
                            double w = Pmat[i, a - 1] * ((a == jb ? 1.0 : 0.0) - Pmat[i, jb - 1]);
                            int ra = (a - 1) * k, rb = (jb - 1) * k;
                            for (int ca = 0; ca < k; ca++)
                                for (int cb = 0; cb < k; cb++)
                                    I[ra + ca, rb + cb] += x[i][ca] * x[i][cb] * w;
                        }
                    _ = pi0;
                }
            }

            for (iterasi = 1; iterasi <= ITERASI_MAKS; iterasi++)
            {
                Hitung(beta, out _, out var g, out var I);
                var inv = Regression.Inverse((double[,])I.Clone());
                if (inv is null)
                {
                    return new HasilMultinomial
                    {
                        N = n, K = p, J = J, Iterasi = iterasi, Konvergen = false,
                        Kategori = unik.Select(u => "Kategori " + Fmt.Num(u, 0)).ToList(),
                        Catatan = "Matriks informasi tidak bisa dibalik (prediktor bergantung)."
                    };
                }
                var delta = new double[m];
                for (int a = 0; a < m; a++)
                {
                    double s = 0;
                    for (int b = 0; b < m; b++) s += inv[a, b] * g[b];
                    delta[a] = s;
                }
                double beda = 0;
                for (int a = 0; a < m; a++) { beta[a] += delta[a]; beda = Math.Max(beda, Math.Abs(delta[a])); }
                if (beda < TOLERANSI) { konvergen = true; break; }
            }

            Hitung(beta, out var Pmat, out _, out var Ifin);
            var invFin = Regression.Inverse((double[,])Ifin.Clone());
            if (invFin is null)
            {
                return new HasilMultinomial
                {
                    N = n, K = p, J = J, Iterasi = iterasi, Konvergen = false,
                    Kategori = unik.Select(u => "Kategori " + Fmt.Num(u, 0)).ToList(),
                    Catatan = "Matriks informasi tidak bisa dibalik pada beta akhir."
                };
            }

            var B = new double[J - 1, k];
            var Se = new double[J - 1, k];
            var Z = new double[J - 1, k];
            var Pv = new double[J - 1, k];
            for (int j = 1; j < J; j++)
                for (int c = 0; c < k; c++)
                {
                    double bv = beta[(j - 1) * k + c];
                    double sv = invFin[(j - 1) * k + c, (j - 1) * k + c];
                    double se = sv > 0 ? Math.Sqrt(sv) : double.NaN;
                    B[j - 1, c] = bv;
                    Se[j - 1, c] = se;
                    Z[j - 1, c] = se > 0 ? bv / se : double.NaN;
                    Pv[j - 1, c] = double.IsNaN(Z[j - 1, c]) ? double.NaN
                        : 2.0 * Distributions.NormalCdf(-Math.Abs(Z[j - 1, c]));
                }

            double ll = 0;
            for (int i = 0; i < n; i++)
            {
                double lin = 0;
                for (int j = 1; j < J; j++)
                {
                    double s = 0;
                    for (int c = 0; c < k; c++) s += x[i][c] * B[j - 1, c];
                    if (yc[i] == j) lin += s;
                }
                double denom = 1.0;
                for (int j = 1; j < J; j++)
                {
                    double s = 0;
                    for (int c = 0; c < k; c++) s += x[i][c] * B[j - 1, c];
                    denom += Math.Exp(s);
                }
                ll += lin - Math.Log(denom);
            }

            var hitungKat = new int[J];
            foreach (int v in yc) hitungKat[v]++;
            double llNol = 0;
            for (int j = 0; j < J; j++)
                if (hitungKat[j] > 0) llNol += hitungKat[j] * Math.Log((double)hitungKat[j] / n);
            double pseudo = Math.Abs(llNol) > 1e-12 ? 1 - ll / llNol : double.NaN;

            if (!konvergen)
                return new HasilMultinomial
                {
                    N = n, K = p, J = J, Iterasi = iterasi, Konvergen = false,
                    B = B, Se = Se, Z = Z, P = Pv,
                    Kategori = unik.Select(u => "Kategori " + Fmt.Num(u, 0)).ToList(),
                    Catatan = $"Tidak konvergen setelah {ITERASI_MAKS} iterasi; koefisien belum stabil."
                };

            return new HasilMultinomial
            {
                N = n, K = p, J = J, Iterasi = iterasi, Konvergen = konvergen,
                B = B, Se = Se, Z = Z, P = Pv, LogLik = ll, LogLikNol = llNol, PseudoR2 = pseudo,
                Kategori = unik.Select(u => "Kategori " + Fmt.Num(u, 0)).ToList()
            };
        }

        public static List<ResultBlock> MultinomialBlocks(Dataset ds, string dependen, List<string> prediktor,
                                                          double alfaT = 0.05)
        {
            var blok = new List<ResultBlock> { Blocks.Heading($"Regresi Multinomial — {dependen}", 1) };

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
                if (unikTeks.Count < 2)
                {
                    blok.Add(Blocks.Note($"Variabel terikat harus punya sedikitnya dua macam nilai "
                                         + $"(angka maupun teks); ditemukan {unikTeks.Count}.", NoteKind.Error));
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

            if (nilaiY.Distinct().Count() < 2)
            {
                blok.Add(Blocks.Note("Variabel terikat harus punya sedikitnya dua macam nilai.",
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

            blok.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.RegresiMultinomial));

            string teksKat = string.Join(", ", h.Kategori.Select((s, idx) =>
                idx == 0 ? $"{s} (dasar)" : s));
            blok.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Banyak amatan", $"n = {h.N}, prediktor = {h.K} (konstanta disertakan)"),
                ("Kategori terikat", teksKat),
                ("Iterasi penskoran Fisher", $"{h.Iterasi} kali, konvergen = {(h.Konvergen ? "ya" : "tidak")}"),
                ("Log-peluang", $"ln L = {Fmt.Num(h.LogLik, 4)}, ln L₀ = {Fmt.Num(h.LogLikNol, 4)}"),
                ("Pseudo R² (McFadden)", $"1 − ln L / ln L₀ = {Fmt.Num(h.PseudoR2, 4)}")));

            if (!h.Konvergen)
            {
                blok.Add(Blocks.Note(h.Catatan ?? "Model tidak konvergen.", NoteKind.Warning));
                return blok;
            }

            var namaKol = new List<string> { "(Konstanta)" };
            namaKol.AddRange(prediktor);
            for (int j = 1; j < h.J; j++)
            {
                var baris = new List<List<string>>();
                for (int c = 0; c < h.K + 1; c++)
                {
                    double b = h.B[j - 1, c], se = h.Se[j - 1, c];
                    double batas = 1.959963984540054 * se;
                    baris.Add(new List<string>
                    {
                        namaKol[c],
                        Fmt.Num(b, 4),
                        Fmt.Num(se, 4),
                        Fmt.Num(h.Z[j - 1, c], 3),
                        Fmt.P(h.P[j - 1, c]),
                        $"[{Fmt.Num(b - batas, 4)}; {Fmt.Num(b + batas, 4)}]"
                    });
                }
                blok.Add(Blocks.Table($"Koefisien (kategori {h.Kategori[j]} vs {h.Kategori[0]})",
                    new[] { "Variabel", "B", "Galat baku", "z", "p", "Selang 95% untuk B" },
                    baris,
                    "B = log peluang relatif terhadap kategori dasar. Uji z memakai sebaran normal."));
            }

            blok.Add(Blocks.Table("Ringkasan model",
                new[] { "N", "ln L", "ln L₀", "Pseudo R²", "Iterasi" },
                new[] { new[] { h.N.ToString(), Fmt.Num(h.LogLik, 4), Fmt.Num(h.LogLikNol, 4),
                               Fmt.Num(h.PseudoR2, 4), h.Iterasi.ToString() } },
                "Pseudo R² McFadden untuk multinomial biasanya jauh lebih kecil daripada R² regresi linear."));

            return blok;
        }
    }
}
