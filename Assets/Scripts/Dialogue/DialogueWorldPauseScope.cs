using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Freezes the gameplay world for the length of one conversation and puts it back exactly as it was.
///
/// Unlike <c>StageIntroActorScope</c>, no real actor is moved, re-posed, or re-parented: the world is
/// stopped where it stands and the conversation is presented on top of it. This scope owns the world
/// pause token, the party's control blocks, the HUD, and world-space UI rendering. Everything it
/// takes is token- or snapshot-based, so <see cref="Restore"/> is safe to call twice and safe to call
/// from an abort path.
/// </summary>
internal sealed class DialogueWorldPauseScope
{
    const ControlBlockFlags DialogueControlBlocks =
        ControlBlockFlags.Move |
        ControlBlockFlags.Shoot |
        ControlBlockFlags.Skill |
        ControlBlockFlags.Rotate;

    readonly Dictionary<StateHub, int> controlTokens = new();
    readonly Dictionary<Camera, int> savedWorldUiMasks = new();

    int pauseToken;
    bool applied;
    bool hudHidden;

    public bool IsApplied => applied;

    public void Apply(IReadOnlyList<CharacteContext> partyContexts)
    {
        if (applied)
            return;

        applied = true;

        // Time.timeScale 0. Dialogue UI, voice, typewriter, and the stage animations all run on the
        // unscaled clock, so they keep going while everything else stops.
        pauseToken = GlobalTimeScaleManager.Instance.AcquirePauseToken();

        for (int i = 0; partyContexts != null && i < partyContexts.Count; i++)
        {
            CharacteContext ctx = partyContexts[i];
            if (ctx == null)
                continue;

            ctx.ResolveReferences();
            StateHub stateHub = ctx.stateHub;
            if (stateHub == null || controlTokens.ContainsKey(stateHub))
                continue;

            controlTokens[stateHub] = stateHub.AcquireExternalControlBlockToken(DialogueControlBlocks);
        }

        UIManager.Instance?.SetHudVisible(false);
        hudHidden = true;

        SuppressWorldUiRendering();
    }

    public void Restore()
    {
        if (!applied)
            return;

        applied = false;

        RestoreWorldUiRendering();

        if (hudHidden)
        {
            UIManager.Instance?.SetHudVisible(true);
            hudHidden = false;
        }

        foreach (KeyValuePair<StateHub, int> pair in controlTokens)
        {
            if (pair.Key != null)
                pair.Key.ReleaseExternalControlBlockToken(pair.Value);
        }

        controlTokens.Clear();

        // Released last: input stays blocked until the world is genuinely handed back.
        if (pauseToken != 0)
        {
            GlobalTimeScaleManager.Instance.ReleasePauseToken(pauseToken);
            pauseToken = 0;
        }
    }

    /// <summary>
    /// World-space UI (overhead health bars, interaction prompts) is not reached by
    /// <see cref="UIManager.SetHudVisible"/>, so it is hidden by taking the WorldUI layer out of
    /// every camera's culling mask and restoring the exact masks afterwards.
    /// </summary>
    void SuppressWorldUiRendering()
    {
        savedWorldUiMasks.Clear();

        int worldUiLayer = LayerMask.NameToLayer("WorldUI");
        if (worldUiLayer < 0)
            return;

        int worldUiMask = 1 << worldUiLayer;
        Camera[] cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || (camera.cullingMask & worldUiMask) == 0)
                continue;

            savedWorldUiMasks[camera] = camera.cullingMask;
            camera.cullingMask &= ~worldUiMask;
        }
    }

    void RestoreWorldUiRendering()
    {
        foreach (KeyValuePair<Camera, int> pair in savedWorldUiMasks)
        {
            if (pair.Key != null)
                pair.Key.cullingMask = pair.Value;
        }

        savedWorldUiMasks.Clear();
    }
}
