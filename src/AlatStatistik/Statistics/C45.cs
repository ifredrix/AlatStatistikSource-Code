using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    // C4.5 (Quinlan 1993): pohon klasifikasi dengan nisbah gain.
    // Kategorik dibelah multi-arah (satu cabang per nilai), numerik
    // dibelah biner pada titik-tengah; kriteria = gain informasi dibagi
    public static class C45
    {
        public sealed class Simpul
        {
            public bool Daun;
            public int Kelas;
            public int[] Cacah = Array.Empty<int>();
            public int N;
            // kategorik: multi-arah; numerik: biner (Ambang, Kiri/Kanan)
            public int Prediktor = -1;
            public bool Numerik;
            public double Ambang;
            public List<List<int>> Grup = new();
            public List<Simpul> Anak = new();
            public double GalatDuga;
        }

        public sealed class Hasil
        {
            public List<string> Kelas = new();
            public List<string> Prediktor = new();
            public bool[] AdalahNumerik = Array.Empty<bool>();
            public Simpul Akar = new();
            public int Kedalaman;
            public int SimpulPenuh, SimpulPangkas;
            public int[] Label = Array.Empty<int>();
            public double Cf;
            public int MinBelah, MaksDalam;
        }

        private static double Entropi(int[] cacah, int n)
        {
            if (n <= 0) return 0;
            double s = 0;
            foreach (int c in cacah)
            {
                if (c <= 0) continue;
                double p = (double)c / n;
                s -= p * Math.Log(p, 2);
            }
            return s;
        }

        // Invers Beta terregulasi via biseksi (monoton naik terhadap x).
        public static double BetaInv(double p, double a, double b)
        {
            double lo = 0, hi = 1;
            for (int i = 0; i < 200; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (Special.BetaInc(a, b, mid) < p) lo = mid;
                else hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        // Batas-atas galat binomial (koreksi kontinuitas + CF).
        public static double Ucf(int galat, int n, double cf)
        {
            if (n <= 0) return 1.0;
            if (galat >= n) return 1.0;
            return BetaInv(cf, galat + 1, n - galat);
        }

        private sealed class Cari
        {
            public int[][] Xk = Array.Empty<int[]>();
            public double[][] Xn = Array.Empty<double[]>();
            public int[] Y = Array.Empty<int>();
            public int NKelas, NPred;
            public bool[] Numerik = Array.Empty<bool>();
            public int MinBelah, MaksDalam;
            public int Kedalaman, CacahSimpul;
        }

        private static Simpul Tumbuh(Cari b, int[] idx, int dalam)
        {
            var node = new Simpul { N = idx.Length };
            b.CacahSimpul++;
            if (dalam > b.Kedalaman) b.Kedalaman = dalam;
            var cacah = new int[b.NKelas];
            foreach (int i in idx) cacah[b.Y[i]]++;
            node.Cacah = cacah;
            int menang = 0;
            for (int c = 1; c < b.NKelas; c++)
                if (cacah[c] > cacah[menang]) menang = c;
            node.Kelas = menang;
            double h0 = Entropi(cacah, idx.Length);
            if (dalam >= b.MaksDalam || idx.Length < b.MinBelah || h0 <= 0)
            {
                node.Daun = true;
                return node;
            }

            int fTerbaik = -1;
            bool numTerbaik = false;
            double ambTerbaik = 0, rasioTerbaik = 0;
            List<List<int>> grupTerbaik = new();
            for (int f = 0; f < b.NPred; f++)
            {
                if (b.Numerik[f])
                {
                    var urut = idx.OrderBy(i => b.Xn[f][i]).ToArray();
                    for (int t = 1; t < urut.Length; t++)
                    {
                        if (b.Xn[f][urut[t - 1]] >= b.Xn[f][urut[t]]) continue;
                        var cL = new int[b.NKelas];
                        var cR = new int[b.NKelas];
                        for (int k = 0; k < t; k++) cL[b.Y[urut[k]]]++;
                        for (int k = t; k < urut.Length; k++) cR[b.Y[urut[k]]]++;
                        double eL = Entropi(cL, t), eR = Entropi(cR, urut.Length - t);
                        double gain = h0 - (t * eL + (urut.Length - t) * eR) / urut.Length;
                        double p1 = (double)t / urut.Length;
                        double info = -(p1 * Math.Log(p1, 2) + (1 - p1) * Math.Log(1 - p1, 2));
                        if (info <= 1e-12) continue;
                        double rasio = gain / info;
                        // Seri dimenangi yang PERTAMA.
                        if (rasio > rasioTerbaik)
                        {
                            rasioTerbaik = rasio;
                            fTerbaik = f;
                            numTerbaik = true;
                            ambTerbaik = 0.5 * (b.Xn[f][urut[t - 1]] + b.Xn[f][urut[t]]);
                        }
                    }
                }
                else
                {
                    var kats = idx.Select(i => b.Xk[f][i]).Distinct().OrderBy(v => v).ToList();
                    if (kats.Count < 2) continue;
                    var hitung = kats.ToDictionary(v => v, v => new int[b.NKelas]);
                    var jml = kats.ToDictionary(v => v, v => 0);
                    foreach (int i in idx)
                    {
                        hitung[b.Xk[f][i]][b.Y[i]]++;
                        jml[b.Xk[f][i]]++;
                    }
                    double sisa = 0, info = 0;
                    foreach (int v in kats)
                    {
                        sisa += jml[v] * Entropi(hitung[v], jml[v]);
                        double p = (double)jml[v] / idx.Length;
                        info -= p * Math.Log(p, 2);
                    }
                    sisa /= idx.Length;
                    if (info <= 1e-12) continue;
                    double rasio = (h0 - sisa) / info;
                    if (rasio > rasioTerbaik)
                    {
                        rasioTerbaik = rasio;
                        fTerbaik = f;
                        numTerbaik = false;
                        grupTerbaik = kats.Select(v => new List<int> { v }).ToList();
                    }
                }
            }
            if (fTerbaik < 0 || rasioTerbaik <= 0)
            {
                node.Daun = true;
                return node;
            }
            node.Prediktor = fTerbaik;
            node.Numerik = numTerbaik;
            if (numTerbaik)
            {
                node.Ambang = ambTerbaik;
                var kiri = new List<int>();
                var kanan = new List<int>();
                foreach (int i in idx)
                    (b.Xn[fTerbaik][i] <= ambTerbaik ? kiri : kanan).Add(i);
                if (kiri.Count < 1 || kanan.Count < 1)
                {
                    node.Daun = true;
                    return node;
                }
                node.Anak.Add(Tumbuh(b, kiri.ToArray(), dalam + 1));
                node.Anak.Add(Tumbuh(b, kanan.ToArray(), dalam + 1));
            }
            else
            {
                node.Grup = grupTerbaik;
                var milik = grupTerbaik.Select((g, gi) => (g, gi))
                    .SelectMany(t => t.g.Select(v => (v, t.gi)))
                    .ToDictionary(t => t.v, t => t.gi);
                var anakIdx = new List<int>[grupTerbaik.Count];
                for (int g = 0; g < grupTerbaik.Count; g++) anakIdx[g] = new List<int>();
                foreach (int i in idx) anakIdx[milik[b.Xk[fTerbaik][i]]].Add(i);
                foreach (var a in anakIdx)
                    node.Anak.Add(Tumbuh(b, a.ToArray(), dalam + 1));
            }
            return node;
        }

        // Pemangkasan pesimis bottom-up (galat-atas binomial, CF).
        // Hanya penggantian-daun (tanpa grafting cabang) — batas yang
        // eksplisit dan sama di acuan Python.
        private static double Pangkas(Simpul node, double cf)
        {
            if (node.Daun)
            {
                int galat = node.N - node.Cacah[node.Kelas];
                node.GalatDuga = node.N * Ucf(galat, node.N, cf);
                return node.GalatDuga;
            }
            double anakGalat = 0;
            foreach (var a in node.Anak) anakGalat += Pangkas(a, cf);
            int galatDaun = node.N - node.Cacah[node.Kelas];
            double daunGalat = node.N * Ucf(galatDaun, node.N, cf);
            if (daunGalat <= anakGalat)
            {
                node.Daun = true;
                node.Anak.Clear();
                node.Grup.Clear();
                node.GalatDuga = daunGalat;
                return daunGalat;
            }
            node.GalatDuga = anakGalat;
            return anakGalat;
        }

        private static int HitungSimpul(Simpul node)
        {
            int c = 1;
            foreach (var a in node.Anak) c += HitungSimpul(a);
            return c;
        }

        public static int Duga(Simpul node, int[] xk, double[] xn)
        {
            while (!node.Daun)
            {
                if (node.Numerik)
                {
                    if (node.Anak.Count < 2) break;
                    node = xn[node.Prediktor] <= node.Ambang ? node.Anak[0] : node.Anak[1];
                }
                else
                {
                    int g = -1;
                    for (int k = 0; k < node.Grup.Count; k++)
                        if (node.Grup[k].Contains(xk[node.Prediktor])) { g = k; break; }
                    if (g < 0 || g >= node.Anak.Count) break;
                    node = node.Anak[g];
                }
            }
            return node.Kelas;
        }

        public static Hasil? Pasang(int[][] xk, double[][] xn, string[] namaPred,
            bool[] numerik, int[] y, List<string> kelas,
            double cf = 0.25, int minBelah = 2, int maksDalam = 8)
        {
            int n = y.Length;
            if (n < 4) return null;
            // Konvensi: xk dan xn SEPANJANG prediktor (dud untuk jenis
            // satunya) — sama seperti yang dibangun C45Blocks.
            if (xk.Length != namaPred.Length || xn.Length != namaPred.Length
                || numerik.Length != namaPred.Length) return null;
            var b = new Cari
            {
                Xk = xk, Xn = xn, Y = y, NKelas = kelas.Count, NPred = namaPred.Length,
                Numerik = numerik, MinBelah = Math.Max(2, minBelah),
                MaksDalam = Math.Max(1, maksDalam),
            };
            var akar = Tumbuh(b, Enumerable.Range(0, n).ToArray(), 0);
            int penuh = HitungSimpul(akar);
            Pangkas(akar, Math.Min(Math.Max(cf, 0.01), 0.99));
            var label = new int[n];
            for (int i = 0; i < n; i++)
            {
                var a = new int[xk.Length];
                var u = new double[xn.Length];
                for (int f = 0; f < xk.Length; f++) a[f] = xk[f][i];
                for (int f = 0; f < xn.Length; f++) u[f] = xn[f][i];
                label[i] = Duga(akar, a, u);
            }
            return new Hasil
            {
                Kelas = new List<string>(kelas),
                Prediktor = new List<string>(namaPred),
                AdalahNumerik = (bool[])numerik.Clone(),
                Akar = akar, Kedalaman = b.Kedalaman,
                SimpulPenuh = penuh, SimpulPangkas = HitungSimpul(akar),
                Label = label, Cf = cf, MinBelah = b.MinBelah, MaksDalam = b.MaksDalam,
            };
        }

        public static List<ResultBlock> C45Blocks(Dataset ds, List<string> prediktor,
            string target, double cf, int minBelah, int maksDalam)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("C4.5 (pohon nisbah gain + pangkas pesimis)", 1)
            };
            var perlu = new List<string>(prediktor) { target };
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < 4)
            {
                blocks.Add(Blocks.Note("Baris lengkap kurang dari 4.",
                                       NoteKind.Error));
                return blocks;
            }
            var tk = ds.Text(target);
            var yTeks = baris.Select(i => tk[i] ?? "(kosong)").ToArray();
            var kelas = yTeks.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (kelas.Count < 2)
            {
                blocks.Add(Blocks.Note("Target harus punya sedikitnya dua kategori.",
                                       NoteKind.Error));
                return blocks;
            }
            var y = yTeks.Select(t => kelas.IndexOf(t)).ToArray();

            var xk = new List<int[]>();
            var xn = new List<double[]>();
            var numerik = new List<bool>();
            var namaKat = new List<List<string>>();
            foreach (var nm in prediktor)
            {
                int idx = ds.IndexOf(nm);
                if (ds.Variables[idx].IsNumeric)
                {
                    var semuaKol = ds.Numeric(nm);
                    var nil = baris.Select(r => semuaKol[r] ?? double.NaN).ToArray();
                    if (nil.Any(double.IsNaN))
                    {
                        blocks.Add(Blocks.Note($"Prediktor '{nm}' punya nilai hilang.",
                                               NoteKind.Error));
                        return blocks;
                    }
                    xn.Add(nil);
                    xk.Add(new int[baris.Count]);
                    numerik.Add(true);
                    namaKat.Add(new List<string>());
                }
                else
                {
                    var teks = baris.Select(r => ds.Text(nm)[r] ?? "(kosong)").ToArray();
                    var label = teks.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
                    if (label.Count < 2)
                    {
                        blocks.Add(Blocks.Note($"Prediktor '{nm}' hanya satu kategori.",
                                               NoteKind.Error));
                        return blocks;
                    }
                    var peta = label.Select((t, i) => (t, i))
                                    .ToDictionary(t => t.t, t => t.i);
                    xk.Add(teks.Select(t => peta[t]).ToArray());
                    xn.Add(new double[baris.Count]);
                    numerik.Add(false);
                    namaKat.Add(label);
                }
            }

            var h = Pasang(xk.ToArray(), xn.ToArray(), prediktor.ToArray(),
                           numerik.ToArray(), y, kelas, cf, minBelah, maksDalam);
            if (h is null)
            {
                blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.",
                                       NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.C45));

            var bingung = new int[kelas.Count, kelas.Count];
            for (int i = 0; i < y.Length; i++) bingung[y[i], h.Label[i]]++;
            int benar = 0;
            for (int c = 0; c < kelas.Count; c++) benar += bingung[c, c];
            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Prediktor", string.Join(", ", prediktor)),
                ("Target", $"{target} ({string.Join(", ", kelas)})"),
                ("Batas", $"CF {Fmt.Num(cf, 2)}, belah ≥ {Fmt.Int(h.MinBelah)}, "
                          + $"dalam ≤ {Fmt.Int(h.MaksDalam)}"),
                ("Simpul penuh → pangkas", $"{Fmt.Int(h.SimpulPenuh)} → {Fmt.Int(h.SimpulPangkas)}"),
                ("Akurasi latih", Fmt.Num((double)benar / y.Length, 4))));

            var barisPohon = new List<string[]>();
            void TulisSimpul(Simpul s, string jalan, int id, ref int next)
            {
                string isi;
                if (s.Daun)
                    isi = $"daun → {kelas[s.Kelas]} (n = {s.N})";
                else if (s.Numerik)
                    isi = $"belah {prediktor[s.Prediktor]} ≤ {Fmt.Num(s.Ambang, 4)} "
                          + $"(galat-duga = {Fmt.Num(s.GalatDuga, 2)})";
                else
                    isi = $"belah {prediktor[s.Prediktor]} atas {s.Grup.Count} cabang "
                          + $"(galat-duga = {Fmt.Num(s.GalatDuga, 2)})";
                barisPohon.Add(new[] { Fmt.Int(id), jalan, isi });
                for (int g = 0; g < s.Anak.Count; g++)
                {
                    next++;
                    string labelG;
                    if (s.Numerik)
                        labelG = g == 0 ? $"≤ {Fmt.Num(s.Ambang, 4)}" : $"> {Fmt.Num(s.Ambang, 4)}";
                    else
                        labelG = string.Join("+", s.Grup[g].Select(v => namaKat[s.Prediktor][v]));
                    TulisSimpul(s.Anak[g], labelG, next, ref next);
                }
            }
            int nn = 1;
            TulisSimpul(h.Akar, "akar", 1, ref nn);
            blocks.Add(Blocks.Table(
                "Pohon C4.5 (sesudah pangkas)",
                new[] { "Simpul", "Jalan", "Isi" },
                barisPohon.ToArray(),
                "Galat-duga = N × batas-atas binomial (CF) — yang dibandingkan "
                + "saat memutuskan memangkas."));
            blocks.Add(Blocks.Table(
                "Matriks kebingungan (latih)",
                new[] { "Asli \\ Duga" }.Concat(kelas).ToArray(),
                kelas.Select((kl, i) => new[] { kl }.Concat(
                    Enumerable.Range(0, kelas.Count).Select(j =>
                        Fmt.Int(bingung[i, j]))).ToArray()).ToArray(),
                "Diagonal = benar."));
            return blocks;
        }
    }
}
