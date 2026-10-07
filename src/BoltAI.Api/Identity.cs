using System.Security.Claims;
using System.Text.Encodings.Web;
using BoltAI.Application;
using BoltAI.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
namespace BoltAI.Api;
public sealed class DevAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IConfiguration config)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Fixed server configuration only: request headers and JSON cannot select an identity.
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, config["DevAuth:UserId"] ?? "DEMO-U100") };
        claims.AddRange((config.GetSection("DevAuth:Accounts").Get<string[]>() ?? ["C100"]).Select(x => new Claim("account_id", x)));
        claims.AddRange((config.GetSection("DevAuth:Roles").Get<string[]>() ?? ["BoltReader"]).Select(x => new Claim(ClaimTypes.Role, x)));
        return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
    }
}
public sealed class HttpUserContext(IHttpContextAccessor accessor) : IUserContext
{
    public UserIdentity GetUser()
    {
        var principal = accessor.HttpContext?.User;
        var id = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal?.FindFirstValue("sub");
        if (principal?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(id) || id.Length > 128)
            throw new SafeFailure("unauthenticated", "Authentication is required.");
        // These claim names are an explicit integration contract, pending BOLT confirmation.
        return new(id, principal.FindAll("account_id").Select(c => c.Value).Where(IsSafeClaim).Take(100).ToHashSet(StringComparer.Ordinal),
            principal.FindAll(ClaimTypes.Role).Select(c => c.Value).Where(IsSafeClaim).Take(30).ToHashSet(StringComparer.Ordinal));
    }
    private static bool IsSafeClaim(string value) => value.Length is > 0 and <= 64 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
}
