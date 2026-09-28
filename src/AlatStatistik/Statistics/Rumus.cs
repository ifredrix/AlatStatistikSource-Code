using System.Collections.Generic;

namespace AlatStatistik.Statistics
{
    public class Rumus
    {
        public string Nama { get; set; } = "";
        public string Bentuk { get; set; } = "";
        public string Jenis { get; set; } = "";
        public string Penemu { get; set; } = "";
        public int Tahun { get; set; }
        public string Catatan { get; set; } = "";

        public string LabelTahun => Tahun > 0 ? Penemu + ", " + Tahun : Penemu;
    }

    public static class DaftarRumus
    {
        public static readonly Rumus Rerata = new()
        {
            Nama = "Rerata (rata-rata aritmetik)",
            Bentuk = "x̄ = (x₁ + x₂ + ⋯ + xₙ) / n  =  Σxᵢ / n",
            Jenis = "Statistik deskriptif — ukuran pemusatan",
            Penemu = "Leibniz; dipakai luas oleh Gauss",
            Tahun = 1810,
            Catatan = "Rerata sensitif terhadap nilai ekstrem; untuk data skewed, median lebih stabil."
        };

        public static readonly Rumus RagamSampel = new()
        {
            Nama = "Ragam dan simpangan baku sampel",
            Bentuk = "s² = Σ(xᵢ − x̄)² / (n − 1)    s = √s²",
            Jenis = "Statistik deskriptif — ukuran sebaran",
            Penemu = "Ronald A. Fisher",
            Tahun = 1925,
            Catatan = "Pembagi (n − 1) untuk koreksi Bessel — menjadikan s² tak bias."
        };

        public static readonly Rumus MedianKuartil = new()
        {
            Nama = "Median dan kuartil",
            Bentuk = "Q(p) = elemen pada posisi (n+1)·p, dengan interpolasi berbobot "
                     + "di antara dua nilai terdekat",
            Jenis = "Statistik deskriptif — ukuran pemusatan dan sebaran",
            Penemu = "Definisi 1 acuan (\"Weighted Average\")",
            Tahun = 0,
            Catatan = "Median = Q(0,5); kuartil bawah = Q(0,25); kuartil atas = Q(0,75). "
                      + "Definisi ini dipakai karena acuan memakainya, dan angka kuartilnya "
                      + "berbeda dari interpolasi (n−1)·p bawaan NumPy — pada `salary` "
                      + "menurut gender, kuartil pertama Female 21.487,50 lawan 21.562,50. "
                      + "Mediannya sendiri sama saja di kedua definisi."
        };

        public static readonly Rumus KuartilTukey = new()
        {
            Nama = "Kuartil Tukey's hinges",
            Bentuk = "Q₁ = median belahan bawah, Q₃ = median belahan atas, "
                     + "Q₂ = median seluruh data",
            Jenis = "Statistik deskriptif — kuartil cara Tukey",
            Penemu = "John W. Tukey",
            Tahun = 1977,
            Catatan = "Berbeda dari definisi 1: batas belahannya ditentukan jumlah data, "
                      + "bukan posisi (n+1)p. Untuk n ganjil median ikut masuk ke kedua "
                      + "belahan. Pada `salary` menurut gender hasilnya 21.525,00 dan "
                      + "28.500,00 (Female) — memang beda dari definisi 1."
        };

        public static readonly Rumus Skewness = new()
        {
            Nama = "Kecondongan (skewness) Fisher-Pearson",
            Bentuk = "g₁ = (1/n) Σ((xᵢ − x̄)/s)³",
            Jenis = "Statistik deskriptif — bentuk sebaran",
            Penemu = "Karl Pearson",
            Tahun = 1895,
            Catatan = "> 0 condong ke kanan; < 0 condong ke kiri; = 0 simetris."
        };

        public static readonly Rumus Kurtosis = new()
        {
            Nama = "Keruncingan (kurtosis) Fisher-Pearson",
            Bentuk = "g₂ = (1/n) Σ((xᵢ − x̄)/s)⁴ − 3",
            Jenis = "Statistik deskriptif — bentuk sebaran",
            Penemu = "Karl Pearson",
            Tahun = 1895,
            Catatan = "0 = normal; > 0 lebih runcing; < 0 lebih datar."
        };

        public static readonly Rumus UjiShapiro = new()
        {
            Nama = "Uji kenormalan Shapiro-Wilk",
            Bentuk = "W = (Σ aᵢ x₍ᵢ₎)² / Σ(xᵢ − x̄)²",
            Jenis = "Uji asumsi — normalitas",
            Penemu = "Samuel S. Shapiro, Martin B. Wilk",
            Tahun = 1965,
            Catatan = "Untuk n ≤ 50 paling berdaya. Tolak H₀ bila p < α → data tidak normal."
        };

        public static readonly Rumus UjiJarqueBera = new()
        {
            Nama = "Uji kenormalan Jarque–Bera",
            Bentuk = "JB = (n / 6) · (S² + K²/4),    S = condong, K = keruncing",
            Jenis = "Uji asumsi — normalitas",
            Penemu = "Carlos M. Jarque, Anil K. Bera",
            Tahun = 1980,
            Catatan = "Berbasis momen ke-3 dan ke-4. Butuh n cukup besar; untuk n &lt; 20 kurang bertenaga."
        };

        public static readonly Rumus UjiShapiroWilk = new()
        {
            Nama = "Uji kenormalan Shapiro–Wilk",
            Bentuk = "W = (Σ aᵢ·x₍ᵢ₎)² / Σ(xᵢ − x̄)²,   aᵢ dari nilai harapan statistik terurut normal",
            Jenis = "Uji asumsi — normalitas",
            Penemu = "Samuel S. Shapiro, Martin B. Wilk; hampiran koefisien oleh J. P. Royston",
            Tahun = 1965,
            Catatan = "W mendekati 1 bila data normal. Paling bertenaga untuk n kecil — "
                      + "inilah uji yang dilaporkan acuan pada uji kenormalan. "
                      + "Koefisiennya dihampiri dengan polinomial Royston (1992)."
        };

        public static readonly Rumus UjiKolmogorovSmirnov = new()
        {
            Nama = "Uji Kolmogorov–Smirnov (kesesuaian sebaran)",
            Bentuk = "D = sup |Fₙ(x) − F(x)|,   p ≈ 2·Σ(−1)^{k−1}·e^{−2k²λ²} dengan λ = √n·D",
            Jenis = "Uji asumsi — kesesuaian sebaran",
            Penemu = "Andrey N. Kolmogorov, Nikolai V. Smirnov",
            Tahun = 1933,
            Catatan = "Di acuan muncul pada uji kenormalan dengan 'Lilliefors Significance Correction'. "
                      + "Tanpa koreksi itu, p terlalu besar karena parameter ditaksir dari data yang sama."
        };

        public static readonly Rumus KoreksiLilliefors = new()
        {
            Nama = "Koreksi Lilliefors untuk uji Kolmogorov–Smirnov",
            Bentuk = "D* = sup |Fₙ(x) − Φ((x − x̄)/s)|,  p = P(D* ≥ D) — sebaran D* "
                     + "diperoleh lewat simulasi Monte Carlo (benih tetap, 10.000 ulangan)",
            Jenis = "Uji asumsi — kesesuaian sebaran",
            Penemu = "Hubert W. Lilliefors",
            Tahun = 1967,
            Catatan = "Perlu karena rerata dan simpangan baku ditaksir dari sampel yang sama; "
                      + "sebaran D* tidak punya bentuk tertutup sehingga disimulasikan langsung. "
                      + "Galat bakunya dilaporkan. Diperiksa terhadap tabel statsmodels 0.15.0: "
                      + "cocok 0,01%–0,15% pada ukuran sampel yang ditabulasi."
        };

        public static readonly Rumus RegresiLogistik = new()
        {
            Nama = "Regresi logistik biner (peluang maksimum)",
            Bentuk = "ln[L/(1−L)] = β₀ + β₁x₁ + ⋯ ,  L = 1/(1 + e^−η);  "
                     + "β baru = β + (XᵀWX)⁻¹ Xᵀ(y − μ)",
            Jenis = "Pemodelan parametrik — keluaran biner",
            Penemu = "Joseph Berkson; bentuk modern oleh David R. Cox",
            Tahun = 1944,
            Catatan = "Diselesaikan dengan iterasi Newton–Raphson (bersama matriks informasi "
                      + "XᵀWX), bukan kuadrat terkecil. Rasio oddsnya e^B. Bila pemisahan "
                      + "sempurna terjadi, matriksnya tidak bisa dibalik dan modelnya "
                      + "dilaporkan gagal — bukan dipaksa keluar angka."
        };

        public static readonly Rumus RegresiRidge = new()
        {
            Nama = "Regresi Ridge (penalti L2, intercept tak diberi penalti)",
            Bentuk = "β = (XᵀX + P)⁻¹ Xᵀy,  P = diag(0, α, α, …, α)",
            Jenis = "Pemodelan parametrik — variabel terikat dari beberapa prediktor",
            Penemu = "Arthur E. Hoerl; dipopulerkan oleh Hoerl & Kennard",
            Tahun = 1970,
            Catatan = "Penalti α mengecilkan koefisien agar ragam taksiran turun bila prediktor "
                      + "saling berkorelasi. Konstanta sengaja tak diberi penalti supaya titik "
                      + "potong tidak terdorong. Bila α = 0, hasilnya sama dengan regresi linear."
        };

        public static readonly Rumus RegresiProbit = new()
        {
            Nama = "Regresi Probit biner (GLM binomial, link probit)",
            Bentuk = "Φ⁻¹[P(y = 1)] = β₀ + β₁x₁ + ⋯ ,  μ = Φ(Xβ);  "
                     + "β_baru = (XᵀWX)⁻¹ XᵀWz,  W = φ(η)² / [μ(1−μ)]",
            Jenis = "Pemodelan parametrik — keluaran biner",
            Penemu = "Chester Bliss (1934, istilah 'probit'); dibangun oleh Ronald A. Fisher",
            Tahun = 1934,
            Catatan = "Sama dengan regresi logistik tetapi link-nya fungsi kejadian (probit) "
                      + "bukan logit. Diselesaikan dengan penskoran Fisher (IRLS). Koefisiennya "
                      + "bukan rasio odds, melainkan perubahan z-skor kejadian."
        };

        public static readonly Rumus RegresiMultinomial = new()
        {
            Nama = "Regresi Multinomial (logit bersar, GLM binomial berganda)",
            Bentuk = "ln[P(y = j)/P(y = 0)] = β₀ⱼ + βⱼ·x,  j = 1…(J−1);  P(y = j) = e^ηⱼ / (1 + Σe^ηₗ).  "
                     + "Perbarui β lewat penskoran Fisher: Δ = I⁻¹·g, dengan I = Σ x xᵀ (P_ij·δ_jk − P_ij·P_ik).",
            Jenis = "Pemodelan parametrik — keluaran berganda (lebih dari dua kategori)",
            Penemu = "David R. Cox (1966, teori log-linear); bentuk ML oleh Nicholas T. Longford",
            Tahun = 1966,
            Catatan = "Setiap kategori bukan-dasar punya satu set koefisien sendiri terhadap kategori "
                      + "dasar (kategori pertama menurut urutan nilai). SE diambil dari √diag(I⁻¹) pada "
                      + "β akhir — itulah yang dipakai statsmodels.MNLogit. Bila hanya dua kategori, "
                      + "ini sama dengan regresi logistik."
        };

        public static readonly Rumus RegresiOrdinal = new()
        {
            Nama = "Regresi Ordinal (logit kumulatif / peluang sebanding)",
            Bentuk = "logit P(Y ≤ j) = αⱼ − β·x,  j = 1…(J−1);  P(Y = j) = Λ(αⱼ − xβ) − Λ(αⱼ₋₁ − xβ),  Λ = 1/(1+e⁻ᵗ).  "
                     + "Dioptimalkan dengan Newton–Raphson; SE = √diag(I⁻¹) matriks informasi teramati.",
            Jenis = "Pemodelan parametrik — keluaran berurutan (ordinal)",
            Penemu = "Peter McCullagh (1980, model logit kumulatif proportional odds)",
            Tahun = 1980,
            Catatan = "Untuk variabel terikat yang berurutan (rendah≺sedang≺tinggi). Asumsi peluang "
                      + "sebanding: efek β sama di semua titik potong. Koefisien β diartikan seperti "
                      + "regresi logistik; αⱼ adalah titik potong antarkategori. Uji sebanding bisa "
                      + "ditambahkan kelak (belum ada di sini)."
        };

        public static readonly Rumus UjiFriedman = new()
        {
            Nama = "Uji Friedman (peringkat dua arah)",
            Bentuk = "χ² = [12/(k·n·(k+1))·ΣR_j² − 3·n·(k+1)] / C,   C = 1 − Σt(t²−1)/(k·(k²−1)·n)",
            Jenis = "Uji nonparametrik — ukuran berulang",
            Penemu = "Milton Friedman",
            Tahun = 1937,
            Catatan = "Peringkat dihitung di dalam setiap blok, lalu dijumlahkan per perlakuan. "
                      + "Inilah pengganti ANOVA berulang bila asumsi normalitas tidak dipenuhi. "
                      + "C mengoreksi nilai yang sama (ikatan); tanpa itu χ² menjadi terlalu kecil."
        };

        public static readonly Rumus UjiRuns = new()
        {
            Nama = "Uji runs Wald–Wolfowitz",
            Bentuk = "z = (R − μ_R)/σ_R,  μ_R = 2n₁n₂/n + 1,  σ_R² = 2n₁n₂(2n₁n₂ − n)/(n²(n − 1))",
            Jenis = "Uji nonparametrik — keacakan urutan",
            Penemu = "Abraham Wald, Jacob Wolfowitz",
            Tahun = 1940,
            Catatan = "Nilai diubah menjadi dua tanda menurut potongannya (median, rerata, atau "
                      + "nilai sendiri), lalu dihitung banyaknya run. Run terlalu sedikit berarti "
                      + "menggerombol, terlalu banyak berarti berosilasi."
        };

        public static readonly Rumus UjiBinomial = new()
        {
            Nama = "Uji binomial satu sampel",
            Bentuk = "P(X = k) = C(n,k) · pᵏ · (1 − p)ⁿ⁻ᵏ,   "
                     + "p-value dua sisi = Σ_{j: P(j) ≤ P(k)} P(j)",
            Jenis = "Uji hipotesis eksak — proporsi",
            Penemu = "Jacob Bernoulli; uji dua sisi eksak sering dikaitkan dengan Henry Berkson",
            Tahun = 1713,
            Catatan = "Menguji apakah peluang sukses yang diobservasi (k/n) menyimpang dari p₀. "
                      + "Untuk dua sisi, jumlahkan semua massa peluang yang lebih kecil dari pmf(k). "
                      + "Cara ini menyamai scipy.stats.binomtest(..., alternative='two-sided')."
        };

        public static readonly Rumus UjiProporsiSatu = new()
        {
            Nama = "Uji proporsi satu sampel",
            Bentuk = "p̂ = k/n,   z = (p̂ − p₀) / √(p₀(1 − p₀)/n),   "
                     + "p normal dua sisi = 2·(1 − Φ(|z|))",
            Jenis = "Uji hipotesis — proporsi satu sampel",
            Penemu = "Jacob Bernoulli (sebaran binomial); uji normal memakai teorema batas pusat",
            Tahun = 1713,
            Catatan = "Selain uji normal "
                      + "di atas, dilaporkan p binomial eksak (scipy.stats.binomtest) dan selang "
                      + "Wilson untuk p̂. Kategori 'sukses' = level pertama bila diurutkan."
        };

