using Installer.Cli;
using NetArchTest.Rules;

namespace UnitTests.Installer.Cli;

public class LayerRulesTests
{
    [Test]
    public void Only_the_composition_root_touches_the_dao()
    {
        var result = Types.InAssembly(typeof(ServiceRegistration).Assembly)
            .That().DoNotHaveName(nameof(ServiceRegistration))
            .ShouldNot()
            .HaveDependencyOn("Installer.Dao")
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
