using System;

namespace AlatStatistik.Statistics
{

    public static class Distributions
    {
        private const double Sqrt2 = 1.4142135623730951;
        private const double Sqrt2Pi = 2.5066282746310002;

        

        public static double NormalPdf(double z)
            => Math.Exp(-0.5 * z * z) / Sqrt2Pi;

        public static double NormalCdf(double z)
            => 0.5 * Special.Erfc(-z / Sqrt2);

        public static double NormalInv(double p)
        {
            if (p <= 0.0) return double.NegativeInfinity;
            if (p >= 1.0) return double.PositiveInfinity;
            return Bisect(NormalCdf, p, -40.0, 40.0);
        }

        

        public static double StudentTTwoSided(double t, double df)
        {
            if (double.IsNaN(t)) return double.NaN;
            double x = df / (df + t * t);
            return Special.BetaInc(0.5 * df, 0.5, x);
        }

        public static double StudentTCdf(double t, double df)
        {
            double p2 = StudentTTwoSided(t, df);
            return t <= 0 ? 0.5 * p2 : 1.0 - 0.5 * p2;
        }

        public static double StudentTInv(double p, double df)
        {
            if (p <= 0.0) return double.NegativeInfinity;
            if (p >= 1.0) return double.PositiveInfinity;
            double bound = Math.Max(40.0, 10.0 * df + 100.0);
            return Bisect(x => StudentTCdf(x, df), p, -bound, bound);
        }

        

        public static double ChiSquareCdf(double x, double df)
        {
            if (x <= 0) return 0.0;
            return Special.GammaP(0.5 * df, 0.5 * x);
        }

        public static double ChiSquareUpper(double x, double df)
        {
            if (x <= 0) return 1.0;
            return Special.GammaQ(0.5 * df, 0.5 * x);
        }

        public static double ChiSquareInv(double p, double df)
        {
            if (p <= 0.0) return 0.0;
            if (p >= 1.0) return double.PositiveInfinity;
            double bound = Math.Max(100.0, 20.0 * df + 200.0);
            return Bisect(x => ChiSquareCdf(x, df), p, 0.0, bound);
        }

        

        public static double FCdf(double x, double df1, double df2)
        {
            if (x <= 0) return 0.0;
            double z = df1 * x / (df1 * x + df2);
            return Special.BetaInc(0.5 * df1, 0.5 * df2, z);
        }

        public static double FUpper(double x, double df1, double df2)
        {
            if (x <= 0) return 1.0;
            double satuMinusZ = df2 / (df1 * x + df2);
            return Special.BetaInc(0.5 * df2, 0.5 * df1, satuMinusZ);
        }

        public static double FInv(double p, double df1, double df2)
        {
            if (p <= 0.0) return 0.0;
            if (p >= 1.0) return double.PositiveInfinity;
            return Bisect(x => FCdf(x, df1, df2), p, 0.0, 1.0e4);
        }

        

        public static double StudentizedRangeCdf(double q, int k, double df)
        {
            if (double.IsNaN(q) || k < 2 || df <= 0) return double.NaN;
            if (q <= 0) return 0.0;

            var (x, w) = GaussLegendre(16);
            double vAtas = ChiSquareInv(1 - 1e-13, df);
            double sAtas = Math.Sqrt(Math.Max(vAtas, 1e-12) / df);

            Func<double, double> isi = s =>
            {
                if (s <= 0) return 0.0;
                double xq = q * s;
                double dalam = RentangDalam(xq, k);
                
                double logF = 0.5 * df * Math.Log(df) + (df - 1) * Math.Log(s)
                              - 0.5 * df * s * s - Special.LnGamma(0.5 * df)
                              - (0.5 * df - 1) * Math.Log(2.0);
                return Math.Exp(logF) * dalam;
            };

            
            
            
            
            return AdaptifGl(isi, 0.0, sAtas, x, w, 1e-9, 30);
        }

        private static double RentangDalam(double x, int k)
        {
            if (x <= 0) return 0.0;
            var (gx, gw) = GaussLegendre(16);
            return AdaptifGl(z =>
            {
                double beda = NormalCdf(z + x) - NormalCdf(z);
                if (beda <= 0) return 0.0;
                return k * NormalPdf(z) * Math.Pow(beda, k - 1);
            }, -12.0, 12.0, gx, gw, 1e-10, 40);
        }

        public static double StudentizedRangeInv(double p, int k, double df)
        {
            if (p <= 0) return 0.0;
            if (p >= 1) return double.PositiveInfinity;
            
            return Brent(q => StudentizedRangeCdf(q, k, df), p, 0.0, 100.0, 1e-10);
        }

        

