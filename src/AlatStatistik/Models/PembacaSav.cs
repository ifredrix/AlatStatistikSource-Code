using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AlatStatistik.Models
{

    public static class PembacaSav
    {

        private const int AkhirHeader = 176;

        private sealed class VarSav
        {
            public string Nama = "";
            public byte[] NamaMentah = Array.Empty<byte>();
            public byte[] LabelMentah = Array.Empty<byte>();
            public string Label = "";

            public int Tipe;

            public int Lebar;

            public int Blok = 1;

            public int Desimal;

            public int TipeFormat;

            public Measure Ukur = Measure.Skala;

            public readonly List<byte[]> HilangMentah = new();
            public bool AdaRentang;
            public double RentangBawah, RentangAtas;

            public readonly List<double> HilangAngka = new();
            public readonly List<string> HilangTeks = new();

            public readonly List<(byte[] Nilai, byte[] Teks)> LabelKasar = new();

            public readonly Dictionary<double, string> LabelAngka = new();
            public readonly Dictionary<string, string> LabelTeks = new();

            public bool Numerik => Tipe == 0 && TipeFormat != 1;

            public bool Tanggal => TipeFormat is 20 or 21 or 22 or 23 or 24 or 25 or 38 or 39;
        }

        
        
        

        public static bool TampaknyaSav(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                var kepala = new byte[4];
                if (fs.Read(kepala, 0, 4) != 4) return false;
                string tanda = System.Text.Encoding.ASCII.GetString(kepala);
                return tanda == "$FL2" || tanda == "$FL3";
            }
            catch (IOException)
            {
                return false;
            }
        }

        public static Dataset Baca(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < AkhirHeader)
                throw new InvalidDataException("Berkas terlalu pendek untuk berkas .sav.");

            string penanda = TeksLatin(data, 0, 4);
            if (penanda == "$FL3")
                throw new NotSupportedException(
                    "Berkas ini .sav terkompresi zlib ($FL3). Belum didukung — "
                    + "buka di program rujukan lalu simpan ulang sebagai .sav biasa.");
            if (penanda != "$FL2")
                throw new InvalidDataException(
                    $"Bukan berkas .sav: penandanya '{penanda}', seharusnya '$FL2'.");

            int layout = BitConverter.ToInt32(data, 64);
            int kompresi = BitConverter.ToInt32(data, 72);
            int nkasus = BitConverter.ToInt32(data, 80);
            double bias = BitConverter.ToDouble(data, 84);
            
            
            
            byte[] labelBerkasMentah = Ambil(data, 109, 64);

            if (layout != 2)
                throw new NotSupportedException($"Layout berkas {layout} belum didukung (hanya 2).");
            if (kompresi != 0 && kompresi != 1)
                throw new NotSupportedException(
                    $"Kompresi berkas {kompresi} belum didukung (hanya 0 dan 1). "
                    + "Simpan ulang di program rujukan tanpa kompresi zlib.");

            
            var vars = new List<VarSav>();
            var rekamKeVar = new List<int>();      
            var blokLabel = new List<(byte[], byte[])>();
            int[]? ukurMentah = null;
            byte[]? namaPanjangMentah = null;
            int? kodeKarakter = null;
            ulong[]? polaHilang = null;
            int pos = AkhirHeader;
            int posData = -1;

            while (pos + 4 <= data.Length)
            {
                int posAwal = pos;
                int tipe = BitConverter.ToInt32(data, pos);

                if (tipe == 2)
                {
                    pos += 4;
                    if (pos + 28 > data.Length)
                        throw new InvalidDataException(
                            $"Rekaman variabel terpotong pada offset {pos}: butuh 28 bita, "
                            + $"sisa {data.Length - pos} bita.");
                    int vtype = BitConverter.ToInt32(data, pos);
                    int punyaLabel = BitConverter.ToInt32(data, pos + 4);
                    int nHilang = BitConverter.ToInt32(data, pos + 8);
                    int fmtCetak = BitConverter.ToInt32(data, pos + 12);
                    byte[] namaSingkat = Ambil(data, pos + 20, 8);
                    
                    pos += 28;

                    byte[] labelMentah = Array.Empty<byte>();
                    if (punyaLabel != 0)
                    {
                        int panjang = BacaInt32(data, pos, "panjang label variabel");
                        pos += 4;
                        labelMentah = Ambil(data, pos, panjang);
                        
                        
                        
                        
                        int bantalan = panjang > 0 ? (4 - panjang % 4) % 4 : 0;
                        pos += Math.Max(0, panjang) + bantalan;
                    }

                    var hilang = new List<byte[]>();
                    if (nHilang != 0)
                    {
                        
                        
                        
                        
                        long n = Math.Abs((long)nHilang);
                        long sisa = data.Length - pos;
                        if (n > sisa / 8)
                            throw new InvalidDataException(
                                $"Rekaman variabel mengaku {n} nilai hilang, "
                                + $"padahal sisa berkas hanya {sisa} bita.");
                        for (int i = 0; i < n; i++)
                            hilang.Add(Ambil(data, pos + 8 * i, 8));
                        pos += 8 * (int)n;
                    }

                    if (vtype == -1)
                    {
                        
                        
                        
                        
                        
                        if (vars.Count == 0)
                            throw new InvalidDataException("Rekaman lanjutan tanpa variabel induk.");
                        vars[^1].Blok++;
                        rekamKeVar.Add(vars.Count - 1);
                        
                        
                        
                    }
                    else
                    {
                        
                        
                        
                        
                        if (vars.Count > data.Length / 32)
                            throw new InvalidDataException(
                                $"Berkas mengaku lebih dari {vars.Count} variabel, "
                                + $"melebihi yang mungkin ditampung {data.Length} bita.");

                        var v = new VarSav
                        {
                            NamaMentah = namaSingkat,
                            LabelMentah = labelMentah,
                            Tipe = vtype,
                            Desimal = fmtCetak & 0xFF,
                            TipeFormat = (fmtCetak >> 16) & 0xFF,
                            Ukur = (vtype > 0 || ((fmtCetak >> 16) & 0xFF) == 1)
                                   ? Measure.Nominal : Measure.Skala,
                            Blok = 1
                        };
                        
                        
                        v.Lebar = vtype > 0 ? vtype : 8;
                        if (vtype >= 255)
                            throw new NotSupportedException(
                                "Berkas ini memakai string sangat panjang (> 255 bita). "
                                + "Belum didukung — pecah variabelnya di program rujukan.");

                        if (nHilang == -2 || nHilang == -3)
                        {
                            if (hilang.Count < 2)
                                throw new InvalidDataException(
                                    $"Rentang nilai hilang butuh 2 nilai, berkas memberi {hilang.Count}.");
                            v.AdaRentang = true;
                            v.RentangBawah = BitConverter.ToDouble(hilang[0], 0);
                            v.RentangAtas = BitConverter.ToDouble(hilang[1], 0);
                            if (nHilang == -3 && hilang.Count >= 3) v.HilangMentah.Add(hilang[2]);
                        }
                        else
                        {
                            foreach (byte[] h in hilang) v.HilangMentah.Add(h);
                        }

                        vars.Add(v);
                        rekamKeVar.Add(vars.Count - 1);
                    }
                }
                else if (tipe == 3)
                {
                    pos += 4;
                    int jumlah = BacaInt32(data, pos, "banyak label nilai");
                    pos += 4;
                    
                    
                    if (jumlah < 0 || jumlah > (data.Length - pos) / 9)
                        throw new InvalidDataException(
                            $"Rekaman label nilai mengaku {jumlah} entri, "
                            + $"padahal sisa berkas hanya {data.Length - pos} bita.");
                    blokLabel.Clear();
                    for (int i = 0; i < jumlah; i++)
                    {
                        
                        
                        if (pos + 9 > data.Length)
                            throw new InvalidDataException(
                                $"Rekaman label nilai terpotong di entri ke-{i + 1}.");
                        byte[] nilai = Ambil(data, pos, 8);
                        pos += 8;
                        int panjang = data[pos];
                        pos += 1;
                        byte[] teks = Ambil(data, pos, panjang);
                        
                        pos += panjang + ((8 - (1 + panjang) % 8) % 8);
                        blokLabel.Add((nilai, teks));
                    }
                }
                else if (tipe == 4)
                {
                    pos += 4;
                    int jumlah = BacaInt32(data, pos, "banyak indeks pemetaan label");
                    pos += 4;
                    
                    
                    long maju = 4L * jumlah;
                    if (jumlah < 0 || pos + maju > data.Length)
                        throw new InvalidDataException(
                            $"Rekaman pemetaan label mengaku {jumlah} indeks, "
                            + $"padahal sisa berkas hanya {data.Length - pos} bita.");
                    for (int i = 0; i < jumlah; i++)
                    {
                        int idx = BitConverter.ToInt32(data, pos + 4 * i);
                        if (idx >= 1 && idx <= rekamKeVar.Count)
                        {
                            var tujuan = vars[rekamKeVar[idx - 1]];
                            tujuan.LabelKasar.AddRange(blokLabel);
                        }
                    }
                    pos += (int)maju;
                    blokLabel.Clear();
                }
                else if (tipe == 6)
                {
                    pos += 4;
                    int baris = BacaInt32(data, pos, "banyak baris dokumen");
                    
                    
                    long maju = 4L + 80L * baris;
                    if (baris < 0 || pos + maju > data.Length)
                        throw new InvalidDataException(
                            $"Rekaman dokumen mengaku {baris} baris teks, "
                            + $"padahal sisa berkas hanya {data.Length - pos} bita.");
                    pos += (int)maju;
                }
                else if (tipe == 7)
                {
                    pos += 4;
                    if (pos + 12 > data.Length)
                        throw new InvalidDataException(
                            $"Rekaman 7 terpotong pada offset {pos}: butuh 12 bita, "
                            + $"sisa {data.Length - pos} bita.");
                    int subtipe = BitConverter.ToInt32(data, pos);
                    int ukuran = BitConverter.ToInt32(data, pos + 4);
                    int jumlah = BitConverter.ToInt32(data, pos + 8);
                    pos += 12;

                    
                    
                    
                    
                    
                    long total = (long)ukuran * jumlah;
                    if (total < 0 || pos + total > data.Length)
                        throw new InvalidDataException(
                            $"Rekaman 7 subtipe {subtipe} mengaku {total} bita isi, "
                            + $"padahal sisa berkas hanya {data.Length - pos} bita.");

                    if (subtipe == 11)
                    {
                        
                        
                        
                        
                        if (jumlah < 0 || jumlah > (data.Length - pos) / 4)
                            throw new InvalidDataException(
                                $"Rekaman tingkat ukur mengaku {jumlah} variabel, "
                                + $"padahal sisa berkas hanya {data.Length - pos} bita.");
                        ukurMentah = new int[jumlah];
                        for (int i = 0; i < jumlah; i++)
                            ukurMentah[i] = BitConverter.ToInt32(data, pos + 4 * i);
                    }
                    else if (subtipe == 13)
                    {
                        namaPanjangMentah = Ambil(data, pos, (int)total);
                    }
                    else if (subtipe == 3 && total >= 32)
                    {
                        
                        
                        
                        
                        
                        
                        
                        kodeKarakter = BitConverter.ToInt32(data, pos + 28);
                    }
                    else if (subtipe == 4 && total >= 24)
                    {
                        
                        
                        
                        polaHilang = new ulong[3];
                        for (int i = 0; i < 3; i++)
                            polaHilang[i] = BitConverter.ToUInt64(data, pos + 8 * i);
                    }
                    pos += (int)total;
                }
                else if (tipe == 999)
                {
                    pos += 4;
                    int pengisi = BacaInt32(data, pos, "pengisi rekaman akhir kamus");
                    long maju = 4L + pengisi;
                    if (pengisi < 0 || pos + maju > data.Length)
                        throw new InvalidDataException(
                            $"Rekaman akhir kamus mengaku {pengisi} bita pengisi, "
                            + $"padahal sisa berkas hanya {data.Length - pos} bita.");
                    pos += (int)maju;
                    posData = pos;
                    break;
                }
                else
                {
                    throw new InvalidDataException(
                        $"Rekaman kamus tipe {tipe} tidak dikenal di offset {pos}.");
                }

                
                
                
                
                
                
                
                if (pos <= posAwal || pos > data.Length)
                    throw new InvalidDataException(
                        $"Rekaman tipe {tipe} di offset {posAwal} tidak memajukan "
                        + $"pembacaan (pos menjadi {pos}) — berkas rusak.");
            }

            if (posData < 0)
                throw new InvalidDataException("Kamus berkas tidak diakhiri rekaman 999.");
            if (vars.Count == 0)
                throw new InvalidDataException("Berkas tidak berisi variabel apa pun.");

            
            Sandi sandi = SandiDariKode(kodeKarakter);
            string labelBerkas = Teks(labelBerkasMentah, sandi);

            foreach (var v in vars)
            {
                v.Nama = Teks(v.NamaMentah, sandi).PotongSpasi();
                v.Label = Teks(v.LabelMentah, sandi).PotongSpasi();
                foreach (byte[] h in v.HilangMentah)
                {
                    if (v.Numerik) v.HilangAngka.Add(BitConverter.ToDouble(h, 0));
                    else v.HilangTeks.Add(Teks(h, sandi).PotongSpasi());
                }
                foreach (var (nilai8, teks) in v.LabelKasar)
                {
                    string isi = Teks(teks, sandi).PotongSpasi();
                    if (v.Numerik) v.LabelAngka[BitConverter.ToDouble(nilai8, 0)] = isi;
                    else v.LabelTeks[Teks(nilai8, sandi).PotongSpasi()] = isi;
                }
            }

            
            
            
            if (namaPanjangMentah != null)
            {
                string daftar = Teks(namaPanjangMentah, sandi);
                var peta = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string potong in daftar.Split('\t', '\0'))
                {
                    int sama = potong.IndexOf('=');
                    if (sama <= 0) continue;
                    peta[potong.Substring(0, sama).Trim()] = potong.Substring(sama + 1).Trim();
                }
                foreach (var v in vars)
                    if (peta.TryGetValue(v.Nama, out string? panjang) && panjang.Length > 0)
                        v.Nama = panjang;
            }

            
            
            
            
            
            
            
            if (ukurMentah != null && ukurMentah.Length >= 2 * vars.Count)
            {
                
                
                
                int langkah = ukurMentah.Length >= 3 * vars.Count ? 3 : 2;
                for (int i = 0; i < vars.Count; i++)
                {
                    Measure? nyata = ukurMentah[langkah * i] switch
                    {
                        1 => Measure.Nominal,
                        2 => Measure.Ordinal,
                        3 => Measure.Skala,
                        _ => (Measure?)null
                    };
                    if (nyata.HasValue) vars[i].Ukur = nyata.Value;
                }
            }

            foreach (var v in vars)
            {
                
                
                
                if (v.Tanggal) v.Ukur = Measure.Nominal;
            }

            
            var dipakai = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in vars)
            {
                string dasar = string.IsNullOrWhiteSpace(v.Nama) ? "V" : v.Nama.Trim();
                string calon = dasar;
                int n = 1;
                while (!dipakai.Add(calon)) calon = $"{dasar}_{++n}";
                v.Nama = calon;
            }

            
            var rows = BacaData(data, posData, vars, kompresi, nkasus, bias, sandi, polaHilang);

            // Berkas yang terpotong di tengah data berhenti membaca dengan rapi
            // dan menghasilkan lebih sedikit baris daripada yang diklaim kamus.
            // Tanpa penjaga ini data rusak tampak sebagai berkas yang sah.
            if (nkasus > 0 && rows.Count < nkasus)
                throw new InvalidDataException(
                    $"Data terpotong: kamus mengaku {nkasus} baris, "
                    + $"hanya {rows.Count} baris yang terbaca.");

            
            var ds = new Dataset
            {
                Name = string.IsNullOrWhiteSpace(labelBerkas)
                       ? Path.GetFileNameWithoutExtension(path)
                       : labelBerkas.PotongSpasi(),
                SourcePath = path
            };
            foreach (var v in vars)
            {
                var nv = new Variable
                {
                    Name = v.Nama,
                    Label = v.Label,
                    Measure = v.Ukur,
                    Decimals = v.Desimal
                };
                foreach (var kv in v.LabelAngka) nv.ValueLabels[kv.Key] = kv.Value;
                foreach (var kv in v.LabelTeks) nv.ValueLabelsTeks[kv.Key] = kv.Value;
                ds.Variables.Add(nv);
            }
            foreach (var baris in rows) ds.Rows.Add(baris);
            return ds;
        }

        
        
        

        private static List<string?[]> BacaData(
            byte[] data, int pos, List<VarSav> vars, int kompresi, int nkasus,
            double bias, Sandi sandi, ulong[]? polaHilang)
        {
            var rows = new List<string?[]>();
            if (pos >= data.Length) return rows;

            int outSlot = 0;
            foreach (var v in vars) outSlot += v.Blok;
            int barisBita = outSlot * 8;

            
            
            bool adaPola = polaHilang != null && polaHilang.Length >= 3;
            ulong polaSysmis = adaPola ? polaHilang![0] : 0;
            ulong polaTertinggi = adaPola ? polaHilang![1] : 0;
            ulong polaTerendah = adaPola ? polaHilang![2] : 0;

            if (kompresi == 0)
            {
                int p = pos;
                while ((nkasus < 0 || rows.Count < nkasus) && p + barisBita <= data.Length)
                {
                    rows.Add(Uraikan(data, p, vars, sandi,
                                     adaPola, polaSysmis, polaTertinggi, polaTerendah));
                    p += barisBita;
                }
                return rows;
            }

            
            
            
            
            var keluaran = new byte[barisBita];
            var kata = new byte[8];
            int posIn = pos;
            int i = 8;                  
            bool berhentiSemua = false;

            while (!berhentiSemua && (nkasus < 0 || rows.Count < nkasus))
            {
                int outOff = 0;
                bool barisSelesai = false;

                while (!barisSelesai)
                {
                    if (i == 8)
                    {
                        if (posIn + 8 > data.Length) { berhentiSemua = true; break; }
                        Array.Copy(data, posIn, kata, 0, 8);
                        posIn += 8;
                        i = 0;
                    }

                    while (i < 8)
                    {
                        byte kode = kata[i];
                        if (kode == 252) { berhentiSemua = true; barisSelesai = true; break; }

                        if (kode == 0)
                        {
                            
                            
                            
                        }
                        else if (kode == 253)
                        {
                            if (posIn + 8 > data.Length) { berhentiSemua = true; barisSelesai = true; break; }
                            Array.Copy(data, posIn, keluaran, outOff, 8);
                            outOff += 8;
                            posIn += 8;
                        }
                        else if (kode == 254)
                        {
                            for (int b = 0; b < 8; b++) keluaran[outOff + b] = 0x20;
                            outOff += 8;
                        }
                        else if (kode == 255)
                        {
                            Array.Copy(BitConverter.GetBytes(polaSysmis), 0, keluaran, outOff, 8);
                            outOff += 8;
                        }
                        else
                        {
                            Array.Copy(BitConverter.GetBytes(kode - bias), 0, keluaran, outOff, 8);
                            outOff += 8;
                        }

                        i++;
                        if (barisBita - outOff < 8) { barisSelesai = true; break; }
                    }
                }

                
                
                if (outOff >= barisBita)
                    rows.Add(Uraikan(keluaran, 0, vars, sandi,
                                     adaPola, polaSysmis, polaTertinggi, polaTerendah));
            }

            return rows;
        }

        private static string?[] Uraikan(
            byte[] data, int pos, List<VarSav> vars, Sandi sandi,
            bool adaPola, ulong polaSysmis, ulong polaTertinggi, ulong polaTerendah)
        {
            int nvar = vars.Count;
            var baris = new string?[nvar];
            int slot = 0;

            for (int v = 0; v < nvar; v++)
            {
                var V = vars[v];
                int awal = pos + slot * 8;

                if (V.Numerik)
                {
                    ulong pola = BitConverter.ToUInt64(data, awal);
                    double x = BitConverter.ToDouble(data, awal);
                    slot++;

                    bool kosong = double.IsNaN(x)
                                  || (adaPola && (pola == polaSysmis
                                                  || pola == polaTertinggi
                                                  || pola == polaTerendah));
                    baris[v] = !kosong && !Hilang(V, x) ? AngkaKeTeks(x, V) : null;
                }
                else
                {
                    
                    string teks = Teks(Ambil(data, awal, V.Lebar), sandi).PotongSpasi();
                    slot += V.Blok;
                    baris[v] = teks.Length == 0 || V.HilangTeks.Contains(teks) ? null : teks;
                }
            }

            return baris;
        }

        private static bool Hilang(VarSav v, double x)
        {
            if (v.AdaRentang && x >= v.RentangBawah && x <= v.RentangAtas) return true;
            foreach (double h in v.HilangAngka)
                if (x == h) return true;
            return false;
        }

        private static string AngkaKeTeks(double v, VarSav var)
        {
            if (var.Tanggal) return TanggalKeTeks(v, var.TipeFormat);
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string PotongSpasi(this string teks) => teks.TrimEnd(' ');

        private static string TanggalKeTeks(double detik, int tipeFormat)
        {
            try
            {
                var epok = new DateTime(1582, 10, 14, 0, 0, 0, DateTimeKind.Unspecified);
                switch (tipeFormat)
                {
                    case 20: case 23: case 24: case 38: case 39:   
                        return epok.AddSeconds(detik)
                                   .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    case 22:                                        
                        return epok.AddSeconds(detik)
                                   .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    case 21: case 25:                               
                        
                        
                        
                        return TimeSpan.FromSeconds(detik)
                                       .ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
                    default:
                        
                        
                        
                        return detik.ToString("R", CultureInfo.InvariantCulture);
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                return detik.ToString("R", CultureInfo.InvariantCulture);
            }
        }

        
        
        

        // Baca 4 bita dengan penjaga batas. BitConverter melempar
        // ArgumentException kalau bita tinggal kurang dari empat, padahal
        // berkas .sav yang rusak harus dilaporkan sebagai InvalidDataException.
        private static int BacaInt32(byte[] data, int pos, string apa)
        {
            if (pos < 0 || pos + 4 > data.Length)
                throw new InvalidDataException(
                    $"Berkas terpotong: {apa} butuh 4 bita pada offset {pos}, "
                    + $"sisa {Math.Max(0, data.Length - pos)} bita.");
            return BitConverter.ToInt32(data, pos);
        }

        private static byte[] Ambil(byte[] data, int pos, int panjang)
        {
            
            
            
            if (panjang <= 0 || pos < 0 || pos > data.Length) return Array.Empty<byte>();
            int tersedia = Math.Min(panjang, Math.Max(0, data.Length - pos));
            var hasil = new byte[tersedia];
            Array.Copy(data, pos, hasil, 0, tersedia);
            return hasil;
        }

        private static string TeksLatin(byte[] data, int pos, int panjang)
            => Teks(Ambil(data, pos, panjang), Sandi.Cp1252);

        private enum Sandi { Utf8, Cp1252, Latin1, Ascii }

        private static Sandi SandiDariKode(int? kode)
        {
            switch (kode)
            {
                case 65001:                       
                    return Sandi.Utf8;
                case 1252:
                case 2:
                case 3:                           
                    return Sandi.Cp1252;
                case 28591:                       
                    return Sandi.Latin1;
                case 20127:                       
                    return Sandi.Ascii;
                case null:
                    
                    
                    
                    
                    
                    
                    
                    return Sandi.Cp1252;
                default:
                    throw new NotSupportedException(
                        $"Berkas ini memakai sandi karakter {kode}"
                        + (NamaSandi.TryGetValue(kode.Value, out string? nama) ? $" ({nama})" : "")
                        + ". Sandi itu belum didukung tanpa menambah paket di luar .NET. "
                        + "Buka di program rujukan lalu simpan ulang dengan sandi UTF-8.");
            }
        }

        private static readonly Dictionary<int, string> NamaSandi = new()
        {
            [1] = "EBCDIC-US", [4] = "DEC-KANJI", [437] = "CP437", [708] = "ASMO-708",
            [737] = "CP737", [775] = "CP775", [850] = "CP850", [852] = "CP852",
            [855] = "CP855", [857] = "CP857", [858] = "CP858", [860] = "CP860",
            [861] = "CP861", [862] = "CP862", [863] = "CP863", [864] = "CP864",
            [865] = "CP865", [866] = "CP866", [869] = "CP869", [874] = "CP874",
            [932] = "CP932 (Jepang)", [936] = "CP936 (Tionghoa Sederhana)",
            [949] = "CP949 (Korea)", [950] = "BIG-5 (Tionghoa Tradisional)",
            [1200] = "UTF-16LE", [1201] = "UTF-16BE",
            [1250] = "WINDOWS-1250", [1251] = "WINDOWS-1251", [1253] = "WINDOWS-1253",
            [1254] = "WINDOWS-1254", [1255] = "WINDOWS-1255", [1256] = "WINDOWS-1256",
            [1257] = "WINDOWS-1257", [1258] = "WINDOWS-1258", [1361] = "CP1361",
            [10000] = "MACROMAN", [10004] = "MACARABIC", [10005] = "MACHEBREW",
            [10006] = "MACGREEK", [10007] = "MACCYRILLIC", [10010] = "MACROMANIA",
            [10017] = "MACUKRAINE", [10021] = "MACTHAI", [10029] = "MACCENTRALEUROPE",
            [10079] = "MACICELAND", [10081] = "MACTURKISH", [10082] = "MACCROATIAN",
            [12000] = "UTF-32LE", [12001] = "UTF-32BE",
            [20866] = "KOI8-R", [20932] = "EUC-JP", [21866] = "KOI8-U",
            [28592] = "ISO-8859-2", [28593] = "ISO-8859-3", [28594] = "ISO-8859-4",
            [28595] = "ISO-8859-5", [28596] = "ISO-8859-6", [28597] = "ISO-8859-7",
            [28598] = "ISO-8859-8", [28599] = "ISO-8859-9", [28603] = "ISO-8859-13",
            [28605] = "ISO-8859-15", [50220] = "ISO-2022-JP", [50225] = "ISO-2022-KR",
            [50229] = "ISO-2022-CN", [51932] = "EUC-JP", [51936] = "GBK",
            [51949] = "EUC-KR", [52936] = "HZ-GB-2312", [54936] = "GB18030",
            [65000] = "UTF-7"
        };

        private static string Teks(byte[] bita, Sandi sandi)
        {
            if (bita.Length == 0) return "";
            if (sandi == Sandi.Utf8)
                return Encoding.UTF8.GetString(bita).Replace("\0", "");

            int akhir = Array.IndexOf(bita, (byte)0);
            if (akhir < 0) akhir = bita.Length;
            if (akhir == 0) return "";

            if (sandi == Sandi.Latin1) return Encoding.Latin1.GetString(bita, 0, akhir);
            if (sandi == Sandi.Ascii) return Encoding.ASCII.GetString(bita, 0, akhir);

            var sb = new StringBuilder(akhir);
            for (int k = 0; k < akhir; k++)
            {
                byte b = bita[k];
                sb.Append(b < 0x80 ? (char)b
                        : b < 0xA0 ? Cp1252Tinggi[b - 0x80]
                        : (char)b);
            }
            return sb.ToString();
        }

        private static readonly char[] Cp1252Tinggi =
        {
            '\u20AC', '\u0081', '\u201A', '\u0192', '\u201E', '\u2026', '\u2020', '\u2021',
            '\u02C6', '\u2030', '\u0160', '\u2039', '\u0152', '\u008D', '\u017D', '\u008F',
            '\u0090', '\u2018', '\u2019', '\u201C', '\u201D', '\u2022', '\u2013', '\u2014',
            '\u02DC', '\u2122', '\u0161', '\u203A', '\u0153', '\u009D', '\u017E', '\u0178'
        };
    }
}
