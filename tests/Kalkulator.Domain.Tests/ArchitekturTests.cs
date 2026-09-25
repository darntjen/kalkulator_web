namespace Kalkulator.Domain.Tests;

public class ArchitekturTests
{
    // Der Rechenkern muss ohne Oberfläche und Datenbank testbar bleiben (docs/04_architektur.md, Abschnitt 4).
    [Theory]
    [InlineData("Kalkulator.Web")]
    [InlineData("Kalkulator.Infrastructure")]
    [InlineData("Kalkulator.Documents")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    public void Domain_hat_keine_Abhaengigkeit_auf(string verboten)
    {
        var referenzen = typeof(DomainAssembly).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(referenzen, r => r.Name!.StartsWith(verboten, StringComparison.Ordinal));
    }
}
