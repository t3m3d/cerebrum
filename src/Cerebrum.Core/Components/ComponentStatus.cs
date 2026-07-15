namespace Cerebrum.Core.Components;

public sealed record ComponentStatus(
    ComponentId Id,
    string DisplayName,
    ComponentState State,
    string Detail,
    DateTimeOffset ChangedAt);
