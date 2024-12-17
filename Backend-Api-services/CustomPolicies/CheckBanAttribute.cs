using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;
using System.Threading.Tasks;
using Backend_Api_services.Services.Interfaces; // IBanService

public class CheckBanAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var httpContext = context.HttpContext;

        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == "userId");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
            {
                // Resolve the ban service from DI
                var banService = httpContext.RequestServices.GetService(typeof(IBanService)) as IBanService;

                bool isBanned = await banService.IsUserBannedAsync(userId);
                if (isBanned)
                {
                    context.Result = new UnauthorizedObjectResult("You have been banned");
                    return;
                }
            }
        }

        // If not authenticated or not banned, just continue
    }
}