        public static readonly Rumus UjiProporsiDua = new()
        {
            Nama = "Uji proporsi dua sampel (bebas)",
            Bentuk = "d = p̂₁ − p̂₂,   SE_pool = √[p̄(1 − p̄)(1/n₁ + 1/n₂)],   "
                     + "z = d/SE_pool,   χ² = N(ad − bc)² / [(a+b)(c+d)(a+c)(b+d)]",
            Jenis = "Uji hipotesis — perbandingan dua proporsi",
            Penemu = "Karl Pearson (chi-kuadrat, 1900); uji dua proporsi memakai pendekatan normal",
            Tahun = 1900,
            Catatan = "χ² di atas "
                      + "adalah Pearson tanpa koreksi (sama dengan scipy.stats.chi2_contingency "
                      + "correction=False); p Fisher eksak dua sisi menyamai scipy.stats.fisher_exact."
        };

        public static readonly Rumus SelangWilson = new()
        {
            Nama = "Selang kepercayaan Wilson",
            Bentuk = "pusat = (p̂ + z²/2n) / (1 + z²/n),   "
                     + "setengah = z/(1 + z²/n)·√(p̂(1 − p̂)/n + z²/4n²)",
            Jenis = "Selang kepercayaan — proporsi",
            Penemu = "Edwin B. Wilson",
            Tahun = 1927,
            Catatan = "Lebih andal daripada selang Wald untuk n kecil dan untuk p dekat 0 atau 1. "
                      + "z = Φ⁻¹(1 − α/2). Dipakai untuk batas proporsi tiap kelompok."
        };

        public static readonly Rumus RasioPeluang = new()
        {
            Nama = "Odds ratio & risk ratio (2×2)",
            Bentuk = "OR = (k₁(n₂ − k₂)) / ((n₁ − k₁)k₂),   RR = p̂₁/p̂₂,   "
                     + "SE(ln OR) = √(1/k₁ + 1/(n₁−k₁) + 1/k₂ + 1/(n₂−k₂))",
            Jenis = "Ukuran efek — proporsi",
            Penemu = "metode Wald pada skala log (Hauck–Donner untuk OR)",
            Tahun = 1977,
            Catatan = "Selang kepercayaan Wald pada skala log: exp(ln· ± z·SE). OR = 1 berarti "
                      + "tidak ada beda peluang; RR = 1 berarti tidak ada beda risiko."
        };

        public static readonly Rumus UjiMoses = new()
        {
            Nama = "Uji Moses reaksi ekstrem (span)",
            Bentuk = "S = rank_maks(kendali) − rank_min(kendali) + 1,   "
                     + "p = P(S ≤ S₀) = Σ_{s=m}^{S₀} (N − s + 1)·C(s − 2, m − 2) / C(N, m)",
            Jenis = "Uji tak berparameter — dua sampel bebas (penyebaran ekstrem)",
            Penemu = "Herbert Moses",
            Tahun = 1952,
            Catatan = "Menguji apakah kelompok uji punya nilai lebih ekstrem (menjau ke ujung), "
                      + "bukan beda kecenderungan. Kendali = kelompok n lebih kecil; rentangnya "
                      + "yang kecil menunjukkan kelompok uji mendorong amatan ke ujung sebaran."
        };

        public static readonly Rumus KorelasiParsial = new()
        {
            Nama = "Korelasi parsial",
            Bentuk = "r(xy·z) = (r_xy − r_xz·r_yz) / √[(1 − r_xz²)(1 − r_yz²)]",
            Jenis = "Ukuran hubungan — korelasi bersyarat",
            Penemu = "George Udny Yule; bentuk modern untuk banyak kendali lewat matriks presisi",
            Tahun = 1897,
            Catatan = "Dihitung dari residual regresi x dan y terhadap variabel kendali, lalu "
                      + "dikorelasikan. Derajat bebasnya n − 2 − k dengan k = banyaknya kendali. "
                      + "Inilah yang acuan keluarkan pada Analyze > Correlate > Partial."
        };

        public static readonly Rumus Agregat = new()
        {
            Nama = "Agregasi (ringkasan per kelompok)",
            Bentuk = "x̄_g = Σ_{i∈g} xᵢ / n_g,   n_g = banyak amatan dalam kelompok g "
                     + "(juga jumlah, n, minimum, maksimum, median per kelompok)",
            Jenis = "Statistik ringkasan — laporan per sub-kelompok",
            Penemu = "John W. Tukey (analisis data eksploratif)",
            Tahun = 1977,
            Catatan = "pecah menurut variabel kunci, "
                      + "lalu hitung statistik ringkasan per kelompok. Satu baris hasil tiap "
                      + "kelompok unik; kelompok dengan nilai kunci kosong dilewati."
        };

        public static readonly Rumus HitungNilaiCocok = new()
        {
            Nama = "Penghitungan nilai dalam kasus (Count Values within Cases)",
            Bentuk = "cᵣ = Σ_{j} 1[xᵣⱼ = t]   (atau 1[lo ≤ xᵣⱼ ≤ hi] bila memakai rentang)",
            Jenis = "Persiapan data — Transform ▸ Count Values within Cases",
            Penemu = "Konsep acuan sejak 1980-an; dasarnya penghitungan indikator",
            Tahun = 0,
            Catatan = "Untuk tiap baris, hitung berapa banyak variabel terpilih yang nilainya "
                      + "cocok dengan target (atau berada dalam rentang). Di sini hanya "
                      + "ditampilkan sebagai laporan, dataset tidak diubah."
        };

        public static readonly Rumus KamusVariabel = new()
        {
            Nama = "Kamus data (Codebook)",
            Bentuk = "untuk tiap variabel: nama, label, tingkat ukur, desimal, jenis, "
                     + "label nilai, N sah, N hilang, dan ringkasan (min/max/rerata)",
            Jenis = "Dokumentasi metadata — File ▸ Display Data File Information ▸ Codebook",
            Penemu = "Konvensi dokumentasi data",
            Tahun = 0,
            Catatan = "Bukan uji statistik; berguna memeriksa definisi variabel, label nilai, "
                      + "dan banyaknya data hilang sebelum menganalisis."
        };

        public static readonly Rumus GabungBerkas = new()
        {
            Nama = "Penggabungan berkas (Merge Files)",
            Bentuk = "tambah kasus: baris disusun berurutan, kolom digabung menurut nama; "
                     + "tambah variabel: kolom disandingkan per posisi baris",
            Jenis = "Persiapan data — Data ▸ Merge Files",
            Penemu = "Konsep penggabungan relasional (Codd); perintah acuan Merge Files",
            Tahun = 1970,
            Catatan = "Tambah kasus (Add Cases) menumpuk baris; kolom yang tidak ada di salah "
                      + "satu berkas diisi kosong. Tambah variabel (Add Variables) menyandingkan "
                      + "kolom baris demi baris; nama yang sama diberi akhiran _2, dan baris "
                      + "berlebih diisi kosong. Di sini hanya pratinjau — dataset tidak diubah."
        };

        public static readonly Rumus Rasio = new()
        {
            Nama = "Statistik rasio (Ratio Statistics)",
            Bentuk = "rᵢ = yᵢ/xᵢ;  rerata berbobot = Σyᵢ/Σxᵢ;  AAD = Σ|rᵢ−median|/n;  "
                     + "COD = AAD/median;  PRD = rerata/rerata berbobot",
            Jenis = "Statistik deskriptif — perbandingan dua variabel",
            Penemu = "Dipopulerkan di penilaian pajak (IAAO); perintah acuan Ratio Statistics",
            Tahun = 0,
            Catatan = "PRD mendekati 1 bila rasio tidak dipengaruhi besar nilai. COD dan PRD "
                      + "biasa dipakai menilai keadilan penilaian. Definisi COD/COV berpusat "
                      + "median berbeda di beberapa sumber — rumusnya ditulis eksplisit di "
                      + "keluaran supaya bisa diperiksa."
        };

        public static readonly Rumus KMeans = new()
        {
            Nama = "K-Means (Lloyd / MacQueen)",
            Bentuk = "minimalkan WCSS = Σ_c Σ_{i∈c} ‖xᵢ − μ_c‖²,  μ_c = rerata anggota klaster c",
            Jenis = "Penggerombolan (clustering) — Analyze ▸ Classify ▸ K-Means Cluster",
            Penemu = "Stuart Lloyd (Bell Labs, 1957); istilah 'k-means' oleh James MacQueen",
            Tahun = 1967,
            Catatan = "Iterasi Lloyd: tetapkan tiap titik ke pusat terdekat, lalu pindahkan pusat "
                      + "ke rerata anggotanya, ulangi sampai tak ada titik pindah. Pusat awal "
                      + "memakai k-means++ dengan benih tetap supaya hasilnya bisa diulang. "
                      + "Variabel sebaiknya sebanding; tidak distandardisasi otomatis."
        };

        public static readonly Rumus Transpose = new()
        {
            Nama = "Transposisi (Data ▸ Transpose)",
            Bentuk = "baris menjadi kolom, kolom menjadi baris: mᵢⱼ' = mⱼᵢ",
            Jenis = "Restrukturisasi data — menukar arah tabel",
            Penemu = "Operasi aljabar dasar (matriks transpos)",
            Tahun = 0,
            Catatan = "Di acuan menciptakan dataset baru. Di sini hanya ditampilkan pratinjau "
                      + "transposisi; dataset asli tidak diubah."
        };

        public static readonly Rumus BatangGalat = new()
        {
            Nama = "Batang galat (Graphs ▸ Error Bar)",
            Bentuk = "rerata x̄ = Σxᵢ/n    SE = s/√n    selang: x̄ ± t(1−α/2, n−1)·SE",
            Jenis = "Grafik ringkasan — rerata beserta ketidakpastiannya",
            Penemu = "Selang kepercayaan Student (1908)",
            Tahun = 1908,
            Catatan = "Di acuan rentangnya bisa berupa selang kepercayaan rerata, lipatan galat "
                      + "baku, atau lipatan simpangan baku; ketiganya disediakan di sini. "
                      + "Selang memakai sebaran t dengan derajat bebas n−1, bukan sebaran normal — "
                      + "untuk n kecil selangnya lebih lebar."
        };

        public static readonly Rumus Frekuensi = new()
        {
            Nama = "Frekuensi dan persen kumulatif",
            Bentuk = "fᵢ = Σ 1[x = kᵢ]    pᵢ = fᵢ / n    Fᵢ = Σⱼ≤ᵢ pⱼ",
            Jenis = "Statistik deskriptif — distribusi",
            Penemu = "Konsep dasar sejak abad ke-17; rumus modern dipakai Fisher",
            Tahun = 0,
            Catatan = "Persen valid mengabaikan nilai kosong; kumulatif mengikuti urutan frekuensi."
        };

        public static readonly Rumus UjiTStudent = new()
        {
            Nama = "Uji-t Student (sampel bebas, ragam sama)",
            Bentuk = "t = (x̄₁ − x̄₂) / (sₚ · √(1/n₁ + 1/n₂)),    sₚ² = ((n₁−1)s₁² + (n₂−1)s₂²) / (n₁+n₂−2)",
            Jenis = "Uji hipotesis parametrik — dua rerata",
            Penemu = "William S. Gosset (pseudonim 'Student')",
            Tahun = 1908,
            Catatan = "Mensyaratkan normalitas dan kesamaan ragam. Dipakai ketika n kecil dan σ tidak diketahui."
        };

        public static readonly Rumus UjiTWelch = new()
        {
            Nama = "Uji-t Welch (ragam tidak sama)",
            Bentuk = "t = (x̄₁ − x̄₂) / √(s₁²/n₁ + s₂²/n₂),    df = (s₁²/n₁ + s₂²/n₂)² / (Σ (sᵢ²/nᵢ)² / (nᵢ−1))",
            Jenis = "Uji hipotesis parametrik — dua rerata",
            Penemu = "Bernard L. Welch",
            Tahun = 1947,
            Catatan = "Lebih aman tanpa asumsi kesamaan ragam. df Welch-Satterthwaite."
        };

        public static readonly Rumus UjiTPasangan = new()
        {
            Nama = "Uji-t berpasangan",
            Bentuk = "t = d̄ / (s_d / √n),    dᵢ = x₁ᵢ − x₂ᵢ,    s_d = simpangan baku d",
            Jenis = "Uji hipotesis parametrik — rerata beda berpasangan",
            Penemu = "Student",
            Tahun = 1908,
            Catatan = "Dipakai untuk dua pengukuran pada subjek yang sama (pre-post, kiri-kanan, dsb)."
        };

        public static readonly Rumus UjiTSatuSampel = new()
        {
            Nama = "Uji-t satu sampel",
            Bentuk = "t = (x̄ − μ₀) / (s / √n)",
            Jenis = "Uji hipotesis parametrik — rerata vs nilai acuan",
            Penemu = "Student",
            Tahun = 1908,
            Catatan = "Menguji apakah rerata sama dengan μ₀. Mensyaratkan normalitas atau n cukup besar."
        };

        public static readonly Rumus AnovaSatuArah = new()
        {
            Nama = "ANOVA satu arah",
            Bentuk = "F = MS_between / MS_within,    MS = SS / df",
            Jenis = "Uji hipotesis parametrik — k rerata",
            Penemu = "Ronald A. Fisher",
            Tahun = 1925,
            Catatan = "Memerlukan normalitas dan kesamaan ragam antar kelompok (Levene menguji yang kedua)."
        };

        public static readonly Rumus AnovaWelch = new()
        {
            Nama = "ANOVA Welch (ragam tidak sama)",
            Bentuk = "F = [ Σwᵢ(x̄ᵢ − x̄')² / (k−1) ] / [ 1 + (2(k−2)/(k²−1)) · Σ (1 − wᵢ/Σw)²/(nᵢ−1) ],    wᵢ = nᵢ/sᵢ²",
            Jenis = "Uji hipotesis parametrik — k rerata tanpa asumsi ragam sama",
            Penemu = "Bernard L. Welch",
            Tahun = 1951,
            Catatan = "Dipakai bila uji Levene menolak kesamaan ragam."
        };

        public static readonly Rumus UjiLevene = new()
        {
            Nama = "Uji Levene (kesamaan ragam)",
            Bentuk = "W = ((N − k)/(k − 1)) · (Σ nᵢ(Z̄ᵢ· − Z̄··)²) / ΣΣ(Zᵢⱼ − Z̄ᵢ·)²,    Zᵢⱼ = |xᵢⱼ − cᵢ|",
            Jenis = "Uji asumsi — kehomogenan ragam",
            Penemu = "Howard Levene",
            Tahun = 1960,
            Catatan = "cᵢ = titik pusat simpangan, dan pilihan itulah yang membedakan variannya: "
                      + "rerata (Levene asli), median (Brown–Forsythe, lebih tahan pencilan), "
                      + "atau rerata terpangkas 5%."
        };

        public static readonly Rumus TukeyHsd = new()
        {
            Nama = "Perbandingan berganda Tukey HSD",
            Bentuk = "q = |x̄ᵢ − x̄ⱼ| / √(MS_dalam/2 · (1/nᵢ + 1/nⱼ)),    p = 1 − P(Q ≤ q) "
                     + "dengan Q ~ rentang terstudentkan (k kelompok, df dalam)",
            Jenis = "Uji lanjut pasca ANOVA — semua pasangan",
            Penemu = "John W. Tukey",
            Tahun = 1949,
            Catatan = "Berbeda dari membandingkan t berpasangan: peluangnya dikoreksi sekaligus "
                      + "untuk seluruh pasangan, sehingga peluang galat keluarga tetap α. "
                      + "acuan memakai uji inilah pada ONEWAY /POSTHOC=TUKEY."
        };

