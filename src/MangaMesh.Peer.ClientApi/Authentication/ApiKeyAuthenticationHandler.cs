using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace MangaMesh.Peer.ClientApi.Authentication
{
    public static class ApiKeyDefaults
    {
        public const string AuthenticationScheme = "ApiKey";
    }

    public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private const string ApiKeyHeaderName = "X-Api-Key";
        private readonly IConfiguration _configuration;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IConfiguration configuration)
            : base(options, logger, encoder)
        {
            _configuration = configuration;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyValues))
                return Task.FromResult(AuthenticateResult.Fail("Missing API key"));

            var providedKey = apiKeyValues.FirstOrDefault();
            var expectedKey = _configuration["ApiKey:Key"];

            if (string.IsNullOrEmpty(expectedKey) || providedKey != expectedKey)
                return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));

            var claims = new[] { new Claim(ClaimTypes.Name, "peer-client") };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
