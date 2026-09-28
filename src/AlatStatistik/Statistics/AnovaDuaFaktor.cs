using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class AnovaDuaFaktor
    {

        public sealed class Suku
        {
            public string Nama = "";
            public double Ss;
            public int Df;
            public double Ms;
            public double F = double.NaN;
            public double P = double.NaN;
        }

        public sealed class Sel
        {
            public string A = "";
            public string B = "";
            public int N;
            public double Rerata;
            public double Sd;
        }

        public sealed class Tepi
        {
            public string Tingkat = "";
            public int N;
            public double Berbobot;
            public double TakBerbobot;
        }

        public sealed class Hasil
        {
            public int N, Ka, Kb, DfResid;
            public List<string> TingkatA = new();
            public List<string> TingkatB = new();

            public List<Suku> Type3 = new();
            public List<Suku> Type2 = new();
            public List<Suku> Type1 = new();

            public List<Sel> Sel = new();
            public List<Tepi> TepiA = new();
            public List<Tepi> TepiB = new();

            public double SsResid, MsResid, Sst, R2, R2Adj;

            public List<string> SelKosong = new();
        }

        

        public static Hasil? Hitung(double[] y, string[] fa, string[] fb)
        {
            int n = y.Length;
            if (n < 8 || fa.Length != n || fb.Length != n) return null;

            var ta = fa.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            var tb = fb.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            int ka = ta.Count, kb = tb.Count;
            if (ka < 2 || kb < 2) return null;

            
            
            
            var selKosong = new List<string>();
            var nSel = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                string k = fa[i] + "\u0001" + fb[i];
                nSel.TryGetValue(k, out int c);
                nSel[k] = c + 1;
            }
            foreach (var a in ta)
                foreach (var b in tb)
                    if (!nSel.ContainsKey(a + "\u0001" + b))
                        selKosong.Add($"{a} × {b}");

            int dfResid = n - ka * kb;
            if (selKosong.Count > 0 || dfResid < 1) return null;

            
            double[][] penuh = Rancang(y, fa, fb, ta, tb, true, true, true);
            double ssePenuh = Sse(y, penuh, out _);
            if (double.IsNaN(ssePenuh)) return null;

            
            var t3 = new List<Suku>
            {
                SukuDari("faktor A", ssePenuh, y, Rancang(y, fa, fb, ta, tb, false, true, true),
                         ka - 1, dfResid),
                SukuDari("faktor B", ssePenuh, y, Rancang(y, fa, fb, ta, tb, true, false, true),
                         kb - 1, dfResid),
                SukuDari("A × B", ssePenuh, y, Rancang(y, fa, fb, ta, tb, true, true, false),
                         (ka - 1) * (kb - 1), dfResid),
            };

            
            
            double sse1 = Sse(y, Rancang(y, fa, fb, ta, tb, false, false, false), out _);
            double sseA = Sse(y, Rancang(y, fa, fb, ta, tb, true, false, false), out _);
            double sseB = Sse(y, Rancang(y, fa, fb, ta, tb, false, true, false), out _);
            double sseAB = Sse(y, Rancang(y, fa, fb, ta, tb, true, true, false), out _);

            var t2 = new List<Suku>
            {
                SukuSederhana("faktor A", sseB - sseAB, ka - 1, dfResid),
                SukuSederhana("faktor B", sseA - sseAB, kb - 1, dfResid),
                SukuSederhana("A × B", sseAB - ssePenuh, (ka - 1) * (kb - 1), dfResid),
            };

            
            var t1 = new List<Suku>
            {
                SukuSederhana("faktor A", sse1 - sseA, ka - 1, dfResid),
                SukuSederhana("faktor B", sseA - sseAB, kb - 1, dfResid),
                SukuSederhana("A × B", sseAB - ssePenuh, (ka - 1) * (kb - 1), dfResid),
            };

            double msResid = ssePenuh / dfResid;
            foreach (var daftar in new[] { t3, t2, t1 })
                foreach (var s in daftar)
                {
                    if (msResid > 0)
                    {
                        s.F = s.Ms / msResid;
                        s.P = Distributions.FUpper(s.F, s.Df, dfResid);
                    }
                }

            
            var daftarSel = new List<Sel>();
            foreach (var a in ta)
                foreach (var b in tb)
                {
                    var nilai = new List<double>();
                    for (int i = 0; i < n; i++)
                        if (string.Equals(fa[i], a, StringComparison.Ordinal)
                            && string.Equals(fb[i], b, StringComparison.Ordinal))
                            nilai.Add(y[i]);
                    daftarSel.Add(new Sel
                    {
                        A = a, B = b, N = nilai.Count,
                        Rerata = nilai.Count > 0 ? nilai.Average() : double.NaN,
                        Sd = nilai.Count > 1 ? Simpangan(nilai) : double.NaN,
                    });
                }

            
            var tepiA = new List<Tepi>();
            foreach (var a in ta)
            {
                var semua = new List<double>();
                var perSel = new List<double>();
                for (int i = 0; i < n; i++)
                    if (string.Equals(fa[i], a, StringComparison.Ordinal)) semua.Add(y[i]);
                foreach (var s in daftarSel) if (s.A == a) perSel.Add(s.Rerata);
                tepiA.Add(new Tepi
                {
                    Tingkat = a, N = semua.Count,
                    Berbobot = semua.Average(), TakBerbobot = perSel.Average(),
                });
            }
            var tepiB = new List<Tepi>();
            foreach (var b in tb)
            {
                var semua = new List<double>();
                var perSel = new List<double>();
                for (int i = 0; i < n; i++)
                    if (string.Equals(fb[i], b, StringComparison.Ordinal)) semua.Add(y[i]);
                foreach (var s in daftarSel) if (s.B == b) perSel.Add(s.Rerata);
                tepiB.Add(new Tepi
                {
                    Tingkat = b, N = semua.Count,
                    Berbobot = semua.Average(), TakBerbobot = perSel.Average(),
                });
            }

            double rata = y.Average();
            double sst = 0;
            for (int i = 0; i < n; i++) { double d = y[i] - rata; sst += d * d; }
            double r2 = sst > 0 ? 1 - ssePenuh / sst : double.NaN;
            double r2adj = 1 - (1 - r2) * (n - 1) / dfResid;

            return new Hasil
            {
                N = n, Ka = ka, Kb = kb, DfResid = dfResid,
                TingkatA = ta, TingkatB = tb,
                Type3 = t3, Type2 = t2, Type1 = t1,
                Sel = daftarSel, TepiA = tepiA, TepiB = tepiB,
                SsResid = ssePenuh, MsResid = msResid, Sst = sst,
                R2 = r2, R2Adj = r2adj, SelKosong = selKosong,
            };
        }

        private static double[][] Rancang(double[] y, string[] fa, string[] fb,
                                          List<string> ta, List<string> tb,
                                          bool pakaiA, bool pakaiB, bool pakaiInter)
        {
            int n = y.Length, ka = ta.Count, kb = tb.Count;
            var ea = new double[n][];
            var eb = new double[n][];
            for (int i = 0; i < n; i++)
            {
                ea[i] = KodeEfek(ta.IndexOf(fa[i]), ka);
                eb[i] = KodeEfek(tb.IndexOf(fb[i]), kb);
            }

            var kolom = new List<double[]>();
            kolom.Add(Enumerable.Repeat(1.0, n).ToArray());
            if (pakaiA) for (int j = 0; j < ka - 1; j++) kolom.Add(Kolom(ea, j));
            if (pakaiB) for (int j = 0; j < kb - 1; j++) kolom.Add(Kolom(eb, j));
            if (pakaiInter)
                for (int j = 0; j < ka - 1; j++)
                    for (int k = 0; k < kb - 1; k++)
                    {
                        var c = new double[n];
                        for (int i = 0; i < n; i++) c[i] = ea[i][j] * eb[i][k];
                        kolom.Add(c);
                    }

            var x = new double[n][];
            for (int i = 0; i < n; i++)
            {
                x[i] = new double[kolom.Count];
                for (int j = 0; j < kolom.Count; j++) x[i][j] = kolom[j][i];
            }
            return x;
        }

        private static double[] KodeEfek(int idx, int g)
        {
            var v = new double[g - 1];
            if (idx < g - 1) v[idx] = 1.0;
            else for (int j = 0; j < g - 1; j++) v[j] = -1.0;
            return v;
        }

        private static double[] Kolom(double[][] e, int j)
        {
            var c = new double[e.Length];
            for (int i = 0; i < e.Length; i++) c[i] = e[i][j];
            return c;
        }

        private static Suku SukuDari(string nama, double ssePenuh, double[] y,
                                     double[][] tereduksi, int df, int dfResid)
        {
            double sseRed = Sse(y, tereduksi, out _);
            double ss = sseRed - ssePenuh;
            double ms = df > 0 ? ss / df : double.NaN;
            return new Suku { Nama = nama, Ss = ss, Df = df, Ms = ms };
        }

        private static Suku SukuSederhana(string nama, double ss, int df, int dfResid)
        {
            double ms = df > 0 ? ss / df : double.NaN;
            return new Suku { Nama = nama, Ss = ss, Df = df, Ms = ms };
        }

        private static double Sse(double[] y, double[][] x, out double[] beta)
        {
            int n = y.Length, k = x[0].Length;
            beta = new double[k];

            var xtx = new double[k, k];
            var xty = new double[k];
            for (int i = 0; i < n; i++)
                for (int a = 0; a < k; a++)
                {
                    xty[a] += x[i][a] * y[i];
                    for (int b = 0; b < k; b++) xtx[a, b] += x[i][a] * x[i][b];
                }

            var inv = Regression.Inverse((double[,])xtx.Clone());
            if (inv is null) return double.NaN;

            for (int a = 0; a < k; a++)
            {
                double s = 0;
                for (int b = 0; b < k; b++) s += inv[a, b] * xty[b];
                beta[a] = s;
            }

            double sse = 0;
            for (int i = 0; i < n; i++)
            {
                double p = 0;
                for (int a = 0; a < k; a++) p += beta[a] * x[i][a];
                double e = y[i] - p;
                sse += e * e;
            }
            return sse;
        }

        private static double Simpangan(List<double> nilai)
        {
            double rata = nilai.Average();
            double jml = 0;
            foreach (double v in nilai) { double d = v - rata; jml += d * d; }
            return Math.Sqrt(jml / (nilai.Count - 1));
        }

        

        public static List<ResultBlock> AnovaDuaFaktorBlocks(Dataset ds, string dependen,
                                                             string faktorA, string faktorB)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("ANOVA dua faktor + interaksi (Type III)", 1)
            };

            if (string.Equals(faktorA, faktorB, StringComparison.Ordinal))
            {
                blocks.Add(Blocks.Note(
                    $"Faktor A dan faktor B sama-sama '{faktorA}'. Pilih dua variabel "
                    + "kelompok yang berbeda.", NoteKind.Error));
                return blocks;
            }

            var perlu = new[] { dependen, faktorA, faktorB };
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < 12)
            {
                blocks.Add(Blocks.Note(
                    $"Baris lengkap hanya {baris.Count}. ANOVA dua faktor butuh "
                    + "minimal 12 baris supaya derajat bebas residualnya cukup.",
                    NoteKind.Error));
                return blocks;
            }

            var semuaY = ds.Numeric(dependen);
            var teksA = ds.Text(faktorA);
            var teksB = ds.Text(faktorB);

            var y = new double[baris.Count];
            var fa = new string[baris.Count];
            var fb = new string[baris.Count];
            for (int i = 0; i < baris.Count; i++)
            {
                y[i] = semuaY[baris[i]] ?? double.NaN;
                fa[i] = teksA[baris[i]] ?? "(kosong)";
                fb[i] = teksB[baris[i]] ?? "(kosong)";
            }

            var ta = fa.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            var tb = fb.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (ta.Count < 2 || tb.Count < 2)
            {
                blocks.Add(Blocks.Note(
                    $"Butuh dua tingkat atau lebih di KEDUA faktor. Terbaca: "
                    + $"'{faktorA}' = {ta.Count} tingkat, '{faktorB}' = {tb.Count} tingkat.",
                    NoteKind.Error));
                return blocks;
            }

            var h = Hitung(y, fa, fb);
            if (h is null)
            {
                
                var selKosong = new List<string>();
                foreach (var a in ta)
                    foreach (var b in tb)
                    {
                        bool ada = false;
                        for (int i = 0; i < y.Length && !ada; i++)
                            ada = string.Equals(fa[i], a, StringComparison.Ordinal)
                                  && string.Equals(fb[i], b, StringComparison.Ordinal);
                        if (!ada) selKosong.Add($"{a} × {b}");
                    }

                if (selKosong.Count > 0)
                    blocks.Add(Blocks.Note(
                        $"Ada {selKosong.Count} sel kosong: {string.Join(", ", selKosong.Take(6))}"
                        + (selKosong.Count > 6 ? ", …" : "") + ". Jumlah kuadrat Type III "
                        + "TIDAK terdefinisi tunggal bila ada sel kosong — pengodean yang "
                        + "berbeda memberi angka yang berbeda (terukur 0,031234 lawan "
                        + "0,015617 pada kasus uji). Gabungkan tingkatnya, tambah amatan "
                        + "di sel itu, atau pakai alat yang lebih sederhana.", NoteKind.Error));
                else
                    blocks.Add(Blocks.Note(
                        $"Derajat bebas residual habis: n = {y.Length} sedangkan selnya "
                        + $"{ta.Count} × {tb.Count} = {ta.Count * tb.Count}. Tambah baris "
                        + "atau kurangi tingkat faktornya.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.AnovaDuaFaktor, DaftarRumus.RerataTepiTakBerbobot));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Dependen", dependen),
                ("Faktor A", $"{faktorA} ({string.Join(", ", h.TingkatA)})"),
                ("Faktor B", $"{faktorB} ({string.Join(", ", h.TingkatB)})"),
                ("Banyak baris lengkap", $"n = {Fmt.Int(h.N)}"),
                ("Bentuk rancangan", $"{Fmt.Int(h.Ka)} × {Fmt.Int(h.Kb)}"),
                ("Derajat bebas residual", $"n − ({Fmt.Int(h.Ka)}×{Fmt.Int(h.Kb)}) "
                                           + $"= {Fmt.Int(h.DfResid)}")));

            var barisTabel = h.Type3.Select(s => new[]
            {
                s.Nama, Fmt.Num(s.Ss, 6), Fmt.Int(s.Df), Fmt.Num(s.Ms, 6),
                double.IsNaN(s.F) ? Fmt.NA : Fmt.Num(s.F, 4),
                double.IsNaN(s.P) ? Fmt.NA : Fmt.P(s.P),
            }).ToList();
            barisTabel.Add(new[] { "Residual", Fmt.Num(h.SsResid, 6),
                                   Fmt.Int(h.DfResid), Fmt.Num(h.MsResid, 6), Fmt.NA, Fmt.NA });
            barisTabel.Add(new[] { "Total (terkoreksi)", Fmt.Num(h.Sst, 6),
                                   Fmt.Int(h.N - 1), Fmt.NA, Fmt.NA, Fmt.NA });

            blocks.Add(Blocks.Table(
                "Uji efek (jumlah kuadrat Type III)",
                new[] { "Sumber", "Jumlah kuadrat", "df", "Rerata kuadrat", "F", "p" },
                barisTabel.ToArray(),
                "Tiap suku diuji SETELAH suku lain diperhitungkan, termasuk suku "
                + "interaksi. Karena itu angkanya tidak bergantung urutan penulisan "
                + "model. Baris A × B menjawab \"apakah pengaruh satu faktor berubah "
                + "menurut tingkat faktor lain?\" — inilah yang tidak bisa dijawab "
                + "ANOVA satu arah. Perhatikan: jumlah kuadrat Type III **tidak** "
                + "menjumlah menjadi jumlah kuadrat total, sebab suku-sukunya tidak "
                + "saling ortogonal pada data tak berimbang. Jangan menjumlahkannya "
                + "untuk menghitung R²."));

            blocks.Add(Blocks.Table(
                "Tiga tipe jumlah kuadrat, dibandingkan",
                new[] { "Sumber", "Type I (berurutan)", "Type II", "Type III" },
                Enumerable.Range(0, h.Type3.Count).Select(i => new[]
                {
                    h.Type3[i].Nama,
                    Fmt.Num(h.Type1[i].Ss, 6),
                    Fmt.Num(h.Type2[i].Ss, 6),
                    Fmt.Num(h.Type3[i].Ss, 6),
                }).ToArray(),
                "Type I bergantung urutan penulisan model — urutan bukan keputusan "
                + "statistik, jadi ia tidak dipakai. Type II tidak memperhitungkan "
                + "interaksi. Type III yang dipakai di tabel utama. Pada data "
                + "BERIMBANG ketiganya sama persis; pada data tak berimbang keduanya "
                + "berbeda, dan perbedaan itulah alasan tabel ini ditampilkan."));

            blocks.Add(Blocks.Table(
                $"Rerata tepi faktor A — {faktorA}",
                new[] { "Tingkat", "n", "Rerata berbobot", "Rerata tak berbobot", "Selisih" },
                h.TepiA.Select(t => new[]
                {
                    t.Tingkat, Fmt.Int(t.N), Fmt.Num(t.Berbobot, 4),
                    Fmt.Num(t.TakBerbobot, 4),
                    Fmt.Num(t.TakBerbobot - t.Berbobot, 4),
                }).ToArray(),
                "Type III menguji kolom **tak berbobot** — tiap tingkat faktor lain "
                + "dianggap sama penting, jadi yang dirata-ratakan adalah rerata SEL, "
                + "bukan amatan. Bila rancangannya tak berimbang, kedua kolom ini "
                + "berbeda, dan perbedaan itulah yang membuat Type III berbeda dari "
                + "Type I dan Type II."));

            blocks.Add(Blocks.Table(
                $"Rerata tepi faktor B — {faktorB}",
                new[] { "Tingkat", "n", "Rerata berbobot", "Rerata tak berbobot", "Selisih" },
                h.TepiB.Select(t => new[]
                {
                    t.Tingkat, Fmt.Int(t.N), Fmt.Num(t.Berbobot, 4),
                    Fmt.Num(t.TakBerbobot, 4),
                    Fmt.Num(t.TakBerbobot - t.Berbobot, 4),
                }).ToArray(),
                "Sama seperti tabel faktor A, tetapi untuk faktor B."));

            blocks.Add(Blocks.Table(
                "Rerata tiap sel",
                new[] { faktorA, faktorB, "n", $"Rerata {dependen}", "Simpangan baku" },
                h.Sel.Select(s => new[]
                {
                    s.A, s.B, Fmt.Int(s.N), Fmt.Num(s.Rerata, 4),
                    double.IsNaN(s.Sd) ? Fmt.NA : Fmt.Num(s.Sd, 4),
                }).ToArray(),
                "Tabel ini yang paling cepat menunjukkan adanya interaksi: bila "
                + "selisih antar tingkat faktor B berbeda-beda menurut tingkat faktor "
                + "A, berarti kedua faktor saling bergantung."));

            blocks.Add(Blocks.Table(
                "Ringkasan model",
                new[] { "Besaran", "Nilai" },
                new[]
                {
                    new[] { "R²", Fmt.Num(h.R2, 6) },
                    new[] { "R² disesuaikan", Fmt.Num(h.R2Adj, 6) },
                    new[] { "Galat baku taksiran",
                            Fmt.Num(Math.Sqrt(Math.Max(0, h.MsResid)), 4) },
                    new[] { "Banyak baris", Fmt.Int(h.N) },
                },
                "R² menggabungkan kedua faktor beserta interaksinya; ia bukan ukuran "
                + "seberapa besar efek salah satu faktornya."));

            if (h.MsResid <= 0)
                blocks.Add(Blocks.Note(
                    "Rerata kuadrat residual tepat nol: modelnya memuat datanya persis, "
                    + "sehingga F dan p tidak terdefinisi dan ditampilkan sebagai "
                    + Fmt.NA + ". Ini terjadi pada data buatan tanpa galat acak; pada "
                    + "data sungguhan hampir tidak pernah.", NoteKind.Warning));

            blocks.Add(Blocks.Note(
                "Type III menguji rerata tepi tak berbobot. Bila rancangannya tak "
                + "berimbang, hipotesis itu berbeda dari \"rerata kelompok yang "
                + "berbeda\", dan pembaca harus tahu mana yang diuji. Bila tiap sel "
                + "hanya berisi satu amatan, derajat bebas residual habis dan "
                + "interaksinya tidak bisa diuji sama sekali.", NoteKind.Info));

            return blocks;
        }
    }
}
