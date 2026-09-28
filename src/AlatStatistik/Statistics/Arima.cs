using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;

namespace AlatStatistik.Statistics
{
    public static class Arima
    {
        public sealed class Hasil
        {
            public int P, D, Q;
            public int N;
            public List<string> NamaKoef = new();
            public double[] Koef = Array.Empty<double>();
            public double Mean;
            public double Sigma2;
            public double Llf;
            public double Aic;
            public double[] Resid = Array.Empty<double>();
            public double[] Fitted = Array.Empty<double>();
            public double[] Ramal = Array.Empty<double>();
            public double[]? RamalSe;
            public double LbStat;
            public double LbP;
            public double AkarArMin = double.NaN;
            public double AkarMaMin = double.NaN;
            public int Iterasi;
            public double TerakhirY;
        }

        private sealed class Saring
        {
            public double Llf;
            public double[] V = Array.Empty<double>();
            public double[] F = Array.Empty<double>();
            public double[] Pas = Array.Empty<double>();
            public double[,] AkhirA = new double[0, 0];
            public double[,] AkhirP = new double[0, 0];
            public bool Ok;
        }

        private static double[,] Kali(double[,] a, double[,] b)
        {
            int n = a.GetLength(0), m = a.GetLength(1), p = b.GetLength(1);
            var c = new double[n, p];
            for (int i = 0; i < n; i++)
                for (int k = 0; k < m; k++)
                {
                    double aik = a[i, k];
                    for (int j = 0; j < p; j++) c[i, j] += aik * b[k, j];
                }
            return c;
        }

        private static double[,] Transpos(double[,] a)
        {
            int n = a.GetLength(0), m = a.GetLength(1);
            var c = new double[m, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++) c[j, i] = a[i, j];
            return c;
        }

        private static Saring Kalman(double[] w, double[] phi, double[] theta,
                                     double sigma2, double[,]? p0Awal = null)
        {
            int n = w.Length;
            int p = phi.Length, q = theta.Length;
            int m = Math.Max(1, Math.Max(p, q + 1));
            var T = new double[m, m];
            for (int j = 0; j < m; j++)
                T[0, j] = j < p ? phi[j] : 0.0;
            for (int i = 1; i < m; i++) T[i, i - 1] = 1.0;
            var Z = new double[1, m];
            Z[0, 0] = 1.0;
            for (int j = 1; j < m; j++)
                Z[0, j] = j - 1 < q ? theta[j - 1] : 0.0;

            // P0 stasioner: vec(P) = (I - T(x)T)^-1 vec(RQR')  [DK01].
            int m2 = m * m;
            var IK = new double[m2, m2];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < m; j++)
                    for (int k = 0; k < m; k++)
                        for (int l = 0; l < m; l++)
                        {
                            double vv = (i == k && j == l ? 1.0 : 0.0)
                                        - T[i, k] * T[j, l];
                            IK[i * m + j, k * m + l] = vv;
                        }
            var inv = p0Awal is null ? Regression.Inverse(IK) : null;
            var s = new Saring();
            var P = new double[m, m];
            if (p0Awal is not null)
            {
                Array.Copy(p0Awal, P, P.Length);
            }
            else
            {
                if (inv is null) return s;
                var rq0 = new double[m2, 1];
                rq0[0, 0] = sigma2;
                var vec0 = Kali(inv, rq0);
                for (int i = 0; i < m; i++)
                    for (int j = 0; j < m; j++) P[i, j] = vec0[i * m + j, 0];
            }
            var a = new double[m, 1];

