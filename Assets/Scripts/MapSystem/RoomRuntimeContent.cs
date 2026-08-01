using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoomRuntimeContent : MonoBehaviour
{
    private const string RuntimeRootName = "RuntimeContent";
    private const string PersistentRootName = "Persistent";
    private const string EncounterRootName = "Encounter";
    private const string TemporaryRootName = "Temporary";

    private Transform runtimeRoot;
    private Transform persistentRoot;
    private Transform encounterRoot;
    private Transform temporaryRoot;

    public Transform PersistentRoot => EnsureChild(ref persistentRoot, PersistentRootName);
    public Transform EncounterRoot => EnsureChild(ref encounterRoot, EncounterRootName);
    public Transform TemporaryRoot => EnsureChild(ref temporaryRoot, TemporaryRootName);

    public void EnsureRoots()
    {
        _ = PersistentRoot;
        _ = EncounterRoot;
        _ = TemporaryRoot;
    }

    public void ClearEncounterContent()
    {
        ClearChildren(EncounterRoot);
    }

    public void ClearTemporaryContent()
    {
        ClearChildren(TemporaryRoot);
    }

    Transform EnsureChild(ref Transform cachedRoot, string childName)
    {
        if (cachedRoot != null)
            return cachedRoot;

        if (runtimeRoot == null)
            runtimeRoot = FindOrCreateChild(transform, RuntimeRootName);

        cachedRoot = FindOrCreateChild(runtimeRoot, childName);
        return cachedRoot;
    }

    static Transform FindOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            return child;

        var childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    static void ClearChildren(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }
}
