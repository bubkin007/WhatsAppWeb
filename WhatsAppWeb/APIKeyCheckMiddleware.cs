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
        //we could inject here our database context to do checks against the db
        if (httpContext.Request.Headers.TryGetValue("X-CUSTOM-HEADER", out StringValues value))
        {
            //do the checks on key
            var apikey = value;
        }
        else
        {
            //return 403
            httpContext.Response.StatusCode = 403;
        }   
        await _next(httpContext);
    }
}

// Extension method used to add the middleware to the HTTP request pipeline.
public static class APIKeyCheckMiddlewareExtensions
{
    public static IApplicationBuilder UseAPIKeyCheckMiddleware(this IApplicationBuilder builder)
    { 
        return builder.UseMiddleware<APIKeyCheckMiddleware>();
    }
}