            var v = new double[n];
            var F = new double[n];
            var pas = new double[n];
            double llf = 0;
            var ZT = Transpos(Z);
            for (int t = 0; t < n; t++)
            {
                double Za = 0;
                for (int j = 0; j < m; j++) Za += Z[0, j] * a[j, 0];
                double vt = w[t] - Za;
                double Ft = 0;
                for (int i = 0; i < m; i++)
                    for (int j = 0; j < m; j++) Ft += Z[0, i] * P[i, j] * Z[0, j];
                if (!(Ft > 0) || double.IsNaN(vt) || double.IsInfinity(vt))
                    return s;
                v[t] = vt;
                F[t] = Ft;
                pas[t] = Za;
                llf += -0.5 * (Math.Log(2 * Math.PI) + Math.Log(Ft)
                               + vt * vt / Ft);
                // K = T P Z'/F ; a = T a + K v ; P = T P T' + RQR' - K Z P T'.
                var PZ = Kali(P, ZT);
                var TPZ = Kali(T, PZ);
                var Ta = Kali(T, a);
                for (int i = 0; i < m; i++) a[i, 0] = Ta[i, 0] + TPZ[i, 0] * vt / Ft;
                var TPT = Kali(Kali(T, P), Transpos(T));
                for (int i = 0; i < m; i++)
                    for (int j = 0; j < m; j++)
                        TPT[i, j] -= TPZ[i, 0] * TPZ[j, 0] / Ft;
                TPT[0, 0] += sigma2;
                P = TPT;
            }
            s.Llf = llf;
            s.V = v;
            s.F = F;
            s.Pas = pas;
            s.AkhirA = a;
            s.AkhirP = P;
            s.Ok = true;
            return s;
        }

        private static double Atanh(double x)
        {
            double c = Math.Max(-0.999999, Math.Min(0.999999, x));
            return 0.5 * Math.Log((1 + c) / (1 - c));
        }

        private static void Buka(int p, int q, double[] x,
                                 out double mean, out double[] phi,
                                 out double[] theta, out double logS2)
        {
            // x[0] = rerata deret-taksir m (definisi 'const' statsmodels),
            // BUKAN intersep Box-Jenkins c = m*(1-Σφ).
            mean = x[0];
            phi = new double[p];
            theta = new double[q];
            if (p == 1) phi[0] = Math.Tanh(x[1]);
            else if (p == 2)
            {
                double pi2 = Math.Tanh(x[2]);
                phi[1] = pi2;
                phi[0] = Math.Tanh(x[1]) * (1 - pi2);
            }
            if (q == 1) theta[0] = Math.Tanh(x[1 + p]);
            else if (q == 2)
            {
                // MA memakai konvensi PLUS (1+θL+θ²L²): daerah invertibelnya
                // CERMINAN segitiga standar, jadi koefisien = NEGATIF dari
                // titik segitiga (γ → θ = −γ, bijeksi Levinson). [Jon80].
                double g2 = Math.Tanh(x[1 + p + 1]);
                theta[1] = -g2;
                theta[0] = -Math.Tanh(x[1 + p]) * (1 - g2);
            }
            logS2 = x[1 + p + q];
        }

        private static double Nll(double[] x, double[] w, int p, int q)
        {
            // Transformasi tanh [Jon80] sudah menjamin stasioneritas /
            // invertibilitas, jadi tanpa penalti batas.
            Buka(p, q, x, out _, out var phi, out var theta, out double logS2);
            double s2 = Math.Exp(logS2);
            if (!(s2 > 0) || double.IsInfinity(s2)) return 1e100;
            var s = Kalman(Demean(w, x[0], phi), phi, theta, s2);
            return s.Ok ? -s.Llf : 1e100;
        }

        private static double[] Demean(double[] w, double m, double[] phi)
        {
            var keluar = new double[w.Length];
            for (int i = 0; i < w.Length; i++) keluar[i] = w[i] - m;
            return keluar;
        }

