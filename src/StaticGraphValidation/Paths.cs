internal static class Paths
{
    public static string? FindRepoRoot(string path)
    {
        DirectoryInfo? directory = File.Exists(path) ? new FileInfo(path).Directory : new DirectoryInfo(path);
        for (; directory is not null; directory = directory.Parent)
        {
            string gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    public static string Relativize(string path, string? repoRoot)
    {
        string fullPath = Path.GetFullPath(path);
        if (!string.IsNullOrWhiteSpace(repoRoot))
        {
            string root = Path.GetFullPath(repoRoot);
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(root, fullPath).Replace('\\', '/');
            }
        }

        return fullPath.Replace('\\', '/');
    }
}

