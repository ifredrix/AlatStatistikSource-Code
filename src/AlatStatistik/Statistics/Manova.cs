using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class Manova
    {
        public sealed class Hasil
        {
            public int N;
            public int P;
            public int K;
            public int DfHip;
            public int DfGalat;
            public List<string> Tingkat = new();
            public List<string> Terikat = new();
            public List<int> NPerGrup = new();
            public double[,] E = new double[0, 0];
            public double[,] H = new double[0, 0];
            public double[] Eigen = Array.Empty<double>();
            public double[] RerataUmum = Array.Empty<double>();
            public List<Statistik> Statistik = new();
            public List<Lanjutan> Lanjutan = new();
            public HasilBox Box = new();
            public double Wilks => Statistik.Count > 0 ? Statistik[0].Nilai : double.NaN;
        }

        public sealed class Statistik
        {
            public string Nama = "";
            public double Nilai;
            public double F;
            public double Df1;
            public double Df2;
            public double P;
        }

        public sealed class Lanjutan
        {
            public string Nama = "";
            public double SsEfek;
            public double SsGalat;
            public double SsTotal;
            public int Df1;
            public int Df2;
            public double F;
            public double P;
            public double Eta2;
            public double Eta2Parsial;
            public List<double> Rerata = new();
            public List<double> SimpanganBaku = new();
        }

        public sealed class HasilBox
        {
            public double M;
            public double Koreksi;
            public double Chi2;
            public int Df;
            public double P;
        }

        public static Hasil? Hitung(List<double[]> y, string[] grup, List<string> namaTerikat)
        {
            int n = grup.Length;
            int p = y.Count;
            if (n < 8 || p < 2 || namaTerikat.Count != p) return null;
            foreach (var kolom in y) if (kolom.Length != n) return null;

            var tingkat = grup.Distinct().OrderBy(g => g, StringComparer.Ordinal).ToList();
            int k = tingkat.Count;
            if (k < 2) return null;

            
            
            int q = k - 1;
            int v = n - k;
            if (v < p + 1) return null;

            var rerataUmum = new double[p];
            for (int j = 0; j < p; j++)
            {
                double s = 0;
                for (int i = 0; i < n; i++) s += y[j][i];
                rerataUmum[j] = s / n;
            }

            var E = new double[p, p];
            var H = new double[p, p];
            var nGrup = new List<int>();
            var rerataGrup = new List<double[]>();

            foreach (var t in tingkat)
            {
                var idx = new List<int>();
                for (int i = 0; i < n; i++)
                    if (string.Equals(grup[i], t, StringComparison.Ordinal)) idx.Add(i);

                int nk = idx.Count;
                nGrup.Add(nk);
                if (nk == 0) return null;

                var bar = new double[p];
                for (int j = 0; j < p; j++)
                {
                    double s = 0;
                    foreach (int i in idx) s += y[j][i];
                    bar[j] = s / nk;
                }
                rerataGrup.Add(bar);

                
                for (int a = 0; a < p; a++)
                    for (int b = 0; b < p; b++)
                    {
                        double s = 0;
                        foreach (int i in idx) s += (y[a][i] - bar[a]) * (y[b][i] - bar[b]);
                        E[a, b] += s;
                    }

                
                for (int a = 0; a < p; a++)
                    for (int b = 0; b < p; b++)
                        H[a, b] += nk * (bar[a] - rerataUmum[a]) * (bar[b] - rerataUmum[b]);
            }

            var eigen = NilaiEigen(E, H);
            if (eigen is null) return null;

            var hasil = new Hasil
            {
                N = n, P = p, K = k, DfHip = q, DfGalat = v,
                Tingkat = tingkat, Terikat = new List<string>(namaTerikat),
                NPerGrup = nGrup, E = E, H = H, Eigen = eigen,
                RerataUmum = rerataUmum,
                Statistik = StatistikMultivariat(eigen, p, q, v),
            };

            hasil.Lanjutan = LanjutanSatuVariabel(y, grup, tingkat, namaTerikat, E, n, k);
            hasil.Box = UjiBox(y, grup, tingkat, p, k, n);

            return hasil;
        }

        private static double[]? NilaiEigen(double[,] E, double[,] H)
        {
            int p = E.GetLength(0);

            var eigE = Aljabar.EigenSimetris(E);
            for (int i = 0; i < p; i++)
                if (eigE.Nilai[i] <= 1e-12) return null;

            
            var d = new double[p, p];
            for (int i = 0; i < p; i++) d[i, i] = 1.0 / Math.Sqrt(eigE.Nilai[i]);
            var em12 = Aljabar.Kali(Aljabar.Kali(eigE.Vektor, d), Aljabar.Transpose(eigE.Vektor));

            var s = Aljabar.Kali(Aljabar.Kali(em12, H), em12);

            
            
            for (int i = 0; i < p; i++)
                for (int j = i + 1; j < p; j++)
                {
                    double r = (s[i, j] + s[j, i]) / 2.0;
                    s[i, j] = r; s[j, i] = r;
                }

            var eig = Aljabar.EigenSimetris(s);
            var nilai = new double[p];
            for (int i = 0; i < p; i++) nilai[i] = eig.Nilai[i];
            return nilai;
        }

        internal static List<Statistik> StatistikMultivariat(double[] eigen, int p, int q, int v)
        {
            int s = Math.Min(p, q);
            double m = (Math.Abs(p - q) - 1) / 2.0;
            double nn = (v - p - 1) / 2.0;

            
            double wilks = 1, pillai = 0, hotelling = 0, roy = double.NegativeInfinity;
            foreach (double e in eigen)
            {
                double lam = e / (1.0 + e);
                wilks *= 1.0 - lam;
                pillai += lam;
                hotelling += e;
                if (e > roy) roy = e;
            }

            var daftar = new List<Statistik>();

            {
                double ps = p, qs = q;
                double tmp = ps * ps + qs * qs - 5;
                double t = tmp > 0 ? Math.Sqrt((ps * ps * qs * qs - 4) / tmp) : 1.0;
                double df1 = ps * qs;
                double df2 = (v - (ps - qs + 1) / 2.0) * t - (ps * qs - 2) / 2.0;
                double lamT = Math.Pow(wilks, 1.0 / t);
                double f = (1.0 - lamT) / lamT * (df2 / df1);
                daftar.Add(new Statistik
                {
                    Nama = "Wilks' lambda (Λ)", Nilai = wilks, F = f, Df1 = df1, Df2 = df2,
                    P = Distributions.FUpper(f, df1, df2),
                });
            }

            {
                double df1 = s * (2 * m + s + 1);
                double df2 = s * (2 * nn + s + 1);
                double f = df2 / df1 * pillai / (s - pillai);
                daftar.Add(new Statistik
                {
                    Nama = "Pillai's trace (V)", Nilai = pillai, F = f, Df1 = df1, Df2 = df2,
                    P = Distributions.FUpper(f, df1, df2),
                });
            }

            
            {
                double df1, df2, f;
                if (nn > 0)
                {
                    double b = (p + 2 * nn) * (q + 2 * nn) / 2.0 / (2 * nn + 1) / (nn - 1);
                    df1 = p * q;
                    df2 = 4 + (p * q + 2) / (b - 1);
                    double c = (df2 - 2) / 2.0 / nn;
                    f = df2 / df1 * hotelling / c;
                }
                else
                {
                    df1 = s * (2 * m + s + 1);
                    df2 = s * (s * nn + 1);
                    f = df2 / df1 / s * hotelling;
                }
                daftar.Add(new Statistik
                {
                    Nama = "Hotelling–Lawley trace (U)", Nilai = hotelling, F = f,
                    Df1 = df1, Df2 = df2,
                    P = Distributions.FUpper(f, df1, df2),
                });
            }

            
            {
                double rr = Math.Max(p, q);
                double df1 = rr;
                double df2 = v - rr + q;
                double f = df2 / df1 * roy;
                daftar.Add(new Statistik
                {
                    Nama = "Roy's greatest root (θ)", Nilai = roy, F = f, Df1 = df1, Df2 = df2,
                    P = Distributions.FUpper(f, df1, df2),
                });
            }

            return daftar;
        }

        private static List<Lanjutan> LanjutanSatuVariabel(
            List<double[]> y, string[] grup, List<string> tingkat,
            List<string> nama, double[,] E, int n, int k)
        {
            var keluaran = new List<Lanjutan>();
            int df1 = k - 1, df2 = n - k;

            for (int j = 0; j < nama.Count; j++)
            {
                double ssGalat = E[j, j];
                double ssTotal = 0;
                for (int i = 0; i < n; i++)
                {
                    double d = y[j][i] - Rerata(y[j]);
                    ssTotal += d * d;
                }
                double ssEfek = ssTotal - ssGalat;
                double f = ssGalat > 0 ? (ssEfek / df1) / (ssGalat / df2) : double.NaN;

                var baris = new Lanjutan
                {
                    Nama = nama[j],
                    SsEfek = ssEfek, SsGalat = ssGalat, SsTotal = ssTotal,
                    Df1 = df1, Df2 = df2, F = f,
                    P = Distributions.FUpper(f, df1, df2),
                    Eta2 = ssTotal > 0 ? ssEfek / ssTotal : double.NaN,
                    Eta2Parsial = ssEfek + ssGalat > 0 ? ssEfek / (ssEfek + ssGalat) : double.NaN,
                };

                foreach (var t in tingkat)
                {
                    var nilai = new List<double>();
                    for (int i = 0; i < n; i++)
                        if (string.Equals(grup[i], t, StringComparison.Ordinal))
                            nilai.Add(y[j][i]);

                    baris.Rerata.Add(Rerata(nilai.ToArray()));
                    double rata = baris.Rerata[baris.Rerata.Count - 1];
                    double jumlah = 0;
                    foreach (double x in nilai) { double d = x - rata; jumlah += d * d; }
                    baris.SimpanganBaku.Add(nilai.Count > 1
                        ? Math.Sqrt(jumlah / (nilai.Count - 1)) : double.NaN);
                }

                keluaran.Add(baris);
            }

            return keluaran;
        }

        private static HasilBox UjiBox(
            List<double[]> y, string[] grup, List<string> tingkat, int p, int k, int n)
        {
            var box = new HasilBox();
            var vg = new List<int>();
            var sg = new List<double[,]>();

            foreach (var t in tingkat)
            {
                var idx = new List<int>();
                for (int i = 0; i < n; i++)
                    if (string.Equals(grup[i], t, StringComparison.Ordinal)) idx.Add(i);
                vg.Add(idx.Count - 1);
                if (idx.Count < 2) { box.P = double.NaN; return box; }

                var bar = new double[p];
                for (int a = 0; a < p; a++)
                {
                    double s = 0;
                    foreach (int i in idx) s += y[a][i];
                    bar[a] = s / idx.Count;
                }

                var s_g = new double[p, p];
                for (int a = 0; a < p; a++)
                    for (int b = 0; b < p; b++)
                    {
                        double s = 0;
                        foreach (int i in idx) s += (y[a][i] - bar[a]) * (y[b][i] - bar[b]);
                        s_g[a, b] = s / (idx.Count - 1);
                    }
                sg.Add(s_g);
            }

            int vgJumlah = vg.Sum();
            if (vgJumlah <= 0) { box.P = double.NaN; return box; }

            var spool = new double[p, p];
            for (int g = 0; g < k; g++)
                for (int a = 0; a < p; a++)
                    for (int b = 0; b < p; b++)
                        spool[a, b] += vg[g] * sg[g][a, b];
            for (int a = 0; a < p; a++)
                for (int b = 0; b < p; b++) spool[a, b] /= vgJumlah;

            double lnPool = LogDet(spool);
            double jumlah = 0;
            for (int g = 0; g < k; g++) jumlah += vg[g] * LogDet(sg[g]);
            if (double.IsNaN(lnPool) || double.IsNaN(jumlah)) { box.P = double.NaN; return box; }

            box.M = vgJumlah * lnPool - jumlah;

            double invers = 0;
            foreach (int x in vg) invers += 1.0 / x;
            box.Koreksi = (invers - 1.0 / vgJumlah)
                          * (2.0 * p * p + 3.0 * p - 1) / (6.0 * (p + 1) * (k - 1));
            box.Chi2 = (1 - box.Koreksi) * box.M;
            box.Df = p * (p + 1) * (k - 1) / 2;
            box.P = Distributions.ChiSquareUpper(box.Chi2, box.Df);
            return box;
        }

        private static double LogDet(double[,] s)
        {
            var eig = Aljabar.EigenSimetris(s);
            double jumlah = 0;
            foreach (double l in eig.Nilai)
            {
                if (l <= 0) return double.NaN;
                jumlah += Math.Log(l);
            }
            return jumlah;
        }

        private static double Rerata(double[] x)
        {
            if (x.Length == 0) return double.NaN;
            double s = 0;
            foreach (double v in x) s += v;
            return s / x.Length;
        }

        

        public static List<ResultBlock> ManovaBlocks(Dataset ds, List<string> terikat,
                                                     string faktor, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("MANOVA (analisis ragam multivariat)", 1)
            };

            if (terikat is null || terikat.Count < 2)
            {
                blocks.Add(Blocks.Note(
                    "MANOVA membandingkan RERATA beberapa variabel terikat sekaligus, "
                    + "jadi butuh minimal dua variabel terikat. Untuk satu variabel, "
                    + "pakai ANOVA biasa.", NoteKind.Error));
                return blocks;
            }

            var perlu = new List<string>(terikat) { faktor };
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < 12)
            {
                blocks.Add(Blocks.Note(
                    $"Baris lengkap hanya {baris.Count}. MANOVA butuh jauh lebih banyak "
                    + "baris daripada jumlah variabel terikatnya.", NoteKind.Error));
                return blocks;
            }

            var semua = terikat.ToDictionary(n => n, n => ds.Numeric(n));
            var semuaG = ds.Text(faktor);

            var kolom = new List<double[]>();
            foreach (var nama in terikat)
            {
                var v = new double[baris.Count];
                for (int i = 0; i < baris.Count; i++) v[i] = semua[nama][baris[i]] ?? double.NaN;
                kolom.Add(v);
            }
            var gp = new string[baris.Count];
            for (int i = 0; i < baris.Count; i++) gp[i] = semuaG[baris[i]] ?? "(kosong)";

            var tingkat = gp.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (tingkat.Count < 2)
            {
                blocks.Add(Blocks.Note(
                    $"Variabel '{faktor}' hanya punya {tingkat.Count} tingkat. "
                    + "MANOVA membutuhkan minimal dua kelompok.", NoteKind.Error));
                return blocks;
            }

            var h = Hitung(kolom, gp, terikat);
            if (h is null)
            {
                blocks.Add(Blocks.Note(
                    "MANOVA tidak bisa dihitung. Penyebab yang paling sering: ada "
                    + "variabel terikat yang nilainya sama di dalam satu kelompok "
                    + "(ragamnya nol), sehingga matriks E tidak bisa dibalik. "
                    + "Periksa juga apakah ada kelompok yang terlalu kecil.",
                    NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.ManovaUji, DaftarRumus.ManovaStatistik, DaftarRumus.ManovaBoxM));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Variabel terikat", string.Join(", ", terikat)),
                ("Faktor", faktor),
                ("Banyak baris lengkap", $"n = {Fmt.Int(h.N)}"),
                ("Banyak variabel terikat", $"p = {Fmt.Int(h.P)}"),
                ("Banyak kelompok", $"k = {Fmt.Int(h.K)}"),
                ("Derajat bebas", $"hipotesis = {Fmt.Int(h.DfHip)}, "
                                  + $"galat = {Fmt.Int(h.DfGalat)}"),
                ("Nilai eigen E⁻¹H", string.Join("; ", h.Eigen.Select(e => Fmt.Num(e, 6))))));

            blocks.Add(Blocks.Table(
                "Uji multivariat",
                new[] { "Statistik", "Nilai", "F", "df1", "df2", "p" },
                h.Statistik.Select(s => new[]
                {
                    s.Nama, Fmt.Num(s.Nilai, 6), Fmt.Num(s.F, 4),
                    Fmt.Num(s.Df1, 2), Fmt.Num(s.Df2, 4), Fmt.P(s.P),
                }).ToArray(),
                "Keempat statistik menguji hipotesis yang SAMA — \"semua rerata "
                + "kelompok sama pada semua variabel terikat sekaligus\" — tetapi "
                + "dengan cara menimbang yang berbeda. Bila keempatnya searah, "
                + "kesimpulannya kuat. Bila bertentangan, Pillai's trace yang "
                + "paling aman dipakai karena paling tahan terhadap asumsi yang "
                + "dilanggar dan kelompok yang tidak seimbang."));

            blocks.Add(Blocks.Table(
                "Uji lanjutan satu variabel",
                new[] { "Variabel terikat", "Jumlah kuadrat efek", "df1",
                        "Jumlah kuadrat galat", "df2", "F", "p", "Eta² parsial" },
                h.Lanjutan.Select(a => new[]
                {
                    a.Nama, Fmt.Num(a.SsEfek, 6), Fmt.Int(a.Df1),
                    Fmt.Num(a.SsGalat, 6), Fmt.Int(a.Df2),
                    Fmt.Num(a.F, 4), Fmt.P(a.P), Fmt.Num(a.Eta2Parsial, 6),
                }).ToArray(),
                "Tabel ini menjawab \"variabel terikat yang MANA yang membuat "
                + "perbedaan itu?\". Ia dilaporkan sebagai tindak lanjut, bukan "
                + "sebagai pengganti uji multivariat: menjalankan beberapa ANOVA "
                + "lalu memakai yang menolak sama dengan menaikkan peluang salah. "
                + "Jumlah kuadrat galat di sini diambil dari diagonal matriks E "
                + "yang sama dengan yang dipakai MANOVA, jadi keduanya tidak "
                + "mungkin saling bertentangan."));

            blocks.Add(Blocks.Table(
                "Rerata tiap kelompok",
                new[] { "Variabel terikat", "Kelompok", "n", "Rerata", "Simpangan baku" },
                h.Lanjutan.SelectMany(a => Enumerable.Range(0, h.K).Select(t => new[]
                {
                    a.Nama, h.Tingkat[t], Fmt.Int(h.NPerGrup[t]),
                    Fmt.Num(a.Rerata[t], 4), Fmt.Num(a.SimpanganBaku[t], 4),
                })).ToArray(),
                "Bacalah tabel ini lebih dulu untuk memeriksa apakah arah "
                + "perbedaannya masuk akal. Uji yang menolak pada data yang "
                + "arahnya tidak masuk akal lebih sering menandakan kekeliruan "
                + "penyiapan data daripada temuan."));

            blocks.Add(Blocks.Table(
                "Uji Box M (kesamaan matriks kovarians)",
                new[] { "Besaran", "Nilai" },
                new[]
                {
                    new[] { "Box M", Fmt.Num(h.Box.M, 6) },
                    new[] { "Faktor koreksi c", Fmt.Num(h.Box.Koreksi, 6) },
                    new[] { "χ² (pendekatan)", Fmt.Num(h.Box.Chi2, 6) },
                    new[] { "df", Fmt.Int(h.Box.Df) },
                    new[] { "p", Fmt.P(h.Box.P) },
                },
                "H0: matriks kovarians semua kelompok sama. MANOVA mengandaikan "
                + "ini. Bila p < 0,05, asumsinya dilanggar — dan itu TIDAK berarti "
                + "analisisnya batal: Pillai's trace tetap cukup tahan. Yang "
                + "sebaiknya dilakukan: periksa apakah ada kelompok dengan "
                + "keragaman jauh lebih besar, dan sebutkan pelanggaran itu apa "
                + "adanya di laporan."));

            blocks.Add(Blocks.Table(
                "Ringkasan",
                new[] { "Besaran", "Nilai" },
                new[]
                {
                    new[] { "Banyak amatan", Fmt.Int(h.N) },
                    new[] { "Variabel terikat", Fmt.Int(h.P) },
                    new[] { "Kelompok", Fmt.Int(h.K) },
                    new[] { "Nilai eigen E⁻¹H (terbesar)", Fmt.Num(h.Eigen[0], 6) },
                    new[] { "Nilai eigen E⁻¹H (terkecil)", Fmt.Num(h.Eigen[h.P - 1], 6) },
                    new[] { "Wilks' lambda", Fmt.Num(h.Wilks, 6) },
                },
                "Nilai eigen E⁻¹H adalah bahan mentah keempat statistik: Wilks "
                + "mengalikan (1−λ) dengan λ = e/(1+e), Pillai menjumlahkan λ, "
                + "Hotelling–Lawley menjumlahkan e, dan Roy mengambil e terbesar. "
                + "Karena itu keempatnya bisa berbeda meski berasal dari matriks "
                + "yang sama."));

            
            var utama = h.Statistik[0];
            var kuat = h.Statistik.Where(s => s.P < alpha).ToList();

            if (kuat.Count == h.Statistik.Count)
            {
                blocks.Add(Blocks.Note(
                    $"Keempat statistik menolak pada taraf {Fmt.Num(100 * (1 - alpha), 0)}%: "
                    + $"ada perbedaan rerata antar kelompok yang tidak bisa dijelaskan "
                    + $"oleh kebetulan. Ini menjawab \"apakah ada perbedaan\", bukan "
                    + "\"kelompok mana yang berbeda\" — untuk itu lihat tabel rerata "
                    + "dan lakukan perbandingan berganda pada variabel terikat yang "
                    + "memang berbeda.", NoteKind.Info));
            }
            else if (kuat.Count > 0)
            {
                blocks.Add(Blocks.Note(
                    $"Statistik multivariatnya TIDAK searah: {kuat.Count} dari "
                    + $"{h.Statistik.Count} menolak. Ini biasanya berarti efeknya "
                    + "hanya kuat pada satu arah (satu nilai eigen menonjol), "
                    + "sehingga Roy dan Hotelling–Lawley menolak sementara Wilks "
                    + "belum. Jangan memilih statistik yang menolak lalu melaporkan "
                    + "hanya itu; laporkan keempatnya.", NoteKind.Warning));
            }
            else
            {
                blocks.Add(Blocks.Note(
                    $"Tidak ada statistik yang menolak pada taraf "
                    + $"{Fmt.Num(100 * (1 - alpha), 0)}% (Wilks p = {Fmt.P(utama.P)}). "
                    + "Tidak ada bukti perbedaan rerata antar kelompok. Perhatikan "
                    + "bahwa \"tidak menolak\" bukan berarti \"sama\" — dengan "
                    + "sampel kecil, perbedaan yang sungguhan pun bisa tak terlihat.",
                    NoteKind.Info));
            }

            if (h.Box.P < 0.05)
            {
                blocks.Add(Blocks.Note(
                    $"Uji Box M menolak (p = {Fmt.P(h.Box.P)}): matriks kovarians "
                    + "kelompok tidak sama. Ini melanggar asumsi MANOVA. Yang paling "
                    + "terpengaruh adalah Wilks dan Roy; Pillai's trace masih dapat "
                    + "dipercaya. Sebutkan pelanggaran ini di laporan, dan jangan "
                    + "menyembunyikannya.", NoteKind.Warning));
            }

            blocks.Add(Blocks.Note(
                "MANOVA mengandaikan tiga hal: amatan saling bebas, tiap variabel "
                + "terikat menyebar normal di dalam kelompok, dan kovariansnya sama "
                + "antar kelompok. Yang ketiga diuji di tabel Box M di atas; yang "
                + "kedua bisa diperiksa per variabel dengan uji kenormalan. Yang "
                + "pertama tidak bisa diuji dari data — ia harus dijamin oleh cara "
                + "datanya dikumpulkan.", NoteKind.Info));

            return blocks;
        }
    }
}