        internal static double[] NelderMead(Func<double[], double> f, double[] x0,
                                           double[] langkah, out int iter)
        {
            int d = x0.Length;
            var sx = new double[d + 1][];
            var fv = new double[d + 1];
            sx[0] = (double[])x0.Clone();
            fv[0] = f(sx[0]);
            for (int i = 0; i < d; i++)
            {
                sx[i + 1] = (double[])x0.Clone();
                sx[i + 1][i] += langkah[i];
                fv[i + 1] = f(sx[i + 1]);
            }
            iter = 0;
            int cap = 2000 * Math.Max(1, d);
            while (iter < cap)
            {
                var ord = Enumerable.Range(0, d + 1).OrderBy(i => fv[i]).ToArray();
                double fmax = fv[ord[d]], fmin = fv[ord[0]];
                double diam = 0;
                for (int i = 1; i <= d; i++)
                    for (int k = 0; k < d; k++)
                        diam = Math.Max(diam, Math.Abs(sx[ord[i]][k] - sx[ord[0]][k]));
                if (Math.Abs(fmax - fmin) < 1e-12 && diam < 1e-7) break;
                var centroid = new double[d];
                for (int i = 0; i < d; i++)
                    for (int k = 0; k < d; k++) centroid[k] += sx[ord[i]][k] / d;
                var xr = new double[d];
                for (int k = 0; k < d; k++)
                    xr[k] = centroid[k] + (centroid[k] - sx[ord[d]][k]);
                double fr = f(xr);
                if (fr < fv[ord[0]])
                {
                    var xe = new double[d];
                    for (int k = 0; k < d; k++)
                        xe[k] = centroid[k] + 2 * (xr[k] - centroid[k]);
                    double fe = f(xe);
                    sx[ord[d]] = fe < fr ? xe : xr;
                    fv[ord[d]] = fe < fr ? fe : fr;
                }
                else if (fr < fv[ord[d - 1]])
                {
                    sx[ord[d]] = xr;
                    fv[ord[d]] = fr;
                }
                else
                {
                    var xc = new double[d];
                    if (fr < fv[ord[d]])
                        for (int k = 0; k < d; k++)
                            xc[k] = centroid[k] + 0.5 * (xr[k] - centroid[k]);
                    else
                        for (int k = 0; k < d; k++)
                            xc[k] = centroid[k] + 0.5 * (sx[ord[d]][k] - centroid[k]);
                    double fc = f(xc);
                    if (fc < fv[ord[d]]) { sx[ord[d]] = xc; fv[ord[d]] = fc; }
                    else
                    {
                        for (int i = 1; i <= d; i++)
                        {
                            for (int k = 0; k < d; k++)
                                sx[ord[i]][k] = sx[ord[0]][k]
                                    + 0.5 * (sx[ord[i]][k] - sx[ord[0]][k]);
                            fv[ord[i]] = f(sx[ord[i]]);
                        }
                    }
                }
                iter++;
            }
            int best = 0;
            for (int i = 1; i <= d; i++) if (fv[i] < fv[best]) best = i;
            return sx[best];
        }

        private static double[,] P0Iterasi(double[] phi, double[] theta, double sigma2)
        {
            // Kovarians stasioner lewat PENGGANDAAN Smith (12x = 4096 langkah
            // setara, konvergensi kuadratik): B <- B + ABA', A <- A^2. Eksak
            // untuk MA murni (T nilpoten), terukur 1e-12 vs Lyapunov scipy
            int p = phi.Length, q = theta.Length;
            int m = Math.Max(1, Math.Max(p, q + 1));
            var A = new double[m, m];
            for (int j = 0; j < m; j++) A[0, j] = j < p ? phi[j] : 0.0;
            for (int i = 1; i < m; i++) A[i, i - 1] = 1.0;
            var B = new double[m, m];
            B[0, 0] = sigma2;
            for (int k = 0; k < 12; k++)
            {
                var AB = Kali(A, B);
                var ABA = Kali(AB, Transpos(A));
                for (int i = 0; i < m; i++)
                    for (int j = 0; j < m; j++) B[i, j] += ABA[i, j];
                A = Kali(A, A);
            }
            return B;
        }

        private static double[] KaliPolinom(double[] a, double[] b)
        {
            var c = new double[a.Length + b.Length - 1];
            for (int i = 0; i < a.Length; i++)
                for (int j = 0; j < b.Length; j++) c[i + j] += a[i] * b[j];
            return c;
        }

