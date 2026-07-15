using System.Text.Json;

namespace Cerebrum.Core.Protocol;

public sealed record BrokerRequest(int Version, string RequestId, string Command);

public sealed record BrokerResponse(
    int Version,
    string RequestId,
    bool Success,
    string Status,
    IReadOnlyList<string>? Capabilities = null);

public static class BrokerProtocol
{
    public const int Version = 1;
    public const int MaximumMessageCharacters = 4096;
    public const string HealthCommand = "health";
    public const string CapabilitiesCommand = "capabilities";
    public const string ShutdownCommand = "shutdown";

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public static bool IsSupportedCommand(string? command) => command is
        HealthCommand or CapabilitiesCommand or ShutdownCommand;
}
