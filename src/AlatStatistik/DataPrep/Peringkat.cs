using System;
using System.Collections.Generic;
using System.Linq;
using AlatStatistik.Models;
using AlatStatistik.Statistics;
using Ukur = AlatStatistik.Models.Measure;

namespace AlatStatistik.DataPrep
{

    public enum CaraIkatan
    {

        Rerata,

        Terkecil,

        Terbesar,

        UrutUnik
    }

    public enum JenisPeringkat
    {
        Peringkat,
        PeringkatFraksional,
        PeringkatPersen,
        SkorNormalBlom,
        Ntiles
    }

    public static class Peringkat
    {

        public static List<double?> Beri(List<double?> nilai, CaraIkatan ikatan = CaraIkatan.Rerata,
                                        bool menurun = false, JenisPeringkat jenis = JenisPeringkat.Peringkat,
                                        int banyakNtile = 4)
        {
            var hasil = new List<double?>(new double?[nilai.Count]);

            
            
            var isi = Enumerable.Range(0, nilai.Count).Where(i => nilai[i].HasValue).ToList();
            if (isi.Count == 0) return hasil;

            int n = isi.Count;

            
            
            var urut = isi.OrderBy(i => nilai[i]!.Value).ToList();
            if (menurun) urut.Reverse();

            var peringkat = new double[n];

            int a = 0;
            while (a < n)
            {
                int b = a;
                double v = nilai[urut[a]]!.Value;
                while (b + 1 < n && Math.Abs(nilai[urut[b + 1]]!.Value - v) < 1e-12) b++;

                int banyak = b - a + 1;
                switch (ikatan)
                {
                    case CaraIkatan.Rerata:
                        double rata = (a + 1 + b + 1) / 2.0;
                        for (int k = a; k <= b; k++) peringkat[k] = rata;
                        break;
                    case CaraIkatan.Terkecil:
                        for (int k = a; k <= b; k++) peringkat[k] = a + 1;
                        break;
                    case CaraIkatan.Terbesar:
                        for (int k = a; k <= b; k++) peringkat[k] = b + 1;
                        break;
                    default: 
                        for (int k = a; k <= b; k++) peringkat[k] = k + 1;
                        break;
                }
                a = b + 1;
            }

            for (int k = 0; k < n; k++)
            {
                double r = peringkat[k];
                double nilaiAkhir = jenis switch
                {
                    JenisPeringkat.PeringkatFraksional => r / n,
                    JenisPeringkat.PeringkatPersen    => 100.0 * r / n,
                    
                    
                    
                    
                    JenisPeringkat.SkorNormalBlom     => Distributions.NormalInv((r - 0.375) / (n + 0.25)),
                    JenisPeringkat.Ntiles             => Math.Min(banyakNtile,
                                                             (int)Math.Ceiling(banyakNtile * r / n)),
                    _                                 => r
                };
                hasil[urut[k]] = nilaiAkhir;
            }

            return hasil;
        }
    }
}
