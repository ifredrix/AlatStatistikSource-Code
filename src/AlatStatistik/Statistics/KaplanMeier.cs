using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class KaplanMeier
    {
        public sealed class Baris
        {
            public double Waktu;
            public int NRisk;
            public int NEvent;
            public double Survival;
            public double Se;
            public double CiLower;
            public double CiUpper;
        }

        public sealed class HasilGrup
        {
            public string Nama = "";
            public List<Baris> Baris = new();
            public int NEventTotal;
            public int NCensored;
        }

        public sealed class HasilLogRank
        {
            public double Chi2;
            public int Df;
            public double P;
        }

        public sealed class Hasil
        {
            public List<HasilGrup> Grup = new();
            public HasilLogRank? LogRank;
            public int NTotal;
        }

        

        public static Hasil Compute(Dataset ds, string waktu, string status, string? grup = null)
        {
            int iW = ds.IndexOf(waktu);
            int iS = ds.IndexOf(status);
            int iG = grup is not null ? ds.IndexOf(grup) : -1;

            var data = new List<(double T, int E, string? G)>();
            foreach (var row in ds.Rows)
            {
                double? t = Dataset.ToDouble(row[iW]);
                int? e = Dataset.ToDouble(row[iS]) is double v ? (int)Math.Round(v) : null;
                if (!t.HasValue || !e.HasValue) continue;
                string? g = iG >= 0 ? row[iG] : null;
                data.Add((t.Value, e.Value, g));
            }

            var hasil = new Hasil { NTotal = data.Count };

            
            var grupNama = iG >= 0
                ? data.Select(d => d.G).Where(g => !string.IsNullOrEmpty(g)).Distinct().OrderBy(g => g).ToList()
                : new List<string?> { null };

            foreach (var gn in grupNama)
            {
                var sub = data.Where(d => gn is null || d.G == gn).ToList();
                var hg = new HasilGrup
                {
                    Nama = gn ?? "Keseluruhan",
                    NEventTotal = sub.Count(x => x.E == 1),
                    NCensored = sub.Count(x => x.E == 0),
                };
                hg.Baris = KmTable(sub);
                hasil.Grup.Add(hg);
            }

            if (grupNama.Count >= 2)
                hasil.LogRank = LogRank(data, grupNama);

            return hasil;
        }

        private static List<Baris> KmTable(List<(double T, int E, string? G)> data)
        {
            var baris = new List<Baris>();
            double S = 1.0;
            double varSum = 0.0;

            
            
            
            
            // Waktu NaN membuat Math.Abs(NaN - NaN) < 1e-12 selalu salah, penghitung
            // tak pernah maju, dan perulangan menggantung. Amatan tak terhingga dibuang.
            var urut = data.Where(d => double.IsFinite(d.T))
                           .OrderBy(d => d.T).ThenByDescending(d => d.E).ToList();

            int idx = 0;
            while (idx < urut.Count)
            {
                double t = urut[idx].T;
                int nRisk = urut.Count - idx;
                int nEvent = 0, nCens = 0;
                while (idx < urut.Count && Math.Abs(urut[idx].T - t) < 1e-12)
                {
                    if (urut[idx].E == 1) nEvent++;
                    else nCens++;
                    idx++;
                }

                if (nEvent > 0)
                {
                    double d = nEvent;
                    double n = nRisk;
                    S *= 1.0 - d / n;
                    
                    
                    
                    
                    
                    
                    
                    if (n - d > 0) varSum += d / (n * (n - d));
                    double se = S * Math.Sqrt(varSum);
                    var (lo, hi) = CiLogLog(S, se);
                    baris.Add(new Baris
                    {
                        Waktu = t,
                        NRisk = nRisk,
                        NEvent = nEvent,
                        Survival = S,
                        Se = se,
                        CiLower = lo,
                        CiUpper = hi,
                    });
                }
            }
            return baris;
        }

        private static (double lower, double upper) CiLogLog(double S, double se, double z = 1.96)
        {
            if (S <= 0 || S >= 1 || se <= 0 || double.IsNaN(se)) return (S, S);
            double seG = se / (S * Math.Abs(Math.Log(S)));
            double expLo = Math.Exp(z * seG);
            double expHi = Math.Exp(-z * seG);
            double lower = Math.Pow(S, expLo);
            double upper = Math.Pow(S, expHi);
            return (lower, upper);
        }

        private static HasilLogRank LogRank(List<(double T, int E, string? G)> data, List<string?> grupNama)
        {
            int J = grupNama.Count;
            var times = data.Where(d => d.E == 1).Select(d => d.T).Distinct().OrderBy(t => t).ToList();

            var U = new double[J];
            var V = new double[J];   

            foreach (double t in times)
            {
                var mask = data.Where(d => d.T >= t).ToList();
                int n = mask.Count;
                if (n <= 1) continue;

                int dTotal = data.Count(x => Math.Abs(x.T - t) < 1e-12 && x.E == 1);
                if (dTotal == 0) continue;

                for (int j = 0; j < J; j++)
                {
                    string? gj = grupNama[j];
                    int nj = mask.Count(x => x.G == gj);
                    int oj = data.Count(x => Math.Abs(x.T - t) < 1e-12 && x.E == 1 && x.G == gj);
                    double ej = nj * (double)dTotal / n;
                    U[j] += oj - ej;

                    
                    double vj = (double)nj * (n - nj) * dTotal * (n - dTotal)
                                / ((double)n * n * (n - 1));
                    V[j] += vj;
                }
            }

            
            
            
            
            double chi2;
            if (J == 2)
            {
                chi2 = V[0] > 0 ? U[0] * U[0] / V[0] : 0;
            }
            else
            {
                
                
                chi2 = 0;
                for (int j = 0; j < J; j++)
                    if (V[j] > 0) chi2 += U[j] * U[j] / V[j];
            }

            int df = J - 1;
            double p = df > 0 && chi2 > 0 ? Distributions.ChiSquareUpper(chi2, df) : 1.0;

            return new HasilLogRank { Chi2 = chi2, Df = df, P = p };
        }

        

        public static List<ResultBlock> Hitung(Dataset ds, string waktu, string status, string? grup = null)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading($"Kaplan–Meier" + (grup is not null ? $" — {grup}" : ""), 1)
            };

            var h = Compute(ds, waktu, status, grup);
            if (h.NTotal == 0)
            {
                blocks.Add(Blocks.Note("Tidak ada amatan lengkap.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.KaplanMeier, DaftarRumus.LogRank));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Waktu", waktu),
                ("Status (1=event, 0=sensor)", status),
                ("Grup", grup ?? "—"),
                ("N total", h.NTotal.ToString())));

            foreach (var g in h.Grup)
            {
                blocks.Add(Blocks.Table(
                    $"Tabel Kaplan–Meier — {g.Nama} (event={g.NEventTotal}, sensor={g.NCensored})",
                    new[] { "Waktu", "Berisiko", "Event", "Survival", "SE", "CI 95% bawah", "CI 95% atas" },
                    g.Baris.Select(b => new[]
                    {
                        Fmt.Num(b.Waktu, 2),
                        Fmt.Int(b.NRisk),
                        Fmt.Int(b.NEvent),
                        Fmt.Num(b.Survival, 4),
                        Fmt.Num(b.Se, 4),
                        Fmt.Num(b.CiLower, 4),
                        Fmt.Num(b.CiUpper, 4),
                    }).ToArray()));
            }

            if (h.LogRank is not null)
            {
                var lr = h.LogRank;
                blocks.Add(Blocks.Table(
                    "Uji log-rank (antar grup)",
                    new[] { "χ²", "df", "p" },
                    new[] { new[] { Fmt.Num(lr.Chi2, 4), Fmt.Int(lr.Df), Fmt.P(lr.P) } },
                    lr.P < 0.05
                        ? "Perbedaan survival antar grup SIGNIFIKAN (p < 0,05)."
                        : "Tidak ada bukti perbedaan survival antar grup (p ≥ 0,05)."));
            }

            return blocks;
        }
    }
}
