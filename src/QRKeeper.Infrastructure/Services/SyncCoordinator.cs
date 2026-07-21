using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Infrastructure.Services;

/// <summary>
/// Coordinates the client side of a LAN sync exchange.
/// </summary>
public sealed class SyncCoordinator : ISyncCoordinator
{
    private readonly ISyncMergeService _syncMergeService;
    private readonly ISyncTransportService _syncTransportService;

    public SyncCoordinator(
        ISyncMergeService syncMergeService,
        ISyncTransportService syncTransportService)
    {
        _syncMergeService = syncMergeService;
        _syncTransportService = syncTransportService;
    }

    /// <inheritdoc />
    public async Task<SyncSessionResult> SyncAsync(
        SyncDeviceInfo localDevice,
        SyncDeviceInfo peer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(localDevice);
        ArgumentNullException.ThrowIfNull(peer);

        IReadOnlyList<SyncRecordDto> localRecords = await _syncMergeService.GetLocalRecordsAsync(cancellationToken);
        SyncRequest request = new()
        {
            Sender = localDevice,
            Records = localRecords.ToList()
        };

        SyncResponse response = await _syncTransportService.SendAsync(peer, request, cancellationToken);
        SyncMergeResult localMergeResult = new();
        if (response.Accepted)
        {
            localMergeResult = await _syncMergeService.ImportMissingAsync(response.Records, cancellationToken);
        }

        return new SyncSessionResult
        {
            PeerResponse = response,
            LocalMergeResult = localMergeResult
        };
    }
}
