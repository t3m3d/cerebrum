using System.Text.RegularExpressions;

namespace Cerebrum.Core.Configuration;

public static partial class CerebrumSettingsPolicy
{
    private static readonly HashSet<string> Themes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cerebrum",
        "Krypton",
        "Graphite",
        "Frost"
    };

    public static bool IsValid(CerebrumSettings? settings)
    {
        if (settings is null || settings.Version != CerebrumSettings.CurrentVersion)
        {
            return false;
        }

        if (!Themes.Contains(settings.ThemePreset) || !ColorPattern().IsMatch(settings.AccentColor))
        {
            return false;
        }

        if (settings.RestartLimit is < 0 or > 10)
        {
            return false;
        }

        return IsOptionalAbsolutePath(settings.WallpaperPath)
            && IsOptionalAbsolutePath(settings.Components.Broker)
            && IsOptionalAbsolutePath(settings.Components.Medulla)
            && IsOptionalAbsolutePath(settings.Components.Thalamus)
            && IsOptionalAbsolutePath(settings.Components.Cortex);
    }

    private static bool IsOptionalAbsolutePath(string? path) =>
        string.IsNullOrWhiteSpace(path) || Path.IsPathFullyQualified(path);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$")]
    private static partial Regex ColorPattern();
}
