using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Trend.MudWeb.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private AuthenticationState _currentState;

        public CustomAuthStateProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;

            // Ambil user dari HttpContext (Cookie)
            var user = _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
            _currentState = new AuthenticationState(user);
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(_currentState);
        }

        public void UpdateSubscriptionStatus(string email, string status)
        {
            // Ambil claims lama agar UserId dll tidak hilang
            var oldClaims = _currentState.User.Claims.Where(c => c.Type != "SubscriptionStatus").ToList();

            var identity = new ClaimsIdentity(oldClaims, "CustomAuth");
            identity.AddClaim(new Claim("SubscriptionStatus", status));

            var user = new ClaimsPrincipal(identity);
            _currentState = new AuthenticationState(user);

            NotifyAuthenticationStateChanged(Task.FromResult(_currentState));
        }
    }
}