        public static readonly Rumus RentangTerstudentkan = new()
        {
            Nama = "Sebaran rentang terstudentkan",
            Bentuk = "P(Q ≤ q) = ∫₀^∞ f_ν(s) · k ∫ φ(z)[Φ(z + q·s) − Φ(z)]^(k−1) dz ds,   "
                     + "dengan s = √(χ²_ν / ν) dan f_ν rapat s",
            Jenis = "Sebaran acuan uji lanjut berganda",
            Penemu = "D. Newman",
            Tahun = 1939,
            Catatan = "Rentang (maks − min) dari k nilai normal baku bebas, dibagi taksiran "
                      + "simpangan baku yang bebas darinya. Di sinilah letak bedanya dengan "
                      + "uji t: yang dibandingkan bukan satu beda terhadap galat bakunya, "
                      + "melainkan rentang terbesar terhadap galat baku. Dihitung dari "
                      + "definisinya, bukan dibaca dari tabel — lihat "
                      + "Distributions.StudentizedRangeCdf. Tukey (1953) memakainya untuk "
                      + "membandingkan semua pasangan rerata sekaligus."
        };

        public static readonly Rumus LeveneMedian = new()
        {
            Nama = "Levene dengan pusat median (Brown–Forsythe)",
            Bentuk = "Zᵢⱼ = |xᵢⱼ − medianᵢ|, lalu W dihitung seperti Levene biasa",
            Jenis = "Uji asumsi — kehomogenan ragam yang tahan pencilan",
            Penemu = "Morton B. Brown dan Alan B. Forsythe",
            Tahun = 1974,
            Catatan = "Median jauh lebih tahan terhadap nilai ekstrem daripada rerata, jadi uji ini "
                      + "tidak mudah dibuat keliru oleh satu amatan pencilan. acuan menampilkannya "
                      + "sebagai baris 'Based on Median'."
        };

        public static readonly Rumus RerataTerpangkas = new()
        {
            Nama = "Rerata terpangkas 5% (dengan bobot pecahan)",
            Bentuk = "buang proporsi p dari tiap ujung; bila p·n tidak bulat, kasus di batas "
                     + "pemotongan diberi bobot sisa: x̄_t = (Σ tengah + (1 − f)(x₍k₎ + x₍n₋₁₋ₖ₎)) / (n − 2k − 2 + 2(1 − f))",
            Jenis = "Ukuran pemusatan yang tahan pencilan",
            Penemu = "Bentuk pemangkasan (trimmed mean) lazim dalam statistik robust",
            Tahun = 0,
            Catatan = "acuan memakai aturan PECAHAN ini untuk Levene 'based on trimmed mean'. "
                      + "Untuk n = 363/27/84, 5% berarti 18,15 / 1,35 / 4,2 kasus. Memotong 18/1/4 "
                      + "memberi F = 56,273 dan 19/2/5 memberi 55,832, sedangkan acuan menulis "
                      + "56,201 — hanya pemotongan pecahan yang menghasilkan 56,2013."
        };

        public static readonly Rumus MannWhitney = new()
        {
            Nama = "Mann-Whitney U",
            Bentuk = "U₁ = n₁n₂ + n₁(n₁+1)/2 − R₁,    U₂ = n₁n₂ − U₁,    Z = (U − μ_U) / σ_U",
            Jenis = "Uji hipotesis nonparametrik — dua kelompok",
            Penemu = "Henry B. Mann, Donald R. Whitney",
            Tahun = 1947,
            Catatan = "Pengganti uji-t bila data tidak normal. Memakai jumlah peringkat (rank)."
        };

        public static readonly Rumus WilcoxonSigned = new()
        {
            Nama = "Wilcoxon signed-rank",
            Bentuk = "W⁺ = Σ rank(dᵢ⁺), dengan dᵢ = x₁ᵢ − x₂ᵢ; hanya |dᵢ| > 0 yang diperingkat",
            Jenis = "Uji hipotesis nonparametrik — pasangan",
            Penemu = "Frank Wilcoxon",
            Tahun = 1945,
            Catatan = "Pengganti uji-t berpasangan bila sebaran dᵢ tidak normal."
        };

        public static readonly Rumus KruskalWallis = new()
        {
            Nama = "Kruskal-Wallis H",
            Bentuk = "H = (12 / (N(N+1))) · Σ (Rⱼ² / nⱼ) − 3(N + 1)",
            Jenis = "Uji hipotesis nonparametrik — k kelompok",
            Penemu = "William H. Kruskal, W. Allen Wallis",
            Tahun = 1952,
            Catatan = "Pengganti ANOVA satu arah bila data tidak normal atau heterogen."
        };

        public static readonly Rumus Pearson = new()
        {
            Nama = "Korelasi Pearson (r)",
            Bentuk = "r = Σ((xᵢ − x̄)(yᵢ − ȳ)) / √(Σ(xᵢ − x̄)² · Σ(yᵢ − ȳ)²)",
            Jenis = "Ukuran hubungan parametrik — dua variabel",
            Penemu = "Karl Pearson",
            Tahun = 1895,
            Catatan = "Mengukur hubungan linear; −1 ≤ r ≤ 1."
        };

        public static readonly Rumus Spearman = new()
        {
            Nama = "Korelasi Spearman (ρ)",
            Bentuk = "ρ = 1 − (6 Σ dᵢ²) / (n(n² − 1)),    dᵢ = peringkat xᵢ − peringkat yᵢ",
            Jenis = "Ukuran hubungan nonparametrik — dua variabel",
            Penemu = "Charles Spearman",
            Tahun = 1904,
            Catatan = "Mengukur hubungan monoton. Tahan terhadap nilai ekstrem dan sebaran tidak normal."
        };

        public static readonly Rumus Kendall = new()
        {
            Nama = "Korelasi Kendall τ-b",
            Bentuk = "τ = (C − D) / Cₘ,    C=konkordan, D=diskordan",
            Jenis = "Ukuran hubungan nonparametrik — dua variabel",
            Penemu = "Maurice G. Kendall",
            Tahun = 1938,
            Catatan = "Untuk data dengan banyak nilai sama, τ-b lebih tepat dibanding τ biasa."
        };

        public static readonly Rumus RegresiLinear = new()
        {
            Nama = "Regresi linear berganda (kuadrat terkecil)",
            Bentuk = "β = (XᵀX)⁻¹ Xᵀy",
            Jenis = "Pemodelan parametrik — variabel terikat dari beberapa prediktor",
            Penemu = "Adrien-Marie Legendre (independen); Carl Friedrich Gauss",
            Tahun = 1805,
            Catatan = "Penaksir tak bias dengan asumsi normalitas, kehomogenan, dan independensi galat."
        };

        public static readonly Rumus KoefisienTerstandar = new()
        {
            Nama = "Koefisien terstandar (Beta)",
            Bentuk = "Beta_j = b_j · (s_j / s_y)",
            Jenis = "Ukuran besaran pengaruh tiap prediktor",
            Penemu = "Ukuran yang lazim dipakai untuk membandingkan kekuatan prediktor",
            Tahun = 0,
            Catatan = "b_j adalah koefisien tak terstandar, s_j simpangan baku prediktor ke-j, "
                      + "dan s_y simpangan baku variabel terikat. Karena satuannya saling "
                      + "menghapus, Beta bisa dibandingkan antar prediktor meski satuannya berbeda "
                      + "(misalnya tahun vs rupiah). Konstanta tidak punya padanan terstandar."
        };

        public static readonly Rumus GalatBakuTaksiran = new()
        {
            Nama = "Galat baku taksiran",
            Bentuk = "s_e = √(SS_residual / (n − k − 1)) = √(MS_residual)",
            Jenis = "Sebaran khas selisih antara nilai amatan dan nilai taksiran",
            Penemu = "Ukuran sebaran galat model",
            Tahun = 0,
            Catatan = "Satuannya sama dengan variabel terikat, jadi bisa dibaca langsung: "
                      + "kira-kira sebesar itulah taksiran model menyimpang dari kenyataan. "
                      + "Berbeda dari galat baku koefisien, yang mengukur ketelitian tiap koefisien."
        };

        public static readonly Rumus ChiSquare = new()
        {
            Nama = "Uji chi-kuadrat untuk tabel kontingensi",
            Bentuk = "χ² = Σ (Oᵢⱼ − Eᵢⱼ)² / Eᵢⱼ,    Eᵢⱼ = (barisᵢ · kolomⱼ) / total",
            Jenis = "Uji hipotesis untuk variabel kategori",
            Penemu = "Karl Pearson",
            Tahun = 1900,
            Catatan = "Memerlukan Eᵢⱼ ≥ 5 untuk sebagian besar sel. Untuk sampel kecil, gunakan uji pasti Fisher."
        };

        public static readonly Rumus CramerV = new()
        {
            Nama = "V Cramér",
            Bentuk = "V = √(χ² / (n · (k − 1))),    k = min(baris, kolom)",
            Jenis = "Ukuran kekuatan hubungan untuk tabel kategori",
            Penemu = "Harald Cramér",
            Tahun = 1946,
            Catatan = "Antara 0 dan 1. V = 0 tidak ada hubungan; V = 1 hubungan sempurna."
        };

        public static readonly Rumus UjiFisher = new()
        {
            Nama = "Uji pasti Fisher",
            Bentuk = "p = (Σ P(T) untuk T dengan P(T) ≤ P(T₀))",
            Jenis = "Uji hipotesis pasti untuk tabel 2×2",
            Penemu = "Ronald A. Fisher",
            Tahun = 1922,
            Catatan = "Dipakai ketika ukuran sampel kecil dan chi-kuadrat tidak valid."
        };

        public static readonly Rumus AlfaCronbach = new()
        {
            Nama = "Alfa Cronbach",
            Bentuk = "α = (k / (k−1)) · (1 − Σσᵢ² / σₜ²)",
            Jenis = "Ukuran konsistensi internal — psikometri",
            Penemu = "Lee J. Cronbach",
            Tahun = 1951,
            Catatan = "α ≥ 0,7 dianggap andal. α > 0,9 sering berarti butir terlalu mirip."
        };

        public static readonly Rumus UjiTanda = new()
        {
            Nama = "Uji tanda (Sign test)",
            Bentuk = "hitung tanda beda dᵢ = xᵢ − yᵢ; abaikan dᵢ = 0; "
                     + "p dua sisi eksak = Σ_{k ≤ min(n₊,n₋)} P(K = k) + Σ_{k ≥ n − min(N)} P(K = k),  K ~ Bin(N, ½)",
            Jenis = "Uji nonparametrik — dua sampel berpasangan",
            Penemu = "John H. Dickey, J. L. Edwards (dikembangkan dari uji tanda); dipakai luas di program rujukan",
            Tahun = 1938,
            Catatan = "Pengganti uji-t berpasangan bila beda tidak simetris. Sama dengan uji binomial "
                      + "satu sampel pada p = ½ terhadap jumlah tanda yang lebih sedikit. Pasangan dengan "
                      + "beda nol dikeluarkan dari N, bukan dihitung sebagai sukses gagal."
        };

        public static readonly Rumus UjiMcNemar = new()
        {
            Nama = "Uji McNemar",
            Bentuk = "hanya sel diskordan b = (0,1) dan c = (1,0) yang dihitung; "
                     + "p eksak = Bin(min(b,c) | b+c, ½) dua sisi;  χ²ₖₖ = (|b−c|−1)² / (b+c),  df = 1",
            Jenis = "Uji nonparametrik — dua ukuran biner berpasangan",
            Penemu = "Quinn McNemar",
            Tahun = 1947,
            Catatan = "Khusus untuk data berpasangan dengan dua kategori. Sel konsisten ((0,0),(1,1)) "
                      + "tidak memberi informasi tentang perubahan, sehingga diabaikan. acuan melaporkan "
                      + "baik p eksak (Exact) maupun chi-kuadrat koreksi kesinambungan."
        };

        public static readonly Rumus KendallW = new()
        {
            Nama = "Kesepakatan Kendall's W",
            Bentuk = "W = 12·S / (k²·n·(n²−1)),  S = Σ(R̄ⱼ − n(k+1)/2)²;  χ² = k(n−1)·W,  df = n−1",
            Jenis = "Ukuran kesepakatan — k penilai",
            Penemu = "Maurice G. Kendall",
            Tahun = 1939,
            Catatan = "Mengukur seberapa sepakat k penilai menilai n subjek. Berkaitan erat dengan uji "
                      + "Friedman: χ² Friedman = k(n−1)·W. Jalur pembanding kedua ada di luar (SciPy): "
                      + "scipy.stats.friedmanchisquare memakai rata-rata ikatan yang sama, lalu "
                      + "W = χ²/(k(n−1)) dibandingkan dengan W di sini di tools/uji_nonpar_related.py. "
                      + "W = 1 berarti sepakat sempurna."
        };

        public static readonly Rumus CochranQ = new()
        {
            Nama = "Uji Cochran's Q",
            Bentuk = "Q = (k−1)(k·ΣGⱼ² − G²) / (kG − ΣLᵢ²),  df = k−1",
            Jenis = "Uji nonparametrik — k ukuran biner berpasangan",
            Penemu = "William G. Cochran",
            Tahun = 1950,
            Catatan = "Bila beberapa ukuran adalah biner "
                      + "dan berpasangan (mis. benar/salah pada k butir), Q menguji apakah proporsi "
                      + "suksesnya sama di semua ukuran. Q ~ χ²(k−1) bila n cukup besar."
        };

        public static readonly Rumus UjiKS2 = new()
        {
            Nama = "Uji Kolmogorov–Smirnov dua sampel",
            Bentuk = "D = sup |F̂₁(x) − F̂₂(x)|;  p = Q_KS(√(n₁·n₂/(n₁+n₂)) · D),  "
                     + "Q_KS(x) = 2·Σ_{j≥1} (−1)^{j−1} e^{−2 j² x²}",
            Jenis = "Uji nonparametrik — dua sampel bebas",
            Penemu = "Nikolai V. Smirnov (perluasan Kolmogorov)",
            Tahun = 1939,
            Catatan = "Menguji apakah dua sampel bebas berasal dari sebaran yang sama, tanpa "
                      + "asumsi bentuk sebaran. Berbeda dari K-S satu sampel yang membandingkan ke "
                      + "sebaran teoretis; di sini kedua sebaran ditaksir dari data. p memakai "
                      + "pendekatan asimptotik sebaran Kolmogorov (sama dengan scipy.stats.ks_2samp "
                      + "method='asymp'). Untuk sampel kecil, acuan melaporkan p eksak."
        };

        public static readonly Rumus MedianTest = new()
        {
            Nama = "Uji median (Mood / Brown–Mood)",
            Bentuk = "gabung semua kelompok → median M; tabel 2×k (di atas / di bawah M);  "
                     + "χ² = Σ (O−E)²/E,  df = k − 1",
            Jenis = "Uji nonparametrik — k sampel bebas",
            Penemu = "Alexander M. Mood; penyempurnaan Brown–Mood",
            Tahun = 1950,
            Catatan = "Pengganti ANOVA bila data sangat tidak normal atau banyak pencilan, karena "
                      + "hanya memakai posisi relatif terhadap median gabungan, bukan nilai mentah. "
                      + "Lebih tahan pencilan tetapi kurang bertenaga daripada Kruskal–Wallis. Kasus "
                      + "sama dengan median digolongkan 'di bawah' (ties='below', bawaan SciPy)."
        };

        public static readonly Rumus ChiGOF = new()
        {
            Nama = "Uji chi-kuadrat kesesuaian (goodness-of-fit)",
            Bentuk = "χ² = Σ (Oᵢ − Eᵢ)² / Eᵢ,  Eᵢ = N · proporsi harapanᵢ,  df = k − 1",
            Jenis = "Uji hipotesis untuk satu variabel kategori",
            Penemu = "Karl Pearson",
            Tahun = 1900,
            Catatan = "Menguji apakah frekuensi pengamatan tiap kategori menyimpang dari frekuensi "
                      + "harapan. Dapat dipercaya bila sebagian besar Eᵢ ≥ 5; "
                      + "bila ada sel kecil, gunakan uji pasti."
        };

