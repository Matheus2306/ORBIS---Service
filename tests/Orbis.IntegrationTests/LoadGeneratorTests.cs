using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class LoadGeneratorTests(DatabaseFixture database)
{
    [Theory]
    [InlineData("authorized", 200)]
    [InlineData("foreign-resource", 404)]
    [InlineData("foreign-host", 404)]
    [InlineData("invalid-token", 401)]
    [InlineData("untrusted-ca", 0)]
    [InlineData("wrong-name", 0)]
    public async Task ExternalGeneratorPreservesTlsAndAuthorization(string scenario, int expectedStatus)
    {
        await using var server = new HttpsApiTests.HttpsServer(database);
        var host = scenario switch { "foreign-host" => database.HostB, "wrong-name" => "localhost", _ => database.HostA };
        var resource = scenario is "foreign-host" or "foreign-resource" ? database.OrderB : database.OrderA;
        var token = server.Application.Token(database.UserA, wrongKey: scenario == "invalid-token");
        var result = await Send(server, host, $"/v1/work-orders/{resource}", token, untrusted: scenario == "untrusted-ca");
        Assert.Equal(expectedStatus, result.GetProperty("code").GetInt32());
        var error = result.GetProperty("error").GetString();
        if (expectedStatus == 0)
        {
            // Uma falha de socket não prova a rejeição criptográfica que este teste exige.
            Assert.Contains("x509:", error);
            Assert.Contains(scenario == "untrusted-ca" ? "unknown authority" : "localhost", error);
        }
        else if (expectedStatus == 200) Assert.Equal("", error);
    }

    [Fact]
    public async Task ExternalGeneratorSendsWritesWithoutLeakingTokensAndReplaysIdempotently()
    {
        await using var server = new HttpsApiTests.HttpsServer(database);
        var token = server.Application.Token(database.UserA);
        var key = Guid.NewGuid().ToString();
        var first = await Send(server, database.HostA, "/v1/work-orders", token, key);
        var replay = await Send(server, database.HostA, "/v1/work-orders", token, key);
        Assert.Equal(201, first.GetProperty("code").GetInt32());
        Assert.Equal(201, replay.GetProperty("code").GetInt32());
        Assert.Equal("false", Header(first, "Idempotency-Replayed"));
        Assert.Equal("true", Header(replay, "Idempotency-Replayed"));
        Assert.Equal(Header(first, "Location"), Header(replay, "Location"));
    }

    private static string? Header(JsonElement result, string name) => result.GetProperty("headers").GetProperty(name)[0].GetString();

    private async Task<JsonElement> Send(HttpsApiTests.HttpsServer server, string host, string path, string token,
        string? idempotencyKey = null, bool untrusted = false)
    {
        var executable = Environment.GetEnvironmentVariable("ORBIS_VEGETA_PATH")
            ?? throw new InvalidOperationException("Build the pinned load generator and run scripts/test-postgres.ps1; this check cannot be skipped.");
        using var pins = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "vegeta-toolchain.json")));
        await using (var file = File.OpenRead(executable))
            Assert.Equal(pins.RootElement.GetProperty("binarySha256").GetString(), Convert.ToHexString(await SHA256.HashDataAsync(file)));
        var root = new NpgsqlConnectionStringBuilder(database.RuntimeConnection).RootCertificate!;
        var directory = Path.Combine(Path.GetDirectoryName(root)!, "generator", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var port = server.Application.ClientOptions.BaseAddress.Port;
        var url = new UriBuilder("https", host, port, path).Uri.AbsoluteUri;
        var headers = new Dictionary<string, string[]> { ["Authorization"] = [$"Bearer {token}"] };
        if (idempotencyKey is not null)
        {
            headers["Idempotency-Key"] = [idempotencyKey];
            headers["Content-Type"] = ["application/json"];
        }
        var target = JsonSerializer.Serialize(new
        {
            method = idempotencyKey is null ? "GET" : "POST",
            url,
            header = headers,
            body = idempotencyKey is null ? "" : Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"description\":\"External generator transport validation\"}"))
        });
        var binary = Path.Combine(directory, "result.bin");
        // Um request por execução: prova transporte/contrato, sem fingir baseline de throughput.
        await Run(executable, ["attack", "-format=json", "-targets=stdin", "-rate=1/s", "-duration=200ms", "-workers=1",
            "-max-workers=1", "-max-connections=1", "-timeout=3s", "-redirects=0", "-max-body=0",
            "-root-certs", untrusted ? Path.Combine(Path.GetDirectoryName(root)!, "untrusted-root.crt") : root,
            "-connect-to", $"{host}:{port}:127.0.0.1:{port}", "-output", binary], target);
        var json = Path.Combine(directory, "result.jsonl");
        await Run(executable, ["encode", "-to=json", "-output", json, binary]);
        var lines = await File.ReadAllLinesAsync(json);
        // Exit code zero do gerador também ocorre com erros HTTP/TLS; a amostra é validada explicitamente.
        Assert.Single(lines);
        Assert.False(lines[0].Contains(token, StringComparison.Ordinal), "Token found in local result; value omitted.");
        using var document = JsonDocument.Parse(lines[0]);
        var result = document.RootElement.Clone();
        Assert.Equal(url, result.GetProperty("url").GetString());
        Assert.Equal(0, result.GetProperty("seq").GetInt64());
        Assert.True(result.GetProperty("latency").GetInt64() > 0);
        Assert.True(result.GetProperty("body").ValueKind == JsonValueKind.Null || result.GetProperty("body").GetString() == "");
        return result;
    }

    private static async Task Run(string executable, string[] arguments, string? input = null)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        // Tokens trafegam somente pelo pipe; proxies herdados não recebem dados do ambiente local.
        foreach (var variable in start.Environment.Keys.Where(key => key.Equals("HTTP_PROXY", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("HTTPS_PROXY", StringComparison.OrdinalIgnoreCase) || key.Equals("ALL_PROXY", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.Environment.Remove(variable);
        start.Environment["NO_PROXY"] = "*";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Load generator did not start.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var errors = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            if (input is not null) await process.StandardInput.WriteLineAsync(input.AsMemory(), deadline.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(deadline.Token);
            await Task.WhenAll(output, errors);
            Assert.True(process.ExitCode == 0, "Generator failed; raw output omitted to protect request data.");
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
    }
}
