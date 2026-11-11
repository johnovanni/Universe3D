namespace Universe3D.Core.Morphology;

/// <summary>A parsed morphology: shared class, readable description, the trimmed source code and the peculiar flag.</summary>
public sealed record MorphologyInfo(MorphologyClass Class, string Description, string OriginalCode, bool IsPeculiar);
