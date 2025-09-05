using Microsoft.Extensions.Primitives;

internal class APIKeyCheckMiddleware
{
    private readonly RequestDelegate _next;

    public APIKeyCheckMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {    
        if (httpContext.Request.Headers.TryGetValue("X-CUSTOM-HEADER", out StringValues value))
        {
            var apikey = value;
        }
        else
        {
            httpContext.Response.StatusCode = 403;
        }
        await _next(httpContext);
    }
}
public static class APIKeyCheckMiddlewareExtensions
{
    public static IApplicationBuilder UseAPIKeyCheckMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<APIKeyCheckMiddleware>();
    }
}

