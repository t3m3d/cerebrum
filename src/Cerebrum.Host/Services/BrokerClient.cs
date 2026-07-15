using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Cerebrum.Core.Protocol;

namespace Cerebrum.Host.Services;

internal sealed class BrokerClient(string pipeName)
{
    public async Task<BrokerResponse?> SendAsync(
        string command,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!BrokerProtocol.IsSupportedCommand(command))
        {
            throw new ArgumentOutOfRangeException(nameof(command));
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var requestId = Guid.NewGuid().ToString("N");

        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(deadline.Token).ConfigureAwait(false);

            using var writer = new StreamWriter(
                pipe,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024,
                leaveOpen: true)
            {
                AutoFlush = true
            };
            var request = new BrokerRequest(BrokerProtocol.Version, requestId, command);
            var requestJson = JsonSerializer.Serialize(request, BrokerProtocol.JsonOptions);
            await writer.WriteLineAsync(requestJson.AsMemory(), deadline.Token).ConfigureAwait(false);

            using var reader = new StreamReader(
                pipe,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);
            var responseJson = await reader.ReadLineAsync(deadline.Token).ConfigureAwait(false);
            if (responseJson is null || responseJson.Length > BrokerProtocol.MaximumMessageCharacters)
            {
                return null;
            }

            var response = JsonSerializer.Deserialize<BrokerResponse>(responseJson, BrokerProtocol.JsonOptions);
            return response is not null
                && response.Version == BrokerProtocol.Version
                && response.RequestId == requestId
                ? response
                : null;
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or OperationCanceledException)
        {
            return null;
        }
    }
}
