using Hangfire.Dashboard;

namespace AIAgentHangfireService.Extensions.ActionFilters
{
    public class LocalAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            return true;
        }
    }
}