        private static (double[] Titik, double[] Bobot) GaussLegendre(int n)
        {
            var titik = new double[n];
            var bobot = new double[n];
            for (int i = 0; i < (n + 1) / 2; i++)
            {
                double z = Math.Cos(Math.PI * (i + 0.75) / (n + 0.5));
                double zLama, turunan = 0;
                do
                {
                    double p1 = 1.0, p2 = 0.0;
                    for (int j = 0; j < n; j++)
                    {
                        double p3 = p2;
                        p2 = p1;
                        p1 = ((2.0 * j + 1.0) * z * p2 - j * p3) / (j + 1.0);
                    }
                    turunan = n * (z * p1 - p2) / (z * z - 1.0);
                    zLama = z;
                    z = zLama - p1 / turunan;
                } while (Math.Abs(z - zLama) > 1e-15);

                titik[i] = -z;
                titik[n - 1 - i] = z;
                bobot[i] = 2.0 / ((1.0 - z * z) * turunan * turunan);
                bobot[n - 1 - i] = bobot[i];
            }
            return (titik, bobot);
        }

        private static double Gl(Func<double, double> f, double a, double b,
                                 double[] titik, double[] bobot)
        {
            double c = 0.5 * (b - a), d = 0.5 * (b + a), jumlah = 0;
            for (int i = 0; i < titik.Length; i++) jumlah += bobot[i] * f(c * titik[i] + d);
            return c * jumlah;
        }

        private static double AdaptifGl(Func<double, double> f, double a, double b,
                                        double[] titik, double[] bobot, double tol, int kedalaman,
                                        double? nilaiUtuh = null)
        {
            double utuh = nilaiUtuh ?? Gl(f, a, b, titik, bobot);
            double m = 0.5 * (a + b);
            double kiri = Gl(f, a, m, titik, bobot);
            double kanan = Gl(f, m, b, titik, bobot);
            double jumlah = kiri + kanan;
            if (kedalaman <= 0 || Math.Abs(jumlah - utuh) <= tol * (1.0 + Math.Abs(jumlah)))
                return jumlah;
            return AdaptifGl(f, a, m, titik, bobot, tol, kedalaman - 1, kiri)
                 + AdaptifGl(f, m, b, titik, bobot, tol, kedalaman - 1, kanan);
        }

        private static double Bisect(Func<double, double> f, double target, double lo, double hi)
        {
            double flo = f(lo);
            for (int i = 0; i < 200; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (mid == lo || mid == hi) break;
                double fmid = f(mid);
                if (fmid < target) { lo = mid; flo = fmid; }
                else hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        private static double Brent(Func<double, double> f, double target,
                                    double a, double b, double tol)
        {
            double fa = f(a) - target, fb = f(b) - target;
            if (fa == 0) return a;
            if (fb == 0) return b;
            if (fa * fb > 0) return Bisect(f, target, a, b);   

            double c = a, fc = fa, d = 0, e = 0;
            for (int i = 0; i < 100; i++)
            {
                if ((fb > 0) == (fc > 0)) { c = a; fc = fa; d = b - a; e = d; }
                if (Math.Abs(fc) < Math.Abs(fb)) { a = b; b = c; c = a; fa = fb; fb = fc; fc = fa; }

                double tol1 = 2.0 * 2.2204460492503131e-16 * Math.Abs(b) + 0.5 * tol;
                double xm = 0.5 * (c - b);
                if (Math.Abs(xm) <= tol1 || fb == 0) return b;

                if (Math.Abs(e) >= tol1 && Math.Abs(fa) > Math.Abs(fb))
                {
                    double s = fb / fa, p, q;
                    if (a == c)
                    {
                        p = 2.0 * xm * s; q = 1.0 - s;
                    }
                    else
                    {
                        q = fa / fc;
                        double r = fb / fc;
                        p = s * (2.0 * xm * q * (q - r) - (b - a) * (r - 1.0));
                        q = (q - 1.0) * (r - 1.0) * (s - 1.0);
                    }
                    if (p > 0) q = -q;
                    p = Math.Abs(p);
                    if (2.0 * p < Math.Min(3.0 * xm * q - Math.Abs(tol1 * q), Math.Abs(e * q)))
                    { e = d; d = p / q; }
                    else { d = xm; e = d; }
                }
                else { d = xm; e = d; }

                a = b; fa = fb;
                b += Math.Abs(d) > tol1 ? d : (xm > 0 ? tol1 : -tol1);
                fb = f(b) - target;
            }
            return b;
        }
    }
}
