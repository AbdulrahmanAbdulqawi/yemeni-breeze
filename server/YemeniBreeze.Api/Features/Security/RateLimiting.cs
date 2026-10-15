using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

namespace YemeniBreeze.Api.Features.Security;

public static class RateLimiting
{
    /// <summary>Per-IP limit for the public forms that write data or send email.</summary>
    public const string PublicForms = "public-forms";

    public static IServiceCollection AddPublicFormProtection(this IServiceCollection services, IConfiguration config)
    {
        // Behind Traefik and the web container's nginx, the client IP is the second-to-last
        // X-Forwarded-For entry. Both proxies sit on the private Docker network, so trust any
        // address but only walk back as many hops as there are proxies (spoofed entries further
        // left are ignored).
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            options.ForwardLimit = config.GetValue("ForwardedHeaders:ForwardLimit", 2);
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PublicForms, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = config.GetValue("RateLimiting:PermitLimit", 10),
                        Window = TimeSpan.FromMinutes(config.GetValue("RateLimiting:WindowMinutes", 10)),
                        QueueLimit = 0
                    }));
        });
        return services;
    }
}
