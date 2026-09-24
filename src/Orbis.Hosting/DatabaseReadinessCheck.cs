using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Orbis.Hosting;

public abstract class DatabaseReadinessCheck(TimeProvider timeProvider) : IHealthCheck, IHostedService, IDisposable
{
    private readonly SemaphoreSlim probeLock = new(1, 1);
    private ProbeResult? latest;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!await IsSafeAsync(cancellationToken))
            throw new InvalidOperationException("Runtime database privileges or tenant protections are unsafe.");
        Volatile.Write(ref latest, new ProbeResult(timeProvider.GetTimestamp(), HealthCheckResult.Healthy()));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var cached = Volatile.Read(ref latest);
        if (IsFresh(cached)) return cached!.Result;

        // Somente a saúde é reutilizada; identidade e permissões continuam consultadas em cada acesso.
        await probeLock.WaitAsync(cancellationToken);
        try
        {
            cached = Volatile.Read(ref latest);
            if (IsFresh(cached)) return cached!.Result;
            HealthCheckResult result;
            try
            {
                result = await IsSafeAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
            }
            catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
            {
                // Falhas também expiram: uma indisponibilidade não deve causar uma tempestade de probes.
                result = HealthCheckResult.Unhealthy();
            }
            Volatile.Write(ref latest, new ProbeResult(timeProvider.GetTimestamp(), result));
            return result;
        }
        finally { probeLock.Release(); }
    }

    private bool IsFresh(ProbeResult? probe) => probe is not null &&
        timeProvider.GetElapsedTime(probe.Timestamp) < TimeSpan.FromSeconds(5);

    public void Dispose() => probeLock.Dispose();

    private sealed record ProbeResult(long Timestamp, HealthCheckResult Result);

    protected abstract Task<bool> IsSafeAsync(CancellationToken cancellationToken);
}