        private static void BukaS(int p, int q, int P, int Q, int s,
                                  bool adaRerata, double[] x,
                                  out double mean, out double[] phi, out double[] theta,
                                  out double[] arDir, out double[] maDir)
        {
            // x = [rerata?, u_ar(p), U_AR(P), v_ma(q), V_MA(Q), logS2].
            // Rerata hanya bila tak-terintegrasi (d + D == 0) — meniru
            // trend='n' statsmodels bila terintegrasi.
            int t = adaRerata ? 1 : 0;
            mean = adaRerata ? x[0] : 0;
            var phiD = new double[p];
            var thetaD = new double[q];
            var phiSD = new double[P];
            var thetaSD = new double[Q];
            if (p == 1) phiD[0] = Math.Tanh(x[t++]);
            else if (p == 2)
            {
                double pi2 = Math.Tanh(x[t + 1]);
                phiD[1] = pi2;
                phiD[0] = Math.Tanh(x[t]) * (1 - pi2);
                t += 2;
            }
            for (int i = 0; i < P; i++) phiSD[i] = Math.Tanh(x[t++]);
            if (q == 1) thetaD[0] = Math.Tanh(x[t++]);
            else if (q == 2)
            {
                // Cermin segitiga standar (konvensi PLUS) — lihat Buka.
                double g2 = Math.Tanh(x[t + 1]);
                thetaD[1] = -g2;
                thetaD[0] = -Math.Tanh(x[t]) * (1 - g2);
                t += 2;
            }
            for (int i = 0; i < Q; i++) thetaSD[i] = Math.Tanh(x[t++]);
            arDir = phiD.Concat(phiSD).ToArray();
            maDir = thetaD.Concat(thetaSD).ToArray();

            var arNs = new double[p + 1];
            arNs[0] = 1.0;
            for (int i = 0; i < p; i++) arNs[i + 1] = -phiD[i];
            var arSs = new double[P * s + 1];
            arSs[0] = 1.0;
            for (int i = 0; i < P; i++) arSs[(i + 1) * s] = -phiSD[i];
            var arTot = KaliPolinom(arNs, arSs);
            var phiFull = new double[arTot.Length - 1];
            for (int i = 0; i < phiFull.Length; i++) phiFull[i] = -arTot[i + 1];

            var maNs = new double[q + 1];
            maNs[0] = 1.0;
            for (int i = 0; i < q; i++) maNs[i + 1] = thetaD[i];
            var maSs = new double[Q * s + 1];
            maSs[0] = 1.0;
            for (int i = 0; i < Q; i++) maSs[(i + 1) * s] = thetaSD[i];
            var maTot = KaliPolinom(maNs, maSs);
            var thetaFull = new double[maTot.Length - 1];
            for (int i = 0; i < thetaFull.Length; i++) thetaFull[i] = maTot[i + 1];

            phi = phiFull;
            theta = thetaFull;
        }

        private static double AkarMusiman(double[] koefLangsung, int s)
        {
            // Batas bawah |akar| untuk faktor musiman orde-1:
            // L^s = 1/c  ->  |L| = |c|^(-1/s) > 1 bila |c| < 1.
            double mn = double.PositiveInfinity;
            foreach (double c in koefLangsung)
            {
                if (Math.Abs(c) < 1e-12) continue;
                double r = Math.Pow(Math.Abs(c), -1.0 / s);
                if (r < mn) mn = r;
            }
            return mn;
        }

