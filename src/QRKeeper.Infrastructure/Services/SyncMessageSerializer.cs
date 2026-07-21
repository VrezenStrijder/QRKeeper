using System.Text.Json;
using System.Text.Json.Serialization;
using QRKeeper.Core.Models;

namespace QRKeeper.Infrastructure.Services;

/// <summary>
/// Serializes sync protocol messages for transport tests and loopback transport.
/// </summary>
public sealed class SyncMessageSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Serializes a sync request to UTF-8 JSON.
    /// </summary>
    public byte[] SerializeRequest(SyncRequest request)
    {
        return JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);
    }

    /// <summary>
    /// Deserializes a sync request from UTF-8 JSON.
    /// </summary>
    public SyncRequest DeserializeRequest(byte[] payload)
    {
        return JsonSerializer.Deserialize<SyncRequest>(payload, JsonOptions)
            ?? new SyncRequest();
    }

    /// <summary>
    /// Serializes a sync response to UTF-8 JSON.
    /// </summary>
    public byte[] SerializeResponse(SyncResponse response)
    {
        return JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);
    }

    /// <summary>
    /// Deserializes a sync response from UTF-8 JSON.
    /// </summary>
    public SyncResponse DeserializeResponse(byte[] payload)
    {
        return JsonSerializer.Deserialize<SyncResponse>(payload, JsonOptions)
            ?? new SyncResponse();
    }

    /// <summary>
    /// Serializes device metadata to UTF-8 JSON.
    /// </summary>
    public byte[] SerializeDeviceInfo(SyncDeviceInfo deviceInfo)
    {
        return JsonSerializer.SerializeToUtf8Bytes(deviceInfo, JsonOptions);
    }

    /// <summary>
    /// Deserializes device metadata from UTF-8 JSON.
    /// </summary>
    public SyncDeviceInfo DeserializeDeviceInfo(byte[] payload)
    {
        return JsonSerializer.Deserialize<SyncDeviceInfo>(payload, JsonOptions)
            ?? new SyncDeviceInfo();
    }
}
