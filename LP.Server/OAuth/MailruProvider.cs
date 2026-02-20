using LP.Entity;
using Microsoft.Extensions.Caching.Memory;

namespace LP.Server.OAuth
{
    public sealed class MailruProvider : IOAuthProvider
    {
        public string Name => "MailRU";

        public IMemoryCache _cache { get; set; }

        public string BuildAuthUrl(string state)
        {
            return "";
        }

        public Task<TokenResponse> ExchangeCodeAsync(string code)
        {
            return null;
        }

        public Task<User> GetUserInfoAsync(string accessToken)
        {
            return null;
        }

        public Task<(bool ok, string? email, string? name)> ValidateAsync(string? token)
        {
            return null;
        }
    }
}
