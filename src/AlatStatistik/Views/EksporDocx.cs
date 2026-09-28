using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using AlatStatistik.Models;

namespace AlatStatistik.Views
{

    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    public static class EksporDocx
    {

        
        public const string PerluasanBerkas = ".docx";

        
        public static string FilterDialog =>
            "Laporan Word — tabel asli bisa diedit (*.docx)|*.docx";

        
        
        
        
        
        
        
        public static string DokumenXml(List<ResultBlock> blok, string judul = "Hasil analisis",
                                        string namaData = "")
        {
            var isi = new StringBuilder();

            isi.Append(Paragraf(judul, "Heading1"));

            string cap = DateTime.Now.ToString("dd MMMM yyyy, HH:mm",
                                               new CultureInfo("id-ID"));
            string sumber = string.IsNullOrWhiteSpace(namaData)
                ? "" : " · Data: " + namaData;
            isi.Append(Paragraf("Dihasilkan AlatStatistik · " + cap + sumber, "Catatan",
                                "<w:spacing w:before=\"0\" w:after=\"240\"/>"));

            foreach (var b in blok)
            {
                switch (b.Kind)
                {
                    case BlockKind.Heading: isi.Append(Judul(b)); break;
                    case BlockKind.Table: isi.Append(Tabel(b)); break;
                    case BlockKind.Note: isi.Append(Catatan(b)); break;
                    case BlockKind.Chart: isi.Append(Grafik(b)); break;
                    case BlockKind.Rumus: isi.Append(Rumus(b)); break;
                    case BlockKind.Substitusi: isi.Append(Substitusi(b)); break;
                }
            }

            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n"
                 + "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">\n"
                 + "<w:body>\n"
                 + isi
                 + "<w:sectPr>"
                 + "<w:pgSz w:w=\"" + LebarHalaman + "\" w:h=\"" + TinggiHalaman + "\"/>"
                 + "<w:pgMar w:top=\"1134\" w:right=\"1418\" w:bottom=\"1134\" w:left=\"1418\""
                 + " w:header=\"708\" w:footer=\"708\" w:gutter=\"0\"/>"
                 + "<w:cols w:space=\"708\"/>"
                 + "<w:docGrid w:linePitch=\"360\"/>"
                 + "</w:sectPr>\n"
                 + "</w:body>\n</w:document>\n";
        }

        
        
        
        
        
        
        
        public static string Simpan(string jalur, List<ResultBlock> blok,
                                    string judul = "Hasil analisis", string namaData = "")
        {
            if (!jalur.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                jalur += ".docx";

            using (var berkas = new FileStream(jalur, FileMode.Create, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(berkas, ZipArchiveMode.Create))
            {
                
                
                Tulis(zip, "[Content_Types].xml", JenisKonten);
                Tulis(zip, "_rels/.rels", RelAkar);
                Tulis(zip, "word/document.xml", DokumenXml(blok, judul, namaData));
                Tulis(zip, "word/styles.xml", Gaya);
                Tulis(zip, "word/_rels/document.xml.rels", RelDokumen);
            }
            return jalur;
        }

        
        
        

        private const int LebarHalaman = 11906;   
        private const int TinggiHalaman = 16838;  
        private const int LebarIsi = LebarHalaman - 1418 - 1418;  

        private static void Tulis(ZipArchive zip, string nama, string isi)
        {
            var entri = zip.CreateEntry(nama, CompressionLevel.Optimal);
            using var aliran = entri.Open();
            byte[] bita = new UTF8Encoding(false).GetBytes(isi);
            aliran.Write(bita, 0, bita.Length);
        }

        
        
        
        private static string Judul(ResultBlock b)
        {
            string gaya = b.Level <= 1 ? "Heading2" : (b.Level == 2 ? "Heading3" : "Heading4");
            return Paragraf(b.Text, gaya);
        }

        
        
        
        private static string Tabel(ResultBlock b)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(b.Title))
                sb.Append(Paragraf(b.Title, "JudulTabel"));

            if (b.Columns.Count == 0 && b.Rows.Count == 0)
            {
                sb.Append(Paragraf("Tabel kosong.", "Catatan"));
                return sb.ToString();
            }

            sb.Append(BuatTabel(b.Columns, b.Rows));

            if (!string.IsNullOrWhiteSpace(b.Footnote))
                sb.Append(Paragraf(b.Footnote, "CatatanKaki"));

            return sb.ToString();
        }

        
        
        
        private static string Catatan(ResultBlock b)
        {
            string garis = b.Note switch
            {
                NoteKind.Ok => "047857",
                NoteKind.Warning => "B45309",
                NoteKind.Error => "B91C1C",
                _ => "0369A1"
            };
            string latar = b.Note switch
            {
                NoteKind.Ok => "EDFBF4",
                NoteKind.Warning => "FEF6E7",
                NoteKind.Error => "FDECEC",
                _ => "F1F7FC"
            };

            string ppr =
                  "<w:pBorders><w:left w:val=\"single\" w:sz=\"18\" w:space=\"8\" w:color=\""
                + garis + "\"/></w:pBorders>"
                + "<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"" + latar + "\"/>"
                + "<w:spacing w:before=\"120\" w:after=\"160\"/>"
                + "<w:ind w:left=\"170\" w:right=\"170\"/>";

            return Paragraf(b.Text, null, ppr, null);
        }

        
        
        
        
        private static string Grafik(ResultBlock b)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(b.Title))
                sb.Append(Paragraf(b.Title, "JudulTabel"));

