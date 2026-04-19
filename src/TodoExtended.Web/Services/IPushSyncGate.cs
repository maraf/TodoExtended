namespace TodoExtended.Web.Services;

public interface IPushSyncGate
{
    bool IsEligible(string? userEmail, string? preferredUsername);
}
