using System;

namespace AlatStatistik.Statistics
{

    public static class Special
    {
        private const double EPS = 3.0e-16;
        private const double FPMIN = 1.0e-300;
        private const int ITMAX = 300;

        

        private static readonly double[] Lanczos =
        {
            0.99999999999980993, 676.5203681218851, -1259.1392167224028,
            771.32342877765313, -176.61502916214059, 12.507343278686905,
            -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7
        };

        public static double LnGamma(double x)
        {
            if (x < 0.5)
            {
                
                return Math.Log(Math.PI / Math.Abs(Math.Sin(Math.PI * x))) - LnGamma(1.0 - x);
            }

            x -= 1.0;
            double a = Lanczos[0];
            double t = x + 7.5;
            for (int i = 1; i < 9; i++) a += Lanczos[i] / (x + i);
            return 0.5 * Math.Log(2.0 * Math.PI) + (x + 0.5) * Math.Log(t) - t + Math.Log(a);
        }

        public static double GammaP(double a, double x)
        {
            if (double.IsNaN(a) || double.IsNaN(x)) return double.NaN;
            if (x <= 0.0) return 0.0;
            if (a <= 0.0) throw new ArgumentOutOfRangeException(nameof(a), "a harus positif.");

            if (x < a + 1.0)
            {
                
                double ap = a, sum = 1.0 / a, del = sum;
                for (int n = 1; n <= ITMAX; n++)
                {
                    ap += 1.0;
                    del *= x / ap;
                    sum += del;
                    if (Math.Abs(del) < Math.Abs(sum) * EPS) break;
                }
                return sum * Math.Exp(-x + a * Math.Log(x) - LnGamma(a));
            }

            
            return 1.0 - GammaQ(a, x);
        }

        public static double GammaQ(double a, double x)
        {
            if (double.IsNaN(a) || double.IsNaN(x)) return double.NaN;
            if (x <= 0.0) return 1.0;
            if (a <= 0.0) throw new ArgumentOutOfRangeException(nameof(a), "a harus positif.");

            if (x < a + 1.0) return 1.0 - GammaP(a, x);

            double b = x + 1.0 - a;
            double c = 1.0 / FPMIN;
            double d = 1.0 / b;
            double h = d;

            for (int i = 1; i <= ITMAX; i++)
            {
                double an = -i * (i - a);
                b += 2.0;
                d = an * d + b;
                if (Math.Abs(d) < FPMIN) d = FPMIN;
                c = b + an / c;
                if (Math.Abs(c) < FPMIN) c = FPMIN;
                d = 1.0 / d;
                double del = d * c;
                h *= del;
                if (Math.Abs(del - 1.0) < EPS) break;
            }

            return Math.Exp(-x + a * Math.Log(x) - LnGamma(a)) * h;
        }

        

        private static double BetaCf(double a, double b, double x)
        {
            double qab = a + b, qap = a + 1.0, qam = a - 1.0;
            double c = 1.0;
            double d = 1.0 - qab * x / qap;
            if (Math.Abs(d) < FPMIN) d = FPMIN;
            d = 1.0 / d;
            double h = d;

            for (int m = 1; m <= ITMAX; m++)
            {
                double m2 = 2 * m;
                double aa = m * (b - m) * x / ((qam + m2) * (a + m2));
                d = 1.0 + aa * d;
                if (Math.Abs(d) < FPMIN) d = FPMIN;
                c = 1.0 + aa / c;
                if (Math.Abs(c) < FPMIN) c = FPMIN;
                d = 1.0 / d;
                h *= d * c;

                aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
                d = 1.0 + aa * d;
                if (Math.Abs(d) < FPMIN) d = FPMIN;
                c = 1.0 + aa / c;
                if (Math.Abs(c) < FPMIN) c = FPMIN;
                d = 1.0 / d;
                double del = d * c;
                h *= del;
                if (Math.Abs(del - 1.0) < EPS) break;
            }
            return h;
        }

        public static double BetaInc(double a, double b, double x)
        {
            if (double.IsNaN(a) || double.IsNaN(b) || double.IsNaN(x)) return double.NaN;
            if (x <= 0.0) return 0.0;
            if (x >= 1.0) return 1.0;

            double bt = Math.Exp(
                LnGamma(a + b) - LnGamma(a) - LnGamma(b)
                + a * Math.Log(x) + b * Math.Log(1.0 - x));

            if (x < (a + 1.0) / (a + b + 2.0))
                return bt * BetaCf(a, b, x) / a;

            return 1.0 - bt * BetaCf(b, a, 1.0 - x) / b;
        }

        

        public static double Erf(double x)
        {
            if (x == 0.0) return 0.0;
            double sign = x < 0 ? -1.0 : 1.0;
            double a = Math.Abs(x);
            return sign * GammaP(0.5, a * a);
        }

        public static double Erfc(double x)
        {
            if (x == 0.0) return 1.0;
            if (x < 0.0) return 2.0 - Erfc(-x);
            if (x > XErfcMaks) return ErfcEksak(x);

            double t = 2.0 * x / XErfcMaks - 1.0;      
            double b1 = 0.0, b2 = 0.0;
            for (int i = KoefErfc.Length - 1; i >= 1; i--)
            {
                double b0 = 2.0 * t * b1 - b2 + KoefErfc[i];
                b2 = b1;
                b1 = b0;
            }
            double g = t * b1 - b2 + KoefErfc[0];
            return g * Math.Exp(-x * x);
        }

        public static double ErfcEksak(double x)
        {
            if (x == 0.0) return 1.0;
            double a = Math.Abs(x);
            double value = a < 26.0 ? GammaQ(0.5, a * a) : 0.0;
            return x >= 0 ? value : 1.0 + GammaP(0.5, a * a);
        }

        

        private const int NErfc = 40;
        private const double XErfcMaks = 10.0;

        private static double SkalaErfc(double x) => Math.Exp(x * x) * ErfcEksak(x);

        private static readonly double[] KoefErfc = BuatKoefErfc();

        private static double[] BuatKoefErfc()
        {
            var c = new double[NErfc];
            for (int j = 0; j < NErfc; j++)
            {
                double jumlah = 0.0;
                for (int i = 0; i < NErfc; i++)
                {
                    double theta = Math.PI * (i + 0.5) / NErfc;
                    double x = 0.5 * XErfcMaks * (1.0 + Math.Cos(theta));
                    jumlah += SkalaErfc(x) * Math.Cos(j * theta);
                }
                c[j] = 2.0 * jumlah / NErfc;
            }
            c[0] *= 0.5;
            return c;
        }
    }
}