        public static Hasil? PasangMusiman(double[] y, int p, int d, int q,
                                           int P, int D, int Q, int s, int h)
        {
            int n0 = y.Length;
            if (n0 < 24 || p < 0 || p > 2 || q < 0 || q > 2 || d < 0 || d > 1
                || P < 0 || P > 1 || Q < 0 || Q > 1 || D < 0 || D > 1
                || s < 2 || s > 12)
                return null;
            if (y.Any(double.IsNaN)) return null;
            bool adaRerata = d + D == 0;
            var w = (double[])y.Clone();
            for (int t = 0; t < d; t++)
            {
                var dw = new double[w.Length - 1];
                for (int i = 0; i < dw.Length; i++) dw[i] = w[i + 1] - w[i];
                w = dw;
            }
            for (int t = 0; t < D; t++)
            {
                var dw = new double[w.Length - s];
                for (int i = 0; i < dw.Length; i++) dw[i] = w[i + s] - w[i];
                w = dw;
            }
            int n = w.Length;
            if (n < 20) return null;
            double mean = 0;
            for (int i = 0; i < n; i++) mean += w[i];
            mean /= n;
            double vr = 0;
            for (int i = 0; i < n; i++) vr += (w[i] - mean) * (w[i] - mean);
            vr /= n;
            if (!(vr > 0)) return null;

            int dim = (adaRerata ? 1 : 0) + p + P + q + Q + 1;
            var langkah = new double[dim];
            for (int i = 0; i < dim; i++) langkah[i] = 0.2;
            if (adaRerata) langkah[0] = Math.Sqrt(vr);
            var xA = new double[dim];
            if (adaRerata) xA[0] = mean;
            xA[dim - 1] = Math.Log(vr);
            var xB = new double[dim];
            if (adaRerata) xB[0] = mean;
            xB[dim - 1] = Math.Log(vr);
            if (dim > 2) { xB[adaRerata ? 1 : 0] = 0.3; }
            Func<double[], double> f = x => NllS(x, w, p, q, P, Q, s, adaRerata);
            var sA = NelderMead(f, xA, langkah, out int itA);
            var sB = NelderMead(f, xB, langkah, out int itB);
            var xs = f(sA) <= f(sB) ? sA : sB;

            BukaS(p, q, P, Q, s, adaRerata, xs,
                  out double m0, out var phiF, out var thetaF,
                  out var arDir, out var maDir);
            double s2 = Math.Exp(xs[dim - 1]);
            var p0 = P0Iterasi(phiF, thetaF, s2);
            var y0 = new double[n];
            for (int i = 0; i < n; i++) y0[i] = w[i] - m0;
            var saring = Kalman(y0, phiF, thetaF, s2, p0);
            if (!saring.Ok) return null;

            var hh = new Hasil
            {
                P = p, D = d, Q = q, N = n,
                Mean = m0, Sigma2 = s2, Llf = saring.Llf,
                Iterasi = Math.Max(itA, itB), TerakhirY = y[n0 - 1],
            };
            hh.Aic = -2 * saring.Llf + 2 * dim;
            hh.NamaKoef = new List<string>();
            var kv = new List<double>();
            if (adaRerata) { hh.NamaKoef.Add("const"); kv.Add(m0); }
            for (int i = 0; i < p; i++)
            {
                hh.NamaKoef.Add($"ar.L{i + 1}");
                kv.Add(arDir[i]);
            }
            for (int i = 0; i < P; i++)
            {
                hh.NamaKoef.Add($"ar.S.L{(i + 1) * s}");
                kv.Add(arDir[p + i]);
            }
            for (int i = 0; i < q; i++)
            {
                hh.NamaKoef.Add($"ma.L{i + 1}");
                kv.Add(maDir[i]);
            }
            for (int i = 0; i < Q; i++)
            {
                hh.NamaKoef.Add($"ma.S.L{(i + 1) * s}");
                kv.Add(maDir[q + i]);
            }
            hh.NamaKoef.Add("sigma2");
            kv.Add(s2);
            hh.Koef = kv.ToArray();

            hh.Resid = new double[n];
            for (int t = 0; t < n; t++) hh.Resid[t] = saring.V[t];
            var pasW = new double[n];
            for (int t = 0; t < n; t++) pasW[t] = saring.Pas[t] + m0;
            hh.Fitted = d + D == 0
                ? pasW
                : IntegralBalik(y, pasW, d, D, s, true, n0 - n).Take(n0).ToArray();

            int hh2 = Math.Min(24, Math.Max(1, h));
            var ramalW = RamalW(saring.AkhirA, phiF, thetaF, m0, hh2);
            var ramalExt = d + D == 0
                ? ramalW
                : IntegralBalik(y, ramalW, d, D, s, false, n0);
            hh.Ramal = new double[hh2];
            for (int t = 0; t < hh2; t++)
                hh.Ramal[t] = d + D == 0 ? ramalExt[t] : ramalExt[n0 + t];
            hh.RamalSe = null;

            int nl = Math.Min(n - 2, Math.Max(10, 2 * s));
            var acf = AcfPacf.Autocorr(hh.Resid, nl);
            var (stat, pv) = AcfPacf.LjungBox(acf, n, nl);
            hh.LbStat = stat;
            hh.LbP = pv;
            double akarAr = p > 0 ? AkarMin(phiF.Take(p).ToArray())
                                  : double.PositiveInfinity;
            if (P > 0)
                akarAr = Math.Min(akarAr, AkarMusiman(arDir.Skip(p).ToArray(), s));
            hh.AkarArMin = akarAr;
            double akarMa = q > 0 ? AkarMin(thetaF.Take(q).ToArray())
                                  : double.PositiveInfinity;
            if (Q > 0)
                akarMa = Math.Min(akarMa, AkarMusiman(maDir.Skip(q).ToArray(), s));
            hh.AkarMaMin = akarMa;
            return hh;
        }

