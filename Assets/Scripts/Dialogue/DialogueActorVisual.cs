using System;
using Animancer;
using UnityEngine;

/// <summary>
/// A presentation-only clone of a live character standing in one dialogue slot. It owns nothing but
/// renderers, an <see cref="Animator"/>, and an <see cref="AnimancerComponent"/>: the factory strips
/// every gameplay component, so there is no context, AI, collider, physics, or combat state here.
/// Animation runs unscaled because the world is frozen while dialogue plays.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueActorVisual : MonoBehaviour
{
    static readonly string[] HeadBoneNames = { "head.x", "head", "neck.x" };

    AnimancerComponent animancer;
    Transform headBone;
    bool headBoneResolved;
    ClipTransition idlePose;
    ClipTransition currentPose;

    /// <summary>The key the sequence casts this actor under — a character id, or a `role.` key.</summary>
    public string CastKey { get; private set; }

    /// <summary>The actual character standing here, whichever key reached it.</summary>
    public string CharacterId { get; private set; }
    public DialogueSlot Slot { get; private set; }
    public CharacterDialogueAnimationProfileSO Profile { get; private set; }

    internal void Bind(
        string castKey,
        string characterId,
        DialogueSlot slot,
        AnimancerComponent animancerComponent,
        CharacterDialogueAnimationProfileSO profile)
    {
        CastKey = castKey;
        CharacterId = characterId;
        Slot = slot;
        animancer = animancerComponent;
        Profile = profile;
    }

    /// <summary>
    /// Resolves the pose the actor holds when it is not speaking. Called once per session so a
    /// speaker change never has to re-resolve it.
    /// </summary>
    internal void SetIdlePose(string idlePoseId)
    {
        idlePose = ResolvePose(idlePoseId);

        // The pose fallback ends at the profile's own idlePose — there is no project-wide default,
        // because these avatars are generic and a clip from another rig would not retarget, it would
        // fold the model over. So an empty idlePose means nothing plays at all and the character
        // stands in its imported bind pose. That reads as a T-pose on screen and is otherwise silent,
        // so say it out loud once per actor.
        if (idlePose == null || !idlePose.IsValid)
        {
            Debug.LogWarning(
                $"[Dialogue] '{CharacterId}' has no usable idle pose" +
                (Profile != null ? $" in '{Profile.name}'" : " (no dialogue pose profile)") +
                " — it will stand in its bind pose. Assign an idle clip authored on this " +
                "character's own rig.", this);
        }
    }

    public void PlayIdlePose()
    {
        PlayPose(idlePose);
    }

    /// <summary>Plays a pose by id, falling back to the profile's idle pose when it is unmapped.</summary>
    public void PlayPoseId(string poseId)
    {
        ClipTransition pose = ResolvePose(poseId);
        PlayPose(pose != null && pose.IsValid ? pose : idlePose);
    }

    void PlayPose(ClipTransition pose)
    {
        if (animancer == null || pose == null || !pose.IsValid)
            return;

        // Re-playing the pose already held would restart it mid-conversation for no visible reason.
        if (ReferenceEquals(pose, currentPose))
            return;

        currentPose = pose;
        animancer.Play(pose);
    }

    /// <summary>
    /// Puts the current pose on the bones immediately, with no fade still running, and samples it.
    ///
    /// Framing measures the head bone and the camera is fitted only once per actor, so sampling
    /// while a crossfade is in flight measures a head somewhere between the bind pose and the real
    /// one — and the character then sits off-centre for the entire conversation with nothing to
    /// correct it. Snapping costs nothing where this is used: the actor has only just appeared, so
    /// there is no visible transition to preserve.
    /// </summary>
    internal void EvaluatePose()
    {
        if (animancer == null)
            return;

        if (currentPose != null && currentPose.IsValid)
            animancer.Play(currentPose, 0f, FadeMode.FixedDuration);

        animancer.Evaluate();
    }

    /// <summary>
    /// The head bone, used as the framing anchor. Every character rig in this project shares the same
    /// bone names (`c_traj` / `neck.x` / `head.x`), which is what makes anatomical framing possible
    /// at all — silhouette bounds cannot be used, because hats and ears add a metre above the head
    /// and by wildly different amounts per character.
    /// </summary>
    internal bool TryGetHeadPosition(out Vector3 position)
    {
        if (!headBoneResolved)
        {
            headBoneResolved = true;
            var bones = GetComponentsInChildren<Transform>(true);

            for (int n = 0; n < HeadBoneNames.Length && headBone == null; n++)
            {
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] != null &&
                        string.Equals(bones[i].name, HeadBoneNames[n], StringComparison.OrdinalIgnoreCase))
                    {
                        headBone = bones[i];
                        break;
                    }
                }
            }

            if (headBone == null)
            {
                Debug.LogWarning(
                    $"[Dialogue] '{CharacterId}' has no head bone (looked for " +
                    $"{string.Join(", ", HeadBoneNames)}). Its portrait falls back to silhouette " +
                    "framing, so its face will not line up with the other actors.", this);
            }
        }

        if (headBone == null)
        {
            position = default;
            return false;
        }

        position = headBone.position;
        return true;
    }

    /// <summary>World-space bounds of everything this actor draws, or false when it draws nothing.</summary>
    internal bool TryGetWorldBounds(out Bounds bounds)
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        bounds = default;

        bool any = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return any;
    }

    /// <summary>
    /// Whether a line naming <paramref name="key"/> means this actor. Both the cast key and the real
    /// character id match, so a sequence that casts by role but names a speaker by character id — or
    /// the other way round — still lands on the right actor instead of silently emphasising nobody.
    /// </summary>
    public bool Matches(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return string.Equals(CastKey, key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(CharacterId, key, StringComparison.OrdinalIgnoreCase);
    }

    ClipTransition ResolvePose(string poseId)
    {
        if (Profile == null)
            return null;

        return Profile.TryGetPose(poseId, out ClipTransition clip) ? clip : null;
    }
}
