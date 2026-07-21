using QRKeeper.Core.Common;
using QRKeeper.Core.Interfaces;
using QRKeeper.Core.Models;

namespace QRKeeper.Infrastructure.Services;

/// <summary>
/// Handles incoming sync requests by importing missing records and returning the local snapshot.
/// </summary>
public sealed class SyncRequestHandler : ISyncRequestHandler
{
    private readonly ISyncMergeService _syncMergeService;

    public SyncRequestHandler(ISyncMergeService syncMergeService)
    {
        _syncMergeService = syncMergeService;
    }

    /// <inheritdoc />
    public async Task<SyncResponse> HandleAsync(
        SyncDeviceInfo responder,
        SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responder);
        ArgumentNullException.ThrowIfNull(request);

        string? validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return new SyncResponse
            {
                Responder = responder,
                Accepted = false,
                Message = validationError
            };
        }

        SyncMergeResult mergeResult = await _syncMergeService.ImportMissingAsync(request.Records, cancellationToken);
        IReadOnlyList<SyncRecordDto> localRecords = await _syncMergeService.GetLocalRecordsAsync(cancellationToken);

        return new SyncResponse
        {
            Responder = responder,
            Accepted = true,
            Records = localRecords.ToList(),
            MergeResult = mergeResult
        };
    }

    /// <summary>
    /// Validates the request before processing it.
    /// </summary>
    private static string? ValidateRequest(SyncRequest request)
    {
        if (request.Sender is null)
        {
            return "同步请求缺少发送方信息。";
        }

        if (!string.Equals(request.Sender.AppName, AppConstants.AppName, StringComparison.Ordinal))
        {
            return "同步请求来自不受支持的应用。";
        }

        if (request.Sender.ProtocolVersion != AppConstants.SyncProtocolVersion)
        {
            return "同步协议版本不兼容。";
        }

        if (string.IsNullOrWhiteSpace(request.Sender.DeviceId))
        {
            return "同步请求缺少设备标识。";
        }

        return null;
    }
}
