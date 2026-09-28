using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.DataPrep
{

    public class HasilHitung
    {
        public List<double?> Nilai = new();
        public string? Galat;
        public int BarisPertamaGagal = -1;
        public List<string> KolomDipakai = new();
        public bool Sukses => Galat is null;
    }

    public static class Ekspresi
    {
        

        private abstract class Node
        {
            // Anak dalam urutan evaluasi, diisi sekali saat dibentuk supaya
            // penjelajahan tidak mengalokasi ulang setiap baris data.
            public abstract IReadOnlyList<Node> Anak { get; }

            // Menggabungkan nilai anak menjadi nilai node ini. Pohon dijelajahi
            // dengan tumpukan eksplisit, bukan rekursi.
            public abstract double Hitung(Func<string, double> ambil, double[] nilaiAnak);

            public virtual string? NamaVariabel => null;
        }

        private sealed class Angka : Node
        {
            private readonly double _v;
            public Angka(double v) { _v = v; Anak = Array.Empty<Node>(); }
            public override IReadOnlyList<Node> Anak { get; }
            public override double Hitung(Func<string, double> ambil, double[] nilaiAnak) => _v;
        }

        private sealed class Variabel : Node
        {
            private readonly string _nama;
            public Variabel(string nama) { _nama = nama; Anak = Array.Empty<Node>(); }
            public override IReadOnlyList<Node> Anak { get; }
            public override double Hitung(Func<string, double> ambil, double[] nilaiAnak) => ambil(_nama);
            public override string? NamaVariabel => _nama;
        }

        private sealed class Biner : Node
        {
            private readonly string _op;
            public Biner(string op, Node kiri, Node kanan) { _op = op; Anak = new[] { kiri, kanan }; }
            public override IReadOnlyList<Node> Anak { get; }

            public override double Hitung(Func<string, double> ambil, double[] nilaiAnak)
            {
                double a = nilaiAnak[0];
                double b = nilaiAnak[1];

                switch (_op)
                {
                    case "+": return Hilang(a, b) ? double.NaN : a + b;
                    case "-": return Hilang(a, b) ? double.NaN : a - b;
                    case "*": return Hilang(a, b) ? double.NaN : a * b;
                    case "/":
                        if (Hilang(a, b)) return double.NaN;
                        if (b == 0) return double.NaN;          
                        return a / b;
                    case "^": return Hilang(a, b) ? double.NaN : Math.Pow(a, b);
                    case "%":
                        if (Hilang(a, b)) return double.NaN;
                        if (b == 0) return double.NaN;
                        return a - b * Math.Floor(a / b);       
                }

                
                if (Hilang(a, b)) return double.NaN;
                return _op switch
                {
                    "="  => a == b ? 1 : 0,
                    "<>" => a != b ? 1 : 0,
                    "<"  => a < b ? 1 : 0,
                    "<=" => a <= b ? 1 : 0,
                    ">"  => a > b ? 1 : 0,
                    ">=" => a >= b ? 1 : 0,
                    "&"  => (a == 0 || b == 0) ? 0 : 1,
                    "|"  => (a != 0 || b != 0) ? 1 : 0,
                    _    => double.NaN
                };
            }

            private static bool Hilang(double a, double b)
                => double.IsNaN(a) || double.IsNaN(b);
        }

        private sealed class Tunggal : Node
        {
            private readonly string _op;
            public Tunggal(string op, Node anak) { _op = op; Anak = new[] { anak }; }
            public override IReadOnlyList<Node> Anak { get; }

            public override double Hitung(Func<string, double> ambil, double[] nilaiAnak)
            {
                double v = nilaiAnak[0];
                if (_op == "~")
                {
                    if (double.IsNaN(v)) return double.NaN;
                    return v == 0 ? 1 : 0;
                }
                return double.IsNaN(v) ? double.NaN : -v;
            }
        }

        private sealed class Panggilan : Node
        {
            private readonly string _nama;
            public Panggilan(string nama, List<Node> arg) { _nama = nama; Anak = arg; }
            public override IReadOnlyList<Node> Anak { get; }

            public override double Hitung(Func<string, double> ambil, double[] nilaiAnak)
            {
                if (_nama == "MISSING")
                    return double.IsNaN(nilaiAnak[0]) ? 1 : 0;

                
                bool statistik = _nama is "SUM" or "MEAN" or "MEDIAN" or "SD" or "VARIANCE"
                                        or "MIN" or "MAX" or "CFVAR";

                var v = nilaiAnak.ToList();
                if (statistik)
                {
                    var ada = v.Where(x => !double.IsNaN(x)).ToList();
                    if (ada.Count == 0) return double.NaN;
                    return _nama switch
                    {
                        "SUM"      => ada.Sum(),
                        "MEAN"     => ada.Average(),
                        "MEDIAN"   => Median(ada),
                        "SD"       => ada.Count < 2 ? double.NaN : Math.Sqrt(ada.Sum(x => (x - ada.Average()) * (x - ada.Average())) / (ada.Count - 1)),
                        "VARIANCE" => ada.Count < 2 ? double.NaN : ada.Sum(x => (x - ada.Average()) * (x - ada.Average())) / (ada.Count - 1),
                        "MIN"      => ada.Min(),
                        "MAX"      => ada.Max(),
                        "CFVAR"    => ada.Count < 2 || ada.Average() == 0 ? double.NaN
                                      : Math.Sqrt(ada.Sum(x => (x - ada.Average()) * (x - ada.Average())) / (ada.Count - 1)) / ada.Average(),
                        _          => double.NaN
                    };
                }

                if (v.Any(double.IsNaN)) return double.NaN;
                double x0 = v.Count > 0 ? v[0] : double.NaN;
                double x1 = v.Count > 1 ? v[1] : double.NaN;

                return _nama switch
                {
                    "ABS"   => Math.Abs(x0),
                    "SQRT"  => x0 < 0 ? double.NaN : Math.Sqrt(x0),
                    "LN"    => x0 <= 0 ? double.NaN : Math.Log(x0),
                    "LG10"  => x0 <= 0 ? double.NaN : Math.Log10(x0),
                    "LOG10" => x0 <= 0 ? double.NaN : Math.Log10(x0),
                    "EXP"   => Math.Exp(x0),
                    "SIN"   => Math.Sin(x0),
                    "COS"   => Math.Cos(x0),
                    "TAN"   => Math.Tan(x0),
                    "ARSIN" => (x0 < -1 || x0 > 1) ? double.NaN : Math.Asin(x0),
                    "ARTAN" => Math.Atan(x0),
                    "ATAN"  => Math.Atan(x0),
                    "RND"   => Math.Floor(x0 + 0.5),
                    "ROUND" => Math.Floor(x0 + 0.5),
                    "TRUNC" => Math.Truncate(x0),
                    "MOD"   => x1 == 0 ? double.NaN : x0 - x1 * Math.Floor(x0 / x1),
                    "POWER" => Math.Pow(x0, x1),
                    "SIGN"  => x0 > 0 ? 1 : x0 < 0 ? -1 : 0,
                    _       => double.NaN
                };
            }

            private static double Median(List<double> ada)
            {
                var u = ada.OrderBy(x => x).ToList();
                int n = u.Count;
                return n % 2 == 1 ? u[n / 2] : 0.5 * (u[n / 2 - 1] + u[n / 2]);
            }
        }

        

        private enum Jenis { Angka, Nama, Operator, Buka, Tutup, Koma, Selesai }

        private sealed class Token
        {
            public Jenis Jenis;
            public string Teks = "";
            public double Angka;
            public int Posisi;
        }

        private sealed class GalatEkspresi : Exception
        {
            public GalatEkspresi(string pesan) : base(pesan) { }
        }

        private static readonly string[] Operator2 =
            { "<=", ">=", "<>", "!=", "==", "**", "&&", "||" };

        private static List<Token> Tokenisasi(string teks)
        {
            var hasil = new List<Token>();
            int i = 0;
            var ci = CultureInfo.InvariantCulture;

            while (i < teks.Length)
            {
                char c = teks[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (char.IsDigit(c) || (c == '.' && i + 1 < teks.Length && char.IsDigit(teks[i + 1])))
                {
                    int mulai = i;
                    while (i < teks.Length && (char.IsDigit(teks[i]) || teks[i] == '.' || teks[i] == '_')) i++;
                    if (i < teks.Length && (teks[i] == 'e' || teks[i] == 'E'))
                    {
                        i++;
                        if (i < teks.Length && (teks[i] == '+' || teks[i] == '-')) i++;
                        while (i < teks.Length && char.IsDigit(teks[i])) i++;
                    }
                    string pot = teks.Substring(mulai, i - mulai).Replace("_", "");
                    if (!double.TryParse(pot, NumberStyles.Float, ci, out double angka))
                        throw new GalatEkspresi($"Angka “{pot}” tidak bisa dibaca pada posisi {mulai + 1}.");
                    hasil.Add(new Token { Jenis = Jenis.Angka, Angka = angka, Posisi = mulai });
                    continue;
                }

                if (char.IsLetter(c) || c == '_' || c == '#')
                {
                    int mulai = i;
                    while (i < teks.Length && (char.IsLetterOrDigit(teks[i]) || teks[i] == '_' || teks[i] == '.')) i++;
                    hasil.Add(new Token { Jenis = Jenis.Nama, Teks = teks.Substring(mulai, i - mulai), Posisi = mulai });
                    continue;
                }

                if (c == '(') { hasil.Add(new Token { Jenis = Jenis.Buka, Posisi = i }); i++; continue; }
                if (c == ')') { hasil.Add(new Token { Jenis = Jenis.Tutup, Posisi = i }); i++; continue; }
                if (c == ',') { hasil.Add(new Token { Jenis = Jenis.Koma, Posisi = i }); i++; continue; }

                string dua = i + 1 < teks.Length ? teks.Substring(i, 2) : "";
                if (Operator2.Contains(dua))
                {
                    string op = dua switch
                    {
                        "!=" => "<>", "==" => "=", "&&" => "&", "||" => "|", "**" => "^",
                        _ => dua
                    };
                    hasil.Add(new Token { Jenis = Jenis.Operator, Teks = op, Posisi = i });
                    i += 2;
                    continue;
                }

                if ("+-*/^%=<>&|~".IndexOf(c) >= 0)
                {
                    char k = c == '=' && hasil.Count > 0 ? '=' : c;
                    hasil.Add(new Token { Jenis = Jenis.Operator, Teks = k.ToString(), Posisi = i });
                    i++;
                    continue;
                }

                throw new GalatEkspresi($"Huruf “{c}” tidak dikenal pada posisi {i + 1}.");
            }

            hasil.Add(new Token { Jenis = Jenis.Selesai, Posisi = teks.Length });
            return hasil;
        }

        
        
        

        private sealed class Parser
        {
            private readonly List<Token> _t;
            private int _p;
            private readonly HashSet<string> _fungsi;
            private readonly Func<string, bool> _kolomAda;

            public Parser(List<Token> t, HashSet<string> fungsi, Func<string, bool> kolomAda)
            {
                _t = t; _fungsi = fungsi; _kolomAda = kolomAda;
            }

            // Batas sarang pengurai: kurung, argumen fungsi, tanda tunggal, dan
            // rantai pangkat masing-masing menambah satu tingkat. Tanpa batas
            // ini "((((...))))" menghabiskan tumpukan sebelum pesan galat
            private const int KedalamanMaks = 100;
            private int _dalam;

            private Node Turun(Func<Node> turun)
            {
                if (++_dalam > KedalamanMaks)
                    throw new GalatEkspresi(
                        $"Ekspresi terlalu bersarang (lebih dari {KedalamanMaks} tingkat).");
                Node n = turun();
                _dalam--;
                return n;
            }

            private Token Kini => _t[_p];
            private Token Makan() => _t[_p++];

            private bool Cocok(string op)
            {
                if (Kini.Jenis == Jenis.Operator && Kini.Teks == op) { _p++; return true; }
                return false;
            }

            public Node Pohon()
            {
                Node n = Atau();
                if (Kini.Jenis != Jenis.Selesai)
                    throw new GalatEkspresi($"Sisa teks tidak bisa dipahami pada posisi {Kini.Posisi + 1}.");
                return n;
            }

            private Node Atau()
            {
                Node k = Dan();
                while (Cocok("|")) k = new Biner("|", k, Dan());
                return k;
            }

            private Node Dan()
            {
                Node k = Banding();
                while (Cocok("&")) k = new Biner("&", k, Banding());
                return k;
            }

            private Node Banding()
            {
                Node k = Tambah();
                while (Kini.Jenis == Jenis.Operator && (Kini.Teks == "=" || Kini.Teks == "<>"
                                                        || Kini.Teks == "<" || Kini.Teks == "<="
                                                        || Kini.Teks == ">" || Kini.Teks == ">="))
                {
                    string op = Makan().Teks;
                    k = new Biner(op, k, Tambah());
                }
                return k;
            }

            private Node Tambah()
            {
                Node k = Kali();
                while (Kini.Jenis == Jenis.Operator && (Kini.Teks == "+" || Kini.Teks == "-"))
                {
                    string op = Makan().Teks;
                    k = new Biner(op, k, Kali());
                }
                return k;
            }

            private Node Kali()
            {
                Node k = Pangkat();
                while (Kini.Jenis == Jenis.Operator && (Kini.Teks == "*" || Kini.Teks == "/" || Kini.Teks == "%"))
                {
                    string op = Makan().Teks;
                    k = new Biner(op, k, Pangkat());
                }
                return k;
            }

            private Node Pangkat()
            {
                Node k = Tanda();
                if (Cocok("^")) k = new Biner("^", k, Turun(Pangkat));
                return k;
            }

            private Node Tanda()
            {
                if (Cocok("-")) return new Tunggal("-", Turun(Tanda));
                if (Cocok("+")) return Turun(Tanda);
                if (Cocok("~")) return new Tunggal("~", Turun(Tanda));
                return Atom();
            }

            private Node Atom()
            {
                var t = Kini;

                if (t.Jenis == Jenis.Angka) { _p++; return new Angka(t.Angka); }

                if (t.Jenis == Jenis.Buka)
                {
                    _p++;
                    Node dalam = Turun(Atau);
                    if (Kini.Jenis != Jenis.Tutup)
                        throw GalatKurung(t.Posisi);
                    _p++;
                    return dalam;
                }

                if (t.Jenis == Jenis.Nama)
                {
                    _p++;
                    string nama = t.Teks;
                    string besar = nama.ToUpperInvariant();

                    if (Kini.Jenis == Jenis.Buka)
                    {
                        if (!_fungsi.Contains(besar))
                            throw new GalatEkspresi($"Fungsi “{nama}” tidak dikenal pada posisi {t.Posisi + 1}.");
                        _p++;
                        var arg = new List<Node>();
                        if (Kini.Jenis != Jenis.Tutup)
                        {
                            arg.Add(Turun(Atau));
                            while (Kini.Jenis == Jenis.Koma) { _p++; arg.Add(Turun(Atau)); }
                        }
                        if (Kini.Jenis != Jenis.Tutup)
                            throw GalatKurung(t.Posisi);
                        _p++;

                        
                        
                        bool variabel = besar is "SUM" or "MEAN" or "MEDIAN" or "SD"
                                                or "VARIANCE" or "MIN" or "MAX" or "CFVAR";
                        int butuh = besar switch
                        {
                            "POWER" or "MOD" => 2,
                            _ => 1
                        };
                        if (variabel)
                        {
                            if (arg.Count < 1)
                                throw new GalatEkspresi($"Fungsi {besar} butuh sedikitnya satu argumen.");
                        }
                        else if (arg.Count != butuh)
                        {
                            throw new GalatEkspresi($"Fungsi {besar} butuh {butuh} argumen, diberi {arg.Count}.");
                        }
                        return new Panggilan(besar, arg);
                    }

                    if (besar == "PI") return new Angka(Math.PI);

                    if (!_kolomAda(nama))
                        throw new GalatEkspresi($"Variabel “{nama}” tidak ada dalam data (posisi {t.Posisi + 1}).");
                    return new Variabel(nama);
                }

                throw new GalatEkspresi($"Teks tidak lengkap pada posisi {t.Posisi + 1}.");
            }

            private static GalatEkspresi GalatKurung(int posisi)
                => new GalatEkspresi($"Kurung belum ditutup (dibuka pada posisi {posisi + 1}).");
        }

        

        public static readonly string[] Fungsi =
        {
            "ABS", "SQRT", "LN", "LG10", "LOG10", "EXP", "SIN", "COS", "TAN",
            "ARSIN", "ARTAN", "ATAN", "RND", "ROUND", "TRUNC", "MOD", "POWER",
            "SIGN", "MISSING",
            "SUM", "MEAN", "MEDIAN", "SD", "VARIANCE", "MIN", "MAX", "CFVAR"
        };

        private static readonly HashSet<string> NamaFungsi =
            new HashSet<string>(Fungsi, StringComparer.OrdinalIgnoreCase);

        public static object? Terjemah(string teks, IReadOnlyList<string> kolom, out string? galat)
        {
            galat = null;
            if (string.IsNullOrWhiteSpace(teks))
            {
                galat = "Ekspresinya masih kosong.";
                return null;
            }

            try
            {
                var token = Tokenisasi(teks);
                var p = new Parser(token, NamaFungsi,
                                   n => kolom.Any(k => string.Equals(k, n, StringComparison.OrdinalIgnoreCase)));
                return p.Pohon();
            }
            catch (GalatEkspresi g) { galat = g.Message; return null; }
            catch (Exception g) { galat = g.Message; return null; }
        }

        // Evaluasi pasca-urutan dengan tumpukan eksplisit. Rantai operator
        // panjang ("a1+a2+...+a9999") membentuk pohon condong kiri sedalam
        // jumlah suku, jadi rekursi akan menghabiskan tumpukan.
        private static double NilaiIteratif(Node akar, Func<string, double> ambil)
        {
            var tugas = new Stack<(Node Node, bool Siap)>();
            var nilai = new Stack<double>();
            tugas.Push((akar, false));

            while (tugas.Count > 0)
            {
                var (n, siap) = tugas.Pop();

                if (!siap)
                {
                    var anak = n.Anak;
                    if (anak.Count == 0)
                    {
                        nilai.Push(n.Hitung(ambil, Array.Empty<double>()));
                        continue;
                    }
                    tugas.Push((n, true));
                    for (int i = anak.Count - 1; i >= 0; i--)
                        tugas.Push((anak[i], false));
                    continue;
                }

                int k = n.Anak.Count;
                var nilaiAnak = new double[k];
                for (int i = k - 1; i >= 0; i--) nilaiAnak[i] = nilai.Pop();
                nilai.Push(n.Hitung(ambil, nilaiAnak));
            }

            return nilai.Pop();
        }

        // Jejakan pohon iteratif untuk mengumpulkan nama variabel yang dipakai.
        private static void KumpulkanNama(Node akar, HashSet<string> ke)
        {
            var tumpuk = new Stack<Node>();
            tumpuk.Push(akar);
            while (tumpuk.Count > 0)
            {
                var n = tumpuk.Pop();
                if (n.NamaVariabel is string nama) ke.Add(nama);
                foreach (var a in n.Anak) tumpuk.Push(a);
            }
        }

        public static HasilHitung Hitung(string teks, Dataset ds)
        {
            var hasil = new HasilHitung();
            var kolom = ds.Names;

            var pohon = Terjemah(teks, kolom, out string? galat) as Node;
            if (pohon is null)
            {
                hasil.Galat = galat ?? "Ekspresi tidak bisa diterjemahkan.";
                return hasil;
            }

            var dipakai = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            KumpulkanNama(pohon, dipakai);
            hasil.KolomDipakai = dipakai.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

            var indeks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string n in dipakai)
            {
                int i = ds.IndexOf(n);
                if (i < 0)
                {
                    hasil.Galat = $"Variabel “{n}” tidak ada dalam data.";
                    return hasil;
                }
                indeks[n] = i;
            }

            for (int r = 0; r < ds.RowCount; r++)
            {
                double Ambil(string nama)
                {
                    var baris = ds.Rows[r];
                    int k = indeks[nama];
                    string? mentah = k < baris.Length ? baris[k] : null;
                    return Dataset.ToDouble(mentah) ?? double.NaN;
                }

                try
                {
                    double v = NilaiIteratif(pohon, Ambil);
                    hasil.Nilai.Add(double.IsNaN(v) || double.IsInfinity(v) ? null : v);
                }
                catch (Exception g)
                {
                    hasil.Galat = $"Baris {r + 1}: {g.Message}";
                    hasil.BarisPertamaGagal = r;
                    return hasil;
                }
            }

            return hasil;
        }
    }
}
