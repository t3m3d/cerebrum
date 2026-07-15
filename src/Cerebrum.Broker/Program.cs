using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Cerebrum.Core.Protocol;

namespace Cerebrum.Broker;

internal static class Program
{
    private static readonly string[] Capabilities =
    [
        "health",
        "capabilities",
        "shutdown"
    ];

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && string.Equals(args[0], "--health", StringComparison.OrdinalIgnoreCase))
        {
            var response = CreateResponse("standalone", success: true, "healthy", Capabilities);
            Console.WriteLine(JsonSerializer.Serialize(response, BrokerProtocol.JsonOptions));
            return 0;
        }

        var pipeName = ReadOption(args, "--pipe");
        if (!args.Contains("--serve", StringComparer.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(pipeName)
            || pipeName.Length > 180)
        {
            return 2;
        }

        using var shutdown = new CancellationTokenSource();
        try
        {
            await ServeAsync(pipeName, shutdown).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 3;
        }
    }

    private static async Task ServeAsync(string pipeName, CancellationTokenSource shutdown)
    {
        while (!shutdown.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
                inBufferSize: 8 * 1024,
                outBufferSize: 8 * 1024);

            await pipe.WaitForConnectionAsync(shutdown.Token).ConfigureAwait(false);
            using var requestDeadline = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
            requestDeadline.CancelAfter(TimeSpan.FromSeconds(3));

            BrokerResponse response;
            try
            {
                response = await HandleRequestAsync(pipe, requestDeadline.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
            {
                response = CreateResponse("invalid", success: false, "invalid-request");
            }

            await WriteResponseAsync(pipe, response, requestDeadline.Token).ConfigureAwait(false);
            if (response.Success && response.Status == "shutting-down")
            {
                shutdown.Cancel();
            }
        }
    }

    private static async Task<BrokerResponse> HandleRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);
        var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (line is null || line.Length > BrokerProtocol.MaximumMessageCharacters)
        {
            throw new InvalidDataException("The broker request was empty or oversized.");
        }

        var request = JsonSerializer.Deserialize<BrokerRequest>(line, BrokerProtocol.JsonOptions)
            ?? throw new InvalidDataException("The broker request was empty.");
        if (request.Version != BrokerProtocol.Version
            || string.IsNullOrWhiteSpace(request.RequestId)
            || request.RequestId.Length > 80
            || !BrokerProtocol.IsSupportedCommand(request.Command))
        {
            return CreateResponse(request.RequestId, success: false, "unsupported-request");
        }

        return request.Command switch
        {
            BrokerProtocol.HealthCommand => CreateResponse(request.RequestId, true, "healthy"),
            BrokerProtocol.CapabilitiesCommand => CreateResponse(request.RequestId, true, "capabilities", Capabilities),
            BrokerProtocol.ShutdownCommand => CreateResponse(request.RequestId, true, "shutting-down"),
            _ => CreateResponse(request.RequestId, false, "unsupported-request")
        };
    }

    private static async Task WriteResponseAsync(
        Stream stream,
        BrokerResponse response,
        CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024,
            leaveOpen: true)
        {
            AutoFlush = true
        };
        var json = JsonSerializer.Serialize(response, BrokerProtocol.JsonOptions);
        await writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private static BrokerResponse CreateResponse(
        string requestId,
        bool success,
        string status,
        IReadOnlyList<string>? capabilities = null) =>
        new(BrokerProtocol.Version, requestId, success, status, capabilities);

    private static string? ReadOption(string[] args, string option)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], option, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}
