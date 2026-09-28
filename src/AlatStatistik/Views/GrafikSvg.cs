using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using AlatStatistik.Models;
using AlatStatistik.Statistics;

namespace AlatStatistik.Views
{

    public static class GrafikSvg
    {
        private const string WarnaGaris = "#4F5A6E";
        private const string WarnaTeks = "#16213A";
        private const string WarnaAksen = "#0EA5E9";
        private const string WarnaBiruMuda = "#38BDF8";
        private const string WarnaGrid = "#D7DDE8";

        private const string WarnaBantu = "#DDE3EC";

        private const double AtasJudul = 26;

        private static readonly string[] Palet =
        {
            "#0EA5E9", "#7C3AED", "#10B981", "#D97706", "#DC2626", "#DB2777"
        };

        public const int LebarBaku = 720;
        public const int TinggiBaku = 420;

        public static string Buat(ChartSpec spec, int lebar = LebarBaku, int tinggi = TinggiBaku,
                                  bool denganJudul = false)
        {
            bool judul = denganJudul && !string.IsNullOrEmpty(spec.Title);
            double atas = judul ? AtasJudul : 0;

            var isi = new StringBuilder();
            switch (spec.Kind)
            {
                case ChartKind.Histogram: Histogram(isi, spec, lebar, (int)Math.Round(tinggi - atas)); break;
                case ChartKind.BoxPlot: BoxPlot(isi, spec, lebar, (int)Math.Round(tinggi - atas)); break;
                case ChartKind.Scatter: Pencar(isi, spec, lebar, (int)Math.Round(tinggi - atas)); break;
                case ChartKind.Bar: Batang(isi, spec, lebar, (int)Math.Round(tinggi - atas)); break;
                case ChartKind.Line: Garis(isi, spec, lebar, (int)Math.Round(tinggi - atas)); break;
                case ChartKind.Pie: Lingkaran(isi, spec, lebar, (int)Math.Round(tinggi - atas)); break;
                case ChartKind.Heatmap: PetaPanas(isi, spec, lebar, (int)Math.Round(tinggi - atas)); break;
                case ChartKind.ErrorBar: BatangGalat(isi, spec, lebar, (int)Math.Round(tinggi - atas)); break;
                case ChartKind.Dendrogram: Dendrogram(isi, spec, lebar, (int)Math.Round(tinggi - atas)); break;
                case ChartKind.Roc: Roc(isi, spec, lebar, (int)Math.Round(tinggi - atas)); break;
            }

            if (isi.Length == 0) return "";

            var sb = new StringBuilder();
            sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(lebar)
              .Append("\" height=\"").Append(tinggi)
              .Append("\" viewBox=\"0 0 ").Append(lebar).Append(' ').Append(tinggi)
              .Append("\" font-family=\"Segoe UI, Calibri, sans-serif\" font-size=\"11\">");
            sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\"/>");

            if (judul)
            {
                
                
                sb.Append(Teks(lebar / 2.0, 6, spec.Title, Rata.Tengah, false, WarnaTeks, 13, true));
                sb.Append($"<g transform=\"translate(0,{N(atas)})\">");
            }

            sb.Append(isi);
            if (judul) sb.Append("</g>");
            sb.Append("</svg>");
            return sb.ToString();
        }

        public static string IsiSaja(ChartSpec spec, int lebar = LebarBaku, int tinggi = TinggiBaku)
        {
            string svg = Buat(spec, lebar, tinggi);
            if (svg.Length == 0) return "";
            int buka = svg.IndexOf('>') + 1;               
            int tutup = svg.LastIndexOf("</svg>", StringComparison.Ordinal);
            return svg.Substring(buka, tutup - buka);
        }

        

