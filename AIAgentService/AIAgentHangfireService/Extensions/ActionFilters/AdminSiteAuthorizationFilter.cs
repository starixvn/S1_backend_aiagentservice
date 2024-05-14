using Hangfire.Dashboard;

namespace AIAgentHangfireService.Extensions.ActionFilters
{
    public class AdminSiteAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();
            return httpContext.User.Identity.IsAuthenticated;
        }
    }
}
