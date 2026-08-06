using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AccessoryReforgeSettings", menuName = "Game/Accessories/Reforge Settings")]
public sealed class AccessoryReforgeSettings : ScriptableObject
{
    const string ResourcesPath = "GameSettings/AccessoryReforgeSettings";

    [Header("Global Modifier Pool")]
    [SerializeField] private List<AccessoryModifierDefinition> modifierPool = new();

    [Header("Reforge Cost")]
    [SerializeField, Min(1)] private int costDivisor = 3;
    [SerializeField, Min(1)] private int costRoundUpUnit = 5;

    static AccessoryReforgeSettings cachedInstance;
    static bool attemptedLoad;
    static bool loggedMissingSettings;

    public IReadOnlyList<AccessoryModifierDefinition> ModifierPool => modifierPool;

    public static AccessoryReforgeSettings Resolve()
    {
        if (!attemptedLoad)
        {
            cachedInstance = Resources.Load<AccessoryReforgeSettings>(ResourcesPath);
            attemptedLoad = true;
        }

        if (cachedInstance == null && !loggedMissingSettings)
        {
            Debug.LogWarning(
                $"[AccessoryReforgeSettings] Missing Resources/{ResourcesPath}.asset. " +
                $"Reforge and modifier-roll features will be unavailable.");
            loggedMissingSettings = true;
        }

        return cachedInstance;
    }

    public static AccessoryModifierDefinition FindModifier(string modifierId)
    {
        if (string.IsNullOrWhiteSpace(modifierId))
            return null;

        AccessoryReforgeSettings settings = Resolve();
        if (settings == null)
            return null;

        return settings.GetModifierById(modifierId);
    }

    public AccessoryModifierDefinition GetModifierById(string modifierId)
    {
        if (string.IsNullOrWhiteSpace(modifierId) || modifierPool == null)
            return null;

        for (int i = 0; i < modifierPool.Count; i++)
        {
            AccessoryModifierDefinition modifier = modifierPool[i];
            if (modifier == null)
                continue;

            if (string.Equals(modifier.RuntimeId, modifierId, StringComparison.Ordinal))
                return modifier;
        }

        return null;
    }

    public int CalculateReforgeCost(AccessoryDefinition accessory)
    {
        if (accessory == null || accessory.baseBuyPrice <= 0)
            return 0;

        float divided = accessory.baseBuyPrice / (float)Mathf.Max(1, costDivisor);
        int roundUpUnit = Mathf.Max(1, costRoundUpUnit);
        return Mathf.CeilToInt(divided / roundUpUnit) * roundUpUnit;
    }

    public void BuildCandidates(AccessoryDefinition accessory, string excludeModifierId, List<AccessoryModifierDefinition> buffer)
    {
        buffer.Clear();

        if (modifierPool == null)
            return;

        for (int i = 0; i < modifierPool.Count; i++)
        {
            AccessoryModifierDefinition modifier = modifierPool[i];
            if (modifier == null)
                continue;

            if (modifier.weight <= 0f)
                continue;

            if (!string.IsNullOrWhiteSpace(excludeModifierId) &&
                string.Equals(modifier.RuntimeId, excludeModifierId, StringComparison.Ordinal))
                continue;

            if (accessory != null && !modifier.CanRollOn(accessory))
                continue;

            buffer.Add(modifier);
        }
    }

    public AccessoryModifierDefinition RollFromCandidates(List<AccessoryModifierDefinition> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
            totalWeight += Mathf.Max(0f, candidates[i].weight);

        if (totalWeight <= 0f)
            return null;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += Mathf.Max(0f, candidates[i].weight);
            if (roll <= cumulative)
                return candidates[i];
        }

        return candidates[candidates.Count - 1];
    }

    void OnValidate()
    {
        costDivisor = Mathf.Max(1, costDivisor);
        costRoundUpUnit = Mathf.Max(1, costRoundUpUnit);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntimeCache()
    {
        cachedInstance = null;
        attemptedLoad = false;
        loggedMissingSettings = false;
    }
}
