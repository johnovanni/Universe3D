namespace Universe3D.Core.Cosmology;

/// <summary>Flat LCDM cosmology (OmegaLambda = 1 - OmegaM) with a z-to-distance lookup table.</summary>
public sealed class CosmologyModel
{
    private const double TableZMax = 8.0;
    private const int TableSize = 8001;
    private const double Step = TableZMax / (TableSize - 1);
    private const double GyrPerInvH0 = 977.792221; // 1/H0 in Gyr for H0 = 1 km/s/Mpc

    private readonly double _h0;
    private readonly double _omegaM;
    private readonly double _omegaL;
    private readonly double[] _comoving = new double[TableSize];
    private readonly double[] _lookback = new double[TableSize];

    public static CosmologyModel Default { get; } = new();

    public CosmologyModel(double h0 = 70, double omegaM = 0.3)
    {
        _h0 = h0;
        _omegaM = omegaM;
        _omegaL = 1.0 - omegaM;
        BuildTable();
    }

    private double E(double z) => Math.Sqrt(_omegaM * Math.Pow(1 + z, 3) + _omegaL);

    private double InvE(double z) => 1.0 / E(z);

    private double InvLookbackIntegrand(double z) => 1.0 / ((1 + z) * E(z));

    private void BuildTable()
    {
        double dc = 0, lb = 0;
        for (int i = 1; i < TableSize; i++)
        {
            double a = (i - 1) * Step, b = i * Step, m = 0.5 * (a + b);
            dc += Simpson(InvE, a, m, b);
            lb += Simpson(InvLookbackIntegrand, a, m, b);
            _comoving[i] = dc;
            _lookback[i] = lb;
        }
        double cOverH0 = RedshiftMath.C / _h0;
        double tH = GyrPerInvH0 / _h0;
        for (int i = 0; i < TableSize; i++)
        {
            _comoving[i] *= cOverH0;
            _lookback[i] *= tH;
        }
    }

    private static double Simpson(Func<double, double> f, double a, double m, double b)
        => (b - a) / 6.0 * (f(a) + 4 * f(m) + f(b));

    private static double IntegrateSimpson(Func<double, double> f, double a, double b, int intervals)
    {
        double h = (b - a) / intervals, sum = 0;
        for (int i = 0; i < intervals; i++)
        {
            double x0 = a + i * h;
            sum += Simpson(f, x0, x0 + 0.5 * h, x0 + h);
        }
        return sum;
    }

    private static double Interpolate(double[] table, double z)
    {
        double pos = z / Step;
        int i = (int)pos;
        if (i >= TableSize - 1) return table[TableSize - 1];
        double frac = pos - i;
        return table[i] + frac * (table[i + 1] - table[i]);
    }

    /// <summary>Line-of-sight comoving distance in Mpc.</summary>
    public double ComovingDistanceMpc(double z)
    {
        if (z <= 0) return 0;
        if (z <= TableZMax) return Interpolate(_comoving, z);
        int n = Math.Max(64, (int)Math.Ceiling((z - TableZMax) / Step));
        return _comoving[TableSize - 1] + RedshiftMath.C / _h0 * IntegrateSimpson(InvE, TableZMax, z, n);
    }

    /// <summary>Lookback time in Gyr.</summary>
    public double LookbackTimeGyr(double z)
    {
        if (z <= 0) return 0;
        if (z <= TableZMax) return Interpolate(_lookback, z);
        int n = Math.Max(64, (int)Math.Ceiling((z - TableZMax) / Step));
        return _lookback[TableSize - 1] + GyrPerInvH0 / _h0 * IntegrateSimpson(InvLookbackIntegrand, TableZMax, z, n);
    }

    /// <summary>Inverse of <see cref="ComovingDistanceMpc"/>.</summary>
    public double RedshiftAtComovingDistance(double dMpc)
    {
        if (dMpc <= 0) return 0;
        double top = _comoving[TableSize - 1];
        if (dMpc <= top)
        {
            int lo = 0, hi = TableSize - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (_comoving[mid] <= dMpc) lo = mid; else hi = mid;
            }
            double span = _comoving[hi] - _comoving[lo];
            double frac = span > 0 ? (dMpc - _comoving[lo]) / span : 0;
            return (lo + frac) * Step;
        }
        // Beyond the table: Newton iterations on the direct integral (dD/dz = c / (H0 E)).
        double z = TableZMax + (dMpc - top) / (RedshiftMath.C / _h0 * InvE(TableZMax));
        for (int k = 0; k < 50; k++)
        {
            double err = ComovingDistanceMpc(z) - dMpc;
            if (Math.Abs(err) < 1e-9) break;
            z -= err / (RedshiftMath.C / _h0 * InvE(z));
        }
        return z;
    }
}
