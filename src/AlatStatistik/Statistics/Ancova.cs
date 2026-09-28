using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Ancova
    {
        public sealed class Hasil
        {
            public int N, K, DfGrup, DfKov, DfResid;
            public List<string> Tingkat = new();
            public List<double> RerataKasar = new();
            public List<double> RerataKovariat = new();
            public List<double> RerataDisesuaikan = new();
            public List<int> NPerGrup = new();

            public double SsGrup, SsKov, SsResid, Sst;
            public double FGrup, PGrup, FKov, PKov;

            public double BKov, SeKov, TKov, PKovKoef;
            public double BKonstanta;

            public double R2, R2Adj;
            public double KovariatRerata;
        }

        public static Hasil? Hitung(double[] y, string[] grup, double[] kov)
        {
            int n = y.Length;
            if (n < 8 || grup.Length != n || kov.Length != n) return null;

            var tingkat = grup.Distinct().OrderBy(g => g, StringComparer.Ordinal).ToList();
            int g = tingkat.Count;
            if (g < 2) return null;

            
            var xPenuh = new double[n][];
            for (int i = 0; i < n; i++)
            {
                xPenuh[i] = new double[g + 1];
                xPenuh[i][0] = 1.0;
                int idx = tingkat.IndexOf(grup[i]);
                for (int j = 1; j < g; j++) xPenuh[i][j] = idx == j ? 1.0 : 0.0;
                xPenuh[i][g] = kov[i];
            }

            
            var xTanpaGrup = new double[n][];
            for (int i = 0; i < n; i++) xTanpaGrup[i] = new[] { 1.0, kov[i] };

            
            var xTanpaKov = new double[n][];
            for (int i = 0; i < n; i++)
            {
                xTanpaKov[i] = new double[g];
                xTanpaKov[i][0] = 1.0;
                int idx = tingkat.IndexOf(grup[i]);
                for (int j = 1; j < g; j++) xTanpaKov[i][j] = idx == j ? 1.0 : 0.0;
            }

            double ssePenuh = Sse(y, xPenuh, out double[] beta);
            double sseTanpaGrup = Sse(y, xTanpaGrup, out _);
            double sseTanpaKov = Sse(y, xTanpaKov, out _);

            if (double.IsNaN(ssePenuh) || double.IsNaN(sseTanpaGrup) || double.IsNaN(sseTanpaKov))
                return null;

            double ssGrup = sseTanpaGrup - ssePenuh;
            double ssKov = sseTanpaKov - ssePenuh;
            int dfGrup = g - 1, dfKov = 1, dfResid = n - (g + 1);

            double msResid = ssePenuh / dfResid;
            double fGrup = msResid > 0 ? (ssGrup / dfGrup) / msResid : double.NaN;
            double fKov = msResid > 0 ? (ssKov / dfKov) / msResid : double.NaN;

            double rataY = y.Average();
            double sst = 0;
            for (int i = 0; i < n; i++) { double d = y[i] - rataY; sst += d * d; }
            double r2 = sst > 0 ? 1 - ssePenuh / sst : double.NaN;
            double r2adj = 1 - (1 - r2) * (n - 1) / dfResid;

            
            double bKov = beta[g];
            double bKonst = beta[0];

            
            double seKov = GalatBaku(y, xPenuh, ssePenuh, dfResid, g);

            double kovRata = kov.Average();
            var kasar = new List<double>();
            var kovGrup = new List<double>();
            var disesuaikan = new List<double>();
            var nGrup = new List<int>();

            for (int t = 0; t < g; t++)
            {
                double jy = 0, jk = 0;
                int c = 0;
                for (int i = 0; i < n; i++)
                {
                    if (!string.Equals(grup[i], tingkat[t], StringComparison.Ordinal)) continue;
                    jy += y[i]; jk += kov[i]; c++;
                }
                double my = c > 0 ? jy / c : double.NaN;
                double mk = c > 0 ? jk / c : double.NaN;
                kasar.Add(my);
                kovGrup.Add(mk);
                
                disesuaikan.Add(my - bKov * (mk - kovRata));
                nGrup.Add(c);
            }

            return new Hasil
            {
                N = n, K = g + 1, DfGrup = dfGrup, DfKov = dfKov, DfResid = dfResid,
                Tingkat = tingkat,
                RerataKasar = kasar, RerataKovariat = kovGrup,
                RerataDisesuaikan = disesuaikan, NPerGrup = nGrup,
                SsGrup = ssGrup, SsKov = ssKov, SsResid = ssePenuh, Sst = sst,
                FGrup = fGrup, PGrup = Distributions.FUpper(fGrup, dfGrup, dfResid),
                FKov = fKov, PKov = Distributions.FUpper(fKov, dfKov, dfResid),
                BKov = bKov, SeKov = seKov,
                TKov = seKov > 0 ? bKov / seKov : double.NaN,
                PKovKoef = seKov > 0
                    ? Distributions.StudentTTwoSided(bKov / seKov, dfResid) : double.NaN,
                BKonstanta = bKonst,
                R2 = r2, R2Adj = r2adj,
                KovariatRerata = kovRata,
            };
        }

        private static double Sse(double[] y, double[][] x, out double[] beta)
        {
            int n = y.Length, k = x[0].Length;
            beta = new double[k];

            var xtx = new double[k, k];
            var xty = new double[k];
            for (int i = 0; i < n; i++)
                for (int a = 0; a < k; a++)
                {
                    xty[a] += x[i][a] * y[i];
                    for (int b = 0; b < k; b++) xtx[a, b] += x[i][a] * x[i][b];
                }

            var inv = Regression.Inverse((double[,])xtx.Clone());
            if (inv is null) return double.NaN;

            for (int a = 0; a < k; a++)
            {
                double s = 0;
                for (int b = 0; b < k; b++) s += inv[a, b] * xty[b];
                beta[a] = s;
            }

            double sse = 0;
            for (int i = 0; i < n; i++)
            {
                double p = 0;
                for (int a = 0; a < k; a++) p += beta[a] * x[i][a];
                double e = y[i] - p;
                sse += e * e;
            }
            return sse;
        }

        private static double GalatBaku(double[] y, double[][] x, double sse, int dfResid, int j)
        {
            int n = y.Length, k = x[0].Length;
            var xtx = new double[k, k];
            for (int i = 0; i < n; i++)
                for (int a = 0; a < k; a++)
                    for (int b = 0; b < k; b++) xtx[a, b] += x[i][a] * x[i][b];

            var inv = Regression.Inverse((double[,])xtx.Clone());
            if (inv is null) return double.NaN;
            double s2 = sse / dfResid;
            return Math.Sqrt(Math.Max(0, s2 * inv[j, j]));
        }

        

        public static List<ResultBlock> AncovaBlocks(Dataset ds, string dependen, string faktor,
                                                     string kovariat)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("ANCOVA (ANOVA dengan kovariat)", 1)
            };

            var perlu = new[] { dependen, faktor, kovariat };
            var baris = ds.CompleteRows(perlu);
            if (baris.Count < 12)
            {
                blocks.Add(Blocks.Note(
                    $"Baris lengkap hanya {baris.Count}. ANCOVA butuh minimal 12 baris "
                    + "supaya derajat bebasnya cukup.", NoteKind.Error));
                return blocks;
            }

            var semuaY = ds.Numeric(dependen);
            var semuaK = ds.Numeric(kovariat);
            var semuaG = ds.Text(faktor);

            var y = new double[baris.Count];
            var kv = new double[baris.Count];
            var gp = new string[baris.Count];
            for (int i = 0; i < baris.Count; i++)
            {
                y[i] = semuaY[baris[i]] ?? double.NaN;
                kv[i] = semuaK[baris[i]] ?? double.NaN;
                gp[i] = semuaG[baris[i]] ?? "(kosong)";
            }

            var tingkat = gp.Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (tingkat.Count < 2)
            {
                blocks.Add(Blocks.Note(
                    $"Variabel '{faktor}' hanya punya {tingkat.Count} tingkat. "
                    + "ANCOVA butuh minimal dua kelompok untuk dibandingkan.", NoteKind.Error));
                return blocks;
            }

            var h = Hitung(y, gp, kv);
            if (h is null)
            {
                blocks.Add(Blocks.Note(
                    "Model tidak bisa disesuaikan. Periksa apakah kovariatnya beragam "
                    + "dan tidak ada kelompok yang kosong.", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai",
                DaftarRumus.Ancova, DaftarRumus.RerataDisesuaikan));

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Dependen", dependen),
                ("Faktor", faktor),
                ("Kovariat", kovariat),
                ("Banyak baris lengkap", $"n = {Fmt.Int(h.N)}"),
                ("Derajat bebas", $"faktor = {Fmt.Int(h.DfGrup)}, kovariat = {Fmt.Int(h.DfKov)}, "
                                  + $"residual = {Fmt.Int(h.DfResid)}"),
                ("Rerata kovariat keseluruhan", Fmt.Num(h.KovariatRerata, 4))));

            blocks.Add(Blocks.Table(
                "Uji efek (Type III)",
                new[] { "Sumber", "Jumlah kuadrat", "df", "F", "p" },
                new[]
                {
                    new[] { faktor, Fmt.Num(h.SsGrup, 6), Fmt.Int(h.DfGrup),
                            Fmt.Num(h.FGrup, 4), Fmt.P(h.PGrup) },
                    new[] { kovariat, Fmt.Num(h.SsKov, 6), Fmt.Int(h.DfKov),
                            Fmt.Num(h.FKov, 4), Fmt.P(h.PKov) },
                    new[] { "Residual", Fmt.Num(h.SsResid, 6), Fmt.Int(h.DfResid), Fmt.NA, Fmt.NA },
                    new[] { "Total (terkoreksi)", Fmt.Num(h.Sst, 6),
                            Fmt.Int(h.DfGrup + h.DfKov + h.DfResid), Fmt.NA, Fmt.NA },
                },
                "Setiap suku diuji SETELAH suku lain diperhitungkan (Type III), jadi "
                + "angkanya tidak bergantung urutan penulisan model. Baris kovariat "
                + "menjawab \"apakah kovariatnya memang perlu dibawa?\" "
                + "Perhatikan: jumlah kuadrat Type III **tidak** menjumlah menjadi "
                + "jumlah kuadrat total — suku-sukunya tidak saling ortogonal. "
                + "Jangan menjumlahkannya untuk menghitung R²."));

            blocks.Add(Blocks.Table(
                "Koefisien kovariat",
                new[] { "Suku", "B", "Galat baku", "t", "p" },
                new[]
                {
                    new[] { "(Konstanta)", Fmt.Num(h.BKonstanta, 6), Fmt.NA, Fmt.NA, Fmt.NA },
                    new[] { kovariat, Fmt.Num(h.BKov, 6), Fmt.Num(h.SeKov, 6),
                            Fmt.Num(h.TKov, 4), Fmt.P(h.PKovKoef) },
                },
                $"Setiap kenaikan satu satuan {kovariat} menggeser {dependen} sebesar "
                + $"{Fmt.Num(h.BKov, 4)} satuan, dengan kelompok dianggap tetap."));

            blocks.Add(Blocks.Table(
                "Rerata kasar dan rerata disesuaikan",
                new[] { faktor, "n", $"Rerata {dependen}", $"Rerata {kovariat}",
                        "Rerata disesuaikan", "Selisih" },
                Enumerable.Range(0, h.Tingkat.Count).Select(t => new[]
                {
                    h.Tingkat[t],
                    Fmt.Int(h.NPerGrup[t]),
                    Fmt.Num(h.RerataKasar[t], 4),
                    Fmt.Num(h.RerataKovariat[t], 4),
                    Fmt.Num(h.RerataDisesuaikan[t], 4),
                    Fmt.Num(h.RerataDisesuaikan[t] - h.RerataKasar[t], 4),
                }).ToArray(),
                "Rerata disesuaikan menjawab \"bagaimana kalau semua kelompok punya "
                + $"nilai {kovariat} yang sama?\". Selisih = disesuaikan − kasar; "
                + "bila kovariatnya seimbang antar kelompok, selisihnya kecil."));

            blocks.Add(Blocks.Table(
                "Ringkasan model",
                new[] { "Besaran", "Nilai" },
                new[]
                {
                    new[] { "R²", Fmt.Num(h.R2, 6) },
                    new[] { "R² disesuaikan", Fmt.Num(h.R2Adj, 6) },
                    new[] { "Galat baku taksiran", Fmt.Num(Math.Sqrt(h.SsResid / h.DfResid), 4) },
                },
                "R² di sini menggabungkan daya jelas faktor DAN kovariat; ia bukan "
                + "ukuran seberapa besar efek faktornya."));

            return blocks;
        }
    }
}
