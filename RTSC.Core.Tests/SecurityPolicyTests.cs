using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RTSC.Core.Security;

namespace RTSC.Core.Tests;

public sealed class SecurityPolicyTests
{
    [Fact]
    public void MutatingApiRequestRequiresAntiforgery()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/events/123/register";

        Assert.True(SecurityPolicy.RequiresApiAntiforgery(context.Request));
    }

    [Theory]
    [InlineData("/api/integrations/max/webhook")]
    [InlineData("/api/integrations/telegram/webhook")]
    [InlineData("/api/integrations/vk/callback")]
    public void ExternalWebhooksAreExemptFromBrowserAntiforgery(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;

        Assert.False(SecurityPolicy.RequiresApiAntiforgery(context.Request));
    }

    [Fact]
    public void SafeApiGetDoesNotRequireAntiforgery()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/events";

        Assert.False(SecurityPolicy.RequiresApiAntiforgery(context.Request));
    }

    [Fact]
    public void AuthBucketIsStricterThanRegularWebBucket()
    {
        var auth = new DefaultHttpContext();
        auth.Request.Path = "/Login";
        var web = new DefaultHttpContext();
        web.Request.Path = "/Events";

        Assert.True(SecurityPolicy.RateLimitFor(auth).PermitLimit < SecurityPolicy.RateLimitFor(web).PermitLimit);
    }

    [Fact]
    public void AuthenticatedClientKeyUsesUserId()
    {
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            ], "test"))
        };

        Assert.Equal($"user:{userId}", SecurityPolicy.ClientKey(context));
    }
}
