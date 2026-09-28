using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    // PRINCALS: penskalaan optimal untuk variabel campuran (nominal,
    // ordinal, numerik) dengan dua dimensi, via ALS (Gifi/de Leeuw).
    //
    public static class Princals
    {
        public enum Aras { Nominal, Ordinal, Numerik }

        public sealed class Kuant
        {
            public string Variabel = "";
            public Aras Aras;
            public List<string> Kategori = new();
            public List<int> Frekuensi = new();
            // [kategori, dimensi]
            public double[,] Nilai = new double[0, 0];
            // ukuran diskriminasi per dimensi (η²); numerik: a²
            public double[] Diskriminan = Array.Empty<double>();
        }

        public sealed class Hasil
        {
            public List<string> NamaVariabel = new();
            public int N, M;
            public int Dimensi = 2;
            public double[] Eigen = Array.Empty<double>();
            public double[] Alpha = Array.Empty<double>();
            public double Loss;
            public List<Kuant> Kuantifikasi = new();
            // [baris, dimensi]
            public double[,] Skor = new double[0, 0];
            public bool Konvergen;
            public int Iterasi;
        }

        private const int Dim = 2;
        private const int IterMaks = 1000;
        private const double TolLoss = 1e-10;

        // PAVA: regresi isotonik naik berbobot. Mengembalikan nilai
        // yang dipadatkan (monoton tak-turun) dengan panjang yang sama.
        public static double[] Pava(double[] y, double[] w)
        {
            int k = y.Length;
            var sum = new List<double>();
            var bob = new List<double>();
            var cnt = new List<int>();
            for (int i = 0; i < k; i++)
            {
                sum.Add(y[i] * w[i]);
                bob.Add(w[i]);
                cnt.Add(1);
                while (sum.Count >= 2
                       && sum[^1] / bob[^1] < sum[^2] / bob[^2] - 1e-15)
                {
                    int m = sum.Count;
                    sum[m - 2] += sum[m - 1];
                    bob[m - 2] += bob[m - 1];
                    cnt[m - 2] += cnt[m - 1];
                    sum.RemoveAt(m - 1);
                    bob.RemoveAt(m - 1);
                    cnt.RemoveAt(m - 1);
                }
            }
            var keluar = new double[k];
            int t = 0;
            for (int b = 0; b < sum.Count; b++)
            {
                double rata = sum[b] / bob[b];
                for (int j = 0; j < cnt[b]; j++) keluar[t++] = rata;
            }
            return keluar;
        }

        private static void Ortonormal(double[,] x)
        {
            int n = x.GetLength(0), p = x.GetLength(1);
            for (int s = 0; s < p; s++)
            {
                double rata = 0;
                for (int i = 0; i < n; i++) rata += x[i, s];
                rata /= n;
                for (int i = 0; i < n; i++) x[i, s] -= rata;
                for (int t = 0; t < s; t++)
                {
                    double c = 0;
                    for (int i = 0; i < n; i++) c += x[i, s] * x[i, t];
                    c /= n;
                    for (int i = 0; i < n; i++) x[i, s] -= c * x[i, t];
                }
                double norm = 0;
                for (int i = 0; i < n; i++) norm += x[i, s] * x[i, s];
                norm = Math.Sqrt(norm / n);
                if (norm < 1e-15) norm = 1e-15;
                for (int i = 0; i < n; i++) x[i, s] /= norm;
            }
        }

        public sealed class Masukan
        {
            public string Nama = "";
            public Aras Aras;
            // kategorik: kode kategori per baris; numerik: nilai (NaN bila hilang)
            public int[] Kode = Array.Empty<int>();
            public double[] Nilai = Array.Empty<double>();
            public List<string> Label = new();
        }

        public static Hasil? Pasang(List<Masukan> vars)
        {
            int m = vars.Count;
            if (m < 2) return null;
            int n = vars[0].Kode.Length;
            if (vars.Any(v => v.Kode.Length != n)) return null;

            // Baku numerik (ragam populasi 1 — syarat identitas sesatan).
            var zbaku = new Dictionary<int, double[]>();
            foreach (var (v, j) in vars.Select((v, j) => (v, j)))
            {
                if (v.Aras != Aras.Numerik) continue;
                double rata = v.Nilai.Average();
                double rag = v.Nilai.Average(t => (t - rata) * (t - rata));
                if (!(rag > 1e-24)) return null;
                zbaku[j] = v.Nilai.Select(t => (t - rata) / Math.Sqrt(rag)).ToArray();
            }

            var kets = vars.Select(v => v.Aras == Aras.Numerik
                ? 0 : v.Label.Count).ToList();
            if (vars.Where((v, j) => v.Aras != Aras.Numerik).Any(v => v.Label.Count < 2))
                return null;

            // Start: gelombang sinus, lalu ortonormal (X′X = nI).
            var X = new double[n, Dim];
            for (int i = 0; i < n; i++)
                for (int s = 0; s < Dim; s++)
                    X[i, s] = Math.Sin((i + 1) * (s + 2) * 1.7);
            Ortonormal(X);

            var Q = new List<double[,]>();
            for (int j = 0; j < m; j++)
                Q.Add(new double[Math.Max(kets[j], 1), Dim]);

            double Sesatan()
            {
                double s = 0;
                for (int j = 0; j < m; j++)
                {
                    var v = vars[j];
                    if (v.Aras == Aras.Numerik)
                    {
                        var z = zbaku[j];
                        var a = new double[Dim];
                        for (int st = 0; st < Dim; st++)
                        {
                            double c = 0;
                            for (int i = 0; i < n; i++) c += z[i] * X[i, st];
                            a[st] = c / n;
                        }
                        for (int i = 0; i < n; i++)
                            for (int st = 0; st < Dim; st++)
                            {
                                double e = X[i, st] - z[i] * a[st];
                                s += e * e;
                            }
                    }
                    else
                    {
                        for (int i = 0; i < n; i++)
                            for (int st = 0; st < Dim; st++)
                            {
                                double e = X[i, st] - Q[j][v.Kode[i], st];
                                s += e * e;
                            }
                    }
                }
                return s / (n * m);
            }

            double lossLama = Sesatan();
            bool konvergen = false;
            int iter = 0;
            for (iter = 1; iter <= IterMaks; iter++)
            {
                // Kuantifikasi: sentroid, lalu PAVA bila ordinal.
                for (int j = 0; j < m; j++)
                {
                    var v = vars[j];
                    if (v.Aras == Aras.Numerik) continue;
                    int k = kets[j];
                    var fk = new double[k];
                    var q = new double[k, Dim];
                    for (int i = 0; i < n; i++)
                    {
                        fk[v.Kode[i]] += 1;
                        for (int st = 0; st < Dim; st++) q[v.Kode[i], st] += X[i, st];
                    }
                    for (int c = 0; c < k; c++)
                        for (int st = 0; st < Dim; st++) q[c, st] /= fk[c];
                    if (v.Aras == Aras.Ordinal)
                        for (int st = 0; st < Dim; st++)
                        {
                            var kol = new double[k];
                            for (int c = 0; c < k; c++) kol[c] = q[c, st];
                            var padat = Pava(kol, fk);
                            for (int c = 0; c < k; c++) q[c, st] = padat[c];
                        }
                    Q[j] = q;
                }

                // Skor objek: rerata kuantifikasi, lalu ortonormal.
                var Xb = new double[n, Dim];
                for (int i = 0; i < n; i++)
                    for (int st = 0; st < Dim; st++)
                    {
                        double s = 0;
                        for (int j = 0; j < m; j++)
                        {
                            var v = vars[j];
                            if (v.Aras == Aras.Numerik)
                            {
                                double a = 0;
                                var z = zbaku[j];
                                for (int t = 0; t < n; t++) a += z[t] * X[t, st];
                                s += zbaku[j][i] * (a / n);
                            }
                            else s += Q[j][v.Kode[i], st];
                        }
                        Xb[i, st] = s / m;
                    }
                for (int i = 0; i < n; i++)
                    for (int st = 0; st < Dim; st++) X[i, st] = Xb[i, st];
                Ortonormal(X);

                double lossBaru = Sesatan();
                if (Math.Abs(lossLama - lossBaru) <= TolLoss * Math.Max(1, lossLama))
                {
                    lossLama = lossBaru;
                    konvergen = true;
                    break;
                }
                lossLama = lossBaru;
            }

            // Final: kuantifikasi sekali lagi dari X final (Q selama loop
            // basi setengah-langkah karena X diortonormalkan sesudahnya),
            // lalu sesatan konsisten. Aturan tanda di bawah memakai Q
            for (int j = 0; j < m; j++)
            {
                var v = vars[j];
                if (v.Aras == Aras.Numerik) continue;
                int k = kets[j];
                var fk = new double[k];
                var q = new double[k, Dim];
                for (int i = 0; i < n; i++)
                {
                    fk[v.Kode[i]] += 1;
                    for (int st = 0; st < Dim; st++) q[v.Kode[i], st] += X[i, st];
                }
                for (int c = 0; c < k; c++)
                    for (int st = 0; st < Dim; st++) q[c, st] /= fk[c];
                if (v.Aras == Aras.Ordinal)
                    for (int st = 0; st < Dim; st++)
                    {
                        var kol = new double[k];
                        for (int c = 0; c < k; c++) kol[c] = q[c, st];
                        var padat = Pava(kol, fk);
                        for (int c = 0; c < k; c++) q[c, st] = padat[c];
                    }
                Q[j] = q;
            }
            lossLama = Sesatan();

            // Ukuran akhir + tanda dimensi (aturan deterministik,
            // lihat bawah: numerik = tanda korelasi, kategorik =
            // tanda kategori pertama yang tak-nol).
            var hasil = new Hasil
            {
                N = n, M = m,
                Eigen = new double[Dim],
                Alpha = new double[Dim],
                Loss = lossLama,
                Skor = (double[,])X.Clone(),
                Konvergen = konvergen,
                Iterasi = iter,
            };
            var tanda = new double[Dim];
            for (int st = 0; st < Dim; st++)
            {
                double t = 1.0;
                var v0 = vars[0];
                if (v0.Aras == Aras.Numerik)
                {
                    double c = 0;
                    var z = zbaku[0];
                    for (int i = 0; i < n; i++) c += z[i] * X[i, st];
                    t = c >= 0 ? 1.0 : -1.0;
                }
                else
                {
                    for (int c = 0; c < kets[0]; c++)
                        if (Math.Abs(Q[0][c, st]) >= 1e-9)
                        {
                            t = Q[0][c, st] > 0 ? 1.0 : -1.0;
                            break;
                        }
                }
                tanda[st] = t;
            }
            for (int i = 0; i < n; i++)
                for (int st = 0; st < Dim; st++) hasil.Skor[i, st] *= tanda[st];

            for (int j = 0; j < m; j++)
            {
                var v = vars[j];
                var ku = new Kuant
                {
                    Variabel = v.Nama, Aras = v.Aras,
                    Diskriminan = new double[Dim],
                };
                if (v.Aras == Aras.Numerik)
                {
                    var z = zbaku[j];
                    for (int st = 0; st < Dim; st++)
                    {
                        double a = 0;
                        for (int i = 0; i < n; i++) a += z[i] * hasil.Skor[i, st];
                        a /= n;
                        ku.Diskriminan[st] = a * a;
                        hasil.Eigen[st] += a * a / m;
                    }
                }
                else
                {
                    int k = kets[j];
                    ku.Nilai = new double[k, Dim];
                    var fk = new double[k];
                    for (int i = 0; i < n; i++) fk[v.Kode[i]] += 1;
                    for (int c = 0; c < k; c++)
                    {
                        ku.Kategori.Add(v.Label[c]);
                        ku.Frekuensi.Add((int)fk[c]);
                        for (int st = 0; st < Dim; st++)
                            ku.Nilai[c, st] = Q[j][c, st] * tanda[st];
                    }
                    for (int st = 0; st < Dim; st++)
                    {
                        double eta = 0;
                        for (int c = 0; c < k; c++)
                            eta += ku.Nilai[c, st] * ku.Nilai[c, st] * fk[c];
                        eta /= n;
                        ku.Diskriminan[st] = eta;
                        hasil.Eigen[st] += eta / m;
                    }
                }
                hasil.Kuantifikasi.Add(ku);
                hasil.NamaVariabel.Add(v.Nama);
            }
            for (int st = 0; st < Dim; st++)
                hasil.Alpha[st] = hasil.Eigen[st] > 1e-12
                    ? m / (double)(m - 1) * (1 - 1 / hasil.Eigen[st])
                    : double.NaN;

            return hasil;
        }

        public static List<ResultBlock> PrincalsBlocks(Dataset ds,
            List<string> nominal, List<string> ordinal, List<string> numerik)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("PRINCALS — penskalaan optimal (2 dimensi)", 1)
            };

            var semua = new List<string>();
            semua.AddRange(nominal);
            semua.AddRange(ordinal);
            semua.AddRange(numerik);
            if (semua.Count < 2)
            {
                blocks.Add(Blocks.Note(
                    "PRINCALS butuh sedikitnya dua variabel.", NoteKind.Error));
                return blocks;
            }
            var baris = ds.CompleteRows(semua);
            if (baris.Count < semua.Count + 8)
            {
                blocks.Add(Blocks.Note(
                    "Baris lengkap kurang. Model ini tidak bisa dijalankan.",
                    NoteKind.Error));
                return blocks;
            }

            var vars = new List<Masukan>();
            foreach (var nm in nominal)
            {
                var teks = baris.Select(r => ds.Text(nm)[r] ?? "(kosong)").ToList();
                var label = teks.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
                if (label.Count < 2)
                {
                    blocks.Add(Blocks.Note(
                        $"Variabel '{nm}' hanya punya satu kategori.", NoteKind.Error));
                    return blocks;
                }
                var peta = label.Select((t, i) => (t, i))
                                .ToDictionary(t => t.t, t => t.i);
                vars.Add(new Masukan
                {
                    Nama = nm, Aras = Aras.Nominal,
                    Kode = teks.Select(t => peta[t]).ToArray(),
                    Label = label,
                });
            }
            foreach (var nm in ordinal)
            {
                var teks = baris.Select(r => ds.Text(nm)[r] ?? "(kosong)").ToList();
                var angka = teks.Select(t => double.TryParse(t.Replace(',', '.'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double v)
                    ? (double?)v : null).ToList();
                List<string> label;
                int[] kode;
                if (angka.All(v => v.HasValue))
                {
                    // Ordinal angka: urutan = urutan numerik.
                    var urut = angka.Select(v => v!.Value).Distinct().OrderBy(v => v).ToList();
                    var peta = urut.Select((t, i) => (t, i))
                                   .ToDictionary(t => t.t, t => t.i);
                    label = urut.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
                    kode = angka.Select(v => peta[v!.Value]).ToArray();
                }
                else
                {
                    // Ordinal teks: urutan = abjad (didokumentasikan).
                    label = teks.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
                    if (label.Count < 2)
                    {
                        blocks.Add(Blocks.Note(
                            $"Variabel '{nm}' hanya punya satu kategori.", NoteKind.Error));
                        return blocks;
                    }
                    var peta = label.Select((t, i) => (t, i))
                                    .ToDictionary(t => t.t, t => t.i);
                    kode = teks.Select(t => peta[t]).ToArray();
                }
                vars.Add(new Masukan { Nama = nm, Aras = Aras.Ordinal, Kode = kode, Label = label });
            }
            foreach (var nm in numerik)
            {
                var semuaKol = ds.Numeric(nm);
                var nil = baris.Select(r => semuaKol[r] ?? double.NaN).ToArray();
                if (nil.Any(double.IsNaN))
                {
                    blocks.Add(Blocks.Note(
                        $"Variabel '{nm}' punya nilai hilang di baris lengkap.",
                        NoteKind.Error));
                    return blocks;
                }
                vars.Add(new Masukan
                {
                    Nama = nm, Aras = Aras.Numerik,
                    Kode = new int[baris.Count], Nilai = nil,
                });
            }

            var h = Pasang(vars);
            if (h is null)
            {
                blocks.Add(Blocks.Note(
                    "Model tidak bisa disesuaikan.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.PrincalsModel, DaftarRumus.PrincalsUkuran));

            double totalEigen = h.Eigen.Sum();
            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Nominal", nominal.Count > 0 ? string.Join(", ", nominal) : "(tidak ada)"),
                ("Ordinal", ordinal.Count > 0 ? string.Join(", ", ordinal) : "(tidak ada)"),
                ("Numerik", numerik.Count > 0 ? string.Join(", ", numerik) : "(tidak ada)"),
                ("Banyak baris lengkap", $"n = {Fmt.Int(h.N)}"),
                ("Iterasi ALS", $"{Fmt.Int(h.Iterasi)} ({(h.Konvergen ? "konvergen" : "TIDAK konvergen")})"),
                ("Sesatan akhir L", Fmt.Num(h.Loss, 6))));

            blocks.Add(Blocks.Table(
                "Ringkasan dimensi",
                new[] { "Dimensi", "Eigen", "Alfa Cronbach", "% ragam" },
                h.Eigen.Select((e, s) => new[]
                {
                    Fmt.Int(s + 1), Fmt.Num(e, 6),
                    double.IsNaN(h.Alpha[s]) ? "(tak hingga)" : Fmt.Num(h.Alpha[s], 6),
                    Fmt.Num(totalEigen > 0 ? 100 * e / totalEigen : 0, 2),
                }).ToArray(),
                "Eigen = rerata ukuran diskriminasi. Alfa boleh negatif — "
                + "artinya dimensi itu lebih buruk daripada tanpa model."));

            blocks.Add(Blocks.Table(
                "Ukuran diskriminasi (η²) per variabel",
                new[] { "Variabel", "Dim 1", "Dim 2", "Rerata" },
                h.Kuantifikasi.Select(k => new[]
                {
                    k.Variabel, Fmt.Num(k.Diskriminan[0], 6),
                    Fmt.Num(k.Diskriminan[1], 6),
                    Fmt.Num((k.Diskriminan[0] + k.Diskriminan[1]) / 2, 6),
                }).ToArray(),
                "η² = proporsi ragam skor objek yang diterangkan variabel itu."));

            foreach (var k in h.Kuantifikasi.Where(k => k.Aras != Aras.Numerik))
                blocks.Add(Blocks.Table(
                    $"Kuantifikasi — {k.Variabel} ({(k.Aras == Aras.Ordinal ? "ordinal" : "nominal")})",
                    new[] { "Kategori", "n", "Dim 1", "Dim 2" },
                    k.Kategori.Select((c, i) => new[]
                    {
                        c, Fmt.Int(k.Frekuensi[i]),
                        Fmt.Num(k.Nilai[i, 0], 4), Fmt.Num(k.Nilai[i, 1], 4),
                    }).ToArray(),
                    k.Aras == Aras.Ordinal
                    ? "Kuantifikasi monoton naik menurut urutan kategori "
                      + "(angka: urutan numerik; teks: abjad)."
                    : "Sentroid tiap kategori pada peta skor objek."));

            int tampil = Math.Min(10, h.N);
            blocks.Add(Blocks.Table(
                $"Skor objek ({tampil} baris pertama dari {Fmt.Int(h.N)})",
                new[] { "Baris", "Dim 1", "Dim 2" },
                Enumerable.Range(0, tampil).Select(i => new[]
                {
                    Fmt.Int(i + 1), Fmt.Num(h.Skor[i, 0], 4), Fmt.Num(h.Skor[i, 1], 4),
                }).ToArray(),
                "Tanda tiap dimensi dipilih dari kategori pertama yang tak-nol "
                + "(atau tanda korelasi bila variabel pertama numerik) — "
                + "peta yang dicerminkan sama sahnya."));

            return blocks;
        }
    }
}
