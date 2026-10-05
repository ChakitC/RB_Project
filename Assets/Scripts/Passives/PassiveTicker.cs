using System.Collections.Generic;
using UnityEngine;

/// <summary>One scheduler for owners with timers. Empty/static passive controllers have no Update.</summary>
public sealed class PassiveTicker : MonoBehaviour
{
    static PassiveTicker instance;
    static readonly HashSet<PassiveController> owners = new();
    readonly List<PassiveController> snapshot = new();
    public static void SetRequired(PassiveController owner, bool required)
    {
        if (!Application.isPlaying) return;
        if (required)
        {
            owners.Add(owner);
            if (instance == null)
            {
                var root = new GameObject("Passive timers");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<PassiveTicker>();
            }
        }
        else owners.Remove(owner);
        if (instance != null) instance.enabled = owners.Count > 0;
    }
    void Update()
    {
        snapshot.Clear(); snapshot.AddRange(owners);
        foreach (var owner in snapshot)
            if (owner != null && owner.isActiveAndEnabled) owner.Tick(Time.deltaTime);
            else owners.Remove(owner);
        enabled = owners.Count > 0;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { owners.Clear(); instance = null; }
}
