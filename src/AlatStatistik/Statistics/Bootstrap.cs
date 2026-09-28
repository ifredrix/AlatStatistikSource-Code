using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Bootstrap
    {
        public enum JenisStat { Rerata, Median, SimpanganBaku, SelisihRerata }

        public const int UlanganBawaan = 2000;
        public const ulong BenihBawaan = 20260924UL;

        

        public sealed class Acak
        {
            private ulong _keadaan;

            public Acak(ulong benih) { _keadaan = benih; }

            public ulong Berikut()
            {
                unchecked
                {
                    _keadaan += 0x9E3779B97F4A7C15UL;
                    ulong z = _keadaan;
                    z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                    z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                    return z ^ (z >> 31);
                }
            }

            public int Indeks(int n) => (int)(Berikut() % (ulong)n);
        }

        

        public sealed class Baris
        {
            public string Variabel = "";
            public string Statistik = "";
            public double Taksiran = double.NaN;
            public double GalatBaku = double.NaN;
            public double Bias = double.NaN;

            public int Kurang;

            public double Z0 = double.NaN;
            public double Percepatan = double.NaN;
            public double BawahPersentil = double.NaN;
            public double AtasPersentil = double.NaN;
            public double BawahBca = double.NaN;
            public double AtasBca = double.NaN;
            public double Bawah = double.NaN;
            public double Atas = double.NaN;
        }

        public sealed class Hasil
        {
            public int N, N0, N1, Ulangan;
            public double Keyakinan = 0.95;
            public bool PakaiBca = true;
            public bool AdaGrup;
            public ulong Benih;
            public string[] Nama = Array.Empty<string>();
            public List<Baris> Baris = new();

            public ulong IndeksHash;

            public int[] IndeksPertama = Array.Empty<int>();

            public double[] SebaranPertama = Array.Empty<double>();
        }

        

        public static double Median(double[] x)
        {
            int n = x.Length;
            if (n == 0) return double.NaN;
            var y = (double[])x.Clone();
            Array.Sort(y);
            return n % 2 == 1 ? y[n / 2] : 0.5 * (y[n / 2 - 1] + y[n / 2]);
        }

        public static double KuantilLinear(double[] terurut, double p)
        {
            int m = terurut.Length;
            if (m == 0) return double.NaN;
            if (m == 1) return terurut[0];
            if (p <= 0) return terurut[0];
            if (p >= 1) return terurut[m - 1];

            double pos = p * (m - 1);
            int lo = (int)Math.Floor(pos);
            int hi = (int)Math.Ceiling(pos);
            if (lo == hi) return terurut[lo];
            double f = pos - lo;
            return terurut[lo] * (1 - f) + terurut[hi] * f;
        }

        public static double Statistik(double[] y, int n0, JenisStat jenis)
        {
            int n = y.Length;
            if (n == 0) return double.NaN;

            switch (jenis)
            {
                case JenisStat.Rerata:
                {
                    double s = 0;
                    for (int i = 0; i < n; i++) s += y[i];
                    return s / n;
                }
                case JenisStat.Median:
                    return Median(y);
                case JenisStat.SimpanganBaku:
                {
                    if (n < 2) return double.NaN;
                    double m = 0;
                    for (int i = 0; i < n; i++) m += y[i];
                    m /= n;
                    double ss = 0;
                    for (int i = 0; i < n; i++) { double d = y[i] - m; ss += d * d; }
                    return Math.Sqrt(ss / (n - 1));
                }
                case JenisStat.SelisihRerata:
                {
                    int n1 = n - n0;
                    if (n0 < 1 || n1 < 1) return double.NaN;
                    double s0 = 0;
                    for (int i = 0; i < n0; i++) s0 += y[i];
                    double s1 = 0;
                    for (int i = n0; i < n; i++) s1 += y[i];
                    return s0 / n0 - s1 / n1;
                }
            }
            return double.NaN;
        }

        public static double Pearson(double[] x, double[] y)
        {
            int n = x.Length;
            double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
            for (int i = 0; i < n; i++)
            {
                sx += x[i]; sy += y[i];
                sxx += x[i] * x[i]; syy += y[i] * y[i]; sxy += x[i] * y[i];
            }
            double atas = n * sxy - sx * sy;
            double bawah = (n * sxx - sx * sx) * (n * syy - sy * sy);
            return bawah > 0 ? atas / Math.Sqrt(bawah) : double.NaN;
        }

        public static (double Slope, double Intercept) Ols(double[] x, double[] y)
        {
            int n = x.Length;
            double sx = 0, sy = 0, sxx = 0, sxy = 0;
            for (int i = 0; i < n; i++)
            {
                sx += x[i]; sy += y[i]; sxx += x[i] * x[i]; sxy += x[i] * y[i];
            }
            double den = n * sxx - sx * sx;
            if (!(den > 0)) return (double.NaN, double.NaN);
            double b = (n * sxy - sx * sy) / den;
            return (b, sy / n - b * sx / n);
        }

        private static double StatPasangan(double[] x, double[] y, int jenis)
        {
            // jenis: 0 = korelasi, 1 = kemiringan, 2 = intersep.
            if (jenis == 0) return Pearson(x, y);
            var (b, a) = Ols(x, y);
            return jenis == 1 ? b : a;
        }

        public static Hasil HitungPasangan(double[] x, double[] y,
                                           int ulangan, ulong benih,
                                           double keyakinan, bool pakaiBca)
        {
            int n = x.Length;
            if (y.Length != n)
                throw new ArgumentException("Pasangan x, y panjangnya beda.");
            if (n < 3)
                throw new ArgumentException("Bootstrap butuh sedikitnya 3 baris lengkap.");
            if (ulangan < 50)
                throw new ArgumentException("Banyak ulangan minimal 50.");
            if (keyakinan <= 0 || keyakinan >= 1)
                throw new ArgumentException("Tingkat keyakinan harus antara 0 dan 1.");

            var acak = new Acak(benih);
            var indeks = new int[ulangan][];
            ulong hash = FnvOffset;
            unchecked
            {
                for (int b = 0; b < ulangan; b++)
                {
                    var idx = new int[n];
                    for (int i = 0; i < n; i++) idx[i] = acak.Indeks(n);
                    indeks[b] = idx;
                    for (int i = 0; i < n; i++)
                    {
                        hash ^= (ulong)idx[i];
                        hash *= FnvPrima;
                    }
                }
            }

            var h = new Hasil
            {
                N = n, Ulangan = ulangan, Keyakinan = keyakinan,
                PakaiBca = pakaiBca, AdaGrup = false, Benih = benih,
                IndeksHash = hash, IndeksPertama = indeks[0],
            };

            double alfa = (1 - keyakinan) / 2;
            double zAlfaBawah = Distributions.NormalInv(alfa);
            double zAlfaAtas = Distributions.NormalInv(1 - alfa);
            string[] label = { "Korelasi Pearson", "Kemiringan OLS", "Intersep OLS" };

            for (int jenis = 0; jenis < 3; jenis++)
            {
                double taksiran = StatPasangan(x, y, jenis);
                var rep = new double[ulangan];
                var px = new double[n];
                var py = new double[n];
                for (int b = 0; b < ulangan; b++)
                {
                    var idx = indeks[b];
                    for (int i = 0; i < n; i++) { px[i] = x[idx[i]]; py[i] = y[idx[i]]; }
                    rep[b] = StatPasangan(px, py, jenis);
                }
                double rataRep = 0;
                for (int b = 0; b < ulangan; b++) rataRep += rep[b];
                rataRep /= ulangan;

                var baris = new Baris
                {
                    Variabel = label[jenis],
                    Statistik = label[jenis],
                    Taksiran = taksiran,
                    GalatBaku = Sd(rep),
                    Bias = rataRep - taksiran,
                };

                var terurut = (double[])rep.Clone();
                Array.Sort(terurut);
                baris.BawahPersentil = KuantilLinear(terurut, alfa);
                baris.AtasPersentil = KuantilLinear(terurut, 1 - alfa);

                int kurang = 0;
                for (int b = 0; b < ulangan; b++)
                    if (rep[b] < taksiran) kurang++;
                double proporsi = (double)kurang / ulangan;
                double z0 = Distributions.NormalInv(Math.Clamp(proporsi, 1e-9, 1 - 1e-9));
                baris.Kurang = kurang;
                baris.Z0 = z0;

                var jk = new double[n];
                var jx = new double[n - 1];
                var jy = new double[n - 1];
                for (int i = 0; i < n; i++)
                {
                    int t = 0;
                    for (int j = 0; j < n; j++)
                        if (j != i) { jx[t] = x[j]; jy[t] = y[j]; t++; }
                    jk[i] = StatPasangan(jx, jy, jenis);
                }
                double jkRata = 0;
                for (int i = 0; i < n; i++) jkRata += jk[i];
                jkRata /= n;
                double s2 = 0, s3 = 0;
                for (int i = 0; i < n; i++)
                {
                    double u = jkRata - jk[i];
                    s2 += u * u;
                    s3 += u * u * u;
                }
                double a = s2 > 0 ? s3 / (6.0 * Math.Pow(s2, 1.5)) : 0.0;
                baris.Percepatan = a;

                double z1 = z0 + (z0 + zAlfaBawah) / (1 - a * (z0 + zAlfaBawah));
                double z2 = z0 + (z0 + zAlfaAtas) / (1 - a * (z0 + zAlfaAtas));
                baris.BawahBca = KuantilLinear(terurut, Distributions.NormalCdf(z1));
                baris.AtasBca = KuantilLinear(terurut, Distributions.NormalCdf(z2));

                baris.Bawah = pakaiBca ? baris.BawahBca : baris.BawahPersentil;
                baris.Atas = pakaiBca ? baris.AtasBca : baris.AtasPersentil;

                h.Baris.Add(baris);
                if (h.SebaranPertama.Length == 0) h.SebaranPertama = rep;
            }
            return h;
        }

        public sealed class HasilPermutasi
        {
            public int N, N0, N1, Ulangan;
            public ulong Benih;
            public double T0;
            public long Hitung;
            public double P;
            public double Se;
        }

        public static HasilPermutasi HitungPermutasi(double[] y, int[] grup,
                                                     int ulangan, ulong benih)
        {
            int n = y.Length;
            if (grup.Length != n)
                throw new ArgumentException("Grup dan data panjangnya beda.");
            if (n < 4)
                throw new ArgumentException("Permutasi butuh sedikitnya 4 baris.");
            if (ulangan < 50)
                throw new ArgumentException("Banyak ulangan minimal 50.");
            var id0 = new List<int>();
            var id1 = new List<int>();
            for (int i = 0; i < n; i++)
                (grup[i] == 0 ? id0 : id1).Add(i);
            int n0 = id0.Count, n1 = id1.Count;
            if (n0 < 2 || n1 < 2)
                throw new ArgumentException("Tiap grup butuh sedikitnya 2 baris.");

            double s0 = 0;
            foreach (int i in id0) s0 += y[i];
            double s1 = 0;
            foreach (int i in id1) s1 += y[i];
            double t0 = s0 / n0 - s1 / n1;

            var acak = new Acak(benih);
            var idx = new int[n];
            long hitung = 0;
            for (int b = 0; b < ulangan; b++)
            {
                for (int i = 0; i < n; i++) idx[i] = i;
                for (int i = n - 1; i >= 1; i--)
                {
                    int j = acak.Indeks(i + 1);
                    (idx[i], idx[j]) = (idx[j], idx[i]);
                }
                double r0 = 0;
                for (int i = 0; i < n0; i++) r0 += y[idx[i]];
                double r1 = 0;
                for (int i = n0; i < n; i++) r1 += y[idx[i]];
                double t = r0 / n0 - r1 / n1;
                if (Math.Abs(t) >= Math.Abs(t0)) hitung++;
            }
            double p = (double)(hitung + 1) / (ulangan + 1);
            return new HasilPermutasi
            {
                N = n, N0 = n0, N1 = n1, Ulangan = ulangan, Benih = benih,
                T0 = t0, Hitung = hitung, P = p,
                Se = Math.Sqrt(p * (1 - p) / ulangan),
            };
        }

        private static double Sd(double[] x)
        {
            int n = x.Length;
            if (n < 2) return double.NaN;
            double m = 0;
            for (int i = 0; i < n; i++) m += x[i];
            m /= n;
            double ss = 0;
            for (int i = 0; i < n; i++) { double d = x[i] - m; ss += d * d; }
            return Math.Sqrt(ss / (n - 1));
        }

        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrima = 1099511628211UL;

        public static Hasil Hitung(double[][] kolom, string[] nama, int[]? grup,
                                   int ulangan, ulong benih, double keyakinan, bool pakaiBca)
        {
            if (kolom.Length == 0 || kolom[0].Length == 0)
                throw new ArgumentException("Tidak ada kolom untuk di-bootstrap.");
            if (ulangan < 50)
                throw new ArgumentException("Banyak ulangan minimal 50.");
            if (keyakinan <= 0 || keyakinan >= 1)
                throw new ArgumentException("Tingkat keyakinan harus antara 0 dan 1.");

            int n = kolom[0].Length;
            int k = kolom.Length;
            if (n < 3)
                throw new ArgumentException("Bootstrap butuh sedikitnya 3 baris lengkap.");

            
            double[][] kol;
            int n0 = 0, n1 = 0;
            bool adaGrup = grup is not null;

            if (adaGrup)
            {
                var baris0 = new List<int>();
                var baris1 = new List<int>();
                for (int i = 0; i < n; i++)
                    (grup![i] == 0 ? baris0 : baris1).Add(i);
                n0 = baris0.Count;
                n1 = baris1.Count;
                if (n0 < 2 || n1 < 2)
                    throw new ArgumentException(
                        "Tiap grup butuh sedikitnya 2 baris lengkap untuk bootstrap selisih.");

                var urut = baris0.Concat(baris1).ToArray();
                kol = new double[k][];
                for (int v = 0; v < k; v++)
                {
                    var c = new double[n];
                    for (int i = 0; i < n; i++) c[i] = kolom[v][urut[i]];
                    kol[v] = c;
                }
            }
            else
            {
                kol = kolom;
            }

            var acak = new Acak(benih);
            var indeks = new int[ulangan][];
            ulong hash = FnvOffset;
            unchecked
            {
                for (int b = 0; b < ulangan; b++)
                {
                    var idx = new int[n];
                    if (adaGrup)
                    {
                        for (int i = 0; i < n0; i++) idx[i] = acak.Indeks(n0);
                        for (int i = 0; i < n1; i++) idx[n0 + i] = n0 + acak.Indeks(n1);
                    }
                    else
                    {
                        for (int i = 0; i < n; i++) idx[i] = acak.Indeks(n);
                    }
                    indeks[b] = idx;
                    for (int i = 0; i < n; i++)
                    {
                        hash ^= (ulong)idx[i];
                        hash *= FnvPrima;
                    }
                }
            }

            var h = new Hasil
            {
                N = n, N0 = n0, N1 = n1, Ulangan = ulangan,
                Keyakinan = keyakinan, PakaiBca = pakaiBca, AdaGrup = adaGrup,
                Benih = benih, Nama = nama, IndeksHash = hash,
                IndeksPertama = indeks[0],
            };

            double alfa = (1 - keyakinan) / 2;
            double zAlfaBawah = Distributions.NormalInv(alfa);
            double zAlfaAtas = Distributions.NormalInv(1 - alfa);

            for (int v = 0; v < k; v++)
            {
                var jenisnya = adaGrup
                    ? new[] { JenisStat.SelisihRerata }
                    : new[] { JenisStat.Rerata, JenisStat.Median, JenisStat.SimpanganBaku };

                foreach (var jenis in jenisnya)
                {
                    double taksiran = Statistik(kol[v], n0, jenis);

                    
                    var rep = new double[ulangan];
                    var y = new double[n];
                    for (int b = 0; b < ulangan; b++)
                    {
                        var idx = indeks[b];
                        for (int i = 0; i < n; i++) y[i] = kol[v][idx[i]];
                        rep[b] = Statistik(y, n0, jenis);
                    }

                    double rataRep = 0;
                    for (int b = 0; b < ulangan; b++) rataRep += rep[b];
                    rataRep /= ulangan;

                    var baris = new Baris
                    {
                        Variabel = nama[v],
                        Statistik = LabelStatistik(jenis, adaGrup),
                        Taksiran = taksiran,
                        GalatBaku = Sd(rep),
                        Bias = rataRep - taksiran,
                    };

                    var terurut = (double[])rep.Clone();
                    Array.Sort(terurut);
                    baris.BawahPersentil = KuantilLinear(terurut, alfa);
                    baris.AtasPersentil = KuantilLinear(terurut, 1 - alfa);

                    
                    int kurang = 0;
                    for (int b = 0; b < ulangan; b++)
                        if (rep[b] < taksiran) kurang++;
                    double proporsi = (double)kurang / ulangan;
                    
                    
                    double z0 = Distributions.NormalInv(Math.Clamp(proporsi, 1e-9, 1 - 1e-9));
                    baris.Kurang = kurang;
                    baris.Z0 = z0;

                    
                    var jk = new double[n];
                    var yj = new double[n - 1];
                    for (int i = 0; i < n; i++)
                    {
                        int t = 0;
                        for (int j = 0; j < n; j++) if (j != i) yj[t++] = kol[v][j];
                        int n0j = (adaGrup && i < n0) ? n0 - 1 : n0;
                        jk[i] = Statistik(yj, n0j, jenis);
                    }
                    double jkRata = 0;
                    for (int i = 0; i < n; i++) jkRata += jk[i];
                    jkRata /= n;

                    double s2 = 0, s3 = 0;
                    for (int i = 0; i < n; i++)
                    {
                        double u = jkRata - jk[i];
                        s2 += u * u;
                        s3 += u * u * u;
                    }
                    double a = s2 > 0 ? s3 / (6.0 * Math.Pow(s2, 1.5)) : 0.0;
                    baris.Percepatan = a;

                    double z1 = z0 + (z0 + zAlfaBawah) / (1 - a * (z0 + zAlfaBawah));
                    double z2 = z0 + (z0 + zAlfaAtas) / (1 - a * (z0 + zAlfaAtas));
                    baris.BawahBca = KuantilLinear(terurut, Distributions.NormalCdf(z1));
                    baris.AtasBca = KuantilLinear(terurut, Distributions.NormalCdf(z2));

                    baris.Bawah = pakaiBca ? baris.BawahBca : baris.BawahPersentil;
                    baris.Atas = pakaiBca ? baris.AtasBca : baris.AtasPersentil;

                    h.Baris.Add(baris);
                    if (h.SebaranPertama.Length == 0) h.SebaranPertama = rep;
                }
            }

            return h;
        }

        public static string LabelStatistik(JenisStat jenis, bool adaGrup) => jenis switch
        {
            JenisStat.Rerata => "Rerata",
            JenisStat.Median => "Median",
            JenisStat.SimpanganBaku => "Simpangan baku",
            JenisStat.SelisihRerata => adaGrup ? "Selisih rerata (grup 1 − grup 2)" : "Selisih rerata",
            _ => jenis.ToString(),
        };

        

        private static int[]? GrupKode(Dataset ds, List<int> barisLengkap,
                                         string grupVar, List<ResultBlock> blocks,
                                         out List<string> labelGrup)
        {
            labelGrup = new List<string>();
            var tingkat = ds.Levels(grupVar);
            if (tingkat.Count < 2)
            {
                blocks.Add(Blocks.Note(
                    $"Variabel grup '{grupVar}' hanya punya {tingkat.Count} tingkat yang terisi; "
                    + "butuh dua. Bootstrap selisih tidak dijalankan.", NoteKind.Error));
                return null;
            }
            if (tingkat.Count > 2)
                blocks.Add(Blocks.Note(
                    $"Variabel grup '{grupVar}' punya {tingkat.Count} tingkat. "
                    + $"Yang dipakai hanya dua yang muncul lebih dulu: "
                    + $"'{tingkat[0]}' dan '{tingkat[1]}'.", NoteKind.Warning));

            var teks = ds.Text(grupVar);
            var grup = new int[barisLengkap.Count];
            for (int i = 0; i < barisLengkap.Count; i++)
                grup[i] = string.Equals(teks[barisLengkap[i]], tingkat[1],
                                        StringComparison.Ordinal) ? 1 : 0;
            labelGrup.Add(tingkat[0]);
            labelGrup.Add(tingkat[1]);
            return grup;
        }

        private static List<ResultBlock> PasanganBlocks(Dataset ds, List<ResultBlock> blocks,
            List<string> dipakai, int ulangan, ulong benih, double keyakinan,
            bool pakaiBca, string mode)
        {
            if (dipakai.Count < 2)
            {
                blocks.Add(Blocks.Note("Korelasi/kemiringan butuh dua variabel numerik.",
                                       NoteKind.Error));
                return blocks;
            }
            string nx = dipakai[0], ny = dipakai[1];
            var barisLengkap = ds.CompleteRows(new[] { nx, ny });
            if (barisLengkap.Count < 3)
            {
                blocks.Add(Blocks.Note("Baris lengkap kurang dari 3.",
                                       NoteKind.Error));
                return blocks;
            }
            var sx = ds.Numeric(nx);
            var sy = ds.Numeric(ny);
            int n = barisLengkap.Count;
            var x = new double[n];
            var y = new double[n];
            for (int i = 0; i < n; i++)
            {
                x[i] = sx[barisLengkap[i]] ?? double.NaN;
                y[i] = sy[barisLengkap[i]] ?? double.NaN;
            }

            Hasil h;
            try
            {
                h = HitungPasangan(x, y, ulangan, benih, keyakinan, pakaiBca);
            }
            catch (Exception ex)
            {
                blocks.Add(Blocks.Note(ex.Message, NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.BootstrapResample, DaftarRumus.BootstrapPersentil,
                DaftarRumus.BootstrapBca, DaftarRumus.Pearson,
                DaftarRumus.RegresiLinear));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Pasangan", $"{nx} lawan {ny} (resample pasangan, bukan kolom)"),
                ("Banyak baris lengkap", $"n = {Fmt.Int(h.N)}"),
                ("Banyak ulangan", $"B = {Fmt.Int(h.Ulangan)}"),
                ("Benih acak", $"{h.Benih} (tetap — hasilnya bisa diulang)"),
                ("Tingkat keyakinan", $"{Fmt.Num(h.Keyakinan * 100, 1)}%"),
                ("Metode selang", pakaiBca ? "BCa (bias-corrected & accelerated)" : "Persentil"),
                ("Sidik indeks resample", $"{h.IndeksHash}")));

            bool kor = mode.StartsWith("Korelasi", StringComparison.OrdinalIgnoreCase);
            var tampil = h.Baris.Where(b => kor
                ? b.Statistik.StartsWith("Korelasi", StringComparison.Ordinal)
                : !b.Statistik.StartsWith("Korelasi", StringComparison.Ordinal)).ToList();
            blocks.Add(Blocks.Table(
                kor ? "Selang kepercayaan korelasi Pearson"
                    : "Selang kepercayaan kemiringan & intersep OLS",
                new[] { "Statistik", "Taksiran", "Galat baku", "Bawah", "Atas" },
                tampil.Select(b => new[]
                {
                    b.Statistik, Fmt.Num(b.Taksiran, 4), Fmt.Num(b.GalatBaku, 4),
                    Fmt.Num(b.Bawah, 4), Fmt.Num(b.Atas, 4),
                }).ToArray(),
                kor ? "Resample pasangan (x, y) bersama-sama; BCa lewat jackknife "
                      + "pasangan-hilang-satu."
                    : "Kemiringan var-1 atas var-2 (OLS); tiap ulangan menghitung "
                      + "keduanya dari pasangan yang sama."));
            return blocks;
        }

        private static List<ResultBlock> PermutasiBlocks(Dataset ds, List<ResultBlock> blocks,
            string var, string grupVar, int ulangan, ulong benih)
        {
            if (string.IsNullOrWhiteSpace(grupVar))
            {
                blocks.Add(Blocks.Note("Uji permutasi butuh variabel grup dua tingkat.",
                                       NoteKind.Error));
                return blocks;
            }
            var barisLengkap = ds.CompleteRows(new[] { var, grupVar });
            if (barisLengkap.Count < 4)
            {
                blocks.Add(Blocks.Note("Baris lengkap kurang dari 4.",
                                       NoteKind.Error));
                return blocks;
            }
            var semua = ds.Numeric(var);
            int n = barisLengkap.Count;
            var y = new double[n];
            for (int i = 0; i < n; i++) y[i] = semua[barisLengkap[i]] ?? double.NaN;

            var labelGrup = new List<string>();
            var grup = GrupKode(ds, barisLengkap, grupVar, blocks, out labelGrup);
            if (grup is null) return blocks;

            HasilPermutasi h;
            try
            {
                h = HitungPermutasi(y, grup, ulangan, benih);
            }
            catch (Exception ex)
            {
                blocks.Add(Blocks.Note(ex.Message, NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Permutasi));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Variabel", var),
                ("Grup pembanding",
                 $"'{labelGrup[0]}' (n₀ = {Fmt.Int(h.N0)}) dan '{labelGrup[1]}' (n₁ = {Fmt.Int(h.N1)})"),
                ("Statistik", $"T = rerata('{labelGrup[0]}') − rerata('{labelGrup[1]}') = {Fmt.Num(h.T0, 4)}"),
                ("Banyak ulangan", $"B = {Fmt.Int(h.Ulangan)}"),
                ("Benih acak", $"{h.Benih} (tetap — hasilnya bisa diulang)")));

            blocks.Add(Blocks.Table(
                "Uji permutasi selisih rerata (dua sisi)",
                new[] { "Ukuran", "Nilai" },
                new[]
                {
                    new[] { "p permutasi", Fmt.P(h.P) },
                    new[] { "Galat baku MC", Fmt.Num(h.Se, 4) },
                    new[] { "Keputusan (α = 0,05)", Compare.Keputusan(h.P, 0.05) },
                },
                $"p = (c+1)/(B+1) dengan c = {Fmt.Int((int)h.Hitung)} permutasi "
                + "yang |T*| ≥ |T₀| — tak pernah nol (Phipson–Smyth)."));
            return blocks;
        }

        public static List<ResultBlock> BootstrapBlocks(Dataset ds, List<string> vars, string grupVar,
                                                        int ulangan, ulong benih, double keyakinan,
                                                        bool pakaiBca, string statistik = "Otomatis")
        {
            var blocks = new List<ResultBlock>();
            blocks.Add(Blocks.Heading("Bootstrap — selang kepercayaan tanpa asumsi sebaran", 1));

            var dipakai = vars.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList();
            if (dipakai.Count == 0)
            {
                blocks.Add(Blocks.Note("Pilih sedikitnya satu variabel numerik.", NoteKind.Error));
                return blocks;
            }

            if (ulangan < 50) ulangan = UlanganBawaan;
            if (keyakinan <= 0 || keyakinan >= 1) keyakinan = 0.95;

            string mode = (statistik ?? "").Trim();
            if (mode.StartsWith("Korelasi", StringComparison.OrdinalIgnoreCase)
                || mode.StartsWith("Kemiringan", StringComparison.OrdinalIgnoreCase))
                return PasanganBlocks(ds, blocks, dipakai, ulangan, benih,
                                      keyakinan, pakaiBca, mode);
            if (mode.StartsWith("Permutasi", StringComparison.OrdinalIgnoreCase)
                || mode.StartsWith("Uji permutasi", StringComparison.OrdinalIgnoreCase))
                return PermutasiBlocks(ds, blocks, dipakai[0], grupVar,
                                       ulangan, benih);

            
            var perlu = new List<string>(dipakai);
            bool adaGrupDiminta = !string.IsNullOrWhiteSpace(grupVar);
            if (adaGrupDiminta) perlu.Add(grupVar);

            var barisLengkap = ds.CompleteRows(perlu);
            if (barisLengkap.Count < 3)
            {
                blocks.Add(Blocks.Note(
                    "Baris lengkap (tanpa nilai kosong) untuk variabel terpilih kurang dari 3. "
                    + "Bootstrap tidak bisa dijalankan.", NoteKind.Error));
                return blocks;
            }

            var kolom = new List<double[]>();
            var namaKolom = new List<string>();
            foreach (var v in dipakai)
            {
                var semua = ds.Numeric(v);
                var c = new double[barisLengkap.Count];
                for (int i = 0; i < barisLengkap.Count; i++)
                    c[i] = semua[barisLengkap[i]] ?? double.NaN;
                kolom.Add(c);
                namaKolom.Add(v);
            }

            int[]? grup = null;
            var labelGrup = new List<string>();
            if (adaGrupDiminta)
            {
                var tingkat = ds.Levels(grupVar);
                if (tingkat.Count < 2)
                {
                    blocks.Add(Blocks.Note(
                        $"Variabel grup '{grupVar}' hanya punya {tingkat.Count} tingkat yang terisi; "
                        + "butuh dua. Bootstrap selisih tidak dijalankan.", NoteKind.Error));
                    return blocks;
                }
                if (tingkat.Count > 2)
                    blocks.Add(Blocks.Note(
                        $"Variabel grup '{grupVar}' punya {tingkat.Count} tingkat. "
                        + $"Yang dipakai hanya dua yang muncul lebih dulu: "
                        + $"'{tingkat[0]}' dan '{tingkat[1]}'.", NoteKind.Warning));

                var teks = ds.Text(grupVar);
                grup = new int[barisLengkap.Count];
                for (int i = 0; i < barisLengkap.Count; i++)
                    grup[i] = string.Equals(teks[barisLengkap[i]], tingkat[1],
                                            StringComparison.Ordinal) ? 1 : 0;
                labelGrup.Add(tingkat[0]);
                labelGrup.Add(tingkat[1]);
            }

            Hasil h;
            try
            {
                h = Hitung(kolom.ToArray(), namaKolom.ToArray(), grup, ulangan, benih, keyakinan, pakaiBca);
            }
            catch (Exception ex)
            {
                blocks.Add(Blocks.Note(ex.Message, NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.BootstrapResample, DaftarRumus.BootstrapPersentil,
                DaftarRumus.BootstrapBca, DaftarRumus.PercepatanJackknife));

            
            
            if (h.AdaGrup)
            {
                string labelSelisih = $"Selisih rerata ('{labelGrup[0]}' − '{labelGrup[1]}')";
                foreach (var b in h.Baris) b.Statistik = labelSelisih;
            }

            var langkah = new List<(string, string)>
            {
                ("Variabel", string.Join(", ", h.Nama)),
                ("Banyak baris lengkap", $"n = {Fmt.Int(h.N)}"),
                ("Banyak ulangan", $"B = {Fmt.Int(h.Ulangan)}"),
                ("Benih acak", $"{h.Benih} (tetap — hasilnya bisa diulang)"),
                ("Tingkat keyakinan", $"{Fmt.Num(h.Keyakinan * 100, 1)}%"),
                ("Metode selang", pakaiBca ? "BCa (bias-corrected & accelerated)" : "Persentil"),
            };
            if (h.AdaGrup)
            {
                langkah.Insert(1, ("Grup pembanding",
                    $"'{labelGrup[0]}' (n₀ = {Fmt.Int(h.N0)}) dan '{labelGrup[1]}' (n₁ = {Fmt.Int(h.N1)})"));
                langkah.Insert(2, ("Resample berlapis",
                    "tiap grup diresample sendiri-sendiri, ukurannya tetap seperti data asli"));
            }
            langkah.Add(("Sidik indeks resample",
                $"{h.IndeksHash} (FNV-1a atas {Fmt.Int(h.Ulangan)}×{Fmt.Int(h.N)} indeks)"));
            blocks.Add(Blocks.Substitusi("Pemasukan nilai", langkah.ToArray()));

            
            
            if (h.AdaGrup)
            {
                var barisGrup = new List<string[]>();
                for (int v = 0; v < kolom.Count; v++)
                {
                    double s0 = 0, s1 = 0;
                    int c0 = 0, c1 = 0;
                    for (int i = 0; i < grup!.Length; i++)
                    {
                        if (grup[i] == 0) { s0 += kolom[v][i]; c0++; }
                        else { s1 += kolom[v][i]; c1++; }
                    }
                    double m0 = c0 > 0 ? s0 / c0 : double.NaN;
                    double m1 = c1 > 0 ? s1 / c1 : double.NaN;
                    barisGrup.Add(new[]
                    {
                        namaKolom[v], Fmt.Num(m0, 4), Fmt.Num(m1, 4), Fmt.Num(m0 - m1, 4),
                    });
                }
                blocks.Add(Blocks.Table(
                    "Rerata tiap grup (angka yang dibandingkan)",
                    new[] { "Variabel", $"Rerata '{labelGrup[0]}'", $"Rerata '{labelGrup[1]}'", "Selisih" },
                    barisGrup.ToArray(),
                    "Kolom Selisih sama dengan Taksiran pada tabel di bawah — "
                    + "ditampilkan di sini supaya asal angkanya jelas."));
            }

            blocks.Add(Blocks.Table(
                $"Selang kepercayaan {Fmt.Num(h.Keyakinan * 100, 1)}% — {h.Baris.First().Statistik}",
                new[] { "Variabel", "Taksiran", "Galat baku", "Bias", "Bawah", "Atas", "Lebar", "Hasil" },
                h.Baris.Select(b => new[]
                {
                    b.Variabel,
                    Fmt.Num(b.Taksiran, 4),
                    Fmt.Num(b.GalatBaku, 4),
                    Fmt.Num(b.Bias, 4),
                    Fmt.Num(b.Bawah, 4),
                    Fmt.Num(b.Atas, 4),
                    Fmt.Num(b.Atas - b.Bawah, 4),
                    b.Bawah <= b.Taksiran && b.Taksiran <= b.Atas ? "memuat taksiran" : "**di luar**",
                }).ToArray(),
                "Galat baku = simpangan baku sebaran hasil resample (pembagi B−1). "
                + "Bias = rerata hasil resample − taksiran asli; makin kecil makin baik. "
                + "Kolom Hasil memeriksa kewajaran: selang seharusnya memuat taksirannya sendiri."));

            blocks.Add(Blocks.Table(
                "Bandingan metode selang & besaran BCa",
                new[] { "Variabel", "Statistik", "Persentil bawah", "Persentil atas",
                        "BCa bawah", "BCa atas", "z₀", "a (percepatan)" },
                h.Baris.Select(b => new[]
                {
                    b.Variabel,
                    b.Statistik,
                    Fmt.Num(b.BawahPersentil, 4),
                    Fmt.Num(b.AtasPersentil, 4),
                    Fmt.Num(b.BawahBca, 4),
                    Fmt.Num(b.AtasBca, 4),
                    Fmt.Num(b.Z0, 4),
                    Fmt.Num(b.Percepatan, 4),
                }).ToArray(),
                "z₀ menjauh dari 0 bila taksiran resample condong ke satu sisi; "
                + "a menjauh dari 0 bila taksiran peka terhadap satu-dua amatan. "
                + "Bila keduanya kecil, kedua metode hampir sama — dan itu memang yang diharapkan."));

            
            var spec = new ChartSpec
            {
                Kind = ChartKind.ErrorBar,
                Title = $"Selang kepercayaan {Fmt.Num(h.Keyakinan * 100, 0)}% per taksiran",
                XTitle = "Taksiran",
                YTitle = "",
                TotalN = h.N,
            };
            foreach (var b in h.Baris)
                spec.ErrorBars.Add(($"{b.Variabel} — {b.Statistik}", b.Taksiran, b.Bawah, b.Atas));
            blocks.Add(Blocks.Chart(spec.Title, spec));

            
            if (h.SebaranPertama.Length > 0)
            {
                var pertama = h.Baris[0];
                var sebar = new ChartSpec
                {
                    Kind = ChartKind.Histogram,
                    Title = $"Sebaran bootstrap — {pertama.Variabel}, {pertama.Statistik}",
                    XTitle = pertama.Statistik,
                    YTitle = "Frekuensi",
                    TotalN = h.Ulangan,
                };
                sebar.Series[pertama.Statistik] = h.SebaranPertama.ToList();
                blocks.Add(Blocks.Chart(sebar.Title, sebar));
            }

            return blocks;
        }
    }
}
