namespace PrintAI.Planning;

public static class AntigravityLocator
{
    public static string? Resolve(string? configuredPath = null)
    {
        var requested = string.IsNullOrWhiteSpace(configuredPath)
            ? Environment.GetEnvironmentVariable("PRINTAI_AGY_PATH")
            : configuredPath;

        if (!string.IsNullOrWhiteSpace(requested))
        {
            var explicitPath = ResolveCandidate(requested.Trim());
            if (explicitPath is not null)
                return explicitPath;
        }

        var userLocalCli = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "bin",
            "agy");

        return ResolveCandidate("agy.exe") ??
               ResolveCandidate("agy") ??
               ResolveCandidate(userLocalCli);
    }

    private static string? ResolveCandidate(string candidate)
    {
        if (Path.IsPathRooted(candidate) ||
            candidate.Contains(Path.DirectorySeparatorChar) ||
            candidate.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(candidate)
                ? Path.GetFullPath(candidate)
                : null;
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var directory in path.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            var fullPath = Path.Combine(directory, candidate);
            if (File.Exists(fullPath))
                return Path.GetFullPath(fullPath);
        }

        return null;
    }
}
