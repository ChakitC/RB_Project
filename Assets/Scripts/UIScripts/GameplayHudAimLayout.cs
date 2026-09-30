using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameplayHudAimLayout : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float transitionDuration = 0.18f;
    [SerializeField] Vector2 vitalPosition = new Vector2(380f, 240f);

    RectTransform vital;
    Vector2 normalAnchorMin;
    Vector2 normalAnchorMax;
    Vector2 normalPosition;
    PlayerContext player;
    bool captured;
    float aimBlend;

    public void Bind(PlayerContext context)
    {
        player = context;
        if (!captured)
        {
            vital = transform.Find("PlayerHpStamina") as RectTransform;
            if (vital != null)
            {
                normalAnchorMin = vital.anchorMin;
                normalAnchorMax = vital.anchorMax;
                normalPosition = vital.anchoredPosition;
            }
            captured = true;
        }

        aimBlend = ShouldMoveStatus ? 1f : 0f;
        ApplyLayout();
    }

    bool ShouldMoveStatus
    {
        get
        {
            if (player == null) return false;
            if (player.WeaponSystem != null && player.WeaponSystem.IsAiming) return true;
            var block = player.interruptionCommand != null ? player.interruptionCommand.ActiveCooperativeBlock : null;
            return block != null && (block.State == CooperativeBlockState.Struggling ||
                block.State == CooperativeBlockState.Success);
        }
    }

    void LateUpdate() => UpdateLayout(Time.unscaledDeltaTime);

    void UpdateLayout(float deltaTime)
    {
        float target = ShouldMoveStatus ? 1f : 0f;
        if (aimBlend == target)
            return;

        aimBlend = Mathf.MoveTowards(aimBlend, target, deltaTime / Mathf.Max(0.01f, transitionDuration));
        ApplyLayout();
    }

    void OnDisable()
    {
        aimBlend = 0f;
        ApplyLayout();
    }

    void ApplyLayout()
    {
        if (vital == null)
            return;

        float t = Mathf.SmoothStep(0f, 1f, aimBlend);
        vital.anchorMin = Vector2.Lerp(normalAnchorMin, Vector2.zero, t);
        vital.anchorMax = Vector2.Lerp(normalAnchorMax, Vector2.zero, t);
        vital.anchoredPosition = Vector2.Lerp(normalPosition, vitalPosition, t);
    }
}
