using System.Text.Json;
using System.Text.Json.Serialization;
using Orbis.DataGenerator;
using Orbis.QueryProbe;

if (args.Length != 2 || !Enum.TryParse<DatasetProfile>(args[0], out var profile) || !Enum.IsDefined(profile))
{
    Console.Error.WriteLine("Usage: Orbis.QueryProbe Uniform|HotTenant NEW_REPORT_PATH. Requires ORBIS_TEST_RUNTIME_CONNECTION; Small seed 42 only.");
    return 2;
}
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    var connection = Environment.GetEnvironmentVariable("ORBIS_TEST_RUNTIME_CONNECTION") ?? throw new InvalidOperationException("Runtime connection missing.");
    await using var output = new FileStream(Path.GetFullPath(args[1]), FileMode.CreateNew, FileAccess.Write, FileShare.None);
    var report = await QueryPlanProbe.CaptureAsync(connection, new(DatasetLevel.Small, profile, 42), cancellationToken: cancellation.Token);
    await JsonSerializer.SerializeAsync(output, report, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }, cancellation.Token);
    Console.WriteLine($"Captured {report.Queries.Length} SQL plans with RLS. Serial SQL timings are not API latency or throughput.");
    return 0;
}
catch (Exception exception) when (exception is not OutOfMemoryException)
{
    Console.Error.WriteLine($"SQL probe failed ({exception.GetType().Name}); report is invalid. Connection/payload details omitted.");
    return 1;
}