        public static readonly Rumus MatriksKorelasi = new()
        {
            Nama = "Matriks korelasi Pearson",
            Bentuk = "R = ZᵀZ / (n − 1),  dengan Z matriks data terstandar (rerata 0, simpangan baku 1)",
            Jenis = "Analisis multivariat — matriks korelasi",
            Penemu = "Karl Pearson",
            Tahun = 1895,
            Catatan = "Dihitung dari data terstandar, bukan dari rumus korelasi pasangan demi pasangan; "
                      + "kedua jalur itu harus memberi angka yang sama, dan hal itu diuji. Baris yang "
                      + "bernilai kosong dibuang lebih dulu (listwise), karena R harus definit positif "
                      + "supaya bisa diurai menjadi nilai eigen."
        };

        public static readonly Rumus UjiBartlett = new()
        {
            Nama = "Uji kebulatan Bartlett (sphericity)",
            Bentuk = "χ² = −(n − 1 − (2p + 5)/6) · ln|R|,  df = p(p − 1)/2",
            Jenis = "Uji hipotesis multivariat — syarat analisis faktor",
            Penemu = "Maurice S. Bartlett",
            Tahun = 1950,
            Catatan = "Menguji H₀: matriks korelasi = matriks identitas (tidak ada korelasi antar "
                      + "variabel). Bila H₀ tidak ditolak, tidak ada struktur bersama untuk "
                      + "difaktorkan. Determinan |R| dihitung dua jalur berbeda — dekomposisi LU dan "
                      + "perkalian nilai eigen — supaya hasilnya bisa saling memeriksa."
        };

        public static readonly Rumus Kmo = new()
        {
            Nama = "KMO (Kaiser–Meyer–Olkin)",
            Bentuk = "KMO = ΣΣ_{i≠j} rᵢⱼ² / (ΣΣ_{i≠j} rᵢⱼ² + ΣΣ_{i≠j} qᵢⱼ²),  "
                     + "qᵢⱼ = −rⁱʲ / √(rⁱⁱ · rʲʲ)  (korelasi parsial, rⁱʲ dari R⁻¹)",
            Jenis = "Analisis multivariat — ukuran kecukupan sampel",
            Penemu = "Henry F. Kaiser; John W. Rice (perluasan MSA)",
            Tahun = 1974,
            Catatan = "Membandingkan besar korelasi dengan besar korelasi parsial: makin kecil "
                      + "korelasi parsialnya, makin layak data difaktorkan. Di bawah 0,50 berarti "
                      + "analisis faktor belum pantas dijalankan pada data itu. KMO per variabel "
                      + "memakai rumus yang sama, tetapi hanya menyilangkan variabel itu dengan "
                      + "yang lain."
        };

        public static readonly Rumus AnalisisFaktor = new()
        {
            Nama = "Analisis faktor (komponen utama & sumbu utama)",
            Bentuk = "R = VΛVᵀ  →  L = V_m · √Λ_m;   komunalitas hᵢ = Σ_f lᵢf²;   "
                     + "sumbu utama mengulang R* = R dengan diagonal diganti hᵢ sampai konvergen",
            Jenis = "Analisis multivariat — pereduksi dimensi",
            Penemu = "Karl Pearson (komponen utama); Charles Spearman (faktor)",
            Tahun = 1901,
            Catatan = "Komponen utama memakai nilai eigen matriks korelasi utuh (Σλ = p); sumbu utama "
                      + "memakai matriks yang diagonalnya diganti komunalitas, dimulai dari korelasi "
                      + "ganda kuadrat, sehingga Σλ < p. Nilai eigennya dihitung dengan rotasi Jacobi "
                      + "siklik dan dibandingkan dengan numpy.linalg.eigh. Tanda vektor eigen tidak "
                      + "ditentukan rumusnya, jadi dipatok: komponen terbesar dibuat positif."
        };

        public static readonly Rumus Varimaks = new()
        {
            Nama = "Rotasi varimaks (ortogonal)",
            Bentuk = "maksimalkan Q = Σ_f [ p·Σᵢ lᵢf⁴ − (Σᵢ lᵢf²)² ] / p²;   "
                     + "sudut per pasangan: tan 4φ = [D − 2AB/p] / [C − (A² − B²)/p]",
            Jenis = "Analisis multivariat — rotasi faktor",
            Penemu = "Henry F. Kaiser",
            Tahun = 1958,
            Catatan = "Rotasi ortogonal: matriks rotasinya T memenuhi TᵀT = I, sehingga komunalitas "
                      + "tiap variabel TIDAK berubah — yang berubah hanya cara ragam itu dibagi antar "
                      + "faktor. Kriteria Q dipakai untuk membuktikan rotasinya benar-benar naik. "
                      + "acuan memakai varimaks dengan normalisasi Kaiser secara bawaan; yang di sini "
                      + "tanpa normalisasi itu, dan karena itu cocok dengan statsmodels."
        };

        public static readonly Rumus JarakEuclid = new()
        {
            Nama = "Jarak Euclid",
            Bentuk = "d(i,j) = √( Σ_c (x_ic − x_jc)² )",
            Jenis = "Analisis multivariat — ukuran jarak",
            Penemu = "Euklides (dipakai luas sejak Legendre & Gauss)",
            Tahun = 1805,
            Catatan = "Jarak yang dipakai semua cara penggabungan di sini, dan syarat bagi centroid, "
                      + "median, dan Ward: ketiganya hanya bermakna bila jaraknya Euclid. Secara "
                      + "bawaan variabelnya diseragamkan lebih dulu (z-skor), kalau tidak variabel "
                      + "bersatuan besar akan mendominasi jaraknya."
        };

        public static readonly Rumus LanceWilliams = new()
        {
            Nama = "Pembaruan Lance–Williams (penggabungan hierarkis)",
            Bentuk = "d(ij,k) = αᵢ·d(i,k) + αⱼ·d(j,k) + β·d(i,j) + γ·|d(i,k) − d(j,k)|;  "
                     + "ward: d²(ij,k) = [(nᵢ+n_k)d²(i,k) + (nⱼ+n_k)d²(j,k) − n_k d²(i,j)] / (nᵢ+nⱼ+n_k)",
            Jenis = "Pengelompokan — penggabungan berurutan",
            Penemu = "G. N. Lance & W. T. Williams",
            Tahun = 1967,
            Catatan = "Satu rumus berbobot yang mencakup single, complete, average, centroid, median, "
                      + "dan Ward — hanya koefisiennya yang berganti. WPGMC (median) dan centroid "
                      + "dapat menghasilkan pembalikan (tinggi penggabungan yang menurun), dan itu "
                      + "bukan kesalahan hitung melainkan sifat metodenya."
        };

        public static readonly Rumus KorelasiKofenatik = new()
        {
            Nama = "Korelasi kofenatik",
            Bentuk = "r = korelasi Pearson antara jarak asli d(i,j) dan jarak kofenatik c(i,j), "
                     + "c(i,j) = tinggi penggabungan tempat i dan j pertama kali sekandum",
            Jenis = "Pengelompokan — ukuran mutu pohon",
            Penemu = "Robert R. Sokal & F. James Rohlf",
            Tahun = 1962,
            Catatan = "Seberapa setia dendrogramnya menampilkan jarak yang sebenarnya. Makin dekat ke "
                      + "1, makin sedikit distorsi. Berguna justru karena ia membandingkan dua hal "
                      + "yang berbeda: matriks jarak yang dihitung langsung, dan jarak yang "
                      + "\"diceritakan\" oleh pohonnya."
        };

        public static readonly Rumus KurvaRoc = new()
        {
            Nama = "Kurva ROC (karakteristik operasi penerima)",
            Bentuk = "sensitivitas = TP/(TP+FN);  spesifisitas = TN/(TN+FP);  "
                     + "kurva = (1 − spesifisitas, sensitivitas) untuk setiap ambang",
            Jenis = "Analisis klasifikasi — mutu penanda/tes",
            Penemu = "Berasal dari radar masa Perang Dunia II (1941); dipakai luas untuk diagnosis "
                     + "medis sejak 1960-an",
            Tahun = 1941,
            Catatan = "Satu titik per ambang yang berbeda. Kurva menyentuh (1,1) bila ambangnya "
                      + "sangat longgar, dan (0,0) bila sangat ketat; penanda yang berguna melengkung "
                      + "ke pojok kiri atas. AUC = luas di bawahnya."
        };

        public static readonly Rumus AucMannWhitney = new()
        {
            Nama = "AUC lewat statistik U Mann–Whitney",
            Bentuk = "AUC = U / (n₁·n₀),  U = Σ_{i∈positif} Σ_{j∈negatif} [ sᵢ > sⱼ ] + ½·[ sᵢ = sⱼ ]",
            Jenis = "Analisis klasifikasi — luas di bawah kurva ROC",
            Penemu = "Henry B. Mann & Donald R. Whitney",
            Tahun = 1947,
            Catatan = "Identitas yang membuat AUC bisa diuji silang: AUC persis sama dengan peluang "
                      + "sebuah amatan positif memperoleh skor lebih tinggi daripada amatan negatif. "
                      + "Karena itu AUC dihitung dua jalur — trapesium di bawah kurva dan U/(n₁·n₀) — "
                      + "dan keduanya harus sepakat."
        };

        public static readonly Rumus HanleyMcNeil = new()
        {
            Nama = "Galat baku AUC (Hanley–McNeil)",
            Bentuk = "Q₁ = A/(2 − A),  Q₂ = 2A²/(1 + A),  "
                     + "SE = √( [A(1−A) + (n₁−1)(Q₁−A²) + (n₀−1)(Q₂−A²)] / (n₁·n₀) )",
            Jenis = "Analisis klasifikasi — ketidakpastian AUC",
            Penemu = "James A. Hanley & Barbara J. McNeil",
            Tahun = 1982,
            Catatan = "AUC bukan proporsi, jadi galat bakunya tidak mengikuti √(p(1−p)/n). Rumus ini "
                      + "memasukkan dua macam ragam di balik AUC (Q₁ untuk pasangan searah, Q₂ untuk "
                      + "pasangan silang). Tanpa itu, selang kepercayaannya akan terlalu sempit."
        };

        public static readonly Rumus PosisiPlotting = new()
        {
            Nama = "Posisi plotting Blom",
            Bentuk = "pᵢ = (i − 3/8) / (n + 1/4),   i = 1..n",
            Jenis = "Statistik deskriptif — posisi untuk plot peluang",
            Penemu = "Gunnar Blom",
            Tahun = 1958,
            Catatan = "Dipakai karena `scipy.stats.probplot` memakainya, dan karena pᵢ = i/(n+1) "
                      + "maupun (i − 0,5)/n memberi kuantil yang berbeda di ujung-ujung data — bagian "
                      + "yang justru paling menentukan pada plot normal. Pilihan ini dicatat di sini "
                      + "supaya tidak tampak sebagai kebetulan."
        };

        public static readonly Rumus PlotQQ = new()
        {
            Nama = "Plot Q–Q normal (quantile–quantile)",
            Bentuk = "sumbu x = Φ⁻¹(pᵢ),  sumbu y = x₍ᵢ₎ (data terurut);  "
                     + "garis rujukan y = x̄ + s·Φ⁻¹(pᵢ)",
            Jenis = "Statistik deskriptif — pemeriksaan kenormalan secara visual",
            Penemu = "Martin B. Wilk & Ram Gnanadesikan",
            Tahun = 1968,
            Catatan = "Titik yang lurus pada garis rujukan berarti bentuk sebarannya cocok dengan "
                      + "normal; yang melengkung di ujung berarti ekor lebih berat atau lebih ringan "
                      + "daripada normal. Beda dengan P–P: Q–Q peka di ekor, P–P peka di bagian "
                      + "tengah sebaran."
        };

        public static readonly Rumus PlotPP = new()
        {
            Nama = "Plot P–P normal (probability–probability)",
            Bentuk = "sumbu x = Φ((x₍ᵢ₎ − x̄)/s),  sumbu y = pᵢ",
            Jenis = "Statistik deskriptif — pemeriksaan kenormalan secara visual",
            Penemu = "Berasal dari sebaran kumulatif empiris (Kolmogorov)",
            Tahun = 1933,
            Catatan = "Membandingkan dua peluang kumulatif, sehingga penyimpangan di bagian tengah "
                      + "sebaran tampak lebih jelas daripada di ekor — kebalikan dari Q–Q. Rerata dan "
                      + "simpangan baku dipakai dari datanya sendiri, sama seperti lazimnya."
        };

        public static readonly Rumus KaplanMeier = new()
        {
            Nama = "Estimator Kaplan–Meier (product-limit)",
            Bentuk = "Ŝ(t) = Π_{tᵢ ≤ t} (1 − dᵢ / nᵢ)",
            Jenis = "Survival — estimasi fungsi survival",
            Penemu = "Edward L. Kaplan & Paul Meier",
            Tahun = 1958,
            Catatan = "Estimator nonparametrik yang tidak mengasumsikan bentuk sebaran waktu survival. "
                      + "Yang dibutuhkan hanya waktu kejadian/event dan indikator sensor (1=event, 0=sensor)."
        };

        public static readonly Rumus LogRank = new()
        {
            Nama = "Uji log-rank (Mantel–Cox)",
            Bentuk = "χ² = (Σⱼ (Oⱼ − Eⱼ))² / Σⱼ Vⱼ   dengan  Eⱼ = nⱼ·d/n",
            Jenis = "Survival — uji perbandingan kurva survival",
            Penemu = "Nathan Mantel",
            Tahun = 1966,
            Catatan = "Membandingkan survival antar dua atau lebih grup. Setiap waktu kejadian memberi "
                      + "kontribusi hipergeometrik; sensor tidak mempengaruhi statistik, hanya menentukan "
                      + "siapa yang masih berisiko."
        };

        public static readonly Rumus Acf = new()
        {
            Nama = "Fungsi autokorelasi (ACF)",
            Bentuk = "rₖ = Σₜ (xₜ − x̄)(xₜ₋ₖ − x̄) / Σₜ (xₜ − x̄)²",
            Jenis = "Deret waktu — autokorelasi",
            Penemu = "George Udny Yule",
            Tahun = 1926,
            Catatan = "Mengukur kekuatan hubungan deret dengan dirinya sendiri pada jarak k. "
                      + "Meluruh perlahan menandakan deret tidak stasioner."
        };

        public static readonly Rumus Pacf = new()
        {
            Nama = "Autokorelasi parsial (PACF) — rekursi Levinson–Durbin",
            Bentuk = "φₖₖ = (rₖ − Σⱼ φₖ₋₁,ⱼ·rₖ₋ⱼ) / (1 − Σⱼ φₖ₋₁,ⱼ·rⱼ)",
            Jenis = "Deret waktu — autokorelasi parsial",
            Penemu = "Norman Levinson & James Durbin",
            Tahun = 1960,
            Catatan = "Hubungan pada lag k SETELAH hubungan lewat lag-lag di antaranya "
                      + "dihilangkan. Terpotong tajam di lag p menandakan kandidat AR(p)."
        };

        public static readonly Rumus LjungBox = new()
        {
            Nama = "Uji Ljung–Box (deret putih)",
            Bentuk = "Q = n(n+2) · Σₖ₌₁ᵐ rₖ²/(n−k)  ~  χ²(df = m)",
            Jenis = "Deret waktu — uji keberadaan autokorelasi",
            Penemu = "Greta M. Ljung & George E. P. Box",
            Tahun = 1978,
            Catatan = "Menguji autokorelasi sampai lag m SEKALIGUS, bukan satu-satu. "
                      + "Penyempurnaan dari uji Box–Pierce yang bias pada sampel kecil."
        };

