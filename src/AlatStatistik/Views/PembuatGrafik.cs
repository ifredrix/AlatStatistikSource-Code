using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;
using AlatStatistik.Statistics;

namespace AlatStatistik.Views
{

    public enum JenisGrafik
    {
        Histogram,
        BoxPlot,
        Pencar,
        Batang,
        Garis,
        Lingkaran,
        PetaPanas
    }

    public static class PembuatGrafik
    {

        public const int BatasBatang = 20;

        public static List<ResultBlock> Buat(JenisGrafik jenis, Dataset ds, List<string> kolom,
                                            string kelompok = "", bool garisRegresi = false)
            => jenis switch
            {
                JenisGrafik.Histogram => BuatSebaran(ds, kolom, kelompok, false),
                JenisGrafik.BoxPlot => BuatSebaran(ds, kolom, kelompok, true),
                JenisGrafik.Pencar => BuatPencar(ds, kolom, garisRegresi),
                JenisGrafik.Garis => BuatGaris(ds, kolom, kelompok),
                JenisGrafik.Lingkaran => BuatLingkaran(ds, kolom),
                JenisGrafik.PetaPanas => BuatPetaPanas(ds, kolom),
                _ => BuatBatang(ds, kolom)
            };

        

        private static List<ResultBlock> BuatSebaran(Dataset ds, List<string> kolom, string kelompok, bool box)
        {
            string judul = box ? "Box plot" : "Histogram";
            var blok = new List<ResultBlock> { Blocks.Heading(judul, 1) };

            foreach (string nama in kolom)
            {
                int i = ds.IndexOf(nama);
                if (i < 0) continue;

                if (ds.Variables[i].Measure != Measure.Skala)
                {
                    blok.Add(Blocks.Note($"{nama}: dilewati, bukan variabel angka. "
                                         + "Histogram dan box plot butuh angka.", NoteKind.Warning));
                    continue;
                }

                var spec = new ChartSpec
                {
                    Kind = box ? ChartKind.BoxPlot : ChartKind.Histogram,
                    Title = $"{judul} — {nama}",
                    XTitle = nama,
                    YTitle = box ? "Nilai" : "Frekuensi"
                };

                int dipakai = 0;

                if (string.IsNullOrEmpty(kelompok))
                {
                    var nilai = Descriptives.CleanNumbers(ds, nama);
                    spec.Series[nama] = nilai;
                    dipakai = nilai.Count;
                }
                else
                {
                    int k = ds.IndexOf(kelompok);
                    var perKelompok = new Dictionary<string, List<double>>();

                    for (int r = 0; r < ds.Rows.Count; r++)
                    {
                        var v = Dataset.ToDouble(ds.Rows[r][i]);
                        if (!v.HasValue) continue;

                        string g = string.IsNullOrWhiteSpace(ds.Rows[r][k]) ? "(kosong)" : ds.Rows[r][k]!;
                        if (!perKelompok.TryGetValue(g, out var daftar))
                        {
                            daftar = new List<double>();
                            perKelompok[g] = daftar;
                        }
                        daftar.Add(v.Value);
                        dipakai++;
                    }

                    foreach (var kv in perKelompok) spec.Series[kv.Key] = kv.Value;
                }

                if (dipakai == 0)
                {
                    blok.Add(Blocks.Note($"{nama}: tidak ada angka yang bisa digambar.", NoteKind.Warning));
                    continue;
                }

                blok.Add(Blocks.Chart(spec.Title, spec));
                blok.Add(Blocks.Note(spec.Series.Count > 1
                    ? $"{nama}: {dipakai} nilai, dikelompokkan menurut {kelompok} "
                      + $"menjadi {spec.Series.Count} kelompok."
                    : $"{nama}: {dipakai} nilai digambar; nilai kosong dilewati.",
                    NoteKind.Info));
            }

            return blok;
        }

        

        private static List<ResultBlock> BuatPencar(Dataset ds, List<string> kolom, bool garisRegresi)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Diagram pencar", 1) };

            if (kolom.Count < 2)
            {
                blok.Add(Blocks.Note("Diagram pencar butuh dua variabel: pilih satu untuk "
                                     + "sumbu mendatar dan satu untuk sumbu tegak.", NoteKind.Warning));
                return blok;
            }

            string xs = kolom[0], ys = kolom[1];
            var nilaiX = ds.Numeric(xs);
            var nilaiY = ds.Numeric(ys);

            var spec = new ChartSpec
            {
                Kind = ChartKind.Scatter,
                Title = $"Pencar — {xs} vs {ys}",
                XTitle = xs,
                YTitle = ys
            };

            var px = new List<double>();
            var py = new List<double>();

            for (int r = 0; r < ds.Rows.Count; r++)
            {
                
                
                if (!nilaiX[r].HasValue || !nilaiY[r].HasValue) continue;

                px.Add(nilaiX[r]!.Value);
                py.Add(nilaiY[r]!.Value);
                spec.Points.Add((nilaiX[r]!.Value, nilaiY[r]!.Value));
            }

