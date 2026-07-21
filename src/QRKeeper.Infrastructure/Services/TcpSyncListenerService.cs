using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using QRKeeper.Core.Common;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Infrastructure.Services;

/// <summary>
/// Listens for incoming sync requests over TCP.
/// </summary>
public sealed class TcpSyncListenerService : ISyncListenerService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISyncLocalDeviceProvider _localDeviceProvider;
    private readonly SyncMessageSerializer _serializer;
    private readonly int _configuredPort;
    private CancellationTokenSource? _cts;
    private TcpListener? _listener;
    private Task? _acceptLoopTask;
    private SyncDeviceInfo? _localDevice;

    public TcpSyncListenerService(
        IServiceScopeFactory scopeFactory,
        ISyncLocalDeviceProvider localDeviceProvider,
        SyncMessageSerializer serializer,
        int configuredPort = SyncNetworkDefaults.TransportPort)
    {
        _scopeFactory = scopeFactory;
        _localDeviceProvider = localDeviceProvider;
        _serializer = serializer;
        _configuredPort = configuredPort;
    }

    /// <inheritdoc />
    public int Port
    {
        get
        {
            return _listener?.LocalEndpoint is IPEndPoint endpoint ? endpoint.Port : _configuredPort;
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is not null)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = StartListener();
        _localDevice = _localDeviceProvider.CreateLocalDeviceInfo(Port);
        _acceptLoopTask = AcceptLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateLocalDeviceAsync(SyncDeviceInfo localDevice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(localDevice);
        _localDevice = localDevice;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();
        _listener?.Stop();

        if (_acceptLoopTask is not null)
        {
            try
            {
                await _acceptLoopTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException)
            {
            }
        }

        _acceptLoopTask = null;
        _listener = null;
        _cts?.Dispose();
        _cts = null;
        _localDevice = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            TcpClient? client = null;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientAsync(client, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                client?.Dispose();
                break;
            }
            catch (ObjectDisposedException)
            {
                client?.Dispose();
                break;
            }
            catch (SocketException)
            {
                client?.Dispose();
                if (!cancellationToken.IsCancellationRequested)
                {
                    continue;
                }

                break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            await using NetworkStream stream = client.GetStream();
            byte[] requestPayload = await ReadFrameAsync(stream, cancellationToken);
            SyncRequest request = _serializer.DeserializeRequest(requestPayload);
            SyncDeviceInfo? localDevice = _localDevice;

            using IServiceScope scope = _scopeFactory.CreateScope();
            ISyncIncomingRequestPolicy incomingRequestPolicy =
                scope.ServiceProvider.GetRequiredService<ISyncIncomingRequestPolicy>();
            ISyncRequestHandler requestHandler =
                scope.ServiceProvider.GetRequiredService<ISyncRequestHandler>();

            bool accepted = localDevice is not null &&
                await incomingRequestPolicy.ShouldAcceptAsync(localDevice, request, cancellationToken);

            SyncResponse response = accepted && localDevice is not null
                ? await requestHandler.HandleAsync(localDevice, request, cancellationToken)
                : new SyncResponse
                {
                    Responder = localDevice ?? new SyncDeviceInfo(),
                    Accepted = false,
                    Message = "Incoming sync rejected."
                };

            await WriteFrameAsync(stream, _serializer.SerializeResponse(response), cancellationToken);
        }
    }

    private TcpListener StartListener()
    {
        try
        {
            TcpListener listener = new(IPAddress.Any, _configuredPort);
            listener.Start();
            return listener;
        }
        catch (SocketException) when (_configuredPort > 0)
        {
            TcpListener listener = new(IPAddress.Any, 0);
            listener.Start();
            return listener;
        }
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
            throw new AppException("同步请求无效。");
        }

        byte[] payload = new byte[length];
        if (length > 0)
        {
            await stream.ReadExactlyAsync(payload, cancellationToken);
        }

        return payload;
    }
}