        public static readonly Rumus BootstrapResample = new()
        {
            Nama = "Bootstrap — resample dengan pengembalian",
            Bentuk = "X*ᵦ = { x*₁, …, x*ₙ } dengan x*ᵢ ~ Seragam{ x₁, …, xₙ }",
            Jenis = "Bootstrap — dasar resampling",
            Penemu = "Bradley Efron",
            Tahun = 1979,
            Catatan = "Data yang ada diperlakukan sebagai wakil populasi. Dari data itu ditarik "
                      + "sampel baru berukuran sama DENGAN pengembalian, diulang B kali. Sebaran "
                      + "taksiran yang terkumpul itulah yang dipakai menaksir ketelitian, tanpa "
                      + "mengasumsikan sebarannya normal."
        };

        public static readonly Rumus BootstrapPersentil = new()
        {
            Nama = "Selang kepercayaan bootstrap — persentil",
            Bentuk = "CI = [ θ*₍α/2₎ , θ*₍1−α/2₎ ]",
            Jenis = "Bootstrap — selang kepercayaan",
            Penemu = "Bradley Efron",
            Tahun = 1979,
            Catatan = "Ujung selang diambil langsung dari kuantil sebaran hasil resample. "
                      + "Sederhana dan tidak pernah keluar dari rentang data, tetapi ikut "
                      + "bergeser bila sebaran taksirannya miring."
        };

        public static readonly Rumus BootstrapBca = new()
        {
            Nama = "Selang kepercayaan bootstrap — BCa",
            Bentuk = "CI = [ Q*(Φ(z₁)) , Q*(Φ(z₂)) ] dengan z₁ = z₀ + (z₀+z₍α/2₎)/(1−a(z₀+z₍α/2₎))",
            Jenis = "Bootstrap — selang kepercayaan terkoreksi",
            Penemu = "Bradley Efron & Robert Tibshirani",
            Tahun = 1993,
            Catatan = "Mengoreksi dua hal sekaligus: bias lewat z₀ dan kemiringan lewat a "
                      + "(percepatan). z₀ mengukur seberapa sering taksiran resample jatuh di "
                      + "bawah taksiran asli; a mengukur kepekaan taksiran terhadap satu-dua "
                      + "amatan. Bila z₀ = 0 dan a = 0, BCa kembali menjadi persentil."
        };

        public static readonly Rumus PercepatanJackknife = new()
        {
            Nama = "Percepatan jackknife (a) untuk BCa",
            Bentuk = "a = Σᵢ (θ̄_(·) − θ̂_(i))³ / (6 · [Σᵢ (θ̄_(·) − θ̂_(i))²]^{3/2})",
            Jenis = "Bootstrap — besaran turunan BCa",
            Penemu = "Bradley Efron & Robert Tibshirani",
            Tahun = 1993,
            Catatan = "θ̂_(i) adalah taksiran yang dihitung SETELAH amatan ke-i dibuang. "
                      + "Bentuknya menyerupai kemiringan sebaran taksiran; tandanya menunjukkan "
                      + "ke arah mana selang perlu digeser."
        };

        public static readonly Rumus Wls = new()
        {
            Nama = "Regresi kuadrat terkecil berbobot (WLS)",
            Bentuk = "β̂ = (X′WX)⁻¹ X′Wy    dengan W = diag(w₁, …, wₙ)",
            Jenis = "Regresi — ragam galat tidak seragam",
            Penemu = "Carl Friedrich Gauss (perluasan kuadrat terkecil)",
            Tahun = 1821,
            Catatan = "Bentuk yang sama dengan OLS; yang berubah hanya matriks normalnya, "
                      + "sebab setiap amatan dihitung wᵢ kali. Bobot yang benar adalah "
                      + "kebalikan ragam galatnya, wᵢ = 1/σᵢ². Pada data heteroskedastik, "
                      + "OLS tetap tak bias tetapi tidak lagi paling teliti — WLS menutup "
                      + "celah itu."
        };

        public static readonly Rumus WlsGalatBaku = new()
        {
            Nama = "Galat baku WLS dan derajat bebas efektif",
            Bentuk = "σ̂² = Σ wᵢeᵢ² / (n − k)    SE(β̂ⱼ) = √(σ̂² · [ (X′WX)⁻¹ ]ⱼⱼ)",
            Jenis = "Regresi — ketelitian taksiran WLS",
            Penemu = "Alexander Aitken",
            Tahun = 1935,
            Catatan = "Pembaginya tetap (n − k), bukan (Σwᵢ − k). Bobot mengubah BERAPA "
                      + "banyak setiap amatan berbicara, bukan berapa banyak amatan yang "
                      + "ada. Memakai derajat bebas berbobot akan menghasilkan galat baku "
                      + "yang tidak sama dengan acuan mana pun."
        };

        public static readonly Rumus RegresiPoisson = new()
        {
            Nama = "Regresi Poisson dengan taut log (IRLS)",
            Bentuk = "ln(μ) = x′β    μ = e^{x′β}    "
                     + "iterasi: β⁽ᵗ⁺¹⁾ = (X′WX)⁻¹X′Wz, wᵢ = μᵢ, zᵢ = ηᵢ + (yᵢ − μᵢ)/μᵢ",
            Jenis = "GLM — respons cacahan",
            Penemu = "John Nelder & Robert Wedderburn (IRLS); Poisson, 1837",
            Tahun = 1972,
            Catatan = "Tautnya kanonik, jadi bobot kerjanya sederhana: wᵢ = μᵢ. Setiap "
                      + "iterasi hanyalah satu WLS, dan itulah sebabnya Poisson ada satu "
                      + "berkas dengan WLS. Karena sebaran Poisson menetapkan "
                      + "ragam = rerata, tidak ada parameter dispersi yang ditaksir: "
                      + "galat baku memakai (X′WX)⁻¹ apa adanya."
        };

        public static readonly Rumus DeviancePoisson = new()
        {
            Nama = "Deviance dan log-kemungkinan Poisson",
            Bentuk = "D = 2 Σ [ yᵢ·ln(yᵢ/μᵢ) − (yᵢ − μᵢ) ]    "
                     + "ln L = Σ [ yᵢ·ln μᵢ − μᵢ − ln Γ(yᵢ + 1) ]",
            Jenis = "GLM — kesesuaian dan pembandingan model",
            Penemu = "Robert Wedderburn",
            Tahun = 1974,
            Catatan = "Deviance mengukur jarak model terhadap model jenuh; ia berperan "
                      + "seperti jumlah kuadrat residual pada regresi biasa. AIC memakai "
                      + "konvensi statsmodels, AIC = −2·ln L + 2k, dengan k mencakup "
                      + "konstanta. Memakai k tanpa konstanta akan menghasilkan AIC yang "
                      + "berbeda 2 dari acuan."
        };

        public static readonly Rumus RegresiQuantile = new()
        {
            Nama = "Regresi kuantil (Koenker–Bassett)",
            Bentuk = "minβ Σ ρ_τ(yᵢ − xᵢ′β),  ρ_τ(u) = u·(τ − 1{u<0})   "
                     + "IWLS: x*ᵢ = xᵢ/wᵢ,  wᵢ = |rᵢ|·(τ bila rᵢ<0, 1−τ bila rᵢ≥0),  "
                     + "β = (x*′X)⁻¹x*′y",
            Jenis = "Regresi robust — seluruh sebaran respon",
            Penemu = "Roger Koenker & Gilbert Bassett",
            Tahun = 1978,
            Catatan = "Berbeda dari OLS yang meminimumkan jumlah kuadrat residual (rekata "
                      + "rerata), regresi kuantil merekatkan suatu *kuantil* τ ∈ (0,1) dari y "
                      + "padanan x. B(τ) adalah slopa hubungan antar-kuantil, bukan rerata, "
                      + "sehingga cocok untuk data heteroskedastik atau efek asimetris. Galat "
                      + "baku memakai sandwich heteroskedastisitas-robust (kepadatan sparsity "
                      + "f(0) lewat kernel Epanechnikov + lebar pita Hall–Sheather), dan uji "
                      + "t memakai distribusi Student-t dengan df = n − k."
        };

        public static readonly Rumus GlmGamma = new()
        {
            Nama = "GLM Gamma (taut log)",
            Bentuk = "ln μᵢ = xᵢ′β,  yᵢ ~ Gamma(rerata μᵢ, ragam φμᵢ²)    "
                     + "IRLS: wᵢ = 1,  zᵢ = ηᵢ + (yᵢ − μᵢ)/μᵢ,  "
                     + "β = (X′WX)⁻¹X′Wz",
            Jenis = "Model linear terampat — respon positif miring",
            Penemu = "John Nelder & Robert Wedderburn",
            Tahun = 1972,
            Catatan = "Untuk respon positif yang ragamnya membesar dengan kuadrat "
                      + "rerata (waktu tunggu, biaya, curah hujan). Taut log membuat "
                      + "prediksi selalu positif dan koefisien terbaca sebagai efek "
                      + "multiplikatif: exp(B) = 1,5 berarti respon naik 50% per "
                      + "satuan prediktor. Skala φ diestimasi dari Pearson χ²/df, "
                      + "bukan 1 — galat baku memakai φ·(X′WX)⁻¹."
        };

        public static readonly Rumus GlmBinom = new()
        {
            Nama = "GLM Binomial (taut logit)",
            Bentuk = "logit(μᵢ) = ln(μᵢ/(1−μᵢ)) = xᵢ′β,  kᵢ ~ Binomial(mᵢ, μᵢ)    "
                     + "IRLS: wᵢ = mᵢμᵢ(1−μᵢ),  zᵢ = ηᵢ + (yᵢ − μᵢ)/(μᵢ(1−μᵢ)),  "
                     + "β = (X′WX)⁻¹X′Wz",
            Jenis = "Model linear terampat — proporsi/cacahan berhasil",
            Penemu = "John Nelder & Robert Wedderburn; logit oleh Joseph Berkson",
            Tahun = 1972,
            Catatan = "Untuk proporsi atau cacahan berhasil dari m percobaan. "
                      + "Taut logit memetakan (0,1) ke seluruh garis nyata sehingga "
                      + "prediksi tak pernah keluar rentang. Skala tetap 1 menurut "
                      + "definisi Binomial. Uji tiap faktor memakai penurunan "
                      + "deviansi terhadap sebaran χ²."
        };

        public static readonly Rumus CampurModel = new()
        {
            Nama = "Model campuran intersep acak",
            Bentuk = "yᵢ = xᵢ′β + u_g(i) + εᵢ,  u_g ~ N(0, σ²u),  εᵢ ~ N(0, σ²)    "
                     + "BLUP: û_g = λ·Σe_g/(1 + λn_g),  λ = σ²u/σ²,  "
                     + "ICC = σ²u/(σ²u + σ²)",
            Jenis = "Model campuran — efek tetap + intersep acak kelompok",
            Penemu = "Charles Henderson (BLUP); lazim REML oleh Desmond Patterson & Robin Thompson",
            Tahun = 1971,
            Catatan = "Kalau amatan di dalam kelompok yang sama saling berkorelasi "
                      + "(murid di sekolah yang sama, pasien di rumah sakit yang "
                      + "sama), OLS menganggap semuanya bebas dan galat bakunya "
                      + "terlalu kecil. Intersep acak memberi tiap kelompok "
                      + "simpangannya sendiri. BLUP menyusutkan dugaan kelompok "
                      + "kecil ke nol — makin kecil kelompoknya, makin kuat susutnya."
        };

        public static readonly Rumus CampurReml = new()
        {
            Nama = "Taksiran REML komponen ragam",
            Bentuk = "−2ℓ_R(λ) = (n−p)·ln σ² + Σ_g ln(1+λn_g) + ln|X′Ṽ⁻¹X| + (n−p)",
            Jenis = "Model campuran — kriteria kemungkinan terbatas",
            Penemu = "Desmond Patterson & Robin Thompson",
            Tahun = 1971,
            Catatan = "REML menaksir ragam SETELAH mengeluarkan efek tetap — "
                      + "derajat bebasnya n−p, bukan n — sehingga taksirannya "
                      + "tidak bias ke bawah seperti ML. Profilnya hanya "
                      + "bergantung satu angka λ = σ²u/σ², jadi dicari dengan "
                      + "pencarian bagian-emas sampai selang bracketnya sempit."
        };

        public static readonly Rumus GlmmModel = new()
        {
            Nama = "GLMM intersep acak (Binomial / Poisson)",
            Bentuk = "g(μᵢ) = xᵢ′β + u_g(i),  u_g ~ N(0, σ²u)    "
                     + "Binomial: g = logit, μ = peluang;  Poisson: g = log, μ = rerata cacahan",
            Jenis = "Model campuran terampat — respon non-normal berkelompok",
            Penemu = "Norman Breslow & Donald Clayton (PQL)",
            Tahun = 1993,
            Catatan = "Kalau responnya 0/1 atau cacahan DAN amatan mengelompok "
                      + "(pasien di rumah sakit yang sama), GLM biasa menganggap "
                      + "semuanya bebas dan galat bakunya terlalu kecil, sedangkan "
                      + "model campuran biasa mengasumsikan respon normal. GLMM "
                      + "menggabungkan keduanya: taut non-linear untuk responnya, "
                      + "intersep acak untuk kelompoknya."
        };

        public static readonly Rumus GlmmPql = new()
        {
            Nama = "Taksiran PQL (kemungkinan kuasi terpenalti)",
            Bentuk = "z = η + (y−μ)/d,  w = d²/V,  lalu LMM-berbobot atas (z, w) "
                     + "diulang sampai β dan λ = σ²u/σ² diam",
            Jenis = "Model campuran terampat — algoritma taksiran",
            Penemu = "Norman Breslow & Donald Clayton",
            Tahun = 1993,
            Catatan = "PQL melinearisasi model di sekitar taksiran sekarang "
                      + "(respon-kerja z, bobot w), menyelesaikan LMM berbobotnya "
                      + "dengan REML profil satu-dimensi seperti model campuran "
                      + "biasa, lalu mengulang. Skala kerja σ² harus ≈1 bila "
                      + "modelnya pas — jauh dari 1 menandakan sebaran-berlebih."
        };

        public static readonly Rumus PrincalsModel = new()
        {
            Nama = "PRINCALS — sesatan penskalaan optimal",
            Bentuk = "L = (1/nm) Σ_j ‖X − G_j Q_j‖²,  X′X = nI    "
                     + "nominal: Q bebas; ordinal: Q monoton; numerik: baku",
            Jenis = "Reduksi dimensi — penskalaan optimal variabel campuran",
            Penemu = "Jan de Leeuw & sistem Gifi (Leiden)",
            Tahun = 1980,
            Catatan = "Tiap variabel kategorik diwakili indikator G dan "
                      + "kuantifikasi kategorinya Q; skor objek X dipilih "
                      + "bersama Q untuk meminimumkan sesatan. Nominal bebas "
                      + "(sentroid), ordinal dipadatkan monoton (PAVA), "
                      + "numerik langsung dibakukan. ALS mengulang "
                      + "kuantifikasi ↔ skor sampai sesatannya diam."
        };

        public static readonly Rumus PrincalsUkuran = new()
        {
            Nama = "Eigen, diskriminasi, dan alfa PRINCALS",
            Bentuk = "η²_js = q′Dq/n,  λ_s = rerata η²_s,  "
                     + "α_s = m/(m−1)·(1 − 1/λ_s)",
            Jenis = "Reduksi dimensi — ukuran mutu dimensi",
            Penemu = "Jan de Leeuw & sistem Gifi (Leiden)",
            Tahun = 1980,
            Catatan = "Ukuran diskriminasi η² = proporsi ragam skor objek "
                      + "yang diterangkan variabel itu. Eigen = reratanya. "
                      + "Alfa Cronbach memakai eigen dan boleh negatif — "
                      + "artinya dimensinya lebih buruk daripada tanpa model. "
                      + "Identitas yang selalu berlaku: "
                      + "L + Σλ = banyak dimensi."
        };

