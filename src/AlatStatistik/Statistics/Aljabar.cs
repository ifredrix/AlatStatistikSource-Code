using System;
using System.Collections.Generic;

namespace AlatStatistik.Statistics
{

    public class HasilEigen
    {

        public double[] Nilai = Array.Empty<double>();

        public double[,] Vektor = new double[0, 0];
    }

    public static class Aljabar
    {

        public static double[,] Kali(double[,] a, double[,] b)
        {
            int m = a.GetLength(0), k = a.GetLength(1), n = b.GetLength(1);
            var hasil = new double[m, n];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++)
                {
                    double s = 0;
                    for (int t = 0; t < k; t++) s += a[i, t] * b[t, j];
                    hasil[i, j] = s;
                }
            return hasil;
        }

        public static double[] KaliVektor(double[,] a, double[] v)
        {
            int m = a.GetLength(0), k = a.GetLength(1);
            var hasil = new double[m];
            for (int i = 0; i < m; i++)
            {
                double s = 0;
                for (int t = 0; t < k; t++) s += a[i, t] * v[t];
                hasil[i] = s;
            }
            return hasil;
        }

        public static double[,] Transpose(double[,] a)
        {
            int m = a.GetLength(0), n = a.GetLength(1);
            var t = new double[n, m];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++) t[j, i] = a[i, j];
            return t;
        }

        public static double Determinan(double[,] a)
        {
            int n = a.GetLength(0);
            var m = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) m[i, j] = a[i, j];

            double det = 1.0;
            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < n; r++)
                    if (Math.Abs(m[r, col]) > Math.Abs(m[pivot, col])) pivot = r;

                // "!(... > tol)" supaya NaN juga dianggap singular; Math.Abs(NaN)
                // < tol selalu salah dan determinan NaN akan lolos.
                if (!(Math.Abs(m[pivot, col]) > 1e-14)) return double.NaN;

                if (pivot != col)
                {
                    for (int j = 0; j < n; j++) (m[col, j], m[pivot, j]) = (m[pivot, j], m[col, j]);
                    det = -det;          
                }

                det *= m[col, col];
                for (int r = col + 1; r < n; r++)
                {
                    double f = m[r, col] / m[col, col];
                    for (int j = col; j < n; j++) m[r, j] -= f * m[col, j];
                }
            }
            return det;
        }

        public static HasilEigen EigenSimetris(double[,] sumber, int putaranMaks = 100, double toleransi = 1e-12)
        {
            int n = sumber.GetLength(0);
            var a = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) a[i, j] = sumber[i, j];

            var v = new double[n, n];
            for (int i = 0; i < n; i++) v[i, i] = 1.0;

            for (int putaran = 0; putaran < putaranMaks; putaran++)
            {
                double luarDiagonal = 0;
                for (int i = 0; i < n - 1; i++)
                    for (int j = i + 1; j < n; j++) luarDiagonal += a[i, j] * a[i, j];

                if (Math.Sqrt(luarDiagonal) <= toleransi) break;

                for (int i = 0; i < n - 1; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        if (Math.Abs(a[i, j]) < 1e-300) continue;

                        double tau = (a[j, j] - a[i, i]) / (2.0 * a[i, j]);
                        double tanda = tau >= 0 ? 1.0 : -1.0;
                        double t = 1.0 / (tau + tanda * Math.Sqrt(tau * tau + 1.0));
                        double c = 1.0 / Math.Sqrt(t * t + 1.0);
                        double s = t * c;

                        
                        for (int k = 0; k < n; k++)
                        {
                            double aki = a[k, i], akj = a[k, j];
                            a[k, i] = c * aki - s * akj;
                            a[k, j] = s * aki + c * akj;
                        }
                        
                        for (int k = 0; k < n; k++)
                        {
                            double aik = a[i, k], ajk = a[j, k];
                            a[i, k] = c * aik - s * ajk;
                            a[j, k] = s * aik + c * ajk;
                        }
                        
                        for (int k = 0; k < n; k++)
                        {
                            double vki = v[k, i], vkj = v[k, j];
                            v[k, i] = c * vki - s * vkj;
                            v[k, j] = s * vki + c * vkj;
                        }
                    }
                }
            }

            var pasangan = new List<(double Nilai, double[] Vektor)>();
            for (int j = 0; j < n; j++)
            {
                var kolom = new double[n];
                for (int i = 0; i < n; i++) kolom[i] = v[i, j];
                pasangan.Add((a[j, j], kolom));
            }

            pasangan.Sort((x, y) => y.Nilai.CompareTo(x.Nilai));

            var nilai = new double[n];
            var vektor = new double[n, n];
            for (int j = 0; j < n; j++)
            {
                var kolom = pasangan[j].Vektor;

                
                int indeks = 0;
                for (int i = 1; i < n; i++)
                    if (Math.Abs(kolom[i]) > Math.Abs(kolom[indeks])) indeks = i;
                if (kolom[indeks] < 0)
                    for (int i = 0; i < n; i++) kolom[i] = -kolom[i];

                nilai[j] = pasangan[j].Nilai;
                for (int i = 0; i < n; i++) vektor[i, j] = kolom[i];
            }

            return new HasilEigen { Nilai = nilai, Vektor = vektor };
        }
    }
}
