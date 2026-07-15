namespace Cerebrum.Core.Configuration;

public sealed record CerebrumSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public string ThemePreset { get; init; } = "Cerebrum";

    public string AccentColor { get; init; } = "#7C8CFF";

    public string? WallpaperPath { get; init; }

    public bool StartMedulla { get; init; } = true;

    public bool StartThalamus { get; init; } = true;

    public bool RestartSessionComponents { get; init; } = true;

    public int RestartLimit { get; init; } = 3;

    public ComponentPaths Components { get; init; } = new();
}

public sealed record ComponentPaths
{
    public string? Broker { get; init; }

    public string? Medulla { get; init; }

    public string? Thalamus { get; init; }

    public string? Cortex { get; init; }
}
