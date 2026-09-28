using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class Dbscan
    {
        public sealed class Hasil
        {
            public List<string> Variabel = new();
            public int N;
            public double Eps;
            public int TitikMin;
            public int[] Label = Array.Empty<int>();
            public bool[] Inti = Array.Empty<bool>();
            public int NGugus;
            public int[] Ukuran = Array.Empty<int>();
            public int Derau;
        }

        public static List<int>[] Tetangga(double[][] x, double eps)
        {
            int n = x.Length;
            int p = x[0].Length;
            var keluar = new List<int>[n];
            for (int i = 0; i < n; i++)
            {
                var daftar = new List<(double, int)>();
                for (int j = 0; j < n; j++)
                {
                    double s = 0;
                    for (int k = 0; k < p; k++)
                    {
                        double d = x[i][k] - x[j][k];
                        s += d * d;
                    }
                    if (s <= eps * eps) daftar.Add((s, j));
                }
                daftar.Sort((a, b) =>
                {
                    int c = a.Item1.CompareTo(b.Item1);
                    return c != 0 ? c : a.Item2.CompareTo(b.Item2);
                });
                keluar[i] = daftar.Select(t => t.Item2).ToList();
            }
            return keluar;
        }

        public static Hasil? Pasang(double[][] x, List<string> variabel,
                                    double eps, int titikMin)
        {
            int n = x.Length;
            if (n < 3 || eps <= 0 || titikMin < 1) return null;
            int p = x[0].Length;
            if (x.Any(r => r.Length != p || r.Any(double.IsNaN))) return null;

            var tetangga = Tetangga(x, eps);
            var inti = new bool[n];
            for (int i = 0; i < n; i++) inti[i] = tetangga[i].Count >= titikMin;

            var label = new int[n];
            for (int i = 0; i < n; i++) label[i] = -1;
            int gugus = 0;
            var antre = new Queue<int>();
            var pernah = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (label[i] != -1 || !inti[i]) continue;
                label[i] = gugus;
                foreach (int j in tetangga[i])
                    if (!pernah[j]) { pernah[j] = true; antre.Enqueue(j); }
                while (antre.Count > 0)
                {
                    int j = antre.Dequeue();
                    if (label[j] == -1) label[j] = gugus;
                    if (inti[j])
                        foreach (int k in tetangga[j])
                            if (!pernah[k]) { pernah[k] = true; antre.Enqueue(k); }
                }
                gugus++;
            }

            var ukuran = new int[gugus];
            int derau = 0;
            for (int i = 0; i < n; i++)
            {
                if (label[i] < 0) derau++;
                else ukuran[label[i]]++;
            }
            return new Hasil
            {
                Variabel = new List<string>(variabel), N = n,
                Eps = eps, TitikMin = titikMin,
                Label = label, Inti = inti, NGugus = gugus,
                Ukuran = ukuran, Derau = derau,
            };
        }

        public static List<ResultBlock> DbscanBlocks(Dataset ds, List<string> vars,
            double eps, int titikMin)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("DBSCAN (gugus kepadatan + derau)", 1)
            };
            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Dbscan));

            var baris = ds.CompleteRows(vars);
            if (baris.Count < 3)
            {
                blocks.Add(Blocks.Note("Baris lengkap kurang dari 3.",
                                       NoteKind.Error));
                return blocks;
            }
            if (eps <= 0) eps = 0.9;
            if (titikMin < 1) titikMin = 5;

            var semua = vars.Select(v => ds.Numeric(v)).ToList();
            var x = new double[baris.Count][];
            for (int i = 0; i < baris.Count; i++)
            {
                x[i] = new double[vars.Count];
                for (int j = 0; j < vars.Count; j++)
                    x[i][j] = semua[j][baris[i]] ?? double.NaN;
            }
            var h = Pasang(x, vars, eps, titikMin);
            if (h is null)
            {
                blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.",
                                       NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Variabel (jarak Euclidean mentah, tanpa dibakukan)",
                 string.Join(", ", vars)),
                ("Jari-jari eps", Fmt.Num(h.Eps, 4)),
                ("Titik minimum", Fmt.Int(h.TitikMin)),
                ("Banyak baris", $"n = {Fmt.Int(h.N)}"),
                ("Gugus", Fmt.Int(h.NGugus)),
                ("Derau", $"{Fmt.Int(h.Derau)} baris")));

            var barisUkuran = new List<string[]>();
            for (int g = 0; g < h.NGugus; g++)
            {
                int anggota = h.Ukuran[g];
                int inti = 0;
                for (int i = 0; i < h.N; i++)
                    if (h.Label[i] == g && h.Inti[i]) inti++;
                barisUkuran.Add(new[] { $"G{g + 1}", Fmt.Int(anggota), Fmt.Int(inti) });
            }
            barisUkuran.Add(new[] { "(derau)", Fmt.Int(h.Derau), "—" });
            blocks.Add(Blocks.Table(
                "Ukuran gugus",
                new[] { "Gugus", "Anggota", "Inti" },
                barisUkuran.ToArray(),
                "Inti = baris dengan ≥ titik-min tetangga dalam eps."));
            if (h.NGugus == 0)
                blocks.Add(Blocks.Note("Tak ada gugus: semua baris derau. "
                                       + "Naikkan eps atau turunkan titik-min.",
                                       NoteKind.Warning));

            return blocks;
        }
    }
}
