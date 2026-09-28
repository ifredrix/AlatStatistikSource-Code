using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class Korespondensi
    {
        public sealed class Hasil
        {
            public List<string> NamaBaris = new();
            public List<string> NamaKolom = new();
            public int N, Dimensi, Df;
            public double Khi2, P, InersiaTotal;
            public double[] Inersia = Array.Empty<double>();
            public double[] Singular = Array.Empty<double>();
            public double[,] KoordBaris = new double[0, 0];
            public double[,] KoordKolom = new double[0, 0];
        }

        public static Hasil? Pasang(double[,] cacah, List<string> namaBaris,
                                    List<string> namaKolom)
        {
            int R = cacah.GetLength(0), C = cacah.GetLength(1);
            if (namaBaris.Count != R || namaKolom.Count != C) return null;
            if (R < 2 || C < 2) return null;
            double n = 0;
            for (int i = 0; i < R; i++)
                for (int j = 0; j < C; j++)
                {
                    if (cacah[i, j] < 0 || double.IsNaN(cacah[i, j])) return null;
                    n += cacah[i, j];
                }
            if (n <= 0) return null;

            var r = new double[R];
            var c = new double[C];
            for (int i = 0; i < R; i++)
                for (int j = 0; j < C; j++)
                {
                    r[i] += cacah[i, j] / n;
                    c[j] += cacah[i, j] / n;
                }
            if (r.Any(v => v <= 0) || c.Any(v => v <= 0)) return null;

            var S = new double[R, C];
            for (int i = 0; i < R; i++)
                for (int j = 0; j < C; j++)
                    S[i, j] = (cacah[i, j] / n - r[i] * c[j])
                              / Math.Sqrt(r[i] * c[j]);

            var StS = new double[C, C];
            for (int a = 0; a < C; a++)
                for (int b = 0; b < C; b++)
                {
                    double s = 0;
                    for (int i = 0; i < R; i++) s += S[i, a] * S[i, b];
                    StS[a, b] = s;
                }
            var eig = Aljabar.EigenSimetris(StS);
            int K = Math.Min(R, C) - 1;
            if (eig.Nilai.Length < K) return null;
            var sg = new double[K];
            for (int k = 0; k < K; k++)
            {
                if (eig.Nilai[k] <= 1e-24) return null;
                sg[k] = Math.Sqrt(eig.Nilai[k]);
            }

            var F = new double[R, K];
            var G = new double[C, K];
            for (int k = 0; k < K; k++)
            {
                for (int j = 0; j < C; j++)
                    G[j, k] = eig.Vektor[j, k] * sg[k] / Math.Sqrt(c[j]);
                for (int i = 0; i < R; i++)
                {
                    double u = 0;
                    for (int j = 0; j < C; j++) u += S[i, j] * eig.Vektor[j, k];
                    u /= sg[k];
                    F[i, k] = u * sg[k] / Math.Sqrt(r[i]);
                }
            }

            var iner = new double[K];
            double total = 0;
            for (int k = 0; k < K; k++) { iner[k] = sg[k] * sg[k]; total += iner[k]; }

            double khi2 = total * n;
            int df = (R - 1) * (C - 1);
            return new Hasil
            {
                NamaBaris = new List<string>(namaBaris),
                NamaKolom = new List<string>(namaKolom),
                N = (int)n, Dimensi = K, Df = df,
                Khi2 = khi2, P = Distributions.ChiSquareUpper(khi2, df),
                InersiaTotal = total, Inersia = iner, Singular = sg,
                KoordBaris = F, KoordKolom = G,
            };
        }

        public static List<ResultBlock> KorespondensiBlocks(Dataset ds, string baris,
            string kolom, double alpha = 0.05)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Analisis korespondensi (tabel kontingensi)", 1)
            };

            var perlu = new List<string> { baris, kolom };
            var idx = ds.CompleteRows(perlu);
            if (idx.Count < 10)
            {
                blocks.Add(Blocks.Note(
                    "Baris lengkap kurang dari 10. Analisis korespondensi "
                    + "tidak bisa dijalankan.", NoteKind.Error));
                return blocks;
            }

            var tb = ds.Text(baris);
            var tk = ds.Text(kolom);
            var lvB = new List<string>();
            var lvK = new List<string>();
            foreach (int i in idx)
            {
                string b = tb[i] ?? "(kosong)", k = tk[i] ?? "(kosong)";
                if (!lvB.Contains(b)) lvB.Add(b);
                if (!lvK.Contains(k)) lvK.Add(k);
            }
            lvB.Sort(StringComparer.Ordinal);
            lvK.Sort(StringComparer.Ordinal);
            if (lvB.Count < 2 || lvK.Count < 2)
            {
                blocks.Add(Blocks.Note(
                    "Perlu minimal dua kategori di tiap variabel.",
                    NoteKind.Error));
                return blocks;
            }

            var cacah = new double[lvB.Count, lvK.Count];
            foreach (int i in idx)
                cacah[lvB.IndexOf(tb[i] ?? "(kosong)"), lvK.IndexOf(tk[i] ?? "(kosong)")] += 1;

            var h = Pasang(cacah, lvB, lvK);
            if (h is null)
            {
                blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Korespondensi));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Baris", baris),
                ("Kolom", kolom),
                ("Ukuran tabel", $"{Fmt.Int(lvB.Count)} × {Fmt.Int(lvK.Count)}"),
                ("Banyak amatan", $"n = {Fmt.Int(h.N)}"),
                ("Khi-kuadrat kebebasan", $"χ² = {Fmt.Num(h.Khi2, 4)} (df = {Fmt.Int(h.Df)})"),
                ("Inersia total = χ²/n", Fmt.Num(h.InersiaTotal, 6))));

            blocks.Add(Blocks.Table(
                "Inersia tiap dimensi",
                new[] { "Dimensi", "Nilai singular", "Inersia", "Proporsi" },
                h.Inersia.Select((v, k) => new[]
                {
                    Fmt.Int(k + 1), Fmt.Num(h.Singular[k], 6),
                    Fmt.Num(v, 6), Fmt.Num(v / h.InersiaTotal, 6),
                }).ToArray(),
                "Inersia = kuadrat nilai singular; jumlahnya = χ²/n. "
                + "Dimensi pertama menangkap hubungan terbesar."));

            blocks.Add(Blocks.Table(
                "Koordinat baris (principal)",
                BarisJudulKoord(h.Dimensi),
                h.NamaBaris.Select((nm, i) => BarisKoord(nm, h.KoordBaris, i, h.Dimensi)).ToArray(),
                "Titik baris yang berdekatan punya profil serupa."));
            blocks.Add(Blocks.Table(
                "Koordinat kolom (principal)",
                BarisJudulKoord(h.Dimensi),
                h.NamaKolom.Select((nm, j) => BarisKoord(nm, h.KoordKolom, j, h.Dimensi)).ToArray(),
                "Baris yang dekat dengan kolom berarti selnya lebih penuh "
                + "dari harapan kebebasan."));

            return blocks;
        }

        private static string[] BarisJudulKoord(int k)
        {
            var judul = new string[k + 1];
            judul[0] = "Kategori";
            for (int j = 0; j < k; j++) judul[j + 1] = $"Dim {j + 1}";
            return judul;
        }

        private static string[] BarisKoord(string nama, double[,] m, int i, int k)
        {
            var baris = new string[k + 1];
            baris[0] = nama;
            for (int j = 0; j < k; j++) baris[j + 1] = Fmt.Num(m[i, j], 4);
            return baris;
        }
    }
}
