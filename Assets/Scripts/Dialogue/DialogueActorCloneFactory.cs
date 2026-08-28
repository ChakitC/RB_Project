using System.Collections.Generic;
using Animancer;
using UnityEngine;

/// <summary>
/// Builds the presentation-only clone of a live character for the dialogue stage.
///
/// The clone is a copy of the character's <see cref="CharacterVisualController.ModelRoot"/>, which is
/// where the live model, the equipped weapon mounts, and any active form override already live — so
/// the actor on stage always reflects the player's real appearance and equipment without the
/// dialogue system knowing anything about equipment.
///
/// Everything that is not part of drawing or animating the model is destroyed: the clone carries no
/// <see cref="CharacteContext"/>, AI, collider, rigidbody, agent, VFX, or combat state. Cloning
/// happens under an inactive staging root so no stripped component's Awake ever runs.
///
/// "Part of drawing" is wider than renderers alone — a shader that is fed by a script needs that
/// script to survive, or the clone renders with values computed for wherever the original character
/// is standing. See <see cref="IsPresentationComponent"/>.
/// </summary>
public static class DialogueActorCloneFactory
{
    static readonly List<Component> ComponentBuffer = new();

    /// <summary>
    /// Clones <paramref name="source"/>'s visual under <paramref name="stagingRoot"/> (which must be
    /// inactive) and returns the stripped actor. Returns null when the source has no visual to clone;
    /// the caller leaves that slot empty rather than failing the sequence.
    /// </summary>
    public static DialogueActorVisual TryCreateClone(
        DialogueCastSource source,
        string castKey,
        DialogueSlot slot,
        CharacterDialogueAnimationProfileSO profile,
        Transform stagingRoot)
    {
        if (!source.IsValid || stagingRoot == null)
            return null;

        Transform modelRoot = source.ModelRoot;
        Object logContext = source.Context != null ? (Object)source.Context : modelRoot;

        GameObject clone = Object.Instantiate(modelRoot.gameObject, stagingRoot, false);
        clone.name = $"DialogueActor_{(string.IsNullOrWhiteSpace(castKey) ? modelRoot.name : castKey)}";
        clone.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        clone.transform.localScale = modelRoot.localScale;

        // A scene NPC is cloned from its own root, so the clone arrives carrying that object's
        // collider, interactable link and trigger. The whitelist strip below removes all of it, the
        // same way it removes a party member's gameplay stack.
        StripGameplayComponents(clone);
        ApplyDialogueLayers(clone);

        Animator animator = clone.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            Debug.LogWarning(
                $"[Dialogue] Clone of '{modelRoot.name}' has no Animator; its dialogue slot stays empty.",
                logContext);
            Object.Destroy(clone);
            return null;
        }

        // The world is frozen at timeScale 0 while dialogue plays, so the poses have to run off the
        // unscaled clock, and they have to keep running while the portrait camera culls them.
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;

        var animancer = animator.gameObject.AddComponent<AnimancerComponent>();
        animancer.Animator = animator;
        animancer.UpdateMode = AnimatorUpdateMode.UnscaledTime;

