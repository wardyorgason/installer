using Installer.Dao;
using NetArchTest.Rules;

namespace UnitTests.Installer.Dao;

public class LayerRulesTests
{
    [Test]
    public void Dao_does_not_depend_on_services_or_cli()
    {
        var result = Types.InAssembly(typeof(DaoServiceCollectionExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Installer.Services", "Installer.Cli")
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, string.Join(", ", result.FailingTypeNames ?? []));
    }


    [Test]
    public void Dtos_depend_on_no_other_installer_project()
    {
        var result = Types.InAssembly(typeof(global::Installer.Dtos.Build.Problem).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Installer.Dao", "Installer.Services", "Installer.Cli")
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
