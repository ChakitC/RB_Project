using NUnit.Framework;
using UnityEditor;

public sealed class MapGeneratorTests
{
    const string FallbackConfigPath = "Assets/Data/Map/Test_Map Run Config SO.asset";
    const string StageOneConfigPath = "Assets/Data/Map/TestStages/Test Stage 01 Map Run Config.asset";
    const int ReportedStageOneSeed = 300233656;

    [Test]
    public void FallbackConfigGeneratesValidMapAcrossSeeds()
    {
        MapRunConfigSO config = AssetDatabase.LoadAssetAtPath<MapRunConfigSO>(FallbackConfigPath);
        Assert.That(config, Is.Not.Null, $"Missing fallback config at '{FallbackConfigPath}'.");

        for (int seed = 0; seed < 256; seed++)
        {
            MapGraph graph = MapGenerator.Generate(config, seed);
            Assert.That(MapPathValidator.Validate(graph, config, out string error), Is.True, $"Seed {seed}: {error}");
        }
    }

    [Test]
    public void StageOneConfigGeneratesValidMapForReportedSeed()
    {
        MapRunConfigSO config = AssetDatabase.LoadAssetAtPath<MapRunConfigSO>(StageOneConfigPath);
        Assert.That(config, Is.Not.Null, $"Missing Stage One config at '{StageOneConfigPath}'.");

        MapGraph graph = MapGenerator.Generate(config, ReportedStageOneSeed);
        Assert.That(MapPathValidator.Validate(graph, config, out string error), Is.True, error);
    }

    [Test]
    public void StageOneConfigGeneratesValidMapAcrossSeeds()
    {
        MapRunConfigSO config = AssetDatabase.LoadAssetAtPath<MapRunConfigSO>(StageOneConfigPath);
        Assert.That(config, Is.Not.Null, $"Missing Stage One config at '{StageOneConfigPath}'.");

        for (int seed = 0; seed < 256; seed++)
        {
            MapGraph graph = MapGenerator.Generate(config, seed);
            Assert.That(MapPathValidator.Validate(graph, config, out string error), Is.True, $"Seed {seed}: {error}");
        }
    }
}
