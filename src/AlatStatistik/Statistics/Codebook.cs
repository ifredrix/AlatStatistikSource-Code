using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Codebook
    {

        public sealed class BarisKamus
        {
            public string Nama = "";
            public string Label = "";
            public string Tipe = "";        
            public string Ukur = "";        
            public int Desimal;
            public int Sah;                 
            public int Hilang;
            public double Min = double.NaN; 
            public double Max = double.NaN;
            public double Rerata = double.NaN;
            public int BanyakKategori;      
            public List<string> ContohKategori = new();   
        }

        private static string NamaUkur(Measure m) => m switch
        {
            Measure.Skala => "Skala",
            Measure.Ordinal => "Ordinal",
            _ => "Nominal"
        };

        public static List<BarisKamus> Hitung(Dataset ds, bool hanyaNumerik)
        {
            var daftar = new List<BarisKamus>();

            for (int i = 0; i < ds.Variables.Count; i++)
            {
                var v = ds.Variables[i];
                if (hanyaNumerik && v.Measure != Measure.Skala) continue;
                bool numerik = v.Measure == Measure.Skala;

                var b = new BarisKamus
                {
                    Nama = v.Name,
                    Label = v.Label,
                    Tipe = numerik ? "Numerik" : "Teks",
                    Ukur = NamaUkur(v.Measure),
                    Desimal = v.Decimals
                };

                if (numerik)
                {
                    var angka = ds.Numeric(v.Name).Where(x => x.HasValue).Select(x => x!.Value).ToList();
                    b.Sah = angka.Count;
                    b.Hilang = ds.RowCount - b.Sah;
                    if (b.Sah > 0)
                    {
                        b.Min = angka.Min();
                        b.Max = angka.Max();
                        b.Rerata = angka.Average();
                    }
                }
                else
                {
                    var teks = ds.Text(v.Name)
                                 .Where(t => !string.IsNullOrWhiteSpace(t))
                                 .Select(t => t ?? "")
                                 .ToList();
                    b.Sah = teks.Count;
                    b.Hilang = ds.RowCount - b.Sah;
                    var unik = teks.Distinct().ToList();
                    b.BanyakKategori = unik.Count;
                    b.ContohKategori = unik.Take(6).ToList();
                }

                daftar.Add(b);
            }

            return daftar;
        }

        public static List<ResultBlock> CodebookBlocks(Dataset ds, bool hanyaNumerik)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading("Codebook — kamus variabel", 1) };

            blocks.Add(Blocks.Rumus("Yang didokumentasikan", DaftarRumus.KamusVariabel));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Banyak variabel", $"{ds.Variables.Count}"),
                ("Banyak baris", $"{ds.RowCount}"),
                ("Hanya numerik", hanyaNumerik ? "ya" : "tidak"),
                ("Catatan", "N sah = sel tak kosong; N hilang = sel kosong/teks pada variabel numerik.")));

            var kolom = new[] { "Variabel", "Label", "Tipe", "Ukur", "Desimal", "N sah", "N hilang", "Ringkasan" };
            var baris = new List<List<string>>();
            var labelNilai = new List<List<string>>();

            foreach (var b in Hitung(ds, hanyaNumerik))
            {
                string ringkas;
                if (b.Tipe == "Numerik")
                {
                    ringkas = b.Sah > 0
                        ? $"min {Fmt.Num(b.Min)}, max {Fmt.Num(b.Max)}, rerata {Fmt.Num(b.Rerata)}"
                        : "—";
                }
                else
                {
                    ringkas = b.ContohKategori.Count > 0
                        ? "kategori: " + string.Join(", ", b.ContohKategori)
                          + (b.BanyakKategori > b.ContohKategori.Count ? ", …" : "")
                        : "—";
                }

                baris.Add(new List<string>
                {
                    b.Nama, b.Label, b.Tipe, b.Ukur,
                    Fmt.Int(b.Desimal), Fmt.Int(b.Sah), Fmt.Int(b.Hilang), ringkas
                });

                var v = ds.Variables.FirstOrDefault(x => x.Name == b.Nama);
                if (v != null && v.ValueLabels.Count > 0)
                {
                    foreach (var kv in v.ValueLabels)
                        labelNilai.Add(new List<string> { v.Name, Fmt.Num(kv.Key), kv.Value });
                }
            }

            blocks.Add(Blocks.Table("Daftar variabel", kolom, baris,
                $"{ds.Variables.Count} variabel, {ds.RowCount} baris. N sah = sel tidak kosong; "
                + $"N hilang = sel kosong/teks pada variabel numerik."));

            if (labelNilai.Count > 0)
                blocks.Add(Blocks.Table("Label nilai", new[] { "Variabel", "Nilai", "Label" }, labelNilai,
                    "Pemetaan nilai numerik ke teks (Value Labels)."));

            return blocks;
        }
    }
}
