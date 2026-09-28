using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Hierarki
    {
        public class LangkahGabung
        {
            public int Kiri;      
            public int Kanan;
            public double Tinggi;
            public int Ukuran;
        }

        public class HasilHierarki
        {
            public int N;
            public int P;
            public List<string> Variabel = new();
            public List<string> Label = new();        
            public string Metode = "ward";
            public bool Standar;

            public double[,] Jarak = new double[0, 0];
            public List<LangkahGabung> Penggabungan = new();
            public double[,] Kofenatik = new double[0, 0];
            public double KorelasiKofenatik = double.NaN;
            public int[] Anggota = Array.Empty<int>();   
            public int JumlahKlaster;
            public List<int> UrutanDaun = new();
        }

        public static readonly string[] CaraGabung = { "ward", "single", "complete", "average", "centroid", "median" };

        

        internal static double[,] MatriksJarak(double[,] x)
        {
            int n = x.GetLength(0), p = x.GetLength(1);
            var d = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    double s = 0;
                    for (int c = 0; c < p; c++) { double e = x[i, c] - x[j, c]; s += e * e; }
                    double jarak = Math.Sqrt(s);
                    d[i, j] = jarak;
                    d[j, i] = jarak;
                }
            return d;
        }

        internal static List<LangkahGabung> Gabung(double[,] jarak, string metode)
        {
            int n = jarak.GetLength(0);
            
            
            
            int kap = 2 * n;
            var d = new double[kap, kap];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) d[i, j] = jarak[i, j];

            var ukuran = new int[kap];
            var aktif = new bool[kap];
            for (int i = 0; i < n; i++) { ukuran[i] = 1; aktif[i] = true; }

            var langkah = new List<LangkahGabung>();
            int idBerikut = n;

            for (int langkahKe = 0; langkahKe < n - 1; langkahKe++)
            {
                int a = -1, b = -1;
                double terdekat = double.MaxValue;
                for (int i = 0; i < idBerikut; i++)
                {
                    if (!aktif[i]) continue;
                    for (int j = i + 1; j < idBerikut; j++)
                    {
                        if (!aktif[j]) continue;
                        if (d[i, j] < terdekat - 1e-12) { terdekat = d[i, j]; a = i; b = j; }
                    }
                }
                if (a < 0 || b < 0) break;

                int idBaru = idBerikut;
                aktif[a] = false; aktif[b] = false;
                aktif[idBaru] = true;
                ukuran[idBaru] = ukuran[a] + ukuran[b];

                
                
                
                
                
                for (int k = 0; k < idBerikut; k++)
                {
                    if (!aktif[k] || k == idBaru) continue;
                    d[idBaru, k] = JarakBaru(metode, d[a, k], d[b, k], d[a, b],
                                              ukuran[a], ukuran[b], ukuran[k]);
                    d[k, idBaru] = d[idBaru, k];
                }

                langkah.Add(new LangkahGabung
                {
                    Kiri = a, Kanan = b, Tinggi = terdekat, Ukuran = ukuran[idBaru]
                });
                idBerikut++;
            }
            return langkah;
        }

        internal static double JarakBaru(string metode, double dik, double djk, double dij,
                                         int ni, int nj, int nk)
        {
            switch (metode)
            {
                case "single":
                    return Math.Min(dik, djk);

                case "complete":
                    return Math.Max(dik, djk);

                case "average":
                    return (ni * dik + nj * djk) / (double)(ni + nj);

                case "centroid":
                    {
                        double nilai = (ni * dik * dik + nj * djk * djk) / (double)(ni + nj)
                                       - (double)(ni * nj) * dij * dij / ((double)(ni + nj) * (ni + nj));
                        return Math.Sqrt(Math.Max(0, nilai));
                    }

                case "median":
                    {
                        double nilai = 0.5 * dik * dik + 0.5 * djk * djk - 0.25 * dij * dij;
                        return Math.Sqrt(Math.Max(0, nilai));
                    }

                default:   
                    {
                        double nilai = ((ni + nk) * dik * dik + (nj + nk) * djk * djk - nk * dij * dij)
                                       / (double)(ni + nj + nk);
                        return Math.Sqrt(Math.Max(0, nilai));
                    }
            }
        }

        internal static double[,] Kofenatik(int n, List<LangkahGabung> langkah)
        {
            var kof = new double[n, n];
            var anggota = new List<int>[n + langkah.Count];
            for (int i = 0; i < n; i++) anggota[i] = new List<int> { i };

            for (int m = 0; m < langkah.Count; m++)
            {
                var g = langkah[m];
                int id = n + m;
                foreach (int i in anggota[g.Kiri])
                    foreach (int j in anggota[g.Kanan])
                    {
                        kof[i, j] = g.Tinggi;
                        kof[j, i] = g.Tinggi;
                    }
                var gab = new List<int>(anggota[g.Kiri]);
                gab.AddRange(anggota[g.Kanan]);
                anggota[id] = gab;
            }
            return kof;
        }

        internal static int[] Potong(int n, List<LangkahGabung> langkah, int k)
        {
            var induk = new int[n + langkah.Count];
            for (int i = 0; i < induk.Length; i++) induk[i] = i;

            int Akar(int x)
            {
                while (induk[x] != x) { induk[x] = induk[induk[x]]; x = induk[x]; }
                return x;
            }

            int pakai = Math.Clamp(n - k, 0, langkah.Count);
            for (int m = 0; m < pakai; m++)
            {
                int a = Akar(langkah[m].Kiri), b = Akar(langkah[m].Kanan);
                if (a != b) induk[b] = a;

                
                
                
                
                
                
                
                
                induk[n + m] = Akar(langkah[m].Kiri);
            }

            var nama = new Dictionary<int, int>();
            var hasil = new int[n];
            int berikut = 1;
            for (int i = 0; i < n; i++)
            {
                int akar = Akar(i);
                if (!nama.TryGetValue(akar, out int nomor)) { nomor = berikut++; nama[akar] = nomor; }
                hasil[i] = nomor;
            }
            return hasil;
        }

        internal static List<int> SusunDaun(int n, List<LangkahGabung> langkah)
        {
            var urutan = new List<int>();
            void Jelajah(int id)
            {
                if (id < n) { urutan.Add(id); return; }
                var g = langkah[id - n];
                Jelajah(g.Kiri);
                Jelajah(g.Kanan);
            }
            if (langkah.Count > 0) Jelajah(n + langkah.Count - 1);
            else for (int i = 0; i < n; i++) urutan.Add(i);
            return urutan;
        }

        public static HasilHierarki? Hitung(Dataset ds, List<string> vars, string metode = "ward",
                                            int jumlahKlaster = 2, bool standar = true)
        {
            if (ds is null || vars is null || vars.Count < 1) return null;

            var idx = vars.Select(v => ds.IndexOf(v)).ToList();
            if (idx.Any(i => i < 0)) return null;

            var baris = ds.CompleteRows(vars);
            int n = baris.Count;
            if (n < 3) return null;

            var x = new double[n, vars.Count];
            for (int r = 0; r < n; r++)
                for (int c = 0; c < vars.Count; c++)
                {
                    double? v = Dataset.ToDouble(ds.Rows[baris[r]][idx[c]]);
                    if (v is null) return null;
                    x[r, c] = v.Value;
                }

            var data = x;
            if (standar)
            {
                data = (double[,])x.Clone();
                for (int c = 0; c < vars.Count; c++)
                {
                    double jumlah = 0;
                    for (int r = 0; r < n; r++) jumlah += data[r, c];
                    double rerata = jumlah / n;
                    double jk = 0;
                    for (int r = 0; r < n; r++) { double e = data[r, c] - rerata; jk += e * e; }
                    double s = Math.Sqrt(jk / (n - 1));
                    if (!(s > 1e-12)) continue;         
                    for (int r = 0; r < n; r++) data[r, c] = (data[r, c] - rerata) / s;
                }
            }

            string cara = (metode ?? "ward").Trim().ToLowerInvariant();
            if (!CaraGabung.Contains(cara)) cara = "ward";

            var jarak = MatriksJarak(data);
            var langkah = Gabung(jarak, cara);
            if (langkah.Count == 0) return null;

            var kof = Kofenatik(n, langkah);

            var pasangan = new List<double>();
            var kofPasangan = new List<double>();
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++) { pasangan.Add(jarak[i, j]); kofPasangan.Add(kof[i, j]); }
            double rKof = Correlation.Pearson(pasangan, kofPasangan);

            int k = Math.Clamp(jumlahKlaster, 1, n);
            var anggota = Potong(n, langkah, k);

            return new HasilHierarki
            {
                N = n, P = vars.Count, Variabel = vars.ToList(),
                Label = baris.Select(b => "baris " + (b + 1)).ToList(),
                Metode = cara, Standar = standar,
                Jarak = jarak, Penggabungan = langkah, Kofenatik = kof,
                KorelasiKofenatik = rKof,
                Anggota = anggota, JumlahKlaster = k,
                UrutanDaun = SusunDaun(n, langkah)
            };
        }

        

        public static List<ResultBlock> HierarkiBlocks(Dataset ds, List<string> vars, string metode,
                                                       int jumlahKlaster, bool standar)
        {
            string namaCara = (metode ?? "ward").Trim().ToLowerInvariant() switch
            {
                "single" => "single linkage (tetangga terdekat)",
                "complete" => "complete linkage (tetangga terjauh)",
                "average" => "average linkage (UPGMA)",
                "centroid" => "centroid (UPGMC)",
                "median" => "median (WPGMC)",
                _ => "Ward"
            };

            var blok = new List<ResultBlock>
            {
                Blocks.Heading($"Pengelompokan hierarkis — {namaCara}", 1)
            };

            
            
            metode = string.IsNullOrEmpty(metode) ? "ward" : metode.Trim().ToLowerInvariant();

            var h = Hitung(ds, vars, metode, jumlahKlaster, standar);
            if (h is null)
            {
                blok.Add(Blocks.Note(
                    "Pengelompokan butuh sedikitnya 3 baris lengkap dan sedikitnya satu variabel angka.",
                    NoteKind.Error));
                return blok;
            }

            blok.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.JarakEuclid, DaftarRumus.LanceWilliams, DaftarRumus.KorelasiKofenatik));

            int banyakPasangan = h.N * (h.N - 1) / 2;
            double tinggiAkhir = h.Penggabungan.Count > 0 ? h.Penggabungan[^1].Tinggi : 0;
            blok.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Amatan yang dipakai (baris lengkap)", $"n = {h.N}"),
                ("Variabel yang dipakai", $"p = {h.P} — {string.Join(", ", h.Variabel)}"),
                ("Penyeragaman", h.Standar ? "z-skor tiap variabel (rerata 0, simpangan baku 1)"
                                           : "nilai mentah apa adanya"),
                ("Banyak pasangan jarak", $"n(n − 1)/2 = {h.N}·{h.N - 1}/2 = {banyakPasangan}"),
                ("Cara penggabungan", namaCara),
                ("Langkah penggabungan", $"{h.Penggabungan.Count} (selalu n − 1 = {h.N - 1})"),
                ("Tinggi penggabungan terakhir", $"d = {Fmt.Num(tinggiAkhir, 4)}"),
                ("Korelasi kofenatik", $"r = {Fmt.Num(h.KorelasiKofenatik, 4)}"),
                ("Potongan klaster", $"k = {h.JumlahKlaster}")));

            
            var barisGabung = new List<List<string>>();
            for (int m = 0; m < h.Penggabungan.Count; m++)
            {
                var g = h.Penggabungan[m];
                barisGabung.Add(new List<string>
                {
                    (m + 1).ToString(),
                    NamaSimpul(g.Kiri, h.N),
                    NamaSimpul(g.Kanan, h.N),
                    Fmt.Num(g.Tinggi, 4),
                    g.Ukuran.ToString()
                });
            }
            blok.Add(Blocks.Table("Langkah penggabungan (matriks linkage)",
                new[] { "Langkah", "Klaster 1", "Klaster 2", "Jarak", "Anggota" },
                barisGabung,
                "Baris ke-m memakai nomor simpul: 1..n untuk amatan, dan n+m untuk klaster hasil "
                + "langkah ke-m — sama penomorannya dengan SciPy."));

            
            var perKlaster = new Dictionary<int, List<int>>();
            for (int i = 0; i < h.N; i++)
            {
                if (!perKlaster.TryGetValue(h.Anggota[i], out var daftar)) { daftar = new List<int>(); perKlaster[h.Anggota[i]] = daftar; }
                daftar.Add(i);
            }
            var barisKlaster = new List<List<string>>();
            foreach (var kv in perKlaster.OrderBy(k => k.Key))
                barisKlaster.Add(new List<string>
                {
                    kv.Key.ToString(),
                    kv.Value.Count.ToString(),
                    string.Join(", ", kv.Value.Take(12).Select(i => h.Label[i]))
                        + (kv.Value.Count > 12 ? $", … (+{kv.Value.Count - 12})" : "")
                });
            blok.Add(Blocks.Table($"Anggota klaster pada potongan k = {h.JumlahKlaster}",
                new[] { "Klaster", "Banyak anggota", "Anggota" }, barisKlaster,
                "Penomoran klaster mengikuti urutan kemunculan amatan."));

            blok.Add(Blocks.Table("Mutu pohon",
                new[] { "Ukuran", "Nilai" },
                new List<List<string>>
                {
                    new() { "Korelasi kofenatik", Fmt.Num(h.KorelasiKofenatik, 4) },
                    new() { "Cara penggabungan", namaCara },
                    new() { "Penyeragaman", h.Standar ? "z-skor" : "mentah" }
                },
                h.KorelasiKofenatik >= 0.75
                    ? $"Pohonnya setia pada jarak aslinya (r = {Fmt.Num(h.KorelasiKofenatik, 4)} ≥ 0,75)."
                    : $"Pohonnya cukup menyimpang dari jarak aslinya (r = {Fmt.Num(h.KorelasiKofenatik, 4)} "
                      + "< 0,75); pertimbangkan cara penggabungan lain."));

            var spec = new ChartSpec
            {
                Kind = ChartKind.Dendrogram,
                Title = $"Dendrogram ({namaCara})",
                XTitle = "Amatan",
                YTitle = "Jarak"
            };
            foreach (var g in h.Penggabungan) spec.Penggabungan.Add((g.Kiri, g.Kanan, g.Tinggi));
            
            spec.LabelDaun.AddRange(h.UrutanDaun.Select(i => h.Label[i]));
            blok.Add(Blocks.Chart("Dendrogram", spec));

            blok.Add(Blocks.Note(
                "Matriks linkage, jarak kofenatik, dan potongan klasternya dibandingkan dengan "
                + "SciPy (`scipy.cluster.hierarchy.linkage`, `cophenet`, `fcluster`) pada data yang "
                + "sama — lihat `docs/acuan_hierarki.json`. Data uji sengaja tanpa jarak kembar: "
                + "bila ada dua pasangan berjarak persis sama, urutan penggabungannya tidak tunggal "
                + "dan SciPy boleh memilih pasangan yang berbeda dari kita (hasil akhirnya tetap "
                + "sah, hanya urutan langkahnya yang berganti)."));

            return blok;
        }

        internal static string NamaSimpul(int id, int n)
            => id < n ? (id + 1).ToString() : $"C{id - n + 1}";
    }
}
