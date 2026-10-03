using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameplayHudBlockVisibility : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float fadeSeconds = 0.18f;

    sealed class Panel
    {
        public CanvasGroup group;
        public float alpha;
        public bool interactable;
        public bool blocksRaycasts;
    }

    readonly List<Panel> panels = new List<Panel>(7);
    PlayerContext player;
    bool captured;
    float hiddenWeight;

    public void Bind(PlayerContext context)
    {
        player = context;
        if (!captured)
        {
            Capture("AmmoDisplay");
            Capture("FollowUpDisplay");
            Capture("MinimapPlaceholder");
            for (int i = 1; i <= 4; i++)
                Capture($"SkillReady Ally {i}");
            captured = true;
        }
        if (player == null)
        {
            hiddenWeight = 0f;
            ApplyVisibility();
        }
    }

    void LateUpdate() => UpdateVisibility(Time.unscaledDeltaTime);

    void UpdateVisibility(float deltaTime)
    {
        CooperativeBlockSession session = player != null && player.interruptionCommand != null
            ? player.interruptionCommand.ActiveCooperativeBlock : null;
        bool hide = session != null && session.IsActive &&
            (session.State == CooperativeBlockState.Struggling || session.State == CooperativeBlockState.Success);
        hiddenWeight = Mathf.MoveTowards(hiddenWeight, hide ? 1f : 0f, deltaTime / Mathf.Max(.01f, fadeSeconds));
        ApplyVisibility();
    }

    void OnDisable()
    {
        hiddenWeight = 0f;
        ApplyVisibility();
    }

    void Capture(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null)
            return;
        CanvasGroup group = child.GetComponent<CanvasGroup>();
        if (group == null)
            group = child.gameObject.AddComponent<CanvasGroup>();
        panels.Add(new Panel { group = group, alpha = group.alpha,
            interactable = group.interactable, blocksRaycasts = group.blocksRaycasts });
    }

    void ApplyVisibility()
    {
        float alpha = 1f - Mathf.SmoothStep(0f, 1f, hiddenWeight);
        foreach (Panel panel in panels)
        {
            if (panel.group == null)
                continue;
            panel.group.alpha = panel.alpha * alpha;
            panel.group.interactable = hiddenWeight == 0f && panel.interactable;
            panel.group.blocksRaycasts = hiddenWeight == 0f && panel.blocksRaycasts;
        }
    }
}
