using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Automation;
using AlatStatistik.Models;
using AlatStatistik.Statistics;

namespace AlatStatistik.Views
{

    public class ChartControl : System.Windows.Controls.Canvas
    {
        private ChartSpec? _spec;

        private static readonly Brush WarnaGaris = new SolidColorBrush(Color.FromRgb(0x4F, 0x5A, 0x6E));
        private static readonly Brush WarnaTeks = new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x3A));
        private static readonly Brush WarnaAksen = new SolidColorBrush(Color.FromRgb(0x0E, 0xA5, 0xE9));

        private static readonly Brush WarnaBantu = new SolidColorBrush(Color.FromRgb(0xDD, 0xE3, 0xEC));
        private static readonly Brush[] Palet =
        {
            new SolidColorBrush(Color.FromRgb(0x0E, 0xA5, 0xE9)),
            new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)),
            new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
            new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)),
            new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
            new SolidColorBrush(Color.FromRgb(0xDB, 0x27, 0x77))
        };

        public bool TampilkanJudul { get; set; }

        private double _atasEkstra;

        public void Gambar(ChartSpec spec)
        {
            _spec = spec;
            AutomationProperties.SetName(this,
                !string.IsNullOrWhiteSpace(spec.Title) ? spec.Title : "Diagram hasil");
            AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (_spec == null || ActualWidth < 40 || ActualHeight < 40) return;

            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)), null,
                             new Rect(0, 0, ActualWidth, ActualHeight));

            _atasEkstra = 0;
            if (TampilkanJudul && !string.IsNullOrEmpty(_spec.Title))
            {
                _atasEkstra = 26;
                var ft = new FormattedText(_spec.Title, CultureInfo.CurrentCulture,
                                           FlowDirection.LeftToRight, new Typeface("Segoe UI"),
                                           13, WarnaTeks, VisualTreeHelper.GetDpi(this).PixelsPerDip)
                {
                    MaxTextWidth = Math.Max(40, ActualWidth - 24),
                    MaxLineCount = 2,
                    Trimming = TextTrimming.CharacterEllipsis
                };
                dc.DrawText(ft, new Point((ActualWidth - ft.Width) / 2, 6));
            }

            switch (_spec.Kind)
            {
                case ChartKind.Histogram: GambarHistogram(dc); break;
                case ChartKind.BoxPlot: GambarBoxPlot(dc); break;
                case ChartKind.Scatter: GambarPencar(dc); break;
                case ChartKind.Bar: GambarBatang(dc); break;
                case ChartKind.Line: GambarGaris(dc); break;
                case ChartKind.Pie: GambarLingkaran(dc); break;
                case ChartKind.Heatmap: GambarPetaPanas(dc); break;
                case ChartKind.ErrorBar: GambarBatangGalat(dc); break;
                case ChartKind.Dendrogram: GambarDendrogram(dc); break;
                case ChartKind.Roc: GambarRoc(dc); break;
            }
        }

        

        private enum Rata { Kiri, Tengah, Kanan }

        private Rect Area(double kiri, double bawah, double atas = 16, double kanan = 18)
            => new Rect(kiri, atas + _atasEkstra,
                        Math.Max(10, ActualWidth - kiri - kanan),
                        Math.Max(10, ActualHeight - atas - _atasEkstra - bawah));

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

        private double KiriUntukNilai(double bawah, double atas, int maksTingkat = 6)
            => KiriUntuk(TingkatBulat(bawah, atas, maksTingkat).Select(Ringkas).ToArray());

        private void SumbuNilai(DrawingContext dc, Rect area, double bawah, double atas,
                                int maksTingkat = 6, params double[] yTerpakai)
        {
            if (!(atas > bawah)) return;
            var pena = new Pen(WarnaBantu, 1);

            foreach (double v in TingkatBulat(bawah, atas, maksTingkat))
            {
                double y = area.Bottom - (v - bawah) / (atas - bawah) * area.Height;
                if (y < area.Top - 0.5 || y > area.Bottom + 0.5) continue;
                dc.DrawLine(pena, new Point(area.Left, y), new Point(area.Right, y));

                bool dekat = yTerpakai.Any(t => Math.Abs(t - y) < 14);
                if (!dekat) Teks(dc, Ringkas(v), new Point(area.Left - 8, y), Rata.Kanan, true);
            }

            
            dc.DrawRectangle(null, new Pen(WarnaBantu, 1), area);
        }

        private double KiriUntuk(params string[] label)
        {
            double lebar = label.Where(s => !string.IsNullOrEmpty(s))
                                .Select(s => Lebar(s))
                                .DefaultIfEmpty(0).Max();
            return Math.Max(66, 36 + lebar);
        }

        private double Lebar(string isi, double ukuran = 11)
        {
            if (string.IsNullOrEmpty(isi)) return 0;
            return new FormattedText(isi, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                     new Typeface("Segoe UI"), ukuran, WarnaTeks,
                                     VisualTreeHelper.GetDpi(this).PixelsPerDip).Width;
        }

        private string Pendek(string isi, double lebarMaks)
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

        private void Sumbu(DrawingContext dc, Rect area, string judulX, string judulY,
                           double judulXBawah = 32)
        {
            var pena = new Pen(WarnaGaris, 1);
            dc.DrawLine(pena, new Point(area.Left, area.Top), new Point(area.Left, area.Bottom));
            dc.DrawLine(pena, new Point(area.Left, area.Bottom), new Point(area.Right, area.Bottom));

            if (!string.IsNullOrEmpty(judulX))
                Teks(dc, judulX, new Point(area.Left + area.Width / 2, area.Bottom + judulXBawah),
                     Rata.Tengah);

            if (!string.IsNullOrEmpty(judulY))
            {
                
                
                const double x = 15;
                double tengahY = area.Top + area.Height / 2;
                dc.PushTransform(new RotateTransform(-90, x, tengahY));
                Teks(dc, judulY, new Point(x, tengahY), Rata.Tengah, true);
                dc.Pop();
            }
        }

        private void Teks(DrawingContext dc, string isi, Point posisi,
                          Rata rata = Rata.Kiri, bool tengahV = false, Brush? warna = null)
        {
            var ft = new FormattedText(isi, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                       new Typeface("Segoe UI"), 11, warna ?? WarnaTeks,
                                       VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double x = rata switch
            {
                Rata.Tengah => posisi.X - ft.Width / 2,
                Rata.Kanan => posisi.X - ft.Width,
                _ => posisi.X
            };
            dc.DrawText(ft, new Point(x, tengahV ? posisi.Y - ft.Height / 2 : posisi.Y));
        }

        private void TeksTerjepit(DrawingContext dc, string isi, double x, double y,
                                  double tepiKiri, double tepiKanan, bool tengahV = false)
        {
            double lebar = Lebar(isi);
            double geser = 0;
            if (x - lebar / 2 < tepiKiri) geser = tepiKiri - (x - lebar / 2);
            else if (x + lebar / 2 > tepiKanan) geser = tepiKanan - (x + lebar / 2);

            Teks(dc, isi, new Point(x + geser, y), Rata.Tengah, tengahV);
        }

        private static string Ringkas(double v)
            => Math.Abs(v) >= 1000 || (Math.Abs(v) < 0.01 && v != 0)
                ? v.ToString("0.0E+0", CultureInfo.InvariantCulture).Replace(".", ",")
                : v.ToString("0.##", CultureInfo.InvariantCulture).Replace(".", ",");

        private static string RingkasLabel(string label)
            => double.TryParse(label, NumberStyles.Any, CultureInfo.InvariantCulture, out double v)
                ? Ringkas(v)
                : label;

        

        private void GambarHistogram(DrawingContext dc)
        {
            var values = _spec!.Series.Values.FirstOrDefault() ?? new List<double>();
            if (values.Count == 0) return;

            double min = values.Min(), max = values.Max();
            if (Math.Abs(max - min) < 1e-12) { max += 1; min -= 1; }

            int bin = Math.Max(5, Math.Min(30, (int)Math.Ceiling(Math.Sqrt(values.Count))));
            double lebar = (max - min) / bin;
            var counts = new int[bin];
            foreach (double v in values)
            {
                int i = (int)Math.Floor((v - min) / lebar);
                if (i >= bin) i = bin - 1;
                if (i >= 0) counts[i]++;
            }

            int tertinggi = counts.Max();
            if (tertinggi == 0) return;

            Rect area = Area(KiriUntukNilai(0, tertinggi), 54);

            
            SumbuNilai(dc, area, 0, tertinggi);

            for (int i = 0; i < bin; i++)
            {
                double x = area.Left + i * area.Width / bin;
                double h = area.Height * counts[i] / tertinggi;
                var warna = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)) { Opacity = 0.85 };
                dc.DrawRectangle(warna, new Pen(WarnaGaris, 1),
                                 new Rect(x + 1, area.Bottom - h, area.Width / bin - 2, h));
            }

            Sumbu(dc, area, _spec.XTitle, "Frekuensi");
            TeksTerjepit(dc, Ringkas(min), area.Left, area.Bottom + 8, 2, ActualWidth - 2);
            TeksTerjepit(dc, Ringkas(max), area.Right, area.Bottom + 8, 2, ActualWidth - 2);
        }

        

        private void GambarBoxPlot(DrawingContext dc)
        {
            var series = _spec!.Series;
            if (series.Count == 0) return;

            double semuaMin = series.Values.SelectMany(v => v).Min();
            double semuaMax = series.Values.SelectMany(v => v).Max();
            if (Math.Abs(semuaMax - semuaMin) < 1e-12) { semuaMax += 1; semuaMin -= 1; }
            double ruang = (semuaMax - semuaMin) * 0.08;
            double rendah = semuaMin - ruang, tinggiSkala = semuaMax + ruang;

            Rect area = Area(KiriUntuk(TingkatBulat(rendah, tinggiSkala).Select(Ringkas)
                                       .Append(Ringkas(semuaMin))
                                       .Append(Ringkas(semuaMax)).ToArray()), 54);

            double y(double nilai)
                => area.Bottom - area.Height * (nilai - rendah) / (tinggiSkala - rendah);

            
            
            
            double yMin = y(semuaMin), yMax = y(semuaMax);
            SumbuNilai(dc, area, rendah, tinggiSkala, 6, yMin, yMax);
            Teks(dc, Ringkas(semuaMin), new Point(area.Left - 8, yMin), Rata.Kanan, true);
            Teks(dc, Ringkas(semuaMax), new Point(area.Left - 8, yMax), Rata.Kanan, true);

            double lebarSlot = area.Width / series.Count;
            int indeks = 0;

            foreach (var kv in series)
            {
                
                
                
                var s = Descriptives.Summarize(kv.Value, Descriptives.DefinisiPagarPencilan);
                double cx = area.Left + lebarSlot * (indeks + 0.5);
                double w = Math.Min(70, lebarSlot * 0.55);
                Brush warna = Palet[indeks % Palet.Length];

                double batasBawah = s.Q1 - 1.5 * s.Iqr;
                double batasAtas = s.Q3 + 1.5 * s.Iqr;
                var dalam = kv.Value.Where(v => v >= batasBawah && v <= batasAtas).ToList();
                double ujungBawah = dalam.Count > 0 ? dalam.Min() : s.Min;
                double ujungAtas = dalam.Count > 0 ? dalam.Max() : s.Max;

                var pena = new Pen(warna, 1.6);

                
                dc.DrawLine(pena, new Point(cx, y(ujungAtas)), new Point(cx, y(s.Q3)));
                dc.DrawLine(pena, new Point(cx, y(ujungBawah)), new Point(cx, y(s.Q1)));
                dc.DrawLine(pena, new Point(cx - w / 4, y(ujungAtas)), new Point(cx + w / 4, y(ujungAtas)));
                dc.DrawLine(pena, new Point(cx - w / 4, y(ujungBawah)), new Point(cx + w / 4, y(ujungBawah)));

                
                double yAtas = y(s.Q3), yBawah = y(s.Q1);
                var isi = new SolidColorBrush(((SolidColorBrush)warna).Color) { Opacity = 0.28 };
                dc.DrawRectangle(isi, pena, new Rect(cx - w / 2, yAtas, w, Math.Max(1, yBawah - yAtas)));

                
                dc.DrawLine(new Pen(warna, 2.6), new Point(cx - w / 2, y(s.Median)), new Point(cx + w / 2, y(s.Median)));

                
                foreach (double v in kv.Value.Where(v => v < batasBawah || v > batasAtas))
                    dc.DrawEllipse(null, new Pen(WarnaTeks, 1), new Point(cx, y(v)), 2.5, 2.5);

                string nama = Pendek(kv.Key, Math.Min(110, lebarSlot - 6));
                Teks(dc, nama, new Point(cx, area.Bottom + 8), Rata.Tengah);
                indeks++;
            }

            Sumbu(dc, area, _spec.XTitle, _spec.YTitle);
        }

        

        private void GambarPencar(DrawingContext dc)
        {
            var titik = _spec!.Points;
            if (titik.Count == 0) return;

            double xMin = titik.Min(p => p.X), xMax = titik.Max(p => p.X);
            double yMin = titik.Min(p => p.Y), yMax = titik.Max(p => p.Y);
            if (Math.Abs(xMax - xMin) < 1e-12) { xMax += 1; xMin -= 1; }
            if (Math.Abs(yMax - yMin) < 1e-12) { yMax += 1; yMin -= 1; }

            Rect area = Area(KiriUntukNilai(yMin, yMax), 54);

            double px(double v) => area.Left + area.Width * (v - xMin) / (xMax - xMin);
            double py(double v) => area.Bottom - area.Height * (v - yMin) / (yMax - yMin);

            SumbuNilai(dc, area, yMin, yMax);
            Sumbu(dc, area, _spec.XTitle, _spec.YTitle);

            
            
            
            
            
            
            dc.PushClip(new RectangleGeometry(area));

            
            if (_spec.RegressionLine.HasValue)
            {
                var (slope, intercept) = _spec.RegressionLine.Value;
                dc.DrawLine(new Pen(WarnaAksen, 2),
                            new Point(px(xMin), py(slope * xMin + intercept)),
                            new Point(px(xMax), py(slope * xMax + intercept)));
            }

            var kuas = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)) { Opacity = 0.75 };
            foreach (var p in titik)
                dc.DrawEllipse(kuas, null, new Point(px(p.X), py(p.Y)), 3, 3);

            dc.Pop();

            TeksTerjepit(dc, Ringkas(xMin), area.Left, area.Bottom + 8, 2, ActualWidth - 2);
            TeksTerjepit(dc, Ringkas(xMax), area.Right, area.Bottom + 8, 2, ActualWidth - 2);
        }

        

        private void GambarBatang(DrawingContext dc)
        {
            var batang = _spec!.Bars;
            if (batang.Count == 0) return;

            double tertinggi = batang.Max(b => b.Value);
            if (tertinggi <= 0) tertinggi = 1;

            Rect area = Area(KiriUntukNilai(0, tertinggi), 54);
            SumbuNilai(dc, area, 0, tertinggi);

            double lebarSlot = area.Width / batang.Count;
            for (int i = 0; i < batang.Count; i++)
            {
                double x = area.Left + i * lebarSlot;
                double h = area.Height * batang[i].Value / tertinggi;
                Brush warna = Palet[i % Palet.Length];
                dc.DrawRectangle(warna, null, new Rect(x + lebarSlot * 0.15, area.Bottom - h,
                                                       lebarSlot * 0.7, h));

                Teks(dc, Ringkas(batang[i].Value),
                     new Point(x + lebarSlot / 2, area.Bottom - h - 10), Rata.Tengah, false, WarnaTeks);

                string nama = Pendek(RingkasLabel(batang[i].Label), Math.Min(110, lebarSlot - 4));
                Teks(dc, nama, new Point(x + lebarSlot / 2, area.Bottom + 8), Rata.Tengah);
            }

            Sumbu(dc, area, _spec.XTitle, _spec.YTitle);
        }

        

        private void GambarGaris(DrawingContext dc)
        {
            var series = _spec!.Series;
            if (series.Count == 0) return;

            
            
            int panjang = series.Values.Max(v => v.Count);
            if (panjang < 2) return;

            
            
            
            
            
            double dataMin = series.Values.SelectMany(v => v).DefaultIfEmpty(0).Min();
            double dataMax = series.Values.SelectMany(v => v).DefaultIfEmpty(0).Max();
            if (dataMax - dataMin < 1e-12) { dataMin -= 1; dataMax += 1; }
            double ruangY = (dataMax - dataMin) * 0.08;
            double yMin = dataMin - ruangY;
            double yMax = dataMax + ruangY;

            Rect area = Area(KiriUntukNilai(yMin, yMax), 54);
            double py(double v) => area.Bottom - area.Height * (v - yMin) / (yMax - yMin);

            SumbuNilai(dc, area, yMin, yMax);
            Sumbu(dc, area, _spec.XTitle, _spec.YTitle);

            
            int idx = 0;
            foreach (var kv in series)
            {
                Brush warnaGaris = Palet[idx % Palet.Length];
                var pena = new Pen(warnaGaris, 1.8);

                var titik = new List<Point>();
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    double x = area.Left + area.Width * i / Math.Max(1, panjang - 1);
                    titik.Add(new Point(x, py(kv.Value[i])));
                }

                if (titik.Count >= 2)
                {
                    var geo = new StreamGeometry();
                    using (var ctx = geo.Open())
                    {
                        ctx.BeginFigure(titik[0], false, false);
                        for (int i = 1; i < titik.Count; i++) ctx.LineTo(titik[i], true, false);
                    }
                    geo.Freeze();
                    dc.DrawGeometry(null, pena, geo);
                }

                var isi = new SolidColorBrush(((SolidColorBrush)warnaGaris).Color) { Opacity = 0.9 };
                foreach (var t in titik) dc.DrawEllipse(isi, null, t, 2.4, 2.4);

                idx++;
            }

            
            
            
            
            var namaSeri = series.Keys.ToList();
            const double barisLegenda = 15;
            double lebarTeksLegenda = Math.Max(40, Math.Min(area.Width * 0.45,
                namaSeri.Select(n => Lebar(n)).DefaultIfEmpty(0).Max()));
            double lebarLegenda = 34 + lebarTeksLegenda;
            double tinggiLegenda = namaSeri.Count * barisLegenda + 10;
            double legendaKanan = area.Right - 10;
            double legendaKiri = legendaKanan - lebarLegenda;
            double legendaAtas = area.Top + 8;

            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF)),
                             new Pen(WarnaGaris, 0.8),
                             new Rect(legendaKiri, legendaAtas, lebarLegenda, tinggiLegenda));

            idx = 0;
            foreach (var kv in series)
            {
                var pena = new Pen(Palet[idx % Palet.Length], 1.8);

                
                
                
                string nama = Pendek(kv.Key, lebarTeksLegenda);
                double yLeg = legendaAtas + 5 + idx * barisLegenda + barisLegenda / 2;
                dc.DrawLine(pena, new Point(legendaKiri + 8, yLeg), new Point(legendaKiri + 26, yLeg));
                Teks(dc, nama, new Point(legendaKanan - 8, yLeg), Rata.Kanan, true, WarnaTeks);

                idx++;
            }

            
            
            TeksTerjepit(dc, "1", area.Left, area.Bottom + 8, 2, ActualWidth - 2);
            if (panjang >= 3)
                TeksTerjepit(dc, ((panjang + 1) / 2).ToString(),
                             area.Left + area.Width / 2, area.Bottom + 8, 2, ActualWidth - 2);
            if (panjang >= 2)
                TeksTerjepit(dc, panjang.ToString(),
                             area.Right, area.Bottom + 8, 2, ActualWidth - 2);
        }

        

        private void GambarLingkaran(DrawingContext dc)
        {
            var iris = _spec!.Bars;
            if (iris.Count == 0) return;

            double total = iris.Sum(b => Math.Max(0, b.Value));
            if (total <= 0) return;

            
            
            double lebarKet = Math.Min(300, Math.Max(120, ActualWidth * 0.42));
            double ketKiri = ActualWidth - lebarKet - 12;
            double cx = ketKiri / 2;
            double cy = _atasEkstra + (ActualHeight - _atasEkstra) / 2 - 4;
            double r = Math.Min(cx - 16, cy - _atasEkstra - 12) - 6;
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

                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(cx, cy), true, true);
                    ctx.LineTo(new Point(x1, y1), true, false);
                    ctx.ArcTo(new Point(x2, y2), new Size(r, r), 0,
                              porsi > 0.5, SweepDirection.Clockwise, true, false);
                }
                geo.Freeze();

                dc.DrawGeometry(Palet[idx % Palet.Length], new Pen(WarnaGaris, 1), geo);

                
                
                
                if (porsi >= 0.10)
                {
                    string persen = (porsi * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
                    if (Lebar(persen) <= r * 0.9)
                    {
                        double tengahSudut = (sudutMulai + sudutAkhir) / 2;
                        Teks(dc, persen,
                             new Point(cx + r * 0.58 * Math.Cos(tengahSudut),
                                       cy + r * 0.58 * Math.Sin(tengahSudut)),
                             Rata.Tengah, true, Brushes.White);
                    }
                }

                sudutMulai = sudutAkhir;
                idx++;
            }

            
            dc.DrawEllipse(null, new Pen(WarnaGaris, 1), new Point(cx, cy), r, r);

            GambarKeteranganIrisan(dc, iris, total, ketKiri, lebarKet);
        }

        private void GambarKeteranganIrisan(
            DrawingContext dc, List<(string Label, double Value)> iris, double total,
            double kiri, double lebar)
        {
            const double baris = 15;
            
            
            int maksBaris = (int)Math.Floor((ActualHeight - 46) / baris);
            if (maksBaris < 1 || lebar < 60) return;

            double lebarTeks = lebar - 22;
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

                dc.DrawRectangle(Palet[i % Palet.Length], new Pen(WarnaGaris, 0.8),
                                 new Rect(kiri, y + 3, 11, 9));
                Teks(dc, nama + "  " + persen, new Point(kiri + 17, y), Rata.Kiri, false);
                y += baris;
            }

            if (dipotong)
            {
                
                
                
                
                double porsiTampil = iris.Take(jumlahTampil).Sum(b => Math.Max(0, b.Value));
                double sisaPersen = total > 0 ? (1 - porsiTampil / total) * 100 : 0;
                string sisa = $"… {iris.Count - jumlahTampil} irisan lain ("
                            + sisaPersen.ToString("0.#", CultureInfo.InvariantCulture)
                            + "% dari yang digambar)";
                Teks(dc, Pendek(sisa, lebarTeks), new Point(kiri + 17, y), Rata.Kiri, false);
            }

            
            
            
            double n = _spec!.TotalN > 0 ? _spec.TotalN : total;
            string catatan = $"N = {Fmt.Int((int)n)}";
            if (Math.Abs(n - total) > 0.5)
                catatan += $" · {iris.Count} kategori terbesar digambar";
            Teks(dc, Pendek(catatan, lebar), new Point(kiri, ActualHeight - 24), Rata.Kiri, false);
        }

        

        private void GambarBatangGalat(DrawingContext dc)
        {
            var data = _spec!.ErrorBars;
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
            double rendah = Math.Min(bawahSemua, tengah), tinggi = Math.Max(atasSemua, tengah);
            double jarak = tinggi - rendah;
            rendah -= jarak * 0.08;
            tinggi += jarak * 0.08;

            
            
            Rect area = Area(KiriUntukNilai(rendah, tinggi), 54);

            double Y(double v) => area.Bottom - (v - rendah) / (tinggi - rendah) * area.Height;

            
            SumbuNilai(dc, area, rendah, tinggi);
            Sumbu(dc, area, _spec.XTitle, _spec.YTitle);

            double langkah = area.Width / data.Count;
            for (int i = 0; i < data.Count; i++)
            {
                var (label, mean, bawah, atas) = data[i];
                double x = area.Left + (i + 0.5) * langkah;
                Brush warna = Palet[i % Palet.Length];
                var pena = new Pen(warna, 2);

                if (!double.IsNaN(bawah) && !double.IsNaN(atas))
                {
                    
                    dc.DrawLine(pena, new Point(x, Y(bawah)), new Point(x, Y(atas)));
                    double tutup = Math.Min(9.0, langkah * 0.25);
                    dc.DrawLine(pena, new Point(x - tutup, Y(bawah)), new Point(x + tutup, Y(bawah)));
                    dc.DrawLine(pena, new Point(x - tutup, Y(atas)), new Point(x + tutup, Y(atas)));
                }

                
                if (!double.IsNaN(mean))
                    dc.DrawEllipse(warna, new Pen(WarnaGaris, 1), new Point(x, Y(mean)), 4, 4);

                string nama = Pendek(RingkasLabel(label), Math.Max(24, langkah - 4));
                Teks(dc, nama, new Point(x, area.Bottom + 8), Rata.Tengah);
            }
        }

        

        private void GambarPetaPanas(DrawingContext dc)
        {
            var matriks = _spec!.Heatmap;
            if (matriks.Count == 0 || matriks[0].Count == 0) return;

            int baris = matriks.Count;
            int kolom = matriks[0].Count;
            double maksAbs = matriks.SelectMany(r => r).Select(Math.Abs).DefaultIfEmpty(0).Max();
            if (maksAbs <= 0) return;

            const double tinggiTeks = 15;

            double lebarBaris = _spec.HeatmapRowLabels.Count > 0
                ? Math.Min(140, _spec.HeatmapRowLabels.Select(s => Lebar(s)).DefaultIfEmpty(0).Max()) : 0;
            double lebarKolom = _spec.HeatmapColLabels.Count > 0
                ? Math.Min(140, _spec.HeatmapColLabels.Select(s => Lebar(s)).DefaultIfEmpty(0).Max()) : 0;

            double kiri = Math.Max(20, lebarBaris + 12);
            double bawah = Math.Max(34, lebarKolom * 0.36 + tinggiTeks + 10);
            double atas = 18 + _atasEkstra;
            const double kanan = 16;

            Rect area = new Rect(kiri, atas,
                                 Math.Max(20, ActualWidth - kiri - kanan),
                                 Math.Max(20, ActualHeight - atas - bawah));

            
            
            double ukuran = Math.Min(70, Math.Min(area.Width / kolom, area.Height / baris));
            if (ukuran < 8) return;

            double lebarPeta = ukuran * kolom;
            double tinggiPeta = ukuran * baris;

            
            
            
            const double jarakBar = 26, barW = 12, ruangAngka = 24;
            double lebarBlok = lebarPeta + jarakBar + barW + ruangAngka;
            double petaKiri = area.Left + Math.Max(0, (area.Width - lebarBlok) / 2);
            double petaAtas = area.Top + Math.Max(0, (area.Height - tinggiPeta) / 2);

            var pena = new Pen(WarnaGaris, 0.5);

            for (int r = 0; r < baris; r++)
            {
                for (int k = 0; k < kolom; k++)
                {
                    double v = r < matriks.Count && k < matriks[r].Count ? matriks[r][k] : 0;
                    double intens = Math.Min(1.0, Math.Abs(v) / maksAbs);

                    double x = petaKiri + k * ukuran;
                    double y = petaAtas + r * ukuran;
                    dc.DrawRectangle(WarnaPanas(intens), pena, new Rect(x, y, ukuran, ukuran));

                    
                    
                    if (ukuran >= 38)
                    {
                        var ft = new FormattedText(
                            Ringkas(v), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                            new Typeface("Segoe UI"), 10, Brushes.White,
                            VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)),
                                         null, new Rect(x, y + ukuran / 2 - 8, ukuran, 16));
                        dc.DrawText(ft, new Point(x + (ukuran - ft.Width) / 2,
                                                  y + ukuran / 2 - ft.Height / 2));
                    }
                }
            }

            
            
            
            double lebarKolomMaks = Math.Max(24, ukuran * 1.414 - tinggiTeks);
            for (int k = 0; k < kolom && k < _spec.HeatmapColLabels.Count; k++)
            {
                double ax = petaKiri + (k + 0.5) * ukuran;
                double ay = petaAtas + tinggiPeta + 6;
                dc.PushTransform(new RotateTransform(-45, ax, ay));
                Teks(dc, Pendek(_spec.HeatmapColLabels[k], lebarKolomMaks),
                     new Point(ax, ay), Rata.Tengah, true);
                dc.Pop();
            }

            
            
            
            bool barisMendatar = ukuran >= 16;
            for (int r = 0; r < baris && r < _spec.HeatmapRowLabels.Count; r++)
            {
                double ay = petaAtas + (r + 0.5) * ukuran;
                if (barisMendatar)
                {
                    
                    
                    
                    Teks(dc, Pendek(_spec.HeatmapRowLabels[r], Math.Max(20, petaKiri - 10)),
                         new Point(petaKiri - 8, ay), Rata.Kanan, true);
                }
                else
                {
                    double ax = petaKiri - 10;
                    dc.PushTransform(new RotateTransform(-90, ax, ay));
                    Teks(dc, Pendek(_spec.HeatmapRowLabels[r], Math.Max(16, ukuran - 4)),
                         new Point(ax, ay), Rata.Tengah, true);
                    dc.Pop();
                }
            }

            
            double barX = petaKiri + lebarPeta + jarakBar;
            double barY = petaAtas;
            double barH = tinggiPeta;
            const int segmen = 18;
            for (int s = 0; s < segmen; s++)
            {
                double t = s / (double)(segmen - 1);
                dc.DrawRectangle(WarnaPanas(t), pena,
                                 new Rect(barX, barY + barH - (s + 1) * barH / segmen,
                                          barW, barH / segmen + 0.5));
            }
            Teks(dc, "0", new Point(barX + barW + 4, barY + barH), Rata.Kiri, true);
            Teks(dc, Ringkas(maksAbs), new Point(barX + barW + 4, barY), Rata.Kiri, true);
        }

        

        private void GambarDendrogram(DrawingContext dc)
        {
            var langkah = _spec!.Penggabungan;
            if (langkah.Count == 0) return;

            int n = _spec.LabelDaun.Count;
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
                string nm = i < _spec.LabelDaun.Count ? _spec.LabelDaun[i] : (i + 1).ToString();
                lebarDaun = Math.Max(lebarDaun, Lebar(nm));
            }
            lebarDaun = Math.Min(lebarDaun, 90);

            Rect area = Area(Math.Max(KiriUntukNilai(0, maksTinggi),
                                      KiriUntuk("0", Ringkas(maksTinggi))),
                             Math.Max(46, lebarDaun * 0.36 + 26));

            double px(double v) => area.Left + area.Width * (v / n);
            double py(double v) => area.Bottom - area.Height * (v / maksTinggi);

            
            
            SumbuNilai(dc, area, 0, maksTinggi, 6, area.Top, area.Bottom);
            Sumbu(dc, area, _spec.XTitle, _spec.YTitle);

            var pena = new Pen(WarnaGaris, 1.4);
            for (int m = 0; m < langkah.Count; m++)
            {
                var g = langkah[m];
                double xKiri = posisi.TryGetValue(g.Kiri, out double a) ? px(a) : area.Left;
                double xKanan = posisi.TryGetValue(g.Kanan, out double b) ? px(b) : area.Left;
                double xInduk = px(posisi[n + m]);
                double yInduk = py(g.Tinggi);

                
                dc.DrawLine(pena, new Point(xKiri, py(TinggiSimpul(g.Kiri, langkah, n))), new Point(xKiri, yInduk));
                dc.DrawLine(pena, new Point(xKanan, py(TinggiSimpul(g.Kanan, langkah, n))), new Point(xKanan, yInduk));
                dc.DrawLine(pena, new Point(xKiri, yInduk), new Point(xKanan, yInduk));
                dc.DrawLine(pena, new Point(xInduk, yInduk), new Point(xInduk, py(TinggiInduk(m, langkah, maksTinggi))));
            }

            
            double lebarDaunMaks = Math.Max(24, area.Width / n * langkahLabel * 1.414 - 15);
            for (int i = 0; i < n; i += langkahLabel)
            {
                string nama = i < _spec.LabelDaun.Count ? _spec.LabelDaun[i] : (i + 1).ToString();
                dc.PushTransform(new RotateTransform(-45, px(i + 0.5), area.Bottom + 10));
                Teks(dc, Pendek(nama, lebarDaunMaks),
                     new Point(px(i + 0.5), area.Bottom + 10), Rata.Tengah, true);
                dc.Pop();
            }

            Teks(dc, Ringkas(maksTinggi), new Point(area.Left - 8, area.Top), Rata.Kanan, true);
            Teks(dc, "0", new Point(area.Left - 8, area.Bottom), Rata.Kanan, true);
        }

        private static double TinggiSimpul(int id, List<(int Kiri, int Kanan, double Tinggi)> langkah, int n)
            => id < n ? 0 : (id - n < langkah.Count ? langkah[id - n].Tinggi : 0);

        private static double TinggiInduk(int m, List<(int Kiri, int Kanan, double Tinggi)> langkah, double maks)
            => m + 1 < langkah.Count ? langkah[m + 1].Tinggi : maks;

        

        private void GambarRoc(DrawingContext dc)
        {
            var titik = _spec!.Points;
            if (titik.Count < 2) return;

            Rect area = Area(KiriUntukNilai(0, 1), 54);
            double px(double v) => area.Left + area.Width * v;
            double py(double v) => area.Bottom - area.Height * v;

            SumbuNilai(dc, area, 0, 1);
            Sumbu(dc, area, _spec.XTitle, _spec.YTitle);

            
            var penaAcak = new Pen(WarnaGaris, 1) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
            dc.DrawLine(penaAcak, new Point(px(0), py(0)), new Point(px(1), py(1)));

            var urut = titik.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            var pena = new Pen(WarnaAksen, 2);
            for (int i = 0; i + 1 < urut.Count; i++)
            {
                
                var a = new Point(px(urut[i].X), py(urut[i].Y));
                var b = new Point(px(urut[i + 1].X), py(urut[i].Y));
                var c = new Point(px(urut[i + 1].X), py(urut[i + 1].Y));
                dc.DrawLine(pena, a, b);
                dc.DrawLine(pena, b, c);
            }

            var kuas = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)) { Opacity = 0.8 };
            foreach (var p in urut)
                dc.DrawEllipse(kuas, null, new Point(px(p.X), py(p.Y)), 2.2, 2.2);

            TeksTerjepit(dc, "0", area.Left, area.Bottom + 8, 2, ActualWidth - 2);
            TeksTerjepit(dc, "1", area.Right, area.Bottom + 8, 2, ActualWidth - 2);
        }

        
        private static Brush WarnaPanas(double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            byte r = (byte)(0x1E + (0xC8 - 0x1E) * t);
            byte g = (byte)(0x3A + (0x4F - 0x3A) * t);
            byte b = (byte)(0x5F + (0x4F - 0x5F) * t);
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
    }
}
