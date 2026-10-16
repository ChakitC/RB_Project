using Animancer;
using UnityEngine;

/// <summary>Animation ownership for Basement models, independent of combat state.</summary>
[DisallowMultipleComponent]
public sealed class BasementAnimationController : MonoBehaviour
{
    [SerializeField] private AnimancerComponent animancer;
    [SerializeField] private BasementAnimationProfileSO profile;

    AnimancerState currentState;
    bool picked;
    bool warned;
    bool inMenu;
    bool inCharacterMenu;
    bool menuChangedAnimation;
    bool selectionChangedInMenu;
    AnimatorUpdateMode savedUpdateMode;

    public Animator Animator => animancer != null ? animancer.Animator : null;
    public bool IsPicked => picked;

    void OnEnable()
    {
        // The shared presentation is enabled before its character is bound.
        if (profile != null)
            ResetToIdle();
    }

    public void Bind(Animator target, BasementAnimationProfileSO animationProfile)
        => Bind(target, animationProfile, false);

    public void Bind(Animator target, BasementAnimationProfileSO animationProfile, bool startPicked)
    {
        Unbind();
        profile = animationProfile;
        if (animancer != null)
            animancer.Animator = target;
        warned = false;
        picked = startPicked;
        Play(picked, true);
    }

    public void Unbind()
    {
        OnDisable();
        if (animancer != null)
        {
            // Destroy the old output before destroying or replacing its skeleton.
            if (animancer.IsGraphInitialized)
                animancer.Graph.Destroy();
            animancer.Animator = null;
        }
        profile = null;
    }

    void OnDisable()
    {
        RestoreUpdateMode();
        currentState = null;
        picked = false;
        inCharacterMenu = false;
    }

    public void ResetToIdle()
    {
        picked = false;
        if (inMenu) { selectionChangedInMenu = true; return; }
        Play(false, true);
    }

    public void SetPicked(bool value)
    {
        if (inMenu || (inCharacterMenu && profile?.inMenuIdle?.Clip != null))
        {
            selectionChangedInMenu |= picked != value;
            picked = value;
            return;
        }
        if (picked == value && currentState != null)
            return;

        picked = value;
        Play(value, false);
    }

    public void SetInCharacterMenu(bool value)
    {
        if (inCharacterMenu == value) return;
        inCharacterMenu = value;
        if (inMenu) { selectionChangedInMenu = true; return; }
        Play(picked, false);
    }

    public void EnterMenu(BasementAnimationProfileSO.MenuEntry entry)
    {
        if (inMenu || animancer == null || Animator == null) return;
        savedUpdateMode = animancer.UpdateMode;
        animancer.UpdateMode = AnimatorUpdateMode.UnscaledTime;
        inMenu = true;
        selectionChangedInMenu = false;
        menuChangedAnimation = entry?.animation?.Clip != null;
        if (menuChangedAnimation) currentState = animancer.Play(entry.animation);
    }

    public void ExitMenu()
    {
        if (!inMenu) return;
        bool restorePose = menuChangedAnimation || selectionChangedInMenu;
        RestoreUpdateMode();
        if (restorePose) Play(picked, false);
    }

    public void SwitchMenu(BasementAnimationProfileSO.MenuEntry entry)
    {
        if (!inMenu)
        {
            EnterMenu(entry);
            return;
        }
        // Keep the session's update mode and selection state. Missing clips preserve the current pose.
        if (animancer == null || Animator == null || entry?.animation?.Clip == null) return;
        menuChangedAnimation = true;
        currentState = animancer.Play(entry.animation);
    }

    void RestoreUpdateMode()
    {
        if (inMenu && animancer != null) animancer.UpdateMode = savedUpdateMode;
        inMenu = menuChangedAnimation = selectionChangedInMenu = false;
    }

    void Play(bool value, bool reset)
    {
        var transition = profile != null ? (value ? profile.pickup : profile.idle) : null;
        if (inCharacterMenu && profile?.inMenuIdle?.Clip != null)
            transition = profile.inMenuIdle;
        if (animancer == null || Animator == null || transition == null || transition.Clip == null)
        {
            if (!warned)
            {
                Debug.LogWarning("[Basement Animation] Assign Animancer, Animator and both profile clips.", this);
                warned = true;
            }
            return;
        }

        // Transitions own fade, speed and start time. Non-looping clips naturally
        // hold their final pose; no end callback or speed override is needed.
        currentState = reset ? animancer.Play(transition, 0f) : animancer.Play(transition);

        if (reset)
            animancer.Evaluate(0f);
    }

    /// <summary>Only resolve within this preview. Ambiguous models need an explicit binding.</summary>
    public static BasementAnimationController Resolve(GameObject root)
    {
        if (root == null)
            return null;

        var direct = root.GetComponent<BasementAnimationController>();
        if (direct != null)
            return direct;

        BasementAnimationController result = null;
        foreach (var candidate in root.GetComponentsInChildren<BasementAnimationController>(true))
        {
            // Ignore outgoing previews waiting for Destroy at the end of the frame.
            bool active = true;
            for (var t = candidate.transform; t != root.transform; t = t.parent)
                active &= t.gameObject.activeSelf;
            if (!active)
                continue;
            if (result != null)
            {
                Debug.LogWarning("[Basement Animation] Multiple preview controllers; bind the model explicitly.", root);
                return null;
            }
            result = candidate;
        }
        return result;
    }
}
