using Cerebrum.Core.Components;

namespace Cerebrum.Core.Performance;

public enum DesktopProcessGroup
{
    WindowsShell,
    CerebrumSession,
    OnDemand,
    Compositor
}

public static class DesktopProcessCatalog
{
    private static readonly HashSet<string> WindowsShellNames = new(
        [
            "explorer",
            "SearchApp",
            "SearchHost",
            "ShellExperienceHost",
            "ShellHost",
            "sihost",
            "StartMenuExperienceHost",
            "TextInputHost",
            "WidgetService",
            "Widgets"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> CerebrumSessionNames = new(
        ComponentCatalog.All
            .Where(component => component.Activation != ComponentActivation.OnDemand)
            .Select(component => Path.GetFileNameWithoutExtension(component.ExecutableName))
            .Append("Cerebrum.Host"),
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> OnDemandNames = new(
        ComponentCatalog.All
            .Where(component => component.Activation == ComponentActivation.OnDemand)
            .Select(component => Path.GetFileNameWithoutExtension(component.ExecutableName)),
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> CompositorNames = new(
        ["dwm"],
        StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> TrackedProcessNames { get; } =
        WindowsShellNames
            .Concat(CerebrumSessionNames)
            .Concat(OnDemandNames)
            .Concat(CompositorNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static bool TryGetGroup(string processName, out DesktopProcessGroup group)
    {
        if (WindowsShellNames.Contains(processName))
        {
            group = DesktopProcessGroup.WindowsShell;
            return true;
        }

        if (CerebrumSessionNames.Contains(processName))
        {
            group = DesktopProcessGroup.CerebrumSession;
            return true;
        }

        if (OnDemandNames.Contains(processName))
        {
            group = DesktopProcessGroup.OnDemand;
            return true;
        }

        group = DesktopProcessGroup.Compositor;
        return CompositorNames.Contains(processName);
    }
}