        private static double NllS(double[] x, double[] w, int p, int q,
                                   int P, int Q, int s, bool adaRerata)
        {
            int m = Math.Max(p + P * s, q + Q * s + 1);
            if (m < 1 || m > 15) return 1e100;
            BukaS(p, q, P, Q, s, adaRerata, x,
                  out double mean, out var phi, out var theta,
                  out _, out _);
            double s2 = Math.Exp(x[x.Length - 1]);
            if (!(s2 > 0) || double.IsInfinity(s2)) return 1e100;
            var p0 = P0Iterasi(phi, theta, s2);
            var y0 = new double[w.Length];
            for (int i = 0; i < w.Length; i++) y0[i] = w[i] - mean;
            var ss = Kalman(y0, phi, theta, s2, p0);
            return ss.Ok ? -ss.Llf : 1e100;
        }

        private static double[] RamalW(double[,] aa0,
                                       double[] phi, double[] theta,
                                       double m0, int hh2)
        {
            int m = Math.Max(1, Math.Max(phi.Length, theta.Length + 1));
            var aa = (double[,])aa0.Clone();
            var ramal = new double[hh2];
            var T = new double[m, m];
            for (int j = 0; j < m; j++) T[0, j] = j < phi.Length ? phi[j] : 0.0;
            for (int i = 1; i < m; i++) T[i, i - 1] = 1.0;
            var Z = new double[m];
            Z[0] = 1.0;
            for (int j = 1; j < m; j++) Z[j] = j - 1 < theta.Length ? theta[j - 1] : 0.0;
            for (int t = 0; t < hh2; t++)
            {
                double Za = 0;
                for (int j = 0; j < m; j++) Za += Z[j] * aa[j, 0];
                ramal[t] = Za + m0;
                aa = Kali(T, aa);
            }
            return ramal;
        }

        private static double[] IntegralBalik(double[] y, double[] wBaru,
                                              int d, int D, int s,
                                              bool satuLangkah, int awal)
        {
            // y = level asal; wBaru[t] sejajar level ke-(awal+t).
            // fitted: awal = n0 - len; ramalan: awal = n0.
            // Mendukung d <= 1, D <= 1 (komutatif, eksak).
            var yExt = new List<double>(y);
            for (int t = 0; t < wBaru.Length; t++)
            {
                int i = awal + t;
                double v = wBaru[t];
                // Masa lalu: y asal bila satu-langkah; yExt bila rekursif.
                // (Untuk ramalan, indeks < n0 tetap aktual karena yExt
                // memuat y asal di sana sampai tertimpa ramalan.)
                if (D > 0) v += satuLangkah ? y[i - s] : yExt[i - s];
                if (d > 0)
                {
                    v += satuLangkah ? y[i - 1] : yExt[i - 1];
                    if (D > 0) v -= satuLangkah ? y[i - s - 1] : yExt[i - s - 1];
                }
                if (i < yExt.Count) yExt[i] = v;
                else yExt.Add(v);
            }
            return yExt.ToArray();
        }

        private static double AkarMin(double[] koef)
        {
            if (koef.Length == 1)
            {
                if (Math.Abs(koef[0]) < 1e-12) return double.PositiveInfinity;
                return Math.Abs(1.0 / koef[0]);
            }
            if (koef.Length == 2)
            {
                // 1 - a L - b L^2 = 0  ->  b L^2 + a L - 1 = 0.
                double a = koef[0], b = koef[1];
                if (Math.Abs(b) < 1e-12) return AkarMin(new[] { a });
                double dis = a * a + 4 * b;
                if (dis < 0) return Math.Abs(1.0 / Math.Sqrt(-b));
                double sq = Math.Sqrt(dis);
                double r1 = (-a + sq) / (2 * b), r2 = (-a - sq) / (2 * b);
                return Math.Min(Math.Abs(r1), Math.Abs(r2));
            }
            return double.NaN;
        }

