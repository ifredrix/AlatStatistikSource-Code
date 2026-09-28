using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    // CHAID (Kass 1980): pohon klasifikasi untuk target kategori dengan
    // belahan multi-arah. Tiap prediktor kategorik digabung dulu kategori
    // yang tak-berbeda nyata (khi-kuadrat): nominal boleh gabung pasangan
    public static class Chaid
    {
        public enum Jenis { Nominal, Ordinal }

        public sealed class Grup
        {
            public List<int> Anggota = new();
            public string Nama = "";
        }

        public sealed class Simpul
        {
            public bool Daun;
            public int Kelas;
            public int[] Cacah = Array.Empty<int>();
            public int N;
            public int Prediktor = -1;
            public List<Grup> GrupAnak = new();
            public List<Simpul> Anak = new();
            public double Khi2, P, PBonf;
        }

        public sealed class Hasil
        {
            public List<string> Kelas = new();
            public List<string> Prediktor = new();
            public Simpul Akar = new();
            public int Kedalaman;
            public int Simpul;
            public int[] Label = Array.Empty<int>();
            public double AlphaGabung, AlphaBelah;
            public int MinAnak, MaksDalam;
        }

        public static long Kombinasi(int n, int k)
        {
            if (k < 0 || k > n) return 0;
            if (k > n - k) k = n - k;
            long h = 1;
            for (int i = 0; i < k; i++)
            {
                h = h * (n - i) / (i + 1);
            }
            return h;
        }

        // p khi-kuadrat Pearson untuk tabel cacah.
        public static double PKhi2(int[,] tab)
        {
            int r = tab.GetLength(0), c = tab.GetLength(1);
            double n = 0;
            var rb = new double[r];
            var cb = new double[c];
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                {
                    n += tab[i, j];
                    rb[i] += tab[i, j];
                    cb[j] += tab[i, j];
                }
            if (n <= 0) return double.NaN;
            double chi = 0;
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                {
                    double harap = rb[i] * cb[j] / n;
                    if (harap <= 0) return double.NaN;
                    double d = tab[i, j] - harap;
                    chi += d * d / harap;
                }
            return Distributions.ChiSquareUpper(chi, (r - 1) * (c - 1));
        }

        private sealed class Cari
        {
            public int[][] X = Array.Empty<int[]>();
            public int[] Y = Array.Empty<int>();
            public int NKelas, NPred;
            public Jenis[] Macam = Array.Empty<Jenis>();
            public double AlphaGabung, AlphaBelah;
            public int MinAnak, MaksDalam;
            public int Kedalaman, CacahSimpul;
        }

        // Gabungkan kategori sampai semua pasangan berbeda nyata.
        // Mengembalikan grup final + p mentah tabel final.
        private static (List<List<int>> Grup, double P) Gabung(
            int[] xk, int[] y, int nKelas, Jenis macam, double alpha)
        {
            var grup = xk.Distinct().OrderBy(v => v)
                           .Select(v => new List<int> { v }).ToList();
            if (grup.Count <= 1) return (grup, double.NaN);
            while (grup.Count > 2)
            {
                double pMaks = -1;
                int iMaks = -1, jMaks = -1;
                for (int i = 0; i < grup.Count; i++)
                    for (int j = i + 1; j < grup.Count; j++)
                    {
                        if (macam == Jenis.Ordinal && j != i + 1) continue;
                        var tab = TabelDua(grup[i], grup[j], xk, y, nKelas);
                        double p = PKhi2(tab);
                        if (double.IsNaN(p)) continue;
                        // Seri dimenangi yang PERTAMA (konvensi deterministik).
                        if (p > pMaks) { pMaks = p; iMaks = i; jMaks = j; }
                    }
                if (iMaks < 0 || pMaks <= alpha) break;
                grup[iMaks].AddRange(grup[jMaks]);
                grup[iMaks].Sort();
                grup.RemoveAt(jMaks);
            }
            var akhir = TabelSemua(grup, xk, y, nKelas);
            return (grup, PKhi2(akhir));
        }

        private static int[,] TabelDua(List<int> a, List<int> b, int[] xk,
                                       int[] y, int nKelas)
        {
            var tab = new int[2, nKelas];
            for (int i = 0; i < xk.Length; i++)
            {
                if (a.Contains(xk[i])) tab[0, y[i]]++;
                else if (b.Contains(xk[i])) tab[1, y[i]]++;
            }
            return tab;
        }

        private static int[,] TabelSemua(List<List<int>> grup, int[] xk,
                                         int[] y, int nKelas)
        {
            var tab = new int[grup.Count, nKelas];
            var milik = new Dictionary<int, int>();
            for (int g = 0; g < grup.Count; g++)
                foreach (int v in grup[g]) milik[v] = g;
            for (int i = 0; i < xk.Length; i++)
                tab[milik[xk[i]], y[i]]++;
            return tab;
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
            if (dalam >= b.MaksDalam || idx.Length < 2 * b.MinAnak)
            {
                node.Daun = true;
                return node;
            }

            int fTerbaik = -1;
            List<List<int>> grupTerbaik = new();
            double pTerbaik = double.PositiveInfinity, chiTerbaik = 0;
            // Kelas yang hadir di simpul ini saja (kolom kosong merusak
            // uji khi-kuadrat: harapan nol + df membesar).
            var yk = idx.Select(i => b.Y[i]).ToArray();
            var hadir = yk.Distinct().OrderBy(v => v).ToList();
            if (hadir.Count < 2) { node.Daun = true; return node; }
            var petaKelas = hadir.Select((v, i) => (v, i))
                                 .ToDictionary(t => t.v, t => t.i);
            var ykKompak = yk.Select(v => petaKelas[v]).ToArray();
            int nHadir = hadir.Count;
            for (int f = 0; f < b.NPred; f++)
            {
                var xk = idx.Select(i => b.X[f][i]).ToArray();
                // yk/ykKompak sudah sejajar posisi-dengan-idx (bukan ID baris).
                var ykNode = ykKompak;
                int c = xk.Distinct().Count();
                if (c < 2) continue;
                var (grup, pMentah) = Gabung(xk, ykNode, nHadir, b.Macam[f], b.AlphaGabung);
                if (grup.Count < 2 || double.IsNaN(pMentah)) continue;
                // Pengali Bonferroni = C(c−1, r−1).
                double pengali = Kombinasi(c - 1, grup.Count - 1);
                double pAdj = Math.Min(1, pMentah * Math.Max(1, pengali));
                // Seri dimenangi yang PERTAMA.
                if (pAdj < pTerbaik)
                {
                    pTerbaik = pAdj;
                    fTerbaik = f;
                    grupTerbaik = grup;
                    var tab = TabelSemua(grup, xk, ykNode, nHadir);
                    double n = 0, chi = 0;
                    var rb = new double[tab.GetLength(0)];
                    var cb = new double[nHadir];
                    for (int i = 0; i < tab.GetLength(0); i++)
                        for (int j = 0; j < nHadir; j++)
                        {
                            n += tab[i, j];
                            rb[i] += tab[i, j];
                            cb[j] += tab[i, j];
                        }
                    for (int i = 0; i < tab.GetLength(0); i++)
                        for (int j = 0; j < nHadir; j++)
                        {
                            double harap = rb[i] * cb[j] / n;
                            double d = tab[i, j] - harap;
                            chi += d * d / harap;
                        }
                    chiTerbaik = chi;
                }
            }
            if (fTerbaik < 0 || pTerbaik >= b.AlphaBelah)
            {
                node.Daun = true;
                return node;
            }
            // Anak terkecil harus memenuhi minimum.
            var milik = new Dictionary<int, int>();
            for (int g = 0; g < grupTerbaik.Count; g++)
                foreach (int v in grupTerbaik[g]) milik[v] = g;
            var anakIdx = new List<int>[grupTerbaik.Count];
            for (int g = 0; g < grupTerbaik.Count; g++) anakIdx[g] = new List<int>();
            foreach (int i in idx) anakIdx[milik[b.X[fTerbaik][i]]].Add(i);
            if (anakIdx.Any(a => a.Count < b.MinAnak))
            {
                node.Daun = true;
                return node;
            }
            node.Prediktor = fTerbaik;
            node.Khi2 = chiTerbaik;
            node.P = pTerbaik;
            node.PBonf = pTerbaik;
            for (int g = 0; g < grupTerbaik.Count; g++)
            {
                node.GrupAnak.Add(new Grup { Anggota = grupTerbaik[g] });
                node.Anak.Add(Tumbuh(b, anakIdx[g].ToArray(), dalam + 1));
            }
            return node;
        }

        public static int Duga(Simpul node, int[] x)
        {
            while (!node.Daun)
            {
                int g = -1;
                for (int k = 0; k < node.GrupAnak.Count; k++)
                    if (node.GrupAnak[k].Anggota.Contains(x[node.Prediktor])) { g = k; break; }
                if (g < 0 || g >= node.Anak.Count) break;
                node = node.Anak[g];
            }
            return node.Kelas;
        }

        public static Hasil? Pasang(int[][] xp, string[] namaPred, Jenis[] macam,
            int[] y, List<string> kelas,
            double alphaGabung = 0.05, double alphaBelah = 0.05,
            int minAnak = 10, int maksDalam = 4)
        {
            int n = y.Length;
            if (n < 4 || xp.Any(v => v.Length != n)) return null;
            if (kelas.Count < 2) return null;
            var b = new Cari
            {
                X = xp, Y = y, NKelas = kelas.Count, NPred = xp.Length,
                Macam = macam, AlphaGabung = alphaGabung, AlphaBelah = alphaBelah,
                MinAnak = Math.Max(1, minAnak), MaksDalam = Math.Max(1, maksDalam),
            };
            var akar = Tumbuh(b, Enumerable.Range(0, n).ToArray(), 0);
            var label = new int[n];
            var semuaX = new int[n][];
            for (int i = 0; i < n; i++)
            {
                semuaX[i] = new int[xp.Length];
                for (int f = 0; f < xp.Length; f++) semuaX[i][f] = xp[f][i];
                label[i] = Duga(akar, semuaX[i]);
            }
            return new Hasil
            {
                Kelas = new List<string>(kelas),
                Prediktor = new List<string>(namaPred),
                Akar = akar, Kedalaman = b.Kedalaman, Simpul = b.CacahSimpul,
                Label = label, AlphaGabung = alphaGabung, AlphaBelah = alphaBelah,
                MinAnak = b.MinAnak, MaksDalam = b.MaksDalam,
            };
        }

        // Binning kuantil untuk prediktor numerik (ordinal treatment).
        public static int[] Binning(double[] v, int maksBin = 10)
        {
            int n = v.Length;
            var urut = v.Select((t, i) => (t, i)).OrderBy(t => t.t).ToArray();
            var kode = new int[n];
            int k = Math.Min(maksBin, n);
            for (int r = 0; r < n; r++)
            {
                int bin = (int)((long)r * k / n);
                kode[urut[r].i] = Math.Min(bin, k - 1);
            }
            // Padatkan label bin yang kosong.
            var ada = kode.Distinct().OrderBy(t => t).ToList();
            var peta = ada.Select((t, i) => (t, i)).ToDictionary(t => t.t, t => t.i);
            return kode.Select(t => peta[t]).ToArray();
        }

        public static List<ResultBlock> ChaidBlocks(Dataset ds, List<string> prediktor,
            string target, double alphaGabung, double alphaBelah,
            int minAnak, int maksDalam)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("CHAID (pohon khi-kuadrat multi-arah)", 1)
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

            var xp = new List<int[]>();
            var macam = new List<Jenis>();
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
                    var bin = Binning(nil);
                    xp.Add(bin);
                    macam.Add(Jenis.Ordinal);
                    namaKat.Add(bin.Distinct().OrderBy(t => t)
                                   .Select(t => $"bin {t + 1}").ToList());
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
                    xp.Add(teks.Select(t => peta[t]).ToArray());
                    macam.Add(Jenis.Nominal);
                    namaKat.Add(label);
                }
            }

            var h = Pasang(xp.ToArray(), prediktor.ToArray(), macam.ToArray(), y, kelas,
                           alphaGabung, alphaBelah, minAnak, maksDalam);
            if (h is null)
            {
                blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.",
                                       NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Chaid));

            var bingung = new int[kelas.Count, kelas.Count];
            for (int i = 0; i < y.Length; i++) bingung[y[i], h.Label[i]]++;
            int benar = 0;
            for (int c = 0; c < kelas.Count; c++) benar += bingung[c, c];
            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Prediktor", string.Join(", ", prediktor)),
                ("Target", $"{target} ({string.Join(", ", kelas)})"),
                ("Batas", $"gabung α {Fmt.Num(alphaGabung, 3)}, belah α {Fmt.Num(alphaBelah, 3)}, "
                          + $"anak ≥ {Fmt.Int(h.MinAnak)}, dalam ≤ {Fmt.Int(h.MaksDalam)}"),
                ("Simpul / kedalaman", $"{Fmt.Int(h.Simpul)} / {Fmt.Int(h.Kedalaman)}"),
                ("Akurasi latih", Fmt.Num((double)benar / y.Length, 4))));

            var barisPohon = new List<string[]>();
            void TulisSimpul(Simpul s, string jalan, int id, ref int next)
            {
                int saya = id;
                string isi = s.Daun
                    ? $"daun → {kelas[s.Kelas]} (n = {s.N})"
                    : $"belah {prediktor[s.Prediktor]} atas {s.GrupAnak.Count} grup "
                      + $"(χ² = {Fmt.Num(s.Khi2, 3)}, p-bonf = {Fmt.P(s.PBonf)})";
                barisPohon.Add(new[] { Fmt.Int(saya), jalan, isi });
                for (int g = 0; g < s.Anak.Count; g++)
                {
                    next++;
                    string labelG = string.Join("+", s.GrupAnak[g].Anggota
                        .Select(v => namaKat[s.Prediktor][v]));
                    TulisSimpul(s.Anak[g], labelG, next, ref next);
                }
            }
            int nn = 1;
            TulisSimpul(h.Akar, "akar", 1, ref nn);
            blocks.Add(Blocks.Table(
                "Pohon CHAID",
                new[] { "Simpul", "Jalan", "Isi" },
                barisPohon.ToArray(),
                "Grup ditulis sebagai gabungan kategori asal."));
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
