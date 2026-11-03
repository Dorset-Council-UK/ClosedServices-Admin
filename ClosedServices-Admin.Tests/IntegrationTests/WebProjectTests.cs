using Aspire.Hosting.Testing;
using System.Net;

namespace ClosedServices_Admin.Tests.IntegrationTests;

public class WebProjectTests
{
    [Fact]
    public async Task WebProject_Starts_ReturningOK()
    {
        using var startupTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.ClosedServices_Admin_AppHost>(startupTimeout.Token);
        await using var app = await builder.BuildAsync(startupTimeout.Token);
        await app.StartAsync(startupTimeout.Token);

        using var requestTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await app.ResourceNotifications.WaitForResourceHealthyAsync("closedservices-admin-web", requestTimeout.Token);

        using var client = app.CreateHttpClient("closedservices-admin-web");
        using var response = await client.GetAsync("/", requestTimeout.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
