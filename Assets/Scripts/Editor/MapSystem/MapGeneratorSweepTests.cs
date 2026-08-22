using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Seed sweep over every <see cref="MapRunConfigSO"/> in the project, including the Test Stages and
/// Boss Rush. New configs are picked up automatically, so a stage added without content cannot slip
/// through unvalidated.
/// </summary>
public sealed class MapGeneratorSweepTests
{
    const int CiSeedCount = 256;
    const int SoakSeedCount = 10000;

    [Test]
    public void ProjectHasRunConfigsToValidate()
    {
        Assert.That(MapContentValidator.LoadAllRunConfigs(), Is.Not.Empty, "No MapRunConfigSO assets were found.");
    }

    [Test]
    public void EveryRunConfigGeneratesValidMapsAcrossSeeds()
    {
        SweepAllConfigs(CiSeedCount);
    }

    /// <summary>
    /// Manual soak run. Explicit so it never slows the normal suite; run it from the Test Runner
    /// after changing the generator, the room definitions, or the branch limits.
    /// </summary>
    [Test, Explicit("Manual soak test: 10,000 seeds per run config.")]
    public void SoakEveryRunConfigAcrossTenThousandSeeds()
    {
        SweepAllConfigs(SoakSeedCount);
    }

    static void SweepAllConfigs(int seedCount)
    {
        List<MapRunConfigSO> configs = MapContentValidator.LoadAllRunConfigs();
        Assert.That(configs, Is.Not.Empty, "No MapRunConfigSO assets were found.");

        for (int i = 0; i < configs.Count; i++)
        {
            MapRunConfigSO config = configs[i];

            // A config that cannot pass its own authoring rules has nothing meaningful to sweep,
            // and MapContentValidationTests already reports it.
            if (!MapRunConfigValidator.Validate(config, out string configError))
                Assert.Fail($"'{config.name}' is not a valid run config:\n{configError}");

            for (int seed = 0; seed < seedCount; seed++)
            {
                MapGraph graph = MapGenerator.Generate(config, seed);

                Assert.That(
                    MapPathValidator.Validate(graph, config, out string pathError),
                    Is.True,
                    $"'{config.name}' seed {seed}: {pathError}");

                Assert.That(
                    MapGraphStructureValidator.Validate(graph, config, out string structureError),
                    Is.True,
                    $"'{config.name}' seed {seed}: {structureError}");
            }
        }
    }
}
