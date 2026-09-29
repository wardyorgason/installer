using Installer.Services;
using NetArchTest.Rules;

namespace UnitTests.Installer.Services;

public class LayerRulesTests
{
    [Test]
    public void Services_do_not_depend_on_the_cli()
    {
        var result = Types.InAssembly(typeof(ServicesServiceCollectionExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Installer.Cli")
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Test]
    public void Services_reach_the_outside_world_only_through_daos()
    {
        var result = Types.InAssembly(typeof(ServicesServiceCollectionExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("System.Diagnostics.Process", "System.IO.Compression", "SkiaSharp", "System.IO.File", "System.IO.Directory")
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
