using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    // Imputasi berganda MICE (chained equations, van Buuren):
    // tiap variabel berhampas dimodelkan dari yang lain secara
    // bergiliran — numerik via OLS Bayes (taksir β, σ² dari posterior,
    public static class Imputasi
    {
        private sealed class Lcg
        {
            private uint _s;
            public Lcg(uint benih) { _s = benih == 0 ? 1u : benih; }
            public double Seragam()
            {
                _s = _s * 1664525u + 1013904223u;
                return (_s >> 8) / 16777216.0;
            }
        }

        private sealed class Acak
        {
            private readonly Lcg _rng;
            private bool _punya;
            private double _simpan;
            public Acak(uint benih) { _rng = new Lcg(benih); }
            public double Seragam() => _rng.Seragam();
            public double Normal()
            {
                if (_punya) { _punya = false; return _simpan; }
                double u1 = Math.Max(_rng.Seragam(), 1e-300);
                double u2 = _rng.Seragam();
                double r = Math.Sqrt(-2 * Math.Log(u1));
                _simpan = r * Math.Sin(2 * Math.PI * u2);
                _punya = true;
                return r * Math.Cos(2 * Math.PI * u2);
            }
            public double KhiKuadrat(int df)
            {
                double s = 0;
                for (int i = 0; i < df; i++) { double z = Normal(); s += z * z; }
                return s;
            }
        }

        // Cholesky bawah L (A = L·L′); null bila tak-definit-positif.
        private static double[,]? Cholesky(double[,] a)
        {
            int n = a.GetLength(0);
            var l = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j <= i; j++)
                {
                    double s = a[i, j];
                    for (int k = 0; k < j; k++) s -= l[i, k] * l[j, k];
                    if (i == j)
                    {
                        if (s <= 0) return null;
                        l[i, j] = Math.Sqrt(s);
                    }
                    else
                    {
                        if (l[j, j] <= 0) return null;
                        l[i, j] = s / l[j, j];
                    }
                }
            return l;
        }

        public sealed class Kolom
        {
            public string Nama = "";
            public bool Numerik;
            // Nilai kini (terisi penuh setelah inisialisasi).
            public double[] Nilai = Array.Empty<double>();
            // Masker hilang asli.
            public bool[] Hilang = Array.Empty<bool>();
            // Label biner (2 aras) — kosong bila numerik.
            public List<string> Aras = new();
        }

        public sealed class Rangka
        {
            public List<Kolom> Koloms = new();
            public int N;
        }

        public sealed class GabungRerata
        {
            public string Nama = "";
            public double Rerata, GalatBaku, Df, Fmi;
            public int NHilang;
            public double RerataCC;
        }

        public sealed class GabungKoef
        {
            public string Nama = "";
            public double B, Se, Df, Fmi;
        }

        public sealed class Hasil
        {
            public int M, Siklus, N;
            public uint Benih;
            public int Fallback;
            public List<GabungRerata> Rerata = new();
            public List<GabungKoef> Koefisien = new();
            public List<string> NamaKoef = new();
            // Jejak rerata per siklus (imputasi pertama) + nama kolom.
            public List<string> JejakNama = new();
            public List<double[]> Jejak = new();
        }

        private static double[,] Rancang(double[][] kolom)
        {
            int n = kolom[0].Length, k = kolom.Length + 1;
            var x = new double[n, k];
            for (int i = 0; i < n; i++)
            {
                x[i, 0] = 1.0;
                for (int j = 0; j < kolom.Length; j++) x[i, j + 1] = kolom[j][i];
            }
            return x;
        }

        // Matriks rancangan intersep saja: n baris × 1 kolom berisi 1.
        private static double[,] RancangIntersep(int n)
        {
            var x = new double[n, 1];
            for (int i = 0; i < n; i++) x[i, 0] = 1.0;
            return x;
        }

        private static double[] KaliMatriksVektor(double[,] a, double[] v)
        {
            int n = a.GetLength(0), k = a.GetLength(1);
            var h = new double[n];
            for (int i = 0; i < n; i++)
            {
                double s = 0;
                for (int j = 0; j < k; j++) s += a[i, j] * v[j];
                h[i] = s;
            }
            return h;
        }

        // Satu langkah Bayes-OLS: taksir dari baris osservasi, imput yang hilang.
        private static void ImputNumerik(double[] yFull, bool[] hilang, double[,] xFull,
                                         List<int> idxObs, List<int> idxMis, Acak acak,
                                         ref int fallback)
        {
            int k = xFull.GetLength(1);
            if (idxObs.Count < k + 1 || idxMis.Count == 0) { fallback++; return; }
            var xtx = new double[k, k];
            var xty = new double[k];
            foreach (int i in idxObs)
                for (int a = 0; a < k; a++)
                {
                    xty[a] += xFull[i, a] * yFull[i];
                    for (int b = 0; b < k; b++) xtx[a, b] += xFull[i, a] * xFull[i, b];
                }
            var inv = Regression.Inverse((double[,])xtx.Clone());
            if (inv is null) { fallback++; return; }
            var beta = new double[k];
            for (int a = 0; a < k; a++)
            {
                double s = 0;
                for (int b = 0; b < k; b++) s += inv[a, b] * xty[b];
                beta[a] = s;
            }
            double rss = 0;
            foreach (int i in idxObs)
            {
                double e = yFull[i];
                for (int a = 0; a < k; a++) e -= xFull[i, a] * beta[a];
                rss += e * e;
            }
            int df = idxObs.Count - k;
            if (df < 1) { fallback++; return; }
            double chi = acak.KhiKuadrat(df);
            if (chi <= 0) { fallback++; return; }
            double sigma2 = rss / chi;
            var l = Cholesky(inv);
            if (l is null) { fallback++; return; }
            var betaBintang = new double[k];
            for (int a = 0; a < k; a++)
            {
                double s = beta[a];
                for (int b = 0; b <= a; b++) s += l[a, b] * acak.Normal() * Math.Sqrt(sigma2);
                betaBintang[a] = s;
            }
            double sd = Math.Sqrt(sigma2);
            foreach (int i in idxMis)
            {
                double mu = 0;
                for (int a = 0; a < k; a++) mu += xFull[i, a] * betaBintang[a];
                yFull[i] = mu + sd * acak.Normal();
            }
        }

        // Satu langkah logit-Bayes (Laplace): MAP via IRLS teruji, lalu
        // ambil β* ~ Normal(MAP, kov) dan undi Bernoulli.
        private static void ImputBiner(double[] yFull, bool[] hilang, double[,] xFull,
                                       List<int> idxObs, List<int> idxMis, Acak acak,
                                       ref int fallback)
        {
            int k = xFull.GetLength(1);
            if (idxObs.Count < k + 2 || idxMis.Count == 0) { fallback++; return; }
            var predObs = new List<double[]>();
            for (int a = 1; a < k; a++)
            {
                var c = new double[idxObs.Count];
                for (int t = 0; t < idxObs.Count; t++) c[t] = xFull[idxObs[t], a];
                predObs.Add(c);
            }
            var namaPred = predObs.Select((_, j) => $"x{j}").ToList();
            var sukses = idxObs.Select(i => yFull[i]).ToArray();
            var coba = Enumerable.Repeat(1.0, idxObs.Count).ToArray();
            var h0 = GlmMulti.FitBinom(sukses, coba, predObs, namaPred);
            if (h0 is null || !h0.Konvergen) { fallback++; return; }
            // Kovarians (X'WX)⁻¹ dari mu terpasang.
            var mu = new double[idxObs.Count];
            for (int t = 0; t < idxObs.Count; t++)
            {
                double eta = h0.B[0];
                for (int a = 1; a < k; a++) eta += h0.B[a] * xFull[idxObs[t], a];
                mu[t] = eta >= 0 ? 1.0 / (1.0 + Math.Exp(-eta))
                                 : Math.Exp(eta) / (1.0 + Math.Exp(eta));
                mu[t] = Math.Min(Math.Max(mu[t], 1e-9), 1 - 1e-9);
            }
            var xtwx = new double[k, k];
            for (int t = 0; t < idxObs.Count; t++)
            {
                double w = mu[t] * (1 - mu[t]);
                for (int a = 0; a < k; a++)
                    for (int b = 0; b < k; b++)
                        xtwx[a, b] += xFull[idxObs[t], a] * w * xFull[idxObs[t], b];
            }
            var inv = Regression.Inverse((double[,])xtwx.Clone());
            var l = inv is null ? null : Cholesky(inv);
            if (l is null) { fallback++; return; }
            var betaBintang = new double[k];
            for (int a = 0; a < k; a++)
            {
                double s = h0.B[a];
                for (int b = 0; b <= a; b++) s += l[a, b] * acak.Normal();
                betaBintang[a] = s;
            }
            foreach (int i in idxMis)
            {
                double eta = 0;
                for (int a = 0; a < k; a++) eta += xFull[i, a] * betaBintang[a];
                double p = 1.0 / (1.0 + Math.Exp(-Math.Max(-700, Math.Min(700, eta))));
                yFull[i] = acak.Seragam() < p ? 1.0 : 0.0;
            }
        }

        public static Rangka BangunRangka(Dataset ds, List<string> numerik,
                                          List<string> biner)
        {
            var rangka = new Rangka();
            int n = ds.RowCount;
            rangka.N = n;
            foreach (var nm in numerik)
            {
                var semua = ds.Numeric(nm);
                var kol = new Kolom { Nama = nm, Numerik = true };
                kol.Nilai = new double[n];
                kol.Hilang = new bool[n];
                for (int i = 0; i < n; i++)
                {
                    if (semua[i].HasValue) kol.Nilai[i] = semua[i]!.Value;
                    else { kol.Nilai[i] = double.NaN; kol.Hilang[i] = true; }
                }
                rangka.Koloms.Add(kol);
            }
            foreach (var nm in biner)
            {
                var teks = ds.Text(nm);
                var label = new List<string>();
                for (int i = 0; i < n; i++)
                {
                    string? s = teks[i];
                    if (!string.IsNullOrWhiteSpace(s) && !label.Contains(s!.Trim()))
                        label.Add(s!.Trim());
                }
                label.Sort(StringComparer.Ordinal);
                var kol = new Kolom { Nama = nm, Numerik = false, Aras = label };
                kol.Nilai = new double[n];
                kol.Hilang = new bool[n];
                for (int i = 0; i < n; i++)
                {
                    string? s = teks[i];
                    if (string.IsNullOrWhiteSpace(s)) { kol.Hilang[i] = true; kol.Nilai[i] = -1; }
                    else kol.Nilai[i] = label.IndexOf(s!.Trim());
                }
                rangka.Koloms.Add(kol);
            }
            return rangka;
        }

        // Rancang prediktor untuk kolom j (kolom lain: numerik mentah,
        // biner 0/1). Mengembalikan (x[n,k], obs, mis).
        private static (double[,] X, List<int> Obs, List<int> Mis) RancangPrediktor(
            Rangka rangka, int j)
        {
            int n = rangka.N;
            var kols = new List<double[]>();
            foreach (var (kol, jj) in rangka.Koloms.Select((v, i) => (v, i)))
            {
                if (jj == j) continue;
                if (kol.Numerik) kols.Add(kol.Nilai);
                else kols.Add(kol.Nilai.Select(v => v < 0 ? 0.0 : v).ToArray());
            }
            // Tanpa prediktor lain (satu kolom), Rancang mengakses kolom[0] yang
            // tidak ada. Intersep saja sudah cukup sebagai matriks rancangan.
            var X = kols.Count == 0 ? RancangIntersep(n) : Rancang(kols.ToArray());
            var target = rangka.Koloms[j];
            var obs = new List<int>();
            var mis = new List<int>();
            for (int i = 0; i < n; i++)
                (target.Hilang[i] ? mis : obs).Add(i);
            return (X, obs, mis);
        }

        public static (List<Rangka> Imps, int Fallback) Mice(Rangka awal, int m,
            int siklus, uint benih, List<double[]>? jejak = null,
            List<string>? jejakNama = null)
        {
            int n = awal.N;
            int fallbackTotal = 0;
            var keluar = new List<Rangka>();
            for (int imp = 0; imp < m; imp++)
            {
                // Salin + inisialisasi (rerata/modus).
                var rk = new Rangka { N = n };
                foreach (var k in awal.Koloms)
                {
                    var c = new Kolom
                    {
                        Nama = k.Nama, Numerik = k.Numerik,
                        Nilai = (double[])k.Nilai.Clone(),
                        Hilang = (bool[])k.Hilang.Clone(),
                        Aras = new List<string>(k.Aras),
                    };
                    rk.Koloms.Add(c);
                }
                foreach (var c in rk.Koloms)
                {
                    var ada = Enumerable.Range(0, n).Where(i => !c.Hilang[i]).ToList();
                    if (ada.Count == 0) continue;
                    if (c.Numerik)
                    {
                        double rata = ada.Average(i => c.Nilai[i]);
                        for (int i = 0; i < n; i++)
                            if (c.Hilang[i]) c.Nilai[i] = rata;
                    }
                    else
                    {
                        var hit = new Dictionary<double, int>();
                        foreach (int i in ada)
                            hit[c.Nilai[i]] = hit.TryGetValue(c.Nilai[i], out int v) ? v + 1 : 1;
                        double modus = hit.OrderByDescending(kv => kv.Value).First().Key;
                        for (int i = 0; i < n; i++)
                            if (c.Hilang[i]) c.Nilai[i] = modus;
                    }
                }
                var acak = new Acak((uint)(benih + (uint)imp * 7919u));
                int fallback = 0;
                for (int s = 0; s < siklus; s++)
                {
                    for (int j = 0; j < rk.Koloms.Count; j++)
                    {
                        // Satu kolom berarti tanpa prediktor: nilai hilang sudah terisi
                        // rerata/modus saat inisialisasi, jangan ditimpa regresi.
                        if (rk.Koloms.Count < 2) continue;
                        var (X, obs, mis) = RancangPrediktor(rk, j);
                        if (mis.Count == 0) continue;
                        if (obs.Count == 0) { fallback++; continue; }
                        if (rk.Koloms[j].Numerik)
                            ImputNumerik(rk.Koloms[j].Nilai, rk.Koloms[j].Hilang,
                                         X, obs, mis, acak, ref fallback);
                        else
                            ImputBiner(rk.Koloms[j].Nilai, rk.Koloms[j].Hilang,
                                       X, obs, mis, acak, ref fallback);
                    }
                    if (imp == 0 && jejak is not null)
                    {
                        var baris = new double[rk.Koloms.Count];
                        for (int j = 0; j < rk.Koloms.Count; j++)
                            baris[j] = rk.Koloms[j].Nilai.Average();
                        jejak.Add(baris);
                    }
                }
                if (imp == 0 && jejakNama is not null)
                    foreach (var c in rk.Koloms) jejakNama.Add(c.Nama);
                fallbackTotal += fallback;
                keluar.Add(rk);
            }
            return (keluar, fallbackTotal);
        }

        private static (double Q, double T, double Df, double Fmi) Rubin(
            double[] q, double[] u)
        {
            int m = q.Length;
            double qbar = q.Average();
            double w = u.Average();
            double b = m > 1 ? q.Sum(v => (v - qbar) * (v - qbar)) / (m - 1) : 0;
            double t = w + (1 + 1.0 / m) * b;
            double df;
            if (b <= 0) df = double.PositiveInfinity;
            else
            {
                double r = (1 + 1.0 / m) * b / w;
                df = w <= 0 ? m - 1 : (m - 1) * (1 + 1 / r) * (1 + 1 / r);
            }
            double fmi = t > 0 ? (b + b / m) / t : 0;
            return (qbar, t, df, Math.Min(Math.Max(fmi, 0), 1));
        }

        public static Hasil Pasang(Rangka awal, string dependen, List<string> bebas,
                                   int m, int siklus, uint benih)
        {
            var hasil = new Hasil { M = m, Siklus = siklus, N = awal.N, Benih = benih };
            var jejak = new List<double[]>();
            var jejakNama = new List<string>();
            var (imps, fallback) = Mice(awal, m, siklus, benih, jejak, jejakNama);
            hasil.JejakNama = jejakNama;
            hasil.Jejak = jejak;
            hasil.Fallback = fallback;

            // Rerata tergabung per kolom.
            foreach (var (kol0, j) in awal.Koloms.Select((v, i) => (v, i)))
            {
                var q = new double[m];
                var u = new double[m];
                for (int m2 = 0; m2 < m; m2++)
                {
                    var v = imps[m2].Koloms[j].Nilai;
                    double rata = v.Average();
                    q[m2] = rata;
                    double rag = v.Sum(t => (t - rata) * (t - rata)) / Math.Max(1, v.Length - 1);
                    u[m2] = rag / v.Length;
                }
                var (Q, T, Df, Fmi) = Rubin(q, u);
                int nHilang = kol0.Hilang.Count(v => v);
                double rataCC = kol0.Hilang.Any(v => v)
                    ? Enumerable.Range(0, awal.N).Where(i => !kol0.Hilang[i])
                        .Average(i => kol0.Nilai[i])
                    : Q;
                hasil.Rerata.Add(new GabungRerata
                {
                    Nama = kol0.Nama, Rerata = Q, GalatBaku = Math.Sqrt(Math.Max(0, T)),
                    Df = Df, Fmi = Fmi, NHilang = nHilang, RerataCC = rataCC,
                });
            }

            // Regresi OLS tergabung (bila diminta).
            if (!string.IsNullOrEmpty(dependen) && bebas.Count > 0)
            {
                var idxDep = awal.Koloms.FindIndex(k => k.Nama == dependen);
                var idxBebas = bebas.Select(b => awal.Koloms.FindIndex(k => k.Nama == b))
                                    .Where(i => i >= 0).ToList();
                if (idxDep >= 0 && idxBebas.Count > 0)
                {
                    int k = idxBebas.Count;
                    var bMat = new List<double[]>();
                    var seMat = new List<double[]>();
                    foreach (var rk in imps)
                    {
                        var y = rk.Koloms[idxDep].Nilai;
                        var pred = idxBebas.Select(i => rk.Koloms[i].Nilai).ToList();
                        var fit = Regression.Fit(y, pred);
                        if (fit is null) continue;
                        bMat.Add(fit.Beta);
                        seMat.Add(fit.Se.Select(s => s * s).ToArray());
                    }
                    if (bMat.Count == m)
                    {
                        var namaK = new List<string> { "(Konstanta)" };
                        namaK.AddRange(idxBebas.Select(i => awal.Koloms[i].Nama));
                        hasil.NamaKoef = namaK;
                        for (int a = 0; a < k + 1; a++)
                        {
                            var (Q, T, Df, Fmi) = Rubin(
                                bMat.Select(b => b[a]).ToArray(),
                                seMat.Select(s => s[a]).ToArray());
                            hasil.Koefisien.Add(new GabungKoef
                            {
                                Nama = namaK[a], B = Q,
                                Se = Math.Sqrt(Math.Max(0, T)), Df = Df, Fmi = Fmi,
                            });
                        }
                    }
                }
            }
            return hasil;
        }

        private static string FmtDf(double df)
            => double.IsPositiveInfinity(df) ? "∞" : Fmt.Num(df, 1);

        public static List<ResultBlock> ImputasiBlocks(
            Dataset ds, List<string> numerik, List<string> biner,
            string dependen, List<string> bebas,
            int m, int siklus, uint benih)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Imputasi berganda MICE (chained equations)", 1)
            };
            if (numerik.Count + biner.Count < 1)
            {
                blocks.Add(Blocks.Note("Pilih sedikitnya satu variabel.",
                                       NoteKind.Error));
                return blocks;
            }
            // Biner harus tepat 2 aras (batas v1).
            foreach (var nm in biner)
            {
                var teks = ds.Text(nm);
                var aras = new List<string>();
                for (int i = 0; i < ds.RowCount; i++)
                {
                    string? s = teks[i];
                    if (!string.IsNullOrWhiteSpace(s) && !aras.Contains(s!.Trim()))
                        aras.Add(s!.Trim());
                }
                if (aras.Count != 2)
                {
                    blocks.Add(Blocks.Note(
                        $"Variabel '{nm}' punya {aras.Count} aras — v1 hanya "
                        + "mendukung biner tepat 2 aras.", NoteKind.Error));
                    return blocks;
                }
            }
            var awal = BangunRangka(ds, numerik, biner);
            if (awal.Koloms.Any(k => k.Hilang.All(v => v)))
            {
                blocks.Add(Blocks.Note("Ada variabel yang seluruhnya hilang.",
                                       NoteKind.Error));
                return blocks;
            }
            if (awal.Koloms.Count >= 2 && awal.Koloms.All(k => !k.Hilang.Any(v => v)))
            {
                blocks.Add(Blocks.Note("Tidak ada nilai hilang — tak ada yang diimputasi.",
                                       NoteKind.Warning));
            }

            var h = Pasang(awal, dependen, bebas, m, siklus, benih);
            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.ImputasiMice, DaftarRumus.ImputasiRubin));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Numerik", numerik.Count > 0 ? string.Join(", ", numerik) : "(tidak ada)"),
                ("Biner", biner.Count > 0 ? string.Join(", ", biner) : "(tidak ada)"),
                ("Imputasi × siklus", $"{Fmt.Int(m)} × {Fmt.Int(siklus)}"),
                ("Benih", $"{benih}"),
                ("Baris", $"n = {Fmt.Int(awal.N)}"),
                ("Fallback rerata/modus", $"{Fmt.Int(h.Fallback)} sel")));

            blocks.Add(Blocks.Table(
                "Kehilangan per variabel",
                new[] { "Variabel", "Hilang", "%", "Rerata CC" },
                h.Rerata.Select(r => new[]
                {
                    r.Nama, Fmt.Int(r.NHilang),
                    Fmt.Num(100.0 * r.NHilang / Math.Max(1, awal.N), 1),
                    Fmt.Num(r.RerataCC, 4),
                }).ToArray(),
                "CC = kasus-lengkap (rata-rata baris tak-hilang)."));

            if (h.Jejak.Count > 0)
                blocks.Add(Blocks.Table(
                    $"Jejak rerata per siklus (imputasi pertama)",
                    new[] { "Siklus" }.Concat(h.JejakNama).ToArray(),
                    h.Jejak.Select((b, i) => new[] { Fmt.Int(i + 1) }.Concat(
                        b.Select(v => Fmt.Num(v, 4))).ToArray()).ToArray(),
                    "Stabil = rantai sudah baur; naik-turun liar = tambah siklus."));

            blocks.Add(Blocks.Table(
                "Rerata tergabung Rubin",
                new[] { "Variabel", "Rerata", "Galat baku", "df", "FMI" },
                h.Rerata.Select(r => new[]
                {
                    r.Nama, Fmt.Num(r.Rerata, 4), Fmt.Num(r.GalatBaku, 4),
                    FmtDf(r.Df), Fmt.Num(r.Fmi, 4),
                }).ToArray(),
                "FMI = proporsi informasi yang hilang akibat ketaklengkapan. "
                + "Biner dilaporkan sebagai proporsi aras kedua."));

            if (h.Koefisien.Count > 0)
                blocks.Add(Blocks.Table(
                    $"Regresi OLS tergabung: {dependen} ~ {string.Join(" + ", bebas)}",
                    new[] { "Suku", "B", "Galat baku", "df", "FMI" },
                    h.Koefisien.Select(k => new[]
                    {
                        k.Nama, Fmt.Num(k.B, 6), Fmt.Num(k.Se, 6),
                        FmtDf(k.Df), Fmt.Num(k.Fmi, 4),
                    }).ToArray(),
                    "Galat baku Rubin (dalam + antar-imputasi) — lebih jujur "
                    + "daripada imputasi-tunggal yang menganggap isian pasti."));
            return blocks;
        }
    }
}
