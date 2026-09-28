using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class KorelasiKanonik
    {

        public sealed class Akar
        {
            public int Indeks;

            public double R;
            public double R2;

            public double Wilks;
            public double Chi2, PChi2;
            public int DfChi2;

            public double F, Df1F, Df2F, PF;

            public double[] KoefX = Array.Empty<double>();
            public double[] KoefY = Array.Empty<double>();
            public double[] MuatanX = Array.Empty<double>();
            public double[] MuatanY = Array.Empty<double>();

            public double ProporsiX, RedundansiX, ProporsiY, RedundansiY;
        }

        public sealed class Hasil
        {
            public int N, P, Q, K;

            public List<string> NamaX = new();
            public List<string> NamaY = new();

            public double[] RerataX = Array.Empty<double>();
            public double[] SdX = Array.Empty<double>();
            public double[] RerataY = Array.Empty<double>();
            public double[] SdY = Array.Empty<double>();

            public double[,] KovX = new double[0, 0];
            public double[,] KovY = new double[0, 0];
            public double[,] KorelasiX = new double[0, 0];
            public double[,] KorelasiY = new double[0, 0];
            public double[,] KorelasiXY = new double[0, 0];

            public double[] R = Array.Empty<double>();

            public double[,] KoefX = new double[0, 0];

            public double[,] KoefY = new double[0, 0];

            public double Skala;

            public double[] NilaiSingular = Array.Empty<double>();

            public double[] NilaiSingular2 = Array.Empty<double>();

            public double SelisihJalur;

            public List<Akar> PerAkar = new();
            public List<Manova.Statistik> Statistik = new();

            public double PWilks;
            public double Alpha = 0.05;

            public double WilksTotal => PerAkar.Count > 0 ? PerAkar[0].Wilks : double.NaN;

            public int DimensiMenolak => PerAkar.Count(a => a.PF < Alpha);
        }

        

        public static Hasil? Hitung(double[][] x, double[][] y,
                                    List<string> namaX, List<string> namaY,
                                    double alpha = 0.05)
        {
            if (x is null || y is null || x.Length == 0 || y.Length == 0) return null;
            if (namaX.Count != x.Length || namaY.Count != y.Length) return null;

            int n = x[0].Length;
            int pX = x.Length, qY = y.Length;

            if (n < 12 || pX < 1 || qY < 1) return null;
            if (x.Any(c => c.Length != n) || y.Any(c => c.Length != n)) return null;
            
            
            if (n <= pX + 2 || n <= qY + 2) return null;

            int k = Math.Min(pX, qY);

            var rerataX = new double[pX];
            for (int j = 0; j < pX; j++) rerataX[j] = Rerata(x[j]);
            var rerataY = new double[qY];
            for (int j = 0; j < qY; j++) rerataY[j] = Rerata(y[j]);

            
            
            
            var sxx = Sscp(x, rerataX);
            var syy = Sscp(y, rerataY);
            var sxy = SscpSilang(x, y, rerataX, rerataY);

            var sx12 = AkarBalik(sxx, pX);
            var sy12 = AkarBalik(syy, qY);
            if (sx12 is null || sy12 is null) return null;

            
            var w = Aljabar.Kali(Aljabar.Kali(sx12, sxy), sy12);
            var wt = Aljabar.Transpose(w);

            
            var m1 = Aljabar.Kali(w, wt);
            Simetriskan(m1);
            var e1 = Aljabar.EigenSimetris(m1);

            
            var m2 = Aljabar.Kali(wt, w);
            Simetriskan(m2);
            var e2 = Aljabar.EigenSimetris(m2);

            var nilai1 = new double[pX];
            for (int i = 0; i < pX; i++) nilai1[i] = Math.Sqrt(Math.Max(0.0, e1.Nilai[i]));
            var nilai2 = new double[qY];
            for (int i = 0; i < qY; i++) nilai2[i] = Math.Sqrt(Math.Max(0.0, e2.Nilai[i]));

            var r = new double[k];
            for (int i = 0; i < k; i++) r[i] = nilai1[i];

            double selisihJalur = 0;
            for (int i = 0; i < k; i++)
                selisihJalur = Math.Max(selisihJalur, Math.Abs(nilai1[i] - nilai2[i]));

            
            
            
            
            
            
            double skala = Math.Sqrt(n - 1.0);

            var koefX = new double[pX, k];
            var koefY = new double[qY, k];
            for (int i = 0; i < k; i++)
            {
                var u = new double[pX];
                for (int j = 0; j < pX; j++) u[j] = e1.Vektor[j, i];
                var a = Aljabar.KaliVektor(sx12, u);

                
                int besar = 0;
                for (int j = 1; j < pX; j++)
                    if (Math.Abs(a[j]) > Math.Abs(a[besar])) besar = j;
                if (a[besar] < 0) for (int j = 0; j < pX; j++) a[j] = -a[j];

                
                
                
                var t = Aljabar.KaliVektor(wt, u);
                if (r[i] <= 1e-12) return null;
                var v = new double[qY];
                for (int j = 0; j < qY; j++) v[j] = t[j] / r[i];
                var b = Aljabar.KaliVektor(sy12, v);

                for (int j = 0; j < pX; j++) koefX[j, i] = a[j] * skala;
                for (int j = 0; j < qY; j++) koefY[j, i] = b[j] * skala;
            }

            var hasil = new Hasil
            {
                N = n, P = pX, Q = qY, K = k,
                NamaX = new List<string>(namaX),
                NamaY = new List<string>(namaY),
                RerataX = rerataX, RerataY = rerataY,
                SdX = x.Select(SimpanganBaku).ToArray(),
                SdY = y.Select(SimpanganBaku).ToArray(),
                KovX = Bagi(sxx, n - 1.0), KovY = Bagi(syy, n - 1.0),
                KorelasiX = KorelasiDariKov(Bagi(sxx, n - 1.0)),
                KorelasiY = KorelasiDariKov(Bagi(syy, n - 1.0)),
                KorelasiXY = KorelasiSilang(sxy, sxx, syy),
                R = r,
                KoefX = koefX, KoefY = koefY,
                Skala = skala,
                NilaiSingular = nilai1, NilaiSingular2 = nilai2,
                SelisihJalur = selisihJalur,
                Alpha = alpha,
            };

            
            for (int i = 0; i < k; i++)
            {
                var akar = new Akar { Indeks = i, R = r[i], R2 = r[i] * r[i] };

                
                double lam = 1;
                for (int j = i; j < k; j++) lam *= 1.0 - r[j] * r[j];
                akar.Wilks = lam;

                
                double mBart = n - 1.0 - (pX + qY + 1) / 2.0;
                akar.Chi2 = -mBart * Math.Log(lam);
                akar.DfChi2 = (pX - i) * (qY - i);
                akar.PChi2 = Distributions.ChiSquareUpper(akar.Chi2, akar.DfChi2);

                
                
                
                
                double pf = qY - i, qf = pX - i;
                double rr = (n - qY - 1) - (pf - qf + 1) / 2.0;
                double uu = (pf * qf - 2) / 4.0;
                double df1 = pf * qf;
                double tt = pf * pf + qf * qf - 5 > 0
                    ? Math.Sqrt((pf * pf * qf * qf - 4.0) / (pf * pf + qf * qf - 5))
                    : 1.0;
                double df2 = rr * tt - 2 * uu;
                double lm = Math.Pow(lam, 1.0 / tt);
                akar.F = (1 - lm) / lm * df2 / df1;
                akar.Df1F = df1;
                akar.Df2F = df2;
                akar.PF = Distributions.FUpper(akar.F, df1, df2);

                
                
                
                var u = new double[n];
                for (int baris = 0; baris < n; baris++)
                {
                    double s = 0;
                    for (int j = 0; j < pX; j++) s += (x[j][baris] - rerataX[j]) * koefX[j, i];
                    u[baris] = s;
                }
                var vv = new double[n];
                for (int baris = 0; baris < n; baris++)
                {
                    double s = 0;
                    for (int j = 0; j < qY; j++) s += (y[j][baris] - rerataY[j]) * koefY[j, i];
                    vv[baris] = s;
                }

                var mx = new double[pX];
                for (int j = 0; j < pX; j++) mx[j] = Korelasi(x[j], u);
                var my = new double[qY];
                for (int j = 0; j < qY; j++) my[j] = Korelasi(y[j], vv);
                akar.MuatanX = mx;
                akar.MuatanY = my;

                akar.ProporsiX = mx.Select(t => t * t).Average();
                akar.ProporsiY = my.Select(t => t * t).Average();
                akar.RedundansiX = akar.ProporsiX * akar.R2;
                akar.RedundansiY = akar.ProporsiY * akar.R2;

                hasil.PerAkar.Add(akar);
            }

            
            
            
            
            
            
            var eigenHl = new double[k];
            for (int i = 0; i < k; i++) eigenHl[i] = r[i] * r[i] / (1.0 - r[i] * r[i]);
            hasil.Statistik = Manova.StatistikMultivariat(eigenHl, qY, pX, n - pX - 1);
            hasil.PWilks = hasil.Statistik.Count > 0 ? hasil.Statistik[0].P : double.NaN;

            return hasil;
        }

        

        private static double Rerata(double[] v)
        {
            if (v.Length == 0) return double.NaN;
            double s = 0;
            foreach (double x in v) s += x;
            return s / v.Length;
        }

        private static double SimpanganBaku(double[] v)
        {
            if (v.Length < 2) return double.NaN;
            double m = Rerata(v), s = 0;
            foreach (double x in v) { double d = x - m; s += d * d; }
            return Math.Sqrt(s / (v.Length - 1));
        }

        private static double Korelasi(double[] a, double[] b)
        {
            int n = a.Length;
            double ma = Rerata(a), mb = Rerata(b);
            double sab = 0, saa = 0, sbb = 0;
            for (int i = 0; i < n; i++)
            {
                double da = a[i] - ma, db = b[i] - mb;
                sab += da * db; saa += da * da; sbb += db * db;
            }
            double penyebut = Math.Sqrt(saa * sbb);
            return penyebut > 0 ? sab / penyebut : double.NaN;
        }

        private static double[,] Sscp(double[][] kolom, double[] rerata)
        {
            int p = kolom.Length, n = kolom[0].Length;
            var m = new double[p, p];
            for (int a = 0; a < p; a++)
                for (int b = a; b < p; b++)
                {
                    double s = 0;
                    for (int i = 0; i < n; i++) s += (kolom[a][i] - rerata[a]) * (kolom[b][i] - rerata[b]);
                    m[a, b] = s; m[b, a] = s;
                }
            return m;
        }

        private static double[,] SscpSilang(double[][] x, double[][] y, double[] rx, double[] ry)
        {
            int p = x.Length, q = y.Length, n = x[0].Length;
            var m = new double[p, q];
            for (int a = 0; a < p; a++)
                for (int b = 0; b < q; b++)
                {
                    double s = 0;
                    for (int i = 0; i < n; i++) s += (x[a][i] - rx[a]) * (y[b][i] - ry[b]);
                    m[a, b] = s;
                }
            return m;
        }

        private static double[,] Bagi(double[,] m, double d)
        {
            int a = m.GetLength(0), b = m.GetLength(1);
            var h = new double[a, b];
            for (int i = 0; i < a; i++)
                for (int j = 0; j < b; j++) h[i, j] = m[i, j] / d;
            return h;
        }

        private static double[,] KorelasiDariKov(double[,] kov)
        {
            int p = kov.GetLength(0);
            var h = new double[p, p];
            for (int a = 0; a < p; a++)
                for (int b = 0; b < p; b++)
                {
                    double penyebut = Math.Sqrt(kov[a, a] * kov[b, b]);
                    h[a, b] = penyebut > 0 ? kov[a, b] / penyebut : double.NaN;
                }
            return h;
        }

        private static double[,] KorelasiSilang(double[,] sxy, double[,] sxx, double[,] syy)
        {
            int p = sxy.GetLength(0), q = sxy.GetLength(1);
            var h = new double[p, q];
            for (int a = 0; a < p; a++)
                for (int b = 0; b < q; b++)
                {
                    double penyebut = Math.Sqrt(sxx[a, a] * syy[b, b]);
                    h[a, b] = penyebut > 0 ? sxy[a, b] / penyebut : double.NaN;
                }
            return h;
        }

        private static double[,]? AkarBalik(double[,] m, int p)
        {
            var e = Aljabar.EigenSimetris(m);
            var d = new double[p, p];
            for (int i = 0; i < p; i++)
            {
                if (e.Nilai[i] <= 1e-12) return null;
                d[i, i] = 1.0 / Math.Sqrt(e.Nilai[i]);
            }
            return Aljabar.Kali(Aljabar.Kali(e.Vektor, d), Aljabar.Transpose(e.Vektor));
        }

        private static void Simetriskan(double[,] m)
        {
            int n = m.GetLength(0);
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    double r = (m[i, j] + m[j, i]) / 2.0;
                    m[i, j] = r; m[j, i] = r;
                }
        }

        

        public static List<ResultBlock> KorelasiKanonikBlocks(Dataset ds, List<string> xVars,
                                                              List<string> yVars, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Korelasi kanonik (hubungan dua blok variabel)", 1)
            };

            if (xVars is null || yVars is null || xVars.Count < 1 || yVars.Count < 1)
            {
                blocks.Add(Blocks.Note(
                    "Korelasi kanonik membutuhkan DUA blok variabel: satu blok X dan "
                    + "satu blok Y, masing-masing minimal satu variabel.",
                    NoteKind.Error));
                return blocks;
            }

            var tumpang = xVars.Intersect(yVars, StringComparer.Ordinal).ToList();
            if (tumpang.Count > 0)
            {
                blocks.Add(Blocks.Note(
                    $"Variabel {string.Join(", ", tumpang)} muncul di kedua blok. "
                    + "Kalau sebuah variabel ada di kedua sisi, korelasinya dengan "
                    + "diri sendiri selalu 1 dan hasilnya tidak bermakna.",
                    NoteKind.Error));
                return blocks;
            }

            if (xVars.Count == 1 && yVars.Count == 1)
            {
                blocks.Add(Blocks.Note(
                    "Kedua blok hanya berisi satu variabel, jadi ini sekadar korelasi "
                    + "Pearson biasa — bukan korelasi kanonik. Tambahkan variabel pada "
                    + "salah satu blok, atau pakai alat Korelasi.", NoteKind.Error));
                return blocks;
            }

            var perlu = new List<string>(xVars);
            perlu.AddRange(yVars);
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < 16)
            {
                blocks.Add(Blocks.Note(
                    $"Baris lengkap hanya {baris.Count}. Korelasi kanonik dengan "
                    + $"{xVars.Count + yVars.Count} variabel butuh jauh lebih banyak "
                    + "baris daripada jumlah variabelnya.", NoteKind.Error));
                return blocks;
            }

            var semua = perlu.ToDictionary(n => n, n => ds.Numeric(n));
            var kolX = new List<double[]>();
            foreach (var nama in xVars)
            {
                var v = new double[baris.Count];
                for (int i = 0; i < baris.Count; i++) v[i] = semua[nama][baris[i]] ?? double.NaN;
                kolX.Add(v);
            }
            var kolY = new List<double[]>();
            foreach (var nama in yVars)
            {
                var v = new double[baris.Count];
                for (int i = 0; i < baris.Count; i++) v[i] = semua[nama][baris[i]] ?? double.NaN;
                kolY.Add(v);
            }

            var h = Hitung(kolX.ToArray(), kolY.ToArray(), xVars, yVars, alpha);
            if (h is null)
            {
                blocks.Add(Blocks.Note(
                    "Korelasi kanonik tidak bisa dihitung. Penyebab yang paling "
                    + "sering: salah satu variabel nilainya sama semua (ragamnya nol), "
                    + "atau ada variabel yang merupakan kombinasi linear persis dari "
                    + "variabel lain di blok yang sama, sehingga matriksnya singular.",
                    NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.KorelasiKanonik, DaftarRumus.BobotKanonik,
                DaftarRumus.UjiKanonik));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Blok X", string.Join(", ", xVars)),
                ("Blok Y", string.Join(", ", yVars)),
                ("Banyak baris lengkap", $"n = {Fmt.Int(h.N)}"),
                ("Ukuran blok", $"p = {Fmt.Int(h.P)}, q = {Fmt.Int(h.Q)}"),
                ("Banyak akar", $"k = min(p,q) = {Fmt.Int(h.K)}"),
                ("Korelasi kanonik", string.Join("; ",
                    h.R.Select(r => Fmt.Num(r, 6))))));

            
            blocks.Add(Blocks.Table(
                "Korelasi kanonik dan uji tiap akar",
                new[] { "Akar", "r", "r²", "Λ (berurut)", "χ²", "df",
                        "p (χ²)", "F", "df1", "df2", "p (F)" },
                h.PerAkar.Select(a => new[]
                {
                    Fmt.Int(a.Indeks + 1), Fmt.Num(a.R, 6), Fmt.Num(a.R2, 6),
                    Fmt.Num(a.Wilks, 6), Fmt.Num(a.Chi2, 4), Fmt.Int(a.DfChi2),
                    Fmt.P(a.PChi2), Fmt.Num(a.F, 4),
                    Fmt.Num(a.Df1F, 2), Fmt.Num(a.Df2F, 4), Fmt.P(a.PF),
                }).ToArray(),
                "HANYA BARIS PERTAMA yang p-nya boleh dipakai. Uji pada akar kedua "
                + "dan seterusnya mengandaikan akar-akar sebelumnya tepat nol — "
                + "yaitu kasus batas yang tidak dipenuhi data nyata — sehingga p-nya "
                + "terlalu besar dan ujinya nyaris tidak pernah menolak. Ini bukan "
                + "soal sampel kecil: pengukuran di bawah H0 menunjukkan hal yang "
                + "sama pada n = 60 sampai n = 1000. Untuk pertanyaan \"adakah "
                + "hubungan antara kedua blok\", bacalah baris pertama saja."));

            
            var barisKoef = new List<string[]>();
            foreach (var a in h.PerAkar)
            {
                for (int j = 0; j < h.P; j++)
                    barisKoef.Add(new[]
                    {
                        Fmt.Int(a.Indeks + 1), "X", h.NamaX[j],
                        Fmt.Num(h.KoefX[j, a.Indeks], 6),
                        Fmt.Num(a.MuatanX[j], 6),
                    });
                for (int j = 0; j < h.Q; j++)
                    barisKoef.Add(new[]
                    {
                        Fmt.Int(a.Indeks + 1), "Y", h.NamaY[j],
                        Fmt.Num(h.KoefY[j, a.Indeks], 6),
                        Fmt.Num(a.MuatanY[j], 6),
                    });
            }
            blocks.Add(Blocks.Table(
                "Bobot kanonik dan muatan",
                new[] { "Akar", "Blok", "Variabel", "Bobot", "Muatan" },
                barisKoef.ToArray(),
                "Bobot (weight) adalah koefisien kombinasi linear yang membentuk "
                + "variat kanonik; ia dinormalkan supaya ragam variatnya = 1. "
                + "Muatan (loading) adalah korelasi variabel asal dengan variat "
                + "kanoniknya. Keduanya sering menunjuk variabel yang berbeda, dan "
                + "yang lebih mudah ditafsirkan biasanya MUATAN — sebab bobot bisa "
                + "membesar atau berubah tanda hanya karena ada variabel lain di blok "
                + "yang sama yang berkorelasi kuat. Bobot yang tandanya berlawanan "
                + "dengan muatannya adalah tanda adanya kolinearitas."));

            
            blocks.Add(Blocks.Table(
                "Ragam yang terambil dan redundansi",
                new[] { "Akar", "r²", "Proporsi ragam X", "Redundansi X",
                        "Proporsi ragam Y", "Redundansi Y" },
                h.PerAkar.Select(a => new[]
                {
                    Fmt.Int(a.Indeks + 1), Fmt.Num(a.R2, 6),
                    Fmt.Num(a.ProporsiX, 6), Fmt.Num(a.RedundansiX, 6),
                    Fmt.Num(a.ProporsiY, 6), Fmt.Num(a.RedundansiY, 6),
                }).ToArray(),
                "Proporsi ragam = rata-rata muatan kuadrat: berapa bagian keragaman "
                + "blok itu yang terwakili oleh variat kanoniknya. Redundansi = "
                + "proporsi ragam × r²: berapa bagian keragaman blok itu yang "
                + "terjelaskan oleh blok di seberangnya. Inilah ukuran yang menjawab "
                + "\"seberapa berguna hubungan ini\", dan angkanya biasanya jauh lebih "
                + "kecil daripada r². Korelasi kanonik yang tinggi TIDAK berarti "
                + "kedua blok saling menjelaskan banyak."));

            
            blocks.Add(Blocks.Table(
                "Statistik multivariat",
                new[] { "Statistik", "Nilai", "F", "df1", "df2", "p" },
                h.Statistik.Select(s => new[]
                {
                    s.Nama, Fmt.Num(s.Nilai, 6), Fmt.Num(s.F, 4),
                    Fmt.Num(s.Df1, 2), Fmt.Num(s.Df2, 4), Fmt.P(s.P),
                }).ToArray(),
                "Keempatnya menguji hipotesis yang sama — \"tidak ada hubungan sama "
                + "sekali antara kedua blok\" — dan keempatnya bertumpu pada Λ₀, "
                + "sehingga di sini sah dipakai. Angka p-nya berbeda-beda karena "
                + "derajat bebas dan rumus hampirannya berbeda; itu wajar. Yang "
                + "penting: keempatnya biasanya searah. Bila bertentangan, Pillai's "
                + "trace yang paling aman, karena paling tahan terhadap asumsi yang "
                + "dilanggar dan terhadap ukuran blok yang tidak seimbang."));

            
            blocks.Add(Blocks.Table(
                "Ringkasan",
                new[] { "Besaran", "Nilai" },
                new[]
                {
                    new[] { "Banyak amatan", Fmt.Int(h.N) },
                    new[] { "Ukuran blok X", Fmt.Int(h.P) },
                    new[] { "Ukuran blok Y", Fmt.Int(h.Q) },
                    new[] { "Banyak akar", Fmt.Int(h.K) },
                    new[] { "Korelasi kanonik terbesar", Fmt.Num(h.R[0], 6) },
                    new[] { "Korelasi kanonik terkecil", Fmt.Num(h.R[h.K - 1], 6) },
                    new[] { "Λ₀ = Π(1−r²)", Fmt.Num(h.WilksTotal, 6) },
                    new[] { "Dimensi yang menolak (akar 1)", Fmt.Int(h.DimensiMenolak) },
                },
                "Λ₀ adalah hasil kali semua (1−r²) dan nilainya sama dengan baris "
                + "Wilks' lambda di tabel statistik multivariat — itu memang besaran "
                + "yang sama, dihitung lewat dua jalan."));

            
            var utama = h.Statistik[0];
            blocks.Add(Blocks.Note(
                $"Uji multivariat {(utama.P < alpha ? "MENOLAK" : "tidak menolak")} "
                + $"pada taraf {Fmt.Num(100 * (1 - alpha), 0)}% "
                + $"(Wilks Λ₀ = {Fmt.Num(h.WilksTotal, 6)}, p = {Fmt.P(utama.P)}). "
                + (utama.P < alpha
                    ? "Ada hubungan antara kedua blok yang tidak bisa dijelaskan oleh "
                      + "kebetulan. Yang belum terjawab: berapa dimensi hubungannya. "
                      + "Lihat baris pertama tabel akar untuk dimensi pertama, dan "
                      + "tabel redundansi untuk tahu seberapa besar artinya secara "
                      + "praktis — bukan hanya secara statistik."
                    : "Tidak ada bukti hubungan antara kedua blok. Perhatikan bahwa "
                      + "\"tidak menolak\" bukan berarti \"tidak berhubungan\": dengan "
                      + "sampel kecil, hubungan yang sungguhan pun bisa tak terlihat."),
                utama.P < alpha ? NoteKind.Ok : NoteKind.Info));

            if (h.K > 1)
            {
                blocks.Add(Blocks.Note(
                    "Alat ini melaporkan uji untuk setiap akar karena itu yang lazim "
                    + "diminta, TETAPI hanya akar pertama yang p-nya sah. Untuk akar "
                    + "kedua dan seterusnya, p yang kecil maupun yang besar sama-sama "
                    + "tidak bisa dipercaya — pengukuran di bawah H0 menunjukkan "
                    + "laju penolakan sekitar 0,002 padahal seharusnya 0,05. Karena "
                    + "itu jangan memutuskan \"ada dua dimensi\" atau \"hanya satu "
                    + "dimensi\" dari p-nya. Yang bisa dipakai untuk menilai akar "
                    + "kedua: besarnya r₂ itu sendiri dan redundansinya, ditambah "
                    + "pengetahuan tentang bidangnya.", NoteKind.Warning));
            }

            blocks.Add(Blocks.Note(
                "Korelasi kanonik mengandaikan hubungan yang LINEAR dan amatan yang "
                + "saling bebas. Yang pertama bisa diperiksa dengan menyebar-plot "
                + "tiap variabel; yang kedua tidak bisa diuji dari data — ia harus "
                + "dijamin oleh cara datanya dikumpulkan. Perhatikan juga bahwa "
                + "korelasi kanonik tidak mengenal arah pengaruh: hasilnya sama saja "
                + "bila kedua blok ditukar, dan bobot yang besar tidak berarti "
                + "variabel itu yang \"menyebabkan\" apa pun.", NoteKind.Info));

            return blocks;
        }
    }
}