            if (spec.Points.Count == 0)
            {
                blok.Add(Blocks.Note("Tidak ada baris yang kedua variabelnya terisi.", NoteKind.Warning));
                return blok;
            }

            if (garisRegresi && px.Count >= 2)
            {
                double mx = px.Average(), my = py.Average();
                double sxy = 0, sxx = 0;
                for (int i = 0; i < px.Count; i++)
                {
                    sxy += (px[i] - mx) * (py[i] - my);
                    sxx += (px[i] - mx) * (px[i] - mx);
                }

                
                
                double kemiringan = sxx > 0 ? sxy / sxx : 0;
                spec.RegressionLine = (kemiringan, my - kemiringan * mx);
            }

            blok.Add(Blocks.Chart(spec.Title, spec));
            blok.Add(Blocks.Note($"{spec.Points.Count} titik digambar dari {ds.RowCount} baris; "
                                 + "baris yang salah satu nilainya kosong dilewati."
                                 + (garisRegresi
                                     ? " Garisnya adalah garis regresi kuadrat terkecil."
                                     : ""),
                                 NoteKind.Info));

            return blok;
        }

        

        private static List<ResultBlock> BuatBatang(Dataset ds, List<string> kolom)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Diagram batang", 1) };

            foreach (string nama in kolom)
            {
                int i = ds.IndexOf(nama);
                if (i < 0) continue;

                var hitung = new Dictionary<string, int>();
                int kosong = 0;

                foreach (var baris in ds.Rows)
                {
                    string? v = baris[i];
                    if (string.IsNullOrWhiteSpace(v)) { kosong++; continue; }
                    hitung[v!] = hitung.TryGetValue(v!, out int n) ? n + 1 : 1;
                }

                if (hitung.Count == 0)
                {
                    blok.Add(Blocks.Note($"{nama}: tidak ada nilai yang terisi.", NoteKind.Warning));
                    continue;
                }

                var spec = new ChartSpec
                {
                    Kind = ChartKind.Bar,
                    Title = $"Frekuensi — {nama}",
                    XTitle = nama,
                    YTitle = "Frekuensi"
                };

                
                
                foreach (var kv in hitung.OrderByDescending(k => k.Value).Take(BatasBatang))
                    spec.Bars.Add((kv.Key, kv.Value));

                blok.Add(Blocks.Chart(spec.Title, spec));
                blok.Add(Blocks.Note($"{nama}: {hitung.Count} nilai berbeda"
                                     + (hitung.Count > BatasBatang
                                         ? $", hanya {BatasBatang} yang paling sering digambar" : "")
                                     + $"; {kosong} baris kosong dilewati.",
                                     hitung.Count > BatasBatang ? NoteKind.Warning : NoteKind.Info));
            }

