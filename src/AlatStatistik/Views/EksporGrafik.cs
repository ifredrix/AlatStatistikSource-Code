using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AlatStatistik.Models;

namespace AlatStatistik.Views
{

    public static class EksporGrafik
    {

        public const int LebarLogis = 640;
        public const int TinggiLogis = 300;

        public const double Skala = 4;

        public static BitmapSource Render(ChartSpec spec, double skala, bool denganJudul = true)
        {
            var kanvas = new ChartControl
            {
                Width = LebarLogis,
                Height = TinggiLogis,
                TampilkanJudul = denganJudul
            };
            kanvas.Gambar(spec);
            kanvas.Measure(new Size(LebarLogis, TinggiLogis));
            kanvas.Arrange(new Rect(0, 0, LebarLogis, TinggiLogis));
            kanvas.UpdateLayout();

            int pikselL = (int)Math.Round(LebarLogis * skala);
            int pikselT = (int)Math.Round(TinggiLogis * skala);
            var rtb = new RenderTargetBitmap(pikselL, pikselT, 96 * skala, 96 * skala,
                                             PixelFormats.Pbgra32);
            rtb.Render(kanvas);
            rtb.Freeze();
            return rtb;
        }

        public static byte[] PngBytes(ChartSpec spec, double skala = Skala, bool denganJudul = true)
        {
            var bmp = Render(spec, skala, denganJudul);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var ms = new MemoryStream();
            enc.Save(ms);
            return ms.ToArray();
        }

        public static DataObject IsiPapanKlip(ChartSpec spec)
        {
            byte[] png = PngBytes(spec, Skala);
            var cadangan = Render(spec, 1);

            var data = new DataObject();
            data.SetData("PNG", new MemoryStream(png));
            data.SetData(DataFormats.Bitmap, cadangan);
            return data;
        }

        public static void SalinKePapanKlip(ChartSpec spec)
        {
            var data = IsiPapanKlip(spec);
            try
            {
                Clipboard.SetDataObject(data, true);
            }
            catch (Exception)
            {
                
                
                Clipboard.SetImage(Render(spec, 1));
            }
        }

        public static void SimpanPng(ChartSpec spec, string berkas)
        {
            File.WriteAllBytes(berkas, PngBytes(spec, Skala));
        }

        public static void SimpanSvg(ChartSpec spec, string berkas)
        {
            string svg = GrafikSvg.Buat(spec, LebarLogis, TinggiLogis, denganJudul: true);
            File.WriteAllText(berkas, svg, new UTF8Encoding(false));
        }

        public static void SimpanDataCsv(ChartSpec spec, string berkas)
        {
            
            
            File.WriteAllText(berkas, DataCsv(spec), new UTF8Encoding(true));
        }

        public static string DataCsv(ChartSpec spec)
        {
            var sb = new StringBuilder();
            var budaya = CultureInfo.GetCultureInfo("id-ID");
            string Angka(double v) => v.ToString("0.######", budaya);
            void Baris(params string[] sel) => sb.AppendLine(string.Join(";", sel));

            switch (spec.Kind)
            {
                case ChartKind.Histogram:
                case ChartKind.Line:
                case ChartKind.BoxPlot:
                    Baris("seri", "urutan", "nilai");
                    foreach (var kv in spec.Series)
                        for (int i = 0; i < kv.Value.Count; i++)
                            Baris(kv.Key, (i + 1).ToString(budaya), Angka(kv.Value[i]));
                    break;

                case ChartKind.Bar:
                case ChartKind.Pie:
                    Baris("label", "nilai");
                    foreach (var (label, value) in spec.Bars)
                        Baris(label, Angka(value));
                    break;

                case ChartKind.Scatter:
                case ChartKind.Roc:
                    Baris("x", "y");
                    foreach (var (x, y) in spec.Points)
                        Baris(Angka(x), Angka(y));
                    break;

                case ChartKind.ErrorBar:
                    Baris("label", "nilai tengah", "batas bawah", "batas atas");
                    foreach (var (label, mean, bawah, atas) in spec.ErrorBars)
                        Baris(label, Angka(mean), Angka(bawah), Angka(atas));
                    break;

                case ChartKind.Heatmap:
                    var kepala = new List<string> { "" };
                    kepala.AddRange(spec.HeatmapColLabels);
                    Baris(kepala.ToArray());
                    for (int r = 0; r < spec.Heatmap.Count; r++)
                    {
                        var sel = new List<string>
                        {
                            r < spec.HeatmapRowLabels.Count ? spec.HeatmapRowLabels[r] : (r + 1).ToString(budaya)
                        };
                        foreach (double v in spec.Heatmap[r]) sel.Add(Angka(v));
                        Baris(sel.ToArray());
                    }
                    break;

                case ChartKind.Dendrogram:
                    Baris("langkah", "simpul kiri", "simpul kanan", "tinggi");
                    for (int i = 0; i < spec.Penggabungan.Count; i++)
                    {
                        var (kiri, kanan, tinggi) = spec.Penggabungan[i];
                        Baris((i + 1).ToString(budaya), kiri.ToString(budaya),
                              kanan.ToString(budaya), Angka(tinggi));
                    }
                    break;
            }

            return sb.ToString();
        }

        public static string NamaAman(string? judul, string bawaan = "grafik")
        {
            if (string.IsNullOrWhiteSpace(judul)) return bawaan;

            var sb = new StringBuilder();
            foreach (char c in judul!)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c is ' ' or '-' or '_') sb.Append('_');
            }

            string hasil = sb.ToString().Trim('_');
            while (hasil.Contains("__")) hasil = hasil.Replace("__", "_");
            return hasil.Length == 0 ? bawaan : hasil;
        }
    }
}
