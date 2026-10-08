using UnityEngine;

[System.Serializable]
public sealed class SkillDefensiveBlockSettings
{
    [Sirenix.OdinInspector.LabelText("Can Be Blocked")]
    public bool enabled = true;
    public DefensiveBlockMode mode = DefensiveBlockMode.Contact;
    [Sirenix.OdinInspector.ShowIf(nameof(UsesTimedApproach))]
    [Tooltip("Time from accepted Block to impact, using the attacker's actor clock.")]
    [Min(0.01f)] public float timedApproachSeconds = 0.22f;
    public bool UsesTimedApproach => mode == DefensiveBlockMode.TimedApproach || mode == DefensiveBlockMode.Cooperative;
    [Sirenix.OdinInspector.ShowIf(nameof(UsesCooperative))]
    [Sirenix.OdinInspector.InfoBox("Cooperative Block always ends the entire request. Continue Skill windows are invalid.",
        Sirenix.OdinInspector.InfoMessageType.Info, VisibleIf = nameof(UsesCooperative))]
    [Sirenix.OdinInspector.ValidateInput(nameof(CooperativeConfigurationValid), "Invalid Cooperative settings or Continue Skill window.")]
    public CooperativeBlockSettings cooperative = new CooperativeBlockSettings();
    bool CooperativeConfigurationValid => !UsesCooperative || (cooperative != null && cooperative.IsValid &&
        (!HasMultipleWindowData || System.Array.TrueForAll(windows, w => w != null && w.onSuccess == DefensiveBlockOutcome.InterruptSkill)));
    public bool UsesCooperative => mode == DefensiveBlockMode.Cooperative;
    public float ApproachDuration => UsesTimedApproach ? timedApproachSeconds : 0f;
    public SkillDefensiveBlockSettings Copy() => JsonUtility.FromJson<SkillDefensiveBlockSettings>(JsonUtility.ToJson(this));
    [Min(0f)] public float commandRange = 8f;
    public LayerMask worldLayers = 1;
    [Tooltip("Preparation before the skill clip advances or releases payload. Uses the caster's time domain; zero disables it.")]
    [Min(0f)] public float windupSeconds;
    [Range(0f, 1f)] public float windowStartNormalized;
    [Range(0f, 1f)] public float windowEndNormalized = 0.62f;
    public int[] hitboxSteps = { 0, 1 };
    [Tooltip("Optional ordered, non-overlapping windows. Empty uses the legacy single window above. Each hitbox step belongs to only one window.")]
    public DefensiveBlockWindow[] windows = System.Array.Empty<DefensiveBlockWindow>();
    public bool HasMultipleWindowData => windows != null && windows.Length > 0;
    [Sirenix.OdinInspector.ShowIf(nameof(UsesTimedApproach))]
    [Tooltip("Attacker root distance in front of the guard at timed impact. Paths that require moving backwards are rejected.")]
    [Min(0.1f)] public float approachStandOff = 1.6f;
    [Header("Threat prediction (before the hitbox is active)")]
    [Min(0.01f)] public float threatHalfWidth = 1.5f;
    [Min(0f)] public float threatForwardReach = 2.3f;
    [Min(0.01f)] public float estimatedChargeSpeed = 8f;
    [Min(0f)] public float knockbackDistance = 2f;
    [Min(0.01f)] public float knockbackSeconds = 0.4f;
    [Header("Defender Recoil (Player and Ally)")]
    [Tooltip("Distance both defenders slide back on a successful Block. Zero disables recoil movement.")]
    [Min(0f)] public float slideDistance = 1.5f;
    [Tooltip("Duration of defender recoil, also used as the minimum Block impact animation duration.")]
    [Min(0.01f)] public float slideSeconds = 0.35f;
    public int FindWindow(float time)
    {
        if (!HasMultipleWindowData) return time >= windowStartNormalized && time <= windowEndNormalized ? 0 : -1;
        for (int i = 0; i < windows.Length; i++) if (windows[i] != null && windows[i].Contains(time)) return i;
        return -1;
    }
    public int FindStepWindow(int step)
    {
        if (!HasMultipleWindowData) return 0; // Preserve request-wide late-hit rejection for legacy profiles.
        for (int i = 0; i < windows.Length; i++) if (windows[i] != null && windows[i].AllowsStep(step)) return i;
        return -1;
    }
    public bool AllowsStep(int step) => HasMultipleWindowData ? FindStepWindow(step) >= 0 :
        step >= 0 && hitboxSteps != null && System.Array.IndexOf(hitboxSteps, step) >= 0;
    public bool AllowsStep(int window, int step) => window >= 0 && (HasMultipleWindowData
        ? window < windows.Length && windows[window] != null && windows[window].AllowsStep(step) : AllowsStep(step));
    public float WindowEnd(int window) => HasMultipleWindowData && window >= 0 && window < windows.Length
        ? windows[window].endNormalized : windowEndNormalized;
    public bool TryGetImpact(int window, out float normalizedTime)
    {
        normalizedTime = 0f;
        if (!UsesTimedApproach || !HasMultipleWindowData || window < 0 || window >= windows.Length ||
            windows[window] == null || !windows[window].useImpactMarker) return false;
        normalizedTime = windows[window].impactNormalized;
        return true;
    }
    public DefensiveBlockOutcome Outcome(int window) => HasMultipleWindowData && window >= 0 && window < windows.Length
        ? windows[window].onSuccess : DefensiveBlockOutcome.InterruptSkill;
    public int[] Steps(int window) => HasMultipleWindowData && window >= 0 && window < windows.Length
        ? windows[window].hitboxSteps : hitboxSteps;
    public bool WindowsAreValid
    {
        get
        {
            if (!HasMultipleWindowData) return windowStartNormalized >= 0f && windowEndNormalized <= 1f &&
                windowEndNormalized >= windowStartNormalized && hitboxSteps != null && hitboxSteps.Length > 0 &&
                System.Array.TrueForAll(hitboxSteps, step => step >= 0);
            for (int i = 0; i < windows.Length; i++)
            {
                var window = windows[i];
                if (window == null || !window.IsValid || (i > 0 && window.startNormalized <= windows[i - 1].endNormalized)) return false;
                if (window.useImpactMarker && (!UsesTimedApproach ||
                    (i + 1 < windows.Length && (windows[i + 1] == null || window.impactNormalized >= windows[i + 1].startNormalized)))) return false;
                foreach (int step in window.hitboxSteps)
                    for (int j = 0; j < i; j++) if (windows[j].AllowsStep(step)) return false;
            }
            return true;
        }
    }
    public bool IsConfigured => enabled && System.Enum.IsDefined(typeof(DefensiveBlockMode), mode) &&
        (!UsesCooperative || (cooperative != null && cooperative.IsValid &&
            (!HasMultipleWindowData || System.Array.TrueForAll(windows, w => w != null && w.onSuccess == DefensiveBlockOutcome.InterruptSkill)))) &&
        (!UsesTimedApproach || (timedApproachSeconds > 0f && !float.IsInfinity(timedApproachSeconds) &&
            approachStandOff > 0f && !float.IsInfinity(approachStandOff))) &&
        commandRange > 0f && windupSeconds >= 0f && WindowsAreValid &&
        knockbackDistance >= 0f && knockbackSeconds > 0f &&
        slideDistance >= 0f && !float.IsInfinity(slideDistance) &&
        slideSeconds > 0f && !float.IsInfinity(slideSeconds) &&
        threatHalfWidth > 0f && threatForwardReach >= 0f && estimatedChargeSpeed > 0f;
}
