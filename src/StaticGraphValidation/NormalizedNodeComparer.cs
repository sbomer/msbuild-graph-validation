internal sealed class NormalizedNodeComparer : IEqualityComparer<NormalizedNode>
{
    public static NormalizedNodeComparer Instance { get; } = new();

    public bool Equals(NormalizedNode? x, NormalizedNode? y) =>
        x is not null &&
        y is not null &&
        StringComparer.OrdinalIgnoreCase.Equals(x.ProjectPath, y.ProjectPath) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.Configuration, y.Configuration) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.TargetFramework, y.TargetFramework) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.RuntimeIdentifier, y.RuntimeIdentifier) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.TargetOS, y.TargetOS) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.TargetArchitecture, y.TargetArchitecture) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.TargetRid, y.TargetRid) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.Role, y.Role);

    public int GetHashCode(NormalizedNode obj) =>
        HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ProjectPath),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Configuration),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TargetFramework),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.RuntimeIdentifier),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TargetOS),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TargetArchitecture),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TargetRid),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Role));
}

