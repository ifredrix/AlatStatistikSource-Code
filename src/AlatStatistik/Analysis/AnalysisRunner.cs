using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;
using AlatStatistik.Statistics;

namespace AlatStatistik.Analysis
{
    public enum FieldKind { Numeric, Categorical, Any }

    public class FieldSpec
    {
        public string Key = "";
        public string Label = "";
        public FieldKind Kind = FieldKind.Any;
        public bool Multiple;
        public int Min = 1;
        public int Max = 99;

        public FieldSpec(string key, string label, FieldKind kind, bool multiple = false, int min = 1, int max = 99)
        {
            Key = key; Label = label; Kind = kind; Multiple = multiple; Min = min; Max = max;
        }
    }

    public enum OptionType { Bool, Choice, Number, Text }

    public class OptionSpec
    {
        public string Key = "";
        public string Label = "";
        public OptionType Type = OptionType.Bool;
        public List<string> Choices = new();
        public double Value;
        public bool Checked;
        public string Text = "";

        public OptionSpec(string key, string label, OptionType type, object? nilai = null)
        {
            Key = key; Label = label; Type = type;
            switch (type)
            {
                case OptionType.Bool: Checked = nilai is true; break;
                case OptionType.Choice: Choices = (nilai as List<string>) ?? new(); Value = 0; break;
                case OptionType.Number: Value = Convert.ToDouble(nilai ?? 0); break;
                case OptionType.Text:  Text = (nilai as string) ?? ""; break;
            }
        }

        public OptionSpec Clone()
        {
            var salin = (OptionSpec)MemberwiseClone();
            salin.Choices = new List<string>(Choices);
            return salin;
        }
    }

    public class AnalysisSpec
    {
        public string Id = "";
        public string Name = "";
        public string Category = "";
        public string Description = "";
        public bool IsML { get; set; } = false;
        public List<FieldSpec> Fields = new();
        public List<OptionSpec> Options = new();
        public Func<RunContext, List<ResultBlock>>? Run;
    }

    public class RunContext
    {
        public Dataset Data = new();
        public Dictionary<string, List<string>> Fields = new();
        public Dictionary<string, OptionSpec> Options = new();
        public double Alpha = 0.05;

        public List<string> Get(string key)
            => Fields.TryGetValue(key, out var v) ? v : new List<string>();

        public string First(string key) => Get(key).FirstOrDefault() ?? "";

        public bool IsChecked(string key)
            => Options.TryGetValue(key, out var o) && o.Checked;

        public double Number(string key)
            => Options.TryGetValue(key, out var o) ? o.Value : 0;

        public string Choice(string key)
        {
            if (!Options.TryGetValue(key, out var o)) return "";
            int i = (int)Math.Round(o.Value);
            return i >= 0 && i < o.Choices.Count ? o.Choices[i] : "";
        }
    }

    public static class AnalysisRunner
    {

        public static List<ResultBlock> GagalBlocks(string pesan)
        {
            return new List<ResultBlock> { Blocks.Note(pesan, NoteKind.Error) };
        }

        // Fmt.Num menyamar sebagai "—", jadi angka NaN/Infinity yang tidak
        // tersaring tidak pernah kelihatan sebagai kegagalan. Tandai di sini
        // supaya pengguna tahu ada hitungan yang merosot diam-diam.
        public static List<ResultBlock> PeringatiTakHingga(List<ResultBlock> blok)
        {
            int jumlah = blok.Where(b => b.Kind == BlockKind.Table)
                             .SelectMany(b => b.Rows.SelectMany(r => r))
                             .Count(TakHingga);

            if (jumlah == 0) return blok;

            blok.Add(Blocks.Note(
                $"Hasil memuat {jumlah} angka yang bukan bilangan (NaN/Infinity). "
                + "Biasanya berarti ada hitungan yang merosot: data seragam, "
                + "pencilan ekstrem, atau prediktor yang saling bergantung.",
                NoteKind.Warning));
            return blok;
        }

        private static bool TakHingga(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            return s.Contains("NaN", StringComparison.OrdinalIgnoreCase)
                || s.Contains("Infinity", StringComparison.OrdinalIgnoreCase)
                || s.IndexOf('∞') >= 0;
        }

        public static List<ResultBlock> Jalankan(AnalysisSpec spec, RunContext ctx)
        {
            if (spec.Run is null)
                return GagalBlocks($"Analisis “{spec.Name}” belum punya pelaksana.");
            return PeringatiTakHingga(spec.Run(ctx));
        }

        public static KuantilDefinisi KuantilDefinisiDari(string? pilihan)
            => (pilihan ?? "").Trim().ToLowerInvariant() switch
            {
                "tukey" => KuantilDefinisi.TukeyHinges,
                "linear" => KuantilDefinisi.Linear,
                _ => KuantilDefinisi.Satu,
            };

