using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class KMeans
    {
        public class Hasil
        {
            public int K;
            public int Iterasi;
            public bool Konvergen;
            public List<List<double>> Titik = new();
            public List<List<double>> Pusat = new();
            public List<int> Anggota = new();
            public List<int> Ukuran = new();
            public List<double> WcssIterasi = new();
            public double Wcss;
        }

        private static double Jarak2(List<double> a, List<double> b)
        {
            double s = 0;
            for (int j = 0; j < a.Count; j++) { double d = a[j] - b[j]; s += d * d; }
            return s;
        }

        public static Hasil? Hitung(Dataset ds, List<string> vars, int k, int maksIterasi, uint benih)
        {
            int p = vars.Count;
            if (p == 0) return null;
            var idx = vars.Select(v => ds.IndexOf(v)).ToList();
            if (idx.Any(i => i < 0)) return null;

            
            var titik = new List<List<double>>();
            for (int r = 0; r < ds.RowCount; r++)
            {
                var baris = new List<double>();
                bool ok = true;
                foreach (int i in idx)
                {
                    double? d = Dataset.ToDouble(ds.Ambil(r, i));
                    if (!d.HasValue) { ok = false; break; }
                    baris.Add(d.Value);
                }
                if (ok) titik.Add(baris);
            }

            int n = titik.Count;
            if (n == 0 || k < 1) return null;
            k = Math.Min(k, n);

            
            uint s = benih == 0 ? 1u : benih;
            double Rnd()
            {
                s = s * 1664525u + 1013904223u;
                return (s >> 8) / 16777216.0;
            }

            
            var pusat = new List<List<double>>();
            int pertama = (int)Math.Floor(Rnd() * n);
            if (pertama < 0) pertama = 0;
            if (pertama >= n) pertama = n - 1;
            pusat.Add(new List<double>(titik[pertama]));

            var d2 = Enumerable.Range(0, n).Select(i => Jarak2(titik[i], pusat[0])).ToArray();
            for (int c = 1; c < k; c++)
            {
                double total = d2.Sum();
                int pilih = n - 1;
                if (total > 0)
                {
                    double u = Rnd() * total, akum = 0;
                    for (int i = 0; i < n; i++) { akum += d2[i]; if (akum >= u) { pilih = i; break; } }
                }
                else pilih = c % n;
                pusat.Add(new List<double>(titik[pilih]));
                for (int i = 0; i < n; i++)
                {
                    double dd = Jarak2(titik[i], pusat[c]);
                    if (dd < d2[i]) d2[i] = dd;
                }
            }

            
            var anggota = new int[n];
            var wcssIter = new List<double>();
            bool konvergen = false;
            int iter = 0;
            int batas = Math.Max(1, maksIterasi);

            for (iter = 1; iter <= batas; iter++)
            {
                bool berubah = false;
                for (int i = 0; i < n; i++)
                {
                    int terbaik = 0;
                    double terdekat = double.MaxValue;
                    for (int c = 0; c < k; c++)
                    {
                        double dd = Jarak2(titik[i], pusat[c]);
                        if (dd < terdekat) { terdekat = dd; terbaik = c; }
                    }
                    if (anggota[i] != terbaik) { anggota[i] = terbaik; berubah = true; }
                }

                var jumlah = Enumerable.Range(0, k).Select(_ => new double[p]).ToList();
                var ukuran = new int[k];
                for (int i = 0; i < n; i++)
                {
                    int c = anggota[i];
                    ukuran[c]++;
                    for (int j = 0; j < p; j++) jumlah[c][j] += titik[i][j];
                }
                
                for (int c = 0; c < k; c++)
                {
                    if (ukuran[c] == 0) continue;
                    for (int j = 0; j < p; j++) pusat[c][j] = jumlah[c][j] / ukuran[c];
                }

                double w = 0;
                for (int i = 0; i < n; i++) w += Jarak2(titik[i], pusat[anggota[i]]);
                wcssIter.Add(w);

                if (iter > 1 && !berubah) { konvergen = true; break; }
            }

            double wcss = 0;
            for (int i = 0; i < n; i++) wcss += Jarak2(titik[i], pusat[anggota[i]]);
            var ukuranAkhir = new int[k];
            for (int i = 0; i < n; i++) ukuranAkhir[anggota[i]]++;

            return new Hasil
            {
                K = k, Iterasi = iter, Konvergen = konvergen,
                Titik = titik, Pusat = pusat, Anggota = anggota.ToList(),
                Ukuran = ukuranAkhir.ToList(), WcssIterasi = wcssIter, Wcss = wcss
            };
        }

        public static List<ResultBlock> KMeansBlocks(Dataset ds, List<string> vars, int k, int maksIterasi, uint benih)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading("K-Means Cluster", 1) };

            var h = Hitung(ds, vars, k, maksIterasi, benih);
            if (h is null)
            {
                blocks.Add(Blocks.Note("Butuh sedikitnya satu variabel numerik dan baris yang lengkap; "
                                       + "k juga harus minimal 1.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.KMeans));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Variabel", string.Join(", ", vars)),
                ("Banyak klaster (k)", $"{h.K}"),
                ("Baris lengkap dipakai", $"{h.Titik.Count} dari {ds.RowCount}"),
                ("Batas iterasi", $"{Math.Max(1, maksIterasi)}"),
                ("Benih", $"{benih}"),
                ("Pusat awal", "k-means++ (benih tetap, bisa diulang)"),
                ("WCSS akhir", Fmt.Num(h.Wcss))));

            
            var kolomPusat = new List<string> { "Klaster" };
            kolomPusat.AddRange(vars);
            var barisPusat = new List<List<string>>();
            for (int c = 0; c < h.K; c++)
            {
                var r = new List<string> { Fmt.Int(c + 1) };
                foreach (double v in h.Pusat[c]) r.Add(Fmt.Num(v));
                barisPusat.Add(r);
            }
            blocks.Add(Blocks.Table("Pusat klaster akhir", kolomPusat, barisPusat,
                "Nilai mentah, tidak distandardisasi — pastikan variabelnya sebanding."));

            
            var barisUkuran = new List<List<string>>();
            for (int c = 0; c < h.K; c++)
                barisUkuran.Add(new List<string>
                {
                    Fmt.Int(c + 1), Fmt.Int(h.Ukuran[c]),
                    Fmt.Num(h.Titik.Count > 0 ? 100.0 * h.Ukuran[c] / h.Titik.Count : 0, 1)
                });
            blocks.Add(Blocks.Table("Banyak anggota", new[] { "Klaster", "N", "Persen" }, barisUkuran,
                $"Total {h.Titik.Count} amatan."));

            
            var barisIter = new List<List<string>>();
            for (int t = 0; t < h.WcssIterasi.Count; t++)
                barisIter.Add(new List<string> { Fmt.Int(t + 1), Fmt.Num(h.WcssIterasi[t]) });
            blocks.Add(Blocks.Table("Riwayat iterasi", new[] { "Iterasi", "WCSS" }, barisIter,
                (h.Konvergen ? "Konvergen — tak ada titik yang pindah lagi. " : "Berhenti di batas iterasi. ")
                + "WCSS seharusnya tidak pernah naik; kalau naik, berarti ada yang salah."));

            return blocks;
        }
    }
}
