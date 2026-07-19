namespace Cerebrum.Core.Components;

public static class ComponentCatalog
{
    public static IReadOnlyList<ComponentDefinition> All { get; } =
    [
        new(
            ComponentId.Broker,
            "System broker",
            "Isolates Windows integration from the desktop process",
            "Cerebrum.Broker.exe",
            "cerebrum",
            "Cerebrum.Broker",
            "CEREBRUM_BROKER_PATH",
            StartsWithSession: false,
            RestartAfterUnexpectedExit: true,
            IsInternal: true),
        new(
            ComponentId.Medulla,
            "Medulla",
            "Taskbar, dock, launcher, clock, and status surfaces",
            "Medulla.exe",
            "medulla",
            "Medulla",
            "CEREBRUM_MEDULLA_PATH",
            StartsWithSession: true,
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
            StartsWithSession: true,
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
            StartsWithSession: false,
            RestartAfterUnexpectedExit: false,
            IsInternal: false)
    ];

    public static ComponentDefinition Get(ComponentId id) =>
        All.First(component => component.Id == id);
}