        var actor = clone.AddComponent<DialogueActorVisual>();
        actor.Bind(castKey, source.CharacterId, slot, animancer, profile);
        return actor;
    }

    /// <summary>
    /// Destroys every component that is not part of drawing or animating the model. A whitelist is
    /// used deliberately: a blacklist would silently let a newly added gameplay component ride along
    /// into the dialogue stage.
    /// </summary>
    static void StripGameplayComponents(GameObject clone)
    {
        var transforms = clone.GetComponentsInChildren<Transform>(true);

        // Scripts first, then the built-ins: a Rigidbody or Animator cannot be removed while a
        // MonoBehaviour still declares [RequireComponent] on it.
        //
        // Scripts also depend on each other — an NPC clone arrives with DialogueTrigger, which
        // requires InteractableLink — so the script pass repeats until a full sweep removes nothing.
        // A single ordered pass leaves whichever component was visited before its dependent behind,
        // which is exactly how an InteractableLink used to survive onto the stage.
        int removedThisSweep;
        int guard = 0;
        do
        {
            removedThisSweep = StripSweep(transforms, scriptsOnly: true);
        }
        while (removedThisSweep > 0 && ++guard < 8);

        StripSweep(transforms, scriptsOnly: false);
        ComponentBuffer.Clear();
    }

    /// <summary>One removal sweep. Returns how many components it managed to destroy.</summary>
    static int StripSweep(Transform[] transforms, bool scriptsOnly)
    {
        int removed = 0;

        for (int t = 0; t < transforms.Length; t++)
        {
            Transform node = transforms[t];
            if (node == null)
                continue;

            ComponentBuffer.Clear();
            node.GetComponents(ComponentBuffer);
            for (int c = 0; c < ComponentBuffer.Count; c++)
            {
                Component component = ComponentBuffer[c];
                if (component == null || IsPresentationComponent(component))
                    continue;

                if (scriptsOnly != component is MonoBehaviour)
                    continue;

                // Unity refuses (and logs) when something still depends on this component. Asking
                // first keeps that out of the console; the next sweep picks it up once its dependent
                // is gone.
                if (!CanDestroy(component))
                    continue;

                Object.DestroyImmediate(component);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>True when no remaining component on the same GameObject requires this one.</summary>
    static bool CanDestroy(Component component)
    {
        System.Type type = component.GetType();

        ComponentBuffer.Clear();
        component.gameObject.GetComponents(ComponentBuffer);
        for (int i = 0; i < ComponentBuffer.Count; i++)
        {
            Component other = ComponentBuffer[i];
            if (other == null || other == component)
                continue;

            object[] requirements = other.GetType()
                .GetCustomAttributes(typeof(RequireComponent), inherit: true);

            for (int r = 0; r < requirements.Length; r++)
            {
                var requirement = (RequireComponent)requirements[r];
                if (Requires(requirement, type))
                    return false;
            }
        }

        return true;
    }

    static bool Requires(RequireComponent requirement, System.Type type)
    {
        return (requirement.m_Type0 != null && requirement.m_Type0.IsAssignableFrom(type))
            || (requirement.m_Type1 != null && requirement.m_Type1.IsAssignableFrom(type))
            || (requirement.m_Type2 != null && requirement.m_Type2.IsAssignableFrom(type));
    }

    static bool IsPresentationComponent(Component component)
    {
        return component is Transform
            || component is Animator
            || component is SkinnedMeshRenderer
            || component is MeshRenderer
            || component is MeshFilter

            // ASP's character panel is presentation, not gameplay: every frame it feeds the toon
            // shader the values it cannot derive on its own — face direction taken from the head
            // bone, and the character's world centre, which drives the character-shadow AABB.
            //
            // Stripping it does not reset those values, it freezes them: the clone's materials keep
            // whatever the live character last wrote, which is a position out in the gameplay world
            // thousands of units from the stage, so the shading is computed for somewhere the actor
            // is not. Measured on a live conversation, the Roma clone stood at (0, -5000, 0) while
            // its materials still read _CharacterCenterWS = (-0.5, 0.1, -1.6).
            //
            // Keeping it is safe on both sides. Update() is driven by the engine frame, not by
            // timeScale, so it keeps working while the world is frozen at 0; and its Start() calls
            // SetupMaterialID, which goes through `renderer.materials` and therefore gives the clone
            // its own material instances — it cannot write back into the live character.
            || component is ASP.ASPCharacterPanel;
    }

    /// <summary>
    /// Moves the clone onto the dialogue rendering channels.
    ///
    /// The Unity layer is 0 — the same layer as everything else — because ASP's layer-filtered
    /// renderer features only draw layer 0, and a clone on a layer of its own renders without the
    /// mesh outline and depth-offset shadow the character has in gameplay. Keeping the clone out of
    /// the gameplay view is left to the distance the stage sits at. The rendering layer mask is what
    /// does the real separation: it claims the dialogue light channel and ASP's feature channels,
    /// and pointedly not the channel the world's sun uses. See <see cref="DialogueLayers"/>.
    /// </summary>
    static void ApplyDialogueLayers(GameObject clone)
    {
        int layer = DialogueLayers.ActorLayer;
        var transforms = clone.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] == null)
                continue;

            if (layer >= 0)
                transforms[i].gameObject.layer = layer;
        }

        var renderers = clone.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.renderingLayerMask = DialogueLayers.ActorRenderingLayerMask;

            // Skinned bounds are only recomputed while a renderer is on screen, so a character that
            // was cloned from a hidden source — the ally helper is kept switched off between
            // summons — carries stale bind-time bounds that sit nowhere near its posed body, and the
            // whole actor gets frustum-culled off the stage. Three actors at a time, only while a
            // conversation is open, is a cheap place to pay for always-correct bounds.
            if (renderer is SkinnedMeshRenderer skinned)
                skinned.updateWhenOffscreen = true;
        }
    }
}
