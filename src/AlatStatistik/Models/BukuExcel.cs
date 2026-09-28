using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace AlatStatistik.Models
{

    public static class BukuExcel
    {
        private static readonly XNamespace NS =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace NS_REL =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace NS_PKG =
            "http://schemas.openxmlformats.org/package/2006/relationships";

        public static bool TampaknyaXlsx(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                var kepala = new byte[4];
                if (fs.Read(kepala, 0, 4) != 4) return false;
                if (kepala[0] != 'P' || kepala[1] != 'K' || kepala[2] != 3 || kepala[3] != 4)
                    return false;
            }
            catch { return false; }

            try
            {
                using var zip = ZipFile.OpenRead(path);
                return zip.Entries.Any(e => e.FullName.EndsWith("workbook.xml",
                    StringComparison.OrdinalIgnoreCase));
            }
            catch { return false; }
        }

        public static List<string> DaftarSheet(string path)
        {
            using var zip = ZipFile.OpenRead(path);
            var wb = BukaX(zip, "xl/workbook.xml")
                     ?? throw new InvalidDataException("Berkas tidak punya xl/workbook.xml.");
            return wb.Descendants(NS + "sheet")
                     .Select(s => (string?)s.Attribute("name") ?? "")
                     .ToList();
        }

        public static Dataset Baca(string path, string? namaSheet = null)
        {
            using var zip = ZipFile.OpenRead(path);

            var wb = BukaX(zip, "xl/workbook.xml")
                     ?? throw new InvalidDataException("Berkas tidak punya xl/workbook.xml.");

            
            var sheetEl = wb.Descendants(NS + "sheet").FirstOrDefault();
            if (!string.IsNullOrEmpty(namaSheet))
            {
                sheetEl = wb.Descendants(NS + "sheet")
                            .FirstOrDefault(s => string.Equals((string?)s.Attribute("name"),
                                                namaSheet, StringComparison.OrdinalIgnoreCase))
                          ?? throw new InvalidDataException(
                                 $"Lembar '{namaSheet}' tidak ada di berkas ini.");
            }
            if (sheetEl is null)
                throw new InvalidDataException("Berkas tidak punya lembar kerja.");

            string rId = (string?)sheetEl.Attribute(NS_REL + "id") ?? "";
            string jalurSheet = JalurLembar(zip, rId);

            
            var shared = BacaStringBersama(zip);
            var gayaTanggal = BacaGayaTanggal(zip);

            
            var sh = BukaX(zip, jalurSheet)
                     ?? throw new InvalidDataException($"Lembar '{jalurSheet}' tidak ditemukan.");

            var baris = new List<string?[]>();
            int kolomTerbesar = 0;

            var semuaBaris = sh.Descendants(NS + "row")
                               .OrderBy(r => NomorBaris(r))
                               .ToList();

            foreach (var row in semuaBaris)
            {
                var sel = new SortedDictionary<int, string?>();
                foreach (var c in row.Elements(NS + "c"))
                {
                    int kol = IndeksKolom((string?)c.Attribute("r"));
                    if (kol < 0) continue;
                    sel[kol] = NilaiSel(c, shared, gayaTanggal);
                }
                if (sel.Count == 0) { baris.Add(Array.Empty<string?>()); continue; }

                int maks = sel.Keys.Max();
                var isi = new string?[maks + 1];
                foreach (var kv in sel) isi[kv.Key] = kv.Value;
                kolomTerbesar = Math.Max(kolomTerbesar, maks + 1);
                baris.Add(isi);
            }

            
            while (baris.Count > 0 && baris[^1].Length == 0) baris.RemoveAt(baris.Count - 1);
            if (baris.Count == 0)
                throw new InvalidDataException("Lembar kerja ini kosong.");

            
            var ds = new Dataset
            {
                Name = Path.GetFileNameWithoutExtension(path),
                SourcePath = path
            };

            var kepala = baris[0];
            int jumlah = Math.Max(kolomTerbesar, kepala.Length);
            var dipakai = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < jumlah; i++)
            {
                string nama = i < kepala.Length ? (kepala[i] ?? "").Trim() : "";
                if (string.IsNullOrEmpty(nama)) nama = $"Kolom{i + 1}";
                string calon = nama;
                int k = 1;
                while (!dipakai.Add(calon)) calon = $"{nama}_{++k}";
                ds.Variables.Add(new Variable { Name = calon, Measure = Measure.Skala });
            }

            for (int r = 1; r < baris.Count; r++)
            {
                var src = baris[r];
                var row = new string?[jumlah];
                for (int c = 0; c < jumlah; c++)
                {
                    string? v = c < src.Length ? src[c] : null;
                    row[c] = string.IsNullOrEmpty(v) ? null : v;
                }
                ds.Rows.Add(row);
            }

            DataImport.InferMeasures(ds);
            DataImport.MuatLabel(ds, path);
            return ds;
        }

        public static void Tulis(Dataset ds, string path)
        {
            var lembar = new StringBuilder();
            lembar.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            lembar.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            lembar.Append("<sheetData>");

            
            lembar.Append("<row r=\"1\">");
            for (int c = 0; c < ds.Variables.Count; c++)
                lembar.Append(Sel(1, c, ds.Variables[c].Name));
            lembar.Append("</row>");

            for (int r = 0; r < ds.Rows.Count; r++)
            {
                lembar.Append($"<row r=\"{r + 2}\">");
                var row = ds.Rows[r];
                for (int c = 0; c < ds.Variables.Count; c++)
                    lembar.Append(Sel(r + 2, c, c < row.Length ? row[c] : null));
                lembar.Append("</row>");
            }

            lembar.Append("</sheetData></worksheet>");

            if (File.Exists(path)) File.Delete(path);
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                TulisEntri(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                    + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                    + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                    + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                    + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                    + "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
                    + "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"
                    + "</Types>");

                TulisEntri(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                    + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                    + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
                    + "</Relationships>");

                TulisEntri(zip, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                    + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" "
                    + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
                    + "<sheets><sheet name=\"Data\" sheetId=\"1\" r:id=\"rId1\"/></sheets>"
                    + "</workbook>");

                TulisEntri(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                    + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                    + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>"
                    + "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>"
                    + "</Relationships>");

                TulisEntri(zip, "xl/styles.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                    + "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                    + "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>"
                    + "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>"
                    + "<borders count=\"1\"><border/></borders>"
                    + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
                    + "<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>"
                    
                    
                    
                    
                    + "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>"
                    + "<dxfs count=\"0\"/>"
                    + "<tableStyles count=\"0\" defaultTableStyle=\"TableStyleMedium2\" defaultPivotStyle=\"PivotStyleLight16\"/>"
                    + "</styleSheet>");

                TulisEntri(zip, "xl/worksheets/sheet1.xml", lembar.ToString());
            }

            
            DataImport.SimpanLabel(ds, path);
        }

        

        private static XDocument? BukaX(ZipArchive zip, string nama)
        {
            var entri = zip.GetEntry(nama)
                        ?? zip.Entries.FirstOrDefault(e =>
                               string.Equals(e.FullName, nama, StringComparison.OrdinalIgnoreCase));
            if (entri is null) return null;
            using var s = entri.Open();
            return XDocument.Load(s);
        }

        private static string JalurLembar(ZipArchive zip, string rId)
        {
            var rels = BukaX(zip, "xl/_rels/workbook.xml.rels");
            if (rels is not null)
            {
                var cocok = rels.Descendants(NS_PKG + "Relationship")
                                .FirstOrDefault(r => (string?)r.Attribute("Id") == rId);
                if (cocok is not null)
                {
                    string target = (string?)cocok.Attribute("Target") ?? "";
                    target = target.Replace('\\', '/');
                    if (target.StartsWith("/")) return target.TrimStart('/');
                    return "xl/" + target;
                }
            }
            return "xl/worksheets/sheet1.xml";
        }

        private static List<string> BacaStringBersama(ZipArchive zip)
        {
            var hasil = new List<string>();
            var doc = BukaX(zip, "xl/sharedStrings.xml");
            if (doc is null) return hasil;
            foreach (var si in doc.Descendants(NS + "si"))
                hasil.Add(string.Concat(si.Descendants(NS + "t").Select(t => t.Value)));
            return hasil;
        }

        private static HashSet<int> BacaGayaTanggal(ZipArchive zip)
        {
            var hasil = new HashSet<int>();
            var doc = BukaX(zip, "xl/styles.xml");
            if (doc is null) return hasil;

            
            
            static bool Bawaan(int id)
                => (id >= 14 && id <= 22) || (id >= 45 && id <= 47)
                   || (id >= 27 && id <= 36) || (id >= 50 && id <= 58);

            
            var buatan = new Dictionary<int, string>();
            foreach (var nf in doc.Descendants(NS + "numFmt"))
            {
                if (int.TryParse((string?)nf.Attribute("numFmtId"), out int id)
                    && id >= 164)
                    buatan[id] = (string?)nf.Attribute("formatCode") ?? "";
            }

            var xfs = doc.Descendants(NS + "cellXfs")
                         .Elements(NS + "xf")
                         .ToList();
            for (int i = 0; i < xfs.Count; i++)
            {
                if (!int.TryParse((string?)xfs[i].Attribute("numFmtId"), out int fmt)) continue;
                if (Bawaan(fmt)) { hasil.Add(i); continue; }
                if (buatan.TryGetValue(fmt, out string? kode) && BerartiTanggal(kode))
                    hasil.Add(i);
            }
            return hasil;
        }

        private static bool BerartiTanggal(string kode)
        {
            var bersih = new StringBuilder();
            bool dalamKutip = false;
            bool dalamKurung = false;
            foreach (char ch in kode)
            {
                if (ch == '"') { dalamKutip = !dalamKutip; continue; }
                if (dalamKutip) continue;
                if (ch == '[') { dalamKurung = true; continue; }
                if (ch == ']') { dalamKurung = false; continue; }
                if (dalamKurung) continue;
                bersih.Append(char.ToLowerInvariant(ch));
            }
            string k = bersih.ToString();
            return k.Contains('y') || k.Contains('m') || k.Contains('d')
                || k.Contains('h') || k.Contains('s');
        }

        private static int NomorBaris(XElement baris)
            => int.TryParse((string?)baris.Attribute("r"), out int n) ? n : 0;

        private static int IndeksKolom(string? r)
        {
            if (string.IsNullOrEmpty(r)) return -1;
            int kol = 0, i = 0;
            while (i < r.Length && char.IsLetter(r[i]))
            {
                kol = kol * 26 + (char.ToUpperInvariant(r[i]) - 'A' + 1);
                i++;
            }
            return i == 0 ? -1 : kol - 1;
        }

        private static string? NilaiSel(
            XElement c, List<string> shared, HashSet<int> gayaTanggal)
        {
            string? t = (string?)c.Attribute("t");
            string? v = c.Element(NS + "v")?.Value;
            if (t == "s")
            {
                if (int.TryParse(v, out int idx) && idx >= 0 && idx < shared.Count)
                    return shared[idx];
                return null;
            }
            if (t == "inlineStr")
                return string.Concat(c.Descendants(NS + "t").Select(x => x.Value));
            if (t == "b")
                return v == "1" ? "1" : "0";
            if (t == "e")
                return null;
            if (t == "str")
                return v;
            if (v is null)
                return null;
            if (int.TryParse((string?)c.Attribute("s"), out int gaya)
                && gayaTanggal.Contains(gaya)
                && double.TryParse(v, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double serial))
            {
                var basis = new DateTime(1899, 12, 30);
                var tanggal = basis.AddDays(serial);
                if (serial % 1 == 0)
                    return tanggal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return tanggal.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
            return v;
        }

        private static string Sel(int baris, int kolom, string? nilai)
        {
            string r = NamaKolom(kolom) + baris;
            double? angka = Dataset.ToDouble(nilai);
            if (angka.HasValue)
            {
                double a = angka.Value;
                if (double.IsNaN(a) || double.IsInfinity(a))
                    return $"<c r=\"{r}\" t=\"inlineStr\"><is><t>{EscapeXml(nilai ?? "")}</t></is></c>";
                return $"<c r=\"{r}\"><v>{a.ToString("R", CultureInfo.InvariantCulture)}</v></c>";
            }
            if (string.IsNullOrEmpty(nilai)) return "";
            return $"<c r=\"{r}\" t=\"inlineStr\"><is><t>{EscapeXml(nilai)}</t></is></c>";
        }
        public static string NamaKolom(int indeks)
        {
            var hasil = new StringBuilder();
            int n = indeks + 1;
            while (n > 0)
            {
                int sisa = (n - 1) % 26;
                hasil.Insert(0, (char)('A' + sisa));
                n = (n - 1) / 26;
            }
            return hasil.ToString();
        }

        private static string EscapeXml(string s)
            => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("'", "&apos;");

        private static void TulisEntri(ZipArchive zip, string nama, string isi)
        {
            var entri = zip.CreateEntry(nama);
            using var s = entri.Open();
            using var w = new StreamWriter(s, new UTF8Encoding(false));
            w.Write(isi);
        }
    }
}
