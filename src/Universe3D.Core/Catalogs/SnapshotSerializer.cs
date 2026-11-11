using System.IO.Compression;
using System.Text;
using Universe3D.Core.Morphology;
using Universe3D.Core.Objects;

namespace Universe3D.Core.Catalogs;

/// <summary>GZip binary snapshot format: magic "U3D1", version 1, then the data set.</summary>
public static class SnapshotSerializer
{
    private const string Magic = "U3D1";
    private const int Version = 1;
    private const byte TagGalaxy = 0, TagCluster = 1, TagQuasar = 2;

    /// <summary>Sanity limits for lengths read from a file, so a corrupt length is rejected instead of allocated.</summary>
    private const int MaxObjects = 20_000_000, MaxStrings = 10_000;

    public static void Write(Stream stream, DataSet dataSet)
    {
        using var gz = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true);
        using var w = new BinaryWriter(gz, Encoding.UTF8, leaveOpen: true);
        w.Write(Encoding.ASCII.GetBytes(Magic));
        w.Write(Version);
        w.Write((int)dataSet.Kind);
        w.Write(dataSet.ExcludedCount);
        w.Write(dataSet.SkippedRows);
        w.Write(dataSet.BuiltUtc.Ticks);
        w.Write((int)dataSet.BuiltUtc.Kind);
        w.Write(dataSet.SourceSummary);
        w.Write(dataSet.Objects.Count);
        foreach (var o in dataSet.Objects) WriteObject(w, o);
    }

    public static DataSet Read(Stream stream)
    {
        try
        {
            using var gz = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
            using var buffered = new BufferedStream(gz, 1 << 16); // BinaryReader issues many tiny reads
            using var r = new BinaryReader(buffered, Encoding.UTF8, leaveOpen: true);
            var magic = Encoding.ASCII.GetString(r.ReadBytes(Magic.Length));
            if (magic != Magic) throw new InvalidDataException($"Not a Universe3D snapshot (magic '{magic}').");
            int version = r.ReadInt32();
            if (version != Version) throw new InvalidDataException($"Unsupported snapshot version {version}.");
            var kind = ReadEnum<DataSetKind>(r);
            int excluded = r.ReadInt32();
            int skipped = r.ReadInt32();
            long ticks = r.ReadInt64();
            var dtKind = ReadEnum<DateTimeKind>(r);
            string summary = r.ReadString();
            int count = ReadLength(r, MaxObjects, "object count");
            var list = new List<CosmicObject>(Math.Min(count, 1_000_000));
            for (int i = 0; i < count; i++) list.Add(ReadObject(r));
            return new DataSet(kind, list, excluded, skipped, new DateTime(ticks, dtKind), summary);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("Snapshot is truncated.", ex);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            // ArgumentOutOfRangeException (e.g. ticks), a malformed string length, or a bad UTF-8 sequence.
            throw new InvalidDataException("Snapshot contains an invalid value.", ex);
        }
    }

    /// <summary>A length in 0..<paramref name="max"/>; anything else is corrupt data.</summary>
    private static int ReadLength(BinaryReader r, int max, string what)
    {
        int n = r.ReadInt32();
        if (n < 0 || n > max) throw new InvalidDataException($"Invalid {what} length {n}.");
        return n;
    }

    /// <summary>An enum value that the type defines (for flags, a non-empty combination of defined bits).</summary>
    private static T ReadEnum<T>(BinaryReader r) where T : struct, Enum
    {
        int v = r.ReadInt32();
        if (!EnumValues<T>.IsValid(v)) throw new InvalidDataException($"Invalid {typeof(T).Name} value {v}.");
        return (T)Enum.ToObject(typeof(T), v);
    }

    /// <summary>The valid int values of an enum, computed once per type.</summary>
    private static class EnumValues<T> where T : struct, Enum
    {
        private static readonly HashSet<int> Defined = Enum.GetValues<T>().Select(x => Convert.ToInt32(x)).ToHashSet();
        private static readonly bool IsFlags = typeof(T).IsDefined(typeof(FlagsAttribute), false);
        private static readonly int AllBits = Defined.Aggregate(0, (m, x) => m | x);

        public static bool IsValid(int v) => IsFlags ? v != 0 && (v & ~AllBits) == 0 : Defined.Contains(v);
    }

    private static void WriteObject(BinaryWriter w, CosmicObject o)
    {
        w.Write(o switch
        {
            Galaxy => TagGalaxy,
            GalaxyCluster => TagCluster,
            Quasar => TagQuasar,
            _ => throw new NotSupportedException(o.GetType().Name),
        });
        w.Write(o.Id);
        w.Write(o.Name);
        WriteStrings(w, o.AltNames);
        w.Write(o.RaDeg);
        w.Write(o.DecDeg);
        w.Write(o.DistanceMpc);
        w.Write((int)o.DistanceSource);
        w.Write(o.DistanceCatalog);
        WriteD(w, o.Z);
        WriteD(w, o.CzHelio);
        WriteD(w, o.VCmb);
        WriteD(w, o.PeculiarKms);
        w.Write(o.LookbackGyr);
        WriteD(w, o.Magnitude);
        WriteStrings(w, o.Sources);
        switch (o)
        {
            case Galaxy g:
                w.Write((int)g.Morphology.Class);
                w.Write(g.Morphology.Description);
                w.Write(g.Morphology.OriginalCode);
                w.Write(g.Morphology.IsPeculiar);
                WriteD(w, g.PaDeg);
                WriteD(w, g.AxisRatio);
                WriteD(w, g.InclinationDeg);
                w.Write(g.InclinationMeasured);
                WriteD(w, g.DiameterKpc);
                break;
            case GalaxyCluster c:
                WriteD(w, c.M500e14);
                WriteD(w, c.R500Mpc);
                WriteD(w, c.L500e37W);
                w.Write(c.Richness.HasValue);
                w.Write(c.Richness ?? 0);
                w.Write(c.BautzMorgan != null);
                w.Write(c.BautzMorgan ?? "");
                w.Write((int)c.Catalogs);
                break;
            case Quasar q:
                w.Write((int)q.Type);
                WriteD(w, q.RMag);
                WriteD(w, q.BMag);
                break;
        }
    }

    private static CosmicObject ReadObject(BinaryReader r)
    {
        byte tag = r.ReadByte();
        string id = r.ReadString(), name = r.ReadString();
        var alt = ReadStrings(r);
        double ra = r.ReadDouble(), dec = r.ReadDouble(), dist = r.ReadDouble();
        var src = ReadEnum<DistanceSource>(r);
        string distCat = r.ReadString();
        var z = ReadD(r);
        var cz = ReadD(r);
        var vcmb = ReadD(r);
        var pec = ReadD(r);
        double lookback = r.ReadDouble();
        var mag = ReadD(r);
        var sources = ReadStrings(r);

        switch (tag)
        {
            case TagGalaxy:
            {
                var morph = new MorphologyInfo(ReadEnum<MorphologyClass>(r), r.ReadString(), r.ReadString(), r.ReadBoolean());
                return new Galaxy
                {
                    Id = id, Name = name, AltNames = alt, RaDeg = ra, DecDeg = dec, DistanceMpc = dist,
                    DistanceSource = src, DistanceCatalog = distCat, Z = z, CzHelio = cz, VCmb = vcmb,
                    PeculiarKms = pec, LookbackGyr = lookback, Magnitude = mag, Sources = sources,
                    Morphology = morph, PaDeg = ReadD(r), AxisRatio = ReadD(r), InclinationDeg = ReadD(r),
                    InclinationMeasured = r.ReadBoolean(), DiameterKpc = ReadD(r),
                };
            }
            case TagCluster:
            {
                double? m500 = ReadD(r), r500 = ReadD(r), l500 = ReadD(r);
                bool hasRich = r.ReadBoolean();
                int rich = r.ReadInt32();
                bool hasBm = r.ReadBoolean();
                string bm = r.ReadString();
                return new GalaxyCluster
                {
                    Id = id, Name = name, AltNames = alt, RaDeg = ra, DecDeg = dec, DistanceMpc = dist,
                    DistanceSource = src, DistanceCatalog = distCat, Z = z, CzHelio = cz, VCmb = vcmb,
                    PeculiarKms = pec, LookbackGyr = lookback, Magnitude = mag, Sources = sources,
                    M500e14 = m500, R500Mpc = r500, L500e37W = l500,
                    Richness = hasRich ? rich : null, BautzMorgan = hasBm ? bm : null,
                    Catalogs = ReadEnum<ClusterCatalogs>(r),
                };
            }
            case TagQuasar:
                return new Quasar
                {
                    Id = id, Name = name, AltNames = alt, RaDeg = ra, DecDeg = dec, DistanceMpc = dist,
                    DistanceSource = src, DistanceCatalog = distCat, Z = z, CzHelio = cz, VCmb = vcmb,
                    PeculiarKms = pec, LookbackGyr = lookback, Magnitude = mag, Sources = sources,
                    Type = ReadEnum<QuasarType>(r), RMag = ReadD(r), BMag = ReadD(r),
                };
            default:
                throw new InvalidDataException($"Unknown object tag {tag}.");
        }
    }

    private static void WriteD(BinaryWriter w, double? v)
    {
        w.Write(v.HasValue);
        w.Write(v ?? 0.0);
    }

    private static double? ReadD(BinaryReader r)
    {
        bool has = r.ReadBoolean();
        double v = r.ReadDouble();
        return has ? v : null;
    }

    private static void WriteStrings(BinaryWriter w, string[] a)
    {
        w.Write(a.Length);
        foreach (var s in a) w.Write(s);
    }

    private static string[] ReadStrings(BinaryReader r)
    {
        int n = ReadLength(r, MaxStrings, "name list");
        var a = new string[n];
        for (int i = 0; i < n; i++) a[i] = r.ReadString();
        return a;
    }
}