        public static readonly Rumus HutanModel = new()
        {
            Nama = "Random forest (bagging + subruang acak)",
            Bentuk = "B pohon CART atas contoh bootstrap; tiap belahan "
                     + "menimbang mCoba fitur acak; duga = suara/rerata",
            Jenis = "Ensembel pohon — penstabil varians CART",
            Penemu = "Leo Breiman",
            Tahun = 2001,
            Catatan = "Satu pohon CART berayun bila datanya sedikit berubah. "
                      + "Hutan menumbuhkan ratusan pohon atas versi data yang "
                      + "diacak-dua-kali (baris di-bootstrap, fitur "
                      + "di-subruang) lalu memungut suara — ayunannya "
                      + "saling meniadakan. mCoba bawaan: akar(p) untuk "
                      + "klasifikasi, p/3 untuk regresi."
        };

        public static readonly Rumus HutanOob = new()
        {
            Nama = "Galat OOB dan kepentingan permutasi",
            Bentuk = "OOB: duga baris-i dari pohon yang tak-memuatnya; "
                     + "penting(f) = galat OOB setelah acak-f − sebelumnya",
            Jenis = "Ensembel pohon — validasi dalam dan kepentingan",
            Penemu = "Leo Breiman",
            Tahun = 2001,
            Catatan = "Tiap pohon tak-melihat ~37% baris (out-of-bag): "
                      + "menduga baris-baris itu berarti menguji tanpa "
                      + "menyisihkan data. Pentingnya fitur diukur dengan "
                      + "merusak satu fitur (acak) dan melihat berapa "
                      + "banyak akurasi jatuh — boleh negatif bila fitur "
                      + "tak membantu."
        };

        public static readonly Rumus Chaid = new()
        {
            Nama = "CHAID — gabung khi-kuadrat + belah Bonferroni",
            Bentuk = "gabung: p(χ²) pasangan > α; belah: p_adj = p·C(c−1,r−1) < α",
            Jenis = "Pohon keputusan — belahan multi-arah kategorik",
            Penemu = "Gordon V. Kass",
            Tahun = 1980,
            Catatan = "Kategori yang tak-berbeda nyata digabung dulu "
                      + "(nominal: pasangan mana pun; ordinal: hanya yang "
                      + "bersebelahan), lalu tabel gabungannya diuji. "
                      + "Pengali Bonferroni = banyaknya cara mempartisi c "
                      + "kategori menjadi r grup — hitungan eksak untuk "
                      + "gabungan-bersebelahan, hampiran konservatif untuk "
                      + "gabungan-bebas serakah. Seri dimenangi yang pertama "
                      + "sehingga hasilnya deterministik."
        };

        public static readonly Rumus C45 = new()
        {
            Nama = "C4.5 — nisbah gain + pangkas pesimis",
            Bentuk = "GR = Gain/InfoBelah; pangkas bila N·UCF(daun) ≤ N·UCF(cabang)",
            Jenis = "Pohon keputusan — nisbah gain dengan pemangkasan",
            Penemu = "J. Ross Quinlan",
            Tahun = 1993,
            Catatan = "Gain dibagi info-belah supaya prediktor berkategori "
                      + "banyak tak-diuntungkan. Numerik dibelah biner pada "
                      + "titik-tengah. Pohon penuh lalu dipangkas dari bawah: "
                      + "simpul diganti daun/cabang-terbaik bila galat-atas "
                      + "binomialnya (CF, default 0,25) lebih kecil. Seri "
                      + "dimenangi yang pertama sehingga deterministik."
        };

        public static readonly Rumus BoostingGradien = new()
        {
            Nama = "Gradient boosting (galat kuadrat)",
            Bentuk = "F_0 = rerata(y); r = y − F; pohon ~ r; F += lr·pohon",
            Jenis = "Ensembel pohon — penekan bias sekuensial (regresi)",
            Penemu = "Jerome H. Friedman",
            Tahun = 2001,
            Catatan = "Tiap ronde memasang pohon kecil pada SISA (yang belum "
                      + "diterangkan), lalu menambahkannya dengan susut lr. "
                      + "SSE latih turun setiap ronde — kalau tidak, ada bug. "
                      + "Deterministik penuh: tanpa undian, benih tak-diperlukan."
        };

        public static readonly Rumus BoostingSamme = new()
        {
            Nama = "AdaBoost SAMME (suara berbobot alfa)",
            Bentuk = "err = Σw(salah)/Σw; α = lr·(ln((1−err)/err) + ln(K−1))",
            Jenis = "Ensembel pohon — penekan bias sekuensial (klasifikasi)",
            Penemu = "Yoav Freund & Robert Schapire; SAMME oleh Ji Zhu dkk.",
            Tahun = 1997,
            Catatan = "Baris yang salah diduga ditimbang-ulang supaya ronde "
                      + "berikutnya fokus ke yang sulit; suara akhir berbobot "
                      + "alfa. Berhenti bila sempurna atau lebih-buruk-dari-acak. "
                      + "Deterministik penuh seperti gradien di atas."
        };

        public static readonly Rumus ImputasiMice = new()
        {
            Nama = "MICE — chained equations Bayes",
            Bentuk = "numerik: y_hilang = Xβ* + σ·z; biner: undi(p*) dengan "
                     + "β* ~ Normal(MAP, kov)",
            Jenis = "Data hilang — imputasi berganda",
            Penemu = "Donald B. Rubin (1987); MICE oleh Stef van Buuren",
            Tahun = 1987,
            Catatan = "Tiap variabel berhampas dimodelkan dari yang lain "
                      + "bergiliran: taksiran posterior (bukan nilai tengah) "
                      + "lalu undian — sehingga ragam antar-imputasi jujur. "
                      + "Biner tepat 2 aras (logit-Laplace); nominal multi "
                      + "belum didukung v1."
        };

        public static readonly Rumus ImputasiRubin = new()
        {
            Nama = "Aturan Rubin (pooling)",
            Bentuk = "Q̄ = rerata(Q); T = W + (1+1/m)B; df Rubin; FMI",
            Jenis = "Data hilang — gabungan m imputasi",
            Penemu = "Donald B. Rubin",
            Tahun = 1987,
            Catatan = "Galat baku gabungan = ragam-dalam (W) + ragam-antar "
                      + "(B): imputasi-tunggal meremehkan galat karena "
                      + "menganggap isian pasti. FMI = proporsi informasi "
                      + "yang hilang."
        };

        public static readonly Rumus CoxModel = new()
        {
            Nama = "Regresi Cox proportional hazards",
            Bentuk = "L(β) = Π_j exp(x_(j)′β) / Σ_{i∈R_j} exp(x_i′β)    "
                     + "h(t|x) = h0(t)·exp(x′β),  HR = exp(β)",
            Jenis = "Analisis ketahanan — laju kejadian dengan sensor",
            Penemu = "David R. Cox",
            Tahun = 1972,
            Catatan = "Kemungkinan parsial [Cox72]: tiap kejadian dibandingkan "
                      + "dengan semua yang masih berisiko saat itu — waktu "
                      + "kejadiannya sendiri tidak dimodelkan, hanya urutannya. "
                      + "HR = 2 berarti lajunya dua kali lipat per satuan "
                      + "kovariat. Asumsinya (proporsional): nisbah laju antar "
                      + "dua orang tetap sepanjang waktu."
        };

        public static readonly Rumus CoxBreslow = new()
        {
            Nama = "Taksiran Breslow (ikatan + hazard dasar)",
            Bentuk = "h0(τ_j) = d_j / Σ_{i∈R_j} exp(x_i′β),  H0 = Σh0,  S0 = exp(−H0)",
            Jenis = "Analisis ketahanan — fungsi dasar + penanganan ikatan",
            Penemu = "Norman E. Breslow",
            Tahun = 1974,
            Catatan = "Bila beberapa kejadian jatuh pada waktu yang sama, "
                      + "penyebutnya dipakai bersama (bukan satu per satu). "
                      + "Alternatif Efron (1977) lebih halus tetapi di sini "
                      + "sengaja tidak dipakai — acuan maupun kode memakai "
                      + "Breslow supaya apel lawan apel. Hazard dasarnya "
                      + "nonparametrik: tanpa model ini, β tidak bisa ditaksir."
        };

        public static readonly Rumus DekomposisiMusiman = new()
        {
            Nama = "Dekomposisi musiman aditif",
            Bentuk = "y_t = T_t + S_t + e_t    "
                     + "T = rata-rata bergerak terpusat,  "
                     + "S_j = rerata(y − T pada musim j) yang dipusatkan",
            Jenis = "Deret waktu — uraian tren, musiman, dan residu",
            Penemu = "Metode klasik deret waktu",
            Tahun = 0,
            Catatan = "Tren diambil dengan rata-rata bergerak terpusat "
                      + "sepanjang satu periode (ujungnya kosong m/2 titik — "
                      + "itu harga yang wajar, bukan data hilang). Musiman tiap "
                      + "bulan adalah rata-rata sisa detrended bulan itu, lalu "
                      + "dipusatkan supaya jumlah satu periodenya nol. Residu "
                      + "yang berpola berarti periodenya salah atau model "
                      + "aditifnya tidak cocok."
        };

        public static readonly Rumus HoltWinters = new()
        {
            Nama = "Pemulusan Holt-Winters aditif",
            Bentuk = "l_t = α(y_t − s_{t−m}) + (1−α)(l_{t−1}+b_{t−1})    "
                     + "b_t = β(l_t − l_{t−1}) + (1−β)b_{t−1}    "
                     + "s_t = γ(y_t − l_t) + (1−γ)s_{t−m}",
            Jenis = "Deret waktu — peramalan dengan tren dan musiman",
            Penemu = "Charles C. Holt; Peter R. Winters",
            Tahun = 1960,
            Catatan = "Tiga rekursi Holt (1957) + musiman Winters (1960). "
                      + "Ramalan h langkah: l_n + h·b_n + musimannya yang "
                      + "berputar. Nilai awal: level = rerata m pertama, musim "
                      + "= simpangannya, tren = kemiringan dua blok pertama — "
                      + "aturan yang sama dipakai acuan, sehingga yang diuji "
                      + "rekursinya. Parameter α,β,γ dipilih pengguna, bukan "
                      + "dioptimasi alat ini."
        };

        public static readonly Rumus Korespondensi = new()
        {
            Nama = "Analisis korespondensi tabel kontingensi",
            Bentuk = "S = D_r^-½(P − rc′)D_c^-½ = UΣV′    "
                     + "F = D_r^-½UΣ,  G = D_c^-½VΣ,  inersia = Σσ² = χ²/n",
            Jenis = "Multivariat kategori — peta hubungan baris–kolom",
            Penemu = "Hermann O. Hirschfeld; Jean-Paul Benzécri",
            Tahun = 1973,
            Catatan = "Residu terstandar diuraikan secara singular [Hir35]: "
                      + "nilai singularnya mengukur kuat hubungan per dimensi, "
                      + "dan jumlah kuadratnya persis khi-kuadrat/n [Ben73]. "
                      + "Titik baris dekat titik kolom berarti selnya lebih "
                      + "penuh dari harapan kebebasan — peta, bukan uji."
        };

        public static readonly Rumus Knn = new()
        {
            Nama = "Klasifikasi k tetangga-terdekat",
            Bentuk = "duga(x) = suara terbanyak di antara k tetangga terdekat "
                     + "(jarak Euclidean)",
            Jenis = "Klasifikasi nonparametrik — tanpa model sebaran",
            Penemu = "Evelyn Fix & Joseph L. Hodges",
            Tahun = 1951,
            Catatan = "Tidak menaksir apa pun: label pendatang baru mengikuti "
                      + "mayoritas tetangganya. k kecil peka noise, k besar "
                      + "menghaluskan batas. Seri seimbang dipecah ke indeks "
                      + "kelas terkecil — deterministik, bukan undian."
        };

        public static readonly Rumus LdaQda = new()
        {
            Nama = "Diskriminan linear / kuadratik (LDA / QDA)",
            Bentuk = "δ_k(x) = −½(x−μ_k)′Σ^−¹(x−μ_k) + ln π_k    "
                     + "LDA: Σ tergabung; QDA: Σ_k per kelas",
            Jenis = "Klasifikasi Gauss — batas linear / kuadratik",
            Penemu = "Ronald A. Fisher",
            Tahun = 1936,
            Catatan = "Tiap kelas dimodelkan Gauss; pendatang masuk ke kelas "
                      + "dengan skor tertinggi. LDA memakai satu kovarians "
                      + "tergabung (batasnya garis/lengkung datar), QDA memakai "
                      + "kovarians tiap kelas (batasnya kuadratik). Kovarians "
                      + "dibagi n (versi kemungkinan-maksimum, mengikuti "
                      + "sklearn), bukan n−G tak-bias ala Fisher — bedanya "
                      + "hanya skala seragam. Peluang "
                      + "lewat softmax atas skornya."
        };

        public static readonly Rumus TwoStep = new()
        {
            Nama = "Klaster TwoStep (jarak log-likelihood + BIC)",
            Bentuk = "ξ_v = −N_v·(Σ½ln(s²_k+s²_kv) + ΣE_kv)    "
                     + "d(J,S) = ξ_J + ξ_S − ξ_{J∪S}    "
                     + "BIC(J) = −2Σξ_v + m_J·ln N",
            Jenis = "Penggerombolan campuran angka + kategori",
            Penemu = "Tianming Chiu, DongPing Fang, John Chen, Yao Wang & Jeris",
            Tahun = 2001,
            Catatan = "Variabel angka dibakukan-z lebih dulu (ragam keseluruhan "
                      + "= 1). Tiap baris mulai sebagai klaster sendiri, lalu "
                      + "pasangan berjarak terkecil digabung sampai satu "
                      + "klaster; seri dimenangi pasangan leksikografis terkecil "
                      + "sehingga hasilnya deterministik. Banyak klaster dipilih "
                      + "otomatis pada BIC terkecil. Kategori di fixture acuan "
                      + "sengaja deterministik per grup: derau kategori "
                      + "terukur memecah serpihan lintas-grup dan menggeser "
                      + "minimum BIC."
        };

        public static readonly Rumus GowerSiluet = new()
        {
            Nama = "Jarak Gower & siluet (pemeriksa mutu klaster)",
            Bentuk = "Gower = (Σ|a−b|/rentang + Σ[beda kategori]) / banyak variabel    "
                     + "s(i) = (b(i) − a(i)) / maks(a(i), b(i))",
            Jenis = "Ukuran kemiripan campuran + validasi klaster",
            Penemu = "John C. Gower; Peter J. Rousseeuw",
            Tahun = 1987,
            Catatan = "Gower dipakai hanya untuk siluet (pemeriksa luar), bukan "
                      + "untuk penggabungan. a(i) = rerata jarak ke seklaster, "
                      + "b(i) = rerata jarak terkecil ke klaster lain. Dekat 1 "
                      + "berarti anggota pas di klasternya."
        };

        public static readonly Rumus Gee = new()
        {
            Nama = "GEE (persamaan taksiran terampat)",
            Bentuk = "Σ Dᵢ′Vᵢ⁻¹(yᵢ − μᵢ) = 0    Vᵢ = φ·Aᵢ½R(α)Aᵢ½    "
                     + "Cov = M₀⁻¹M₁M₀⁻¹",
            Jenis = "Regresi longitudinal/klaster — korelasi-kerja + sandwich",
            Penemu = "Kung-Yee Liang & Scott L. Zeger",
            Tahun = 1986,
            Catatan = "Korelasi-kerja Exchangeable (α momen Pearson) atau "
                      + "Independence; α dijepit agar R definit-positif. "
                      + "Sandwich kebal terhadap salah-spesifikasi R; naif = "
                      + "φ·M₀⁻¹. Binomial memakai taut logit."
        };

