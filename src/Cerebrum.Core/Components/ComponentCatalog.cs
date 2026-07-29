namespace Cerebrum.Core.Components;

public static class ComponentCatalog
{
    public static IReadOnlyList<ComponentDefinition> All { get; } =
    [
        new(
            ComponentId.Found,
            "Found",
            "Outer session supervisor and Explorer recovery surface",
            "found.exe",
            "found",
            ".",
            "CEREBRUM_FOUND_PATH",
            Activation: ComponentActivation.ExternalSupervisor,
            RestartAfterUnexpectedExit: false,
            IsInternal: false),
        new(
            ComponentId.Broker,
            "System broker",
            "Isolates Windows integration from the desktop process",
            "Cerebrum.Broker.exe",
            "cerebrum",
            "Cerebrum.Broker",
            "CEREBRUM_BROKER_PATH",
            Activation: ComponentActivation.OnDemand,
            RestartAfterUnexpectedExit: true,
            IsInternal: true),
        new(
            ComponentId.Wallpaper,
            "Krypton wallpaper",
            "Desktop artwork and ambient interaction layer",
            "krypton-wallpaper.exe",
            "wallpaperbank",
            "krypton-lang",
            "CEREBRUM_WALLPAPER_PATH",
            Activation: ComponentActivation.Session,
            RestartAfterUnexpectedExit: true,
            IsInternal: false),
        new(
            ComponentId.Parietal,
            "Parietal",
            "Global application menu, system tray, clock, and status bar",
            "Parietal.exe",
            "parietal",
            "Parietal",
            "CEREBRUM_PARIETAL_PATH",
            Activation: ComponentActivation.Session,
            RestartAfterUnexpectedExit: true,
            IsInternal: false),
        new(
            ComponentId.Medulla,
            "Medulla",
            "Dock, running applications, pins, and launcher",
            "Medulla.exe",
            "medulla",
            "Medulla",
            "CEREBRUM_MEDULLA_PATH",
            Activation: ComponentActivation.Session,
            RestartAfterUnexpectedExit: true,
            IsInternal: false),
        new(
            ComponentId.Thalamus,
            "Thalamus",
            "Window overview, tiling, monitor placement, and layouts",
            "Thalamus.exe",
            "thalamus",
            "Thalamus",
            "CEREBRUM_THALAMUS_PATH",
            Activation: ComponentActivation.Session,
            RestartAfterUnexpectedExit: true,
            IsInternal: false),
        new(
            ComponentId.Cortex,
            "Cortex",
            "File management, archives, search, and file operations",
            "Cortex.exe",
            "cortex-win",
            "Cortex",
            "CEREBRUM_CORTEX_PATH",
            Activation: ComponentActivation.OnDemand,
            RestartAfterUnexpectedExit: false,
            IsInternal: false),
        new(
            ComponentId.Snip,
            "Snip",
            "Region, window, and full-desktop capture and annotation",
            "Snip.exe",
            "snip",
            "Snip",
            "CEREBRUM_SNIP_PATH",
            Activation: ComponentActivation.OnDemand,
            RestartAfterUnexpectedExit: false,
            IsInternal: false)
    ];

    public static ComponentDefinition Get(ComponentId id) =>
        All.First(component => component.Id == id);
}
