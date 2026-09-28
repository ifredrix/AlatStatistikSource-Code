using System.Collections.Generic;
using System.Linq;

namespace AlatStatistik.Models
{
    public enum BlockKind { Heading, Table, Note, Chart, Rumus, Substitusi }

    public enum NoteKind { Info, Ok, Warning, Error }

    public class ResultBlock
    {
        public BlockKind Kind { get; set; }
        public string Text { get; set; } = "";
        public string? Title { get; set; }
        public List<string> Columns { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();
        public string? Footnote { get; set; }
        public NoteKind Note { get; set; } = NoteKind.Info;
        public int Level { get; set; } = 1;
        public ChartSpec? Chart { get; set; }
        public List<RumusInfo> RumusList { get; set; } = new();
        public List<LangkahHitung> Langkah { get; set; } = new();
    }

    public class LangkahHitung
    {
        public string Uraian { get; set; } = "";
        public string Hitungan { get; set; } = "";
    }

    public class RumusInfo
    {
        public string Nama { get; set; } = "";
        public string Bentuk { get; set; } = "";
        public string Jenis { get; set; } = "";
        public string Penemu { get; set; } = "";
        public int Tahun { get; set; }
        public string Catatan { get; set; } = "";

        public string LabelTahun => Tahun > 0 ? Penemu + ", " + Tahun : Penemu;
    }

    public enum ChartKind { Histogram, BoxPlot, Scatter, Bar, Line, Pie, Heatmap, ErrorBar, Dendrogram, Roc }

    public class ChartSpec
    {
        public ChartKind Kind { get; set; }

        public Dictionary<string, List<double>> Series { get; } = new();

        public List<(double X, double Y)> Points { get; } = new();

        public List<(string Label, double Value)> Bars { get; } = new();

        public int TotalN { get; set; }

        public List<(string Label, double Mean, double Bawah, double Atas)> ErrorBars { get; } = new();

        public List<List<double>> Heatmap { get; } = new();
        public List<string> HeatmapRowLabels { get; } = new();
        public List<string> HeatmapColLabels { get; } = new();

        public List<(int Kiri, int Kanan, double Tinggi)> Penggabungan { get; } = new();

        public List<string> LabelDaun { get; } = new();

        public string XTitle { get; set; } = "";
        public string YTitle { get; set; } = "";
        public string Title { get; set; } = "";

        public (double Slope, double Intercept)? RegressionLine { get; set; }
    }

    public static class Blocks
    {
        public static ResultBlock Heading(string text, int level = 1)
            => new() { Kind = BlockKind.Heading, Text = text, Level = level };

        public static ResultBlock Table(string title, IEnumerable<string> columns,
                                        IEnumerable<IEnumerable<string>> rows, string? footnote = null)
        {
            var block = new ResultBlock
            {
                Kind = BlockKind.Table, Title = title, Footnote = footnote,
                Columns = new List<string>(columns)
            };
            foreach (var row in rows) block.Rows.Add(new List<string>(row));
            return block;
        }

        public static ResultBlock Note(string text, NoteKind kind = NoteKind.Info)
            => new() { Kind = BlockKind.Note, Text = text, Note = kind };

        public static ResultBlock Chart(string title, ChartSpec spec)
            => new() { Kind = BlockKind.Chart, Title = title, Chart = spec };

        public static ResultBlock Rumus(string judul, IEnumerable<Statistics.Rumus> daftar)
        {
            var list = new List<RumusInfo>();
            foreach (var r in daftar)
            {
                list.Add(new RumusInfo
                {
                    Nama = r.Nama,
                    Bentuk = r.Bentuk,
                    Jenis = r.Jenis,
                    Penemu = r.Penemu,
                    Tahun = r.Tahun,
                    Catatan = r.Catatan
                });
            }
            return new ResultBlock
            {
                Kind = BlockKind.Rumus,
                Title = judul,
                RumusList = list
            };
        }

        public static ResultBlock Rumus(string judul, params Statistics.Rumus[] daftar)
            => Rumus(judul, (IEnumerable<Statistics.Rumus>)daftar);

        public static ResultBlock Substitusi(string judul, IEnumerable<LangkahHitung> langkah)
            => new()
            {
                Kind = BlockKind.Substitusi,
                Title = judul,
                Langkah = langkah.Where(l => !string.IsNullOrWhiteSpace(l.Hitungan)).ToList()
            };

        public static ResultBlock Substitusi(string judul, params (string Uraian, string Hitungan)[] langkah)
            => Substitusi(judul, langkah.Select(p => new LangkahHitung
            {
                Uraian = p.Uraian,
                Hitungan = p.Hitungan
            }));
    }
}
