using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SafeLearnKids.Web.Services;

// US-50: while a child is using the device (the child-mode cookie is set and belongs to this parent), every page of
// the parent area sends them back to the child home. Only the PIN screen (/kids-exit) ends child mode.
public sealed class ChildModeFilter(KidContext kids) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (Equals(context.RouteData.Values["area"], "Parent") && http.User.IsInRole("Parent") && http.Request.Cookies.ContainsKey(KidContext.CookieName))
        {
            if (await kids.CurrentAsync(http.RequestAborted) is not null)
            {
                context.Result = HttpMethods.IsGet(http.Request.Method) ? new RedirectResult("/kids-home") : new StatusCodeResult(StatusCodes.Status403Forbidden);
                return;
            }
            // A cookie left from another account or a deleted child means nothing.
            kids.Clear();
        }
        await next();
    }
}
