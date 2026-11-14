using System.Reflection;

namespace SecureLogin.UnitTests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_does_not_reference_other_SecureLogin_projects()
    {
        var references = typeof(SecureLogin.Domain.DomainMarker).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => reference.Name?.StartsWith("SecureLogin.", StringComparison.Ordinal) == true);
    }
}
