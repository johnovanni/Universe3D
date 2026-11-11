using Universe3D.Core.Coordinates;

namespace Universe3D.Core.Catalogs;

/// <summary>
/// Positional nearest-neighbour matcher. Points hash into Dec bands (one tolerance tall) and RA cells
/// whose count per band shrinks toward the poles so cells stay at least one tolerance wide on the sky.
/// </summary>
public sealed class SkyCrossMatcher
{
    private readonly double _tolDeg;
    private readonly int _bandCount;
    private readonly int[] _raCells;
    private readonly Dictionary<(int band, int cell), List<(double ra, double dec, int index)>> _grid = new();

    public SkyCrossMatcher(double toleranceArcsec = 30)
    {
        _tolDeg = toleranceArcsec / 3600.0;
        _bandCount = Math.Max(1, (int)Math.Ceiling(180.0 / _tolDeg));
        _raCells = new int[_bandCount];
        for (int b = 0; b < _bandCount; b++)
        {
            double lo = -90 + b * _tolDeg, hi = lo + _tolDeg;
            double maxAbs = Math.Min(90, Math.Max(Math.Abs(lo), Math.Abs(hi)));
            double cosd = Math.Cos(maxAbs * Math.PI / 180.0);
            _raCells[b] = Math.Max(1, (int)Math.Floor(360.0 * cosd / _tolDeg));
        }
    }

    private int Band(double dec) => Math.Clamp((int)Math.Floor((dec + 90) / _tolDeg), 0, _bandCount - 1);

    private int Cell(int band, double ra)
    {
        double r = ra % 360.0;
        if (r < 0) r += 360.0;
        int n = _raCells[band];
        return Math.Min(n - 1, (int)(r / 360.0 * n));
    }

    public void Add(double raDeg, double decDeg, int index)
    {
        int b = Band(decDeg);
        var key = (b, Cell(b, raDeg));
        if (!_grid.TryGetValue(key, out var list)) _grid[key] = list = new();
        list.Add((raDeg, decDeg, index));
    }

    /// <summary>Index of the nearest added point within tolerance, or -1.</summary>
    public int FindNearest(double raDeg, double decDeg)
    {
        int best = -1;
        double bestSep = _tolDeg;
        int b0 = Band(decDeg);
        for (int b = Math.Max(0, b0 - 1); b <= Math.Min(_bandCount - 1, b0 + 1); b++)
        {
            int n = _raCells[b];
            int c0 = Cell(b, raDeg);
            bool wide = n >= 3;
            int from = wide ? -1 : 0, to = wide ? 1 : n - 1;
            for (int d = from; d <= to; d++)
            {
                int c = wide ? ((c0 + d) % n + n) % n : d;
                if (!_grid.TryGetValue((b, c), out var list)) continue;
                foreach (var (ra, dec, index) in list)
                {
                    double sep = SkyMath.AngularSeparationDeg(raDeg, decDeg, ra, dec);
                    if (sep <= bestSep) { bestSep = sep; best = index; }
                }
            }
        }
        return best;
    }
}
