using Universe3D.Core.Coordinates;

namespace Universe3D.Core.Objects;

/// <summary>Anything plotted in the universe: shared identity, position, velocity and source fields.</summary>
public abstract class CosmicObject
{
    /// <summary>Format <c>&lt;catalog&gt;:&lt;catalog identifier&gt;</c>, unique within a data set.</summary>
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string[] AltNames { get; init; } = Array.Empty<string>();
    public double RaDeg { get; init; }
    public double DecDeg { get; init; }
    public double DistanceMpc { get; init; }
    public DistanceSource DistanceSource { get; init; }
    public string DistanceCatalog { get; init; } = "";

    public double? Z { get; init; }
    public double? CzHelio { get; init; }
    public double? VCmb { get; init; }
    public double? PeculiarKms { get; init; }
    public double LookbackGyr { get; init; }

    public double? Magnitude { get; init; }
    public string[] Sources { get; init; } = Array.Empty<string>();

    /// <summary>Equatorial position in Mpc, Sun at the origin.</summary>
    public Vector3d PositionEq => SkyMath.UnitVector(RaDeg, DecDeg) * DistanceMpc;

    /// <summary>The shader filter bit index for this object's type.</summary>
    public abstract uint TypeCode { get; }
}
