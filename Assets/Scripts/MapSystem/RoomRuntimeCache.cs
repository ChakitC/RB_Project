using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// Owns every room instance a run has spawned. A visited node keeps its instance so revisiting it
/// costs nothing and its uncollected drops come back; only one instance is ever active, because two
/// live rooms would mean two overlapping NavMeshes.
/// </summary>
public sealed class RoomRuntimeCache
{
    public sealed class Entry
    {
        public readonly MapNode Node;
        public readonly GameObject Instance;
        public readonly RoomController Controller;

        public Entry(MapNode node, GameObject instance, RoomController controller)
        {
            Node = node;
            Instance = instance;
            Controller = controller;
        }

        public RoomRuntimeContent RuntimeContent => Controller != null ? Controller.RuntimeContent : null;

        public Transform PersistentRoot
        {
            get
            {
                RoomRuntimeContent content = RuntimeContent;
                return content != null ? content.PersistentRoot : null;
            }
        }
    }

    private readonly Dictionary<string, Entry> entries = new();
    private readonly Action<string> log;

    public RoomRuntimeCache(Action<string> log)
    {
        this.log = log;
    }

    public int Count => entries.Count;

    public Entry GetOrCreate(MapNode node, RoomDefinitionSO roomDefinition, Transform anchor, Transform parent)
    {
        if (entries.TryGetValue(node.Id, out Entry cached) && cached.Instance != null)
        {
            Log($"Reusing cached room for node '{node.Id}' (instance {cached.Instance.GetInstanceID()}).");
            return cached;
        }

        Quaternion roomRotation = anchor.rotation * Quaternion.Euler(0f, node.RoomYawDegrees, 0f);
        Log($"Creating room for node '{node.Id}' ({node.Type}) with '{roomDefinition.name}', yaw={node.RoomYawDegrees:0}.");

        GameObject instance = UnityEngine.Object.Instantiate(roomDefinition.RoomPrefab, anchor.position, roomRotation, parent);
        RoomController controller = instance.GetComponentInChildren<RoomController>(true);
        if (controller == null)
        {
            Debug.LogWarning(
                $"[MapRunController] Spawned room '{roomDefinition.RoomPrefab.name}' has no RoomController. Adding one at runtime.",
                instance);
            controller = instance.AddComponent<RoomController>();
        }

        var entry = new Entry(node, instance, controller);
        entries[node.Id] = entry;
        Log($"Cached room for node '{node.Id}' (instance {instance.GetInstanceID()}).");
        return entry;
    }

    public void Activate(Entry entry)
    {
        if (entry == null || entry.Instance == null)
            return;

        if (!entry.Instance.activeSelf)
            entry.Instance.SetActive(true);

        Physics.SyncTransforms();
    }

    /// <summary>
    /// Hides a room without touching its content. The NavMesh instance is dropped explicitly so two
    /// rooms occupying the same world space never overlap; re-activating the surface re-adds it.
    /// </summary>
    public void Deactivate(Entry entry)
    {
        if (entry == null || entry.Instance == null)
            return;

        RemoveNavMeshData(entry.Instance);
        entry.Instance.SetActive(false);
        Log($"Deactivated cached room for node '{entry.Node.Id}'.");
    }

    /// <summary>Drops the encounter and temporary content of a room the party has finished with.</summary>
    public void ClearTransientContent(Entry entry)
    {
        RoomRuntimeContent content = entry?.RuntimeContent;
        if (content == null)
            return;

        content.ClearTemporaryContent();
        content.ClearEncounterContent();
    }

    /// <summary>
    /// The root transform of every cached room, live or hidden. A room transition must not destroy
    /// anything underneath one of them: that content belongs to the room, not to the world.
    /// </summary>
    public IEnumerable<Transform> RoomRoots()
    {
        foreach (Entry entry in entries.Values)
        {
            if (entry != null && entry.Instance != null)
                yield return entry.Instance.transform;
        }
    }

    public void DestroyAll()
    {
        foreach (Entry entry in entries.Values)
        {
            if (entry == null || entry.Instance == null)
                continue;

            RemoveNavMeshData(entry.Instance);
            entry.Instance.SetActive(false);
            UnityEngine.Object.Destroy(entry.Instance);
        }

        entries.Clear();
    }

    static void RemoveNavMeshData(GameObject roomInstance)
    {
        if (roomInstance == null)
            return;

        NavMeshSurface[] surfaces = roomInstance.GetComponentsInChildren<NavMeshSurface>(true);
        for (int i = 0; i < surfaces.Length; i++)
        {
            if (surfaces[i] != null)
                surfaces[i].RemoveData();
        }
    }

    void Log(string message)
    {
        log?.Invoke(message);
    }
}
