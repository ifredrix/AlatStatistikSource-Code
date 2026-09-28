using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class AnovaBerulang
    {
        public sealed class Hasil
        {
            public int N, K, DfSubjek, DfWaktu, DfGalat;

            public int SubjekDibuang;

            public List<string> Tingkat = new();
            public List<double> RerataWaktu = new();
            public List<double> SdWaktu = new();

            public double SsTotal, SsSubjek, SsDalam, SsWaktu, SsGalat;
            public double MsWaktu, MsGalat, F, P, Eta2Parsial;

            public double[,] Kovarians = new double[0, 0];

            public double[] AkarCiri = Array.Empty<double>();

            public double MauchlyW = double.NaN;
            public double MauchlyChi2 = double.NaN;
            public double MauchlyP = double.NaN;
            public int MauchlyDf;

            public double EpsGG = 1, EpsHF = 1, EpsBawah = 1;
            public double PGG = double.NaN, PHF = double.NaN, PBawah = double.NaN;

            public bool MauchlyAda => K > 2;
        }

        public static Hasil? Hitung(double[] nilai, string[] subjek, string[] waktu)
        {
            int nBaris = nilai.Length;
            if (nBaris < 8 || subjek.Length != nBaris || waktu.Length != nBaris) return null;

            var tingkat = waktu.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            int k = tingkat.Count;
            if (k < 2) return null;

            var daftarSubjek = subjek.Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList();
            var indeksTingkat = new Dictionary<string, int>();
            for (int j = 0; j < k; j++) indeksTingkat[tingkat[j]] = j;

            
            var lebar = new Dictionary<string, double?[]>();
            foreach (var s in daftarSubjek) lebar[s] = new double?[k];

            for (int i = 0; i < nBaris; i++)
            {
                var sel = lebar[subjek[i]];
                int j = indeksTingkat[waktu[i]];
                if (sel[j] is not null) return null;   
                sel[j] = nilai[i];
            }

            
            var subjekLengkap = new List<string>();
            foreach (var s in daftarSubjek)
            {
                bool lengkap = true;
                for (int j = 0; j < k; j++)
                    if (lebar[s][j] is null) { lengkap = false; break; }
                if (lengkap) subjekLengkap.Add(s);
            }

            int n = subjekLengkap.Count;
            int dibuang = daftarSubjek.Count - n;
            if (n < 4) return null;

            var Y = new double[n][];
            for (int i = 0; i < n; i++)
            {
                var sel = lebar[subjekLengkap[i]];
                var baris = new double[k];
                for (int j = 0; j < k; j++) baris[j] = sel[j] ?? double.NaN;
                Y[i] = baris;
            }

            
            var rerataWaktu = new double[k];
            for (int j = 0; j < k; j++)
            {
                double s = 0;
                for (int i = 0; i < n; i++) s += Y[i][j];
                rerataWaktu[j] = s / n;
            }
            double rerataUmum = rerataWaktu.Average();

            var rerataSubjek = new double[n];
            for (int i = 0; i < n; i++)
            {
                double s = 0;
                for (int j = 0; j < k; j++) s += Y[i][j];
                rerataSubjek[i] = s / k;
            }

            
            double ssTotal = 0;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < k; j++)
                {
                    double d = Y[i][j] - rerataUmum;
                    ssTotal += d * d;
                }

            double ssSubjek = 0;
            for (int i = 0; i < n; i++)
            {
                double d = rerataSubjek[i] - rerataUmum;
                ssSubjek += k * d * d;
            }

            double ssWaktu = 0;
            for (int j = 0; j < k; j++)
            {
                double d = rerataWaktu[j] - rerataUmum;
                ssWaktu += n * d * d;
            }

            double ssDalam = ssTotal - ssSubjek;
            double ssGalat = ssDalam - ssWaktu;

            int dfSubjek = n - 1;
            int dfWaktu = k - 1;
            int dfGalat = (n - 1) * (k - 1);
            if (dfGalat <= 0 || ssGalat <= 0) return null;

            double msWaktu = ssWaktu / dfWaktu;
            double msGalat = ssGalat / dfGalat;
            double f = msWaktu / msGalat;

            
            var S = new double[k, k];
            for (int a = 0; a < k; a++)
                for (int b = 0; b < k; b++)
                {
                    double s = 0;
                    for (int i = 0; i < n; i++)
                        s += (Y[i][a] - rerataWaktu[a]) * (Y[i][b] - rerataWaktu[b]);
                    S[a, b] = s / (n - 1);
                }

            var hasil = new Hasil
            {
                N = n, K = k, DfSubjek = dfSubjek, DfWaktu = dfWaktu, DfGalat = dfGalat,
                SubjekDibuang = dibuang,
                Tingkat = tingkat,
                RerataWaktu = rerataWaktu.ToList(),
                SdWaktu = Enumerable.Range(0, k).Select(j =>
                {
                    double s = 0;
                    for (int i = 0; i < n; i++)
                    {
                        double d = Y[i][j] - rerataWaktu[j];
                        s += d * d;
                    }
                    return Math.Sqrt(s / (n - 1));
                }).ToList(),
                SsTotal = ssTotal, SsSubjek = ssSubjek, SsDalam = ssDalam,
                SsWaktu = ssWaktu, SsGalat = ssGalat,
                MsWaktu = msWaktu, MsGalat = msGalat, F = f,
                P = Distributions.FUpper(f, dfWaktu, dfGalat),
                Eta2Parsial = ssWaktu + ssGalat > 0 ? ssWaktu / (ssWaktu + ssGalat) : double.NaN,
                Kovarians = S,
                EpsBawah = 1.0 / (k - 1),
            };

            var C = BasisKontras(k);
            if (C is null) return null;

            var M = Aljabar.Kali(Aljabar.Kali(C, S), Aljabar.Transpose(C));
            
            for (int a = 0; a < k - 1; a++)
                for (int b = a + 1; b < k - 1; b++)
                {
                    double r = (M[a, b] + M[b, a]) / 2.0;
                    M[a, b] = r; M[b, a] = r;
                }

            var eig = Aljabar.EigenSimetris(M);
            hasil.AkarCiri = eig.Nilai.ToArray();

            
            double jejak = 0, jejak2 = 0, hasilKali = 1;
            foreach (double l in eig.Nilai)
            {
                jejak += l;
                jejak2 += l * l;
                hasilKali *= l;
            }
            if (jejak2 <= 0) return null;

            double epsGg = jejak * jejak / ((k - 1) * jejak2);
            hasil.EpsGG = Math.Min(1.0, Math.Max(hasil.EpsBawah, epsGg));

            double pembilangHf = n * (k - 1) * hasil.EpsGG - 2;
            double penyebutHf = (k - 1) * (n - 1 - (k - 1) * hasil.EpsGG);
            hasil.EpsHF = penyebutHf > 0
                ? Math.Min(1.0, pembilangHf / penyebutHf)
                : 1.0;

            
            if (hasil.MauchlyAda && jejak > 0)
            {
                double rata = jejak / (k - 1);
                hasil.MauchlyW = hasilKali / Math.Pow(rata, k - 1);
                double faktor = (n - 1) - (2.0 * k * k - k + 2) / (6.0 * (k - 1));
                hasil.MauchlyChi2 = -faktor * Math.Log(hasil.MauchlyW);
                hasil.MauchlyDf = k * (k - 1) / 2 - 1;
                hasil.MauchlyP = hasil.MauchlyDf > 0
                    ? Distributions.ChiSquareUpper(hasil.MauchlyChi2, hasil.MauchlyDf)
                    : double.NaN;
            }

            
            hasil.PGG = Distributions.FUpper(f, hasil.EpsGG * dfWaktu, hasil.EpsGG * dfGalat);
            hasil.PHF = Distributions.FUpper(f, hasil.EpsHF * dfWaktu, hasil.EpsHF * dfGalat);
            hasil.PBawah = Distributions.FUpper(f, hasil.EpsBawah * dfWaktu, hasil.EpsBawah * dfGalat);

            return hasil;
        }

        private static double[,]? BasisKontras(int k)
        {
            var konstan = new double[k];
            for (int j = 0; j < k; j++) konstan[j] = 1.0 / Math.Sqrt(k);

            var baris = new List<double[]>();
            for (int i = 0; i < k - 1; i++)
            {
                var v = new double[k];
                for (int j = 0; j <= i; j++) v[j] = 1.0;
                v[i + 1] = -(i + 1);

                double dk = 0;
                for (int j = 0; j < k; j++) dk += v[j] * konstan[j];
                for (int j = 0; j < k; j++) v[j] -= dk * konstan[j];

                foreach (var b in baris)
                {
                    double d = 0;
                    for (int j = 0; j < k; j++) d += v[j] * b[j];
                    for (int j = 0; j < k; j++) v[j] -= d * b[j];
                }

                double norma = 0;
                for (int j = 0; j < k; j++) norma += v[j] * v[j];
                norma = Math.Sqrt(norma);
                if (norma < 1e-12) return null;
                for (int j = 0; j < k; j++) v[j] /= norma;
                baris.Add(v);
            }

            var c = new double[k - 1, k];
            for (int i = 0; i < k - 1; i++)
                for (int j = 0; j < k; j++) c[i, j] = baris[i][j];
            return c;
        }

        

        public static List<ResultBlock> AnovaBerulangBlocks(Dataset ds, string dependen,
                                                            string subjek, string faktor,
                                                            double alpha = 0.05)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("ANOVA berulang (repeated measures)", 1)
            };

            if (string.Equals(subjek, faktor, StringComparison.Ordinal))
            {
                blocks.Add(Blocks.Note(
                    "Variabel subjek dan variabel waktu tidak boleh sama. Variabel "
                    + "subjek menandai ORANG yang diukur, variabel waktu menandai "
                    + "PENGUKURAN ke berapa.", NoteKind.Error));
                return blocks;
            }

            var perlu = new[] { dependen, subjek, faktor };
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < 8)
            {
                blocks.Add(Blocks.Note(
                    $"Baris lengkap hanya {baris.Count}. ANOVA berulang butuh minimal "
                    + "beberapa subjek yang masing-masing diukur lebih dari sekali.",
                    NoteKind.Error));
                return blocks;
            }

            var semuaY = ds.Numeric(dependen);
            var semuaS = ds.Text(subjek);
            var semuaW = ds.Text(faktor);

            var nilai = new double[baris.Count];
            var sub = new string[baris.Count];
            var wkt = new string[baris.Count];
            for (int i = 0; i < baris.Count; i++)
            {
                nilai[i] = semuaY[baris[i]] ?? double.NaN;
                sub[i] = semuaS[baris[i]] ?? "(kosong)";
                wkt[i] = semuaW[baris[i]] ?? "(kosong)";
            }

            var tingkat = wkt.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (tingkat.Count < 2)
            {
                blocks.Add(Blocks.Note(
                    $"Variabel '{faktor}' hanya punya {tingkat.Count} tingkat. ANOVA "
                    + "berulang membutuhkan minimal dua pengukuran per subjek.",
                    NoteKind.Error));
                return blocks;
            }

            var h = Hitung(nilai, sub, wkt);
            if (h is null)
            {
                blocks.Add(Blocks.Note(
                    "ANOVA berulang tidak bisa dihitung. Penyebab yang paling sering: "
                    + "(a) ada subjek yang tidak diukur lengkap di semua tingkat waktu, "
                    + "sehingga tinggal kurang dari 4 subjek; (b) ada pasangan "
                    + "subjek–waktu yang muncul lebih dari sekali (data ganda); atau "
                    + "(c) nilai terikatnya sama persis di dalam satu subjek, sehingga "
                    + "ragamnya nol. Periksa ketiganya.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.AnovaBerulangUji, DaftarRumus.Sferisitas));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Variabel terikat", dependen),
                ("Variabel subjek", subjek),
                ("Variabel waktu", faktor),
                ("Banyak subjek lengkap", $"n = {Fmt.Int(h.N)}"),
                ("Banyak tingkat waktu", $"k = {Fmt.Int(h.K)}"),
                ("Derajat bebas", $"waktu = {Fmt.Int(h.DfWaktu)}, "
                                  + $"galat = {Fmt.Int(h.DfGalat)} "
                                  + $"= (n−1)(k−1) = {Fmt.Int(h.N - 1)}×{Fmt.Int(h.K - 1)}")));

            blocks.Add(Blocks.Table(
                "Uji efek waktu (dalam subjek)",
                new[] { "Sumber", "Jumlah kuadrat", "df", "Kuadrat tengah", "F", "p" },
                new[]
                {
                    new[] { faktor, Fmt.Num(h.SsWaktu, 6), Fmt.Int(h.DfWaktu),
                            Fmt.Num(h.MsWaktu, 6), Fmt.Num(h.F, 4), Fmt.P(h.P) },
                    new[] { "Galat (waktu × subjek)", Fmt.Num(h.SsGalat, 6),
                            Fmt.Int(h.DfGalat), Fmt.Num(h.MsGalat, 6), Fmt.NA, Fmt.NA },
                },
                "Galatnya adalah interaksi waktu × subjek, dan derajat bebasnya "
                + "(n−1)(k−1) — BUKAN nk−k. Ini kekeliruan klasik pada uji ini: "
                + "memakai galat antar-kelompok membuat F terlalu kecil, memakai "
                + "galat total membuat F terlalu besar."));

            blocks.Add(Blocks.Table(
                "Penguraian jumlah kuadrat",
                new[] { "Sumber", "Jumlah kuadrat", "df" },
                new[]
                {
                    new[] { "Antar subjek", Fmt.Num(h.SsSubjek, 6), Fmt.Int(h.DfSubjek) },
                    new[] { "  " + faktor + " (dalam subjek)", Fmt.Num(h.SsWaktu, 6),
                            Fmt.Int(h.DfWaktu) },
                    new[] { "  Galat (waktu × subjek)", Fmt.Num(h.SsGalat, 6),
                            Fmt.Int(h.DfGalat) },
                    new[] { "Total", Fmt.Num(h.SsTotal, 6),
                            Fmt.Int(h.DfSubjek + h.DfWaktu + h.DfGalat) },
                },
                "Perhatikan bahwa antar-subjek dan dalam-subjek menjumlah menjadi "
                + "total. Yang diuji hanyalah suku waktu; suku antar-subjek ada "
                + "justru supaya perbedaan antar orang TIDAK masuk ke galat."));

            blocks.Add(Blocks.Table(
                "Rerata tiap waktu",
                new[] { "Waktu", "Rerata", "Simpangan baku", "n" },
                Enumerable.Range(0, h.K).Select(j => new[]
                {
                    h.Tingkat[j], Fmt.Num(h.RerataWaktu[j], 4),
                    Fmt.Num(h.SdWaktu[j], 4), Fmt.Int(h.N),
                }).ToArray(),
                "Simpangan baku di sini adalah keragaman ANTAR SUBJEK pada waktu itu — "
                + "ia menggambarkan seberapa berbeda orang-orangnya, bukan seberapa "
                + "besar efek waktunya."));

            if (h.MauchlyAda)
            {
                blocks.Add(Blocks.Table(
                    "Uji Mauchly dan koreksi sferisitas",
                    new[] { "Besaran", "Nilai" },
                    new[]
                    {
                        new[] { "Mauchly W", Fmt.Num(h.MauchlyW, 6) },
                        new[] { "χ² (pendekatan)", Fmt.Num(h.MauchlyChi2, 6) },
                        new[] { "df", Fmt.Int(h.MauchlyDf) },
                        new[] { "p", Fmt.P(h.MauchlyP) },
                        new[] { "Epsilon Greenhouse-Geisser", Fmt.Num(h.EpsGG, 6) },
                        new[] { "Epsilon Huynh-Feldt", Fmt.Num(h.EpsHF, 6) },
                        new[] { "Epsilon batas bawah", Fmt.Num(h.EpsBawah, 6) },
                    },
                    "Sphericity berarti ragam semua selisih antar pasangan waktu sama. "
                    + "Bila p < 0,05 asumsinya dilanggar — dan pelanggaran itu membuat "
                    + "p terlalu KECIL, jadi kau bisa terlalu percaya diri. Bila itu "
                    + "terjadi, bacalah tabel p terkoreksi di bawah, bukan p tanpa "
                    + "koreksi."));
            }

            blocks.Add(Blocks.Table(
                "p tanpa koreksi dan p terkoreksi",
                new[] { "Koreksi", "Epsilon", "df1", "df2", "p" },
                new[]
                {
                    new[] { "Tidak dikoreksi", Fmt.NA, Fmt.Int(h.DfWaktu), Fmt.Int(h.DfGalat),
                            Fmt.P(h.P) },
                    new[] { "Greenhouse-Geisser", Fmt.Num(h.EpsGG, 6),
                            Fmt.Num(h.EpsGG * h.DfWaktu, 4), Fmt.Num(h.EpsGG * h.DfGalat, 4),
                            Fmt.P(h.PGG) },
                    new[] { "Huynh-Feldt", Fmt.Num(h.EpsHF, 6),
                            Fmt.Num(h.EpsHF * h.DfWaktu, 4), Fmt.Num(h.EpsHF * h.DfGalat, 4),
                            Fmt.P(h.PHF) },
                    new[] { "Batas bawah", Fmt.Num(h.EpsBawah, 6),
                            Fmt.Num(h.EpsBawah * h.DfWaktu, 4), Fmt.Num(h.EpsBawah * h.DfGalat, 4),
                            Fmt.P(h.PBawah) },
                },
                "Koreksinya bekerja dengan MENGALIKAN derajat bebas dengan epsilon, "
                + "sehingga p menjadi lebih besar. Greenhouse-Geisser paling "
                + "konservatif (paling aman), Huynh-Feldt lebih longgar dan dipakai "
                + "bila epsilon mendekati 1, batas bawah adalah yang paling "
                + "konservatif dari semuanya."));

            blocks.Add(Blocks.Table(
                "Ringkasan",
                new[] { "Besaran", "Nilai" },
                new[]
                {
                    new[] { "Eta² parsial", Fmt.Num(h.Eta2Parsial, 6) },
                    new[] { "Banyak subjek", Fmt.Int(h.N) },
                    new[] { "Banyak tingkat waktu", Fmt.Int(h.K) },
                    new[] { "Subjek dibuang (data tidak lengkap)", Fmt.Int(h.SubjekDibuang) },
                },
                "Eta² parsial = SS_waktu / (SS_waktu + SS_galat). Ia menjawab "
                + "\"berapa besar bagian ragam dalam-subjek yang dijelaskan waktu\", "
                + "bukan \"berapa besar bagian dari seluruh ragam\"."));

            if (h.SubjekDibuang > 0)
            {
                blocks.Add(Blocks.Note(
                    $"{Fmt.Int(h.SubjekDibuang)} subjek dibuang karena tidak diukur "
                    + "lengkap di semua tingkat waktu. Ini pembuangan menyeluruh "
                    + "(listwise): seluruh data subjek itu tidak dipakai, supaya "
                    + "tidak ada sel yang diisi taksiran. Periksa apakah yang dibuang "
                    + "berpola — bila subjek yang gugur justru yang paling parah, "
                    + "hasilnya bisa menyesatkan.", NoteKind.Warning));
            }

            if (h.MauchlyAda && h.MauchlyP < alpha)
            {
                blocks.Add(Blocks.Note(
                    $"Mauchly menolak (p = {Fmt.P(h.MauchlyP)}): asumsi sphericity "
                    + $"DILANGGAR. Pakai p terkoreksi — Greenhouse-Geisser "
                    + $"{Fmt.P(h.PGG)} atau Huynh-Feldt {Fmt.P(h.PHF)} — bukan "
                    + $"p tanpa koreksi {Fmt.P(h.P)}. Pada data ini koreksinya "
                    + "memperbesar p, jadi kesimpulan tanpa koreksi bisa terlalu "
                    + "percaya diri.", NoteKind.Warning));
            }

            if (!h.MauchlyAda)
            {
                blocks.Add(Blocks.Note(
                    "Dengan hanya dua tingkat waktu, sphericity selalu terpenuhi — "
                    + "hanya ada satu selisih, jadi tidak ada yang perlu disamakan. "
                    + "Karena itu Mauchly tidak dihitung dan koreksinya tidak "
                    + "diperlukan.", NoteKind.Info));
            }

            if (h.P < alpha && h.PGG >= alpha)
            {
                blocks.Add(Blocks.Note(
                    "Perhatikan: tanpa koreksi hasilnya nyata, tetapi SETELAH "
                    + "koreksi Greenhouse-Geisser tidak lagi nyata. Ini contoh "
                    + "persis mengapa koreksi itu penting — kesimpulannya berbeda "
                    + "tergantung koreksinya, dan itu harus dilaporkan, bukan "
                    + "disembunyikan dengan memilih salah satu.", NoteKind.Warning));
            }

            return blocks;
        }
    }
}
