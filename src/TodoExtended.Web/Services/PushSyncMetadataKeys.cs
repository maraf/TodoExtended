namespace TodoExtended.Web.Services;

public static class PushSyncMetadataKeys
{
    public static string State(string userId) => $"PushSync:State:{userId}";

    public static string PreferredUsername(string userId) => $"PushSync:PreferredUsername:{userId}";

    public static string LastSuccess(string userId) => $"PushSync:LastSuccess:{userId}";

    public static string LastFailure(string userId) => $"PushSync:LastFailure:{userId}";
}
