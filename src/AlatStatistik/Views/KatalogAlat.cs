using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AlatStatistik.Analysis;

namespace AlatStatistik.Views
{
    
    
    
    public sealed class ItemAlat
    {
        public string Nama { get; set; } = "";
        public string Kategori { get; set; } = "";
        public string Kunci { get; set; } = "";
        public AnalysisSpec? Spec { get; set; }

        
        
        
        
        public override string ToString() => Nama;
    }

    public static class Katalog
    {
        
        
        
        public static List<ItemAlat> Semua(IEnumerable<AnalysisSpec> analisis)
        {
            var hasil = new List<ItemAlat>();

            foreach (var spec in analisis
                     .OrderBy(s => UrutKategori(s.Category))
                     .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                hasil.Add(new ItemAlat
                {
                    Nama = spec.Name,
                    Kategori = spec.IsML ? "ML" : spec.Category,
                    Spec = spec,
                    Kunci = KunciPencarian(spec)
                });
            }

            return hasil;
        }

        
        
        
        private static int UrutKategori(string kategori) => kategori switch
        {
            "Deskriptif" => 0,
            "Kenormalan" => 1,
            "Perbandingan" => 2,
            "Korelasi & regresi" => 3,
            _ => 4
        };

        
        
        
        
        
        public static string KunciPencarian(AnalysisSpec spec)
        {
            var sb = new StringBuilder();
            sb.Append(spec.Name).Append(' ').Append(spec.Category).Append(' ').Append(spec.Description).Append(' ');

            string n = spec.Name.ToLowerInvariant();
            if (n.Contains("uji-t") || n.Contains("uji t") || n.Contains("t ") || n.Contains("t-sampel"))
                sb.Append("t-test ttest independent beda rerata mean ");
            if (n.Contains("anova")) sb.Append("analysis of variance f-test ");
            if (n.Contains("korelasi")) sb.Append("correlation pearson spearman kendall ");
            if (n.Contains("regresi")) sb.Append("regression ols gls linear ");
            if (n.Contains("reliabilitas") || n.Contains("cronbach")) sb.Append("alpha cronbach reliability ");
            if (n.Contains("chi") || n.Contains("kai") || n.Contains("silang")) sb.Append("chi-square crosstab kontingensi ");
            if (n.Contains("kenormalan") || n.Contains("normalitas")) sb.Append("shapiro wilk kolmogorov smirnov jarque bera ");
            if (n.Contains("mann") || n.Contains("wilcoxon") || n.Contains("kruskal")) sb.Append("nonparametrik rank peringkat ");
            if (n.Contains("bootstrap")) sb.Append("resampling bca persentil ");
            if (n.Contains("histogram") || n.Contains("box") || n.Contains("pencar")) sb.Append("grafik plot chart ");
            if (n.Contains("klaster") || n.Contains("k-means")) sb.Append("cluster clustering ");
            if (n.Contains("survival") || n.Contains("kaplan")) sb.Append("cox hazard ");

            return sb.ToString().ToLowerInvariant();
        }

        
        
        
        public static List<ItemAlat> Cari(List<ItemAlat> semua, string pertanyaan)
        {
            string q = pertanyaan.Trim();
            if (q.Length == 0) return semua;

            string kecil = q.ToLowerInvariant();
            return semua.Where(it => it.Kunci.Contains(kecil, StringComparison.Ordinal)).ToList();
        }
    }
}
