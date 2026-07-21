using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Infrastructure.Services;

/// <summary>
/// Local loopback transport used to validate sync request and response flow.
/// </summary>
public sealed class LoopbackSyncTransportService : ISyncTransportService
{
    private readonly ISyncRequestHandler _requestHandler;
    private readonly SyncMessageSerializer _serializer;

    public LoopbackSyncTransportService(
        ISyncRequestHandler requestHandler,
        SyncMessageSerializer serializer)
    {
        _requestHandler = requestHandler;
        _serializer = serializer;
    }

    /// <inheritdoc />
    public async Task<SyncResponse> SendAsync(
        SyncDeviceInfo peer,
        SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        byte[] requestPayload = _serializer.SerializeRequest(request);
        SyncRequest transmittedRequest = _serializer.DeserializeRequest(requestPayload);

        SyncResponse response = await _requestHandler.HandleAsync(peer, transmittedRequest, cancellationToken);
        byte[] responsePayload = _serializer.SerializeResponse(response);
        return _serializer.DeserializeResponse(responsePayload);
    }
}
