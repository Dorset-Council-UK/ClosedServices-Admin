
using ClosedServices_Admin.Endpoints.Account;
using ClosedServices_Admin_Shared.Options;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Polly;

namespace Microsoft.AspNetCore.Builder;

internal static class AuthenticationExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        /// <summary>
        /// Configures authentication, authorization and policies.
        /// </summary>
        internal IHostApplicationBuilder AddAuthentication()
        {
            var azureAdSection = builder.Configuration
                .GetSection(ClosedServicesOptions.SectionName)
                .GetSection(AzureAdOptions.SectionName);


            // Setup Authentication
            builder.Services
                .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(azureAdSection);

            builder.Services.AddResilientHttpClients();

            const string authRetryCookieName = "authretry-closedservices";

            builder.Services
            .AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
            .Configure<IHttpClientFactory>((options, httpClientFactory) =>
            {
                options.Backchannel = httpClientFactory.CreateClient("OpenIdConnectResilient");
                options.SignedOutRedirectUri = "/account/signed-out";
                options.AccessDeniedPath = "/account/access-denied";
                options.Events ??= new OpenIdConnectEvents();
                var existingRedirectHandler = options.Events.OnRedirectToIdentityProvider;
                var existingOnRemoteFailureHandler = options.Events.OnRemoteFailure;
                var existingOnTokenValidatedHandler = options.Events.OnTokenValidated;

                // Workaround for Entra External ID stale session errors on first login of the day.
                // When a user's Entra session expires overnight, the first authentication attempt can fail
                // with AADSTS50133 (session invalid due to expiry) or AADSTS165000 (session context missing).
                // This handler retries authentication once to obtain a fresh session. If the retry also fails
                // (indicating a genuine issue like a required password change), the user is redirected to an
                // error page to avoid an infinite redirect loop.
                options.Events.OnRemoteFailure = async context =>
                {
                    if (context.Failure?.Message?.Contains("AADSTS50133", StringComparison.Ordinal) == true ||
                        context.Failure?.Message?.Contains("AADSTS165000", StringComparison.Ordinal) == true)
                    {
                        
                        var hasRetried = context.Request.Cookies.ContainsKey(authRetryCookieName);

                        if (!hasRetried)
                        {
                            context.Response.Cookies.Append(authRetryCookieName, "1", new CookieOptions
                            {
                                HttpOnly = true,
                                IsEssential = true,
                                Secure = true,
                                SameSite = SameSiteMode.Lax,
                                MaxAge = TimeSpan.FromMinutes(5)
                            });

                            var signInPath = context.Request.PathBase.Add("/MicrosoftIdentity/Account/SignIn");
                            context.Response.Redirect($"{signInPath}?returnUrl=%2F");
                            context.HandleResponse();
                        }
                        else
                        {
                            context.Response.Cookies.Delete(authRetryCookieName);
                            var loginFailedPath = context.Request.PathBase.Add("/account/login-failed");
                            context.Response.Redirect(loginFailedPath);
                            context.HandleResponse();
                        }
                    }
                    else
                    {

                        if (existingOnRemoteFailureHandler != null)
                            await existingOnRemoteFailureHandler(context);
                    }
                };
                options.Events.OnTokenValidated = async context =>
                {
                    context.Response.Cookies.Delete(authRetryCookieName);
                    if (existingOnTokenValidatedHandler != null)
                        await existingOnTokenValidatedHandler(context);
                };
            });

            // Add Blazor cascading authentication state
            builder.Services.AddCascadingAuthenticationState();

            // Setup Authorization
            builder.Services.AddAuthorization();
            //builder.Services
            //    .AddAuthorizationBuilder()
            //    .AddPolicy(PolicyNames.Reader, policy => policy
            //        .RequireAuthenticatedUser()
            //        .RequireAssertion(context =>
            //            context.User.IsInRole(RoleNames.Reader) ||
            //            context.User.IsInRole(RoleNames.Writer) ||
            //            context.User.IsInRole(RoleNames.Admin)))
            //    .AddPolicy(PolicyNames.Writer, policy => policy
            //        .RequireAuthenticatedUser()
            //        .RequireAssertion(context =>
            //            context.User.IsInRole(RoleNames.Writer) ||
            //            context.User.IsInRole(RoleNames.Admin)))
            //    .AddPolicy(PolicyNames.Admin, policy => policy
            //        .RequireAuthenticatedUser()
            //        .RequireRole(RoleNames.Admin))
            //    .SetFallbackPolicy(policy: null); // Anonymous access allowed unless [Authorize] is used


            return builder;
        }
    }

    extension(IServiceCollection services)
    {
        /// <summary>
        /// Configures resilient HttpClients for default and OpenIdConnect backchannel.
        /// </summary>
        internal IServiceCollection AddResilientHttpClients()
        {
            // Default HttpClient resilience
            services
                .AddHttpClient(Options.DefaultName)
                .AddStandardResilienceHandler();

            // OpenIdConnect backchannel resilience
            const string clientName = "OpenIdConnectResilient";
            services
                .AddHttpClient(clientName)
                .AddStandardResilienceHandler();

            services
                .AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
                .Configure<IHttpClientFactory>((options, httpClientFactory) =>
                {
                    options.Backchannel = httpClientFactory.CreateClient(clientName);
                });

            return services;
        }
    }

    extension(WebApplication app)
    {
        /// <summary>
        /// Configures authentication-related HTTP endpoints for the application, including sign in, sign out, and Microsoft Identity challenge routes.
        /// </summary>
        /// <returns>The <see cref="WebApplication"/> instance with authentication endpoints mapped.</returns>
        internal WebApplication MapAuthenticationEndpoints()
        {
            app.MapGet("sign-in", AccountEndpoints.SignIn)
                .WithTags("Account")
                .WithDisplayName("Sign in")
                .WithSummary("Signs the user into the application")
                .AllowAnonymous();

            app.MapGet("sign-out", AccountEndpoints.SignOut)
                .WithTags("Account")
                .WithDisplayName("Sign out")
                .WithSummary("Signs the user out of the application.")
                .AllowAnonymous();

            app.MapGet("MicrosoftIdentity/Account/Challenge", AccountEndpoints.Challenge)
                .WithTags("Account")
                .WithDisplayName("Challenges the user.")
                .WithSummary("Challenge generating a redirect to Azure AD to sign in the user.")
                .AllowAnonymous();

            return app;
        }
    }
}
