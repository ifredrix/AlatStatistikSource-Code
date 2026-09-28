using System;

namespace AlatStatistik.Models
{
    public class DataChange
    {
        public DateTime Waktu { get; set; } = DateTime.Now;

        public int Baris { get; set; }

        public string Kolom { get; set; } = "";

        public string? NilaiLama { get; set; }

        public string? NilaiBaru { get; set; }

        public bool PerSel => Baris >= 0;

        public bool Struktural => Baris == -2;

        public string Ringkasan => PerSel
            ? Kolom + " [" + BarisSatu + "]: " + Teks(NilaiLama) + " -> " + Teks(NilaiBaru)
            : Struktural
                ? Teks(NilaiBaru)
                : "kolom '" + Teks(NilaiLama) + "' -> '" + Teks(NilaiBaru) + "'";

        public string BarisSatu => PerSel ? (Baris + 1).ToString() : "-";

        public string Jenis => PerSel ? "Sel" : (Struktural ? "Struktur" : "Variabel");

        public static DataChange Struktur(string keterangan)
            => new()
            {
                Baris = -2,
                Kolom = "-",
                NilaiLama = null,
                NilaiBaru = keterangan,
                Waktu = DateTime.Now
            };

        private static string Teks(string? v)
            => string.IsNullOrEmpty(v) ? "(kosong)" : v!;
    }
}
