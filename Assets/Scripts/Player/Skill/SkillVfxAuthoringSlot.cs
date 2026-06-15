using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[HideMonoScript]
public sealed class SkillVfxAuthoringSlot : MonoBehaviour
{
    [SerializeField, Min(0), LabelText("VFX Cue Index")]
    private int cueIndex;

    [SerializeField, AssetsOnly, LabelText("VFX Prefabs To Add")]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    private List<GameObject> prefabsToAdd = new List<GameObject>();

    [ShowInInspector, ReadOnly, LabelText("VFX Entry Count")]
    private int EntryCount => GetComponentsInChildren<SkillVfxAuthoringEntry>(true).Length;

    public int CueIndex => cueIndex;

    public void Configure(int value)
    {
        cueIndex = Mathf.Max(0, value);
        RefreshName();
    }

    public void SetCueIndex(int value)
    {
        cueIndex = Mathf.Max(0, value);
        RefreshName();
    }

    [Button("Add VFX Prefabs", ButtonSizes.Large)]
    private void AddVfxPrefabs()
    {
        SetAnimationVfxData authoring = ResolveAuthoring();
        if (authoring == null)
        {
            Debug.LogWarning("Could not find SetAnimationVfxData for this VFX slot.", this);
            return;
        }

        authoring.AddPrefabsToSlot(this, prefabsToAdd);
        prefabsToAdd.Clear();
    }

    [Button("Add Empty VFX Entry")]
    [PropertyTooltip("Create an entry without a prefab. Use this for Stop Loop or manual setup.")]
    private void AddEmptyVfxEntry()
    {
        SetAnimationVfxData authoring = ResolveAuthoring();
        if (authoring != null)
            authoring.AddEmptyEntryToSlot(this);
    }

    [Button("Play Slot Preview")]
    private void PlaySlotPreview()
    {
        SetAnimationVfxData authoring = ResolveAuthoring();
        if (authoring != null)
            authoring.PlayVfx(cueIndex);
    }

    [Button("Stop Slot Preview")]
    private void StopSlotPreview()
    {
        SetAnimationVfxData authoring = ResolveAuthoring();
        if (authoring != null)
            authoring.StopVfx(cueIndex);
    }

    void RefreshName()
    {
        gameObject.name = $"Vfx_Slot_{cueIndex + 1}";
    }

    SetAnimationVfxData ResolveAuthoring()
    {
        SetAnimationVfxData authoring = GetComponentInParent<SetAnimationVfxData>();
        if (authoring != null)
            return authoring;

#if UNITY_EDITOR
        SetAnimationVfxData[] candidates = FindObjectsByType<SetAnimationVfxData>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] != null && candidates[i].ContainsAuthoringTransform(transform))
                return candidates[i];
        }
#endif

        return null;
    }
}