        public static readonly Rumus Robust = new()
        {
            Nama = "Regresi robust (M-estimasi IRLS)",
            Bentuk = "bobot = ψ(r/s)/(r/s)    Huber: min(1, t/|u|)    "
                     + "Tukey: (1−(u/c)²)² bila |u| ≤ c, nol bila tidak",
            Jenis = "Regresi kebal pencilan — IRLS atas WLS",
            Penemu = "Peter J. Huber; Beaton & Tukey",
            Tahun = 1974,
            Catatan = "Skala = median(|residu|)/0,6745 (pusat NOL, mengikuti "
                      + "statsmodels) dihitung ulang tiap putaran; "
                      + "iterasi berhenti bila |Δβ| < 1e-10. Tukey menolak "
                      + "pencilan jauh (bobot nol); Huber hanya membatasi "
                      + "pengaruhnya. Galat baku tak-dibandingkan di sini "
                      + "(kovariansi sandwich punya beberapa varian)."
        };

        public static readonly Rumus Cacah = new()
        {
            Nama = "Regresi cacahan lanjut (ZIP, NB & ZINB)",
            Bentuk = "ZIP: P(y=0) = π + (1−π)e^−λ,  P(y) = (1−π)·Poisson(λ)    "
                     + "NB: Var = μ + αμ²    ZINB: gabungan keduanya via EM",
            Jenis = "Regresi cacahan — nol-berlebih & sebaran-lebih",
            Penemu = "Diane Lambert; Cameron & Trivedi; Dempster–Laird–Rubin",
            Tahun = 1992,
            Catatan = "ZIP/ZINB ditaksir EM: peluang nol-struktural = rerata "
                      + "bobot E-step; M-step Poisson/NB terbobot; galat baku "
                      + "tak-dilaporkan untuk ZIP/ZINB (dinyatakan). NB "
                      + "ditaksir MLE penuh (Nelder–Mead atas β + log α); "
                      + "α = 0 berarti Poisson."
        };

        public static readonly Rumus Dbscan = new()
        {
            Nama = "DBSCAN (gugus berbasis kepadatan)",
            Bentuk = "inti ⇔ |N_eps| ≥ titik-min    perluas keterjangkauan "
                     + "lewat antrean; sisanya derau",
            Jenis = "Penggerombolan — tanpa menentukan k di muka",
            Penemu = "Martin Ester, Hans-Peter Kriegel, Jörg Sander & Xiaowei Xu",
            Tahun = 1996,
            Catatan = "Jarak Euclidean mentah (variabel TIDAK dibakukan — "
                      + "samakan skalanya dulu bila perlu). Titik inti yang "
                      + "saling terjangkau membentuk gugus; titik batas ikut "
                      + "gugus penemu pertama; sisanya derau (−1). Antrean "
                      + "FIFO deterministik; urutan titik sesuai urutan baris."
        };

        public static readonly Rumus Pohon = new()
        {
            Nama = "Pohon keputusan CART (belah biner rekursif)",
            Bentuk = "Gini = 1 − Σ p_k²    Δ = n·I − n_L·I_L − n_R·I_R    "
                     + "ambang = titik-tengah terurut",
            Jenis = "Klasifikasi & regresi nonparametrik — Analyze ▸ Classify ▸ Tree",
            Penemu = "Leo Breiman, Jerome Friedman, Richard Olshen & Charles Stone",
            Tahun = 1984,
            Catatan = "Tiap simpul dibelah pada ambang yang paling menurunkan "
                      + "ketakmurnian (Gini untuk kelas, SSE untuk angka). "
                      + "Fitur dinilai urut indeks dan ambang menaik; seri "
                      + "dimenangi yang pertama sehingga deterministik. "
                      + "Berhenti pada kedalaman-maks / belah-min (tanpa "
                      + "pemangkasan pasca-tumbuh — dinyatakan). Daun = "
                      + "mayoritas (seri ke indeks terkecil) / rerata."
        };

        public static readonly Rumus Pakar = new()
        {
            Nama = "Expert Modeler lite (turnamen RMSE rolling-origin)",
            Bentuk = "RMSE_o(m) = √(Σ(ŷ − y)²/H)    pilih m dengan rerata RMSE_o terkecil",
            Jenis = "Deret waktu — pemilihan model otomatis",
            Penemu = "Rob J. Hyndman & George Athanasopoulos",
            Tahun = 2018,
            Catatan = "12 ordo musiman ditandingkan: tiap ordo ditaksir ulang "
                      + "pada tiap origin lalu meramal data-tahan; kriteria = "
                      + "rerata RMSE (bukan AICc — sampel likelihood beda antar "
                      + "diferensiasi). Benchmark naif musiman wajib dikalahkan. "
                      + "Satu origin bisa adversarial (terukur), makanya tiga."
        };

        public static readonly Rumus Arima = new()
        {
            Nama = "ARIMA Box–Jenkins (MLE Kalman eksak)",
            Bentuk = "φ(L)(1−L)^d y_t = c + θ(L) ε_t    "
                     + "α_t = T α_{t−1} + R ε_t,  y_t = m + Z α_t",
            Jenis = "Deret waktu — autoregresi + integrasi + rerata bergerak",
            Penemu = "George E. P. Box & Gwilym M. Jenkins; saringan Rudolf E. Kalman",
            Tahun = 1970,
            Catatan = "Ordo (p,d,q) dipilih pengguna (p,q ≤ 2, d ≤ 1). Deret "
                      + "dideferensiasi dulu bila d = 1, lalu ARMA ditaksir "
                      + "dengan kemungkinan-maksimum eksak lewat saringan "
                      + "Kalman berinisialisasi stasioner [DK01]; optimasi "
                      + "Nelder–Mead deterministik atas parameter "
                      + "tertransformasi-tanh [Jon80] sehingga selalu "
                      + "stasioner/invertibel. Likelihood dilaporkan atas "
                      + "deret hasil-diferensiasi (n−d amatan). SE ramalan "
                      + "hanya untuk d = 0."
        };

        public static readonly Rumus Eksak = new()
        {
            Nama = "Uji eksak Fisher–Freeman–Halton (R × C) + Monte Carlo",
            Bentuk = "P(T) = (Π rᵢ! Π cⱼ!) / (n! Π tᵢⱼ!)    "
                     + "p = Σ P(T) untuk P(T) ≤ P(T₀)",
            Jenis = "Uji kebebasan tabel kontingensi — eksak bersyarat margin",
            Penemu = "Ronald A. Fisher; perluasan Freeman & Halton; Monte Carlo Hope",
            Tahun = 1951,
            Catatan = "Bersyarat pada total baris/kolom (prinsip Fisher 1935). "
                      + "Enumerasi penuh hanya untuk ruang tabel kecil (dibatasi "
                      + "2 juta; alternatif tabel besar: jaringan Mehta–Patel "
                      + "1983 — SENGAJA tidak dipakai). Monte Carlo menaksir p "
                      + "yang sama lewat permutasi label (benih tetap, bisa "
                      + "diulang persis)."
        };

        public static readonly Rumus Permutasi = new()
        {
            Nama = "Uji permutasi selisih rerata (acak-ulang label grup)",
            Bentuk = "T = x̄₀ − x̄₁    p = (c+1)/(B+1),  c = #{|T*| ≥ |T₀|}",
            Jenis = "Uji bebas-sebaran — H0: label grup dapat dipertukarkan",
            Penemu = "Ronald A. Fisher; Egon S. B. Pitman; Phipson & Smyth",
            Tahun = 2010,
            Catatan = "Di bawah H0 (grup tak beda), label grup dipertukarkan "
                      + "acak tanpa pengembalian (benih tetap, bisa diulang "
                      + "persis). p memakai +1 di pembilang dan penyebut "
                      + "sehingga tak pernah nol (Phipson–Smyth 2010)."
        };

        public static readonly Rumus Validasi = new()
        {
            Nama = "Validasi data (aturan nilai + kunci ganda)",
            Bentuk = "rentang: min ≤ x ≤ maks    aras: x ∈ {a, b, c}    "
                     + "hilang: sel kosong    ganda: kunci muncul > 1x",
            Jenis = "Mutu data — Validate Data",
            Penemu = "Carlo Batini & Monica Scannapieca",
            Tahun = 2006,
            Catatan = "Aturan ditulis sebagai teks ('usia 0 120; ...' dan "
                      + "'grup A,B,C; ...') supaya bisa diuji persis. Sel kosong "
                      + "dilaporkan sebagai 'hilang' (bukan rentang/aras), angka "
                      + "tak-terurai sebagai 'format', dan kunci yang hilang "
                      + "dikecualikan dari uji ganda."
        };

        public static readonly Rumus Ancova = new()
        {
            Nama = "ANCOVA — uji Type III dengan kovariat",
            Bentuk = "SS_A = SSE(model tanpa A) − SSE(model penuh)    "
                     + "F = (SS_A/df_A) / (SSE_penuh/df_resid)",
            Jenis = "ANOVA — membandingkan rerata sambil mengendalikan kovariat",
            Penemu = "Ronald A. Fisher; istilah Type III oleh Frank Yates",
            Tahun = 1934,
            Catatan = "Type III menguji setiap suku SETELAH suku lain diperhitungkan, "
                      + "sehingga hasilnya tidak bergantung urutan penulisan. Dua jalur "
                      + "harus sepakat: hipotesis umum (anova_lm typ=3) dan selisih "
                      + "jumlah kuadrat residual. Pada data seimbang keduanya memang "
                      + "sama; pada data tidak seimbang pun Type III tetap terdefinisi, "
                      + "itulah alasannya dipakai."
        };

        public static readonly Rumus RerataDisesuaikan = new()
        {
            Nama = "Rerata disesuaikan (adjusted means) ANCOVA",
            Bentuk = "ȳ_g^* = ȳ_g − b_kov · ( x̄_g,kov − x̄_kov )",
            Jenis = "ANCOVA — rerata setelah kovariat disetarakan",
            Penemu = "Ronald A. Fisher",
            Tahun = 1934,
            Catatan = "Menjawab pertanyaan \"bagaimana kalau semua kelompok punya nilai "
                      + "kovariat yang sama?\". Rerata tiap kelompok digeser sebesar "
                      + "kemiringan kovariat dikali selisih kovariat kelompok itu dari "
                      + "kovariat keseluruhan. Bila b_kov = 0, rerata disesuaikan sama "
                      + "dengan rerata kasar."
        };

        public static readonly Rumus KurvaNonlinear = new()
        {
            Nama = "Regresi non-linear — Levenberg–Marquardt",
            Bentuk = "(J′J + λ·diag(J′J))·δ = J′r    θ⁽ᵗ⁺¹⁾ = θ⁽ᵗ⁾ + δ",
            Jenis = "Regresi — model kurva dengan parameter tidak linear",
            Penemu = "Kenneth Levenberg & Donald Marquardt",
            Tahun = 1944,
            Catatan = "Perbaikan Gauss–Newton: λ kecil membuatnya mirip Gauss–Newton "
                      + "(cepat di dekat jawaban), λ besar membuatnya mirip turun-gradien "
                      + "(aman jauh dari jawaban). λ diturunkan bila langkah memperbaiki "
                      + "SSE dan dinaikkan bila tidak — jadi algoritmanya tidak meledak "
                      + "seperti Gauss–Newton telanjang. Tebakan awal diambil dari "
                      + "regresi linear ln(y) atas x, bukan dikarang."
        };

        public static readonly Rumus KurvaGalatBaku = new()
        {
            Nama = "Galat baku parameter kurva non-linear",
            Bentuk = "s² = SSE/(n − k)    Cov(θ̂) = s² · (J′J)⁻¹    SE(θ̂ⱼ) = √Covⱼⱼ",
            Jenis = "Regresi — ketelitian parameter kurva",
            Penemu = "Donald Marquardt",
            Tahun = 1963,
            Catatan = "Bentuk yang sama dengan regresi linear, hanya J (matriks turunan "
                      + "model terhadap parameternya) menggantikan X. Inilah yang dipakai "
                      + "`curve_fit` untuk `pcov`, dan memang harus sama: keduanya "
                      + "menaksir matriks informasi yang sama."
        };

        public static readonly Rumus Sls2 = new()
        {
            Nama = "Regresi dua tahap (2SLS) — variabel instrumental",
            Bentuk = "β̂ = (X̂′X̂)⁻¹X̂′y,   X̂ = P_Z X = Z(Z′Z)⁻¹Z′X",
            Jenis = "Regresi — penaksiran saat regresor berkorelasi dengan galat",
            Penemu = "Henri Theil & Robert Basmann",
            Tahun = 1953,
            Catatan = "Kalau sebuah regresor berkorelasi dengan galat, OLS berbias dan "
                      + "tak konsisten — menambah data tidak menolong. 2SLS mengganti "
                      + "regresor itu dengan ramalannya dari instrumen, sebab ramalan "
                      + "itu tidak lagi berkorelasi dengan galat. Nama \"dua tahap\" "
                      + "berasal dari cara menghitungnya, tetapi bentuk tertutup "
                      + "(X̂′X̂)⁻¹X̂′y dan dua regresi berurutan itu aljabar yang sama "
                      + "persis — bukan dua penaksir yang berbeda."
        };

        public static readonly Rumus Sls2GalatBaku = new()
        {
            Nama = "Galat baku 2SLS",
            Bentuk = "σ² = Σuᵢ²/(n − k),  uᵢ = yᵢ − xᵢ′β̂,  Cov(β̂) = σ² · (X̂′X̂)⁻¹",
            Jenis = "Regresi — ketelitian koefisien 2SLS",
            Penemu = "Henri Theil",
            Tahun = 1953,
            Catatan = "Residualnya memakai x yang SESUNGGUHNYA, bukan x̂. Memakai x̂ "
                      + "akan memberi σ² yang terlalu kecil dan galat baku yang terlalu "
                      + "sempit, sehingga variabel yang sebenarnya tidak berarti tampak "
                      + "berarti. Ramalan hanya dipakai untuk menaksir koefisien, bukan "
                      + "untuk mengukur sisaannya."
        };

        public static readonly Rumus Sls2TahapPertama = new()
        {
            Nama = "Tahap pertama 2SLS & kekuatan instrumen",
            Bentuk = "x_endogen = Z·π + v,   F = ((SSE_terbatas − SSE_penuh)/k_instrumen) "
                     + "/ (SSE_penuh/df)",
            Jenis = "Regresi — memeriksa apakah instrumennya cukup kuat",
            Penemu = "Robert Basmann; patokan F > 10 oleh Douglas Staiger & James Stock",
            Tahun = 1957,
            Catatan = "Instrumen yang lemah membuat 2SLS berbias ke arah OLS dan galat "
                      + "bakunya terlalu sempit — dalam keadaan ekstrem 2SLS lebih buruk "
                      + "daripada OLS. Ujinya: regresikan variabel endogen atas SELURUH "
                      + "instrumen, lalu uji apakah instrumen yang dikecualikan (yang "
                      + "hanya muncul di sini, tidak di persamaan struktural) menerangkan "
                      + "sebagian ragamnya. F di bawah 10 patut dicurigai."
        };

        public static readonly Rumus Sargan = new()
        {
            Nama = "Uji Sargan (kelebihan identifikasi)",
            Bentuk = "J = n · u′P_Z u / (u′u)   ~ χ²(k_instrumen − k_regresor)",
            Jenis = "Regresi — memeriksa kesahihan instrumen",
            Penemu = "John Denis Sargan",
            Tahun = 1958,
            Catatan = "Menjawab pertanyaan \"apakah instrumen saya benar-benar tidak "
                      + "berkorelasi dengan galat?\". Turunannya dari kriteria GMM: "
                      + "g = Z′u/n, Ŝ = Z′Z/n, σ² = u′u/n, maka J = n·g′Ŝ⁻¹g/σ² = "
                      + "n·u′P_Z u/(u′u). Hanya terdefinisi bila instrumen lebih banyak "
                      + "daripada regresor; pada model pas-identifikasi tidak ada yang "
                      + "bisa diuji. Karena modelnya selalu memuat konstanta, rerata "
                      + "residualnya tepat nol, sehingga R² terpusat dan tak terpusat "
                      + "memberi J yang sama persis — bukan kebetulan, melainkan akibat "
                      + "adanya konstanta."
        };