        public static List<AnalysisSpec> Semua()
        {
            return new List<AnalysisSpec>
            {
                new()
                {
                    Id = "deskriptif", Name = "Statistik deskriptif", Category = "Deskriptif",
                    Description = "Rerata, simpangan baku, condong, keruncing, dan uji kenormalan.",
                    Fields = { new FieldSpec("vars", "Variabel", FieldKind.Numeric, true, 1) },
                    Options = { new OptionSpec("normalitas", "Sertakan uji kenormalan", OptionType.Bool, true) },
                    Run = c => Descriptives.Describe(c.Data, c.Get("vars"), c.IsChecked("normalitas"))
                },
                new()
                {
                    Id = "frekuensi", Name = "Frekuensi", Category = "Deskriptif",
                    Description = "Tabel frekuensi dan diagram batang untuk variabel kategori.",
                    Fields = { new FieldSpec("var", "Variabel", FieldKind.Any, false, 1, 1) },
                    Run = c => Descriptives.Frequencies(c.Data, c.First("var"))
                },
                new()
                {
                    Id = "distribusi", Name = "Histogram & box plot", Category = "Deskriptif",
                    Description = "Gambar sebaran nilai untuk melihat bentuk data.",
                    Fields = { new FieldSpec("var", "Variabel", FieldKind.Numeric, false, 1, 1) },
                    Run = c => Descriptives.Distribution(c.Data, c.First("var"))
                },
                new()
                {
                    Id = "agregat", Name = "Agregasi per kelompok", Category = "Deskriptif",
                    Description = "Ringkaskan tiap kelompok (rerata, jumlah, n, min, maks, median).",
                    Fields =
                    {
                        new FieldSpec("kunci", "Variabel pengelompokan", FieldKind.Any, true, 1),
                        new FieldSpec("nilai", "Variabel yang diringkas", FieldKind.Numeric, true, 1)
                    },
                    Options =
                    {
                        new OptionSpec("fungsi", "Fungsi ringkasan", OptionType.Choice,
                            new List<string> { "rerata", "jumlah", "n", "minimum", "maksimum", "median" })
                    },
                    Run = c => Aggregate.AgregatBlocks(c.Data, c.Get("kunci"), c.Get("nilai"), c.Choice("fungsi"))
                },
                new()
                {
                    Id = "kmeans", Name = "K-Means Cluster", Category = "Klaster", IsML = true,
                    Description = "Kelompokkan amatan ke k klaster.",
                    Fields = { new FieldSpec("var", "Variabel", FieldKind.Numeric, true, 1) },
                    Options =
                    {
                        new OptionSpec("k", "Banyak klaster", OptionType.Number, 3),
                        new OptionSpec("iterasi", "Batas iterasi", OptionType.Number, 20),
                        new OptionSpec("benih", "Benih (untuk hasil yang bisa diulang)", OptionType.Number, 42)
                    },
                    Run = c => KMeans.KMeansBlocks(c.Data, c.Get("var"),
                                                   (int)Math.Max(1, c.Number("k")),
                                                   (int)Math.Max(1, c.Number("iterasi")),
                                                   (uint)Math.Max(1, c.Number("benih")))
                },
                new()
                {
                    Id = "rasio", Name = "Rasio (Ratio Statistics)", Category = "Deskriptif",
                    Description = "Statistik rasio pembilang/penyebut, boleh per kelompok.",
                    Fields =
                    {
                        new FieldSpec("pembilang", "Pembilang", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("penyebut", "Penyebut", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("kelompok", "Kelompok (boleh kosong)", FieldKind.Categorical, false, 0, 1)
                    },
                    Run = c => Ratio.RatioBlocks(c.Data, c.First("pembilang"), c.First("penyebut"),
                                                 c.First("kelompok"))
                },
                new()
                {
                    Id = "merge_files", Name = "Merge Files (pratinjau)", Category = "Deskriptif",
                    Description = "Gabung dengan berkas lain (tambah kasus / tambah variabel) — pratinjau, dataset tak diubah.",
                    Options =
                    {
                        new OptionSpec("berkas", "Berkas kedua (jalur CSV)", OptionType.Text,
                                       "samples/agregat_sintetik.csv"),
                        new OptionSpec("cara", "Cara", OptionType.Choice,
                                       new List<string> { "tambah_kasus", "tambah_variabel" })
                    },
                    Run = c =>
                    {
                        string jalur = c.Options.TryGetValue("berkas", out var oB) ? oB.Text : "";
                        string cara = c.Choice("cara");
                        return MergeFiles.MergeFilesBlocks(c.Data, jalur, cara);
                    }
                },
                new()
                {
                    Id = "means", Name = "Means (rerata per kelompok)", Category = "Perbandingan",
                    Description = "Rerata, simpangan baku, dan median tiap kelompok.",
                    Fields =
                    {
                        new FieldSpec("nilai", "Variabel nilai", FieldKind.Numeric, true, 1),
                        new FieldSpec("faktor", "Variabel kelompok", FieldKind.Categorical, true, 0)
                    },
                    Run = c => Means.MeansBlocks(c.Data, c.Get("faktor"), c.Get("nilai"))
                },
                new()
                {
                    Id = "count_values", Name = "Count Values within Cases", Category = "Deskriptif",
                    Description = "Hitung berapa banyak variabel terpilih yang cocok dengan target — pratinjau (dataset tak diubah).",
                    Fields = { new FieldSpec("var", "Variabel yang dihitung", FieldKind.Numeric, true, 1) },
                    Options =
                    {
                        new OptionSpec("nilai", "Nilai yang dicari", OptionType.Number, 0.0),
                        new OptionSpec("pakai_rentang", "Pakai rentang bukan nilai persis", OptionType.Bool, false),
                        new OptionSpec("batas_bawah", "Batas bawah rentang", OptionType.Number, 0.0),
                        new OptionSpec("batas_atas", "Batas atas rentang", OptionType.Number, 1.0)
                    },
                    Run = c => CountValues.CountValuesBlocks(c.Data, c.Get("var"), c.Number("nilai"),
                                                  c.IsChecked("pakai_rentang"), c.Number("batas_bawah"), c.Number("batas_atas"))
                },
                new()
                {
                    Id = "explore", Name = "Explore", Category = "Deskriptif",
                    Description = "Statistik deskriptif & uji kenormalan per kelompok.",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel terikat", FieldKind.Numeric, true, 1),
                        new FieldSpec("faktor", "Variabel kelompok", FieldKind.Categorical, true, 0)
                    },
                    Options =
                    {
                        
                        
                        
                        new OptionSpec("kuantil", "Definisi kuantil Q1/Q3", OptionType.Choice,
                                       new List<string> { "def1", "tukey", "linear" })
                    },
                    Run = c => Explore.ExploreBlocks(c.Data, c.Get("dependen"), c.Get("faktor"),
                                                     KuantilDefinisiDari(c.Choice("kuantil")))
                },
                new()
                {
                    Id = "codebook", Name = "Codebook (kamus variabel)", Category = "Deskriptif",
                    Description = "Dokumentasi variabel: nama, label, ukur, nilai sah/hilang.",
                    Options = { new OptionSpec("hanya_numerik", "Hanya variabel numerik", OptionType.Bool, false) },
                    Run = c => Codebook.CodebookBlocks(c.Data, c.IsChecked("hanya_numerik"))
                },
                new()
                {
                    Id = "pie", Name = "Diagram lingkaran (pie)", Category = "Deskriptif",
                    Description = "Frekuensi tiap kategori sebagai diagram lingkaran.",
                    Fields = { new FieldSpec("var", "Variabel", FieldKind.Any, false, 1, 1) },
                    Run = c => PieChart.PieChartBlocks(c.Data, c.First("var"))
                },
                new()
                {
                    Id = "error_bar", Name = "Batang galat (Error Bar)", Category = "Perbandingan",
                    Description = "Rerata tiap kelompok beserta rentangnya (selang kepercayaan, galat baku, atau simpangan baku).",
                    Fields =
                    {
                        new FieldSpec("nilai", "Variabel nilai", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("kelompok", "Variabel kelompok (opsional)", FieldKind.Any, true, 0, 1)
                    },
                    Options =
                    {
                        new OptionSpec("jenis", "Jenis rentang", OptionType.Choice,
                            new List<string> { "ci", "se", "sd" }),
                        new OptionSpec("aras", "Tingkat kepercayaan (untuk ci)", OptionType.Number, 0.95),
                        new OptionSpec("kelipatan", "Lipatan SE/SD (untuk se & sd)", OptionType.Number, 2)
                    },
                    Run = c =>
                    {
                        string jenis = c.Choice("jenis");
                        return ErrorBar.ErrorBarBlocks(c.Data, c.First("nilai"),
                            c.Get("kelompok").Count > 0 ? c.Get("kelompok")[0] : null,
                            string.IsNullOrEmpty(jenis) ? "ci" : jenis,
                            c.Number("aras") > 0 && c.Number("aras") < 1 ? c.Number("aras") : 0.95,
                            c.Number("kelipatan") > 0 ? c.Number("kelipatan") : 2);
                    }
                },
                new()
                {
                    Id = "transpose", Name = "Transpose (pratinjau)", Category = "Deskriptif",
                    Description = "Tukar baris↔kolom sebagai pratinjau.",
                    Options = { new OptionSpec("maks_kolom", "Maksimum kolom kasus", OptionType.Number, 30) },
                    Run = c => Transpose.TransposeBlocks(c.Data, (int)c.Number("maks_kolom"))
                },
                new()
                {
                    Id = "t_satu", Name = "Uji-t satu sampel", Category = "Perbandingan",
                    Description = "Menguji apakah rerata sama dengan nilai tertentu.",
                    Fields = { new FieldSpec("var", "Variabel", FieldKind.Numeric, false, 1, 1) },
                    Options = { new OptionSpec("mu", "Nilai yang diuji", OptionType.Number, 0.0) },
                    Run = c => Compare.OneSampleT(c.Data, c.First("var"), c.Number("mu"), c.Alpha)
                },
                new()
                {
                    Id = "t_bebas", Name = "Uji-t dua sampel bebas", Category = "Perbandingan",
                    Description = "Membandingkan rerata dua kelompok (Student dan Welch sekaligus).",
                    Fields =
                    {
                        new FieldSpec("nilai", "Variabel nilai", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("faktor", "Variabel kelompok", FieldKind.Categorical, false, 1, 1)
                    },
                    Run = c => Compare.IndependentT(c.Data, c.First("nilai"), c.First("faktor"), c.Alpha)
                },
                new()
                {
                    Id = "t_pasangan", Name = "Uji-t berpasangan", Category = "Perbandingan",
                    Description = "Membandingkan dua pengukuran pada subjek yang sama.",
                    Fields = { new FieldSpec("pasang", "Dua variabel", FieldKind.Numeric, true, 2, 2) },
                    Run = c =>
                    {
                        var v = c.Get("pasang");
                        return Compare.PairedT(c.Data, v[0], v[1], c.Alpha);
                    }
                },
                new()
                {
                    Id = "anova", Name = "ANOVA satu arah", Category = "Perbandingan",
                    Description = "Membandingkan rerata lebih dari dua kelompok.",
                    Fields =
                    {
                        new FieldSpec("nilai", "Variabel nilai", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("faktor", "Variabel kelompok", FieldKind.Categorical, false, 1, 1)
                    },
                    Run = c => Anova.OneWay(c.Data, c.First("nilai"), c.First("faktor"), c.Alpha)
                },
                new()
                {
                    Id = "anova_dua_faktor", Name = "ANOVA dua faktor (Type III)",
                    Category = "Perbandingan",
                    Description = "Dua faktor sekaligus beserta interaksinya — "
                                  + "menjawab apakah pengaruh satu faktor "
                                  + "berubah menurut tingkat faktor lain.",
                    Fields =
                    {
                        new FieldSpec("nilai", "Variabel nilai", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("faktorA", "Faktor pertama (minimal 2 tingkat)",
                                      FieldKind.Categorical, false, 1, 1),
                        new FieldSpec("faktorB", "Faktor kedua (minimal 2 tingkat)",
                                      FieldKind.Categorical, false, 1, 1)
                    },
                    Run = c => AnovaDuaFaktor.AnovaDuaFaktorBlocks(
                        c.Data, c.First("nilai"), c.First("faktorA"), c.First("faktorB"))
                },
                new()
                {
                    Id = "mannwhitney", Name = "Mann–Whitney U", Category = "Perbandingan",
                    Description = "Pengganti uji-t bebas bila data tidak normal.",
                    Fields =
                    {
                        new FieldSpec("nilai", "Variabel nilai", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("faktor", "Variabel kelompok", FieldKind.Categorical, false, 1, 1)
                    },
                    Run = c => Compare.MannWhitney(c.Data, c.First("nilai"), c.First("faktor"), c.Alpha)
                },
                new()
                {
                    Id = "wilcoxon", Name = "Wilcoxon signed-rank", Category = "Perbandingan",
                    Description = "Pengganti uji-t berpasangan bila beda tidak normal.",
                    Fields = { new FieldSpec("pasang", "Dua variabel", FieldKind.Numeric, true, 2, 2) },
                    Run = c =>
                    {
                        var v = c.Get("pasang");
                        return Compare.Wilcoxon(c.Data, v[0], v[1], c.Alpha);
                    }
                },
                new()
                {
                    Id = "tanda", Name = "Uji tanda (Sign)", Category = "Perbandingan",
                    Description = "Uji beda dua pengukuran berpasangan tanpa asumsi bentuk sebaran.",
                    Fields = { new FieldSpec("pasang", "Dua variabel berpasangan", FieldKind.Numeric, true, 2, 2) },
                    Run = c =>
                    {
                        var v = c.Get("pasang");
                        if (v.Count != 2) return GagalBlocks("Pilih tepat dua variabel untuk uji tanda.");
                        if (v.Any(n => c.Data.IndexOf(n) < 0)) return GagalBlocks("Ada variabel yang tidak ditemukan dalam data.");
                        var h = NonparametrikBerhubungan.UjiTanda(
                                    NonparametrikBerhubungan.KolomDouble(c.Data, v[0]),
                                    NonparametrikBerhubungan.KolomDouble(c.Data, v[1]));
                        if (h is null) return GagalBlocks("Data tidak cukup (butuh pasangan beda tak nol).");
                        return NonparametrikBerhubungan.TandaBlocks(h);
                    }
                },
                new()
                {
                    Id = "mcnemar", Name = "Uji McNemar", Category = "Perbandingan",
                    Description = "Dua pengukuran biner berpasangan (0/1).",
                    Fields = { new FieldSpec("pasang", "Dua variabel biner berpasangan", FieldKind.Numeric, true, 2, 2) },
                    Run = c =>
                    {
                        var v = c.Get("pasang");
                        if (v.Count != 2) return GagalBlocks("Pilih tepat dua variabel untuk uji McNemar.");
                        if (v.Any(n => c.Data.IndexOf(n) < 0)) return GagalBlocks("Ada variabel yang tidak ditemukan dalam data.");
                        var h = NonparametrikBerhubungan.UjiMcNemar(
                                    NonparametrikBerhubungan.KolomDouble(c.Data, v[0]),
                                    NonparametrikBerhubungan.KolomDouble(c.Data, v[1]));
                        if (h is null) return GagalBlocks("Data tidak cukup (butuh sel diskordan b/c ≥ 1).");
                        return NonparametrikBerhubungan.McNemarBlocks(h);
                    }
                },
                new()
                {
                    Id = "kendall_w", Name = "Kesepakatan Kendall's W", Category = "Perbandingan",
                    Description = "Kesepakatan k penilai menilai n subjek.",
                    Fields = { new FieldSpec("ukuran", "Perlakuan yang dinilai (≥ 3)", FieldKind.Numeric, true, 3) },
                    Run = c =>
                    {
                        var v = c.Get("ukuran");
                        if (v.Count < 3) return GagalBlocks("Kendall's W butuh sedikitnya 3 perlakuan.");
                        if (v.Any(n => c.Data.IndexOf(n) < 0)) return GagalBlocks("Ada variabel yang tidak ditemukan dalam data.");
                        var h = NonparametrikBerhubungan.UjiKendallW(
                                    NonparametrikBerhubungan.KolomKelompokDouble(c.Data, v));
                        if (h is null) return GagalBlocks("Data tidak cukup (butuh ≥ 3 perlakuan dan ≥ 2 subjek).");
                        return NonparametrikBerhubungan.KendallWBlocks(h);
                    }
                },
                new()
                {
                    Id = "cochran_q", Name = "Uji Cochran's Q", Category = "Perbandingan",
                    Description = "k ukuran biner berpasangan (0/1).",
                    Fields = { new FieldSpec("ukuran", "Ukuran biner berpasangan (≥ 2)", FieldKind.Numeric, true, 2) },
                    Run = c =>
                    {
                        var v = c.Get("ukuran");
                        if (v.Count < 2) return GagalBlocks("Cochran's Q butuh sedikitnya 2 ukuran.");
                        if (v.Any(n => c.Data.IndexOf(n) < 0)) return GagalBlocks("Ada variabel yang tidak ditemukan dalam data.");
                        var h = NonparametrikBerhubungan.UjiCochranQ(
                                    NonparametrikBerhubungan.KolomKelompokDouble(c.Data, v));
                        if (h is null) return GagalBlocks("Data tidak cukup (butuh ≥ 2 ukuran dan ≥ 2 subjek).");
                        return NonparametrikBerhubungan.CochranQBlocks(h);
                    }
                },
                new()
                {
                    Id = "kruskal", Name = "Kruskal–Wallis", Category = "Perbandingan",
                    Description = "Pengganti ANOVA satu arah bila data tidak normal.",
                    Fields =
                    {
                        new FieldSpec("nilai", "Variabel nilai", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("faktor", "Variabel kelompok", FieldKind.Categorical, false, 1, 1)
                    },
                    Run = c => Compare.KruskalWallis(c.Data, c.First("nilai"), c.First("faktor"), c.Alpha)
                },
                new()
                {
                    Id = "levene", Name = "Uji Levene (kesamaan ragam)", Category = "Perbandingan",
                    Description = "Uji kesamaan ragam antar kelompok.",
                    Fields =
                    {
                        new FieldSpec("nilai", "Variabel nilai", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("faktor", "Variabel kelompok", FieldKind.Categorical, false, 1, 1)
                    },
                    Run = c => Levene.Bloks(c.Data, c.First("nilai"), c.First("faktor"), c.Alpha)
                },
                new()
                {
                    Id = "prop_satu", Name = "Uji proporsi satu sampel", Category = "Perbandingan",
                    Description = "Uji apakah proporsi kategori sukses menyimpang dari proporsi uji.",
                    Fields =
                    {
                        new FieldSpec("keluar", "Variabel biner (keluar)", FieldKind.Categorical, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("p0", "Proporsi uji (H₀)", OptionType.Number, 0.5)
                    },
                    Run = c => Proporsi.SatuBlocks(c.Data, c.First("keluar"), c.Number("p0"), c.Alpha)
                },
                new()
                {
                    Id = "prop_dua", Name = "Uji proporsi dua sampel", Category = "Perbandingan",
                    Description = "Bandingkan proporsi dua kelompok.",
                    Fields =
                    {
                        new FieldSpec("keluar", "Variabel biner (keluar)", FieldKind.Categorical, false, 1, 1),
                        new FieldSpec("grup", "Variabel kelompok (2 kategori)", FieldKind.Categorical, false, 1, 1)
                    },
                    Run = c => Proporsi.DuaBlocks(c.Data, c.First("keluar"), c.First("grup"), c.Alpha)
                },
                new()
                {
                    Id = "moses", Name = "Uji Moses reaksi ekstrem", Category = "Perbandingan",
                    Description = "Apakah satu kelompok menunjukkan nilai lebih ekstrem.",
                    Fields =
                    {
                        new FieldSpec("nilai", "Variabel nilai", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("grup", "Variabel kelompok (2 kategori)", FieldKind.Categorical, false, 1, 1)
                    },
                    Run = c => Moses.MosesBlocks(c.Data, c.First("nilai"), c.First("grup"), c.Alpha)
                },
                new()
                {
                    Id = "ks_dua", Name = "Kolmogorov–Smirnov dua sampel", Category = "Perbandingan",
                    Description = "Apakah dua sampel bebas berasal dari sebaran yang sama.",
                    Fields =
                    {
                        new FieldSpec("nilai", "Variabel nilai", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("faktor", "Variabel kelompok (tepat 2 level)", FieldKind.Categorical, false, 1, 1)
                    },
                    Run = c =>
                    {
                        string nilai = c.First("nilai");
                        string faktor = c.First("faktor");
                        var grup = NonparametrikBebas.KelompokDouble(c.Data, nilai, faktor);
                        if (grup.Count != 2) return GagalBlocks("K-S dua sampel butuh tepat 2 kelompok pada variabel kelompok.");
                        if (grup.Any(g => g.Count < 1)) return GagalBlocks("Ada kelompok yang kosong.");
                        var h = NonparametrikBebas.UjiKS2(grup[0], grup[1]);
                        if (h is null) return GagalBlocks("Data tidak cukup untuk uji K-S dua sampel.");
                        var nama = c.Data.Levels(faktor);
                        return NonparametrikBebas.KS2Blocks(h,
                            nama.Count >= 2 ? nama[0] : "sampel 1",
                            nama.Count >= 2 ? nama[1] : "sampel 2");
                    }
                },
                new()
                {
                    Id = "median_test", Name = "Uji median (k sampel)", Category = "Perbandingan",
                    Description = "Apakah median beberapa kelompok bebas sama.",
                    Fields =
                    {
                        new FieldSpec("nilai", "Variabel nilai", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("faktor", "Variabel kelompok", FieldKind.Categorical, false, 1, 1)
                    },
                    Run = c =>
                    {
                        string nilai = c.First("nilai");
                        string faktor = c.First("faktor");
                        var grup = NonparametrikBebas.KelompokDouble(c.Data, nilai, faktor);
                        if (grup.Count < 2) return GagalBlocks("Uji median butuh sedikitnya 2 kelompok.");
                        var h = NonparametrikBebas.UjiMedianTest(grup);
                        if (h is null) return GagalBlocks("Data tidak cukup untuk uji median.");
                        return NonparametrikBebas.MedianTestBlocks(h, c.Data.Levels(faktor));
                    }
                },
                new()
                {
                    Id = "korelasi", Name = "Korelasi", Category = "Korelasi & regresi",
                    Description = "Pearson, Spearman, atau Kendall antar beberapa variabel.",
                    Fields = { new FieldSpec("vars", "Variabel", FieldKind.Numeric, true, 2) },
                    Options =
                    {
                        new OptionSpec("metode", "Metode", OptionType.Choice,
                            new List<string> { "pearson", "spearman", "kendall" })
                    },
                    Run = c => Correlation.Matrix(c.Data, c.Get("vars"), c.Choice("metode"), c.Alpha)
                },
                new()
                {
                    Id = "korelasi_parsial", Name = "Korelasi parsial", Category = "Korelasi & regresi",
                    Description = "Hubungan dua variabel setelah variabel kendali dihilangkan.",
                    Fields =
                    {
                        new FieldSpec("var1", "Variabel pertama", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("var2", "Variabel kedua", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("kontrol", "Variabel kendali (boleh kosong)", FieldKind.Numeric, true, 0)
                    },
                    Run = c => Correlation.ParsialBlocks(c.Data, c.First("var1"), c.First("var2"),
                                                          c.Get("kontrol"), c.Alpha)
                },
                new()
                {
                    Id = "logistik", Name = "Regresi logistik biner", Category = "Korelasi & regresi", IsML = true,
                    Description = "Menerangkan variabel yang hanya punya dua macam nilai.",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel terikat (dua macam nilai)",
                                      FieldKind.Any, false, 1, 1),
                        new FieldSpec("bebas", "Variabel bebas", FieldKind.Numeric, true, 1)
                    },
                    Run = c => Logistic.LogisticBlocks(c.Data, c.First("dependen"), c.Get("bebas"), c.Alpha)
                },
                new()
                {
                    Id = "friedman", Name = "Uji Friedman", Category = "Perbandingan",
                    Description = "Ukur berulang nonparametrik: pengganti ANOVA berulang.",
                    Fields = { new FieldSpec("ukuran", "Perlakuan yang diukur berulang",
                                             FieldKind.Numeric, true, 3) },
                    Run = c => Nonparametrik.FriedmanBlocks(c.Data, c.Get("ukuran"), c.Alpha)
                },
                new()
                {
                    Id = "runs", Name = "Uji runs (keacakan)", Category = "Perbandingan",
                    Description = "Apakah urutan nilai acak, menggerombol, atau berosilasi.",
                    Fields = { new FieldSpec("var", "Variabel urut", FieldKind.Numeric, false, 1, 1) },
                    Options =
                    {
                        new OptionSpec("potong", "Potong pada", OptionType.Choice,
                            new List<string> { "median", "rerata", "nilai sendiri" }),
                        new OptionSpec("nilai", "Nilai potong (bila 'nilai sendiri')",
                            OptionType.Number, 0)
                    },
                    Run = c =>
                    {
                        string cara = c.Choice("potong");
                        var jenis = cara == "rerata" ? CaraPotong.Rerata
                                  : cara == "nilai sendiri" ? CaraPotong.NilaiSendiri
                                  : CaraPotong.Median;
                        return Nonparametrik.RunsBlocks(c.Data, c.First("var"), jenis,
                                                        c.Number("nilai"), c.Alpha);
                    }
                },
                new()
                {
                    Id = "binomial", Name = "Uji binomial", Category = "Perbandingan",
                    Description = "Apakah peluang sukses yang diobservasi menyimpang dari yang dihipotesiskan.",
                    Fields =
                    {
                        new FieldSpec("variabel", "Variabel dengan dua macam nilai",
                                      FieldKind.Any, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("p0", "Peluang yang dihipotesiskan",
                                       OptionType.Number, 0.5),
                        new OptionSpec("cara", "Varian", OptionType.Choice,
                                       new List<string> { "dua-sisi", "kurang", "lebih" }),
                        new OptionSpec("label_sukses", "Nilai yang dianggap 'sukses' (kosong = nilai kedua)",
                                       OptionType.Text, "")
                    },
                    Run = c =>
                    {
                        string nama = c.First("variabel");
                        double p0 = c.Number("p0");
                        string cara = c.Choice("cara");
                        string labelInput = c.Options.TryGetValue("label_sukses", out var oLabel) ? oLabel.Text : "";

                        int idx = c.Data.IndexOf(nama);
                        if (idx < 0) return GagalBlocks($"Variabel '{nama}' tidak ditemukan.");

                        var nilai = new List<string>();
                        for (int r = 0; r < c.Data.RowCount; r++)
                        {
                            string?[] baris = c.Data.Rows[r];
                            string? s = idx < baris.Length ? baris[idx] : null;
                            if (!string.IsNullOrWhiteSpace(s)) nilai.Add(s!.Trim());
                        }
                        if (nilai.Count == 0) return GagalBlocks("Tidak ada data valid untuk uji binomial.");

                        string? labelSukses;
                        if (!string.IsNullOrWhiteSpace(labelInput))
                        {
                            labelSukses = labelInput.Trim();
                            if (!nilai.Contains(labelSukses))
                                return GagalBlocks($"Nilai sukses '{labelSukses}' tidak ditemukan di data.");
                        }
                        else
                        {
                            var unik = nilai.Distinct(StringComparer.Ordinal).ToList();
                            if (unik.Count != 2) return GagalBlocks("Variabel harus punya tepat dua macam nilai.");
                            labelSukses = unik[0];
                        }

                        int k = nilai.Count(v => v == labelSukses);
                        int n = nilai.Count;
                        var h = Binomial.Uji(k, n, p0, cara);
                        if (h is null) return GagalBlocks("Data tidak memadai untuk uji binomial.");
                        return Binomial.BinomialBlocks(h);
                    }
                },
                new()
                {
                    Id = "ks_satu", Name = "Uji Kolmogorov–Smirnov satu sampel", Category = "Perbandingan",
                    Description = "Apakah data berasal dari sebaran normal (uji kesesuaian sebaran).",
                    Fields = { new FieldSpec("var", "Variabel", FieldKind.Numeric, false, 1, 1) },
                    Run = c => Normality.KSBlocks(c.Data, c.First("var"))
                },
                new()
                {
                    Id = "regresi", Name = "Regresi linear", Category = "Korelasi & regresi", IsML = true,
                    Description = "Menerangkan satu variabel dari beberapa variabel bebas.",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel terikat", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("bebas", "Variabel bebas", FieldKind.Numeric, true, 1)
                    },
                    Run = c => Regression.Linear(c.Data, c.First("dependen"), c.Get("bebas"), c.Alpha)
                },
                new()
                {
                    Id = "ridge", Name = "Regresi Ridge", Category = "Korelasi & regresi", IsML = true,
                    Description = "Regresi linear dengan penalti L2 untuk prediktor yang berkorelasi.",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel terikat", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("bebas", "Variabel bebas", FieldKind.Numeric, true, 1)
                    },
                    Options =
                    {
                        new OptionSpec("alpha", "Parameter penalti (α)", OptionType.Number, 1.0)
                    },
                    Run = c => Ridge.RidgeBlocks(c.Data, c.First("dependen"), c.Get("bebas"),
                                                 c.Number("alpha"), c.Alpha)
                },
                new()
                {
                    Id = "probit", Name = "Regresi Probit biner", Category = "Korelasi & regresi", IsML = true,
                    Description = "Regresi biner dengan link probit.",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel terikat (dua macam nilai)",
                                      FieldKind.Any, false, 1, 1),
                        new FieldSpec("bebas", "Variabel bebas", FieldKind.Numeric, true, 1)
                    },
                    Run = c => Probit.ProbitBlocks(c.Data, c.First("dependen"), c.Get("bebas"), c.Alpha)
                },
                new()
                {
                    Id = "multinomial", Name = "Regresi Multinomial", Category = "Korelasi & regresi", IsML = true,
                    Description = "Regresi logit bersar untuk variabel terikat berganda (>=3 kategori) - ",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel terikat (>=3 kategori)",
                                      FieldKind.Any, false, 1, 1),
                        new FieldSpec("bebas", "Variabel bebas", FieldKind.Numeric, true, 1)
                    },
                    Run = c => Multinomial.MultinomialBlocks(c.Data, c.First("dependen"), c.Get("bebas"), c.Alpha)
                },
                new()
                {
                    Id = "ordinal", Name = "Regresi Ordinal", Category = "Korelasi & regresi", IsML = true,
                    Description = "Regresi logit kumulatif untuk variabel terikat berurutan (>=3 kategori) - ",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel terikat (>=3 kategori berurutan)",
                                      FieldKind.Any, false, 1, 1),
                        new FieldSpec("bebas", "Variabel bebas", FieldKind.Numeric, true, 1)
                    },
                    Run = c => Ordinal.OrdinalBlocks(c.Data, c.First("dependen"), c.Get("bebas"), c.Alpha)
                },
                new()
                {
                    Id = "silang", Name = "Tabel silang & chi-kuadrat", Category = "Kategori",
                    Description = "Uji hubungan dua variabel kategori.",
                    Fields =
                    {
                        new FieldSpec("baris", "Variabel baris", FieldKind.Categorical, false, 1, 1),
                        new FieldSpec("kolom", "Variabel kolom", FieldKind.Categorical, false, 1, 1)
                    },
                    Run = c => Crosstab.Analyze(c.Data, c.First("baris"), c.First("kolom"), c.Alpha)
                },
                new()
                {
                    Id = "chi_gof", Name = "Uji chi-kuadrat kesesuaian", Category = "Kategori",
                    Description = "Apakah frekuensi kategori menyimpang dari harapan.",
                    Fields =
                    {
                        new FieldSpec("var", "Variabel kategori", FieldKind.Categorical, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("peluang", "Proporsi harapan (pisah spasi; kosong = sama rata)",
                            OptionType.Text, "")
                    },
                    Run = c =>
                    {
                        string nama = c.First("var");
                        var (kategori, hitung) = NonparametrikBebas.HitungKategori(c.Data, nama);
                        if (kategori.Count < 2) return GagalBlocks("Variabel kategori butuh sedikitnya 2 macam nilai.");
                        if (hitung.Sum() < 2) return GagalBlocks("Tidak ada data valid untuk uji chi-kuadrat.");

                        List<double> props;
                        string teksProp = c.Options.TryGetValue("peluang", out var o) ? o.Text : "";
                        if (!string.IsNullOrWhiteSpace(teksProp))
                        {
                            var angka = teksProp.Split(new[] { ' ', ',', ';', '\t' },
                                StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => double.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : double.NaN)
                                .Where(v => !double.IsNaN(v) && v > 0)
                                .ToList();
                            if (angka.Count != kategori.Count)
                                return GagalBlocks($"Banyak proporsi ({angka.Count}) harus sama dengan banyak kategori ({kategori.Count}).");
                            props = angka;
                        }
                        else
                        {
                            props = kategori.Select(_ => 1.0).ToList();
                        }

                        var h = NonparametrikBebas.UjiChiGOF(kategori, hitung, props);
                        return NonparametrikBebas.ChiGOFBlocks(h, nama);
                    }
                },
                new()
                {
                    Id = "reliabilitas", Name = "Reliabilitas (alfa Cronbach)", Category = "Reliabilitas",
                    Description = "Konsistensi internal kuesioner berskala banyak aitem.",
                    Fields = { new FieldSpec("aitem", "Aitem", FieldKind.Numeric, true, 2) },
                    Run = c => Reliability.CronbachAlpha(c.Data, c.Get("aitem"))
                },
                new()
                {
                    Id = "faktor", Name = "Analisis faktor (PCA/EFA)", Category = "Multivariat", IsML = true,
                    Description = "Meringkas banyak variabel menjadi beberapa faktor.",
                    Fields = { new FieldSpec("var", "Variabel", FieldKind.Numeric, true, 3) },
                    Options =
                    {
                        new OptionSpec("metode", "Cara ekstraksi", OptionType.Choice,
                            new List<string> { "pca", "paf" }),
                        new OptionSpec("rotasi", "Rotasi", OptionType.Choice,
                            new List<string> { "varimax", "none" }),
                        new OptionSpec("jumlah", "Banyak faktor (0 = aturan Kaiser, nilai eigen > 1)",
                            OptionType.Number, 0)
                    },
                    Run = c =>
                    {
                        int jumlah = (int)Math.Round(c.Number("jumlah"));
                        return Faktor.FaktorBlocks(c.Data, c.Get("var"),
                                                   string.IsNullOrEmpty(c.Choice("metode")) ? "pca" : c.Choice("metode"),
                                                   string.IsNullOrEmpty(c.Choice("rotasi")) ? "varimax" : c.Choice("rotasi"),
                                                   Math.Max(0, jumlah));
                    }
                },
                new()
                {
                    Id = "hierarki", Name = "Klaster hierarkis (dendrogram)", Category = "Klaster", IsML = true,
                    Description = "Pohon penggabungan amatan.",
                    Fields = { new FieldSpec("var", "Variabel", FieldKind.Numeric, true, 1) },
                    Options =
                    {
                        new OptionSpec("cara", "Cara penggabungan", OptionType.Choice,
                            new List<string> { "ward", "single", "complete", "average", "centroid", "median" }),
                        new OptionSpec("klaster", "Potong pada berapa klaster", OptionType.Number, 2),
                        new OptionSpec("standar", "Seragamkan variabel jadi z-skor lebih dulu",
                            OptionType.Bool, true)
                    },
                    Run = c =>
                    {
                        int k = (int)Math.Round(c.Number("klaster"));
                        return Hierarki.HierarkiBlocks(c.Data, c.Get("var"),
                                                       string.IsNullOrEmpty(c.Choice("cara")) ? "ward" : c.Choice("cara"),
                                                       Math.Max(1, k), c.IsChecked("standar"));
                    }
                },
                new()
                {
                    Id = "roc", Name = "Kurva ROC & AUC", Category = "Perbandingan", IsML = true,
                    Description = "Seberapa baik sebuah penanda memisahkan dua keadaan.",
                    Fields =
                    {
                        new FieldSpec("penanda", "Variabel penanda (angka)", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("emas", "Variabel keadaan (dua macam nilai)", FieldKind.Any, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("positif", "Nilai yang dianggap keadaan positif (kosong = nilai kedua)",
                            OptionType.Text, "")
                    },
                    Run = c =>
                    {
                        string teks = c.Options.TryGetValue("positif", out var o) ? o.Text : "";
                        return Roc.RocBlocks(c.Data, c.First("penanda"), c.First("emas"), teks ?? "");
                    }
                },
                new()
                {
                    Id = "plot_sebaran", Name = "Plot P–P & Q–Q normal", Category = "Kenormalan",
                    Description = "Periksa kenormalan secara visual.",
                    Fields = { new FieldSpec("var", "Variabel", FieldKind.Numeric, false, 1, 1) },
                    Options =
                    {
                        new OptionSpec("jenis", "Plot yang ditampilkan", OptionType.Choice,
                            new List<string> { "keduanya", "qq", "pp" })
                    },
                    Run = c => PlotSebaran.PlotSebaranBlocks(c.Data, c.First("var"),
                                    string.IsNullOrEmpty(c.Choice("jenis")) ? "keduanya" : c.Choice("jenis"))
                },
                new()
                {
                    Id = "kaplan_meier", Name = "Kaplan–Meier & log-rank", Category = "Survival",
                    Description = "Estimator survival nonparametrik dan uji perbandingan antar grup.",
                    Fields =
                    {
                        new FieldSpec("waktu", "Waktu (time-to-event)", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("status", "Status (1=event, 0=sensor)", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("grup", "Grup perbandingan (opsional)", FieldKind.Any, false, 0, 1)
                    },
                    Run = c => KaplanMeier.Hitung(c.Data, c.First("waktu"), c.First("status"),
                                                   c.First("grup"))
                },
                new()
                {
                    Id = "acf_pacf", Name = "ACF & PACF (deret waktu)", Category = "Deret waktu",
                    Description = "Autokorelasi, autokorelasi parsial, dan uji Ljung–Box.",
                    Fields = { new FieldSpec("var", "Variabel deret waktu", FieldKind.Numeric, false, 1, 1) },
                    Options = { new OptionSpec("lag", "Lag maksimum", OptionType.Number, 12) },
                    Run = c =>
                    {
                        int m = (int)Math.Round(c.Number("lag"));
                        return AcfPacf.Hitung(c.Data, c.First("var"), m <= 0 ? 12 : m);
                    }
                },
                new()
                {
                    Id = "bootstrap", Name = "Bootstrap (selang kepercayaan)", Category = "Deskriptif",
                    Description = "Selang kepercayaan tanpa asumsi sebaran — metode persentil dan BCa.",
                    Fields =
                    {
                        new FieldSpec("var", "Variabel yang di-bootstrap", FieldKind.Numeric, true, 1, 8),
                        new FieldSpec("grup", "Grup untuk selisih rerata (opsional)", FieldKind.Any, false, 0, 1)
                    },
                    Options =
                    {
                        new OptionSpec("ulangan", "Banyak ulangan resample", OptionType.Number, 2000),
                        new OptionSpec("benih", "Benih acak (hasil bisa diulang)", OptionType.Number, 20260924),
                        new OptionSpec("keyakinan", "Tingkat keyakinan", OptionType.Number, 0.95),
                        new OptionSpec("metode", "Metode selang", OptionType.Choice,
                                       new List<string> { "BCa (bias-corrected & accelerated)", "Persentil" }),
                        new OptionSpec("statistik", "Statistik yang di-bootstrap", OptionType.Choice,
                                       new List<string> { "Otomatis", "Korelasi Pearson (2 var)",
                                                          "Kemiringan OLS (2 var)", "Uji permutasi" })
                    },
                    Run = c => Bootstrap.BootstrapBlocks(
                        c.Data, c.Get("var"), c.First("grup"),
                        (int)Math.Round(c.Number("ulangan")),
                        (ulong)Math.Round(c.Number("benih")),
                        c.Number("keyakinan"),
                        !c.Choice("metode").StartsWith("Persentil", StringComparison.OrdinalIgnoreCase),
                        c.Choice("statistik"))
                },
                new()
                {
                    Id = "wls", Name = "Regresi kuadrat terkecil berbobot (WLS)", Category = "Korelasi & regresi",
                    Description = "Regresi untuk data yang ragam galatnya tidak seragam, "
                                  + "dengan bobot per amatan.",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel terikat", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("bebas", "Variabel bebas", FieldKind.Numeric, true, 1, 8),
                        new FieldSpec("bobot", "Variabel bobot (harus > 0)", FieldKind.Numeric, false, 1, 1)
                    },
                    Run = c => Glm.WlsBlocks(c.Data, c.First("dependen"), c.Get("bebas"),
                                             c.First("bobot"))
                },
                new()
                {
                    Id = "poisson", Name = "Regresi Poisson (data cacahan)", Category = "Korelasi & regresi",
                    Description = "Regresi untuk respons cacahan (0, 1, 2, …) dengan taut log.",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel cacahan", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("bebas", "Variabel bebas", FieldKind.Numeric, true, 1, 8)
                    },
                    Run = c => Glm.PoissonBlocks(c.Data, c.First("dependen"), c.Get("bebas"))
                },
                new()
                {
                    Id = "ancova", Name = "ANCOVA (ANOVA dengan kovariat)", Category = "Perbandingan",
                    Description = "Membandingkan rerata kelompok setelah perbedaan kovariat "
                                  + "disingkirkan; uji Type III dan rerata disesuaikan.",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel terikat", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("faktor", "Variabel pengelompok", FieldKind.Any, false, 1, 1),
                        new FieldSpec("kovariat", "Kovariat", FieldKind.Numeric, false, 1, 1)
                    },
                    Run = c => Ancova.AncovaBlocks(c.Data, c.First("dependen"), c.First("faktor"),
                                                   c.First("kovariat"))
                },
                new()
                {
                    Id = "regresi_nonlinear", Name = "Regresi non-linear (kurva)", Category = "Korelasi & regresi",
                    Description = "Menyesuaikan kurva eksponensial atau pangkat dengan "
                                  + "Levenberg–Marquardt.",
                    Fields =
                    {
                        new FieldSpec("x", "Variabel sumbu X", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("y", "Variabel sumbu Y", FieldKind.Numeric, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("model", "Bentuk kurva", OptionType.Choice,
                                       new List<string> { "Eksponensial  y = a·e^(b·x)",
                                                          "Pangkat  y = a·x^b" })
                    },
                    Run = c =>
                    {
                        var model = c.Choice("model").StartsWith("Pangkat", StringComparison.OrdinalIgnoreCase)
                            ? ModelKurva.Pangkat : ModelKurva.Eksponensial;
                        return KurvaNonlinear.KurvaBlocks(c.Data, c.First("x"), c.First("y"), model);
                    }
                },
                new()
                {
                    Id = "quantile", Name = "Regresi kuantil", Category = "Korelasi & regresi",
                    Description = "Menaksir hubungan antar-kuantil τ ∈ (0,1) — melengkapi OLS. " +
                                  "Galat baku robust, uji t memakai Student-t.",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel terikat", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("bebas", "Variabel bebas", FieldKind.Numeric, true, 1, 8)
                    },
                    Options =
                    {
                        new OptionSpec("quantiles", "Kuantil τ (pisahkan dengan koma, 0<τ<1)",
                                       OptionType.Text, "0.25,0.5,0.75")
                    },
                    Run = c => QuantileRegression.QuantileBlocks(c.Data, c.First("dependen"),
                                                                  c.Get("bebas"),
                                                                  c.Options["quantiles"].Text)
                },
                new()
                {
                    Id = "glm_multi", Name = "GLM multifaktor (Gamma / Binomial)",
                    Category = "Korelasi & regresi",
                    Description = "Model linear terampat multifaktor: Gamma taut-log "
                                  + "untuk respon positif, Binomial taut-logit untuk "
                                  + "proporsi — lengkap dengan uji tiap faktor.",
                    Fields =
                    {
                        new FieldSpec("respon", "Variabel respon", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("percobaan", "Banyak percobaan (khusus Binomial)",
                                      FieldKind.Numeric, false, 0, 1),
                        new FieldSpec("faktor", "Faktor (kategori)", FieldKind.Categorical, true, 1, 4),
                        new FieldSpec("kovariat", "Kovariat (angka, opsional)",
                                      FieldKind.Numeric, true, 0, 8)
                    },
                    Options =
                    {
                        new OptionSpec("keluarga", "Keluarga sebaran", OptionType.Choice,
                            new List<string> { "Gamma (taut log)", "Binomial (taut logit)" })
                    },
                    Run = c => GlmMulti.GlmMultiBlocks(c.Data, c.First("respon"),
                        c.Get("percobaan").Count > 0 ? c.First("percobaan") : "",
                        c.Get("faktor"), c.Get("kovariat"), c.Choice("keluarga"))
                },
                new()
                {
                    Id = "campur", Name = "Model campuran (intersep acak)",
                    Category = "Korelasi & regresi",
                    Description = "Regresi dengan intersep acak per kelompok "
                                  + "(REML): efek tetap GLS, komponen ragam, "
                                  + "ICC, dan BLUP tiap kelompok.",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel respon", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("bebas", "Prediktor efek tetap", FieldKind.Numeric, true, 1, 8),
                        new FieldSpec("grup", "Variabel kelompok", FieldKind.Categorical, false, 1, 1)
                    },
                    Run = c => Campur.CampurBlocks(c.Data, c.First("dependen"),
                                                   c.Get("bebas"), c.First("grup"), c.Alpha)
                },
                new()
                {
                    Id = "glmm", Name = "GLMM (respon non-normal berkelompok)",
                    Category = "Korelasi & regresi",
                    Description = "Binomial/Poisson dengan intersep acak per "
                                  + "kelompok (PQL): efek tetap, komponen ragam, "
                                  + "dan BLUP tiap kelompok.",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel respon", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("percobaan", "Banyak percobaan (khusus Binomial)",
                                      FieldKind.Numeric, false, 0, 1),
                        new FieldSpec("bebas", "Prediktor efek tetap", FieldKind.Numeric, true, 1, 8),
                        new FieldSpec("grup", "Variabel kelompok", FieldKind.Categorical, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("keluarga", "Keluarga sebaran", OptionType.Choice,
                            new List<string> { "Binomial (taut logit)", "Poisson (taut log)" })
                    },
                    Run = c => Glmm.GlmmBlocks(c.Data, c.First("dependen"),
                        c.Get("percobaan").Count > 0 ? c.First("percobaan") : "",
                        c.Get("bebas"), c.First("grup"), c.Choice("keluarga"))
                },
                new()
                {
                    Id = "chaid", Name = "CHAID (pohon khi-kuadrat)",
                    Category = "Pohon & ensembel", IsML = true,
                    Description = "Pohon klasifikasi multi-arah: gabung kategori "
                                  + "tak-berbeda (khi-kuadrat), belah Bonferroni.",
                    Fields =
                    {
                        new FieldSpec("prediktor", "Prediktor (kategori/angka)", FieldKind.Any, true, 1, 8),
                        new FieldSpec("target", "Target kategori", FieldKind.Categorical, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("alpha_gabung", "Alfa penggabungan", OptionType.Number, 0.05),
                        new OptionSpec("alpha_belah", "Alfa pembelahan", OptionType.Number, 0.05),
                        new OptionSpec("min_anak", "Minimum isi simpul anak", OptionType.Number, 10),
                        new OptionSpec("maks_dalam", "Kedalaman maksimum", OptionType.Number, 4)
                    },
                    Run = c => Chaid.ChaidBlocks(c.Data, c.Get("prediktor"),
                        c.First("target"), c.Number("alpha_gabung"), c.Number("alpha_belah"),
                        (int)c.Number("min_anak"), (int)c.Number("maks_dalam"))
                },
                new()
                {
                    Id = "c45", Name = "C4.5 (pohon nisbah gain)",
                    Category = "Pohon & ensembel", IsML = true,
                    Description = "Pohon klasifikasi nisbah gain: multi-arah "
                                  + "kategorik, ambang numerik, pangkas pesimis.",
                    Fields =
                    {
                        new FieldSpec("prediktor", "Prediktor (kategori/angka)", FieldKind.Any, true, 1, 8),
                        new FieldSpec("target", "Target kategori", FieldKind.Categorical, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("cf", "Kepercayaan pangkas (CF)", OptionType.Number, 0.25),
                        new OptionSpec("min_belah", "Minimum belah simpul", OptionType.Number, 2),
                        new OptionSpec("maks_dalam", "Kedalaman maksimum", OptionType.Number, 8)
                    },
                    Run = c => C45.C45Blocks(c.Data, c.Get("prediktor"),
                        c.First("target"), c.Number("cf"),
                        (int)c.Number("min_belah"), (int)c.Number("maks_dalam"))
                },
                new()
                {
                    Id = "boosting", Name = "Boosting (gradien / AdaBoost)",
                    Category = "Pohon & ensembel", IsML = true,
                    Description = "Ensembel sekuensial: gradien untuk regresi, "
                                  + "AdaBoost SAMME untuk klasifikasi — jejak "
                                  + "ronde deterministik.",
                    Fields =
                    {
                        new FieldSpec("fitur", "Fitur (angka)", FieldKind.Numeric, true, 1, 8),
                        new FieldSpec("target", "Target (kategori/angka)", FieldKind.Any, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("mode", "Mode", OptionType.Choice,
                            new List<string> { "Otomatis", "Klasifikasi", "Regresi" }),
                        new OptionSpec("ronde", "Banyak ronde", OptionType.Number, 100),
                        new OptionSpec("lr", "Laju susut (0–1]", OptionType.Number, 0.1),
                        new OptionSpec("maks_dalam", "Kedalaman maksimum pohon", OptionType.Number, 3),
                        new OptionSpec("min_belah", "Minimum belah simpul", OptionType.Number, 2)
                    },
                    Run = c => Boosting.BoostingBlocks(c.Data, c.Get("fitur"),
                        c.First("target"), c.Choice("mode"),
                        (int)c.Number("ronde"), c.Number("lr"),
                        (int)c.Number("maks_dalam"), (int)c.Number("min_belah"))
                },
                new()
                {
                    Id = "imputasi", Name = "Imputasi berganda (MICE)",
                    Category = "Deskriptif",
                    Description = "Isi nilai hilang m kali (chained equations "
                                  + "Bayes) + gabungan Rubin: rerata dan "
                                  + "regresi OLS.",
                    Fields =
                    {
                        new FieldSpec("numerik", "Variabel numerik", FieldKind.Numeric, true, 0, 8),
                        new FieldSpec("biner", "Variabel biner (2 aras)", FieldKind.Categorical, true, 0, 8),
                        new FieldSpec("dependen", "Terikat regresi tergabung (opsional)",
                                      FieldKind.Numeric, false, 0, 1),
                        new FieldSpec("bebas", "Bebas regresi tergabung (opsional)",
                                      FieldKind.Numeric, true, 0, 8)
                    },
                    Options =
                    {
                        new OptionSpec("m", "Banyak imputasi", OptionType.Number, 5),
                        new OptionSpec("siklus", "Siklus rantai", OptionType.Number, 10),
                        new OptionSpec("benih", "Benih acak (hasil bisa diulang)", OptionType.Number, 20260930)
                    },
                    Run = c => Imputasi.ImputasiBlocks(c.Data, c.Get("numerik"), c.Get("biner"),
                        c.First("dependen"), c.Get("bebas"),
                        (int)c.Number("m"), (int)c.Number("siklus"),
                        (uint)Math.Max(1, c.Number("benih")))
                },
                new()
                {
                    Id = "cox", Name = "Regresi Cox proportional hazards",
                    Category = "Survival",
                    Description = "Laju kejadian dengan sensor kanan: kemungkinan "
                                  + "parsial Cox, ikatan Breslow, fungsi dasar, "
                                  + "dan nisbah hazard.",
                    Fields =
                    {
                        new FieldSpec("waktu", "Waktu amatan", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("status", "Status (1 = kejadian, 0 = tersensor)",
                                      FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("bebas", "Kovariat", FieldKind.Numeric, true, 1, 8)
                    },
                    Run = c => Cox.CoxBlocks(c.Data, c.First("waktu"),
                                             c.First("status"), c.Get("bebas"), c.Alpha)
                },
                new()
                {
                    Id = "musiman", Name = "Dekomposisi & ramalan deret waktu",
                    Category = "Deret waktu",
                    Description = "Uraian tren–musiman–residu aditif dan "
                                  + "peramalan Holt-Winters (parameter tetap).",
                    Fields =
                    {
                        new FieldSpec("var", "Variabel deret waktu", FieldKind.Numeric, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("metode", "Metode", OptionType.Choice,
                            new List<string> { "Dekomposisi musiman", "Holt-Winters aditif" }),
                        new OptionSpec("periode", "Periode musiman", OptionType.Number, 12),
                        new OptionSpec("alpha", "Alpha (level)", OptionType.Number, 0.6),
                        new OptionSpec("beta", "Beta (tren)", OptionType.Number, 0.2),
                        new OptionSpec("gamma", "Gamma (musiman)", OptionType.Number, 0.4),
                        new OptionSpec("horizon", "Horizon ramalan", OptionType.Number, 6)
                    },
                    Run = c => Musiman.MusimanBlocks(c.Data, c.First("var"), c.Choice("metode"),
                        (int)c.Number("periode"), c.Number("alpha"), c.Number("beta"),
                        c.Number("gamma"), (int)c.Number("horizon"))
                },
                new()
                {
                    Id = "korespondensi", Name = "Analisis korespondensi",
                    Category = "Multivariat",
                    Description = "Peta hubungan baris–kolom tabel kontingensi: "
                                  + "inersia, koordinat principal, kontribusi.",
                    Fields =
                    {
                        new FieldSpec("baris", "Variabel baris", FieldKind.Categorical, false, 1, 1),
                        new FieldSpec("kolom", "Variabel kolom", FieldKind.Categorical, false, 1, 1)
                    },
                    Run = c => Korespondensi.KorespondensiBlocks(c.Data, c.First("baris"),
                                                                c.First("kolom"), c.Alpha)
                },
                new()
                {
                    Id = "princals", Name = "PRINCALS (penskalaan optimal)",
                    Category = "Multivariat",
                    Description = "Reduksi dimensi campuran nominal/ordinal/"
                                  + "numerik (ALS): skor objek 2D, kuantifikasi "
                                  + "kategori, eigen dan alfa per dimensi.",
                    Fields =
                    {
                        new FieldSpec("nominal", "Variabel nominal", FieldKind.Categorical, true, 0, 8),
                        new FieldSpec("ordinal", "Variabel ordinal", FieldKind.Any, true, 0, 8),
                        new FieldSpec("numerik", "Variabel numerik", FieldKind.Numeric, true, 0, 8)
                    },
                    Run = c => Princals.PrincalsBlocks(c.Data, c.Get("nominal"),
                                                       c.Get("ordinal"), c.Get("numerik"))
                },
                new()
                {
                    Id = "klasifikasi", Name = "Klasifikasi (k-NN / LDA / QDA)",
                    Category = "Klaster",
                    Description = "Duga kelas pendatang: suara tetangga (k-NN) "
                                  + "atau diskriminan Gauss (LDA/QDA) — lengkap "
                                  + "dengan matriks kebingungan dan LOO.",
                    Fields =
                    {
                        new FieldSpec("fitur", "Fitur (angka)", FieldKind.Numeric, true, 1, 8),
                        new FieldSpec("kelas", "Kelas (kategori)", FieldKind.Categorical, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("metode", "Metode", OptionType.Choice,
                            new List<string> { "k-NN", "LDA", "QDA" }),
                        new OptionSpec("k", "Tetangga (khusus k-NN)", OptionType.Number, 5)
                    },
                    Run = c => Klasifikasi.KlasifikasiBlocks(c.Data, c.Get("fitur"),
                        c.First("kelas"), c.Choice("metode"), (int)c.Number("k"))
                },
                new()
                {
                    Id = "twostep", Name = "Klaster TwoStep (campuran)",
                    Category = "Klaster", IsML = true,
                    Description = "Kelompokkan baris berdasar angka + kategori: "
                                  + "jarak log-likelihood, BIC pilih k otomatis, "
                                  + "siluet Gower pemeriksa mutu.",
                    Fields =
                    {
                        new FieldSpec("numerik", "Variabel angka", FieldKind.Numeric, true, 1, 8),
                        new FieldSpec("kategori", "Variabel kategori", FieldKind.Categorical, true, 1, 4)
                    },
                    Options =
                    {
                        new OptionSpec("k_maks", "BIC dinilai sampai k =", OptionType.Number, 8)
                    },
                    Run = c => TwoStep.TwoStepBlocks(c.Data, c.Get("numerik"),
                        c.Get("kategori"), (int)c.Number("k_maks"))
                },
                new()
                {
                    Id = "arima", Name = "ARIMA (Box–Jenkins)",
                    Category = "Deret waktu", IsML = true,
                    Description = "Ramalan deret waktu: autoregresi + diferensiasi "
                                  + "+ rerata bergerak, ditaksir dengan MLE Kalman "
                                  + "eksak.",
                    Fields =
                    {
                        new FieldSpec("deret", "Deret waktu (angka)", FieldKind.Numeric, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("p", "Ordo AR (0–2)", OptionType.Number, 1),
                        new OptionSpec("d", "Diferensiasi (0–1)", OptionType.Number, 1),
                        new OptionSpec("q", "Ordo MA (0–2)", OptionType.Number, 1),
                        new OptionSpec("P", "Ordo AR musiman (0–1)", OptionType.Number, 0),
                        new OptionSpec("D", "Diferensiasi musiman (0–1)", OptionType.Number, 0),
                        new OptionSpec("Q", "Ordo MA musiman (0–1)", OptionType.Number, 0),
                        new OptionSpec("s", "Periode musiman", OptionType.Number, 12),
                        new OptionSpec("ramal", "Langkah ramalan", OptionType.Number, 8)
                    },
                    Run = c => Arima.ArimaBlocks(c.Data, c.First("deret"),
                        (int)c.Number("p"), (int)c.Number("d"),
                        (int)c.Number("q"), (int)c.Number("ramal"),
                        (int)c.Number("P"), (int)c.Number("D"),
                        (int)c.Number("Q"), (int)c.Number("s"))
                },
                new()
                {
                    Id = "pakar", Name = "Expert Modeler lite (pemilih model)",
                    Category = "Deret waktu", IsML = true,
                    Description = "Pilih model deret waktu otomatis: turnamen "
                                  + "RMSE rolling-origin atas 12 ordo musiman.",
                    Fields =
                    {
                        new FieldSpec("deret", "Deret waktu bulanan (angka)", FieldKind.Numeric, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("ramal", "Langkah ramalan", OptionType.Number, 12),
                        new OptionSpec("s", "Periode musiman (1 = tak-musiman)", OptionType.Number, 12)
                    },
                    Run = c => Pakar.PakarBlocks(c.Data, c.First("deret"),
                        (int)c.Number("ramal"), (int)c.Number("s"))
                },
                new()
                {
                    Id = "pohon", Name = "Pohon keputusan (CART)",
                    Category = "Pohon & ensembel", IsML = true,
                    Description = "Klasifikasi / regresi pohon: belah Gini / SSE "
                                  + "rekursif dengan batas kedalaman.",
                    Fields =
                    {
                        new FieldSpec("fitur", "Fitur (angka)", FieldKind.Numeric, true, 1, 8),
                        new FieldSpec("target", "Target (kategori/angka)", FieldKind.Any, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("mode", "Mode", OptionType.Choice,
                            new List<string> { "Otomatis", "Klasifikasi", "Regresi" }),
                        new OptionSpec("maks_kedalaman", "Kedalaman maksimum", OptionType.Number, 4),
                        new OptionSpec("min_belah", "Minimum belah simpul", OptionType.Number, 2)
                    },
                    Run = c => Pohon.PohonBlocks(c.Data, c.Get("fitur"),
                        c.First("target"), c.Choice("mode"),
                        (int)c.Number("maks_kedalaman"), (int)c.Number("min_belah"))
                },
                new()
                {
                    Id = "hutan", Name = "Random forest",
                    Category = "Pohon & ensembel", IsML = true,
                    Description = "Klasifikasi / regresi ensembel: ratusan pohon "
                                  + "bootstrap + subruang acak, duga OOB, "
                                  + "kepentingan permutasi.",
                    Fields =
                    {
                        new FieldSpec("fitur", "Fitur (angka)", FieldKind.Numeric, true, 1, 8),
                        new FieldSpec("target", "Target (kategori/angka)", FieldKind.Any, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("mode", "Mode", OptionType.Choice,
                            new List<string> { "Otomatis", "Klasifikasi", "Regresi" }),
                        new OptionSpec("pohon", "Banyak pohon", OptionType.Number, 200),
                        new OptionSpec("mcoba", "Fitur per belahan (0 = bawaan √p / p÷3)",
                            OptionType.Number, 0),
                        new OptionSpec("maks_kedalaman", "Kedalaman maksimum", OptionType.Number, 6),
                        new OptionSpec("min_belah", "Minimum belah simpul", OptionType.Number, 2),
                        new OptionSpec("benih", "Benih acak (hasil bisa diulang)", OptionType.Number, 42)
                    },
                    Run = c => Hutan.HutanBlocks(c.Data, c.Get("fitur"),
                        c.First("target"), c.Choice("mode"),
                        (int)c.Number("pohon"), (int)c.Number("mcoba"),
                        (int)c.Number("maks_kedalaman"), (int)c.Number("min_belah"),
                        (uint)Math.Max(1, c.Number("benih")))
                },
                new()
                {
                    Id = "dbscan", Name = "DBSCAN (gugus kepadatan)",
                    Category = "Klaster", IsML = true,
                    Description = "Kelompokkan titik rapat, tandai sisanya derau: "
                                  + "tanpa menentukan banyak klaster di muka.",
                    Fields =
                    {
                        new FieldSpec("var", "Variabel (angka)", FieldKind.Numeric, true, 1, 8)
                    },
                    Options =
                    {
                        new OptionSpec("eps", "Jari-jari ketetanggaan", OptionType.Number, 0.9),
                        new OptionSpec("min_titik", "Titik minimum inti", OptionType.Number, 5)
                    },
                    Run = c => Dbscan.DbscanBlocks(c.Data, c.Get("var"),
                        c.Number("eps"), (int)c.Number("min_titik"))
                },
                new()
                {
                    Id = "cacah", Name = "Regresi cacahan lanjut (ZIP / NB)",
                    Category = "Korelasi & regresi",
                    Description = "Cacahan dengan nol-berlebih (ZIP, EM) atau "
                                  + "sebaran-lebih (Binomial Negatif, MLE).",
                    Fields =
                    {
                        new FieldSpec("cacahan", "Variabel cacahan (bulat ≥ 0)", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("bebas", "Prediktor (angka)", FieldKind.Numeric, true, 1, 4)
                    },
                    Options =
                    {
                        new OptionSpec("keluarga", "Keluarga", OptionType.Choice,
                            new List<string> { "ZIP", "Binomial Negatif", "ZINB" })
                    },
                    Run = c => Cacah.CacahBlocks(c.Data, c.First("cacahan"),
                        c.Get("bebas"), c.Choice("keluarga"))
                },
                new()
                {
                    Id = "robust", Name = "Regresi robust (kebal pencilan)",
                    Category = "Korelasi & regresi",
                    Description = "Regresi yang tak-mudah goyah oleh pencilan: "
                                  + "norma Huber atau dwibobot Tukey via IRLS.",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel terikat", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("bebas", "Prediktor (angka)", FieldKind.Numeric, true, 1, 8)
                    },
                    Options =
                    {
                        new OptionSpec("norma", "Norma", OptionType.Choice,
                            new List<string> { "Huber", "Tukey" })
                    },
                    Run = c => Robust.RobustBlocks(c.Data, c.First("dependen"),
                        c.Get("bebas"), c.Choice("norma"))
                },
                new()
                {
                    Id = "gee", Name = "GEE (longitudinal / klaster)",
                    Category = "Korelasi & regresi",
                    Description = "Regresi untuk data berkelompok: korelasi "
                                  + "kerja Exchangeable/Independence + galat "
                                  + "baku sandwich.",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel terikat", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("bebas", "Prediktor (angka)", FieldKind.Numeric, true, 1, 8),
                        new FieldSpec("grup", "Variabel grup", FieldKind.Categorical, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("keluarga", "Keluarga", OptionType.Choice,
                            new List<string> { "Gauss", "Binomial" }),
                        new OptionSpec("korelasi", "Korelasi kerja", OptionType.Choice,
                            new List<string> { "Exchangeable", "Independence" })
                    },
                    Run = c => Gee.GeeBlocks(c.Data, c.First("dependen"),
                        c.Get("bebas"), c.First("grup"),
                        c.Choice("keluarga"), c.Choice("korelasi"))
                },
                new()
                {
                    Id = "validasi", Name = "Validasi data (aturan nilai)",
                    Category = "Deskriptif",
                    Description = "Periksa data lawan aturan: rentang angka, "
                                  + "daftar aras, sel hilang, format, kunci ganda.",
                    Fields =
                    {
                        new FieldSpec("angka", "Variabel angka", FieldKind.Numeric, true, 1, 8),
                        new FieldSpec("kategori", "Variabel kategori", FieldKind.Categorical, true, 1, 4),
                        new FieldSpec("kunci", "Variabel kunci (kegandaan)", FieldKind.Any, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("rentang", "Aturan rentang ('var min maks; ...')",
                                       OptionType.Text, "usia 0 150"),
                        new OptionSpec("aras", "Aturan aras ('var a,b,c; ...')",
                                       OptionType.Text, "kelompok A,B")
                    },
                    Run = c => Validasi.ValidasiBlocks(c.Data, c.Get("angka"),
                        c.Get("kategori"), c.First("kunci"),
                        c.Options["rentang"].Text ?? "",
                        c.Options["aras"].Text ?? "")
                },
                new()
                {
                    Id = "eksak", Name = "Uji eksak R × C (Fisher–Freeman–Halton)",
                    Category = "Kategori",
                    Description = "Uji kebebasan eksak untuk tabel R × C: enumerasi "
                                  + "penuh bila kecil, Monte Carlo (benih tetap) bila "
                                  + "besar.",
                    Fields =
                    {
                        new FieldSpec("baris", "Variabel baris", FieldKind.Categorical, false, 1, 1),
                        new FieldSpec("kolom", "Variabel kolom", FieldKind.Categorical, false, 1, 1)
                    },
                    Options =
                    {
                        new OptionSpec("metode", "Metode", OptionType.Choice,
                            new List<string> { "Keduanya", "Eksak", "Monte Carlo" }),
                        new OptionSpec("ulangan", "Ulangan Monte Carlo", OptionType.Number, 10000)
                    },
                    Run = c => Eksak.EksakBlocks(c.Data, c.First("baris"),
                        c.First("kolom"), c.Choice("metode"), (int)c.Number("ulangan"))
                },
                new()
                {
                    Id = "sls2", Name = "Regresi dua tahap (2SLS / variabel instrumental)",
                    Category = "Korelasi & regresi",
                    Description = "Menaksir pengaruh variabel endogen memakai instrumen, "
                                  + "lengkap dengan uji Sargan dan Hausman.",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel terikat", FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("endogen", "Variabel endogen (diduga berkorelasi dengan galat)",
                                      FieldKind.Numeric, true, 1, 4),
                        new FieldSpec("eksogen", "Variabel eksogen (ikut di kedua tahap, opsional)",
                                      FieldKind.Numeric, true, 0, 8),
                        new FieldSpec("instrumen", "Instrumen (dipakai hanya di tahap 1)",
                                      FieldKind.Numeric, true, 1, 8)
                    },
                    Run = c => Sls2.Sls2Blocks(c.Data, c.First("dependen"), c.Get("endogen"),
                                               c.Get("eksogen"), c.Get("instrumen"))
                },
                new()
                {
                    Id = "manova", Name = "MANOVA (ragam multivariat satu faktor)",
                    Category = "Perbandingan",
                    Description = "Membandingkan rerata beberapa variabel terikat "
                                  + "sekaligus, lengkap dengan uji Box M.",
                    Fields =
                    {
                        new FieldSpec("terikat", "Variabel terikat (minimal dua)",
                                      FieldKind.Numeric, true, 2, 8),
                        new FieldSpec("faktor", "Variabel kelompok",
                                      FieldKind.Categorical, false, 1, 1)
                    },
                    Run = c => Manova.ManovaBlocks(c.Data, c.Get("terikat"),
                                                   c.First("faktor"), c.Alpha)
                },
                new()
                {
                    Id = "anova_berulang", Name = "ANOVA berulang (repeated measures)",
                    Category = "Perbandingan",
                    Description = "Membandingkan rerata beberapa pengukuran pada "
                                  + "orang yang sama, dengan uji Mauchly dan "
                                  + "koreksi Greenhouse-Geisser.",
                    Fields =
                    {
                        new FieldSpec("dependen", "Variabel terikat",
                                      FieldKind.Numeric, false, 1, 1),
                        new FieldSpec("subjek", "Variabel subjek (penanda orang)",
                                      FieldKind.Categorical, false, 1, 1),
                        new FieldSpec("faktor", "Variabel waktu (faktor dalam subjek)",
                                      FieldKind.Categorical, false, 1, 1)
                    },
                    Run = c => AnovaBerulang.AnovaBerulangBlocks(c.Data, c.First("dependen"),
                                                                 c.First("subjek"),
                                                                 c.First("faktor"), c.Alpha)
                },
                new()
                {
                    Id = "korelasi_kanonik", Name = "Korelasi kanonik (dua blok variabel)",
                    Category = "Korelasi & regresi",
                    Description = "Mengukur kekuatan hubungan antara DUA blok "
                                  + "variabel sekaligus, dan berapa dimensi "
                                  + "hubungannya.",
                    Fields =
                    {
                        new FieldSpec("x", "Blok X (minimal satu variabel)",
                                      FieldKind.Numeric, true, 1, 8),
                        new FieldSpec("y", "Blok Y (minimal satu variabel)",
                                      FieldKind.Numeric, true, 1, 8)
                    },
                    Run = c => KorelasiKanonik.KorelasiKanonikBlocks(c.Data, c.Get("x"),
                                                                     c.Get("y"), c.Alpha)
                },
                new()
                {
                    Id = "custom_tables", Name = "Custom Tables", Category = "Deskriptif",
                    Description = "Tabel berlapis dengan statistik beragam, penomoran otomatis, dan persentase.",
                    Options =
                    {
                        new OptionSpec("konfigurasi", "Konfigurasi tabel (JSON)", OptionType.Text, "")
                    },
                    Run = c =>
                    {
                        // Untuk saat ini, gunakan konfigurasi default
                        // Nanti bisa dihubungkan dengan JendelaCustomTables
                        var spec = new AlatStatistik.Statistics.CustomTableSpec
                        {
                            TableNumber = "1",
                            TableTitle = "Tabel Kustom",
                            Statistics = new List<AlatStatistik.Statistics.StatistikType>
                            {
                                AlatStatistik.Statistics.StatistikType.Count,
                                AlatStatistik.Statistics.StatistikType.Percent
                            }
                        };
                        
                        // Tambahkan dimensi baris jika ada variabel kategori
                        var kategoriVars = c.Data.Variables.Where(v => !v.IsNumeric)
                                                          .Select(v => v.Name).Take(2).ToList();
                        if (kategoriVars.Any())
                        {
                            spec.RowDimensions.Add(new AlatStatistik.Statistics.DimensionSpec
                            {
                                Variable = kategoriVars[0],
                                Label = kategoriVars[0],
                                ShowTotal = true
                            });
                        }
                        
                        // Tambahkan dimensi kolom jika ada variabel kategori kedua
                        if (kategoriVars.Count > 1)
                        {
                            spec.ColumnDimensions.Add(new AlatStatistik.Statistics.DimensionSpec
                            {
                                Variable = kategoriVars[1],
                                Label = kategoriVars[1],
                                ShowTotal = true
                            });
                        }
                        
                        return AlatStatistik.Statistics.CustomTables.CustomTableBlocks(c.Data, spec);
                    }
                }
            };
        }

        public static List<string> Kategori(List<AnalysisSpec> specs)
            => specs.Select(s => s.Category).Distinct().ToList();
    }
}
