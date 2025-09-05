using Microsoft.Extensions.Primitives;

internal class APIKeyCheckMiddleware
{
    const string CustomHeaderName = "X-CUSTOM-HEADER";
    private readonly RequestDelegate _next;

    public APIKeyCheckMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        StringValues value;
        string apikey = string.Empty;
        if (httpContext.Request.Headers.TryGetValue(CustomHeaderName, out value))
        {
            apikey = value;
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

