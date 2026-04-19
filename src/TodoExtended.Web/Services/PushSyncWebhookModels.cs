using System.Text.Json.Serialization;

namespace TodoExtended.Web.Services;

public sealed record PushSyncNotificationEnvelope(
    [property: JsonPropertyName("value")] List<PushSyncNotification>? Value);

public sealed record PushSyncNotification(
    [property: JsonPropertyName("subscriptionId")] string? SubscriptionId,
    [property: JsonPropertyName("clientState")] string? ClientState,
    [property: JsonPropertyName("resource")] string? Resource,
    [property: JsonPropertyName("changeType")] string? ChangeType);
