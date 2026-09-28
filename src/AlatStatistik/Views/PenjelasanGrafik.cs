using AlatStatistik.Models;

namespace AlatStatistik.Views
{

    public sealed class CaraMembaca
    {

        public string Bacaan { get; set; } = "";

        public string Perhatikan { get; set; } = "";

        public string HatiHati { get; set; } = "";
    }

    public static class PenjelasanGrafik
    {

        public static CaraMembaca? Untuk(ChartKind jenis) => jenis switch
        {
            ChartKind.Histogram => new CaraMembaca
            {
                Bacaan = "Tinggi tiap batang menyatakan berapa banyak kasus yang nilainya jatuh "
                       + "di rentang itu. Seluruh batang bersama-sama menggambarkan bentuk sebaran "
                       + "satu variabel.",
                Perhatikan = "Berapa puncaknya dan di mana letaknya. Satu puncak yang miring ke "
                           + "satu sisi berarti sebarannya tidak seimbang. Dua puncak yang terpisah "
                           + "jauh biasanya tanda ada dua kelompok berbeda yang tercampur dalam satu "
                           + "data.",
                HatiHati = "Bentuknya ikut berubah kalau jumlah kelasnya diubah — sebaran yang sama "
                         + "bisa tampak bergerigi atau terlalu halus. Program ini memakai aturan "
                         + "√n kelas, jadi jumlah kelasnya bukan pilihanmu."
            },

            ChartKind.BoxPlot => new CaraMembaca
            {
                Bacaan = "Kotaknya memuat 50% kasus yang ada di tengah: dari kuartil 1 (bawah kotak) "
                       + "sampai kuartil 3 (atas kotak). Garis di dalam kotak adalah median. Kumis "
                       + "menjangkau nilai terjauh yang masih dianggap wajar, dan bulatan kecil adalah "
                       + "pencilan.",
                Perhatikan = "Letak garis median di dalam kotak: kalau ia tidak di tengah, sebarannya "
                           + "miring. Panjang kotak menunjukkan seberapa menyebar 50% data yang tengah. "
                           + "Membandingkan beberapa kotak sekaligus menunjukkan kelompok mana yang "
                           + "nilainya lebih tinggi dan mana yang lebih seragam.",
                HatiHati = "Angka di ujung sumbu tegak adalah ujung SKALA, bukan nilai terkecil dan "
                         + "terbesar — skalanya sengaja diberi ruang kosong di kedua ujung. Nilai "
                         + "terkecil dan terbesar yang sebenarnya ada di tabel ringkasan. Kotak yang "
                         + "sangat pendek pada data yang sedikit juga bisa menyesatkan."
            },

            ChartKind.Scatter => new CaraMembaca
            {
                Bacaan = "Tiap titik adalah satu kasus, dengan nilai variabel pertama sebagai posisi "
                       + "mendatar dan variabel kedua sebagai posisi tegak. Garis lurus yang menembus "
                       + "titik-titik itu adalah garis regresinya.",
                Perhatikan = "Arah garisnya: naik berarti kedua variabel cenderung bergerak searah, "
                           + "turun berarti berlawanan. Seberapa rapat titik-titik menggerombol di "
                           + "sekitar garis menunjukkan seberapa kuat hubungannya. Titik yang jauh "
                           + "sendirian perlu diperiksa — satu titik ekstrem bisa membelokkan garis.",
                HatiHati = "Hubungan yang terlihat bukan bukti sebab-akibat. Kalau titik-titiknya "
                         + "melengkung, menggerombol jadi dua kelompok, atau menyebar makin lebar ke "
                         + "satu sisi, angka korelasi Pearson tidak lagi menggambarkan hubungannya "
                         + "dengan jujur — lihat gambarnya, jangan hanya angkanya."
            },

            ChartKind.Bar => new CaraMembaca
            {
                Bacaan = "Tinggi tiap batang adalah frekuensi kategori itu — berapa banyak baris data "
                       + "yang berisi nilai tersebut.",
                Perhatikan = "Kategori mana yang paling sering muncul dan seberapa jauh jaraknya dari "
                           + "yang lain. Batang yang sangat pendek atau tidak muncul sama sekali "
                           + "menunjukkan kategori langka, dan itu sering justru yang dicari.",
                HatiHati = "Sumbu tegaknya selalu mulai dari nol, jadi tinggi batang bisa dibandingkan "
                         + "langsung. Kalau jumlah kategorinya lebih banyak daripada batas yang muat "
                         + "digambar, hanya kategori yang paling sering yang tampil — jumlah "
                         + "kategorinya yang sebenarnya disebutkan di catatan bawah gambar."
            },

            ChartKind.Line => new CaraMembaca
            {
                Bacaan = "Tiap deret digambar sebagai garis yang menghubungkan nilai-nilai berurutan. "
                       + "Sumbu mendatarnya adalah nomor baris di data, bukan tanggal.",
                Perhatikan = "Arah umum tiap garis: menanjak, menurun, atau mendatar. Apakah satu "
                           + "deret konsisten di atas deret lain, dan apakah ada lonjakan atau "
                           + "penurunan yang tiba-tiba.",
                HatiHati = "Karena sumbu mendatarnya urutan baris, gambar ini baru bermakna sebagai "
                         + "deret waktu kalau datanya sudah diurutkan menurut waktu lebih dulu. Nilai "
                         + "yang kosong dilewati, sehingga garis bisa melompat seolah tidak ada jeda. "
                         + "Garis yang naik-turun tajam pada data yang sedikit biasanya hanya derau."
            },

            ChartKind.Pie => new CaraMembaca
            {
                Bacaan = "Tiap irisan adalah satu kategori, dan besar sudutnya sebanding dengan "
                       + "bagiannya terhadap keseluruhan. Daftar di sebelah kanan menyebut nama dan "
                       + "persentasenya.",
                Perhatikan = "Irisan mana yang paling besar, dan apakah ada beberapa irisan kecil "
                           + "yang sebenarnya bisa digabung jadi satu kategori bermakna.",
                HatiHati = "Diagram lingkaran hanya cocok untuk data berkategori yang jumlahnya "
                         + "sedikit. Untuk variabel bersambung — misalnya tinggi badan yang hampir "
                         + "tiap barisnya berbeda — hampir semua irisan jadi sama kecil, dan "
                         + "gambarnya tidak lagi memberi tahu apa pun. Kalau begitu, pakai histogram. "
                         + "Angka N di bawah daftar adalah jumlah seluruh kasus, sedangkan yang "
                         + "digambar hanya kategori terbesar."
            },

            ChartKind.Heatmap => new CaraMembaca
            {
                Bacaan = "Tiap sel adalah hubungan antara variabel di barisnya dan variabel di "
                       + "kolomnya. Merah berarti searah, biru berarti berlawanan, dan makin pekat "
                       + "warnanya makin kuat hubungannya.",
                Perhatikan = "Sel di luar garis diagonal miring — itulah pasangan variabel yang "
                           + "hubungannya perlu diperiksa. Nilai yang mendekati nol berarti kedua "
                           + "variabel itu praktis tidak berhubungan.",
                HatiHati = "Sel di sepanjang diagonal selalu bernilai 1, karena itu variabel dengan "
                         + "dirinya sendiri — bukan temuan. Matriksnya simetris, jadi separuhnya "
                         + "hanya salinan. Seperti halnya diagram pencar, hubungan yang kuat bukan "
                         + "berarti sebab-akibat, dan satu dua kasus ekstrem bisa menciptakan "
                         + "hubungan yang sebenarnya tidak ada."
            },

            ChartKind.ErrorBar => new CaraMembaca
            {
                Bacaan = "Titik adalah nilai rerata tiap kelompok. Garis tegak beserta tutup di kedua "
                       + "ujungnya adalah rentang ketidakpastian rerata itu — jenisnya (selang "
                       + "kepercayaan, galat baku, atau simpangan baku) disebutkan pada pilihan yang "
                       + "kamu pakai.",
                Perhatikan = "Panjang pendeknya rentang: rentang yang panjang berarti rerata itu "
                           + "kurang pasti, biasanya karena datanya sedikit atau beragam. Bandingkan "
                           + "apakah rentang antar-kelompok saling bertumpuk.",
                HatiHati = "Rentang yang tidak bertumpuk sering dianggap bukti ada perbedaan, dan "
                         + "rentang yang bertumpuk sering dianggap bukti tidak ada perbedaan. Kedua "
                         + "dugaan itu tidak selalu benar. Kalau yang kamu butuhkan adalah keputusan "
                         + "ada-tidaknya perbedaan, jalankan uji hipotesisnya — gambar ini hanya "
                         + "memberi gambaran kasar."
            },

            ChartKind.Dendrogram => new CaraMembaca
            {
                Bacaan = "Daun di bagian bawah adalah kasus atau variabel yang dikelompokkan. Dua "
                       + "cabang yang bertemu pada ketinggian tertentu berarti keduanya digabung di "
                       + "jarak sejauh itu — makin tinggi pertemuannya, makin tidak mirip keduanya.",
                Perhatikan = "Lompatan ketinggian yang mencolok. Kalau ada satu garis mendatar yang "
                           + "jauh lebih tinggi daripada yang lain, memotong tepat di bawahnya "
                           + "biasanya memberi jumlah kelompok yang paling wajar.",
                HatiHati = "Bentuk pohonnya berubah kalau ukuran jarak atau cara penggabungannya "
                         + "diganti — tidak ada satu pohon yang paling benar. Urutan daun dari kiri "
                         + "ke kanan juga bukan urutan peringkat; yang bermakna hanya siapa "
                         + "bergabung dengan siapa dan pada ketinggian berapa."
            },

            ChartKind.Roc => new CaraMembaca
            {
                Bacaan = "Tiap titik mewakili satu nilai ambang pemisah. Sumbu tegak adalah "
                       + "kemampuan menangkap kasus positif yang sebenarnya (sensitivitas), dan "
                       + "sumbu mendatar adalah banyaknya kasus negatif yang ikut tertangkap "
                       + "(1 − spesifisitas).",
                Perhatikan = "Seberapa dekat kurvanya membelok ke sudut kiri atas. Makin dekat, "
                           + "makin baik pemisahannya. Garis putus-putus diagonal adalah pembanding: "
                           + "itu hasil orang yang menebak secara acak.",
                HatiHati = "Kurva yang menempel pada garis diagonal berarti tidak lebih baik "
                         + "daripada menebak. Luas di bawah kurva (AUC) tidak terlihat dari "
                         + "gambarnya — angkanya ada di tabel hasil. Kurva yang tampak bagus pada "
                         + "data yang tidak seimbang pun bisa menyesatkan, jadi periksa juga jumlah "
                         + "kasus di tiap kelas."
            },

            _ => null
        };
    }
}