        private static string N(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        private static string Ringkas(double v)
            => Math.Abs(v) >= 1000 || (Math.Abs(v) < 0.01 && v != 0)
                ? v.ToString("0.0E+0", CultureInfo.InvariantCulture).Replace(".", ",")
                : v.ToString("0.##", CultureInfo.InvariantCulture).Replace(".", ",");

        private static string RingkasLabel(string label)
            => double.TryParse(label, NumberStyles.Any, CultureInfo.InvariantCulture, out double v)
                ? Ringkas(v)
                : label;

        private static string Esc(string s) => s
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;");

        private enum Rata { Kiri, Tengah, Kanan }

        private static string Teks(double x, double y, string isi, Rata rata = Rata.Kiri,
                                   bool tengahV = false, string warna = WarnaTeks,
                                   double ukuran = 11, bool tebal = false)
        {
            string anchor = rata switch
            {
                Rata.Tengah => "middle",
                Rata.Kanan => "end",
                _ => "start"
            };
            string baseline = tengahV ? "middle" : "hanging";
            string bobot = tebal ? " font-weight=\"600\"" : "";
            return $"<text x=\"{N(x)}\" y=\"{N(y)}\" text-anchor=\"{anchor}\" "
                 + $"dominant-baseline=\"{baseline}\" fill=\"{warna}\" "
                 + $"font-size=\"{N(ukuran)}\"{bobot}>{Esc(isi)}</text>";
        }

        private static string Garis(double x1, double y1, double x2, double y2,
                                    string warna, double tebal, bool putus = false)
        {
            string gaya = putus ? " stroke-dasharray=\"4,3\"" : "";
            return $"<line x1=\"{N(x1)}\" y1=\"{N(y1)}\" x2=\"{N(x2)}\" y2=\"{N(y2)}\" "
                 + $"stroke=\"{warna}\" stroke-width=\"{N(tebal)}\"{gaya}/>";
        }

        private static string TeksTerjepit(double x, double y, string isi, double lebarKanvas,
                                           double tepi = 2, bool tengahV = false)
        {
            double w = Lebar(isi);
            double geser = 0;
            if (x - w / 2 < tepi) geser = tepi - (x - w / 2);
            else if (x + w / 2 > lebarKanvas - tepi) geser = lebarKanvas - tepi - (x + w / 2);

            return Teks(x + geser, y, isi, Rata.Tengah, tengahV);
        }

        private static string IdKlip(Area a)
        {
            string Bagian(double v) => N(v).Replace("-", "n").Replace('.', '_');
            return $"klip-{Bagian(a.Kiri)}-{Bagian(a.Atas)}-{Bagian(a.Lebar)}-{Bagian(a.Tinggi)}";
        }

        private static double Lebar(string isi, double ukuran = 11)
        {
            if (string.IsNullOrEmpty(isi)) return 0;
            return new FormattedText(isi, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                     new Typeface("Segoe UI"), ukuran, Brushes.Black, 1.0).Width;
        }

        private static string Pendek(string isi, double lebarMaks)
        {
            if (lebarMaks <= 0) return "";
            if (Lebar(isi) <= lebarMaks) return isi;
            for (int n = isi.Length - 1; n > 0; n--)
            {
                string coba = isi.Substring(0, n) + "…";
                if (Lebar(coba) <= lebarMaks) return coba;
            }
            return "…";
        }

        private static double KiriUntuk(params string[] label)
        {
            double lebar = label.Where(s => !string.IsNullOrEmpty(s))
                                .Select(s => Lebar(s))
                                .DefaultIfEmpty(0).Max();
            return Math.Max(66, 36 + lebar);
        }

        private static List<double> TingkatBulat(double bawah, double atas, int maksTingkat = 6)
        {
            var hasil = new List<double>();
            if (!(atas > bawah) || double.IsNaN(atas) || double.IsNaN(bawah)) return hasil;

            double kasar = (atas - bawah) / Math.Max(1, maksTingkat);
            double pangkat = Math.Pow(10, Math.Floor(Math.Log10(kasar)));
            double sisa = kasar / pangkat;
            double langkah = pangkat * (sisa <= 1 ? 1 : sisa <= 2 ? 2 : sisa <= 5 ? 5 : 10);

            double mulai = Math.Ceiling(bawah / langkah - 1e-9) * langkah;
            for (double v = mulai; v <= atas + 1e-9 && hasil.Count < 12; v += langkah)
                hasil.Add(Math.Abs(v) < langkah * 1e-9 ? 0 : v);
            return hasil;
        }

        private static double KiriUntukNilai(double bawah, double atas, int maksTingkat = 6)
            => KiriUntuk(TingkatBulat(bawah, atas, maksTingkat).Select(Ringkas).ToArray());

        private static string SumbuNilai(Area a, double bawah, double atas,
                                         int maksTingkat = 6, params double[] yTerpakai)
        {
            if (!(atas > bawah)) return "";
            var sb = new StringBuilder();

            foreach (double v in TingkatBulat(bawah, atas, maksTingkat))
            {
                double y = a.Bawah - (v - bawah) / (atas - bawah) * a.Tinggi;
                if (y < a.Atas - 0.5 || y > a.Bawah + 0.5) continue;
                sb.Append(Garis(a.Kiri, y, a.Kanan, y, WarnaBantu, 1));

                bool dekat = yTerpakai.Any(t => Math.Abs(t - y) < 14);
                if (!dekat) sb.Append(Teks(a.Kiri - 8, y, Ringkas(v), Rata.Kanan, true));
            }

            
            sb.Append($"<rect x=\"{N(a.Kiri)}\" y=\"{N(a.Atas)}\" width=\"{N(a.Lebar)}\" "
                    + $"height=\"{N(a.Tinggi)}\" fill=\"none\" stroke=\"{WarnaBantu}\" "
                    + $"stroke-width=\"1\"/>");
            return sb.ToString();
        }

        private static string Sumbu(Area a, string judulX, string judulY, double judulXBawah = 32)
        {
            var sb = new StringBuilder();
            sb.Append(Garis(a.Kiri, a.Atas, a.Kiri, a.Bawah, WarnaGaris, 1));
            sb.Append(Garis(a.Kiri, a.Bawah, a.Kanan, a.Bawah, WarnaGaris, 1));
            if (!string.IsNullOrEmpty(judulX))
                sb.Append(Teks(a.Kiri + a.Lebar / 2, a.Bawah + judulXBawah, judulX, Rata.Tengah));
            if (!string.IsNullOrEmpty(judulY))
            {
                
                const double x = 15;
                double tengahY = a.Atas + a.Tinggi / 2;
                sb.Append($"<g transform=\"rotate(-90 {N(x)} {N(tengahY)})\">")
                  .Append(Teks(x, tengahY, judulY, Rata.Tengah, true))
                  .Append("</g>");
            }
            return sb.ToString();
        }

        private readonly struct Area
        {
            public Area(double kiri, double bawah, int lebar, int tinggi,
                        double atas = 16, double kanan = 18)
            {
                Kiri = kiri;
                Atas = atas;
                Lebar = Math.Max(10, lebar - kiri - kanan);
                Tinggi = Math.Max(10, tinggi - atas - bawah);
            }

            public double Kiri { get; }
            public double Atas { get; }
            public double Lebar { get; }
            public double Tinggi { get; }
            public double Kanan => Kiri + Lebar;
            public double Bawah => Atas + Tinggi;
        }

        

        private static void Histogram(StringBuilder sb, ChartSpec spec, int lebar, int tinggi)
        {
            var nilai = spec.Series.Values.FirstOrDefault() ?? new List<double>();
            if (nilai.Count == 0) return;

            double min = nilai.Min(), maks = nilai.Max();
            if (Math.Abs(maks - min) < 1e-12) { maks += 1; min -= 1; }

            int bin = Math.Max(5, Math.Min(30, (int)Math.Ceiling(Math.Sqrt(nilai.Count))));
            double langkah = (maks - min) / bin;
            var hitung = new int[bin];
            foreach (double v in nilai)
            {
                int i = (int)Math.Floor((v - min) / langkah);
                if (i >= bin) i = bin - 1;
                if (i >= 0) hitung[i]++;
            }

            int tertinggi = hitung.Max();
            if (tertinggi == 0) return;

            var a = new Area(KiriUntukNilai(0, tertinggi), 54, lebar, tinggi);

            
            sb.Append(SumbuNilai(a, 0, tertinggi));

            for (int i = 0; i < bin; i++)
            {
                double x = a.Kiri + i * a.Lebar / bin;
                double h = a.Tinggi * hitung[i] / (double)tertinggi;
                sb.Append($"<rect x=\"{N(x + 1)}\" y=\"{N(a.Bawah - h)}\" "
                        + $"width=\"{N(a.Lebar / bin - 2)}\" height=\"{N(h)}\" "
                        + $"fill=\"{WarnaBiruMuda}\" fill-opacity=\"0.85\" stroke=\"{WarnaGaris}\"/>");
            }

            sb.Append(Sumbu(a, spec.XTitle, "Frekuensi"));
            sb.Append(TeksTerjepit(a.Kiri, a.Bawah + 8, Ringkas(min), lebar));
            sb.Append(TeksTerjepit(a.Kanan, a.Bawah + 8, Ringkas(maks), lebar));
        }

        

        private static void BoxPlot(StringBuilder sb, ChartSpec spec, int lebar, int tinggi)
        {
            var seri = spec.Series;
            if (seri.Count == 0) return;

            double semuaMin = seri.Values.SelectMany(v => v).Min();
            double semuaMaks = seri.Values.SelectMany(v => v).Max();
            if (Math.Abs(semuaMaks - semuaMin) < 1e-12) { semuaMaks += 1; semuaMin -= 1; }
            double ruang = (semuaMaks - semuaMin) * 0.08;
            double rendah = semuaMin - ruang, tinggiSkala = semuaMaks + ruang;

            var a = new Area(KiriUntuk(TingkatBulat(rendah, tinggiSkala).Select(Ringkas)
                                       .Append(Ringkas(semuaMin))
                                       .Append(Ringkas(semuaMaks)).ToArray()), 54, lebar, tinggi);

            double Y(double nilai)
                => a.Bawah - a.Tinggi * (nilai - rendah) / (tinggiSkala - rendah);

            
            
            
            double yMin = Y(semuaMin), yMaks = Y(semuaMaks);
            sb.Append(SumbuNilai(a, rendah, tinggiSkala, 6, yMin, yMaks));
            sb.Append(Teks(a.Kiri - 8, yMin, Ringkas(semuaMin), Rata.Kanan, true));
            sb.Append(Teks(a.Kiri - 8, yMaks, Ringkas(semuaMaks), Rata.Kanan, true));

            double lebarSlot = a.Lebar / seri.Count;
            int indeks = 0;

            foreach (var kv in seri)
            {
                var s = Descriptives.Summarize(kv.Value, Descriptives.DefinisiPagarPencilan);
                double cx = a.Kiri + lebarSlot * (indeks + 0.5);
                double w = Math.Min(70, lebarSlot * 0.55);
                string warna = Palet[indeks % Palet.Length];

                double batasBawah = s.Q1 - 1.5 * s.Iqr;
                double batasAtas = s.Q3 + 1.5 * s.Iqr;
                var dalam = kv.Value.Where(v => v >= batasBawah && v <= batasAtas).ToList();
                double ujungBawah = dalam.Count > 0 ? dalam.Min() : s.Min;
                double ujungAtas = dalam.Count > 0 ? dalam.Max() : s.Max;

                
                sb.Append(Garis(cx, Y(ujungAtas), cx, Y(s.Q3), warna, 1.6));
                sb.Append(Garis(cx, Y(ujungBawah), cx, Y(s.Q1), warna, 1.6));
                sb.Append(Garis(cx - w / 4, Y(ujungAtas), cx + w / 4, Y(ujungAtas), warna, 1.6));
                sb.Append(Garis(cx - w / 4, Y(ujungBawah), cx + w / 4, Y(ujungBawah), warna, 1.6));

                
                double yAtas = Y(s.Q3), yBawah = Y(s.Q1);
                sb.Append($"<rect x=\"{N(cx - w / 2)}\" y=\"{N(yAtas)}\" width=\"{N(w)}\" "
                        + $"height=\"{N(Math.Max(1, yBawah - yAtas))}\" fill=\"{warna}\" "
                        + $"fill-opacity=\"0.28\" stroke=\"{warna}\" stroke-width=\"1.6\"/>");

                
                sb.Append(Garis(cx - w / 2, Y(s.Median), cx + w / 2, Y(s.Median), warna, 2.6));

                
                foreach (double v in kv.Value.Where(v => v < batasBawah || v > batasAtas))
                    sb.Append($"<circle cx=\"{N(cx)}\" cy=\"{N(Y(v))}\" r=\"2.5\" fill=\"none\" "
                            + $"stroke=\"{WarnaTeks}\"/>");

                sb.Append(Teks(cx, a.Bawah + 8,
                               Pendek(kv.Key, Math.Max(20, Math.Min(110, lebarSlot - 6))),
                               Rata.Tengah));
                indeks++;
            }

            sb.Append(Sumbu(a, spec.XTitle, spec.YTitle));
        }

        

        private static void Pencar(StringBuilder sb, ChartSpec spec, int lebar, int tinggi)
        {
            var titik = spec.Points;
            if (titik.Count == 0) return;

            double xMin = titik.Min(p => p.X), xMaks = titik.Max(p => p.X);
            double yMin = titik.Min(p => p.Y), yMaks = titik.Max(p => p.Y);
            if (Math.Abs(xMaks - xMin) < 1e-12) { xMaks += 1; xMin -= 1; }
            if (Math.Abs(yMaks - yMin) < 1e-12) { yMaks += 1; yMin -= 1; }

            var a = new Area(KiriUntukNilai(yMin, yMaks), 54, lebar, tinggi);

            double px(double v) => a.Kiri + a.Lebar * (v - xMin) / (xMaks - xMin);
            double py(double v) => a.Bawah - a.Tinggi * (v - yMin) / (yMaks - yMin);

            sb.Append(SumbuNilai(a, yMin, yMaks));
            sb.Append(Sumbu(a, spec.XTitle, spec.YTitle));

            
            
            
            
            
            string idKlip = IdKlip(a);
            sb.Append($"<defs><clipPath id=\"{idKlip}\">"
                    + $"<rect x=\"{N(a.Kiri)}\" y=\"{N(a.Atas)}\" "
                    + $"width=\"{N(a.Lebar)}\" height=\"{N(a.Tinggi)}\"/>"
                    + "</clipPath></defs>");
            sb.Append($"<g clip-path=\"url(#{idKlip})\">");

            if (spec.RegressionLine.HasValue)
            {
                var (slope, intercept) = spec.RegressionLine.Value;
                sb.Append(Garis(px(xMin), py(slope * xMin + intercept),
                                px(xMaks), py(slope * xMaks + intercept), WarnaAksen, 2));
            }

            foreach (var p in titik)
                sb.Append($"<circle cx=\"{N(px(p.X))}\" cy=\"{N(py(p.Y))}\" r=\"3\" "
                        + $"fill=\"{WarnaBiruMuda}\" fill-opacity=\"0.75\"/>");

            sb.Append("</g>");

            sb.Append(TeksTerjepit(a.Kiri, a.Bawah + 8, Ringkas(xMin), lebar));
            sb.Append(TeksTerjepit(a.Kanan, a.Bawah + 8, Ringkas(xMaks), lebar));
        }

        

        private static void Batang(StringBuilder sb, ChartSpec spec, int lebar, int tinggi)
        {
            var batang = spec.Bars;
            if (batang.Count == 0) return;

            double tertinggi = batang.Max(b => b.Value);
            if (tertinggi <= 0) tertinggi = 1;

            var a = new Area(KiriUntukNilai(0, tertinggi), 54, lebar, tinggi);
            sb.Append(SumbuNilai(a, 0, tertinggi));

            double lebarSlot = a.Lebar / batang.Count;
            for (int i = 0; i < batang.Count; i++)
            {
                double x = a.Kiri + i * lebarSlot;
                double h = a.Tinggi * batang[i].Value / tertinggi;
                sb.Append($"<rect x=\"{N(x + lebarSlot * 0.15)}\" y=\"{N(a.Bawah - h)}\" "
                        + $"width=\"{N(lebarSlot * 0.7)}\" height=\"{N(h)}\" "
                        + $"fill=\"{Palet[i % Palet.Length]}\"/>");
                sb.Append(Teks(x + lebarSlot / 2, a.Bawah - h - 10,
                               Ringkas(batang[i].Value), Rata.Tengah));
                sb.Append(Teks(x + lebarSlot / 2, a.Bawah + 8,
                               Pendek(RingkasLabel(batang[i].Label),
                                      Math.Max(20, Math.Min(110, lebarSlot - 4))),
                               Rata.Tengah));
            }

            sb.Append(Sumbu(a, spec.XTitle, spec.YTitle));
        }

        

        private static void Garis(StringBuilder sb, ChartSpec spec, int lebar, int tinggi)
        {
            var seri = spec.Series;
            if (seri.Count == 0) return;

            int panjang = seri.Values.Max(v => v.Count);
            if (panjang < 2) return;

            
            
            double dataMin = seri.Values.SelectMany(v => v).DefaultIfEmpty(0).Min();
            double dataMaks = seri.Values.SelectMany(v => v).DefaultIfEmpty(0).Max();
            if (Math.Abs(dataMaks - dataMin) < 1e-12) { dataMaks += 1; dataMin -= 1; }
            double ruang = (dataMaks - dataMin) * 0.08;
            double yMin = dataMin - ruang, yMaks = dataMaks + ruang;

            var a = new Area(KiriUntukNilai(yMin, yMaks), 54, lebar, tinggi);

            double py(double v) => a.Bawah - a.Tinggi * (v - yMin) / (yMaks - yMin);

            sb.Append(SumbuNilai(a, yMin, yMaks));
            sb.Append(Sumbu(a, spec.XTitle, spec.YTitle));

            
            int idx = 0;
            foreach (var kv in seri)
            {
                string warna = Palet[idx % Palet.Length];
                var sbGaris = new StringBuilder();
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    double x = a.Kiri + a.Lebar * i / Math.Max(1, panjang - 1);
                    sbGaris.Append(i == 0 ? "M" : " L").Append(N(x)).Append(' ').Append(N(py(kv.Value[i])));
                }
                if (kv.Value.Count >= 2)
                    sb.Append($"<path d=\"{sbGaris}\" fill=\"none\" stroke=\"{warna}\" stroke-width=\"1.8\"/>");

                for (int i = 0; i < kv.Value.Count; i++)
                {
                    double x = a.Kiri + a.Lebar * i / Math.Max(1, panjang - 1);
                    sb.Append($"<circle cx=\"{N(x)}\" cy=\"{N(py(kv.Value[i]))}\" r=\"2.4\" "
                            + $"fill=\"{warna}\" fill-opacity=\"0.9\"/>");
                }
                idx++;
            }

            
            
            
            
            var namaSeri = seri.Keys.ToList();
            const double barisLegenda = 15;
            double lebarTeksLegenda = Math.Max(40, Math.Min(a.Lebar * 0.45,
                namaSeri.Select(n => Lebar(n)).DefaultIfEmpty(0).Max()));
            double lebarLegenda = 34 + lebarTeksLegenda;
            double tinggiLegenda = namaSeri.Count * barisLegenda + 10;
            double legendaKanan = a.Kanan - 10;
            double legendaKiri = legendaKanan - lebarLegenda;
            double legendaAtas = a.Atas + 8;

            sb.Append($"<rect x=\"{N(legendaKiri)}\" y=\"{N(legendaAtas)}\" "
                    + $"width=\"{N(lebarLegenda)}\" height=\"{N(tinggiLegenda)}\" "
                    + $"fill=\"#FFFFFF\" fill-opacity=\"0.93\" stroke=\"{WarnaGaris}\" "
                    + $"stroke-width=\"0.8\"/>");

            idx = 0;
            foreach (var kv in seri)
            {
                string warna = Palet[idx % Palet.Length];
                double yLeg = legendaAtas + 5 + idx * barisLegenda + barisLegenda / 2;
                sb.Append(Garis(legendaKiri + 8, yLeg, legendaKiri + 26, yLeg, warna, 1.8));
                sb.Append(Teks(legendaKanan - 8, yLeg, Pendek(kv.Key, lebarTeksLegenda),
                               Rata.Kanan, true));
                idx++;
            }

            
            sb.Append(TeksTerjepit(a.Kiri, a.Bawah + 8, "1", lebar));
            if (panjang >= 3)
                sb.Append(TeksTerjepit(a.Kiri + a.Lebar / 2, a.Bawah + 8,
                                       ((panjang + 1) / 2).ToString(CultureInfo.InvariantCulture), lebar));
            if (panjang >= 2)
                sb.Append(TeksTerjepit(a.Kanan, a.Bawah + 8,
                                       panjang.ToString(CultureInfo.InvariantCulture), lebar));
        }

        

