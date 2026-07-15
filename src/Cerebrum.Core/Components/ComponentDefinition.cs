namespace Cerebrum.Core.Components;

public sealed record ComponentDefinition(
    ComponentId Id,
    string DisplayName,
    string Role,
    string ExecutableName,
    string RepositoryDirectory,
    string ProjectDirectory,
    string EnvironmentVariable,
    bool StartsWithSession,
    bool RestartAfterUnexpectedExit,
    bool IsInternal);
