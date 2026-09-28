using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{

    public static class Moses
    {
        public class HasilMoses
        {
            public string Kontrol = "";
            public string Uji = "";
            public int NKontrol, NUji, N;
            public double RentangKontrol, RentangUji;
            public double RentangHarapan;
            public double P;
            public string Metode = "";
        }

        
        static List<double> RankingDouble(IEnumerable<double> nilai)
        {
            var ls = nilai.ToList();
            int n = ls.Count;
            var urut = ls.Select((v, i) => (v, i)).OrderBy(x => x.v).ToList();
            var rank = new double[n];
            int a = 0;
            while (a < n)
            {
                int b = a;
                while (b + 1 < n && urut[b + 1].v.Equals(urut[b].v)) b++;
                double rata = (a + 1 + b + 1) / 2.0;
                for (int k = a; k <= b; k++) rank[urut[k].i] = rata;
                a = b + 1;
            }
            return rank.ToList();
        }

        static long Cnk(int n, int k)
        {
            if (k < 0 || k > n) return 0;
            k = Math.Min(k, n - k);
            long r = 1;
            for (int i = 1; i <= k; i++)
                r = r * (n - k + i) / i;
            return r;
        }

        
        static IEnumerable<int[]> Kombinasi(int n, int m)
        {
            var c = new int[m];
            for (int i = 0; i < m; i++) c[i] = i;
            yield return c;
            while (true)
            {
                int i = m - 1;
                while (i >= 0 && c[i] == n - m + i) i--;
                if (i < 0) yield break;
                c[i]++;
                for (int j = i + 1; j < m; j++) c[j] = c[j - 1] + 1;
                yield return c;
            }
        }

        static double Rentang(IReadOnlyList<double> rank, IEnumerable<int> indeks)
        {
            double mn = double.PositiveInfinity, mx = double.NegativeInfinity;
            foreach (var i in indeks)
            {
                if (rank[i] < mn) mn = rank[i];
                if (rank[i] > mx) mx = rank[i];
            }
            return mx - mn + 1;
        }

        
        public static HasilMoses? Hitung(Dataset ds, string varNilai, string varGrup, double alpha = 0.05)
        {
            var teksGrup = ds.Text(varGrup);
            var angka = ds.Numeric(varNilai);
            if (angka == null) return null;

            var pasangan = new List<(double V, string G)>();
            for (int i = 0; i < angka.Length; i++)
            {
                if (!angka[i].HasValue) continue;
                string? gv = teksGrup[i];
                if (string.IsNullOrWhiteSpace(gv)) continue;
                pasangan.Add((angka[i]!.Value, gv.Trim()));
            }
            if (pasangan.Count < 4) return null;

            var levelG = pasangan.Select(p => p.G).Distinct().OrderBy(s => s).ToList();
            if (levelG.Count != 2) return null;

            string g1 = levelG[0], g2 = levelG[1];
            var nil1 = pasangan.Where(p => p.G == g1).Select(p => p.V).ToList();
            var nil2 = pasangan.Where(p => p.G == g2).Select(p => p.V).ToList();
            if (nil1.Count < 1 || nil2.Count < 1) return null;

            
            string kontrol = nil1.Count <= nil2.Count ? g1 : g2;
            string grupUji = kontrol == g1 ? g2 : g1;
            var rankKontrol = kontrol == g1 ? nil1 : nil2;
            var rankUji = kontrol == g1 ? nil2 : nil1;

            int m = rankKontrol.Count;
            int N = pasangan.Count;

            
            var rankSemua = RankingDouble(pasangan.Select(p => p.V));
            
            var indeksKontrol = new List<int>();
            int idx = 0;
            foreach (var p in pasangan)
            {
                if (p.G == kontrol) indeksKontrol.Add(idx);
                idx++;
            }

            double S = Rentang(rankSemua, indeksKontrol);
            double S2 = Rentang(rankSemua, Enumerable.Range(0, N).Where(i => pasangan[i].G == grupUji));

            long total = Cnk(N, m);
            double nilaiP;   
            double harapan;
            string metode;
            const long BATAS_EKSak = 3_000_000;

            if (total <= BATAS_EKSak)
            {
                
                long le = 0, sumSpan = 0;
                foreach (var kom in Kombinasi(N, m))
                {
                    double sp = Rentang(rankSemua, kom);
                    if (sp <= S + 1e-9) le++;
                    sumSpan += (long)Math.Round(sp);
                }
                nilaiP = (double)le / total;
                harapan = (double)sumSpan / total;
                metode = "eksak (sebaran tertutup)";
            }
            else
            {
                
                
                
                var le = System.Numerics.BigInteger.Zero;
                var sumSpan = System.Numerics.BigInteger.Zero;
                var totalBi = (System.Numerics.BigInteger)total;
                for (int s = m; s <= N; s++)
                {
                    var c = (System.Numerics.BigInteger)(N - s + 1) * Cnk(s - 2, m - 2);
                    if (s <= S + 1e-9) le += c;
                    sumSpan += (System.Numerics.BigInteger)s * c;
                }
                nilaiP = (double)le / (double)totalBi;
                harapan = (double)sumSpan / (double)totalBi;
                metode = "eksak (sebaran tertutup)";
            }

            return new HasilMoses
            {
                Kontrol = kontrol,
                Uji = grupUji,
                NKontrol = m,
                NUji = N - m,
                N = N,
                RentangKontrol = S,
                RentangUji = S2,
                RentangHarapan = harapan,
                P = nilaiP,
                Metode = metode
            };
        }

        
        public static List<ResultBlock> MosesBlocks(Dataset ds, string varNilai, string varGrup, double alpha = 0.05)
        {
            var h = Hitung(ds, varNilai, varGrup, alpha);
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading($"Uji Moses reaksi ekstrem ({varNilai} menurut {varGrup})", 1)
            };
            if (h == null)
            {
                blocks.Add(Blocks.Note("Perlu tepat dua kelompok dan cukup amatan (tanpa nilai hilang).", NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.UjiMoses));
            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ($"Kelompok kendali (n lebih kecil)", h.Kontrol),
                ($"Kelompok uji", h.Uji),
                ("Banyak amatan (N)", $"N = {h.N}"),
                ("n kendali (m)", $"m = {h.NKontrol}"),
                ("Rentang kendali (S)", $"S = {Fmt.Num(h.RentangKontrol, 4)}"),
                ("Rentang uji", $"{Fmt.Num(h.RentangUji, 4)}"),
                ("Rentang harapan H₀", $"{Fmt.Num(h.RentangHarapan, 4)}"),
                ("Metode", h.Metode)));

            blocks.Add(Blocks.Table("Uji Moses reaksi ekstrem",
                new[] { "Ukuran", "Nilai" },
                new List<List<string>>
                {
                    new() { "Kelompok kendali", h.Kontrol },
                    new() { "Kelompok uji", h.Uji },
                    new() { "n kendali", h.NKontrol.ToString() },
                    new() { "n uji", h.NUji.ToString() },
                    new() { "Rentang (span) kendali", Fmt.Num(h.RentangKontrol, 4) },
                    new() { "Rentang (span) uji", Fmt.Num(h.RentangUji, 4) },
                    new() { "Rentang harapan (H₀)", Fmt.Num(h.RentangHarapan, 4) },
                    new() { "p (satu sisi, P(span ≤ S))", Fmt.P(h.P) },
                    new() { "Metode", h.Metode }
                },
                h.P < alpha
                    ? $"Kelompok uji menunjukkan reaksi lebih ekstrem (p = {Fmt.P(h.P)} < α = {Fmt.Num(alpha, 2)})"
                    : $"Tidak cukup bukti reaksi lebih ekstrem (p = {Fmt.P(h.P)} ≥ α = {Fmt.Num(alpha, 2)})"));

            blocks.Add(Blocks.Note(
                "Rentang = peringkat terbesar − terkecil + 1 dalam kelompok. Rentang kendali yang " +
                "kecil berarti kelompok uji mendorong amatannya ke ujung sebaran (reaksi ekstrem). " +
                "p dihitung dari sebaran tertutup semua cara memilih m peringkat dari N bila memungkinkan, " +
                "atau hampiran normal bila terlalu banyak kombinasi. Uji ini satu sisi untuk arah \"lebih ekstrem\"."));
            return blocks;
        }
    }
}
