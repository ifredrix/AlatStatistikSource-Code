using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace AlatStatistik.Models
{

    public enum Measure
    {
        Skala,      
        Ordinal,    
        Nominal     
    }

    public class Variable : System.ComponentModel.INotifyPropertyChanged
    {
        private string _name = "";
        private string _label = "";
        private Measure _measure = Measure.Skala;
        private int _decimals = 2;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void Ubah<T>(ref T simpan, T nilai, [System.Runtime.CompilerServices.CallerMemberName] string? properti = null)
        {
            if (EqualityComparer<T>.Default.Equals(simpan, nilai)) return;
            simpan = nilai;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(properti));
        }

        public string Name { get => _name; set => Ubah(ref _name, value); }
        public string Label { get => _label; set => Ubah(ref _label, value); }
        public Measure Measure { get => _measure; set => Ubah(ref _measure, value); }
        public int Decimals { get => _decimals; set => Ubah(ref _decimals, value); }

        public Dictionary<double, string> ValueLabels { get; } = new();

        public Dictionary<string, string> ValueLabelsTeks { get; } = new();

        public bool IsNumeric => Measure == Measure.Skala;

        public override string ToString() => string.IsNullOrWhiteSpace(Label) ? Name : $"{Label} [{Name}]";
    }

    public class Dataset
    {
        public List<Variable> Variables { get; } = new();
        public List<string?[]> Rows { get; } = new();
        public string Name { get; set; } = "Data 1";
        public string? SourcePath { get; set; }

        public List<DataChange> Riwayat { get; } = new();

        public int RowCount => Rows.Count;
        public int ColumnCount => Variables.Count;
        public bool IsEmpty => Variables.Count == 0;

        // Tab Variabel bisa menyisipkan Variable tanpa lewat TambahVariabel(),
        // sehingga baris lebih pendek dari jumlah kolom. True bila ada yang diubah.
        public bool SinkronkanStruktur()
        {
            bool berubah = false;

            for (int r = 0; r < Rows.Count; r++)
            {
                if (Rows[r].Length == Variables.Count) continue;

                var rapih = new string?[Variables.Count];
                Array.Copy(Rows[r], rapih, Math.Min(Rows[r].Length, Variables.Count));
                Rows[r] = rapih;
                berubah = true;
            }

            var dipakai = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < Variables.Count; c++)
            {
                string nama = (Variables[c].Name ?? "").Trim();

                if (nama.Length == 0 || !dipakai.Add(nama))
                {
                    nama = NamaVariabelUnik(nama.Length == 0 ? "V" + (c + 1) : nama);
                    Variables[c].Name = nama;
                    dipakai.Add(nama);
                    berubah = true;
                }
            }

            return berubah;
        }

        // Baca sel aman: di luar jangkauan berarti kosong, bukan pengecualian.
        // Baris bisa lebih pendek dari jumlah kolom bila impor tidak memanggil
        // SinkronkanStruktur(), jadi semua pembacaan sel sebaiknya lewat sini.
        public string? Ambil(int baris, int kolom)
        {
            if (baris < 0 || baris >= Rows.Count) return null;

            string?[] r = Rows[baris];
            return kolom >= 0 && kolom < r.Length ? r[kolom] : null;
        }



        public Variable? VariableByName(string name)
            => Variables.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

        public int IndexOf(string name)
            => Variables.FindIndex(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

        public List<string> Names => Variables.Select(v => v.Name).ToList();

        public List<string> NumericColumns()
            => Variables.Where(v => v.Measure == Measure.Skala).Select(v => v.Name).ToList();

        public List<string> CategoricalColumns()
            => Variables.Where(v => v.Measure != Measure.Skala).Select(v => v.Name).ToList();

        public double?[] Numeric(string name)
        {
            int i = IndexOf(name);
            if (i < 0) throw new ArgumentException($"Variabel '{name}' tidak ada.", nameof(name));

            var result = new double?[Rows.Count];
            for (int r = 0; r < Rows.Count; r++)
                result[r] = ToDouble(Ambil(r, i));
            return result;
        }

        public string?[] Text(string name)
        {
            int i = IndexOf(name);
            if (i < 0) throw new ArgumentException($"Variabel '{name}' tidak ada.", nameof(name));

            var hasil = new string?[Rows.Count];
            for (int r = 0; r < Rows.Count; r++) hasil[r] = Ambil(r, i);
            return hasil;
        }

        public List<string> Levels(string name)
        {
            var seen = new List<string>();
            foreach (string? value in Text(name))
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (!seen.Contains(value)) seen.Add(value);
            }
            return seen;
        }

        public static Dataset Baru(int baris = 20, int kolom = 3)
        {
            var ds = new Dataset { Name = "Data baru" };
            for (int j = 1; j <= Math.Max(1, kolom); j++)
                ds.Variables.Add(new Variable { Name = "V" + j });

            for (int i = 0; i < Math.Max(1, baris); i++)
                ds.Rows.Add(new string?[ds.Variables.Count]);

            return ds;
        }

        public void TambahBaris(int jumlah = 1)
        {
            SinkronkanStruktur();
            for (int i = 0; i < Math.Max(1, jumlah); i++)
                Rows.Add(new string?[Variables.Count]);
        }

        public Variable TambahVariabel(string nama, Measure ukur = Measure.Skala)
        {
            string namaUnik = NamaVariabelUnik(nama);
            var v = new Variable { Name = namaUnik, Measure = ukur };
            Variables.Add(v);

            
            
            for (int r = 0; r < Rows.Count; r++)
            {
                if (Rows[r].Length < Variables.Count)
                {
                    var diperpanjang = new string?[Variables.Count];
                    Array.Copy(Rows[r], diperpanjang, Rows[r].Length);
                    Rows[r] = diperpanjang;
                }
            }
            return v;
        }

        public bool HapusVariabel(string nama)
        {
            int i = IndexOf(nama);
            if (i < 0 || Variables.Count <= 1) return false;

            Variables.RemoveAt(i);
            for (int r = 0; r < Rows.Count; r++)
            {
                var sisa = new string?[Variables.Count];
                for (int c = 0, t = 0; c < Rows[r].Length; c++)
                {
                    if (c == i) continue;
                    if (t < sisa.Length) sisa[t++] = Rows[r][c];
                }
                Rows[r] = sisa;
            }
            return true;
        }

        public string NamaVariabelUnik(string nama)
        {
            string dasar = string.IsNullOrWhiteSpace(nama) ? "V" : nama.Trim();
            if (IndexOf(dasar) < 0) return dasar;

            int nomor = 2;
            while (IndexOf(dasar + "_" + nomor) >= 0) nomor++;
            return dasar + "_" + nomor;
        }

        public DataChange? SetCell(int rowIndex, string columnName, string? newValue)
        {
            int col = IndexOf(columnName);
            if (col < 0) throw new ArgumentException($"Variabel '{columnName}' tidak ada.", nameof(columnName));
            if (rowIndex < 0 || rowIndex >= Rows.Count)
                throw new ArgumentOutOfRangeException(nameof(rowIndex));

            // Panjangkan dulu bila barisnya lebih pendek dari jumlah variabel.
            if (Rows[rowIndex].Length < Variables.Count)
            {
                var rapih = new string?[Variables.Count];
                Array.Copy(Rows[rowIndex], rapih, Rows[rowIndex].Length);
                Rows[rowIndex] = rapih;
            }

            string? oldValue = Rows[rowIndex][col];
            if (string.Equals(oldValue ?? "", newValue ?? "", StringComparison.Ordinal)) return null;

            Rows[rowIndex][col] = newValue;

            var entri = new DataChange
            {
                Baris = rowIndex,
                Kolom = columnName,
                NilaiLama = oldValue,
                NilaiBaru = newValue,
                Waktu = DateTime.Now
            };
            Riwayat.Add(entri);
            return entri;
        }

        public DataChange? GantiNama(string namaLama, string namaBaru)
        {
            int i = IndexOf(namaLama);
            if (i < 0) return null;

            string bersih = (namaBaru ?? "").Trim();
            if (bersih.Length == 0) return null;

            var v = Variables[i];
            if (string.Equals(v.Name, bersih, StringComparison.Ordinal)) return null;

            int bentrok = IndexOf(bersih);
            if (bentrok >= 0 && bentrok != i) return null;

            string sebelum = v.Name;
            v.Name = bersih;

            var entri = new DataChange
            {
                Baris = -1,
                Kolom = v.Name,          
                NilaiLama = sebelum,
                NilaiBaru = v.Name,
                Waktu = DateTime.Now
            };
            Riwayat.Add(entri);
            return entri;
        }

        public DataChange? UndoLast()
        {
            if (Riwayat.Count == 0) return null;
            var terakhir = Riwayat[^1];

            
            
            
            
            
            
            
            if (terakhir.Struktural) return null;

            Riwayat.RemoveAt(Riwayat.Count - 1);

            int col = IndexOf(terakhir.Kolom);
            if (col < 0) return terakhir;

            if (terakhir.Baris >= 0)
            {
                
                if (terakhir.Baris < Rows.Count)
                {
                    string?[] baris = Rows[terakhir.Baris];
                    if (col >= baris.Length)
                    {
                        var rapih = new string?[Variables.Count];
                        Array.Copy(baris, rapih, baris.Length);
                        baris = rapih;
                        Rows[terakhir.Baris] = baris;
                    }
                    baris[col] = terakhir.NilaiLama;
                }
            }
            else
            {
                
                string? namaLama = terakhir.NilaiLama;
                if (!string.IsNullOrWhiteSpace(namaLama) && IndexOf(namaLama!) < 0)
                    Variables[col].Name = namaLama!;
            }

            return terakhir;
        }

        public List<int> CompleteRows(IEnumerable<string> names)
        {
            var idx = names.Select(IndexOf).ToList();
            var keep = new List<int>();
            for (int r = 0; r < Rows.Count; r++)
            {
                bool ok = true;
                foreach (int i in idx)
                {
                    if (i < 0 || string.IsNullOrWhiteSpace(Ambil(r, i))) { ok = false; break; }
                }
                if (ok) keep.Add(r);
            }
            return keep;
        }

        public static double? ToDouble(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Trim();

            
            if (s.Contains(',') && !s.Contains('.'))
                s = s.Replace(",", ".");
            else if (s.Contains(',') && s.Contains('.'))
                s = s.Contains('.') && s.LastIndexOf('.') > s.LastIndexOf(',')
                    ? s.Replace(",", "")          
                    : s.Replace(".", "").Replace(",", "."); 
            else
                s = s.Replace(" ", "");

            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                return null;

            // TryParse menerima "NaN", "Infinity", dan "1e999". Semua itu bukan
            // angka yang bisa dihitung: NaN meracuni setiap rumus downstream
            // tanpa pernah memunculkan galat. Anggap sama dengan sel kosong.
            return double.IsFinite(v) ? v : (double?)null;
        }

        

        public DataTable ToDataTable()
        {
            SinkronkanStruktur();

            var table = new DataTable(string.IsNullOrWhiteSpace(Name) ? "Data" : Name);
            var sudah = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // DataColumn melempar DuplicateNameException bila nama kosong/kembar.
            foreach (var v in Variables)
            {
                string nama = (v.Name ?? "").Trim();
                if (nama.Length == 0 || !sudah.Add(nama))
                {
                    nama = NamaVariabelUnik(nama.Length == 0 ? "V" + (table.Columns.Count + 1) : nama);
                    sudah.Add(nama);
                }

                table.Columns.Add(new DataColumn(nama, typeof(string)) { Caption = v.Label });
            }

            for (int r = 0; r < Rows.Count; r++)
            {
                var dr = table.NewRow();
                for (int i = 0; i < table.Columns.Count; i++)
                    dr[i] = Ambil(r, i) ?? "";
                table.Rows.Add(dr);
            }
            return table;
        }

        public void FromDataTable(DataTable? table)
        {
            Rows.Clear();
            if (table == null) return;

            foreach (DataRow dr in table.Rows)
            {
                var values = new string?[Variables.Count];
                for (int i = 0; i < Variables.Count; i++)
                {
                    object? cell = i < table.Columns.Count ? dr[i] : null;
                    string? text = cell is DBNull ? null : Convert.ToString(cell, CultureInfo.InvariantCulture);
                    values[i] = string.IsNullOrWhiteSpace(text) ? null : text;
                }
                Rows.Add(values);
            }
        }

        public Dataset Clone()
        {
            var copy = new Dataset { Name = Name, SourcePath = SourcePath };
            foreach (var v in Variables)
            {
                var nv = new Variable
                {
                    Name = v.Name, Label = v.Label, Measure = v.Measure, Decimals = v.Decimals
                };
                foreach (var kv in v.ValueLabels) nv.ValueLabels[kv.Key] = kv.Value;
                foreach (var kv in v.ValueLabelsTeks) nv.ValueLabelsTeks[kv.Key] = kv.Value;
                copy.Variables.Add(nv);
            }
            foreach (var row in Rows) copy.Rows.Add((string?[])row.Clone());
            return copy;
        }
    }
}
