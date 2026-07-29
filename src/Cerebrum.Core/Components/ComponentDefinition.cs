namespace Cerebrum.Core.Components;

public sealed record ComponentDefinition(
    ComponentId Id,
    string DisplayName,
    string Role,
    string ExecutableName,
    string RepositoryDirectory,
    string ProjectDirectory,
    string EnvironmentVariable,
    ComponentActivation Activation,
    bool RestartAfterUnexpectedExit,
    bool IsInternal)
{
    public bool StartsWithSession => Activation == ComponentActivation.Session;
}
