using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using TodoExtended.Web.Services;

namespace TodoExtended.Tests.Services;

public class AuthExceptionHelperTests
{
    #region IsConsentRequired

    [Fact]
    public void IsConsentRequired_DirectMsalUiRequiredException_ReturnsTrue()
    {
        var ex = new MsalUiRequiredException("consent_required", "User consent required");

        Assert.True(AuthExceptionHelper.IsConsentRequired(ex));
    }

    [Fact]
    public void IsConsentRequired_WrappedMsalUiRequiredException_ReturnsTrue()
    {
        var inner = new MsalUiRequiredException("consent_required", "User consent required");
        var wrapped = new InvalidOperationException("Cached tokens expired. User must sign in via OIDC again.", inner);

        Assert.True(AuthExceptionHelper.IsConsentRequired(wrapped));
    }

    [Fact]
    public void IsConsentRequired_UnrelatedExceptionWithNoInner_ReturnsFalse()
    {
        var ex = new InvalidOperationException("Something else went wrong");

        Assert.False(AuthExceptionHelper.IsConsentRequired(ex));
    }

    [Fact]
    public void IsConsentRequired_AggregateExceptionContainingMsalUiRequired_ReturnsTrue()
    {
        var inner = new MsalUiRequiredException("consent_required", "User consent required");
        var agg = new AggregateException(inner);

        Assert.True(AuthExceptionHelper.IsConsentRequired(agg));
    }

    [Fact]
    public void IsConsentRequired_DeepNestedMsalUiRequiredException_ReturnsTrue()
    {
        var msal = new MsalUiRequiredException("consent_required", "User consent required");
        var level2 = new InvalidOperationException("mid", msal);
        var level1 = new Exception("outer", level2);

        Assert.True(AuthExceptionHelper.IsConsentRequired(level1));
    }

    #endregion

    #region IsAuthException

    [Fact]
    public void IsAuthException_WrappedMsalUiRequiredException_ReturnsTrue()
    {
        var inner = new MsalUiRequiredException("consent_required", "User consent required");
        var wrapped = new InvalidOperationException("Cached tokens expired.", inner);

        Assert.True(AuthExceptionHelper.IsAuthException(wrapped));
    }

    [Fact]
    public void IsAuthException_UnrelatedGenericException_ReturnsFalse()
    {
        var ex = new Exception("totally unrelated");

        Assert.False(AuthExceptionHelper.IsAuthException(ex));
    }

    [Fact]
    public void IsAuthException_NotAuthenticatedException_ReturnsTrue()
    {
        var ex = new NotAuthenticatedException();

        Assert.True(AuthExceptionHelper.IsAuthException(ex));
    }

    #endregion
}
