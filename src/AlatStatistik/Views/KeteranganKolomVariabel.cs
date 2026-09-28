namespace AlatStatistik.Views
{

    public static class KeteranganKolomVariabel
    {
        public const string Nama = "Nama";
        public const string Label = "Label";
        public const string TingkatUkur = "Tingkat ukur";
        public const string Desimal = "Desimal";
        public const string LabelNilai = "Label nilai";

        public static readonly string[] SemuaKolom =
            { Nama, Label, TingkatUkur, Desimal, LabelNilai };

        public const string TanpaPilihan =
            "Klik salah satu sel di tabel untuk melihat keterangan kolomnya.";

        public static string Untuk(string kolom) => kolom switch
        {
            Nama =>
                "Nama pendek variabel. Inilah yang dipakai di rumus dan di daftar "
                + "pilihan analisis, dan harus unik. Ganti lewat menu Data → Ganti "
                + "nama variabel supaya perubahannya ikut tercatat di Riwayat.",

            Label =>
                "Nama panjang variabel. Pada berkas .sav diisi dari label di dalam "
                + "berkasnya (mis. salary → \"Current Salary\"); pada data CSV kosong, "
                + "karena CSV tidak menyimpan label — dan bisa diketik sendiri di sini. "
                + "Dipakai alat Codebook (kamus variabel), dan disimpan ke berkas "
                + ".labels.json di sebelah datanya supaya tidak hilang saat disimpan.",

            TingkatUkur =>
                "Skala, Ordinal, atau Nominal. Menentukan variabel ini boleh dipakai "
                + "di analisis mana: Skala untuk uji yang menuntut angka (uji-t, ANOVA, "
                + "korelasi, regresi), Nominal/Ordinal untuk pengelompokan dan tabel "
                + "frekuensi. Salah memilih di sini membuat variabelnya tidak muncul "
                + "di daftar pilihan analisis.",

            Desimal =>
                "Banyak angka di belakang koma saat nilai ditampilkan. Ini hanya "
                + "mengatur tampilan — nilai yang tersimpan dan dipakai menghitung "
                + "tidak berubah.",

            LabelNilai =>
                "Padanan kolom Values di Variable View: ringkasan pasangan "
                + "kode dan teksnya, mis. \"1 = Laki-laki; 2 = Perempuan\". Kolom ini "
                + "HANYA BACA — isinya datang dari label nilai di dalam berkas .sav, "
                + "atau dari berkas .labels.json di sebelah datanya. Kalau kosong, "
                + "variabelnya belum punya label nilai; mengisinya dilakukan lewat "
                + "berkas label, bukan dengan mengetik di sini.",

            _ => TanpaPilihan
        };
    }
}
