namespace Universe3D.Core.Objects;

public sealed class Quasar : CosmicObject
{
    public QuasarType Type { get; init; }
    public double? RMag { get; init; }
    public double? BMag { get; init; }

    public override uint TypeCode => (uint)Type;
}
