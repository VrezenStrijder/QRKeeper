using System.Net.Sockets;
using QRKeeper.Core.Common;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Infrastructure.Services;

/// <summary>
/// Sends sync requests over TCP to a selected LAN peer.
/// </summary>
public sealed class TcpSyncTransportService : ISyncTransportService
{
    private readonly SyncMessageSerializer _serializer;

    public TcpSyncTransportService(SyncMessageSerializer serializer)
    {
        _serializer = serializer;
    }

    /// <inheritdoc />
    public async Task<SyncResponse> SendAsync(
        SyncDeviceInfo peer,
        SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(peer.Endpoint))
        {
            throw new AppException("同步设备缺少地址信息。");
        }

        if (peer.TransportPort <= 0)
        {
            throw new AppException("同步设备缺少端口信息。");
        }

        using TcpClient client = new();
        await client.ConnectAsync(peer.Endpoint, peer.TransportPort, cancellationToken);
        client.NoDelay = true;

        await using NetworkStream stream = client.GetStream();
        await WriteFrameAsync(stream, _serializer.SerializeRequest(request), cancellationToken);
        byte[] responsePayload = await ReadFrameAsync(stream, cancellationToken);
        return _serializer.DeserializeResponse(responsePayload);
    }

    private static async Task WriteFrameAsync(
        Stream stream,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        byte[] lengthBuffer = BitConverter.GetBytes(payload.Length);
        await stream.WriteAsync(lengthBuffer, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<byte[]> ReadFrameAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        byte[] lengthBuffer = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(lengthBuffer, cancellationToken);
        int length = BitConverter.ToInt32(lengthBuffer, 0);
        if (length < 0)
        {
            throw new AppException("同步响应无效。");
        }

        byte[] payload = new byte[length];
        if (length > 0)
        {
            await stream.ReadExactlyAsync(payload, cancellationToken);
        }

        return payload;
    }
}
