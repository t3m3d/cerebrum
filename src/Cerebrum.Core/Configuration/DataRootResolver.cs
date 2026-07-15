namespace Cerebrum.Core.Configuration;

public sealed record DataRootResult(string Path, bool UsedOverride, bool RejectedOverride);

public static class DataRootResolver
{
    public static DataRootResult Resolve(string? overridePath = null)
    {
        overridePath ??= Environment.GetEnvironmentVariable("CEREBRUM_DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (Path.IsPathFullyQualified(overridePath))
            {
                return new(Path.GetFullPath(overridePath), UsedOverride: true, RejectedOverride: false);
            }

            return new(DefaultPath(), UsedOverride: false, RejectedOverride: true);
        }

        return new(DefaultPath(), UsedOverride: false, RejectedOverride: false);
    }

    private static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cerebrum",
        "Desktop");
}
