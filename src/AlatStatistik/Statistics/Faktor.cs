using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Faktor
    {
        public class HasilFaktor
        {
            public int N;                       
            public int P;                       
            public List<string> Variabel = new();

            public double[,] R = new double[0, 0];          
            public double[] EigenSemua = Array.Empty<double>();
            public double EigenTerpakaiJumlah;

            public double[,] Muatan = new double[0, 0];     
            public double[,] MuatanMentah = new double[0, 0]; 
            public double[] Komunalitas = Array.Empty<double>();
            public double[] KomunalitasMentah = Array.Empty<double>();
            public double[] RagamFaktor = Array.Empty<double>();   
            public double[] Persen = Array.Empty<double>();        
            public double[] Kumulatif = Array.Empty<double>();

            public double Kmo;
            public double[] KmoPerVariabel = Array.Empty<double>();
            public double BartlettChi2;
            public int BartlettDf;
            public double BartlettP;

            public string Metode = "pca";       
            public string Rotasi = "none";      
            public int JumlahFaktor;

            public double KriteriaAwal;         
            public double KriteriaAkhir;        
            public double[,] T = new double[0, 0];   
            public int Iterasi;                 
            public bool Konvergen;

            public double[,] Skor = new double[0, 0];   
        }

        

        internal static double[,]? MatriksTerstandar(Dataset ds, List<string> vars, out List<int> baris)
        {
            baris = new List<int>();
            if (ds is null || vars is null || vars.Count < 3) return null;

            var idx = vars.Select(v => ds.IndexOf(v)).ToList();
            if (idx.Any(i => i < 0)) return null;

            baris = ds.CompleteRows(vars);
            int n = baris.Count, p = vars.Count;
            if (n < 3) return null;

            var x = new double[n, p];
            for (int r = 0; r < n; r++)
                for (int c = 0; c < p; c++)
                {
                    double? v = Dataset.ToDouble(ds.Rows[baris[r]][idx[c]]);
                    if (v is null) return null;      
                    x[r, c] = v.Value;
                }

            var z = new double[n, p];
            for (int c = 0; c < p; c++)
            {
                double jumlah = 0;
                for (int r = 0; r < n; r++) jumlah += x[r, c];
                double rerata = jumlah / n;

                double jk = 0;
                for (int r = 0; r < n; r++) { double d = x[r, c] - rerata; jk += d * d; }
                double s = Math.Sqrt(jk / (n - 1));
                if (!(s > 1e-12)) return null;       

                for (int r = 0; r < n; r++) z[r, c] = (x[r, c] - rerata) / s;
            }
            return z;
        }

        internal static double[,] MatriksKorelasi(double[,] z)
        {
            int n = z.GetLength(0), p = z.GetLength(1);
            var r = new double[p, p];
            for (int a = 0; a < p; a++)
            {
                for (int b = a; b < p; b++)
                {
                    double s = 0;
                    for (int i = 0; i < n; i++) s += z[i, a] * z[i, b];
                    double nilai = s / (n - 1);
                    r[a, b] = nilai;
                    r[b, a] = nilai;
                }
            }
            
            for (int a = 0; a < p; a++) r[a, a] = 1.0;
            return r;
        }

        public static double KriteriaVarimaks(double[,] muatan)
        {
            int p = muatan.GetLength(0), m = muatan.GetLength(1);
            double q = 0;
            for (int f = 0; f < m; f++)
            {
                double jml2 = 0, jml4 = 0;
                for (int i = 0; i < p; i++)
                {
                    double l = muatan[i, f];
                    jml2 += l * l;
                    jml4 += l * l * l * l;
                }
                q += p * jml4 - jml2 * jml2;
            }
            return q / (p * p);
        }

        public static (double[,] Muatan, double[,] T, int Putaran) Varimaks(double[,] muatan,
                                                                          int putaranMaks = 500,
                                                                          double toleransi = 1e-10)
        {
            int p = muatan.GetLength(0), m = muatan.GetLength(1);
            var l = (double[,])muatan.Clone();
            var t = new double[m, m];
            for (int i = 0; i < m; i++) t[i, i] = 1.0;

            int putaran = 0;
            for (putaran = 1; putaran <= putaranMaks; putaran++)
            {
                double maksSudut = 0;
                for (int r = 0; r < m - 1; r++)
                {
                    for (int s = r + 1; s < m; s++)
                    {
                        double A = 0, B = 0, C = 0, D = 0;
                        for (int i = 0; i < p; i++)
                        {
                            double x = l[i, r], y = l[i, s];
                            double u = x * x - y * y;
                            double v = 2.0 * x * y;
                            A += u;
                            B += v;
                            C += u * u - v * v;
                            D += 2.0 * u * v;
                        }

                        double atas = D - 2.0 * A * B / p;
                        double bawah = C - (A * A - B * B) / p;
                        if (Math.Abs(atas) < 1e-18 && Math.Abs(bawah) < 1e-18) continue;

                        double phi = 0.25 * Math.Atan2(atas, bawah);
                        if (Math.Abs(phi) > maksSudut) maksSudut = Math.Abs(phi);

                        double c = Math.Cos(phi), sn = Math.Sin(phi);
                        for (int i = 0; i < p; i++)
                        {
                            double lr = l[i, r], ls = l[i, s];
                            l[i, r] = c * lr + sn * ls;
                            l[i, s] = -sn * lr + c * ls;
                        }
                        for (int i = 0; i < m; i++)
                        {
                            double tr = t[i, r], ts = t[i, s];
                            t[i, r] = c * tr + sn * ts;
                            t[i, s] = -sn * tr + c * ts;
                        }
                    }
                }
                if (maksSudut < toleransi) break;
            }
            return (l, t, Math.Min(putaran, putaranMaks));
        }

        public static HasilFaktor? Hitung(Dataset ds, List<string> vars, string metode = "pca",
                                          string rotasi = "varimax", int jumlahFaktor = 0,
                                          bool denganSkor = true)
        {
            var z = MatriksTerstandar(ds, vars, out var baris);
            if (z is null) return null;

            int n = z.GetLength(0), p = z.GetLength(1);
            var R = MatriksKorelasi(z);

            var hasil = new HasilFaktor
            {
                N = n, P = p, Variabel = vars.ToList(), R = R,
                Metode = (metode ?? "pca").Trim().ToLowerInvariant(),
                Rotasi = (rotasi ?? "none").Trim().ToLowerInvariant()
            };

            
            var Rinv = Regression.Inverse(R);
            if (Rinv is null) return null;

            var q = new double[p, p];                 
            for (int i = 0; i < p; i++)
                for (int j = 0; j < p; j++)
                {
                    if (i == j) { q[i, j] = 0; continue; }
                    double pembagi = Math.Sqrt(Math.Abs(Rinv[i, i] * Rinv[j, j]));
                    q[i, j] = pembagi > 1e-12 ? -Rinv[i, j] / pembagi : 0;
                }

            double jmlR2 = 0, jmlQ2 = 0;
            for (int i = 0; i < p; i++)
                for (int j = 0; j < p; j++)
                {
                    if (i == j) continue;
                    jmlR2 += R[i, j] * R[i, j];
                    jmlQ2 += q[i, j] * q[i, j];
                }
            hasil.Kmo = jmlR2 + jmlQ2 > 0 ? jmlR2 / (jmlR2 + jmlQ2) : double.NaN;

            var kmoVar = new double[p];
            for (int i = 0; i < p; i++)
            {
                double r2 = 0, q2 = 0;
                for (int j = 0; j < p; j++)
                {
                    if (i == j) continue;
                    r2 += R[i, j] * R[i, j];
                    q2 += q[i, j] * q[i, j];
                }
                kmoVar[i] = r2 + q2 > 0 ? r2 / (r2 + q2) : double.NaN;
            }
            hasil.KmoPerVariabel = kmoVar;

            double detR = Aljabar.Determinan(R);
            if (!double.IsNaN(detR) && detR > 0)
            {
                hasil.BartlettChi2 = -(n - 1 - (2.0 * p + 5.0) / 6.0) * Math.Log(detR);
                hasil.BartlettDf = p * (p - 1) / 2;
                hasil.BartlettP = Distributions.ChiSquareUpper(hasil.BartlettChi2, hasil.BartlettDf);
            }
            else
            {
                hasil.BartlettChi2 = double.NaN;
                hasil.BartlettDf = p * (p - 1) / 2;
                hasil.BartlettP = double.NaN;
            }

            
            double[,] muatan;
            double[] eigenSemua;

            if (hasil.Metode == "paf")
            {
                
                var h = new double[p];
                for (int i = 0; i < p; i++)
                    h[i] = Rinv[i, i] > 1e-12 ? 1.0 - 1.0 / Rinv[i, i] : 0.9;
                for (int i = 0; i < p; i++) h[i] = Math.Clamp(h[i], 0.05, 0.99);

                eigenSemua = Array.Empty<double>();
                muatan = new double[p, 0];
                hasil.Konvergen = false;

                for (int iter = 1; iter <= 200; iter++)
                {
                    var Rred = (double[,])R.Clone();
                    for (int i = 0; i < p; i++) Rred[i, i] = h[i];

                    var eig = Aljabar.EigenSimetris(Rred);
                    if (iter == 1) eigenSemua = eig.Nilai;

                    int k = TentukanJumlahFaktor(eig.Nilai, jumlahFaktor, p);
                    var muat = new double[p, k];
                    for (int f = 0; f < k; f++)
                    {
                        double akar = eig.Nilai[f] > 0 ? Math.Sqrt(eig.Nilai[f]) : 0;
                        for (int i = 0; i < p; i++) muat[i, f] = eig.Vektor[i, f] * akar;
                    }

                    var hBaru = new double[p];
                    for (int i = 0; i < p; i++)
                    {
                        double s = 0;
                        for (int f = 0; f < k; f++) s += muat[i, f] * muat[i, f];
                        
                        
                        
                        
                        
                        
                        
                        
                        hBaru[i] = Math.Clamp(s, 0.05, 0.99);
                    }

                    double beda = 0;
                    for (int i = 0; i < p; i++) beda = Math.Max(beda, Math.Abs(hBaru[i] - h[i]));
                    h = hBaru;
                    muatan = muat;
                    hasil.Iterasi = iter;

                    if (beda < 1e-10) { hasil.Konvergen = true; break; }
                }
            }
            else
            {
                var eig = Aljabar.EigenSimetris(R);
                eigenSemua = eig.Nilai;
                int k = TentukanJumlahFaktor(eig.Nilai, jumlahFaktor, p);

                muatan = new double[p, k];
                for (int f = 0; f < k; f++)
                {
                    double akar = eig.Nilai[f] > 0 ? Math.Sqrt(eig.Nilai[f]) : 0;
                    for (int i = 0; i < p; i++) muatan[i, f] = eig.Vektor[i, f] * akar;
                }
                hasil.Konvergen = true;
            }

            hasil.EigenSemua = eigenSemua;
            hasil.MuatanMentah = (double[,])muatan.Clone();
            hasil.KomunalitasMentah = Komunalitas(muatan);
            hasil.KriteriaAwal = KriteriaVarimaks(muatan);
            hasil.JumlahFaktor = muatan.GetLength(1);
            if (hasil.JumlahFaktor == 0) return null;

            
            var t = Identitas(hasil.JumlahFaktor);
            if (hasil.Rotasi == "varimax" && hasil.JumlahFaktor >= 2)
            {
                var (rot, tRot, putaran) = Varimaks(muatan);
                muatan = rot;
                t = tRot;
                hasil.Iterasi = Math.Max(hasil.Iterasi, putaran);
            }
            hasil.Muatan = muatan;
            hasil.T = t;
            hasil.KriteriaAkhir = KriteriaVarimaks(muatan);
            hasil.Komunalitas = Komunalitas(muatan);

            
            var ragam = new double[hasil.JumlahFaktor];
            for (int f = 0; f < hasil.JumlahFaktor; f++)
            {
                double s = 0;
                for (int i = 0; i < p; i++) s += muatan[i, f] * muatan[i, f];
                ragam[f] = s;
            }
            hasil.RagamFaktor = ragam;
            hasil.Persen = ragam.Select(v => 100.0 * v / p).ToArray();

            var kum = new double[hasil.JumlahFaktor];
            double jalan = 0;
            for (int f = 0; f < hasil.JumlahFaktor; f++) { jalan += hasil.Persen[f]; kum[f] = jalan; }
            hasil.Kumulatif = kum;

            
            if (denganSkor)
            {
                double[,] bobot;
                if (hasil.Metode == "paf")
                {
                    
                    bobot = Aljabar.Kali(Rinv, muatan);
                }
                else
                {
                    
                    var eig = Aljabar.EigenSimetris(R);
                    var v = new double[p, hasil.JumlahFaktor];
                    for (int f = 0; f < hasil.JumlahFaktor; f++)
                        for (int i = 0; i < p; i++) v[i, f] = eig.Vektor[i, f];
                    bobot = Aljabar.Kali(v, t);
                }
                hasil.Skor = Skor(z, bobot);
            }

            return hasil;
        }

        internal static double[] Komunalitas(double[,] muatan)
        {
            int p = muatan.GetLength(0), m = muatan.GetLength(1);
            var h = new double[p];
            for (int i = 0; i < p; i++)
            {
                double s = 0;
                for (int f = 0; f < m; f++) s += muatan[i, f] * muatan[i, f];
                h[i] = s;
            }
            return h;
        }

        internal static double[,] Identitas(int m)
        {
            var t = new double[m, m];
            for (int i = 0; i < m; i++) t[i, i] = 1.0;
            return t;
        }

        internal static double[,] Skor(double[,] z, double[,] bobot)
        {
            int n = z.GetLength(0), m = bobot.GetLength(1);
            var s = new double[n, m];
            for (int i = 0; i < n; i++)
                for (int f = 0; f < m; f++)
                {
                    double jumlah = 0;
                    for (int c = 0; c < z.GetLength(1); c++) jumlah += z[i, c] * bobot[c, f];
                    s[i, f] = jumlah;
                }
            return s;
        }

        internal static int TentukanJumlahFaktor(double[] eigen, int diminta, int p)
        {
            if (diminta > 0) return Math.Clamp(diminta, 1, p);

            int k = eigen.Count(e => e > 1.0);
            if (k < 1) k = 1;
            return Math.Clamp(k, 1, p);
        }

        

        public static List<ResultBlock> FaktorBlocks(Dataset ds, List<string> vars, string metode,
                                                     string rotasi, int jumlahFaktor)
        {
            var blok = new List<ResultBlock>();
            string judul = metode == "paf" ? "Analisis faktor (sumbu utama)" : "Analisis komponen utama";
            blok.Add(Blocks.Heading($"{judul} — {string.Join(", ", vars)}", 1));

            var h = Hitung(ds, vars, metode, rotasi, jumlahFaktor);
            if (h is null)
            {
                blok.Add(Blocks.Note(
                    "Analisis faktor butuh sedikitnya 3 variabel angka dengan ragam lebih dari nol "
                    + "dan sedikitnya 3 baris yang lengkap. Periksa tingkat ukur variabelnya di tab Variabel.",
                    NoteKind.Error));
                return blok;
            }

            blok.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.MatriksKorelasi, DaftarRumus.UjiBartlett, DaftarRumus.Kmo,
                DaftarRumus.AnalisisFaktor, DaftarRumus.Varimaks));

            var langkah = new List<(string, string)>
            {
                ("Baris terpakai (listwise)", $"n = {h.N}"),
                ("Banyak variabel", $"p = {h.P}"),
                ("Determinan matriks korelasi", $"|R| = {Fmt.Num(Aljabar.Determinan(h.R), 6)}"),
                ("Statistik Bartlett",
                 $"χ² = −(n − 1 − (2p + 5)/6)·ln|R| = −({h.N} − 1 − {Fmt.Num((2.0 * h.P + 5.0) / 6.0, 4)})"
                 + $"·ln({Fmt.Num(Aljabar.Determinan(h.R), 6)}) = {Fmt.Num(h.BartlettChi2, 4)}"),
                ("Derajat bebas Bartlett", $"df = p(p − 1)/2 = {h.P}·{h.P - 1}/2 = {h.BartlettDf}"),
                ("KMO", $"Σr²/(Σr² + Σq²) = {Fmt.Num(h.Kmo, 4)}"),
                ("Nilai eigen terbesar", $"λ₁ = {Fmt.Num(h.EigenSemua.Length > 0 ? h.EigenSemua[0] : double.NaN, 4)}"),
                ("Jumlah nilai eigen = p", $"Σλ = {Fmt.Num(h.EigenSemua.Sum(), 4)} (p = {h.P})"),
                ("Faktor yang dipakai", h.JumlahFaktor.ToString()),
                ("Kriteria varimaks sebelum rotasi", Fmt.Num(h.KriteriaAwal, 6))
            };
            if (h.Rotasi == "varimax" && h.JumlahFaktor >= 2)
                langkah.Add(("Kriteria varimaks sesudah rotasi",
                             $"{Fmt.Num(h.KriteriaAkhir, 6)} (naik dari {Fmt.Num(h.KriteriaAwal, 6)})"));
            blok.Add(Blocks.Substitusi("Pemasukan nilai", langkah.ToArray()));

            
            string tafsirKmo = h.Kmo >= 0.9 ? "sangat baik" :
                               h.Kmo >= 0.8 ? "baik" :
                               h.Kmo >= 0.7 ? "sedang" :
                               h.Kmo >= 0.6 ? "cukup" :
                               h.Kmo >= 0.5 ? "kurang" : "tidak dapat diterima";
            blok.Add(Blocks.Table("KMO dan uji kebulatan Bartlett",
                new[] { "Ukuran", "Nilai" },
                new List<List<string>>
                {
                    new() { "KMO (kecukupan sampel)", Fmt.Num(h.Kmo, 4) },
                    new() { "Tafsir KMO", tafsirKmo },
                    new() { "χ² Bartlett", Fmt.Num(h.BartlettChi2, 4) },
                    new() { "df", h.BartlettDf.ToString() },
                    new() { "p Bartlett", Fmt.P(h.BartlettP) }
                },
                h.BartlettP < 0.05
                    ? $"Korelasi antar variabel cukup untuk dianalisis (p = {Fmt.P(h.BartlettP)} < 0,05)."
                    : $"Matriks korelasi belum menyimpang dari matriks identitas (p = {Fmt.P(h.BartlettP)} ≥ 0,05) — "
                      + "struktur bersama lemah, hasil faktor perlu diwaspadai."));

            
            var barisKmo = new List<List<string>>();
            for (int i = 0; i < h.P; i++)
                barisKmo.Add(new List<string> { h.Variabel[i], Fmt.Num(h.KmoPerVariabel[i], 4) });
            blok.Add(Blocks.Table("KMO per variabel (kecukupan sampel per variabel)",
                new[] { "Variabel", "KMO" }, barisKmo,
                "Nilai di bawah 0,50 menandakan variabel itu lebih baik dikeluarkan dari analisis."));

            
            var barisKom = new List<List<string>>();
            for (int i = 0; i < h.P; i++)
                barisKom.Add(new List<string>
                {
                    h.Variabel[i],
                    Fmt.Num(h.KomunalitasMentah.Length > i ? h.KomunalitasMentah[i] : 1.0, 4),
                    Fmt.Num(h.Komunalitas[i], 4)
                });
            blok.Add(Blocks.Table("Komunalitas",
                new[] { "Variabel", "Awal (seluruh faktor)", "Ekstraksi" }, barisKom,
                "Komunalitas = bagian ragam variabel yang diterangkan oleh faktor yang dipakai. "
                + (h.Rotasi == "varimaks" && h.JumlahFaktor >= 2
                    ? "Rotasi varimaks tidak mengubahnya (rotasinya ortogonal)."
                    : "")));

            
            var barisRagam = new List<List<string>>();
            for (int f = 0; f < h.JumlahFaktor; f++)
            {
                barisRagam.Add(new List<string>
                {
                    $"Faktor {f + 1}",
                    Fmt.Num(h.EigenSemua.Length > f ? h.EigenSemua[f] : double.NaN, 4),
                    Fmt.Num(h.RagamFaktor[f], 4),
                    Fmt.Num(h.Persen[f], 2) + "%",
                    Fmt.Num(h.Kumulatif[f], 2) + "%"
                });
            }
            blok.Add(Blocks.Table("Ragam yang diterangkan",
                new[] { "Faktor", "Nilai eigen (tanpa rotasi)", "Jumlah kuadrat muatan", "% ragam", "% kumulatif" },
                barisRagam,
                h.Metode == "paf"
                    ? "Sumbu utama: komunalitas ditaksir berulang, sehingga nilai eigen berasal dari matriks tereduksi."
                    : "Komponen utama: jumlah seluruh nilai eigen sama dengan banyak variabel (p)."));

            
            var judulKolom = new List<string> { "Variabel" };
            for (int f = 0; f < h.JumlahFaktor; f++) judulKolom.Add($"Faktor {f + 1}");
            var barisMuatan = new List<List<string>>();
            for (int i = 0; i < h.P; i++)
            {
                var baris = new List<string> { h.Variabel[i] };
                for (int f = 0; f < h.JumlahFaktor; f++) baris.Add(Fmt.Num(h.Muatan[i, f], 4));
                barisMuatan.Add(baris);
            }
            blok.Add(Blocks.Table(h.Rotasi == "varimax" && h.JumlahFaktor >= 2
                                    ? "Matriks muatan (setelah rotasi varimaks)"
                                    : "Matriks muatan",
                judulKolom, barisMuatan,
                "Muatan |0,40| atau lebih lazim dipakai sebagai ambang penamaan faktor."));

            
            if (h.Skor.GetLength(0) > 0)
            {
                var barisSkor = new List<List<string>>();
                int batas = Math.Min(10, h.Skor.GetLength(0));
                for (int i = 0; i < batas; i++)
                {
                    var baris = new List<string> { (i + 1).ToString() };
                    for (int f = 0; f < h.JumlahFaktor; f++) baris.Add(Fmt.Num(h.Skor[i, f], 3));
                    barisSkor.Add(baris);
                }
                var judulSkor = new List<string> { "Baris" };
                for (int f = 0; f < h.JumlahFaktor; f++) judulSkor.Add($"Skor F{f + 1}");
                blok.Add(Blocks.Table("Skor faktor (10 baris pertama)", judulSkor, barisSkor,
                    h.Metode == "paf"
                        ? "Skor dihitung dengan metode regresi: B = R⁻¹·Λ, skor = Z·B."
                        : "Skor komponen utama: Z diproyeksikan ke vektor eigen, lalu diputar bila dirotasi."));
            }

            
            if (h.EigenSemua.Length >= 2)
            {
                var spec = new ChartSpec
                {
                    Kind = ChartKind.Scatter,
                    Title = "Diagram scree (nilai eigen)",
                    XTitle = "Nomor komponen",
                    YTitle = "Nilai eigen"
                };
                for (int i = 0; i < h.EigenSemua.Length; i++)
                    spec.Points.Add((i + 1, h.EigenSemua[i]));
                blok.Add(Blocks.Chart("Diagram scree (nilai eigen)", spec));
            }

            blok.Add(Blocks.Note(
                $"Metode ekstraksi: {(h.Metode == "paf" ? "sumbu utama (principal axis factoring)" : "komponen utama")}"
                + $"; rotasi: {(h.Rotasi == "varimax" && h.JumlahFaktor >= 2 ? "varimaks" : "tanpa rotasi")}"
                + $"; faktor yang dipakai: {h.JumlahFaktor}"
                + (jumlahFaktor == 0 ? " (aturan Kaiser: nilai eigen > 1)" : " (ditentukan sendiri)")
                + ". Angkanya dibandingkan dengan NumPy (`numpy.linalg.eigh`) dan rotasi varimaksnya "
                + "dibandingkan dengan statsmodels — lihat `docs/acuan_faktor.json`.",
                NoteKind.Info));

            return blok;
        }
    }
}
