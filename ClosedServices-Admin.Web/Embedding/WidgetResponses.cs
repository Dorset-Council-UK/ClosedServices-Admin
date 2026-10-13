using System.Text.Encodings.Web;

namespace ClosedServices_Admin.Embedding;

public static class WidgetResponses
{
    public static bool IsWidgetRequest(HttpContext context) => string.Equals(
        context.Request.Path.Value?.TrimEnd('/'), "/embed/service-status", StringComparison.OrdinalIgnoreCase);

    public static IApplicationBuilder UseWidgetResponses(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var isWidget = IsWidgetRequest(context);
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                if (isWidget)
                {
                    headers.CacheControl = "no-store";
                    headers.Remove("X-Frame-Options");
                    headers["Content-Security-Policy"] = "default-src 'none'; script-src 'self'; style-src 'self'; "
                        + "base-uri 'none'; form-action 'none'; frame-ancestors *";
                    headers["Referrer-Policy"] = "no-referrer";
                }
                else
                {
                    if (!headers.ContainsKey("X-Frame-Options"))
                    {
                        headers["X-Frame-Options"] = "SAMEORIGIN";
                    }
                    if (!headers.ContainsKey("Content-Security-Policy"))
                    {
                        headers["Content-Security-Policy"] = "frame-ancestors 'self'";
                    }
                }
                return Task.CompletedTask;
            });
            try
            {
                await next(context);
            }
            catch (Exception exception) when (isWidget && !context.Response.HasStarted
                && !context.RequestAborted.IsCancellationRequested)
            {
                context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("PublicStatusWidget")
                    .LogError(exception, "Unable to render public service status widget");
                context.Response.Clear();
                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError,
                    "Service status is temporarily unavailable. Please try again shortly.", context.RequestAborted);
            }
        });
    }

    public static Task WriteErrorAsync(HttpContext context, int statusCode, string message, CancellationToken cancellationToken)
    {
        var encoder = HtmlEncoder.Default;
        var assetRoot = encoder.Encode(context.Request.PathBase.Add("/embed-assets").Value!);
        var refreshUrl = encoder.Encode($"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}");
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "text/html; charset=utf-8";
        return context.Response.WriteAsync("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Service status</title>"
            + $"<link rel=\"stylesheet\" href=\"{assetRoot}/service-status.css\">"
            + $"<script src=\"{assetRoot}/service-status.js\" defer></script></head><body><main><h1>Service status</h1>"
            + $"<p class=\"error\" role=\"alert\">{encoder.Encode(message)}</p>"
            + $"<p>Updates every minute. <a href=\"{refreshUrl}\">Refresh now</a></p>"
            + "<noscript><p>Automatic updates require JavaScript. Use “Refresh now” to check the latest status.</p></noscript>"
            + "</main></body></html>", cancellationToken);
    }
}
