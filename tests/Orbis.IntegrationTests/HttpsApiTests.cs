using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Npgsql;
using Orbis.Application.WorkOrders;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class HttpsApiTests(DatabaseFixture database)
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    public async Task RealKestrelPreservesAuthenticationIsolationAndIdempotency(int major, int minor)
    {
        await using var server = new HttpsServer(database);
        using var client = server.CreateClient();
        client.DefaultRequestVersion = new Version(major, minor);
        client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        client.DefaultRequestHeaders.Host = database.HostA;
        using var anonymous = await client.GetAsync($"/v1/work-orders/{database.OrderA}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(new Version(major, minor), anonymous.Version);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.Application.Token(database.UserA));
        using var detail = await client.GetAsync($"/v1/work-orders/{database.OrderA}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var created = await client.PostAsJsonAsync("/v1/work-orders", new { description = "Real HTTPS request" });
        using var replayed = await client.PostAsJsonAsync("/v1/work-orders", new { description = "Real HTTPS request" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replayed.StatusCode);
        var first = await created.Content.ReadFromJsonAsync<CreatedOrder>();
        Assert.Equal(first, await replayed.Content.ReadFromJsonAsync<CreatedOrder>());
        Assert.Equal("false", created.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal("true", replayed.Headers.GetValues("Idempotency-Replayed").Single());
        using var foreignId = await client.GetAsync($"/v1/work-orders/{database.OrderB}");
        Assert.Equal(HttpStatusCode.NotFound, foreignId.StatusCode);
        client.DefaultRequestHeaders.Host = database.HostB;
        using var foreignHost = await client.GetAsync($"/v1/work-orders/{database.OrderB}");
        Assert.Equal(HttpStatusCode.NotFound, foreignHost.StatusCode);
        client.DefaultRequestHeaders.Host = database.HostA;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.Application.Token(database.UserA, wrongKey: true));
        using var invalidToken = await client.GetAsync($"/v1/work-orders/{database.OrderA}");
        Assert.Equal(HttpStatusCode.Unauthorized, invalidToken.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HttpsRejectsUntrustedRootOrMismatchedName(bool untrustedRoot)
    {
        await using var server = new HttpsServer(database);
        using var client = server.CreateClient(untrustedRoot);
        if (!untrustedRoot) client.BaseAddress = new UriBuilder(client.BaseAddress!) { Host = "localhost" }.Uri;
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("/health/live"));
        Assert.IsType<AuthenticationException>(error.InnerException);
    }

    private sealed class HttpsServer : IAsyncDisposable
    {
        private readonly X509Certificate2 certificate;
        private readonly X509Certificate2 root;
        private readonly X509Certificate2 untrustedRoot;
        public ApiFactory Application { get; }

        public HttpsServer(DatabaseFixture database)
        {
            var rootPath = new NpgsqlConnectionStringBuilder(database.RuntimeConnection).RootCertificate!;
            var directory = Path.GetDirectoryName(rootPath)!;
            certificate = LoadServerCertificate(directory);
            root = X509Certificate2.CreateFromPem(File.ReadAllText(rootPath));
            untrustedRoot = X509Certificate2.CreateFromPem(File.ReadAllText(Path.Combine(directory, "untrusted-root.crt")));
            Application = new ApiFactory(database, "Performance");
            // Listener real, porta dinâmica e loopback: o teste não publica a API na rede local.
            Application.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
            Application.StartServer();
            if (Application.ClientOptions.BaseAddress.Scheme != "https") throw new InvalidOperationException("HTTPS listener was not configured.");
        }

        public HttpClient CreateClient(bool untrusted = false)
        {
            var policy = new X509ChainPolicy
            {
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                // A CA efêmera não publica CRL/OCSP; cadeia, validade, EKU e nome continuam verificados pelo TLS.
                RevocationMode = X509RevocationMode.NoCheck,
                VerificationFlags = X509VerificationFlags.NoFlag
            };
            policy.CustomTrustStore.Add(untrusted ? untrustedRoot : root);
            return new HttpClient(new SocketsHttpHandler { SslOptions = new SslClientAuthenticationOptions { CertificateChainPolicy = policy } })
            { BaseAddress = Application.ClientOptions.BaseAddress, Timeout = TimeSpan.FromSeconds(10) };
        }

        private static X509Certificate2 LoadServerCertificate(string directory)
        {
            var pem = X509Certificate2.CreateFromPemFile(Path.Combine(directory, "server.crt"), Path.Combine(directory, "server.key"));
            if (!OperatingSystem.IsWindows()) return pem;
            using (pem)
            {
                // Schannel exige um contêiner de chave; sem PersistKeySet, Dispose libera a chave temporária do usuário.
                var pkcs12 = pem.Export(X509ContentType.Pkcs12);
                try { return X509CertificateLoader.LoadPkcs12(pkcs12, null, X509KeyStorageFlags.UserKeySet); }
                finally { CryptographicOperations.ZeroMemory(pkcs12); }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Application.DisposeAsync();
            certificate.Dispose(); root.Dispose(); untrustedRoot.Dispose();
        }
    }
}
