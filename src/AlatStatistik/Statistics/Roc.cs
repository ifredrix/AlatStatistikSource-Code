using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Roc
    {
        public class TitikRoc
        {
            public double Ambang;
            public double Sensitivitas;
            public double Spesifisitas;
            public double SatuSpesifisitas;   
            public int PositifBenar;
            public int NegatifSalah;
        }

        public class HasilRoc
        {
            public int NP;                 
            public int NN;                 
            public double Auc;             
            public double AucMannWhitney;  
            public double U;               
            public double Se;              
            public double Bawah, Atas;     
            public double AmbangTerbaik;
            public double YoudenTerbaik;
            public double SensTerbaik, SpekTerbaik;
            public List<TitikRoc> Titik = new();
            public string LabelPositif = "";
        }

        public static double AucMannWhitney(List<double> positif, List<double> negatif, out double u)
        {
            double jumlah = 0;
            foreach (double p in positif)
                foreach (double n in negatif)
                {
                    if (p > n) jumlah += 1.0;
                    else if (Math.Abs(p - n) < 1e-12) jumlah += 0.5;
                }
            u = jumlah;
            return positif.Count * negatif.Count > 0 ? jumlah / (positif.Count * negatif.Count) : double.NaN;
        }

        public static double GalatBakuHanley(double a, int nPos, int nNeg)
        {
            if (double.IsNaN(a) || nPos <= 0 || nNeg <= 0) return double.NaN;
            double q1 = a / (2.0 - a);
            double q2 = 2.0 * a * a / (1.0 + a);
            double dalam = (a * (1.0 - a)
                            + (nPos - 1) * (q1 - a * a)
                            + (nNeg - 1) * (q2 - a * a)) / (nPos * nNeg);
            return dalam > 0 ? Math.Sqrt(dalam) : 0;
        }

        public static HasilRoc? Hitung(List<double> skor, List<int> emas, double aras = 0.95)
        {
            if (skor is null || emas is null || skor.Count != emas.Count) return null;

            var pasangan = new List<(double Skor, int Emas)>();
            for (int i = 0; i < skor.Count; i++)
                if (!double.IsNaN(skor[i])) pasangan.Add((skor[i], emas[i]));

            int nP = pasangan.Count(p => p.Emas == 1);
            int nN = pasangan.Count(p => p.Emas == 0);
            if (nP == 0 || nN == 0) return null;

            var hasil = new HasilRoc { NP = nP, NN = nN };

            var positif = pasangan.Where(p => p.Emas == 1).Select(p => p.Skor).ToList();
            var negatif = pasangan.Where(p => p.Emas == 0).Select(p => p.Skor).ToList();
            hasil.AucMannWhitney = AucMannWhitney(positif, negatif, out double u);
            hasil.U = u;

            
            var ambang = pasangan.Select(p => p.Skor).Distinct().OrderByDescending(v => v).ToList();
            var titik = new List<TitikRoc>
            {
                new() { Ambang = double.PositiveInfinity, Sensitivitas = 0, Spesifisitas = 1,
                        SatuSpesifisitas = 0, PositifBenar = 0, NegatifSalah = 0 }
            };

            foreach (double b in ambang)
            {
                int tp = pasangan.Count(p => p.Emas == 1 && p.Skor >= b);
                int fp = pasangan.Count(p => p.Emas == 0 && p.Skor >= b);
                titik.Add(new TitikRoc
                {
                    Ambang = b,
                    Sensitivitas = tp / (double)nP,
                    Spesifisitas = (nN - fp) / (double)nN,
                    SatuSpesifisitas = fp / (double)nN,
                    PositifBenar = tp,
                    NegatifSalah = fp
                });
            }
            titik.Add(new TitikRoc
            {
                Ambang = double.NegativeInfinity, Sensitivitas = 1, Spesifisitas = 0,
                SatuSpesifisitas = 1, PositifBenar = nP, NegatifSalah = nN
            });
            hasil.Titik = titik;

            
            double luas = 0;
            for (int i = 0; i + 1 < titik.Count; i++)
            {
                double lebar = titik[i + 1].SatuSpesifisitas - titik[i].SatuSpesifisitas;
                luas += lebar * (titik[i].Sensitivitas + titik[i + 1].Sensitivitas) / 2.0;
            }
            hasil.Auc = luas;

            hasil.Se = GalatBakuHanley(hasil.Auc, nP, nN);
            double z = Distributions.NormalInv(1.0 - (1.0 - aras) / 2.0);
            hasil.Bawah = Math.Clamp(hasil.Auc - z * hasil.Se, 0, 1);
            hasil.Atas = Math.Clamp(hasil.Auc + z * hasil.Se, 0, 1);

            
            double terbaik = double.NegativeInfinity;
            foreach (var t in titik)
            {
                double j = t.Sensitivitas + t.Spesifisitas - 1.0;
                if (j > terbaik)
                {
                    terbaik = j;
                    hasil.YoudenTerbaik = j;
                    hasil.AmbangTerbaik = t.Ambang;
                    hasil.SensTerbaik = t.Sensitivitas;
                    hasil.SpekTerbaik = t.Spesifisitas;
                }
            }
            return hasil;
        }

        internal static (List<double> Skor, List<int> Emas, string Label)? Ambil(Dataset ds,
                                                                                string varSkor,
                                                                                string varEmas,
                                                                                string nilaiPositif)
        {
            int iSkor = ds.IndexOf(varSkor), iEmas = ds.IndexOf(varEmas);
            if (iSkor < 0 || iEmas < 0) return null;

            var nilai = new List<(string Emas, string Skor)>();
            for (int r = 0; r < ds.RowCount; r++)
            {
                string? s = ds.Rows[r][iSkor];
                string? g = ds.Rows[r][iEmas];
                if (string.IsNullOrWhiteSpace(s) || string.IsNullOrWhiteSpace(g)) continue;
                nilai.Add((g.Trim(), s.Trim()));
            }
            if (nilai.Count < 2) return null;

            var unik = nilai.Select(v => v.Emas).Distinct(StringComparer.Ordinal).ToList();
            string positif;
            if (!string.IsNullOrWhiteSpace(nilaiPositif))
            {
                positif = nilaiPositif.Trim();
                if (!unik.Contains(positif)) return null;
            }
            else if (unik.Count >= 2)
            {
                
                
                
                if (unik.Count == 2) positif = unik[1];
                else positif = unik.OrderBy(v => v, StringComparer.Ordinal).Last();
            }
            else return null;

            var skor = new List<double>();
            var emas = new List<int>();
            foreach (var v in nilai)
            {
                double? angka = Dataset.ToDouble(v.Skor);
                if (angka is null) continue;
                skor.Add(angka.Value);
                emas.Add(string.Equals(v.Emas, positif, StringComparison.Ordinal) ? 1 : 0);
            }
            if (skor.Count < 2) return null;
            return (skor, emas, positif);
        }

        

        public static List<ResultBlock> RocBlocks(Dataset ds, string varSkor, string varEmas,
                                                  string nilaiPositif, double aras = 0.95)
        {
            var blok = new List<ResultBlock>
            {
                Blocks.Heading($"Kurva ROC — {varSkor} terhadap {varEmas}", 1)
            };

            var diambil = Ambil(ds, varSkor, varEmas, nilaiPositif);
            if (diambil is null)
            {
                blok.Add(Blocks.Note(
                    "Kurva ROC butuh satu variabel angka (penanda/skor) dan satu variabel keadaan "
                    + "dengan dua macam nilai yang tidak kosong. Tentukan juga nilai mana yang "
                    + "dianggap keadaan positif.",
                    NoteKind.Error));
                return blok;
            }

            var h = Hitung(diambil.Value.Skor, diambil.Value.Emas, aras);
            if (h is null)
            {
                blok.Add(Blocks.Note("Kedua keadaan harus sama-sama punya amatan (n₁ > 0 dan n₀ > 0).",
                                     NoteKind.Error));
                return blok;
            }
            h.LabelPositif = diambil.Value.Label;

            blok.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.KurvaRoc, DaftarRumus.AucMannWhitney, DaftarRumus.HanleyMcNeil));

            blok.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Keadaan positif", $"nilai '{h.LabelPositif}' — n₁ = {h.NP}"),
                ("Keadaan negatif", $"n₀ = {h.NN}"),
                ("Statistik U Mann–Whitney", $"U = {Fmt.Num(h.U, 0)}"),
                ("AUC jalur peringkat", $"U / (n₁·n₀) = {Fmt.Num(h.U, 0)} / ({h.NP}·{h.NN}) = {Fmt.Num(h.AucMannWhitney, 6)}"),
                ("AUC jalur trapesium", $"Σ Δ(1−spes)·(sensᵢ + sensᵢ₊₁)/2 = {Fmt.Num(h.Auc, 6)}"),
                ("Selisih dua jalur itu", Fmt.Num(Math.Abs(h.Auc - h.AucMannWhitney), 12)),
                ("Q₁ Hanley–McNeil", $"A/(2 − A) = {Fmt.Num(h.Auc / (2 - h.Auc), 6)}"),
                ("Q₂ Hanley–McNeil", $"2A²/(1 + A) = {Fmt.Num(2 * h.Auc * h.Auc / (1 + h.Auc), 6)}"),
                ("Galat baku", $"SE = {Fmt.Num(h.Se, 6)}"),
                ("Youden J terbaik", $"J = sens + spek − 1 = {Fmt.Num(h.YoudenTerbaik, 6)}")));

            string tafsir = h.Auc >= 0.9 ? "sangat baik" :
                            h.Auc >= 0.8 ? "baik" :
                            h.Auc >= 0.7 ? "cukup" :
                            h.Auc >= 0.6 ? "lemah" : "gagal";
            blok.Add(Blocks.Table("Luas di bawah kurva (AUC)",
                new[] { "Ukuran", "Nilai" },
                new List<List<string>>
                {
                    new() { "AUC", Fmt.Num(h.Auc, 4) },
                    new() { "Tafsir", tafsir },
                    new() { "Galat baku (Hanley–McNeil)", Fmt.Num(h.Se, 4) },
                    new() { $"Selang {Fmt.Num(aras * 100, 0)}% bawah", Fmt.Num(h.Bawah, 4) },
                    new() { $"Selang {Fmt.Num(aras * 100, 0)}% atas", Fmt.Num(h.Atas, 4) },
                    new() { "Ambang terbaik (Youden J)", Fmt.Num(h.AmbangTerbaik, 4) },
                    new() { "Sensitivitas pada ambang itu", Fmt.Num(h.SensTerbaik, 4) },
                    new() { "Spesifisitas pada ambang itu", Fmt.Num(h.SpekTerbaik, 4) },
                    new() { "Youden J", Fmt.Num(h.YoudenTerbaik, 4) }
                },
                h.Bawah > 0.5
                    ? $"Penanda ini memisahkan kedua keadaan lebih baik daripada lempar koin "
                      + $"(batas bawah selang {Fmt.Num(h.Bawah, 4)} > 0,5)."
                    : $"Selang kepercayaannya masih mencakup 0,5 (batas bawah {Fmt.Num(h.Bawah, 4)}) — "
                      + "belum cukup bukti bahwa penanda ini memisahkan kedua keadaan."));

            var barisTitik = new List<List<string>>();
            foreach (var t in h.Titik)
                barisTitik.Add(new List<string>
                {
                    double.IsInfinity(t.Ambang) ? (double.IsPositiveInfinity(t.Ambang) ? "+∞" : "−∞") : Fmt.Num(t.Ambang, 4),
                    Fmt.Num(t.Sensitivitas, 4),
                    Fmt.Num(t.Spesifisitas, 4),
                    Fmt.Num(t.SatuSpesifisitas, 4),
                    t.PositifBenar.ToString(),
                    t.NegatifSalah.ToString()
                });
            blok.Add(Blocks.Table($"Koordinat kurva ({barisTitik.Count} titik)",
                new[] { "Ambang", "Sensitivitas", "Spesifisitas", "1 − spesifisitas", "TP", "FP" },
                barisTitik,
                "Satu baris per ambang yang berbeda; baris pertama dan terakhir adalah ujung kurva."));

            var spec = new ChartSpec
            {
                Kind = ChartKind.Roc,
                Title = $"Kurva ROC (AUC = {Fmt.Num(h.Auc, 3)})",
                XTitle = "1 − spesifisitas",
                YTitle = "Sensitivitas"
            };
            foreach (var t in h.Titik) spec.Points.Add((t.SatuSpesifisitas, t.Sensitivitas));
            blok.Add(Blocks.Chart("Kurva ROC", spec));

            blok.Add(Blocks.Note(
                "AUC dihitung dua jalur yang berbeda dan keduanya dicetak di atas supaya bisa "
                + "diperiksa sendiri; yang dibandingkan dengan SciPy (`scipy.stats.mannwhitneyu`) "
                + "adalah jalur peringkatnya — lihat `docs/acuan_roc.json`. Galat baku memakai "
                + "Hanley–McNeil, bukan rumus proporsi, karena AUC bukan proporsi biasa."));

            return blok;
        }
    }
}
