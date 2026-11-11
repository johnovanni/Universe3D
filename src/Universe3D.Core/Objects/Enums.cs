namespace Universe3D.Core.Objects;

public enum DataSetKind { LocalVolume, NearbyUniverse, Clusters, Quasars }

public enum DistanceSource { Measured, Redshift }

public enum QuasarType { Quasar, Agn, BlLac, Type2 }

[Flags]
public enum ClusterCatalogs { Mcxc = 1, Abell = 2 }
