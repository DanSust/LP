using LP.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;

namespace LP.Server.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [ApiController]
    public class BaseAuthController : ControllerBase
    {
        protected Guid UserId => Guid.Parse(GetUserId());

        private string GetUserId()
        {
            // 🔒 Security: User is guaranteed to be authenticated here due to [Authorize]
            var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            Claim? userIdClaim = null;
            if (isAuthenticated)
            {
                userIdClaim = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
            }
            //var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
            //                  ?? User.FindFirst("sub") // JWT 'sub' claim
            //                  ?? User.FindFirst("ID"); // Custom claim

            return userIdClaim?.Value ?? Guid.Empty.ToString(); //throw new UnauthorizedAccessException("User ID not found in claims");
        }
    }
    
}
