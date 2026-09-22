using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Orbis.Api;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class ControllerContractTests(DatabaseFixture database)
{
    [Fact]
    public async Task EveryBusinessRouteUsesAControllerAndRequiresTheCompletePolicyAndLimiter()
    {
        await using var application = new ApiFactory(database);
        using var client = application.CreateClient();
        var routes = application.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>().Where(endpoint => endpoint.RoutePattern.RawText!.TrimStart('/').StartsWith("v1/", StringComparison.Ordinal)).ToArray();
        Assert.Equal(8, routes.Length);
        foreach (var route in routes)
        {
            Assert.NotNull(route.Metadata.GetMetadata<ControllerActionDescriptor>());
            Assert.Null(route.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.Contains(route.Metadata.GetOrderedMetadata<IAuthorizeData>(), policy => policy.Policy == ApiPolicies.TenantAccess);
            Assert.Equal("database-operations", route.Metadata.GetMetadata<EnableRateLimitingAttribute>()!.PolicyName);
            var path = "/" + route.RoutePattern.RawText!.TrimStart('/').Replace("{id:guid}", database.OrderA.ToString(), StringComparison.Ordinal);
            using var message = new HttpRequestMessage(new HttpMethod(Assert.Single(route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)), path);
            using var response = await client.SendAsync(message);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("sub")]
    [InlineData("client_id")]
    [InlineData("jti")]
    [InlineData("iat")]
    public async Task ExplicitControllerAuthorizationDoesNotWeakenRequiredClaims(string claim)
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, application.Token(database.UserA, omittedClaim: claim));
        using var response = await client.GetAsync("/v1/work-orders");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"description\":\"private-marker\"")]
    [InlineData("{\"description\":\"valid\",\"tenantId\":\"private-marker\"}")]
    [InlineData("null")]
    [InlineData("")]
    public async Task InvalidBodiesReturnProblemDetailsWithoutEchoingInput(string body)
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, application.Token(database.UserA));
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.PostAsync("/v1/work-orders", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-marker", json, StringComparison.Ordinal);
        using var problem = JsonDocument.Parse(json);
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task InvalidQueryAndUnsupportedMediaTypeAreRejectedBeforeExecutingCommands()
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, application.Token(database.UserA));
        using var query = await client.GetAsync("/v1/work-orders?limit=private-marker");
        Assert.Equal(HttpStatusCode.BadRequest, query.StatusCode);
        Assert.DoesNotContain("private-marker", await query.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var media = await client.PostAsync("/v1/work-orders", new StringContent("private-marker", Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, media.StatusCode);
    }

    [Fact]
    public async Task DevelopmentOpenApiDescribesAllBusinessRoutesAndJsonContracts()
    {
        await using var application = new ApiFactory(database, "Development");
        using var client = application.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var paths = document!.RootElement.GetProperty("paths");
        Assert.Equal(8, paths.EnumerateObject().Where(path => path.Name.StartsWith("/v1/", StringComparison.Ordinal))
            .Sum(path => path.Value.EnumerateObject().Count()));
        var orders = paths.GetProperty("/v1/work-orders");
        Assert.Equal(new[] { "cursor", "limit" }, orders.GetProperty("get").GetProperty("parameters").EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString()).Order());
        Assert.True(orders.GetProperty("post").GetProperty("requestBody").GetProperty("required").GetBoolean());
        Assert.True(orders.GetProperty("post").GetProperty("responses").TryGetProperty("201", out _));
        foreach (var action in new[] { "assign", "accept", "start", "complete", "cancel" })
            Assert.True(paths.GetProperty($"/v1/work-orders/{{id}}/{action}").GetProperty("post").GetProperty("responses").TryGetProperty("200", out _));
    }

    private HttpClient Client(ApiFactory application, string token)
    {
        var client = application.CreateClient();
        client.BaseAddress = new Uri($"https://{database.HostA}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
