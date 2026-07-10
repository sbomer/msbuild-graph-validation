internal enum IdentitySource
{
    None,
    Global
}

internal sealed record IdentityDimension(string Value, IdentitySource Source);

internal sealed record CanonicalNode(
    string ProjectPath,
    string Role,
    IReadOnlyList<string> RequestedTargets,
    IdentityDimension Configuration,
    IdentityDimension TargetFramework,
    IdentityDimension RuntimeIdentifier,
    IdentityDimension TargetOS,
    IdentityDimension TargetArchitecture,
    IdentityDimension TargetRid);

internal sealed record CanonicalEdge(CanonicalNode From, CanonicalNode To, string Role);
