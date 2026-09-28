using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public sealed class HasilSls2
    {

        public List<string> Nama = new();
        public double[] B = Array.Empty<double>();
        public double[] Se = Array.Empty<double>();
        public double[] T = Array.Empty<double>();
        public double[] P = Array.Empty<double>();
        public double[] Residual = Array.Empty<double>();
        public double[] Fitted = Array.Empty<double>();

        public int N, K, DfResid;
        public double Sse, Sigma2, R2, R2Adj;

        
        public List<string> NamaTahap1 = new();
        public double[] BTahap1 = Array.Empty<double>();
        public double[] SeTahap1 = Array.Empty<double>();

        public double[] VHat = Array.Empty<double>();
        public double R2Tahap1, R2Tahap1Adj, FTahap1, PTahap1;
        public int Df1Tahap1, Df2Tahap1;

        public List<double> R2Tahap1Semua = new();
        public List<double> FTahap1Semua = new();
        public List<double> PTahap1Semua = new();

        
        public double SarganJ = double.NaN, SarganP = double.NaN;
        public int SarganDf;
        public double HausmanT = double.NaN, HausmanP = double.NaN;
        public int HausmanDf;

        
        public double[] BOls = Array.Empty<double>();
        public double[] SeOls = Array.Empty<double>();

        public double JarakOls = double.NaN;

        public int JumlahEndogen, JumlahEksogen, JumlahInstrumen;
        public int KolomX, KolomZ;

        public int IdxEndogen;
    }

    public static class Sls2
    {
        

        public static HasilSls2? Hitung(double[] y, List<double[]> endogen, List<string> namaEndogen,
                                        List<double[]> eksogen, List<string> namaEksogen,
                                        List<double[]> instrumen, List<string> namaInstrumen)
        {
            int n = y.Length;
            int kEks = eksogen.Count, kEnd = endogen.Count, kIns = instrumen.Count;
            if (kEnd < 1 || kIns < 1) return null;

            
            
            if (kIns < kEnd) return null;

            int kx = 1 + kEks + kEnd;      
            int kz = 1 + kEks + kIns;      
            if (n < kz + 2) return null;
            if (endogen.Any(v => v.Length != n) || eksogen.Any(v => v.Length != n)
                || instrumen.Any(v => v.Length != n)) return null;

            
            var X = Susun(n, kx, eksogen, endogen);
            var Z = Susun(n, kz, eksogen, instrumen);

            var invZtZ = Regression.Inverse(Silang(Z, Z));
            if (invZtZ == null) return null;

            
            
            
            var ZtX = Silang(Z, X);                    
            var koefProyeksi = KaliMatriks(invZtZ, ZtX);   
            var Xhat = Kali(Z, koefProyeksi);          

            var invXhXh = Regression.Inverse(Silang(Xhat, Xhat));
            if (invXhXh == null) return null;

            var beta = KaliVektor(invXhXh, SilangVektor(Xhat, y));

            
            var resid = new double[n];
            var fitted = new double[n];
            double sse = 0, meanY = y.Average(), sst = 0;
            for (int i = 0; i < n; i++)
            {
                double pred = 0;
                for (int a = 0; a < kx; a++) pred += X[i][a] * beta[a];
                fitted[i] = pred;
                resid[i] = y[i] - pred;
                sse += resid[i] * resid[i];
                sst += (y[i] - meanY) * (y[i] - meanY);
            }

            int dfResid = n - kx;
            double sigma2 = sse / dfResid;
            double r2 = sst > 0 ? 1.0 - sse / sst : double.NaN;
            double r2Adj = 1.0 - (1.0 - r2) * (n - 1) / (double)dfResid;

            var se = new double[kx];
            var t = new double[kx];
            var pv = new double[kx];
            for (int a = 0; a < kx; a++)
            {
                se[a] = Math.Sqrt(Math.Max(0, sigma2 * invXhXh[a, a]));
                t[a] = se[a] > 0 ? beta[a] / se[a] : double.NaN;
                pv[a] = Distributions.StudentTTwoSided(t[a], dfResid);
            }

            var nama = new List<string> { "(Konstanta)" };
            nama.AddRange(namaEksogen);
            nama.AddRange(namaEndogen);

            var hasil = new HasilSls2
            {
                Nama = nama,
                B = beta, Se = se, T = t, P = pv,
                Residual = resid, Fitted = fitted,
                N = n, K = kx, DfResid = dfResid,
                Sse = sse, Sigma2 = sigma2, R2 = r2, R2Adj = r2Adj,
                JumlahEndogen = kEnd, JumlahEksogen = kEks, JumlahInstrumen = kIns,
                KolomX = kx, KolomZ = kz,
                IdxEndogen = 1 + kEks,
                NamaTahap1 = new List<string> { "(Konstanta)" }
            };
            hasil.NamaTahap1.AddRange(namaEksogen);
            hasil.NamaTahap1.AddRange(namaInstrumen);

            
            
            for (int j = 0; j < kEnd; j++)
            {
                var penuh = Regression.Fit(endogen[j], eksogen.Concat(instrumen).ToList());
                if (penuh == null) return null;

                var terbatas = Regression.Fit(endogen[j], eksogen.ToList());
                if (terbatas == null) return null;

                double ssePenuh = penuh.Sse;
                double sseTerbatas = terbatas.Sse;
                int df2 = penuh.DfRes;
                double f = ((sseTerbatas - ssePenuh) / kIns) / (ssePenuh / df2);

                hasil.R2Tahap1Semua.Add(penuh.R2);
                hasil.FTahap1Semua.Add(f);
                hasil.PTahap1Semua.Add(Distributions.FUpper(f, kIns, df2));

                if (j == 0)
                {
                    hasil.BTahap1 = penuh.Beta;
                    hasil.SeTahap1 = penuh.Se;
                    hasil.VHat = penuh.Residuals;
                    hasil.R2Tahap1 = penuh.R2;
                    hasil.R2Tahap1Adj = penuh.AdjR2;
                    hasil.FTahap1 = f;
                    hasil.PTahap1 = Distributions.FUpper(f, kIns, df2);
                    hasil.Df1Tahap1 = kIns;
                    hasil.Df2Tahap1 = df2;
                }
            }

            
            
            
            hasil.SarganDf = kz - kx;
            if (hasil.SarganDf > 0)
            {
                var ztu = SilangVektor(Z, resid);
                double uPzU = 0;
                for (int a = 0; a < kz; a++)
                {
                    double s = 0;
                    for (int b = 0; b < kz; b++) s += invZtZ[a, b] * ztu[b];
                    uPzU += ztu[a] * s;
                }
                double uu = resid.Sum(v => v * v);
                if (uu > 0) hasil.SarganJ = n * uPzU / uu;
                hasil.SarganP = Distributions.ChiSquareUpper(hasil.SarganJ, hasil.SarganDf);
            }

            
            
            var vhat = hasil.VHat;
            if (vhat.Length == n)
            {
                var predAug = eksogen.Concat(endogen).ToList();
                predAug.Add(vhat);
                var aug = Regression.Fit(y, predAug);
                if (aug != null)
                {
                    int last = aug.Beta.Length - 1;
                    hasil.HausmanT = aug.Se[last] > 0 ? aug.Beta[last] / aug.Se[last] : double.NaN;
                    hasil.HausmanP = Distributions.StudentTTwoSided(hasil.HausmanT, aug.DfRes);
                    hasil.HausmanDf = 1;
                }
            }

            
            var ols = Regression.Fit(y, eksogen.Concat(endogen).ToList());
            if (ols != null)
            {
                hasil.BOls = ols.Beta;
                hasil.SeOls = ols.Se;
                int idx = hasil.IdxEndogen;
                if (idx < ols.Beta.Length && se[idx] > 0)
                    hasil.JarakOls = Math.Abs(beta[idx] - ols.Beta[idx]) / se[idx];
            }

            return hasil;
        }

        

        public static List<ResultBlock> Sls2Blocks(Dataset ds, string dependen,
                                                   List<string> endogen, List<string> eksogen,
                                                   List<string> instrumen, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Regresi dua tahap (2SLS / variabel instrumental)", 1)
            };

            if (endogen.Count == 0)
            {
                blocks.Add(Blocks.Note("Pilih sedikitnya satu variabel endogen.", NoteKind.Error));
                return blocks;
            }
            if (instrumen.Count == 0)
            {
                blocks.Add(Blocks.Note(
                    "Pilih sedikitnya satu instrumen. Tanpa instrumen, modelnya tidak "
                    + "teridentifikasi — 2SLS tidak bisa dijalankan.", NoteKind.Error));
                return blocks;
            }
            if (instrumen.Count < endogen.Count)
            {
                blocks.Add(Blocks.Note(
                    $"Instrumen yang dikecualikan ada {instrumen.Count}, sedangkan variabel "
                    + $"endogen ada {endogen.Count}. Syarat identifikasi (order condition) "
                    + "menuntut jumlah instrumen sedikitnya sama banyak; kalau tidak, "
                    + "modelnya kurang teridentifikasi.", NoteKind.Error));
                return blocks;
            }

            var perlu = new List<string> { dependen };
            perlu.AddRange(endogen);
            perlu.AddRange(eksogen);
            perlu.AddRange(instrumen);

            var baris = ds.CompleteRows(perlu);
            int kolomZ = 1 + eksogen.Count + instrumen.Count;
            if (baris.Count < kolomZ + 3)
            {
                blocks.Add(Blocks.Note(
                    $"Baris lengkap hanya {baris.Count}, sedangkan modelnya butuh sedikitnya "
                    + $"{kolomZ + 3} (ada {kolomZ} kolom instrumen). Tambah data atau "
                    + "kurangi variabelnya.", NoteKind.Error));
                return blocks;
            }

            double[] Ambil(string v)
            {
                var semua = ds.Numeric(v);
                var a = new double[baris.Count];
                for (int i = 0; i < baris.Count; i++) a[i] = semua[baris[i]] ?? double.NaN;
                return a;
            }

            var y = Ambil(dependen);
            var kolEnd = endogen.Select(Ambil).ToList();
            var kolEks = eksogen.Select(Ambil).ToList();
            var kolIns = instrumen.Select(Ambil).ToList();

            var h = Hitung(y, kolEnd, endogen, kolEks, eksogen, kolIns, instrumen);
            if (h is null)
            {
                blocks.Add(Blocks.Note(
                    "Model tidak bisa ditaksir. Periksa apakah ada kolom instrumen yang "
                    + "merupakan gabungan linear kolom lain (misalnya konstanta ganda), "
                    + "atau apakah jumlah instrumennya memang cukup.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.Sls2, DaftarRumus.Sls2GalatBaku,
                DaftarRumus.Sls2TahapPertama, DaftarRumus.Sargan, DaftarRumus.HausmanEndogen));

            double tCrit = Distributions.StudentTInv(1 - alpha / 2.0, h.DfResid);

            
            var sukuStruktur = new List<string> { Fmt.Num(h.B[0], 3) };
            for (int j = 1; j < h.Nama.Count; j++)
                sukuStruktur.Add($"{Fmt.Num(h.B[j], 3)}·{h.Nama[j]}");

            var langkah = new List<LangkahHitung>
            {
                new()
                {
                    Uraian = "Variabel terikat",
                    Hitungan = dependen
                },
                new()
                {
                    Uraian = "Variabel endogen (diramalkan di tahap 1)",
                    Hitungan = string.Join(", ", endogen)
                },
                new()
                {
                    Uraian = "Variabel eksogen (ikut di kedua tahap)",
                    Hitungan = eksogen.Count > 0 ? string.Join(", ", eksogen) : "(tidak ada)"
                },
                new()
                {
                    Uraian = "Instrumen (hanya di tahap 1)",
                    Hitungan = string.Join(", ", instrumen)
                },
                new()
                {
                    Uraian = "Banyak amatan & kolom",
                    Hitungan = $"n = {Fmt.Int(h.N)}   kolom X = {Fmt.Int(h.KolomX)}   "
                               + $"kolom Z = {Fmt.Int(h.KolomZ)}"
                },
                new()
                {
                    Uraian = "Syarat identifikasi",
                    Hitungan = $"instrumen {Fmt.Int(h.JumlahInstrumen)} ≥ endogen "
                               + $"{Fmt.Int(h.JumlahEndogen)} → "
                               + (h.SarganDf > 0
                                    ? $"kelebihan identifikasi {Fmt.Int(h.SarganDf)} (bisa diuji Sargan)"
                                    : "pas-identifikasi (Sargan tak terdefinisi)")
                },
                new()
                {
                    Uraian = "Tahap 1 — ramalan " + endogen[0],
                    Hitungan = string.Join("  +  ",
                        Enumerable.Range(0, h.NamaTahap1.Count).Select(j =>
                            $"{Fmt.Num(h.BTahap1[j], 3)}·{h.NamaTahap1[j]}"))
                },
                new()
                {
                    Uraian = "Tahap 2 — persamaan struktural",
                    Hitungan = $"{dependen} = {string.Join("  +  ", sukuStruktur)}"
                },
                new()
                {
                    Uraian = "Ragam residual",
                    Hitungan = $"σ² = SSE/(n−k) = {Fmt.Num(h.Sse, 3)} / {Fmt.Int(h.DfResid)}"
                               + $" = {Fmt.Num(h.Sigma2, 3)}"
                }
            };
            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data", langkah));

            
            var barisTahap1 = h.NamaTahap1.Select((nm, j) => new[]
            {
                nm, Fmt.Num(h.BTahap1[j], 6), Fmt.Num(h.SeTahap1[j], 6),
                Fmt.Num(h.BTahap1[j] / h.SeTahap1[j], 4),
                Fmt.P(Distributions.StudentTTwoSided(
                    h.BTahap1[j] / h.SeTahap1[j], h.Df2Tahap1))
            }).ToArray();

            blocks.Add(Blocks.Table(
                $"Tahap 1 — {endogen[0]} diramalkan dari seluruh instrumen",
                new[] { "Suku", "B", "Galat baku", "t", "p" }, barisTahap1,
                $"R² = {Fmt.Num(h.R2Tahap1, 4)}   R² disesuaikan = {Fmt.Num(h.R2Tahap1Adj, 4)}   "
                + $"F instrumen yang dikecualikan = {Fmt.Num(h.FTahap1, 4)} "
                + $"(df {Fmt.Int(h.Df1Tahap1)}; {Fmt.Int(h.Df2Tahap1)}, p = {Fmt.P(h.PTahap1)}). "
                + "Instrumen yang lemah membuat 2SLS berbias ke arah OLS; patokan kasar F > 10."));

            if (h.JumlahEndogen > 1)
            {
                blocks.Add(Blocks.Table(
                    "Tahap 1 — ringkasan tiap variabel endogen",
                    new[] { "Variabel endogen", "R²", "F instrumen", "p" },
                    endogen.Select((nm, j) => new[]
                    {
                        nm, Fmt.Num(h.R2Tahap1Semua[j], 4),
                        Fmt.Num(h.FTahap1Semua[j], 4), Fmt.P(h.PTahap1Semua[j])
                    }).ToArray(),
                    "Tabel rincian di atas hanya untuk variabel endogen pertama."));
            }

            
            blocks.Add(Blocks.Table(
                "Koefisien 2SLS",
                new[] { "Suku", "B", "Galat baku", "t", "p",
                        $"Selang kepercayaan {Fmt.Int((int)Math.Round(100 * (1 - alpha)))}%" },
                h.Nama.Select((nm, j) => new[]
                {
                    nm, Fmt.Num(h.B[j], 6), Fmt.Num(h.Se[j], 6),
                    Fmt.Num(h.T[j], 4), Fmt.P(h.P[j]),
                    $"[{Fmt.Num(h.B[j] - tCrit * h.Se[j], 6)}; "
                    + $"{Fmt.Num(h.B[j] + tCrit * h.Se[j], 6)}]"
                }).ToArray(),
                "Galat baku memakai σ² dari residual SESUNGGUHNYA (x asli), "
                + $"dengan derajat bebas {Fmt.Int(h.DfResid)}. Angka ini bisa dibandingkan "
                + "langsung dengan OLS pada tabel berikut."));

            blocks.Add(Blocks.Table(
                "Ringkasan model",
                new[] { "Besaran", "Nilai" },
                new[]
                {
                    new[] { "R²", Fmt.Num(h.R2, 6) },
                    new[] { "R² disesuaikan", Fmt.Num(h.R2Adj, 6) },
                    new[] { "σ² (ragam residual)", Fmt.Num(h.Sigma2, 6) },
                    new[] { "Jumlah kuadrat residual", Fmt.Num(h.Sse, 6) },
                    new[] { "Banyak amatan", Fmt.Int(h.N) },
                    new[] { "Derajat bebas residual", Fmt.Int(h.DfResid) },
                },
                "R² di sini mengukur kecocokan persamaan struktural, bukan kualitas "
                + "instrumen — model dengan R² tinggi tetap bisa memakai instrumen yang lemah."));

            
            if (h.BOls.Length == h.B.Length)
            {
                var barisBanding = new List<string[]>();
                for (int j = 0; j < h.B.Length; j++)
                {
                    bool endogenIni = j == h.IdxEndogen;
                    barisBanding.Add(new[]
                    {
                        h.Nama[j],
                        Fmt.Num(h.BOls[j], 6),
                        h.SeOls.Length == h.B.Length ? Fmt.Num(h.SeOls[j], 6) : Fmt.NA,
                        Fmt.Num(h.B[j], 6),
                        Fmt.Num(h.Se[j], 6),
                        Fmt.Num(h.B[j] - h.BOls[j], 6),
                        endogenIni && h.Se[j] > 0
                            ? $"{Fmt.Num(Math.Abs(h.B[j] - h.BOls[j]) / h.Se[j], 2)} galat baku"
                            : "—"
                    });
                }

                blocks.Add(Blocks.Table(
                    "2SLS dibanding OLS pada data yang sama",
                    new[] { "Suku", "OLS B", "OLS galat baku", "2SLS B", "2SLS galat baku",
                            "Selisih B", "Selisih (galat baku 2SLS)" },
                    barisBanding,
                    "Perbedaan kolom B inilah inti persoalannya: OLS berbias karena "
                    + "variabel endogen berkorelasi dengan galat, sedangkan 2SLS tidak. "
                    + "Tetapi perhatikan juga galat bakunya — galat baku 2SLS biasanya "
                    + "LEBIH BESAR, dan itu memang harganya: 2SLS membuang sebagian "
                    + "informasi demi menghilangkan bias. Kalau kedua kolom B hampir "
                    + "sama persis, variabelnya kemungkinan besar sebenarnya tidak "
                    + "endogen, dan OLS sudah cukup (sekaligus lebih teliti)."));
            }

            
            var barisDiag = new List<string[]>
            {
                new[]
                {
                    "Sargan (kelebihan identifikasi)",
                    h.SarganDf > 0 ? Fmt.Num(h.SarganJ, 6) : Fmt.NA,
                    Fmt.Int(h.SarganDf),
                    h.SarganDf > 0 ? Fmt.P(h.SarganP) : Fmt.NA,
                    h.SarganDf > 0
                        ? "H0: instrumen sah (tidak berkorelasi dengan galat)."
                        : "Pas-identifikasi — tidak ada yang bisa diuji."
                },
                new[]
                {
                    "Hausman bentuk regresi (endogenitas)",
                    Fmt.Num(h.HausmanT, 6),
                    Fmt.Int(h.HausmanDf),
                    Fmt.P(h.HausmanP),
                    "H0: variabel endogen sebenarnya EKSOGEN. Menolak berarti 2SLS memang diperlukan."
                },
                new[]
                {
                    "F instrumen yang dikecualikan",
                    Fmt.Num(h.FTahap1, 6),
                    $"{Fmt.Int(h.Df1Tahap1)}; {Fmt.Int(h.Df2Tahap1)}",
                    Fmt.P(h.PTahap1),
                    "Kekuatan instrumen. Di bawah 10 patut dicurigai lemah."
                }
            };

            blocks.Add(Blocks.Table(
                "Diagnostik",
                new[] { "Pemeriksaan", "Nilai", "df", "p", "Yang diuji" }, barisDiag,
                "Tiga pemeriksaan dengan tiga pertanyaan berbeda: apakah instrumennya sah, "
                + "apakah regresornya memang endogen, dan apakah instrumennya cukup kuat."));

            
            if (h.SarganDf > 0 && !double.IsNaN(h.SarganP) && h.SarganP < 0.05)
                blocks.Add(Blocks.Note(
                    $"Uji Sargan menolak (p = {Fmt.P(h.SarganP)}). Artinya ada instrumen yang "
                    + "tampaknya tidak sah — ia memengaruhi variabel terikat lewat jalur lain "
                    + "selain variabel endogen. Hasil 2SLS di atas karena itu patut dicurigai.",
                    NoteKind.Warning));

            if (h.FTahap1 < 10)
                blocks.Add(Blocks.Note(
                    $"F instrumen yang dikecualikan hanya {Fmt.Num(h.FTahap1, 2)} (di bawah 10). "
                    + "Instrumen yang lemah membuat 2SLS berbias ke arah OLS dan galat bakunya "
                    + "terlalu sempit — hasilnya bisa lebih buruk daripada OLS biasa.",
                    NoteKind.Warning));

            if (!double.IsNaN(h.HausmanP) && h.HausmanP >= 0.05)
                blocks.Add(Blocks.Note(
                    "Uji Hausman tidak menolak: tidak ada bukti bahwa variabel endogen itu "
                    + "benar-benar endogen. OLS lebih efisien dalam keadaan seperti ini, jadi "
                    + "pertimbangkan memakai regresi linear biasa.", NoteKind.Info));

            if (h.JarakOls < 2 && !double.IsNaN(h.JarakOls))
                blocks.Add(Blocks.Note(
                    $"Koefisien variabel endogen hanya berbeda {Fmt.Num(h.JarakOls, 2)} galat baku "
                    + "antara OLS dan 2SLS. Perbedaan sekecil itu bisa saja kebetulan pada "
                    + "sampel ini, bukan bukti bahwa endogenitasnya tidak ada.", NoteKind.Warning));

            blocks.Add(Blocks.Note(
                "2SLS hanya sebaik instrumennya, dan kesahihan instrumen tidak bisa "
                + "dibuktikan dari data — ia harus beralasan dari luar data. Uji Sargan "
                + "hanya bisa memeriksa bila instrumennya lebih banyak daripada variabel "
                + "endogen, dan ia mengandaikan sedikitnya satu instrumen sudah pasti sah.",
                NoteKind.Info));

            return blocks;
        }

        

        private static double[][] Susun(int n, int lebar, List<double[]> a, List<double[]> b)
        {
            var m = new double[n][];
            for (int i = 0; i < n; i++)
            {
                m[i] = new double[lebar];
                m[i][0] = 1.0;
                int c = 1;
                for (int j = 0; j < a.Count; j++) m[i][c++] = a[j][i];
                for (int j = 0; j < b.Count; j++) m[i][c++] = b[j][i];
            }
            return m;
        }

        private static double[,] Silang(double[][] a, double[][] b)
        {
            int n = a.Length, pa = a[0].Length, pb = b[0].Length;
            var m = new double[pa, pb];
            for (int i = 0; i < n; i++)
            {
                var ai = a[i];
                var bi = b[i];
                for (int r = 0; r < pa; r++)
                {
                    double v = ai[r];
                    if (v == 0) continue;
                    for (int c = 0; c < pb; c++) m[r, c] += v * bi[c];
                }
            }
            return m;
        }

        private static double[] SilangVektor(double[][] a, double[] y)
        {
            int n = a.Length, p = a[0].Length;
            var h = new double[p];
            for (int i = 0; i < n; i++)
            {
                var ai = a[i];
                for (int r = 0; r < p; r++) h[r] += ai[r] * y[i];
            }
            return h;
        }

        private static double[][] Kali(double[][] a, double[,] b)
        {
            int n = a.Length, p = a[0].Length, q = b.GetLength(1);
            var h = new double[n][];
            for (int i = 0; i < n; i++)
            {
                h[i] = new double[q];
                var ai = a[i];
                for (int r = 0; r < p; r++)
                {
                    double v = ai[r];
                    if (v == 0) continue;
                    for (int c = 0; c < q; c++) h[i][c] += v * b[r, c];
                }
            }
            return h;
        }

        private static double[,] KaliMatriks(double[,] m, double[,] b)
        {
            int p = m.GetLength(0), q = m.GetLength(1), r = b.GetLength(1);
            var h = new double[p, r];
            for (int i = 0; i < p; i++)
                for (int t = 0; t < q; t++)
                {
                    double v = m[i, t];
                    if (v == 0) continue;
                    for (int c = 0; c < r; c++) h[i, c] += v * b[t, c];
                }
            return h;
        }

        private static double[] KaliVektor(double[,] m, double[] v)
        {
            int p = m.GetLength(0), q = m.GetLength(1);
            var h = new double[p];
            for (int i = 0; i < p; i++)
            {
                double s = 0;
                for (int j = 0; j < q; j++) s += m[i, j] * v[j];
                h[i] = s;
            }
            return h;
        }
    }
}
