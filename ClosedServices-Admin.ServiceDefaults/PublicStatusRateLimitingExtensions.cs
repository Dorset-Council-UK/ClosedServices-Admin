using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Threading.RateLimiting;

namespace Microsoft.Extensions.Hosting;

public static class PublicStatusRateLimitingExtensions
{
    public const string PolicyName = "public-status";

    public static IServiceCollection AddPublicStatusRateLimiting(this IServiceCollection services,
        IConfiguration configuration, Func<HttpContext, CancellationToken, Task>? onRejected = null)
    {
        services.AddRateLimiter(options =>
        {
            var section = configuration.GetSection("PublicStatus:RateLimit");
            var permitLimit = section.GetValue("PermitLimit", 120);
            var windowSeconds = section.GetValue("WindowSeconds", 60);
            if (permitLimit <= 0 || windowSeconds <= 0)
            {
                throw new InvalidOperationException("Public status rate-limit values must be positive.");
            }
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = windowSeconds.ToString(CultureInfo.InvariantCulture);
                if (onRejected is not null)
                {
                    await onRejected(context.HttpContext, cancellationToken);
                }
                else
                {
                    await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Too many requests. Please try again later.").ExecuteAsync(context.HttpContext);
                }
            };
        });
        return services;
    }
}