            if (b.Chart == null)
            {
                sb.Append(Paragraf("Grafik tidak tersedia.", "Catatan"));
                return sb.ToString();
            }

            var (kolom, baris) = DataGrafik(b.Chart);
            if (baris.Count == 0)
            {
                sb.Append(Paragraf("Grafik ini tidak dapat digambar (datanya kosong "
                                 + "atau terlalu kecil).", "Catatan"));
                return sb.ToString();
            }

            
            
            
            
            
            sb.Append(Paragraf("Data diagram (angka di bawah ini bisa disalin, diedit, "
                             + "dan dibuat ulang menjadi grafik lewat Sisipkan > Grafik "
                             + "di Word).", "CatatanKaki"));
            sb.Append(BuatTabel(kolom, baris));

            sb.Append(CaraMembaca(b.Chart));
            return sb.ToString();
        }

        private static string CaraMembaca(ChartSpec spec)
        {
            var cara = PenjelasanGrafik.Untuk(spec.Kind);
            if (cara == null) return "";

            var sb = new StringBuilder();
            sb.Append(Paragraf("Cara membaca diagram ini", "JudulTabel"));
            sb.Append(BagianCara("Apa yang dibaca", cara.Bacaan));
            sb.Append(BagianCara("Yang perlu diperhatikan", cara.Perhatikan));
            sb.Append(BagianCara("Hati-hati", cara.HatiHati));
            return sb.ToString();
        }

        private static string BagianCara(string label, string teks)
        {
            if (string.IsNullOrWhiteSpace(teks)) return "";
            string ppr = "<w:spacing w:before=\"80\" w:after=\"40\"/>"
                       + "<w:ind w:left=\"284\"/>";
            return Paragraf(label, null, ppr, "<w:b/><w:sz w:val=\"19\" w:color=\"5A6478\"/>")
                 + Paragraf(teks, null,
                            "<w:spacing w:before=\"0\" w:after=\"120\"/><w:ind w:left=\"568\"/>",
                            "<w:sz w:val=\"20\"/>");
        }

        
        
        
        private static string Rumus(ResultBlock b)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(b.Title))
                sb.Append(Paragraf(b.Title, "JudulTabel"));

            if (b.RumusList.Count == 0)
                return sb.Append(Paragraf("Tidak ada rumus.", "Catatan")).ToString();

            var kolom = new List<string> { "Rumus", "Bentuk", "Asal" };
            var baris = new List<List<string>>();
            var catatan = new List<string>();

            foreach (var r in b.RumusList)
            {
                string asal = string.IsNullOrWhiteSpace(r.Jenis) ? r.LabelTahun
                            : r.Jenis + " · " + r.LabelTahun;
                baris.Add(new List<string> { r.Nama, r.Bentuk, asal });
                if (!string.IsNullOrWhiteSpace(r.Catatan))
                    catatan.Add(r.Nama + ": " + r.Catatan);
            }

            sb.Append(BuatTabel(kolom, baris, monospace: 1));
            foreach (var c in catatan)
                sb.Append(Paragraf(c, "CatatanKaki"));

            return sb.ToString();
        }

        
        
        
        private static string Substitusi(ResultBlock b)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(b.Title))
                sb.Append(Paragraf(b.Title, "JudulTabel"));

            if (b.Langkah.Count == 0)
                return sb.Append(Paragraf("Tidak ada perhitungan.", "Catatan")).ToString();

            var kolom = new List<string> { "Langkah", "Perhitungan" };
            var baris = new List<List<string>>();
            foreach (var l in b.Langkah)
                baris.Add(new List<string> { l.Uraian ?? "", l.Hitungan ?? "" });

            sb.Append(BuatTabel(kolom, baris, monospace: 1));
            return sb.ToString();
        }

        
        
        

        
        
        
        
        
        
        private static string BuatTabel(List<string> kolom, List<List<string>> baris,
                                        int monospace = -1)
        {
            int jumlah = Math.Max(kolom.Count, 1);
            foreach (var r in baris) jumlah = Math.Max(jumlah, r.Count);
            int[] lebar = LebarKolom(jumlah);

            var sb = new StringBuilder();
            sb.Append("<w:tbl>");

            sb.Append("<w:tblPr>");
            sb.Append("<w:tblStyle w:val=\"TableGrid\"/>");
            sb.Append("<w:tblW w:w=\"5000\" w:type=\"pct\"/>");
            sb.Append("<w:tblLayout w:type=\"fixed\"/>");
            sb.Append("<w:tblBorders>");
            foreach (string sisi in new[] { "top", "left", "bottom", "right", "insideH", "insideV" })
                sb.Append("<w:" + sisi + " w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B7C0D0\"/>");
            sb.Append("</w:tblBorders>");
            sb.Append("<w:tblCellMar>"
                    + "<w:top w:w=\"55\" w:type=\"dxa\"/><w:left w:w=\"100\" w:type=\"dxa\"/>"
                    + "<w:bottom w:w=\"55\" w:type=\"dxa\"/><w:right w:w=\"100\" w:type=\"dxa\"/>"
                    + "</w:tblCellMar>");
            sb.Append("</w:tblPr>");

            sb.Append("<w:tblGrid>");
            for (int i = 0; i < jumlah; i++)
                sb.Append("<w:gridCol w:w=\"" + lebar[i] + "\"/>");
            sb.Append("</w:tblGrid>");

            
            sb.Append("<w:tr><w:trPr><w:tblHeader/><w:cantSplit/></w:trPr>");
            for (int i = 0; i < jumlah; i++)
            {
                string isi = i < kolom.Count ? kolom[i] : "";
                sb.Append(Sel(isi, lebar[i], i == 0 ? "left" : "right",
                              "<w:b/><w:sz w:val=\"19\"/><w:color w:val=\"16213A\"/>",
                              "EEF2F8"));
            }
            sb.Append("</w:tr>");

            
            for (int n = 0; n < baris.Count; n++)
            {
                var r = baris[n];
                sb.Append("<w:tr><w:trPr><w:cantSplit/></w:trPr>");
                for (int i = 0; i < jumlah; i++)
                {
                    string isi = i < r.Count ? (r[i] ?? "") : "";
                    string rpr = "<w:sz w:val=\"19\"/>";
                    if (i == monospace)
                        rpr = "<w:rFonts w:ascii=\"Consolas\" w:hAnsi=\"Consolas\" w:cs=\"Consolas\"/>"
                            + "<w:sz w:val=\"19\"/>";
                    sb.Append(Sel(isi, lebar[i], i == 0 ? "left" : "right", rpr,
                                  n % 2 == 1 ? "F7F9FC" : "FFFFFF"));
                }
                sb.Append("</w:tr>");
            }

            sb.Append("</w:tbl>");

            
            
            sb.Append(Paragraf("", null, "<w:spacing w:before=\"0\" w:after=\"100\"/>"));
            return sb.ToString();
        }

        private static string Sel(string isi, int lebar, string jajar, string rpr, string latar)
        {
            return "<w:tc>"
                 + "<w:tcPr>"
                 + "<w:tcW w:w=\"" + lebar + "\" w:type=\"dxa\"/>"
                 + "<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"" + latar + "\"/>"
                 + "<w:vAlign w:val=\"center\"/>"
                 + "</w:tcPr>"
                 + "<w:p>"
                 + "<w:pPr><w:spacing w:before=\"20\" w:after=\"20\" w:line=\"240\" w:lineRule=\"auto\"/>"
                 + "<w:jc w:val=\"" + jajar + "\"/></w:pPr>"
                 + "<w:r><w:rPr>" + rpr + "</w:rPr>"
                 + "<w:t xml:space=\"preserve\">" + Teks(isi) + "</w:t></w:r>"
                 + "</w:p>"
                 + "</w:tc>";
        }

        private static int[] LebarKolom(int jumlah)
        {
            if (jumlah <= 0) return Array.Empty<int>();
            if (jumlah == 1) return new[] { LebarIsi };

            double bobotPertama = jumlah >= 3 ? 1.7 : 1.35;
            double total = bobotPertama + (jumlah - 1);

            var lebar = new int[jumlah];
            int terpakai = 0;
            for (int i = 0; i < jumlah; i++)
            {
                double bobot = i == 0 ? bobotPertama : 1.0;
                lebar[i] = (int)Math.Round(LebarIsi * bobot / total);
                terpakai += lebar[i];
            }
            if (terpakai != LebarIsi) lebar[jumlah - 1] += LebarIsi - terpakai;
            return lebar;
        }

        
        
        
        private static string Paragraf(string teks, string? gaya,
                                       string? pprTambahan = null, string? rpr = null)
        {
            var sb = new StringBuilder();
            sb.Append("<w:p><w:pPr>");
            if (!string.IsNullOrEmpty(gaya))
                sb.Append("<w:pStyle w:val=\"" + gaya + "\"/>");
            if (!string.IsNullOrEmpty(pprTambahan))
                sb.Append(pprTambahan);
            sb.Append("</w:pPr>");
            sb.Append("<w:r>");
            if (!string.IsNullOrEmpty(rpr)) sb.Append("<w:rPr>" + rpr + "</w:rPr>");
            sb.Append("<w:t xml:space=\"preserve\">" + Teks(teks) + "</w:t>");
            sb.Append("</w:r></w:p>");
            return sb.ToString();
        }

        
        
        

        private static (List<string> kolom, List<List<string>> baris) DataGrafik(ChartSpec s)
        {
            var kolom = new List<string>();
            var baris = new List<List<string>>();

            if (s.Bars.Count > 0)
            {
                kolom.Add(string.IsNullOrWhiteSpace(s.XTitle) ? "Kategori" : s.XTitle);
                kolom.Add(string.IsNullOrWhiteSpace(s.YTitle) ? "Nilai" : s.YTitle);
                foreach (var b in s.Bars)
                    baris.Add(new List<string> { b.Label, Angka(b.Value) });
                return (kolom, baris);
            }

            if (s.ErrorBars.Count > 0)
            {
                kolom.AddRange(new[] { "Kelompok", "Rata-rata", "Batas bawah", "Batas atas" });
                foreach (var e in s.ErrorBars)
                    baris.Add(new List<string> { e.Label, Angka(e.Mean), Angka(e.Bawah), Angka(e.Atas) });
                return (kolom, baris);
            }

            if (s.Points.Count > 0)
            {
                kolom.Add(string.IsNullOrWhiteSpace(s.XTitle) ? "X" : s.XTitle);
                kolom.Add(string.IsNullOrWhiteSpace(s.YTitle) ? "Y" : s.YTitle);
                foreach (var p in s.Points)
                    baris.Add(new List<string> { Angka(p.X), Angka(p.Y) });
                return (kolom, baris);
            }

            if (s.Series.Count > 0)
            {
                kolom.Add("Baris ke-");
                foreach (var kv in s.Series) kolom.Add(kv.Key);
                int n = 0;
                foreach (var kv in s.Series) n = Math.Max(n, kv.Value.Count);
                for (int i = 0; i < n; i++)
                {
                    var r = new List<string> { (i + 1).ToString(CultureInfo.InvariantCulture) };
                    foreach (var kv in s.Series)
                        r.Add(i < kv.Value.Count ? Angka(kv.Value[i]) : "-");
                    baris.Add(r);
                }
                return (kolom, baris);
            }

            if (s.Heatmap.Count > 0)
            {
                kolom.Add("");
                foreach (var c in s.HeatmapColLabels) kolom.Add(c);
                for (int i = 0; i < s.Heatmap.Count; i++)
                {
                    var r = new List<string>
                    {
                        i < s.HeatmapRowLabels.Count ? s.HeatmapRowLabels[i]
                                                     : (i + 1).ToString(CultureInfo.InvariantCulture)
                    };
                    foreach (var v in s.Heatmap[i]) r.Add(Angka(v));
                    baris.Add(r);
                }
                return (kolom, baris);
            }

            if (s.Penggabungan.Count > 0)
            {
                kolom.AddRange(new[] { "Tahap", "Kiri", "Kanan", "Tinggi" });
                for (int i = 0; i < s.Penggabungan.Count; i++)
                {
                    var g = s.Penggabungan[i];
                    baris.Add(new List<string>
                    {
                        (i + 1).ToString(CultureInfo.InvariantCulture),
                        NamaDaun(s, g.Kiri),
                        NamaDaun(s, g.Kanan),
                        Angka(g.Tinggi)
                    });
                }
                return (kolom, baris);
            }

            return (kolom, baris);
        }

        private static string NamaDaun(ChartSpec s, int indeks)
        {
            if (indeks >= 0 && indeks < s.LabelDaun.Count) return s.LabelDaun[indeks];
            return indeks.ToString(CultureInfo.InvariantCulture);
        }

        private static string Angka(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return "-";
            return d.ToString("0.######", CultureInfo.InvariantCulture);
        }

        
        
        

        
        
        
        private static string Teks(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return Esc(s).Replace("\n", "</w:t><w:br/><w:t xml:space=\"preserve\">");
        }

        private static string Esc(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                
                if (c == '\r' || c == '\uFFFE' || c == '\uFFFF') continue;
                if (c < 0x20 && c != '\t' && c != '\n') { sb.Append(' '); continue; }
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        
        
        

        private const string JenisKonten =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n"
          + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">\n"
          + "  <Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>\n"
          + "  <Default Extension=\"xml\" ContentType=\"application/xml\"/>\n"
          + "  <Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>\n"
          + "  <Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/>\n"
          + "</Types>\n";

        private const string RelAkar =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n"
          + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\n"
          + "  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>\n"
          + "</Relationships>\n";

        private const string RelDokumen =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n"
          + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">\n"
          + "  <Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>\n"
          + "</Relationships>\n";

        
        
        
        private const string Gaya =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n"
          + "<w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">\n"
          + "  <w:docDefaults>\n"
          + "    <w:rPrDefault><w:rPr>\n"
          + "      <w:rFonts w:ascii=\"Calibri\" w:hAnsi=\"Calibri\" w:eastAsia=\"Calibri\" w:cs=\"Calibri\"/>\n"
          + "      <w:sz w:val=\"21\"/><w:szCs w:val=\"21\"/>\n"
          + "      <w:lang w:val=\"id-ID\" w:eastAsia=\"id-ID\" w:bidi=\"id-ID\"/>\n"
          + "    </w:rPr></w:rPrDefault>\n"
          + "    <w:pPrDefault><w:pPr>\n"
          + "      <w:spacing w:after=\"120\" w:line=\"276\" w:lineRule=\"auto\"/>\n"
          + "    </w:pPr></w:pPrDefault>\n"
          + "  </w:docDefaults>\n"
          + "  <w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\">\n"
          + "    <w:name w:val=\"Normal\"/><w:qFormat/>\n"
          + "  </w:style>\n"
          + GayaJudul
          + "  <w:style w:type=\"paragraph\" w:styleId=\"Catatan\">\n"
          + "    <w:name w:val=\"Catatan\"/><w:basedOn w:val=\"Normal\"/><w:qFormat/>\n"
          + "    <w:pPr><w:spacing w:after=\"120\"/></w:pPr>\n"
          + "    <w:rPr><w:i/><w:color w:val=\"5A6478\"/><w:sz w:val=\"19\"/></w:rPr>\n"
          + "  </w:style>\n"
          + "  <w:style w:type=\"paragraph\" w:styleId=\"CatatanKaki\">\n"
          + "    <w:name w:val=\"Catatan kaki tabel\"/><w:basedOn w:val=\"Normal\"/><w:qFormat/>\n"
          + "    <w:pPr><w:spacing w:before=\"0\" w:after=\"160\"/></w:pPr>\n"
          + "    <w:rPr><w:i/><w:color w:val=\"5A6478\"/><w:sz w:val=\"18\"/></w:rPr>\n"
          + "  </w:style>\n"
          + "  <w:style w:type=\"paragraph\" w:styleId=\"JudulTabel\">\n"
          + "    <w:name w:val=\"Judul tabel\"/><w:basedOn w:val=\"Normal\"/><w:qFormat/>\n"
          + "    <w:pPr><w:keepNext/><w:spacing w:before=\"200\" w:after=\"80\"/></w:pPr>\n"
          + "    <w:rPr><w:b/><w:color w:val=\"16213A\"/><w:sz w:val=\"20\"/></w:rPr>\n"
          + "  </w:style>\n"
          + "  <w:style w:type=\"table\" w:styleId=\"TableGrid\">\n"
          + "    <w:name w:val=\"Table Grid\"/>\n"
          + "    <w:tblPr>\n"
          + "      <w:tblBorders>\n"
          + "        <w:top w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B7C0D0\"/>\n"
          + "        <w:left w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B7C0D0\"/>\n"
          + "        <w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B7C0D0\"/>\n"
          + "        <w:right w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B7C0D0\"/>\n"
          + "        <w:insideH w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B7C0D0\"/>\n"
          + "        <w:insideV w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"B7C0D0\"/>\n"
          + "      </w:tblBorders>\n"
          + "    </w:tblPr>\n"
          + "  </w:style>\n"
          + "</w:styles>\n";

        private const string GayaJudul =
            "  <w:style w:type=\"paragraph\" w:styleId=\"Heading1\">\n"
          + "    <w:name w:val=\"heading 1\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/>\n"
          + "    <w:pPr><w:keepNext/><w:spacing w:before=\"0\" w:after=\"80\"/><w:outlineLvl w:val=\"0\"/></w:pPr>\n"
          + "    <w:rPr><w:b/><w:color w:val=\"16213A\"/><w:sz w:val=\"34\"/></w:rPr>\n"
          + "  </w:style>\n"
          + "  <w:style w:type=\"paragraph\" w:styleId=\"Heading2\">\n"
          + "    <w:name w:val=\"heading 2\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/>\n"
          + "    <w:pPr><w:keepNext/><w:spacing w:before=\"280\" w:after=\"120\"/><w:outlineLvl w:val=\"1\"/></w:pPr>\n"
          + "    <w:rPr><w:b/><w:color w:val=\"0F5C8A\"/><w:sz w:val=\"27\"/></w:rPr>\n"
          + "  </w:style>\n"
          + "  <w:style w:type=\"paragraph\" w:styleId=\"Heading3\">\n"
          + "    <w:name w:val=\"heading 3\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/>\n"
          + "    <w:pPr><w:keepNext/><w:spacing w:before=\"220\" w:after=\"100\"/><w:outlineLvl w:val=\"2\"/></w:pPr>\n"
          + "    <w:rPr><w:b/><w:color w:val=\"16213A\"/><w:sz w:val=\"23\"/></w:rPr>\n"
          + "  </w:style>\n"
          + "  <w:style w:type=\"paragraph\" w:styleId=\"Heading4\">\n"
          + "    <w:name w:val=\"heading 4\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/>\n"
          + "    <w:pPr><w:keepNext/><w:spacing w:before=\"180\" w:after=\"80\"/><w:outlineLvl w:val=\"3\"/></w:pPr>\n"
          + "    <w:rPr><w:b/><w:color w:val=\"5A6478\"/><w:sz w:val=\"21\"/></w:rPr>\n"
          + "  </w:style>\n";
    }
}
