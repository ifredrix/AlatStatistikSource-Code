using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class ErrorBar
    {

        public enum Jenis
        {
            SelangKepercayaan,   
            GalatBaku,           
            SimpanganBaku        
        }

        public sealed class Baris
        {
            public string Label = "";
            public int N;
            public double Rerata = double.NaN;
            public double Sd = double.NaN;      
            public double Se = double.NaN;      
            public double Bawah = double.NaN;   
            public double Atas = double.NaN;    
            public double TGalat = double.NaN;  
        }

        public static List<Baris> Hitung(Dataset ds, string nilai, string? kelompok,
                                         Jenis jenis, double aras = 0.95, double kelipatan = 2.0)
        {
            var hasil = new List<Baris>();
            int iv = ds.IndexOf(nilai);
            if (iv < 0) return hasil;

            var grup = new Dictionary<string, List<double>>();
            var urutan = new List<string>();

            if (string.IsNullOrWhiteSpace(kelompok))
            {
                urutan.Add("Semua");
                grup["Semua"] = new List<double>();
            }

            int ik = string.IsNullOrWhiteSpace(kelompok) ? -1 : ds.IndexOf(kelompok!);

            for (int r = 0; r < ds.RowCount; r++)
            {
                string kunci = "Semua";
                if (ik >= 0)
                {
                    string? k = ik < ds.Rows[r].Length ? ds.Rows[r][ik] : null;
                    if (string.IsNullOrWhiteSpace(k)) continue;
                    kunci = k!;
                }

                if (!grup.TryGetValue(kunci, out var daftar))
                {
                    daftar = new List<double>();
                    grup[kunci] = daftar;
                    urutan.Add(kunci);
                }

                double? angka = iv < ds.Rows[r].Length ? Dataset.ToDouble(ds.Rows[r][iv]) : null;
                if (angka.HasValue) daftar.Add(angka.Value);
            }

            foreach (string kunci in urutan)
            {
                var angka = grup[kunci];
                var b = new Baris { Label = kunci, N = angka.Count };
                if (angka.Count == 0) { hasil.Add(b); continue; }

                b.Rerata = angka.Average();
                if (angka.Count > 1)
                {
                    double ragam = angka.Sum(x => (x - b.Rerata) * (x - b.Rerata)) / (angka.Count - 1);
                    b.Sd = Math.Sqrt(ragam);
                    b.Se = b.Sd / Math.Sqrt(angka.Count);
                }

                switch (jenis)
                {
                    case Jenis.SelangKepercayaan:
                        if (!double.IsNaN(b.Se) && aras > 0 && aras < 1)
                        {
                            b.TGalat = Distributions.StudentTInv(1.0 - (1.0 - aras) / 2.0, angka.Count - 1);
                            double setengah = b.TGalat * b.Se;
                            b.Bawah = b.Rerata - setengah;
                            b.Atas = b.Rerata + setengah;
                        }
                        break;

                    case Jenis.GalatBaku:
                        if (!double.IsNaN(b.Se))
                        {
                            b.Bawah = b.Rerata - kelipatan * b.Se;
                            b.Atas = b.Rerata + kelipatan * b.Se;
                        }
                        break;

                    default:    
                        if (!double.IsNaN(b.Sd))
                        {
                            b.Bawah = b.Rerata - kelipatan * b.Sd;
                            b.Atas = b.Rerata + kelipatan * b.Sd;
                        }
                        break;
                }

                hasil.Add(b);
            }

            return hasil;
        }

        public static string NamaJenis(string kode) => kode switch
        {
            "ci" => "Selang kepercayaan rerata",
            "se" => "Lipatan galat baku",
            _ => "Lipatan simpangan baku"
        };

        public static Jenis DariKode(string kode) => kode switch
        {
            "ci" => Jenis.SelangKepercayaan,
            "se" => Jenis.GalatBaku,
            _ => Jenis.SimpanganBaku
        };

        public static List<ResultBlock> ErrorBarBlocks(Dataset ds, string nilai, string? kelompok,
                                                       string kodeJenis, double aras, double kelipatan)
        {
            var blocks = new List<ResultBlock> { Blocks.Heading("Batang galat — rerata dan rentangnya", 1) };

            var jenis = DariKode(kodeJenis);
            var baris = Hitung(ds, nilai, kelompok, jenis, aras, kelipatan);

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.Rerata, DaftarRumus.RagamSampel, DaftarRumus.BatangGalat));

            string keterangan = jenis switch
            {
                Jenis.SelangKepercayaan => $"rerata ± t(1−α/2, n−1) × SE, α = {Fmt.Num(1 - aras, 2)}",
                Jenis.GalatBaku => $"rerata ± {Fmt.Num(kelipatan, 1)} × SE",
                _ => $"rerata ± {Fmt.Num(kelipatan, 1)} × SD"
            };

            blocks.Add(Blocks.Substitusi("Pemasukan nilai dari data",
                ("Variabel nilai", nilai),
                ("Variabel kelompok", string.IsNullOrWhiteSpace(kelompok) ? "(tidak ada — satu kelompok)" : kelompok!),
                ("Jenis rentang", $"{NamaJenis(kodeJenis)} — {keterangan}"),
                ("Banyak kelompok", $"{baris.Count}")));

            if (baris.Count == 0 || (baris.Count == 1 && baris[0].N == 0))
            {
                blocks.Add(Blocks.Note($"Variabel '{nilai}' tidak punya nilai angka yang bisa dihitung.", NoteKind.Error));
                return blocks;
            }

            var kolom = new[] { "Kelompok", "N", "Rerata", "Simp. baku", "Galat baku", "Bawah", "Atas" };
            var tabel = new List<List<string>>();
            foreach (var b in baris)
                tabel.Add(new List<string>
                {
                    b.Label, Fmt.Int(b.N), Fmt.Num(b.Rerata), Fmt.Num(b.Sd), Fmt.Num(b.Se),
                    Fmt.Num(b.Bawah), Fmt.Num(b.Atas)
                });
            blocks.Add(Blocks.Table("Rerata dan rentang tiap kelompok", kolom, tabel,
                "Simpangan baku memakai ddof = 1. Galat baku = SD/√n. "
                + (jenis == Jenis.SelangKepercayaan
                    ? "Rentang memakai sebaran t dengan derajat bebas n−1."
                    : "Rentang memakai lipatan yang dipilih.")));

            var spec = new ChartSpec
            {
                Kind = ChartKind.ErrorBar,
                Title = $"{nilai}" + (string.IsNullOrWhiteSpace(kelompok) ? "" : $" per {kelompok}"),
                XTitle = string.IsNullOrWhiteSpace(kelompok) ? "" : kelompok!,
                YTitle = keterangan
            };
            foreach (var b in baris) spec.ErrorBars.Add((b.Label, b.Rerata, b.Bawah, b.Atas));

            blocks.Add(Blocks.Chart($"Batang galat — {nilai}", spec));

            return blocks;
        }
    }
}
