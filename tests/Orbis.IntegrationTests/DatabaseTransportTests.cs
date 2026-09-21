using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class DatabaseTransportTests(DatabaseFixture database)
{
    [Fact]
    public async Task RuntimeNegotiatesEncryptedTransportWithFullVerification()
    {
        var settings = new NpgsqlConnectionStringBuilder(database.RuntimeConnection);
        Assert.Equal(SslMode.VerifyFull, settings.SslMode);
        Assert.False(string.IsNullOrWhiteSpace(settings.RootCertificate));
        await using var connection = new NpgsqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT ssl,version,cipher,bits FROM pg_stat_ssl WHERE pid=pg_backend_pid()", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.Contains(reader.GetString(1), new[] { "TLSv1.2", "TLSv1.3" });
        Assert.False(string.IsNullOrWhiteSpace(reader.GetString(2)));
        Assert.True(reader.GetInt32(3) >= 128);
    }

    [Fact]
    public async Task ServerRejectsUnencryptedConnectionEvenWithValidCredentials()
    {
        var settings = new NpgsqlConnectionStringBuilder(database.RuntimeConnection) { Pooling = false, SslMode = SslMode.Disable, RootCertificate = null };
        await using var connection = new NpgsqlConnection(settings.ConnectionString);
        var error = await Assert.ThrowsAsync<PostgresException>(() => connection.OpenAsync());
        Assert.Equal(PostgresErrorCodes.InvalidAuthorizationSpecification, error.SqlState);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClientRejectsUntrustedRootOrMismatchedServerName(bool untrustedRoot)
    {
        var settings = new NpgsqlConnectionStringBuilder(database.RuntimeConnection) { Pooling = false };
        if (untrustedRoot) settings.RootCertificate = Path.Combine(Path.GetDirectoryName(settings.RootCertificate!)!, "untrusted-root.crt");
        else settings.Host = "localhost"; // localhost não faz parte dos SANs do certificado de teste.
        await using var connection = new NpgsqlConnection(settings.ConnectionString);
        var error = await Assert.ThrowsAsync<NpgsqlException>(() => connection.OpenAsync());
        Assert.IsType<AuthenticationException>(error.InnerException);
    }

    [Fact]
    public async Task PerformanceEnvironmentStartsAndServesAuthorizedOrderOverVerifiedDatabaseTls()
    {
        await using var application = new ApiFactory(database, "Performance");
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Host = database.HostA;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", application.Token(database.UserA));
        using var response = await client.GetAsync($"/v1/work-orders/{database.OrderA}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void PerformanceEnvironmentRejectsEncryptionWithoutCertificateVerification()
    {
        var settings = new NpgsqlConnectionStringBuilder(database.RuntimeConnection) { SslMode = SslMode.Require };
        using var baseApplication = new ApiFactory(database, "Performance");
        using var application = baseApplication.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Orbis"] = settings.ConnectionString })));
        var error = Assert.Throws<InvalidOperationException>(() => application.CreateClient());
        Assert.Contains("full certificate verification", error.Message, StringComparison.Ordinal);
    }
}
