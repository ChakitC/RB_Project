using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Proves that each <see cref="MapGraphStructureValidator"/> invariant actually fires. The seed
/// sweep only ever sees healthy graphs, so the rules are exercised against broken ones here.
/// </summary>
public sealed class MapGraphStructureValidatorDetectionTests
{
    MapRunConfigSO config;
    MapGenerationProfileSO generationProfile;

    [SetUp]
    public void SetUp()
    {
        // Branch limits live on the generation profile, so the config needs one to have any.
        generationProfile = ScriptableObject.CreateInstance<MapGenerationProfileSO>();
        generationProfile.name = "StructureTestProfile";

        config = ScriptableObject.CreateInstance<MapRunConfigSO>();
        config.name = "StructureTestConfig";
        SerializedObject serialized = new SerializedObject(config);
        serialized.FindProperty("generationProfile").objectReferenceValue = generationProfile;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        SetBranchLimits(0, 0);
    }

    [TearDown]
    public void TearDown()
    {
        if (config != null)
            Object.DestroyImmediate(config);
        config = null;

        if (generationProfile != null)
            Object.DestroyImmediate(generationProfile);
        generationProfile = null;
    }

    [Test]
    public void MinimalStartToBossGraphIsValid()
    {
        Assert.That(MapGraphStructureValidator.Validate(BuildStartToBoss(), config, out string error), Is.True, error);
    }

    [Test]
    public void OutgoingEdgeWithoutTheMatchingIncomingEdgeIsRejected()
    {
        var graph = new MapGraph();
        var start = new MapNode("start", MapNodeType.Start, 0, true);
        var boss = new MapNode("boss", MapNodeType.Boss, 1, true);
        graph.AddNode(start);
        graph.AddNode(boss);
        start.AddOutgoing(boss.Id);

        Assert.That(MapGraphStructureValidator.Validate(graph, config, out string error), Is.False);
        Assert.That(error, Does.Contain("no matching incoming edge"));
    }

    [Test]
    public void CriticalPathWithANonAdjacentPairIsRejected()
    {
        var graph = new MapGraph();
        graph.AddNode(new MapNode("start", MapNodeType.Start, 0, true));
        graph.AddNode(new MapNode("boss", MapNodeType.Boss, 1, true));

        Assert.That(MapGraphStructureValidator.Validate(graph, config, out string error), Is.False);
        Assert.That(error, Does.Contain("not adjacent"));
    }

    [Test]
    public void BranchCountBelowMinBranchCountIsRejected()
    {
        SetBranchLimits(2, 3);

        Assert.That(MapGraphStructureValidator.Validate(BuildStartToBoss(), config, out string error), Is.False);
        Assert.That(error, Does.Contain("below MinBranchCount 2"));
    }

    [Test]
    public void BranchCountAboveMaxBranchCountIsRejected()
    {
        MapGraph graph = BuildStartToBoss();
        var branch = new MapNode("branch_00", MapNodeType.Reward, 1, false);
        graph.AddNode(branch);
        graph.AddEdge("start", branch.Id);

        Assert.That(MapGraphStructureValidator.Validate(graph, config, out string error), Is.False);
        Assert.That(error, Does.Contain("above MaxBranchCount 0"));
    }

    static MapGraph BuildStartToBoss()
    {
        var graph = new MapGraph();
        graph.AddNode(new MapNode("start", MapNodeType.Start, 0, true));
        graph.AddNode(new MapNode("boss", MapNodeType.Boss, 1, true));
        graph.AddEdge("start", "boss");
        return graph;
    }

    void SetBranchLimits(int min, int max)
    {
        SerializedObject serialized = new SerializedObject(generationProfile);
        serialized.FindProperty("minBranchCount").intValue = min;
        serialized.FindProperty("maxBranchCount").intValue = max;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
