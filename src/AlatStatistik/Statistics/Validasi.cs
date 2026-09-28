using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class Validasi
    {
        public sealed class Pelanggaran
        {
            public int Baris;
            public string Variabel = "";
            public string Aturan = "";
            public string Nilai = "";
        }

        public sealed class Hasil
        {
            public int N;
            public List<Pelanggaran> Daftar = new();
            public Dictionary<string, int> Ringkasan = new();
        }

        public static bool UraiRentang(string teks,
            out List<(string Var, double Min, double Maks)> aturan, out string galat)
        {
            aturan = new List<(string, double, double)>();
            galat = "";
            foreach (string bagian in teks.Split(';'))
            {
                string b = bagian.Trim();
                if (b.Length == 0) continue;
                string[] tok = b.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (tok.Length != 3
                    || !double.TryParse(tok[1], NumberStyles.Float,
                                        CultureInfo.InvariantCulture, out double mn)
                    || !double.TryParse(tok[2], NumberStyles.Float,
                                        CultureInfo.InvariantCulture, out double mx))
                {
                    galat = $"rentang: perlu 'var min maks', bermasalah di '{b}'.";
                    return false;
                }
                aturan.Add((tok[0], mn, mx));
            }
            return true;
        }

        public static bool UraiAras(string teks,
            out Dictionary<string, List<string>> aras, out string galat)
        {
            aras = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            galat = "";
            foreach (string bagian in teks.Split(';'))
            {
                string b = bagian.Trim();
                if (b.Length == 0) continue;
                int sp = b.IndexOf(' ');
                if (sp < 0)
                {
                    galat = $"aras: perlu 'var a,b,c', bermasalah di '{b}'.";
                    return false;
                }
                string var = b.Substring(0, sp).Trim();
                var lv = b.Substring(sp + 1).Split(',')
                          .Select(t => t.Trim()).ToList();
                if (var.Length == 0 || lv.Count == 0)
                {
                    galat = $"aras: perlu 'var a,b,c', bermasalah di '{b}'.";
                    return false;
                }
                aras[var] = lv;
            }
            return true;
        }

        public static Hasil? Pasang(Dictionary<string, string?[]> kolom,
                                    List<string> angka, List<string> kategori,
                                    string kunci, string teksRentang, string teksAras)
        {
            if (!UraiRentang(teksRentang, out var rentang, out _)) return null;
            if (!UraiAras(teksAras, out var aras, out _)) return null;
            if (!kolom.TryGetValue(kunci, out var kolKunci)) return null;
            int n = kolKunci.Length;
            foreach (string v in angka.Concat(kategori))
                if (!kolom.TryGetValue(v, out var c) || c.Length != n) return null;

            var hitung = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string? raw in kolKunci)
            {
                string t = (raw ?? "").Trim();
                if (t.Length == 0) continue;
                hitung[t] = hitung.TryGetValue(t, out int c) ? c + 1 : 1;
            }

            var h = new Hasil { N = n };
            void Catat(int i, string v, string aturan, string nilai)
            {
                h.Daftar.Add(new Pelanggaran
                {
                    Baris = i + 1, Variabel = v, Aturan = aturan, Nilai = nilai
                });
            }

            for (int i = 0; i < n; i++)
            {
                foreach (string v in angka)
                {
                    string t = (kolom[v][i] ?? "").Trim();
                    if (t.Length == 0) { Catat(i, v, "hilang", t); continue; }
                    if (!double.TryParse(t, NumberStyles.Float,
                                         CultureInfo.InvariantCulture, out double x))
                    {
                        Catat(i, v, "format", t);
                        continue;
                    }
                    foreach (var (rv, mn, mx) in rentang)
                        if (rv == v && (x < mn || x > mx))
                            Catat(i, v, "rentang", t);
                }
                foreach (string v in kategori)
                {
                    string t = (kolom[v][i] ?? "").Trim();
                    if (t.Length == 0) { Catat(i, v, "hilang", t); continue; }
                    if (aras.TryGetValue(v, out var lv) && !lv.Contains(t))
                        Catat(i, v, "aras", t);
                }
                {
                    string t = (kolKunci[i] ?? "").Trim();
                    if (t.Length == 0) Catat(i, kunci, "hilang", t);
                    else if (hitung.TryGetValue(t, out int c) && c > 1)
                        Catat(i, kunci, "ganda", t);
                }
            }
            h.Ringkasan = new Dictionary<string, int>
            {
                ["hilang"] = 0, ["format"] = 0, ["rentang"] = 0,
                ["aras"] = 0, ["ganda"] = 0,
            };
            foreach (var p in h.Daftar) h.Ringkasan[p.Aturan]++;
            return h;
        }

        public static List<ResultBlock> ValidasiBlocks(Dataset ds, List<string> angka,
            List<string> kategori, string kunci, string teksRentang, string teksAras)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("Validasi data (aturan nilai + kunci ganda)", 1)
            };
            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Validasi));

            if (!UraiRentang(teksRentang, out _, out string g1))
            {
                blocks.Add(Blocks.Note(g1, NoteKind.Error));
                return blocks;
            }
            if (!UraiAras(teksAras, out _, out string g2))
            {
                blocks.Add(Blocks.Note(g2, NoteKind.Error));
                return blocks;
            }
            if (ds.IndexOf(kunci) < 0)
            {
                blocks.Add(Blocks.Note($"Variabel kunci '{kunci}' tidak ada.",
                                       NoteKind.Error));
                return blocks;
            }

            var kolom = new Dictionary<string, string?[]>(StringComparer.Ordinal);
            foreach (string v in angka.Concat(kategori).Concat(new[] { kunci }))
            {
                if (ds.IndexOf(v) < 0)
                {
                    blocks.Add(Blocks.Note($"Variabel '{v}' tidak ada.",
                                           NoteKind.Error));
                    return blocks;
                }
                kolom[v] = ds.Text(v);
            }
            var h = Pasang(kolom, angka, kategori, kunci, teksRentang, teksAras);
            if (h is null)
            {
                blocks.Add(Blocks.Note("Aturan tidak bisa diterapkan.",
                                       NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Variabel angka", string.Join(", ", angka)),
                ("Variabel kategori", string.Join(", ", kategori)),
                ("Kunci", kunci),
                ("Aturan rentang", teksRentang),
                ("Aturan aras", teksAras),
                ("Banyak baris", $"n = {Fmt.Int(h.N)}"),
                ("Pelanggaran", $"{Fmt.Int(h.Daftar.Count)} sel")));

            blocks.Add(Blocks.Table(
                "Ringkasan pelanggaran per jenis",
                new[] { "Jenis", "Banyak" },
                h.Ringkasan.Select(kv =>
                    new[] { kv.Key, Fmt.Int(kv.Value) }).ToArray(),
                "hilang = sel kosong; format = angka tak-terurai; rentang = di "
                + "luar min–maks; aras = di luar daftar; ganda = kunci berulang."));

            if (h.Daftar.Count > 0)
                blocks.Add(Blocks.Table(
                    "Daftar pelanggaran (semua, tanpa diringkas)",
                    new[] { "Baris", "Variabel", "Aturan", "Nilai" },
                    h.Daftar.Select(p => new[]
                    {
                        Fmt.Int(p.Baris), p.Variabel, p.Aturan,
                        p.Nilai.Length == 0 ? "(kosong)" : p.Nilai
                    }).ToArray(),
                    "Urut baris menaik; kunci hilang dikecualikan dari uji ganda."));
            else
                blocks.Add(Blocks.Note("Bersih: tidak ada pelanggaran.",
                                       NoteKind.Ok));

            return blocks;
        }
    }
}
