using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Orbis.DataGenerator;

if (args.Length != 4 || !Enum.TryParse<DatasetLevel>(args[0], out var level) || !Enum.IsDefined(level) ||
    !Enum.TryParse<DatasetProfile>(args[1], out var profile) || !Enum.IsDefined(profile) ||
    !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var seed))
{
    Console.Error.WriteLine("Usage: Orbis.DataGenerator Small|Medium|Large Uniform|HotTenant SEED NEW_MANIFEST_PATH. Connection: ORBIS_DATASET_CONNECTION environment variable.");
    return 2;
}
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    var connection = Environment.GetEnvironmentVariable("ORBIS_DATASET_CONNECTION") ?? throw new InvalidOperationException("Missing dataset connection.");
    DatasetImporter.ValidateDestination(connection);
    // CreateNew protege evidência anterior; o manifesto só fica completo depois da verificação.
    await using var output = new FileStream(Path.GetFullPath(args[3]), FileMode.CreateNew, FileAccess.Write, FileShare.None);
    var manifest = await DatasetImporter.ImportAsync(connection, new(level, profile, seed), cancellation.Token);
    await JsonSerializer.SerializeAsync(output, manifest, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }, cancellation.Token);
    Console.WriteLine($"Verified synthetic dataset: {manifest.Recipe.UserCount} users, {manifest.Recipe.TenantCount} tenants, {manifest.Recipe.OrderCount} orders. This is not an API load benchmark.");
    return 0;
}
catch (Exception exception) when (exception is not OutOfMemoryException)
{
    // Exceções de conexão/SQL podem conter payloads ou credenciais; detalhes não são enviados ao console.
    Console.Error.WriteLine($"Dataset generation failed ({exception.GetType().Name}). No valid manifest was produced. Inspect the isolated environment before retrying.");
    return 1;
}
