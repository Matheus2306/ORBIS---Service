using Orbis.Application.Tenancy;
using Orbis.Domain.WorkOrders;

namespace Orbis.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void DomainDoesNotReferenceInfrastructureOrTransport()
    {
        var dependencies = typeof(WorkOrder).Assembly.GetReferencedAssemblies().Select(x => x.Name!);
        Assert.DoesNotContain(dependencies, name => name.StartsWith("Orbis.", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplicationDoesNotReferenceDatabaseOrApi()
    {
        var dependencies = typeof(TenantScope).Assembly.GetReferencedAssemblies().Select(x => x.Name!);
        Assert.DoesNotContain(dependencies, name => name is "Orbis.Infrastructure" or "Orbis.Api" ||
            name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }
}