        private static void Lingkaran(StringBuilder sb, ChartSpec spec, int lebar, int tinggi)
        {
            var iris = spec.Bars;
            if (iris.Count == 0) return;

            double total = iris.Sum(b => Math.Max(0, b.Value));
            if (total <= 0) return;

            double lebarKet = Math.Min(300, Math.Max(120, lebar * 0.42));
            double ketKiri = lebar - lebarKet - 12;
            double cx = ketKiri / 2;
            double cy = tinggi / 2.0 - 4;
            double r = Math.Min(cx - 16, cy - 12) - 6;
            if (r < 26) return;

            double sudutMulai = -Math.PI / 2;
            int idx = 0;

            foreach (var (label, value) in iris)
            {
                double porsi = Math.Max(0, value) / total;
                double sudutAkhir = sudutMulai + porsi * 2 * Math.PI;

                double x1 = cx + r * Math.Cos(sudutMulai);
                double y1 = cy + r * Math.Sin(sudutMulai);
                double x2 = cx + r * Math.Cos(sudutAkhir);
                double y2 = cy + r * Math.Sin(sudutAkhir);
                int busurBesar = porsi > 0.5 ? 1 : 0;

                sb.Append($"<path d=\"M {N(cx)} {N(cy)} L {N(x1)} {N(y1)} "
                        + $"A {N(r)} {N(r)} 0 {busurBesar} 1 {N(x2)} {N(y2)} Z\" "
                        + $"fill=\"{Palet[idx % Palet.Length]}\" stroke=\"{WarnaGaris}\"/>");

                
                
                if (porsi >= 0.10)
                {
                    string persen = (porsi * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
                    if (Lebar(persen) <= r * 0.9)
                    {
                        double tengahSudut = (sudutMulai + sudutAkhir) / 2;
                        sb.Append(Teks(cx + r * 0.58 * Math.Cos(tengahSudut),
                                       cy + r * 0.58 * Math.Sin(tengahSudut),
                                       persen, Rata.Tengah, true, "#FFFFFF"));
                    }
                }

                sudutMulai = sudutAkhir;
                idx++;
            }

            sb.Append($"<circle cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"{N(r)}\" fill=\"none\" "
                    + $"stroke=\"{WarnaGaris}\"/>");

            
            const double baris = 15;
            
            int maksBaris = (int)Math.Floor((tinggi - 46) / baris);
            if (maksBaris < 1 || lebarKet < 60) return;

            double lebarTeks = lebarKet - 22;
            bool dipotong = iris.Count > maksBaris;
            int jumlahTampil = dipotong ? maksBaris - 1 : iris.Count;

            double y = 6;
            for (int i = 0; i < jumlahTampil; i++)
            {
                double nilai = Math.Max(0, iris[i].Value);
                string persen = (total > 0 ? nilai / total * 100 : 0)
                                .ToString("0.#", CultureInfo.InvariantCulture) + "%";
                double lebarPersen = Lebar(persen);
                string nama = Pendek(RingkasLabel(iris[i].Label),
                                     Math.Max(24, lebarTeks - lebarPersen - 8));

                sb.Append($"<rect x=\"{N(ketKiri)}\" y=\"{N(y + 3)}\" width=\"11\" height=\"9\" "
                        + $"fill=\"{Palet[i % Palet.Length]}\" stroke=\"{WarnaGaris}\" "
                        + $"stroke-width=\"0.8\"/>");
                sb.Append(Teks(ketKiri + 17, y, nama + "  " + persen, Rata.Kiri, false));
                y += baris;
            }

            if (dipotong)
            {
                
                
                
                
                double porsiTampil = iris.Take(jumlahTampil).Sum(b => Math.Max(0, b.Value));
                double sisaPersen = total > 0 ? (1 - porsiTampil / total) * 100 : 0;
                string sisa = $"… {iris.Count - jumlahTampil} irisan lain ("
                            + sisaPersen.ToString("0.#", CultureInfo.InvariantCulture)
                            + "% dari yang digambar)";
                sb.Append(Teks(ketKiri + 17, y, Pendek(sisa, lebarTeks), Rata.Kiri, false));
            }

            
            double n = spec.TotalN > 0 ? spec.TotalN : total;
            string catatan = "N = " + Fmt.Int((int)n);
            if (Math.Abs(n - total) > 0.5)
                catatan += $" · {iris.Count} kategori terbesar digambar";
            sb.Append(Teks(ketKiri, tinggi - 24, Pendek(catatan, lebarKet), Rata.Kiri, false));
        }

        

        private static void PetaPanas(StringBuilder sb, ChartSpec spec, int lebar, int tinggi)
        {
            var matriks = spec.Heatmap;
            if (matriks.Count == 0 || matriks[0].Count == 0) return;

            int baris = matriks.Count;
            int kolom = matriks[0].Count;
            double maksAbs = matriks.SelectMany(r => r).Select(Math.Abs).DefaultIfEmpty(0).Max();
            if (maksAbs <= 0) return;

            const double tinggiTeks = 15;

            double lebarBaris = spec.HeatmapRowLabels.Count > 0
                ? Math.Min(140, spec.HeatmapRowLabels.Select(s => Lebar(s)).DefaultIfEmpty(0).Max()) : 0;
            double lebarKolom = spec.HeatmapColLabels.Count > 0
                ? Math.Min(140, spec.HeatmapColLabels.Select(s => Lebar(s)).DefaultIfEmpty(0).Max()) : 0;

            double kiri = Math.Max(20, lebarBaris + 12);
            double bawah = Math.Max(34, lebarKolom * 0.36 + tinggiTeks + 10);
            const double atas = 18;
            const double kanan = 16;

            double lebarArea = Math.Max(20, lebar - kiri - kanan);
            double tinggiArea = Math.Max(20, tinggi - atas - bawah);

            double ukuran = Math.Min(70, Math.Min(lebarArea / kolom, tinggiArea / baris));
            if (ukuran < 8) return;

            double lebarPeta = ukuran * kolom;
            double tinggiPeta = ukuran * baris;

            
            
            const double jarakBar = 26, barW = 12, ruangAngka = 24;
            double lebarBlok = lebarPeta + jarakBar + barW + ruangAngka;
            double petaKiri = kiri + Math.Max(0, (lebarArea - lebarBlok) / 2);
            double petaAtas = atas + Math.Max(0, (tinggiArea - tinggiPeta) / 2);

            for (int r = 0; r < baris; r++)
            {
                for (int k = 0; k < kolom; k++)
                {
                    double v = r < matriks.Count && k < matriks[r].Count ? matriks[r][k] : 0;
                    double intens = Math.Min(1.0, Math.Abs(v) / maksAbs);
                    double x = petaKiri + k * ukuran;
                    double y = petaAtas + r * ukuran;
                    sb.Append($"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(ukuran)}\" "
                            + $"height=\"{N(ukuran)}\" fill=\"{WarnaPanas(intens)}\" "
                            + $"stroke=\"{WarnaGaris}\" stroke-width=\"0.5\"/>");
                    if (ukuran >= 38)
                    {
                        sb.Append($"<rect x=\"{N(x)}\" y=\"{N(y + ukuran / 2 - 8)}\" "
                                + $"width=\"{N(ukuran)}\" height=\"16\" fill=\"#000000\" fill-opacity=\"0.43\"/>");
                        sb.Append(Teks(x + ukuran / 2, y + ukuran / 2, Ringkas(v),
                                       Rata.Tengah, true, "#FFFFFF", 10));
                    }
                }
            }

            
            
            double lebarKolomMaks = Math.Max(24, ukuran * 1.414 - tinggiTeks);
            for (int k = 0; k < kolom && k < spec.HeatmapColLabels.Count; k++)
            {
                double x = petaKiri + (k + 0.5) * ukuran;
                double y = petaAtas + tinggiPeta + 6;
                sb.Append($"<g transform=\"rotate(-45 {N(x)} {N(y)})\">")
                  .Append(Teks(x, y, Pendek(spec.HeatmapColLabels[k], lebarKolomMaks), Rata.Tengah, true))
                  .Append("</g>");
            }

            
            
            bool barisMendatar = ukuran >= 16;
            for (int r = 0; r < baris && r < spec.HeatmapRowLabels.Count; r++)
            {
                double y = petaAtas + (r + 0.5) * ukuran;
                if (barisMendatar)
                {
                    sb.Append(Teks(petaKiri - 8, y,
                                   Pendek(spec.HeatmapRowLabels[r], Math.Max(20, petaKiri - 10)),
                                   Rata.Kanan, true));
                }
                else
                {
                    double x = petaKiri - 10;
                    sb.Append($"<g transform=\"rotate(-90 {N(x)} {N(y)})\">")
                      .Append(Teks(x, y, Pendek(spec.HeatmapRowLabels[r], Math.Max(16, ukuran - 4)),
                                   Rata.Tengah, true))
                      .Append("</g>");
                }
            }

            double barX = petaKiri + lebarPeta + jarakBar, barY = petaAtas;
            double barH = tinggiPeta;
            int segmen = 18;
            for (int s = 0; s < segmen; s++)
            {
                double t = s / (double)(segmen - 1);
                sb.Append($"<rect x=\"{N(barX)}\" y=\"{N(barY + barH - (s + 1) * barH / segmen)}\" "
                        + $"width=\"{N(barW)}\" height=\"{N(barH / segmen + 0.5)}\" "
                        + $"fill=\"{WarnaPanas(t)}\" stroke=\"{WarnaGaris}\" stroke-width=\"0.5\"/>");
            }
            sb.Append(Teks(barX + barW + 4, barY + barH, "0", Rata.Kiri, true));
            sb.Append(Teks(barX + barW + 4, barY, Ringkas(maksAbs), Rata.Kiri, true));
        }

        

        private static void BatangGalat(StringBuilder sb, ChartSpec spec, int lebar, int tinggi)
        {
            var data = spec.ErrorBars;
            if (data.Count == 0) return;

            double bawahSemua = data.Where(d => !double.IsNaN(d.Bawah)).Select(d => d.Bawah).DefaultIfEmpty(0).Min();
            double atasSemua = data.Where(d => !double.IsNaN(d.Atas)).Select(d => d.Atas).DefaultIfEmpty(0).Max();
            double tengah = data.Select(d => d.Mean).DefaultIfEmpty(0).Average();
            if (atasSemua - bawahSemua < 1e-12)
            {
                double ruang = Math.Max(1.0, Math.Abs(tengah) * 0.1);
                bawahSemua -= ruang;
                atasSemua += ruang;
            }
            double rendah = Math.Min(bawahSemua, tengah), tinggiNilai = Math.Max(atasSemua, tengah);
            double jarak = tinggiNilai - rendah;
            rendah -= jarak * 0.08;
            tinggiNilai += jarak * 0.08;

            
            
            var a = new Area(KiriUntukNilai(rendah, tinggiNilai), 54, lebar, tinggi);

            double Y(double v) => a.Bawah - (v - rendah) / (tinggiNilai - rendah) * a.Tinggi;

            
            sb.Append(SumbuNilai(a, rendah, tinggiNilai));
            sb.Append(Sumbu(a, spec.XTitle, spec.YTitle));

            double langkah = a.Lebar / data.Count;
            for (int i = 0; i < data.Count; i++)
            {
                var (label, mean, bawah, atas) = data[i];
                double x = a.Kiri + (i + 0.5) * langkah;
                string warna = Palet[i % Palet.Length];

                if (!double.IsNaN(bawah) && !double.IsNaN(atas))
                {
                    sb.Append(Garis(x, Y(bawah), x, Y(atas), warna, 2));
                    double tutup = Math.Min(9.0, langkah * 0.25);
                    sb.Append(Garis(x - tutup, Y(bawah), x + tutup, Y(bawah), warna, 2));
                    sb.Append(Garis(x - tutup, Y(atas), x + tutup, Y(atas), warna, 2));
                }

                if (!double.IsNaN(mean))
                    sb.Append($"<circle cx=\"{N(x)}\" cy=\"{N(Y(mean))}\" r=\"4\" fill=\"{warna}\" "
                            + $"stroke=\"{WarnaGaris}\"/>");

                sb.Append(Teks(x, a.Bawah + 8,
                               Pendek(RingkasLabel(label), Math.Max(24, langkah - 4)), Rata.Tengah));
            }
        }

        

        private static void Dendrogram(StringBuilder sb, ChartSpec spec, int lebar, int tinggi)
        {
            var langkah = spec.Penggabungan;
            if (langkah.Count == 0) return;

            int n = spec.LabelDaun.Count;
            if (n < 2) return;

            var posisi = new Dictionary<int, double>();
            for (int i = 0; i < n; i++) posisi[i] = i + 0.5;
            for (int m = 0; m < langkah.Count; m++)
            {
                var g = langkah[m];
                if (!posisi.TryGetValue(g.Kiri, out double pk)) pk = g.Kiri < n ? g.Kiri + 0.5 : 0.5;
                if (!posisi.TryGetValue(g.Kanan, out double pk2)) pk2 = g.Kanan < n ? g.Kanan + 0.5 : 0.5;
                posisi[n + m] = (pk + pk2) / 2.0;
            }

            double maksTinggi = langkah.Max(g => g.Tinggi);
            if (maksTinggi <= 0) maksTinggi = 1;

            
            
            int langkahLabel = (int)Math.Ceiling(n / 30.0);
            double lebarDaun = 0;
            for (int i = 0; i < n; i += langkahLabel)
            {
                string nm = i < spec.LabelDaun.Count ? spec.LabelDaun[i] : (i + 1).ToString(CultureInfo.InvariantCulture);
                lebarDaun = Math.Max(lebarDaun, Lebar(nm));
            }
            lebarDaun = Math.Min(lebarDaun, 90);

            var a = new Area(Math.Max(KiriUntukNilai(0, maksTinggi),
                                      KiriUntuk("0", Ringkas(maksTinggi))),
                             Math.Max(46, lebarDaun * 0.36 + 26), lebar, tinggi);

            double px(double v) => a.Kiri + a.Lebar * (v / n);
            double py(double v) => a.Bawah - a.Tinggi * (v / maksTinggi);

            
            
            sb.Append(SumbuNilai(a, 0, maksTinggi, 6, a.Atas, a.Bawah));
            sb.Append(Sumbu(a, spec.XTitle, spec.YTitle));

            for (int m = 0; m < langkah.Count; m++)
            {
                var g = langkah[m];
                double xKiri = posisi.TryGetValue(g.Kiri, out double pa) ? px(pa) : a.Kiri;
                double xKanan = posisi.TryGetValue(g.Kanan, out double pb) ? px(pb) : a.Kiri;
                double xInduk = px(posisi[n + m]);
                double yInduk = py(g.Tinggi);

                sb.Append(Garis(xKiri, py(TinggiSimpul(g.Kiri, langkah, n)), xKiri, yInduk, WarnaGaris, 1.4));
                sb.Append(Garis(xKanan, py(TinggiSimpul(g.Kanan, langkah, n)), xKanan, yInduk, WarnaGaris, 1.4));
                sb.Append(Garis(xKiri, yInduk, xKanan, yInduk, WarnaGaris, 1.4));
                sb.Append(Garis(xInduk, yInduk, xInduk, py(TinggiInduk(m, langkah, maksTinggi)), WarnaGaris, 1.4));
            }

            double lebarDaunMaks = Math.Max(24, a.Lebar / n * langkahLabel * 1.414 - 15);
            for (int i = 0; i < n; i += langkahLabel)
            {
                string nama = i < spec.LabelDaun.Count ? spec.LabelDaun[i] : (i + 1).ToString(CultureInfo.InvariantCulture);
                double x = px(i + 0.5), y = a.Bawah + 10;
                sb.Append($"<g transform=\"rotate(-45 {N(x)} {N(y)})\">")
                  .Append(Teks(x, y, Pendek(nama, lebarDaunMaks), Rata.Tengah, true))
                  .Append("</g>");
            }

            sb.Append(Teks(a.Kiri - 8, a.Atas, Ringkas(maksTinggi), Rata.Kanan, true));
            sb.Append(Teks(a.Kiri - 8, a.Bawah, "0", Rata.Kanan, true));
        }

        private static double TinggiSimpul(int id, List<(int Kiri, int Kanan, double Tinggi)> langkah, int n)
            => id < n ? 0 : (id - n < langkah.Count ? langkah[id - n].Tinggi : 0);

        private static double TinggiInduk(int m, List<(int Kiri, int Kanan, double Tinggi)> langkah, double maks)
            => m + 1 < langkah.Count ? langkah[m + 1].Tinggi : maks;

        

        private static void Roc(StringBuilder sb, ChartSpec spec, int lebar, int tinggi)
        {
            var titik = spec.Points;
            if (titik.Count < 2) return;

            var a = new Area(KiriUntukNilai(0, 1), 54, lebar, tinggi);
            double px(double v) => a.Kiri + a.Lebar * v;
            double py(double v) => a.Bawah - a.Tinggi * v;

            sb.Append(SumbuNilai(a, 0, 1));
            sb.Append(Sumbu(a, spec.XTitle, spec.YTitle));
            sb.Append(Garis(px(0), py(0), px(1), py(1), WarnaGaris, 1, putus: true));

            var urut = titik.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            for (int i = 0; i + 1 < urut.Count; i++)
            {
                sb.Append(Garis(px(urut[i].X), py(urut[i].Y), px(urut[i + 1].X), py(urut[i].Y), WarnaAksen, 2));
                sb.Append(Garis(px(urut[i + 1].X), py(urut[i].Y), px(urut[i + 1].X), py(urut[i + 1].Y), WarnaAksen, 2));
            }

            foreach (var p in urut)
                sb.Append($"<circle cx=\"{N(px(p.X))}\" cy=\"{N(py(p.Y))}\" r=\"2.2\" "
                        + $"fill=\"{WarnaBiruMuda}\" fill-opacity=\"0.8\"/>");

            sb.Append(TeksTerjepit(a.Kiri, a.Bawah + 8, "0", lebar));
            sb.Append(TeksTerjepit(a.Kanan, a.Bawah + 8, "1", lebar));
        }

        private static string WarnaPanas(double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            byte r = (byte)(0x1E + (0xC8 - 0x1E) * t);
            byte g = (byte)(0x3A + (0x4F - 0x3A) * t);
            byte b = (byte)(0x5F + (0x4F - 0x5F) * t);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}
