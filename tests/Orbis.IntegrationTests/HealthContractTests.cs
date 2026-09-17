using System.Net;
using System.Net.Http.Headers;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class HealthContractTests(DatabaseFixture database)
{
    [Fact]
    public async Task HealthyDatabaseAllowsReadinessWithoutExposingOpenApiInTesting()
    {
        await using var application = new ApiFactory(database);
        using var client = application.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", application.Token(database.UserA));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }
}
