namespace Cerebrum.Core.Components;

public static class ComponentCommands
{
    public static IReadOnlyList<string> ThalamusOverview() => ["--overview"];

    public static IReadOnlyList<string> SnipCapture(SnipCaptureMode mode) =>
        mode switch
        {
            SnipCaptureMode.Region => ["--capture=region"],
            SnipCaptureMode.Window => ["--capture=window"],
            SnipCaptureMode.Fullscreen => ["--capture=fullscreen"],
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

    public static IReadOnlyList<string> CortexOpen(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Cortex paths must be fully qualified.", nameof(path));
        }

        return ["--open", Path.GetFullPath(path)];
    }
}
