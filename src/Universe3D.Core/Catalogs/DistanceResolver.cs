using Universe3D.Core.Coordinates;
using Universe3D.Core.Cosmology;
using Universe3D.Core.Objects;

namespace Universe3D.Core.Catalogs;

public sealed record ResolvedDistance(
    double DistanceMpc, DistanceSource Source, string Catalog,
    double? VCmb, double? PeculiarKms, double LookbackGyr);

/// <summary>Chooses a distance: a measured one if present, otherwise one derived from redshift.</summary>
public sealed class DistanceResolver(CosmologyModel cosmo)
{
    private const double LowZLimit = 0.03;
    public const string RedshiftCatalog = "Redshift";

    public ResolvedDistance? Resolve(double raDeg, double decDeg, double? measuredMpc, string? measuredCatalog,
        double? czHelio, double? z)
    {
        double? cz = czHelio ?? (z is { } zz && IsFinite(zz) ? zz * RedshiftMath.C : null);
        if (cz is { } c0 && !IsFinite(c0)) cz = null;
        double? zEff = z is { } z1 && IsFinite(z1) ? z1 : cz / RedshiftMath.C;

        var (l, b) = SkyMath.ToSpherical(SkyFrames.EquatorialToGalactic * SkyMath.UnitVector(raDeg, decDeg));
        double? vCmb = cz is { } c ? RedshiftMath.HelioToCmb(c, l, b) : null;

        if (measuredMpc is { } d && IsFinite(d) && d > 0)
        {
            double zDistance = cosmo.RedshiftAtComovingDistance(d);
            // R38: the convention of the catalog that measured the distance.
            double? pec = cz is { } czMeasured ? PeculiarVelocityRule.Compute(measuredCatalog, d, czMeasured, l, b) : null;
            // R8: look-back is the light-travel time implied by the distance, never the observed z.
            return new ResolvedDistance(d, DistanceSource.Measured, measuredCatalog ?? "", vCmb, pec,
                cosmo.LookbackTimeGyr(zDistance));
        }

        if (cz is not { } czVal || zEff is not { } zObs) return null;

        double zDist;
        if (zObs < LowZLimit)
        {
            double v = vCmb!.Value;
            if (v <= 0) v = RedshiftMath.HelioToLocalGroup(czVal, l, b);
            if (v <= 0) return null;
            zDist = v / RedshiftMath.C;
        }
        else
        {
            if (zObs <= 0) return null;
            zDist = zObs;
        }

        double dist = cosmo.ComovingDistanceMpc(zDist);
        if (!IsFinite(dist) || dist <= 0) return null;
        return new ResolvedDistance(dist, DistanceSource.Redshift, RedshiftCatalog, vCmb, null,
            cosmo.LookbackTimeGyr(zDist));
    }

    private static bool IsFinite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
}
