using Microsoft.Extensions.Options;
using TodoExtended.Web.Services;

namespace TodoExtended.Tests.Services;

public class PushSyncGateTests
{
    [Fact]
    public void IsEligible_WhenUserIsAllowlistedButPushSyncIsDisabled_ReturnsFalse()
    {
        // Arrange
        var gate = new PushSyncGate(Options.Create(new PushSyncOptions
        {
            AllowedUsers = ["user@example.com"],
        }));

        // Act
        var result = gate.IsEligible("user@example.com", preferredUsername: null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsEligible_WhenUserIsAllowlistedWithDifferentCase_ReturnsTrue()
    {
        // Arrange
        var gate = new PushSyncGate(Options.Create(new PushSyncOptions
        {
            Enabled = true,
            AllowedUsers = ["Allowed.User@Example.com"],
        }));

        // Act
        var result = gate.IsEligible("allowed.user@example.com", preferredUsername: null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsEligible_WhenPreferredUsernameMatchesAllowlist_ReturnsTrue()
    {
        // Arrange
        var gate = new PushSyncGate(Options.Create(new PushSyncOptions
        {
            Enabled = true,
            AllowedUsers = ["preferred.user"],
        }));

        // Act
        var result = gate.IsEligible(userEmail: null, preferredUsername: "Preferred.User");

        // Assert
        Assert.True(result);
    }
}
