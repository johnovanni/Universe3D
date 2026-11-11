using Universe3D.Core.Morphology;

namespace Universe3D.Core.Objects;

public sealed class Galaxy : CosmicObject
{
    public MorphologyInfo Morphology { get; init; } = new(MorphologyClass.Unknown, "", "", false);
    public double? PaDeg { get; init; }
    public double? AxisRatio { get; init; }
    public double? InclinationDeg { get; init; }
    public bool InclinationMeasured { get; init; }
    public double? DiameterKpc { get; init; }

    public override uint TypeCode => (uint)Morphology.Class;
}