            return blok;
        }

        

        private static List<ResultBlock> BuatGaris(Dataset ds, List<string> kolom, string kelompok)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Diagram garis", 1) };

            if (kolom.Count == 0)
            {
                blok.Add(Blocks.Note("Pilih sedikitnya satu variabel.", NoteKind.Warning));
                return blok;
            }

            
            
            
            var semua = new Dictionary<string, List<double>>();

            if (string.IsNullOrEmpty(kelompok))
            {
                foreach (string nama in kolom)
                {
                    int i = ds.IndexOf(nama);
                    if (i < 0) continue;
                    semua[nama] = Descriptives.CleanNumbers(ds, nama);
                }
            }
            else
            {
                int k = ds.IndexOf(kelompok);
                foreach (string nama in kolom)
                {
                    int i = ds.IndexOf(nama);
                    if (i < 0) continue;

                    var perKategori = new Dictionary<string, List<double>>();
                    for (int r = 0; r < ds.Rows.Count; r++)
                    {
                        var v = Dataset.ToDouble(ds.Rows[r][i]);
                        if (!v.HasValue) continue;
                        string g = string.IsNullOrWhiteSpace(ds.Rows[r][k]) ? "(kosong)" : ds.Rows[r][k]!;
                        if (!perKategori.TryGetValue(g, out var daftar))
                        {
                            daftar = new List<double>();
                            perKategori[g] = daftar;
                        }
                        daftar.Add(v.Value);
                    }

                    foreach (var kv in perKategori) semua[$"{nama} ({kv.Key})"] = kv.Value;
                }
            }

            if (semua.Count == 0)
            {
                blok.Add(Blocks.Note("Tidak ada angka yang bisa digambar.", NoteKind.Warning));
                return blok;
            }

            var spec = new ChartSpec
            {
                Kind = ChartKind.Line,
                Title = "Diagram garis",
                XTitle = "Urutan baris",
                YTitle = "Nilai"
            };

            foreach (var kv in semua) spec.Series[kv.Key] = kv.Value;

            blok.Add(Blocks.Chart(spec.Title, spec));

            int panjang = semua.Values.Max(v => v.Count);
            blok.Add(Blocks.Note($"{semua.Count} deret digambar,panjang paling panjang {panjang}. "
                                 + "Nilai kosong dilewati; sumbu X adalah urutan baris di data, "
                                 + "bukan waktu — untuk deret waktu, urutkan datanya dulu.",
                                 NoteKind.Info));

            return blok;
        }

        

        private static List<ResultBlock> BuatLingkaran(Dataset ds, List<string> kolom)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Diagram lingkaran", 1) };

            foreach (string nama in kolom)
            {
                int i = ds.IndexOf(nama);
                if (i < 0) continue;

                var hitung = new Dictionary<string, int>();
                int total = 0;
                foreach (var baris in ds.Rows)
                {
                    string? v = baris[i];
                    if (string.IsNullOrWhiteSpace(v)) continue;
                    hitung[v!] = hitung.TryGetValue(v!, out int n) ? n + 1 : 1;
                    total++;
                }

                if (hitung.Count == 0)
                {
                    blok.Add(Blocks.Note($"{nama}: tidak ada nilai yang terisi.", NoteKind.Warning));
                    continue;
                }

                var spec = new ChartSpec
                {
                    Kind = ChartKind.Pie,
                    Title = $"Komposisi — {nama}",
                    XTitle = nama,
                    YTitle = "",
                    
                    
                    
                    TotalN = total
                };

                foreach (var kv in hitung.OrderByDescending(k => k.Value).Take(BatasBatang))
                    spec.Bars.Add((kv.Key, kv.Value));

                blok.Add(Blocks.Chart(spec.Title, spec));
                blok.Add(Blocks.Note($"{nama}: {hitung.Count} kategori berbeda"
                                     + (hitung.Count > BatasBatang
                                         ? $", hanya {BatasBatang} yang terbesar digambar" : "")
                                     + $" (N = {total}).",
                                     hitung.Count > BatasBatang ? NoteKind.Warning : NoteKind.Info));
            }

            return blok;
        }

        

        private static List<ResultBlock> BuatPetaPanas(Dataset ds, List<string> kolom)
        {
            var blok = new List<ResultBlock> { Blocks.Heading("Peta panas korelasi", 1) };

            var angka = new List<(string nama, List<double> nilai)>();
            foreach (string nama in kolom)
            {
                int i = ds.IndexOf(nama);
                if (i < 0 || ds.Variables[i].Measure != Measure.Skala)
                {
                    blok.Add(Blocks.Note($"{nama}: dilewati, bukan variabel angka.", NoteKind.Warning));
                    continue;
                }
                angka.Add((nama, Descriptives.CleanNumbers(ds, nama)));
            }

            if (angka.Count < 2)
            {
                blok.Add(Blocks.Note("Peta panas butuh sedikitnya dua variabel angka.", NoteKind.Warning));
                return blok;
            }

            
            
            int dipakai = angka[0].nilai.Count;
            foreach (var (_, nilai) in angka) dipakai = Math.Min(dipakai, nilai.Count);
            if (dipakai < 3)
            {
                blok.Add(Blocks.Note("Tidak cukup baris yang lengkap untuk menghitung korelasi.", NoteKind.Warning));
                return blok;
            }

            var spec = new ChartSpec
            {
                Kind = ChartKind.Heatmap,
                Title = $"Peta panas — korelasi Pearson (n = {dipakai})",
                XTitle = "", YTitle = ""
            };

            for (int i = 0; i < angka.Count; i++)
            {
                spec.HeatmapRowLabels.Add(angka[i].nama);
                spec.HeatmapColLabels.Add(angka[i].nama);
                var baris = new List<double>();
                for (int j = 0; j < angka.Count; j++)
                {
                    if (j == i) baris.Add(1.0);
                    else if (j < i) baris.Add(0); 
                    else
                    {
                        var x = angka[i].nilai.Take(dipakai).ToList();
                        var y = angka[j].nilai.Take(dipakai).ToList();
                        baris.Add(Pearson(x, y));
                    }
                }
                spec.Heatmap.Add(baris);
            }

            
            for (int i = 0; i < angka.Count; i++)
                for (int j = 0; j < i; j++) spec.Heatmap[i][j] = spec.Heatmap[j][i];

            blok.Add(Blocks.Chart(spec.Title, spec));
            blok.Add(Blocks.Note(
                $"Diagonal bernilai 1 (variabel dengan dirinya sendiri). "
                + $"Warna lebih tua = |r| lebih kuat. "
                + $"Hanya {dipakai} baris yang dipakai karena korelasi Pearson butuh pasangan lengkap.",
                NoteKind.Info));

            return blok;
        }

        private static double Pearson(List<double> x, List<double> y)
        {
            int n = Math.Min(x.Count, y.Count);
            if (n < 3) return 0;
            double mx = 0, my = 0;
            for (int i = 0; i < n; i++) { mx += x[i]; my += y[i]; }
            mx /= n; my /= n;

            double sxx = 0, syy = 0, sxy = 0;
            for (int i = 0; i < n; i++)
            {
                double dx = x[i] - mx, dy = y[i] - my;
                sxx += dx * dx; syy += dy * dy; sxy += dx * dy;
            }
            if (sxx <= 0 || syy <= 0) return 0;
            return sxy / Math.Sqrt(sxx * syy);
        }
    }
}
