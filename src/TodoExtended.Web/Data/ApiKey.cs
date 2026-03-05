namespace TodoExtended.Web.Data;

public class ApiKey
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public required string KeyHash { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastUsedUtc { get; set; }
    public bool IsRevoked { get; set; }
    
    public User? User { get; set; }
}