        public static readonly Rumus HausmanEndogen = new()
        {
            Nama = "Uji Hausman bentuk regresi (Durbin–Wu–Hausman)",
            Bentuk = "y = x′β + δ·v̂ + e,   uji t pada δ̂   (v̂ = residual tahap pertama)",
            Jenis = "Regresi — memeriksa apakah regresornya benar-benar endogen",
            Penemu = "Jerry A. Hausman; bentuk regresi oleh James Durbin & De-Min Wu",
            Tahun = 1978,
            Catatan = "Menjawab \"perlukah 2SLS sama sekali?\". Bila variabelnya "
                      + "sebenarnya eksogen, OLS sudah tak berbias DAN lebih efisien, "
                      + "jadi 2SLS hanya membuang ketelitian. Caranya: masukkan "
                      + "residual tahap pertama sebagai regresor tambahan pada OLS; "
                      + "kalau δ̂ berbeda nyata dari nol, variabelnya memang endogen. "
                      + "Bentuk regresi ini dipakai karena ia stabil secara numerik — "
                      + "versi selisih dua matriks ragam menuntut rank dan pseudo-invers "
                      + "atas matriks yang hampir singular."
        };

        public static readonly Rumus ManovaUji = new()
        {
            Nama = "MANOVA — matriks jumlah kuadrat silang",
            Bentuk = "E = Σ_g Σ_i (y_gi − ȳ_g)(y_gi − ȳ_g)′    "
                     + "H = Σ_g n_g (ȳ_g − ȳ)(ȳ_g − ȳ)′",
            Jenis = "Multivariat — perbandingan rerata beberapa variabel sekaligus",
            Penemu = "Samuel S. Wilks",
            Tahun = 1932,
            Catatan = "E adalah ragam DI DALAM kelompok dan H ragam ANTAR kelompok — "
                      + "keduanya berbentuk MATRIKS, bukan satu angka, sebab yang "
                      + "dibandingkan adalah vektor rerata. Yang diuji adalah akar "
                      + "ciri E⁻¹H: bila semua rerata kelompok sama, H = 0 sehingga "
                      + "semua akarnya nol. Karena E⁻¹H tidak simetris, akarnya "
                      + "dihitung lewat matriks simetris E^(−1/2)·H·E^(−1/2) yang "
                      + "akarnya sama."
        };

        public static readonly Rumus ManovaStatistik = new()
        {
            Nama = "Empat statistik multivariat (Wilks, Pillai, Hotelling–Lawley, Roy)",
            Bentuk = "λᵢ = eᵢ/(1+eᵢ)   Λ = Π(1−λᵢ)   V = Σλᵢ   U = Σeᵢ   θ = max eᵢ",
            Jenis = "Multivariat — uji hipotesis gabungan",
            Penemu = "Wilks; Pillai; Hotelling & Lawley; Roy",
            Tahun = 1953,
            Catatan = "Keempatnya menguji hipotesis yang sama dengan penimbangan "
                      + "berbeda: Wilks mengalikan, Pillai menjumlahkan λᵢ, "
                      + "Hotelling–Lawley menjumlahkan eᵢ, Roy hanya memakai yang "
                      + "terbesar. Karena itu keempatnya bisa berbeda meski berasal "
                      + "dari matriks yang sama. Pillai paling tahan terhadap asumsi "
                      + "yang dilanggar dan kelompok tidak seimbang; Roy paling tajam "
                      + "tetapi paling sensitif. Derajat bebasnya mengikuti konvensi "
                      + "SAS/statsmodels dengan s = min(p,q), m = (|p−q|−1)/2, "
                      + "dan n = (v−p−1)/2."
        };

        public static readonly Rumus ManovaBoxM = new()
        {
            Nama = "Uji Box M (kesamaan matriks kovarians)",
            Bentuk = "M = (N−k)·ln|S_pool| − Σ(n_g−1)·ln|S_g|,   "
                     + "χ² = (1−c)·M   dengan c = (Σ1/(n_g−1) − 1/(N−k))·(2p²+3p−1)/(6(p+1)(k−1))",
            Jenis = "Multivariat — pemeriksaan asumsi",
            Penemu = "George E. P. Box",
            Tahun = 1949,
            Catatan = "MANOVA mengandaikan matriks kovarians semua kelompok sama. "
                      + "Uji ini memeriksanya, dan ia TIDAK menolak hipotesis nol "
                      + "berarti asumsinya terpenuhi — hanya berarti tidak ada bukti "
                      + "yang menentangnya. Sebarannya didekati dengan chi-kuadrat, "
                      + "jadi p-nya adalah hampiran. ln|S| dihitung dari jumlah "
                      + "logaritma akar cirinya, bukan dari determinan langsung: "
                      + "determinan matriks berukuran sedang mudah meluap atau "
                      + "menyusut ke nol, sedangkan jumlah logaritma tidak."
        };

        public static readonly Rumus AnovaBerulangUji = new()
        {
            Nama = "ANOVA berulang (satu faktor within-subject)",
            Bentuk = "SS_total = SS_antar subjek + SS_waktu + SS_galat,   "
                     + "F = (SS_waktu/(k−1)) / (SS_galat/((n−1)(k−1)))",
            Jenis = "Perbandingan — rerata berulang pada orang yang sama",
            Penemu = "Ronald A. Fisher; rancangan berulang oleh Samuel S. Wilks",
            Tahun = 1946,
            Catatan = "Galatnya adalah interaksi waktu × subjek, dengan derajat bebas "
                      + "(n−1)(k−1) — BUKAN nk−k. Ini kekeliruan klasik pada uji ini: "
                      + "memakai galat antar-kelompok membuat F terlalu kecil, memakai "
                      + "galat total membuat F terlalu besar. Suku antar-subjek ada "
                      + "justru supaya perbedaan antar orang tidak masuk ke galat, "
                      + "sehingga ujinya jauh lebih tajam daripada ANOVA biasa."
        };

        public static readonly Rumus Sferisitas = new()
        {
            Nama = "Uji Mauchly dan epsilon koreksi sferisitas",
            Bentuk = "W = Πλᵢ / (tr(M)/(k−1))^(k−1),   "
                     + "ε_GG = (Σλᵢ)² / ((k−1)·Σλᵢ²),   "
                     + "ε_HF = (n(k−1)ε_GG − 2) / ((k−1)(n − 1 − (k−1)ε_GG))",
            Jenis = "Perbandingan — pemeriksaan asumsi dan koreksinya",
            Penemu = "John W. Mauchly; Samuel W. Greenhouse & Seymour Geisser; "
                     + "Huynh & Feldt",
            Tahun = 1958,
            Catatan = "λᵢ adalah akar ciri matriks kontras M = C·S·C′, dengan S matriks "
                      + "kovarians antar waktu dan C basis ortonormal ruang ortogonal "
                      + "terhadap vektor konstan. Karena sum(λ) = tr(M), "
                      + "sum(λ²) = tr(M²), dan Πλ = det(M), epsilon dan W bisa dihitung "
                      + "tanpa dekomposisi akar ciri sama sekali — itulah jalur kedua "
                      + "yang dipakai untuk saling memeriksa. ε TIDAK bergantung pada "
                      + "pilihan basis ortonormalnya, sebab dua basis ortonormal "
                      + "dihubungkan matriks ortogonal yang tidak mengubah akar ciri. "
                      + "Koreksinya bekerja dengan mengalikan derajat bebas dengan ε, "
                      + "sehingga p menjadi lebih besar: pelanggaran sphericity membuat "
                      + "p terlalu kecil, jadi koreksinya membuat kesimpulan lebih "
                      + "berhati-hati, bukan lebih longgar."
        };

        public static readonly Rumus KorelasiKanonik = new()
        {
            Nama = "Korelasi kanonik (nilai singular bentuk akar balik)",
            Bentuk = "W = Sxx^(−1/2)·Sxy·Syy^(−1/2),   "
                     + "rᵢ = nilai singular ke-i dari W",
            Jenis = "Hubungan — dua blok variabel sekaligus",
            Penemu = "Harold Hotelling",
            Tahun = 1936,
            Catatan = "Sxx, Syy, Sxy adalah matriks jumlah kuadrat–silang setelah "
                      + "rerata dikurangi (SSCP), bukan matriks kovarians yang dibagi "
                      + "(n−1) — pembagi itu hanya menggeser skala dan tidak mengubah "
                      + "korelasinya. Nilai singular W sama dengan akar akar ciri W·W′ "
                      + "(p×p) DAN akar akar ciri W′·W (q×q). Dua matriks itu berbeda — "
                      + "bahkan berbeda ukuran saat p ≠ q — sehingga keduanya dipakai "
                      + "sebagai dua jalur yang saling memeriksa. Karena itu kelas ini "
                      + "tidak perlu memecah matriks tak simetris dan tidak membalik "
                      + "matriks secara eksplisit: hanya M^(−1/2) yang dibutuhkan, dan "
                      + "itu pun lewat dekomposisi akar ciri matriks simetris."
        };

        public static readonly Rumus BobotKanonik = new()
        {
            Nama = "Bobot dan muatan kanonik",
            Bentuk = "aᵢ = Sxx^(−1/2)·uᵢ,   bᵢ = Syy^(−1/2)·(W′uᵢ)/rᵢ,   "
                     + "muatan = corr(Xⱼ, Uᵢ) = corr(Xⱼ, aᵢ′X)",
            Jenis = "Hubungan — penafsiran arah hubungan",
            Penemu = "Harold Hotelling",
            Tahun = 1936,
            Catatan = "Bobot dinormalkan supaya ragam variat kanoniknya = 1 "
                      + "(aᵢ′·Sxx·aᵢ = n−1). statsmodels memakai penskalaan lain, "
                      + "aᵢ′·Sxx·aᵢ = 1, yaitu ragam 1/(n−1); hubungan keduanya satu "
                      + "angka saja, bobot_acuan = bobot_di_sini / √(n−1), sehingga "
                      + "bisa diperiksa persis. Muatan TIDAK bergantung pada penskalaan "
                      + "itu, sebab korelasi tidak berubah bila salah satu vektornya "
                      + "dikalikan tetapan. Pembagian dengan rᵢ pada rumus bᵢ sekaligus "
                      + "yang menjamin corr(Uᵢ,Vᵢ) = +rᵢ, bukan −rᵢ."
        };

        public static readonly Rumus UjiKanonik = new()
        {
            Nama = "Uji chi-kuadrat Bartlett berurut dan statistik multivariat",
            Bentuk = "Λᵢ = Π_{j≥i}(1−rⱼ²),   "
                     + "χ² = −(n−1−(p+q+1)/2)·ln Λᵢ,   df = (p−i)(q−i)",
            Jenis = "Hubungan — pengujian keberartian",
            Penemu = "Maurice S. Bartlett; Samuel S. Wilks",
            Tahun = 1938,
            Catatan = "HANYA AKAR PERTAMA yang sah diuji dengan rumus ini. Pada akar 0 "
                      + "hipotesisnya \"semua korelasi kanonik nol\"; pada akar i ≥ 1 "
                      + "hipotesisnya memaksa akar ciri tepat nol, yaitu kasus batas, "
                      + "sehingga dalil Wilks bentuk sederhana tidak berlaku. Diukur "
                      + "lewat simulasi di bawah H0: pada akar 0 rata-rata p = 0,51 dan "
                      + "laju tolak 0,049 (benar); pada akar 1 dan 2 rata-rata p ≈ 0,70 "
                      + "dan laju tolak ≈ 0,002 (nyaris tidak pernah menolak). Nisbahnya "
                      + "stabil dari n = 60 sampai n = 1000, jadi ini bukan soal "
                      + "konvergensi lambat. Hampiran F per akar dan keempat statistik "
                      + "multivariat bertumpu pada Λᵢ yang sama, sehingga untuk akar "
                      + "kedua dan seterusnya p-nya tidak boleh dipakai. Yang boleh "
                      + "dipercaya hanya Λ₀ = Π semua (1−rⱼ²)."
        };

        public static readonly Rumus AnovaDuaFaktor = new()
        {
            Nama = "ANOVA dua faktor, jumlah kuadrat Type III",
            Bentuk = "SS_III(suku) = SSE(model tanpa kolom suku itu) − SSE(model penuh),   "
                     + "y = μ + αᵢ + βⱼ + (αβ)ᵢⱼ + ε",
            Jenis = "Perbandingan — dua faktor sekaligus beserta interaksinya",
            Penemu = "Frank Yates; dipopulerkan sebagai \"Type III\" oleh SAS",
            Tahun = 1934,
            Catatan = "Rancangan matriksnya memakai PENGODEAN EFEK (satu tingkat "
                      + "menjadi −1 di semua kolom), bukan pengodean dummy. Diukur "
                      + "24 Sep 2026 pada 2x2 berimbang yang bisa dihitung tangan "
                      + "(SS_A = 320, SS_B = 180, SS_AB = 20): pengodean dummy memberi "
                      + "90/40/20 — salah — sedangkan pengodean efek memberi 320/180/20. "
                      + "Kontras hanya berpengaruh bila ada suku turunan yang memuat "
                      + "suku yang diuji, sehingga ANCOVA (tanpa interaksi) tidak "
                      + "terkena: terukur selisihnya hanya 5,1e-13. Jumlah kuadrat "
                      + "Type III TIDAK menjumlah menjadi jumlah kuadrat total — "
                      + "suku-sukunya tidak ortogonal pada data tak berimbang."
        };

        public static readonly Rumus RerataTepiTakBerbobot = new()
        {
            Nama = "Rerata tepi tak berbobot (yang diuji Type III)",
            Bentuk = "rerata tepi tak berbobot tingkat ke-i = (1/b)·Σⱼ ȳᵢⱼ,   "
                     + "rerata berbobot = (1/nᵢ)·Σⱼ nᵢⱼ ȳᵢⱼ",
            Jenis = "Perbandingan — arti hipotesis yang sedang diuji",
            Penemu = "—",
            Catatan = "Type III menguji hipotesis atas rerata tepi TAK BERBOBOT: tiap "
                      + "tingkat faktor lain dianggap sama penting, jadi yang "
                      + "dirata-ratakan adalah rerata SEL, bukan amatan. Bila "
                      + "rancangannya berimbang kedua kolom itu sama persis; bila tak "
                      + "berimbang keduanya berbeda, dan selisihnya itulah yang membuat "
                      + "Type III berbeda dari Type I dan Type II. Pada data uji 3x2, "
                      + "faktor A memberi 8,781 / 22,430 / 29,637 (berbobot) lawan "
                      + "7,646 / 22,220 / 29,638 (tak berbobot) — cukup berbeda untuk "
                      + "mengubah kesimpulan bila salah pilih."
        };

        public static readonly Rumus TabelKustom = new()
        {
            Nama = "Tabel kustom berlapis (Custom Tables)",
            Bentuk = "n_g = Σᵢ 1[baris i jatuh di sel g],   %_g = n_g / N × 100%",
            Jenis = "Deskriptif — tabel berlapis dengan hitungan dan persentase",
            Penemu = "—",
            Tahun = 0,
            Catatan = "Sel terbentuk dari kombinasi nilai dimensi baris dan dimensi "
                      + "kolom. Count menghitung amatan yang jatuh di sel itu; "
                      + "Percent membaginya dengan N, banyak amatan yang dipakai "
                      + "tabel ini. Baris Total dihitung dari seluruh amatan, bukan "
                      + "dari menjumlahkan persentase sel — karena itu persentase "
                      + "per sel hanya dijamin berjumlah 100% bila setiap amatan "
                      + "jatuh di tepat satu sel."
        };
    }
}
