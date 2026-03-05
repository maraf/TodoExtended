namespace TodoExtended.Web.Data;

public class UserToken
{
    public required string UserId { get; set; }
    public required byte[] EncryptedCacheData { get; set; }
    public DateTime UpdatedUtc { get; set; }
    
    public User? User { get; set; }
}