        public static Hasil? Pasang(double[] y, int p, int d, int q, int h)
        {
            int n0 = y.Length;
            if (n0 < 12 || p < 0 || p > 2 || q < 0 || q > 2 || d < 0 || d > 1)
                return null;
            if (y.Any(double.IsNaN)) return null;
            var w = (double[])y.Clone();
            for (int t = 0; t < d; t++)
            {
                var dw = new double[w.Length - 1];
                for (int i = 0; i < dw.Length; i++) dw[i] = w[i + 1] - w[i];
                w = dw;
            }
            int n = w.Length;
            if (n < 10) return null;
            double mean = w.Sum() / n;
            double vr = w.Sum(v => (v - mean) * (v - mean)) / n;
            if (!(vr > 0)) return null;

            // Mulai-1: Yule-Walker untuk AR; mulai-2: nol.
            double r1 = 0;
            {
                double ss = 0;
                for (int i = 0; i < n; i++) ss += (w[i] - mean) * (w[i] - mean);
                double s1 = 0;
                for (int i = 1; i < n; i++) s1 += (w[i] - mean) * (w[i - 1] - mean);
                r1 = ss > 0 ? s1 / ss : 0;
            }
            int dim = 1 + p + q + 1;
            var langkah = new double[dim];
            langkah[0] = Math.Sqrt(vr);
            for (int i = 1; i < dim - 1; i++) langkah[i] = 0.2;
            langkah[dim - 1] = 0.2;
            var xA = new double[dim];
            xA[0] = mean;
            if (p >= 1) xA[1] = Atanh(Math.Max(-0.9, Math.Min(0.9, r1)));
            xA[dim - 1] = Math.Log(vr);
            Func<double[], double> f = x => Nll(x, w, p, q);
            var xB = new double[dim];
            xB[0] = mean;
            xB[dim - 1] = Math.Log(vr);
            var sA = NelderMead(f, xA, langkah, out int itA);
            var sB = NelderMead(f, xB, langkah, out int itB);
            var xs = f(sA) <= f(sB) ? sA : sB;

            Buka(p, q, xs, out double m0, out var phi, out var theta, out double logS2);
            double s2 = Math.Exp(logS2);
            var saring = Kalman(Demean(w, m0, phi), phi, theta, s2);
            if (!saring.Ok) return null;

            var hh = new Hasil
            {
                P = p, D = d, Q = q, N = n,
                Mean = m0, Sigma2 = s2, Llf = saring.Llf,
                Iterasi = Math.Max(itA, itB), TerakhirY = y[n0 - 1],
            };
            int kpar = 1 + p + q + 1;
            hh.Aic = -2 * saring.Llf + 2 * kpar;
            hh.NamaKoef = new List<string> { d > 0 ? "trend" : "const" };
            var kv = new List<double> { m0 };
            for (int i = 0; i < p; i++)
            {
                hh.NamaKoef.Add($"ar.L{i + 1}");
                kv.Add(phi[i]);
            }
            for (int i = 0; i < q; i++)
            {
                hh.NamaKoef.Add($"ma.L{i + 1}");
                kv.Add(theta[i]);
            }
            hh.NamaKoef.Add("sigma2");
            kv.Add(s2);
            hh.Koef = kv.ToArray();

            hh.Resid = new double[n];
            hh.Fitted = new double[n];
            for (int t = 0; t < n; t++)
            {
                hh.Resid[t] = saring.V[t];
                // d = 1: fitted dikembalikan ke skala level
                // (ŷ_t = y_{t-1} + ŵ_t) agar sebanding acuan.
                hh.Fitted[t] = d == 0 ? saring.Pas[t] + m0
                                      : y[t] + saring.Pas[t] + m0;
            }

            // Ramalan: dari keadaan akhir; d = 1 diintegralkan dari y_T.
            int hh2 = Math.Min(24, Math.Max(1, h));
            var ramal = new double[hh2];
            double[]? ramalSe = d == 0 ? new double[hh2] : null;
            {
                int m = Math.Max(1, Math.Max(p, q + 1));
                var aa = (double[,])saring.AkhirA.Clone();
                var PP = (double[,])saring.AkhirP.Clone();
                var T = new double[m, m];
                for (int j = 0; j < m; j++) T[0, j] = j < p ? phi[j] : 0.0;
                for (int i = 1; i < m; i++) T[i, i - 1] = 1.0;
                var Z = new double[m];
                Z[0] = 1.0;
                for (int j = 1; j < m; j++) Z[j] = j - 1 < q ? theta[j - 1] : 0.0;
                double jalan = y[n0 - 1];
                for (int t = 0; t < hh2; t++)
                {
                    double Za = 0;
                    for (int j = 0; j < m; j++) Za += Z[j] * aa[j, 0];
                    double Fh = 0;
                    for (int i = 0; i < m; i++)
                        for (int j = 0; j < m; j++) Fh += Z[i] * PP[i, j] * Z[j];
                    double pw = Za + m0;
                    if (d == 0) { ramal[t] = pw; if (ramalSe is not null) ramalSe[t] = Math.Sqrt(Math.Max(Fh, 0)); }
                    else { jalan += pw; ramal[t] = jalan; }
                    var Ta = Kali(T, aa);
                    aa = Ta;
                    var TPT = Kali(Kali(T, PP), Transpos(T));
                    TPT[0, 0] += s2;
                    PP = TPT;
                }
            }
            hh.Ramal = ramal;
            hh.RamalSe = ramalSe;

            var acf = AcfPacf.Autocorr(hh.Resid, 10);
            var (stat, pv) = AcfPacf.LjungBox(acf, n, 10);
            hh.LbStat = stat;
            hh.LbP = pv;
            hh.AkarArMin = AkarMin(phi);
            hh.AkarMaMin = AkarMin(theta);
            return hh;
        }

