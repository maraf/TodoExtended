namespace TodoExtended.Web.Data;

public class User
{
    public required string Id { get; set; }      // Entra ID OID
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public string? HomeAccountId { get; set; }   // MSAL home account ID (oid.tid) for token cache lookup
    public bool IsDarkMode { get; set; }
    public string? TimeZone { get; set; }        // IANA timezone ID from Graph mailboxSettings
    public string? PinnedTags { get; set; }      // JSON-encoded list of pinned tag names
    
    public ICollection<ApiKey> ApiKeys { get; set; } = [];
    public UserToken? Token { get; set; }
}
