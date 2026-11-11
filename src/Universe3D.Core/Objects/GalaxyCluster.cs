namespace Universe3D.Core.Objects;

public sealed class GalaxyCluster : CosmicObject
{
    public double? M500e14 { get; init; }
    public double? R500Mpc { get; init; }
    public double? L500e37W { get; init; }
    public int? Richness { get; init; }
    public string? BautzMorgan { get; init; }
    public ClusterCatalogs Catalogs { get; init; }

    public override uint TypeCode => (uint)Catalogs;
}
