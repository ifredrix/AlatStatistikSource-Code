using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AlatStatistik.Models
{
    public static class DataImport
    {
        public const string FilterBuka =
            "Data (*.csv;*.txt;*.tsv;*.sav;*.xlsx)|*.csv;*.txt;*.tsv;*.sav;*.xlsx";
        public const string FilterSimpan =
            "Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv";

        public static Dataset Load(string path)
            => PembacaSav.TampaknyaSav(path) ? PembacaSav.Baca(path) : LoadCsv(path);

        public static void Simpan(Dataset ds, string path)
        {
            if (path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                BukuExcel.Tulis(ds, path);
            else
                SaveCsv(ds, path);
        }

        public static Dataset LoadCsv(string path)
        {
            var lines = File.ReadAllLines(path)
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToList();
            if (lines.Count == 0)
                throw new InvalidDataException("Berkas tidak berisi apa pun.");

            char sep = DetectSeparator(lines);
            var header = Split(lines[0], sep);

            var ds = new Dataset
            {
                Name = Path.GetFileNameWithoutExtension(path),
                SourcePath = path
            };

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Count; i++)
            {
                string name = header[i].Trim();
                if (string.IsNullOrEmpty(name)) name = $"Var{i + 1}";
                string candidate = name;
                int k = 1;
                while (!used.Add(candidate)) candidate = $"{name}_{++k}";
                ds.Variables.Add(new Variable { Name = candidate, Measure = Measure.Skala });
            }

            for (int r = 1; r < lines.Count; r++)
            {
                var cells = Split(lines[r], sep);
                var row = new string?[ds.Variables.Count];
                for (int c = 0; c < ds.Variables.Count; c++)
                {
                    string? value = c < cells.Count ? cells[c].Trim() : null;
                    row[c] = string.IsNullOrEmpty(value) ? null : value;
                }
                ds.Rows.Add(row);
            }

            InferMeasures(ds);
            MuatLabel(ds, path);
            return ds;
        }

        // label variabel

        public static string JalurLabel(string path)
            => Path.ChangeExtension(path, null) + ".labels.json";

        public static void MuatLabel(Dataset ds, string path)
        {
            string jalur = JalurLabel(path);
            if (!File.Exists(jalur)) return;

            // Isinya DISALIN ke kamus di dalam blok `using`, bukan dibaca
            // setelahnya: `JsonElement` hanya menunjuk ke dalam `JsonDocument`,
            // jadi memakainya setelah dokumennya dibuang melempar
            var label = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var dok = System.Text.Json.JsonDocument.Parse(File.ReadAllText(jalur));
                if (!dok.RootElement.TryGetProperty("kolom", out var kolom)
                    || kolom.ValueKind != System.Text.Json.JsonValueKind.Object)
                    throw new InvalidDataException("tidak ada bagian \"kolom\"");

                foreach (var isi in kolom.EnumerateObject())
                    if (isi.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                        label[isi.Name] = isi.Value.GetString() ?? "";
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    $"Berkas label '{jalur}' tidak bisa dibaca ({ex.Message}). "
                    + "Label variabelnya tidak bisa dipakai. Hapus berkas itu kalau "
                    + "labelnya memang tidak diperlukan lagi, lalu buka datanya kembali.", ex);
            }

            foreach (var v in ds.Variables)
                if (label.TryGetValue(v.Name, out string? teks))
                    v.Label = teks;
        }

        public static bool SimpanLabel(Dataset ds, string path)
        {
            string jalur = JalurLabel(path);
            bool adaLabel = ds.Variables.Any(v => !string.IsNullOrWhiteSpace(v.Label));
            if (!adaLabel && !File.Exists(jalur)) return false;

            var kolom = new Dictionary<string, string>();
            foreach (var v in ds.Variables)
                if (!string.IsNullOrWhiteSpace(v.Label)) kolom[v.Name] = v.Label;

            var isi = new System.Text.Json.Nodes.JsonObject
            {
                ["catatan"] = "Label variabel untuk " + Path.GetFileName(path)
                              + " — ditulis AlatStatistik. Hapus berkas ini kalau labelnya tidak diperlukan.",
                ["kolom"] = new System.Text.Json.Nodes.JsonObject(
                    kolom.Select(kv => new KeyValuePair<string, System.Text.Json.Nodes.JsonNode?>(
                        kv.Key, System.Text.Json.Nodes.JsonValue.Create(kv.Value))))
            };

            string teks = isi.ToJsonString(new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            try
            {
                File.WriteAllText(jalur, teks, new System.Text.UTF8Encoding(false));
                PesanLabelTerakhir = null;
                return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException
                                        || ex is IOException
                                        || ex is NotSupportedException
                                        || ex is System.Security.SecurityException)
            {
                
                
                
                
                
                string? cadangan = SimpanLabelCadangan(ds, path, teks);
                PesanLabelTerakhir = cadangan == null
                    ? "Folder asal data tidak bisa ditulis, jadi label variabel tidak tersimpan.\n\n"
                      + $"Alasan: {ex.Message}\n\n"
                      + "Simpan datanya ke folder Anda sendiri (Dokumen, misalnya) supaya label ikut tersimpan."
                    : "Folder asal data tidak bisa ditulis (mis. Program Files atau folder milik akun lain).\n\n"
                      + $"Label variabel disimpan sebagai gantinya di:\n{cadangan}";
                return false;
            }
        }

        
        
        
        
        
        public static string? PesanLabelTerakhir { get; set; }

        
        
        
        
        private static string? SimpanLabelCadangan(Dataset ds, string path, string teks)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AlatStatistik", "labels");

                if (string.IsNullOrEmpty(folder)) return null;

                Directory.CreateDirectory(folder);

                string nama = Path.GetFileNameWithoutExtension(path);
                foreach (char c in Path.GetInvalidFileNameChars()) nama = nama.Replace(c, '_');
                if (nama.Length == 0) nama = "data";

                string berkas = Path.Combine(folder, nama + ".labels.json");
                File.WriteAllText(berkas, teks, new System.Text.UTF8Encoding(false));
                return berkas;
            }
            catch
            {
                return null;
            }
        }

        public static void InferMeasures(Dataset ds)
        {
            foreach (var v in ds.Variables)
            {
                int index = ds.Variables.IndexOf(v);
                int terisi = 0, angka = 0;
                foreach (var row in ds.Rows)
                {
                    string? nilai = row[index];
                    if (string.IsNullOrWhiteSpace(nilai)) continue;
                    terisi++;
                    if (Dataset.ToDouble(nilai).HasValue) angka++;
                }

                if (terisi == 0) continue;      // tidak ada bukti: pertahankan
                v.Measure = angka == terisi ? Measure.Skala : Measure.Nominal;
            }
        }

        public static void SaveCsv(Dataset ds, string path)
        {
            using (var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8))
            {
                writer.WriteLine(string.Join(",", ds.Variables.Select(v => Quote(v.Name))));
                foreach (var row in ds.Rows)
                    writer.WriteLine(string.Join(",", row.Select(c => Quote(c ?? ""))));
            }

            // Label variabel tidak punya tempat di dalam CSV. Ditulis ke
            // berkas pendamping supaya tidak hilang tanpa kabar.
            SimpanLabel(ds, path);
        }

        private static string Quote(string s)
            => s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

        private static char DetectSeparator(List<string> lines)
        {
            char tebakan = ',';
            int terbanyak = 0;
            foreach (char sep in new[] { ',', ';', '\t', '|' })
            {
                int jumlah = Split(lines[0], sep).Count;
                if (jumlah > terbanyak) { terbanyak = jumlah; tebakan = sep; }
            }
            return tebakan;
        }

        private static List<string> Split(string line, char sep)
        {
            var hasil = new List<string>();
            var current = new System.Text.StringBuilder();
            bool dalamKutip = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (dalamKutip && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else dalamKutip = !dalamKutip;
                }
                else if (ch == sep && !dalamKutip)
                {
                    hasil.Add(current.ToString());
                    current.Clear();
                }
                else current.Append(ch);
            }
            hasil.Add(current.ToString());
            return hasil;
        }
    }
}