        public static List<ResultBlock> ArimaBlocks(Dataset ds, string deret,
            int p, int d, int q, int h,
            int P = 0, int D = 0, int Q = 0, int s = 12)
        {
            var blocks = new List<ResultBlock>
            {
                Blocks.Heading("ARIMA (Box–Jenkins, MLE Kalman)", 1)
            };
            var nilai = Descriptives.CleanNumbers(ds, deret)
                                   .Where(v => !double.IsNaN(v)).ToArray();
            if (nilai.Length < 12)
            {
                blocks.Add(Blocks.Note("Amatan hingga kurang dari 12.",
                                       NoteKind.Error));
                return blocks;
            }
            p = Math.Min(2, Math.Max(0, p));
            d = Math.Min(1, Math.Max(0, d));
            q = Math.Min(2, Math.Max(0, q));
            P = Math.Min(1, Math.Max(0, P));
            D = Math.Min(1, Math.Max(0, D));
            Q = Math.Min(1, Math.Max(0, Q));
            s = Math.Min(12, Math.Max(2, s));
            bool musiman = P + D + Q > 0;

            var hh = musiman ? PasangMusiman(nilai, p, d, q, P, D, Q, s,
                                             Math.Min(24, Math.Max(1, h)))
                             : Pasang(nilai, p, d, q, Math.Min(24, Math.Max(1, h)));
            if (hh is null)
            {
                blocks.Add(Blocks.Note("Model tidak bisa disesuaikan.",
                                       NoteKind.Error));
                return blocks;
            }

            blocks.Add(Blocks.Rumus("Rumus yang dipakai", DaftarRumus.Arima));
            string ordo = musiman ? $"ARIMA({hh.P},{hh.D},{hh.Q})×({P},{D},{Q},{s})"
                                  : $"ARIMA({hh.P},{hh.D},{hh.Q})";

            blocks.Add(Blocks.Substitusi("Pemasukan nilai",
                ("Deret", deret),
                ("Ordo", ordo),
                ("Amatan taksir (setelah diferensiasi)",
                 $"n = {Fmt.Int(hh.N)}"),
                ("ln L", Fmt.Num(hh.Llf, 4)),
                ("AIC", Fmt.Num(hh.Aic, 4)),
                ("Ljung-Box(10) residu",
                 $"Q = {Fmt.Num(hh.LbStat, 3)}, p = {Fmt.Num(hh.LbP, 4)}")));

            blocks.Add(Blocks.Table(
                "Koefisien (MLE Kalman eksak)",
                new[] { "Parameter", "Nilai" },
                hh.NamaKoef.Select((nm, i) =>
                    new[] { nm, Fmt.Num(hh.Koef[i], 6) }).ToArray(),
                "const = rerata deret (d = 0); trend = rerata diferensiasi "
                + "(d = 1, kemiringan tren linear)."));

            var barisR = new List<string[]>();
            for (int t = 0; t < hh.Ramal.Length; t++)
            {
                string se = hh.RamalSe is null ? "—"
                    : Fmt.Num(hh.RamalSe[t], 4);
                barisR.Add(new[] { $"T+{t + 1}", Fmt.Num(hh.Ramal[t], 4), se });
            }
            blocks.Add(Blocks.Table(
                $"Ramalan {hh.Ramal.Length} langkah",
                new[] { "Horizon", "Ramalan", "SE" },
                barisR.ToArray(),
                d == 0 ? "SE dari rekursi MSE Kalman."
                       : "SE tak dilaporkan untuk d = 1 (akumulasi "
                       + "integral; hanya titik ramalan)."));

            return blocks;
        }
    }
}
